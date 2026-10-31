using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alak.Core.Capture;
using Alak.Core.Model;
using Alak.Inference.Abstractions;
using Alak.Inference.Abstractions.Tasks;
using Microsoft.Maui.Storage;

namespace Alak.App.Services;

/// <summary>
/// On-device evidence harness for the M8/M9 gates (spec §9.4 steps 2–3 and M9 exit
/// criteria). Triggered only by an explicit adb intent in a debug build; writes JSON
/// under files/diagnostics and logs a one-line summary under the AlakDiag tag.
/// Never runs in the background and never touches the network.
/// </summary>
public sealed class SlmDiagnosticsService
{
    private const string Tag = "AlakDiag";
    private static readonly Regex s_codeClaim = new(@"(?<![A-Za-z0-9])[A-Z0-9]{4,12}(?![A-Za-z0-9])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private readonly IModelService _modelService;
    private readonly IInferenceEngine _engine;
    private readonly IDeviceResourceGuard _resourceGuard;

    public SlmDiagnosticsService(IModelService modelService, IInferenceEngine engine, IDeviceResourceGuard resourceGuard)
    {
        _modelService = modelService;
        _engine = engine;
        _resourceGuard = resourceGuard;
    }

    public async Task<string> RunAsync(string kind, CancellationToken ct = default)
    {
        Log($"run start: {kind}");
        var report = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["requested"] = kind,
            ["startedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["model"] = _modelService.ActiveModelPath,
            ["device"] = _resourceGuard.Capture()
        };

        try
        {
            if (kind is "all" or "bench") report["benchmark"] = await RunBenchmarkAsync(ct);
            if (kind is "all" or "golden") report["golden"] = await RunGoldenPromptsAsync(ct);
            if (kind is "all" or "cancel") report["cancellation"] = await RunCancellationAsync(ct);
            if (kind is "all" or "oversize") report["oversize"] = await RunOversizeGuardAsync(ct);
            if (kind is "all" or "thermal") report["thermal"] = await RunThermalAsync(ct);

            report["completedAt"] = DateTimeOffset.UtcNow.ToString("O");
            return await WriteReportAsync(kind, report);
        }
        catch (Exception ex)
        {
            report["error"] = ex.ToString();
            return await WriteReportAsync(kind, report);
        }
    }

    private async Task<object?> RunBenchmarkAsync(CancellationToken ct)
    {
        var result = await _modelService.RunBenchmarkAsync(ct);
        Debug.WriteLine($"[{Tag}] benchmark: {result?.Detail ?? "unavailable"}");
        return result;
    }

    private async Task<object> RunGoldenPromptsAsync(CancellationToken ct)
    {
        var explainCases = GoldenExplainCases();
        var explainResults = new List<object>();
        var valid = 0;
        var claimsChecked = 0;
        var claimsGrounded = 0;

        foreach (var c in explainCases)
        {
            if (ct.IsCancellationRequested) break;
            var sw = Stopwatch.StartNew();
            var result = await _modelService.ExplainNotificationAsync(c.Title, c.Text, c.Package, c.Category, ct);
            sw.Stop();

            var isFallback = result == null ||
                             string.Equals(result.Summary, "Explain_Failed", StringComparison.OrdinalIgnoreCase) ||
                             result.Summary.Contains("Unable to analyze", StringComparison.OrdinalIgnoreCase) ||
                             result.Summary.Contains("excluded from AI", StringComparison.OrdinalIgnoreCase);
            var (checkedCount, groundedCount) = result == null ? (0, 0) : CheckClaims(result, c.Text + " " + c.Title);
            var isValid = !isFallback && result != null && !string.IsNullOrWhiteSpace(result.Summary);

            if (isValid) valid++;
            claimsChecked += checkedCount;
            claimsGrounded += groundedCount;

            explainResults.Add(new
            {
                c.Id,
                c.Lang,
                valid = isValid,
                latencyMs = sw.Elapsed.TotalMilliseconds,
                summary = result?.Summary,
                facts = result?.Facts,
                claimsChecked = checkedCount,
                claimsGrounded = groundedCount
            });
        }

        var digestResults = new List<object>();
        var digestValid = 0;
        foreach (var c in GoldenDigestCases())
        {
            if (ct.IsCancellationRequested) break;
            var sw = Stopwatch.StartNew();
            var result = await _modelService.GenerateDigestAsync(c.Notifications, ct);
            sw.Stop();
            var isValid = result != null && result.Sections.Length > 0 && !string.IsNullOrWhiteSpace(result.Headline);
            if (isValid) digestValid++;
            digestResults.Add(new
            {
                c.Id,
                c.Lang,
                valid = isValid,
                latencyMs = sw.Elapsed.TotalMilliseconds,
                headline = result?.Headline,
                sections = result?.Sections.Length ?? 0
            });
        }

        var report = new
        {
            explainTotal = explainResults.Count,
            explainValid = valid,
            explainJsonValidity = explainResults.Count == 0 ? 0 : (double)valid / explainResults.Count,
            claimsChecked,
            claimsGrounded,
            digestTotal = digestResults.Count,
            digestValid,
            explain = explainResults,
            digest = digestResults
        };

        Debug.WriteLine($"[{Tag}] golden: explain {valid}/{explainResults.Count} valid, grounding {claimsGrounded}/{claimsChecked}, digest {digestValid}/{digestResults.Count}");
        return report;
    }

    private async Task<object> RunCancellationAsync(CancellationToken ct)
    {
        if (!await _modelService.EnsureModelLoadedAsync(ct)) return new { ok = false, reason = "model not loaded" };

        var request = new GenerationRequest(
            Messages: new[] { new ChatMessage("user", "Write a long paragraph about offline software.") },
            MaxTokens: 400,
            Temperature: 0.7f,
            TopP: 0.9f,
            Stop: new[] { "<|im_end|>", "</s>" },
            Format: new ResponseFormat.Text());

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var watch = Stopwatch.StartNew();
        double cancelAtMs = -1;
        var sawFirstToken = false;

        try
        {
            await foreach (var chunk in _engine.GenerateAsync(request, cts.Token))
            {
                if (!sawFirstToken && !string.IsNullOrEmpty(chunk.TextDelta))
                {
                    sawFirstToken = true;
                    cancelAtMs = watch.Elapsed.TotalMilliseconds;
                    cts.Cancel();
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }

        watch.Stop();
        var latency = cancelAtMs >= 0 ? watch.Elapsed.TotalMilliseconds - cancelAtMs : -1;
        var ok = latency >= 0 && latency < 500;
        Debug.WriteLine($"[{Tag}] cancellation: latency {latency:F0} ms (ok={ok})");
        return new { ok, cancelLatencyMs = latency, firstTokenSeen = sawFirstToken };
    }

    private async Task<object> RunOversizeGuardAsync(CancellationToken ct)
    {
        var modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
        Directory.CreateDirectory(modelsDir);
        var oversizePath = Path.Combine(modelsDir, "diagnostic-oversize.gguf");
        var previous = Preferences.Get("active_slm_model_path", string.Empty);

        try
        {
            await _modelService.UnloadAsync(ct);
            using (var stream = new FileStream(oversizePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.SetLength(2L * 1024 * 1024 * 1024); // sparse 2 GB
            }

            Preferences.Set("active_slm_model_path", oversizePath);
            var loaded = await _modelService.EnsureModelLoadedAsync(ct);
            var reason = _modelService.LastLoadError;
            var refused = !loaded && !string.IsNullOrWhiteSpace(reason);

            Debug.WriteLine($"[{Tag}] oversize guard: refused={refused} reason={reason}");
            return new { refused, loaded, reason };
        }
        finally
        {
            Preferences.Set("active_slm_model_path", previous);
            try { File.Delete(oversizePath); } catch { }
        }
    }

    private async Task<object> RunThermalAsync(CancellationToken ct)
    {
        var runs = new List<object>();
        for (var i = 0; i < 5 && !ct.IsCancellationRequested; i++)
        {
            var result = await _modelService.RunBenchmarkAsync(ct);
            var snapshot = _resourceGuard.Capture();
            runs.Add(new
            {
                run = i + 1,
                tokensPerSecond = result?.TokensPerSecond ?? 0,
                loadMs = result?.LoadMs ?? 0,
                thermalStatus = snapshot.ThermalStatus,
                pssKb = snapshot.PssKb
            });
        }

        return runs;
    }

    private static (int Checked, int Grounded) CheckClaims(T301ExplainResult result, string source)
    {
        var normalizedSource = Normalize(source);
        var checkedCount = 0;
        var grounded = 0;

        foreach (var fact in result.Facts)
        {
            if (string.IsNullOrWhiteSpace(fact.Value) || !LooksLikeClaim(fact.Value)) continue;
            checkedCount++;
            if (normalizedSource.Contains(Normalize(fact.Value), StringComparison.Ordinal)) grounded++;
        }

        foreach (Match match in s_codeClaim.Matches(result.Summary + " " + (result.Action ?? string.Empty)))
        {
            var token = match.Value;
            if (token.Length < 4 || token.All(char.IsDigit)) continue;
            checkedCount++;
            if (normalizedSource.Contains(token.ToLowerInvariant(), StringComparison.Ordinal)) grounded++;
        }

        return (checkedCount, grounded);
    }

    /// <summary>Only codes/amounts/dates/URLs are grounding-checkable (SLM-003).</summary>
    private static bool LooksLikeClaim(string value)
    {
        if (value.Any(char.IsDigit)) return true;
        if (value.Contains('@') || value.Contains("://", StringComparison.Ordinal)) return true;
        return s_codeClaim.IsMatch(value);
    }

    private static string Normalize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            var mapped = c switch
            {
                >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
                >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
                _ => c
            };
            if (char.IsLetterOrDigit(mapped)) sb.Append(char.ToLowerInvariant(mapped));
        }
        return sb.ToString();
    }

    private static async Task<string> WriteReportAsync(string kind, Dictionary<string, object?> report)
    {
        try
        {
            var dir = Path.Combine(FileSystem.AppDataDirectory, "diagnostics");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"m8-{kind}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Log($"report written: {path}");
            return path;
        }
        catch (Exception ex)
        {
            Log($"report write failed: {ex.Message}");
            return string.Empty;
        }
    }

    private static void Log(string message)
    {
        Debug.WriteLine($"[{Tag}] {message}");
#if ANDROID
        global::Android.Util.Log.Info(Tag, message);
#endif
    }

    private sealed record GoldenExplain(string Id, string Lang, string Package, string Category, string Title, string Text);

    private static List<GoldenExplain> GoldenExplainCases() => new()
    {
        new("en-promo", "en", "com.digikala", "promo", "Digikala weekend sale",
            "30% off all dairy. Code: MILK30. Valid until 2026-11-05. Min spend 200,000 IRR."),
        new("en-orders", "en", "com.shop", "orders", "Order shipped",
            "Your order A1234 has shipped and arrives tomorrow between 9 and 12."),
        new("en-money", "en", "com.bank", "money", "Salary deposited",
            "Salary of 25,000,000 IRR was deposited to your account ending 4321."),
        new("en-work", "en", "com.office", "work", "Meeting moved",
            "The planning meeting moved to 10:30 in room 4. Please update your calendar."),
        new("en-social", "en", "com.social", "social", "New follower",
            "Sara started following you. Send her a message to say hello."),
        new("fa-promo", "fa", "ir.digikala", "promo", "تخفیف پایان هفته",
            "تخفیف ۵۰٪ روی همه لبنیات. کد: SHIR50. تا ۱۵ آبان. حداقل خرید ۲۰۰,۰۰۰ تومان."),
        new("fa-orders", "fa", "ir.shop", "orders", "سفارش ارسال شد",
            "سفارش شما به شماره A1234 ارسال شد و فردا بین ساعت ۹ تا ۱۲ می‌رسد."),
        new("fa-money", "fa", "ir.bank", "money", "واریز حقوق",
            "مبلغ ۲۵,۰۰۰,۰۰۰ ریال حقوق به حساب شما واریز شد."),
        new("fa-work", "fa", "ir.office", "work", "جلسه جابجا شد",
            "جلسه برنامه‌ریزی به ساعت ۱۰:۳۰ در اتاق ۴ منتقل شد."),
        new("fa-social", "fa", "ir.social", "social", "دنبال‌کننده جدید",
            "سارا شما را دنبال کرد. برای سلام کردن به او پیام بدهید.")
    };

    private sealed record GoldenDigest(string Id, string Lang, IReadOnlyList<Notification> Notifications);

    private static List<GoldenDigest> GoldenDigestCases() => new()
    {
        new("en-digest", "en", new[]
        {
            Make("d1", "com.digikala", "Promo", "30% off dairy with code MILK30.", "promo"),
            Make("d2", "com.bank", "Salary", "25,000,000 IRR deposited.", "money"),
            Make("d3", "com.shop", "Order", "Order A1234 arrives tomorrow.", "orders")
        }),
        new("fa-digest", "fa", new[]
        {
            Make("d4", "ir.digikala", "تخفیف", "تخفیف ۵۰٪ لبنیات با کد SHIR50.", "promo"),
            Make("d5", "ir.bank", "واریز", "واریز ۲۵,۰۰۰,۰۰۰ ریال حقوق.", "money"),
            Make("d6", "ir.shop", "سفارش", "سفارش A1234 فردا می‌رسد.", "orders")
        })
    };

    private static Notification Make(string id, string package, string title, string text, string category) =>
        new(id, package, "", title, text, category, DateTimeOffset.UtcNow, false, true);
}
