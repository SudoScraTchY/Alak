using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Alak.Core.Model;
using Alak.Inference.Abstractions;
using Alak.Inference.Abstractions.Tasks;
using Microsoft.Maui.Storage;

namespace Alak.App.Services;

public sealed record InstalledModelInfo(
    string Name,
    string Path,
    long SizeBytes,
    string FormattedSize,
    string Quantization,
    bool IsActive);

public interface IModelService
{
    bool IsModelDownloaded { get; }
    bool IsModelLoaded { get; }
    string? ActiveModelPath { get; }
    Task<bool> EnsureModelLoadedAsync(CancellationToken ct = default);
    Task<bool> ImportModelAsync(FileResult file, IProgress<double>? progress = null, CancellationToken ct = default);
    Task<string?> ClassifyAsync(string title, string text, string package, CancellationToken ct = default);
    Task<string> BenchmarkAsync(CancellationToken ct = default);
    Task<T301ExplainResult?> ExplainNotificationAsync(string title, string text, string package, string category, CancellationToken ct = default);
    Task<T303DigestResult?> GenerateDigestAsync(IEnumerable<Notification> notifications, CancellationToken ct = default);
    T303DigestResult GenerateRulesDigest(IEnumerable<Notification> notifications);
    IAsyncEnumerable<string> GenerateDigestStreamAsync(IEnumerable<Notification> notifications, CancellationToken ct = default);
    IReadOnlyList<InstalledModelInfo> GetInstalledModels();
    Task SetActiveModelAsync(string path);
    Task DeleteModelAsync(string path);
}

/// <summary>
/// Implements §9.1–9.4 Zero-Network SLM Runtime with SAF model import,
/// GBNF grammar-constrained decoding, SLM-002 error handling, and Grounding Guard (SLM-003).
/// </summary>
public sealed class ModelService : IModelService
{
    private const uint GgufMagic = 0x46554747; // 'G' 'G' 'U' 'F' in little endian

    private readonly IInferenceEngine _engine;
    private readonly string _modelsDir;
    private bool _isLoaded;

    public bool IsModelDownloaded => !string.IsNullOrEmpty(ActiveModelPath) && File.Exists(ActiveModelPath) && new FileInfo(ActiveModelPath).Length > 10_000_000;
    public bool IsModelLoaded => _isLoaded;

    public string? ActiveModelPath
    {
        get
        {
            var saved = Preferences.Get("active_slm_model_path", string.Empty);
            if (!string.IsNullOrEmpty(saved) && File.Exists(saved))
            {
                return saved;
            }
            if (Directory.Exists(_modelsDir))
            {
                var firstGguf = Directory.GetFiles(_modelsDir, "*.gguf").FirstOrDefault();
                if (!string.IsNullOrEmpty(firstGguf)) return firstGguf;
            }
            return null;
        }
    }

    public ModelService(IInferenceEngine engine)
    {
        _engine = engine;
        _modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        Directory.CreateDirectory(_modelsDir);
    }

    public static bool IsArm64LseSupported()
    {
        try
        {
            if (File.Exists("/proc/cpuinfo"))
            {
                var text = File.ReadAllText("/proc/cpuinfo");
                if (text.Contains("Features", StringComparison.OrdinalIgnoreCase))
                {
                    return text.Contains("atomics", StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch { }
        return true;
    }

    public async Task<bool> EnsureModelLoadedAsync(CancellationToken ct = default)
    {
        if (_isLoaded) return true;
        if (!IsModelDownloaded || string.IsNullOrEmpty(ActiveModelPath)) return false;

        try
        {
            var modelPath = ActiveModelPath;
            var fileRef = new ModelFileRef(modelPath, null, new FileInfo(modelPath).Length);
            await _engine.LoadAsync(fileRef, new ModelLoadOptions(ContextTokens: 1024, Threads: 2), null, ct);
            _isLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModelService] Failed to load model: {ex.Message}");
            _isLoaded = false;
            return false;
        }
    }

    /// <summary>
    /// Implements SAF Model Import (§9.3 / MOD-001):
    /// Verifies GGUF magic header (0x46554747), copies into app-private storage,
    /// sets active model, and initializes inference engine without network permissions.
    /// </summary>
    public async Task<bool> ImportModelAsync(FileResult file, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (file == null) return false;

        Directory.CreateDirectory(_modelsDir);
        var fileName = Path.GetFileName(file.FileName);
        if (!fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".gguf";
        }
        var targetPath = Path.Combine(_modelsDir, fileName);
        var tempPath = targetPath + ".import.tmp";

        try
        {
            await using (var sourceStream = await file.OpenReadAsync())
            {
                // Verify GGUF magic header: 0x46554747 ('GGUF')
                byte[] magic = new byte[4];
                int read = await sourceStream.ReadAsync(magic, ct);
                if (read < 4)
                {
                    System.Diagnostics.Debug.WriteLine("[ModelService] Selected file is too small to be a valid GGUF model.");
                    return false;
                }

                uint magicValue = BitConverter.ToUInt32(magic, 0);
                if (magicValue != GgufMagic)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModelService] Invalid GGUF header: expected 0x46554747, got 0x{magicValue:X8}.");
                    return false;
                }

                if (sourceStream.CanSeek)
                {
                    sourceStream.Position = 0;
                }

                long totalLength = 0;
                try { totalLength = sourceStream.Length; } catch { }

                if (totalLength > 0)
                {
                    try
                    {
                        var drive = new DriveInfo(Path.GetPathRoot(_modelsDir) ?? _modelsDir);
                        if (drive.AvailableFreeSpace < totalLength + 200 * 1024 * 1024L)
                        {
                            throw new InvalidOperationException("Insufficient disk space to import model (need file size + 200 MB).");
                        }
                    }
                    catch (InvalidOperationException) { throw; }
                    catch { }
                }

                await using (var targetStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    if (!sourceStream.CanSeek)
                    {
                        await targetStream.WriteAsync(magic, ct);
                    }

                    var buffer = new byte[81920];
                    long totalRead = sourceStream.CanSeek ? 0 : 4;

                    int bytesRead;
                    while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                    {
                        await targetStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                        totalRead += bytesRead;
                        if (totalLength > 0 && progress != null)
                        {
                            progress.Report((double)totalRead / totalLength);
                        }
                    }
                }
            }

            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
            File.Move(tempPath, targetPath);
            Preferences.Set("active_slm_model_path", targetPath);

            await _engine.UnloadAsync();
            _isLoaded = false;
            return await EnsureModelLoadedAsync(ct);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }
    }

    public async Task<string?> ClassifyAsync(string title, string text, string package, CancellationToken ct = default)
    {
        if (!await EnsureModelLoadedAsync(ct)) return null;

        try
        {
            var systemPrompt = "You are a notification classifier. Classify into one word: offers, work, people, money, orders, security, social, updates, or other.";
            var userPrompt = $"App: {package}\nTitle: {title}\nBody: {text}\nCategory:";

            var messages = new[]
            {
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", userPrompt)
            };

            var request = new GenerationRequest(
                Messages: messages,
                MaxTokens: 10,
                Temperature: 0.1f,
                TopP: 0.9f,
                Stop: new[] { "\n", "<|im_end|>", "</s>" },
                Format: new ResponseFormat.Text()
            );

            var sb = new System.Text.StringBuilder();
            await foreach (var chunk in _engine.GenerateAsync(request, ct))
            {
                if (!string.IsNullOrEmpty(chunk.TextDelta))
                {
                    sb.Append(chunk.TextDelta);
                }
            }

            var output = sb.ToString().Trim().ToLowerInvariant();
            return NormalizeCategory(output);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModelService] SLM inference failed: {ex.Message}");
            return null;
        }
    }

    public async Task<string> BenchmarkAsync(CancellationToken ct = default)
    {
        if (!await EnsureModelLoadedAsync(ct))
        {
            return "Model not downloaded or loaded.";
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var messages = new[]
        {
            new ChatMessage("user", "Hello, give me 5 words about technology.")
        };

        var request = new GenerationRequest(
            Messages: messages,
            MaxTokens: 20,
            Temperature: 0.2f,
            TopP: 0.9f,
            Stop: new[] { "<|im_end|>", "</s>" },
            Format: new ResponseFormat.Text()
        );

        int tokens = 0;
        await foreach (var chunk in _engine.GenerateAsync(request, ct))
        {
            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                tokens++;
            }
        }
        sw.Stop();

        var seconds = sw.Elapsed.TotalSeconds;
        var tps = seconds > 0 ? tokens / seconds : 0;
        return $"{tokens} tokens in {sw.ElapsedMilliseconds} ms ({tps:F1} tok/s)";
    }

    public async Task<T301ExplainResult?> ExplainNotificationAsync(string title, string text, string package, string category, CancellationToken ct = default)
    {
        // SLM-004: OTP safety guard
        if (category.Equals("security", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("کد تایید", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("رمز یکبار", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("OTP", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("کد تایید", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("رمز یکبار", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("OTP", StringComparison.OrdinalIgnoreCase))
        {
            return new T301ExplainResult(
                Summary: "Security notifications containing verification codes are excluded from AI processing for privacy.",
                Importance: "high",
                Action: "Never share verification codes.",
                CategorySuggestion: "security",
                Tags: new[] { "security", "otp" },
                Facts: Array.Empty<T3Fact>()
            );
        }

        if (!await EnsureModelLoadedAsync(ct)) return null;

        try
        {
            var safeText = text.Length > 1200 ? text.Substring(0, 1200) : text;
            var safeTitle = title.Length > 200 ? title.Substring(0, 200) : title;

            var grammarGbnf = await GetExplainGrammarAsync();
            var format = !string.IsNullOrWhiteSpace(grammarGbnf)
                ? (ResponseFormat)new ResponseFormat.Grammar(grammarGbnf)
                : new ResponseFormat.Text();

            var systemPrompt = await GetExplainPromptAsync();
            var userPrompt = $"App: {package}\nCategory: {category}\nTitle: {safeTitle}\nBody: {safeText}";

            // Generation attempt 1
            var rawOutput = await GenerateAsync(systemPrompt, userPrompt, format, ExplainTask.MaxTokens, ExplainTask.Temperature, ExplainTask.TopP, ct);
            var parsed = TryParseExplain(rawOutput);

            // SLM-002: On failure, retry ONCE with a shorter, stricter prompt
            if (parsed == null)
            {
                var strictSystem = "Output ONLY raw JSON with keys: summary, importance, action, category_suggestion, tags, facts.";
                var retryOutput = await GenerateAsync(strictSystem, userPrompt, format, ExplainTask.MaxTokens, 0.1f, 0.8f, ct);
                parsed = TryParseExplain(retryOutput);
            }

            // SLM-002: Never show raw unparsed model text on failure
            if (parsed == null)
            {
                return new T301ExplainResult(
                    Summary: LocalizationManager.Instance["Explain_Failed"] ?? "Unable to analyze notification.",
                    Importance: "normal",
                    Action: null,
                    CategorySuggestion: null,
                    Tags: new[] { category },
                    Facts: Array.Empty<T3Fact>()
                );
            }

            // SLM-003: Grounding Guard
            return GroundingGuard.Apply(parsed, safeTitle, safeText);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModelService] ExplainNotificationAsync failed: {ex.Message}");
            return null;
        }
    }

    public async Task<T303DigestResult?> GenerateDigestAsync(IEnumerable<Notification> notifications, CancellationToken ct = default)
    {
        var notifsList = notifications
            .Where(n => !n.Category.Equals("security", StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();

        if (notifsList.Count == 0)
        {
            return new T303DigestResult(
                Headline: "No notifications to digest.",
                Sections: Array.Empty<T303Section>(),
                FollowUps: Array.Empty<string>()
            );
        }

        if (!await EnsureModelLoadedAsync(ct))
        {
            return GenerateRulesDigest(notifsList);
        }

        try
        {
            var promptBuilder = new System.Text.StringBuilder();
            promptBuilder.AppendLine("Notifications from today:");
            foreach (var n in notifsList)
            {
                promptBuilder.AppendLine($"- [{n.Category}] {n.Title}: {n.Text}");
            }

            var grammarGbnf = await GetDigestGrammarAsync();
            var format = !string.IsNullOrWhiteSpace(grammarGbnf)
                ? (ResponseFormat)new ResponseFormat.Grammar(grammarGbnf)
                : new ResponseFormat.Text();

            var systemPrompt = await GetDigestPromptAsync();
            var userPrompt = promptBuilder.ToString();

            // Generation attempt 1
            var rawOutput = await GenerateAsync(systemPrompt, userPrompt, format, DigestTask.MaxTokens, DigestTask.Temperature, DigestTask.TopP, ct);
            var parsed = TryParseDigest(rawOutput);

            // SLM-002: Retry once on failure
            if (parsed == null)
            {
                var strictSystem = "Output ONLY raw JSON with keys: headline, sections, follow_ups.";
                var retryOutput = await GenerateAsync(strictSystem, userPrompt, format, DigestTask.MaxTokens, 0.1f, 0.8f, ct);
                parsed = TryParseDigest(retryOutput);
            }

            return parsed ?? GenerateRulesDigest(notifsList);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ModelService] GenerateDigestAsync failed: {ex.Message}");
            return GenerateRulesDigest(notifsList);
        }
    }

    public T303DigestResult GenerateRulesDigest(IEnumerable<Notification> notifications)
    {
        var list = notifications.ToList();
        var total = list.Count;
        var groups = list.GroupBy(n => n.Category).ToList();

        var sections = new List<T303Section>();
        foreach (var g in groups)
        {
            var bullets = g.Take(3).Select(n => $"{n.Title}: {n.Text}").Select(s => s.Length > 60 ? s.Substring(0, 57) + "..." : s).ToArray();
            sections.Add(new T303Section(g.Key, bullets));
        }

        var headline = total switch
        {
            0 => "No notifications today.",
            1 => "1 notification recorded today.",
            _ => $"{total} notifications sorted today across {groups.Count} categories."
        };

        return new T303DigestResult(
            Headline: headline,
            Sections: sections.ToArray(),
            FollowUps: Array.Empty<string>()
        );
    }

    public async IAsyncEnumerable<string> GenerateDigestStreamAsync(
        IEnumerable<Notification> notifications,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var notifsList = notifications
            .Where(n => !n.Category.Equals("security", StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .ToList();
        if (!await EnsureModelLoadedAsync(ct))
        {
            yield break;
        }

        var promptBuilder = new System.Text.StringBuilder();
        promptBuilder.AppendLine("Notifications from today:");
        foreach (var n in notifsList)
        {
            promptBuilder.AppendLine($"- [{n.Category}] {n.Title}: {n.Text}");
        }

        var grammarGbnf = await GetDigestGrammarAsync();
        var format = !string.IsNullOrWhiteSpace(grammarGbnf)
            ? (ResponseFormat)new ResponseFormat.Grammar(grammarGbnf)
            : new ResponseFormat.Text();

        var systemPrompt = await GetDigestPromptAsync();
        var userPrompt = promptBuilder.ToString();

        var messages = new[]
        {
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", userPrompt)
        };

        var request = new GenerationRequest(
            Messages: messages,
            MaxTokens: DigestTask.MaxTokens,
            Temperature: DigestTask.Temperature,
            TopP: DigestTask.TopP,
            Stop: new[] { "<|im_end|>", "</s>" },
            Format: format
        );

        await foreach (var chunk in _engine.GenerateAsync(request, ct))
        {
            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                yield return chunk.TextDelta;
            }
        }
    }

    public IReadOnlyList<InstalledModelInfo> GetInstalledModels()
    {
        var list = new List<InstalledModelInfo>();
        if (!Directory.Exists(_modelsDir)) return list;

        var activePath = ActiveModelPath;
        foreach (var file in Directory.GetFiles(_modelsDir, "*.gguf"))
        {
            var fi = new FileInfo(file);
            var name = Path.GetFileNameWithoutExtension(file);
            var sizeMb = fi.Length / (1024.0 * 1024.0);
            var quant = "GGUF";
            var lower = name.ToLowerInvariant();
            if (lower.Contains("q4_k_m")) quant = "Q4_K_M";
            else if (lower.Contains("q4_0")) quant = "Q4_0";
            else if (lower.Contains("q8_0")) quant = "Q8_0";
            else if (lower.Contains("q5_k_m")) quant = "Q5_K_M";

            bool isActive = !string.IsNullOrEmpty(activePath) && string.Equals(file, activePath, StringComparison.OrdinalIgnoreCase);
            list.Add(new InstalledModelInfo(name, file, fi.Length, $"{sizeMb:F1} MB", quant, isActive));
        }

        return list;
    }

    public async Task SetActiveModelAsync(string path)
    {
        if (!File.Exists(path)) return;
        Preferences.Set("active_slm_model_path", path);
        await _engine.UnloadAsync();
        _isLoaded = false;
        await EnsureModelLoadedAsync();
    }

    public async Task DeleteModelAsync(string path)
    {
        if (string.Equals(path, ActiveModelPath, StringComparison.OrdinalIgnoreCase))
        {
            Preferences.Remove("active_slm_model_path");
            await _engine.UnloadAsync();
            _isLoaded = false;
        }

        if (File.Exists(path))
        {
            try { File.Delete(path); } catch { }
        }
    }

    private async Task<string> GenerateAsync(string systemPrompt, string userPrompt, ResponseFormat format, int maxTokens, float temperature, float topP, CancellationToken ct)
    {
        var messages = new[]
        {
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", userPrompt)
        };

        var request = new GenerationRequest(
            Messages: messages,
            MaxTokens: maxTokens,
            Temperature: temperature,
            TopP: topP,
            Stop: new[] { "<|im_end|>", "</s>" },
            Format: format
        );

        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in _engine.GenerateAsync(request, ct))
        {
            if (!string.IsNullOrEmpty(chunk.TextDelta))
            {
                sb.Append(chunk.TextDelta);
            }
        }

        return sb.ToString().Trim();
    }

    private static T301ExplainResult? TryParseExplain(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            return ExplainTask.ParseResult(raw);
        }
        catch
        {
            return null;
        }
    }

    private static T303DigestResult? TryParseDigest(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        try
        {
            return DigestTask.ParseResult(raw);
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> GetExplainGrammarAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("grammars/explain.gbnf");
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(content)) return content;
        }
        catch { }

        return @"root ::= ""{"" ws ""\""summary\"""" ws "":"" ws string ws "","" ws ""\""importance\"""" ws "":"" ws importance ws ( "","" ws ""\""action\"""" ws "":"" ws ( string | ""null"" ) )? ( "","" ws ""\""category_suggestion\"""" ws "":"" ws ( category | ""null"" ) )? ( "","" ws ""\""tags\"""" ws "":"" ws string_list )? ( "","" ws ""\""facts\"""" ws "":"" ws fact_list )? ""}""
importance ::= ""\""low\"""" | ""\""normal\"""" | ""\""high\""""
category ::= ""\""offers\"""" | ""\""work\"""" | ""\""people\"""" | ""\""money\"""" | ""\""orders\"""" | ""\""security\"""" | ""\""social\"""" | ""\""updates\"""" | ""\""other\""""
string_list ::= ""["" ws ( string ( ws "","" ws string )* )? ws ""]""
fact_list ::= ""["" ws ( fact ( ws "","" ws fact )* )? ws ""]""
fact ::= ""{"" ws ""\""label\"""" ws "":"" ws string ws "","" ws ""\""value\"""" ws "":"" ws string ws ""}""
string ::= ""\"""" char* ""\""""
char ::= [^""\\\x7F\x00-\x1F] | ""\\"" ([""\\/bfnrt] | ""u"" [0-9a-fA-F]{4})
ws ::= [ \t\n\r]*";
    }

    private static async Task<string> GetDigestGrammarAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("grammars/digest.gbnf");
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(content)) return content;
        }
        catch { }

        return @"root ::= ""{"" ws ""\""headline\"""" ws "":"" ws string ws "","" ws ""\""sections\"""" ws "":"" ws section_list ws ( "","" ws ""\""follow_ups\"""" ws "":"" ws string_list )? ""}""
section_list ::= ""["" ws ( section ( ws "","" ws section )* )? ws ""]""
section ::= ""{"" ws ""\""category\"""" ws "":"" ws category ws "","" ws ""\""bullets\"""" ws "":"" ws string_list ws ""}""
category ::= ""\""offers\"""" | ""\""work\"""" | ""\""people\"""" | ""\""money\"""" | ""\""orders\"""" | ""\""security\"""" | ""\""social\"""" | ""\""updates\"""" | ""\""other\""""
string_list ::= ""["" ws ( string ( ws "","" ws string )* )? ws ""]""
string ::= ""\"""" char* ""\""""
char ::= [^""\\\x7F\x00-\x1F] | ""\\"" ([""\\/bfnrt] | ""u"" [0-9a-fA-F]{4})
ws ::= [ \t\n\r]*";
    }

    private static async Task<string> GetExplainPromptAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("prompts/explain_prompt.md");
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(content)) return content;
        }
        catch { }

        return "You are an offline assistant analyzing a mobile notification. Output ONLY a valid JSON object matching this schema: {\"summary\": \"string <= 25 words\", \"importance\": \"low\"|\"normal\"|\"high\", \"action\": \"string or null\", \"category_suggestion\": \"string or null\", \"tags\": [\"tag1\", \"tag2\"], \"facts\": [{\"label\": \"string\", \"value\": \"string\"}]}. No markdown.";
    }

    private static async Task<string> GetDigestPromptAsync()
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync("prompts/digest_prompt.md");
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(content)) return content;
        }
        catch { }

        return "You are an offline assistant generating a concise daily digest of notifications. Output ONLY a valid JSON object matching this schema: {\"headline\": \"brief headline summary\", \"sections\": [{\"category\": \"string\", \"bullets\": [\"bullet 1 <= 20 words\"]}], \"follow_ups\": [\"string\"]}. No markdown.";
    }

    private static string? NormalizeCategory(string raw)
    {
        var lower = raw.ToLowerInvariant();
        if (lower.Contains("offer") || lower.Contains("promo") || lower.Contains("discount") || lower.Contains("تخفیف")) return "promo";
        if (lower.Contains("work") || lower.Contains("کار")) return "work";
        if (lower.Contains("people") || lower.Contains("chat") || lower.Contains("message") || lower.Contains("پیام")) return "people";
        if (lower.Contains("money") || lower.Contains("bank") || lower.Contains("transfer") || lower.Contains("مالی")) return "money";
        if (lower.Contains("order") || lower.Contains("delivery") || lower.Contains("سفارش")) return "orders";
        if (lower.Contains("secur") || lower.Contains("otp") || lower.Contains("code") || lower.Contains("امنیت")) return "security";
        if (lower.Contains("social") || lower.Contains("شبکه")) return "social";
        if (lower.Contains("update") || lower.Contains("بروزرسانی")) return "updates";
        if (lower.Contains("other") || lower.Contains("سایر")) return "other";
        return null;
    }
}
