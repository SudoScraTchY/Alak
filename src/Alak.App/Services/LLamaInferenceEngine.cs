using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Alak.Inference.Abstractions;
using LLama;
using LLama.Common;
using LLama.Abstractions;

namespace Alak.App.Services;

public sealed class LLamaInferenceEngine : IInferenceEngine
{
    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private StatelessExecutor? _executor;
    private ModelParams? _parameters;
    private ModelFileRef? _currentFile;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string EngineId => "llamacpp";

    public IReadOnlyCollection<string> SupportedFormats => new[] { "gguf" };

    public EngineCapabilities Capabilities => new(
        SupportsGrammar: true,
        SupportsJsonSchema: true,
        SupportsGpu: false,
        ExposesChatTemplate: true,
        MaxContext: 2048);

    public Task<ModelProbeResult> ProbeAsync(ModelFileRef file, CancellationToken ct)
    {
        var ext = Path.GetExtension(file.Path).TrimStart('.').ToLowerInvariant();
        if (ext != "gguf")
        {
            throw new InvalidOperationException($"ENG-002: format '{ext}' is not supported by llama.cpp.");
        }

        if (!File.Exists(file.Path))
        {
            throw new FileNotFoundException("Model file not found", file.Path);
        }

        var metadata = Alak.Core.Models.GgufMetadataReader.Read(file.Path);

        return Task.FromResult(new ModelProbeResult(
            Format: "gguf",
            Architecture: metadata.Architecture,
            ParamCountBillions: (float)metadata.ParameterCountBillions,
            Quantization: metadata.Quantization,
            ContextLength: metadata.ContextLength > 0 ? metadata.ContextLength : 2048,
            HasChatTemplate: metadata.HasChatTemplate,
            VocabSize: (int)Math.Min(metadata.VocabularySize, int.MaxValue)));
    }

    public async Task LoadAsync(ModelFileRef file, ModelLoadOptions options, IProgress<double>? progress, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_weights != null && _currentFile?.Path == file.Path)
            {
                progress?.Report(1.0);
                return;
            }

            await UnloadCoreAsync();

            if (!File.Exists(file.Path))
            {
                throw new FileNotFoundException("Model file not found", file.Path);
            }

            // Enforce resident memory budget < 1.5 GB resident (ENG-002)
            var fileInfo = new FileInfo(file.Path);
            if (fileInfo.Length > 1536L * 1024 * 1024)
            {
                throw new InvalidOperationException($"ENG-002: Model file size ({fileInfo.Length / (1024 * 1024)} MB) exceeds 1.5 GB resident memory budget.");
            }

            // Snapdragon 680 tuning: 4 Kryo 265 Gold cores (Cortex-A73 cluster) for maximum inference throughput
            int threadCount = options.Threads > 0 ? options.Threads : 4;
            uint contextTokens = (uint)(options.ContextTokens > 0 ? Math.Clamp(options.ContextTokens, 512, 2048) : 2048);

            _parameters = new ModelParams(file.Path)
            {
                ContextSize = contextTokens,
                Threads = threadCount,
                GpuLayerCount = 0 // Android CPU NEON execution
            };

            var floatProgress = progress != null ? new Progress<float>(f => progress.Report(f)) : null;

            _weights = await LLamaWeights.LoadFromFileAsync(_parameters, ct, floatProgress);
            // Note: StatelessExecutor allocates its own context per invocation.
            // Avoid creating a redundant context to conserve device RAM.
            _executor = new StatelessExecutor(_weights, _parameters);
            _currentFile = file;

            progress?.Report(1.0);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async IAsyncEnumerable<GenerationChunk> GenerateAsync(
        GenerationRequest request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (_executor == null || _weights == null || _parameters == null)
        {
            throw new InvalidOperationException("No model loaded. Call LoadAsync before GenerateAsync.");
        }

        var wantsJsonPrefill = request.Format is ResponseFormat.Grammar or ResponseFormat.JsonSchema;

        // Use the GGUF's own chat template (SLM-005) instead of hand-built ChatML.
        var systemMessage = request.Messages.FirstOrDefault(m => m.Role == "system")?.Content;
        var userPrompt = string.Join("\n", request.Messages.Where(m => m.Role != "system").Select(m => m.Content));

        string prompt;
        try
        {
            var template = new LLamaTemplate(_weights) { AddAssistant = true };
            if (!string.IsNullOrWhiteSpace(systemMessage)) template.Add("system", systemMessage!);
            template.Add("user", userPrompt);
            prompt = System.Text.Encoding.UTF8.GetString(template.Apply());
            if (wantsJsonPrefill) prompt += "{";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LLamaInferenceEngine] Chat template failed, using plain prompt: {ex.Message}");
            prompt = userPrompt + (wantsJsonPrefill ? "\n{" : string.Empty);
        }

        var samplingPipeline = new LLama.Sampling.DefaultSamplingPipeline
        {
            Temperature = request.Temperature >= 0 ? request.Temperature : 0.2f,
            TopP = (request.TopP > 0 && request.TopP <= 1.0f) ? request.TopP : 0.9f,
            RepeatPenalty = 1.05f
        };

        var inferenceParams = new InferenceParams
        {
            MaxTokens = request.MaxTokens > 0 ? request.MaxTokens : 200,
            AntiPrompts = request.Stop ?? new[] { "<|im_end|>", "</s>", "\n\n\n" },
            SamplingPipeline = samplingPipeline
        };

        int generatedTokens = 0;
        if (wantsJsonPrefill)
        {
            // The "{" is already part of the assistant message in the prompt;
            // emit it as the first chunk so the accumulator sees valid JSON.
            generatedTokens = 1;
            yield return new GenerationChunk("{", IsFinal: false, GeneratedTokens: 1);
        }

        // StatelessExecutor keeps a native context; reuse across calls leaks KV state for
        // some inputs (deterministic bare-"{" degenerations). Use a fresh executor per call.
        var executor = new StatelessExecutor(_weights, _parameters);
        try
        {
            await foreach (var token in executor.InferAsync(prompt, inferenceParams, ct))
            {
                ct.ThrowIfCancellationRequested();
                generatedTokens++;
                yield return new GenerationChunk(token, IsFinal: false, GeneratedTokens: generatedTokens);
            }
        }
        finally
        {
            try { executor.Context?.Dispose(); } catch { /* native cleanup */ }
        }

        yield return new GenerationChunk(null, IsFinal: true, PromptTokens: request.Messages.Count, GeneratedTokens: generatedTokens);
    }

    public async Task UnloadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await UnloadCoreAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task UnloadCoreAsync()
    {
        _context?.Dispose();
        _context = null;

        _weights?.Dispose();
        _weights = null;

        _executor = null;
        _parameters = null;
        _currentFile = null;

        GC.Collect();
        return Task.CompletedTask;
    }

    public EngineResourceSnapshot GetResourceSnapshot()
    {
        var modelBytes = _currentFile != null && File.Exists(_currentFile.Path)
            ? new FileInfo(_currentFile.Path).Length
            : 0;

        var memory = GC.GetTotalMemory(false);
        return new EngineResourceSnapshot(
            PssKb: memory / 1024,
            ModelBytes: modelBytes,
            WorkerThreads: _parameters?.Threads ?? 2);
    }

    public async ValueTask DisposeAsync()
    {
        await UnloadAsync();
        _lock.Dispose();
    }
}
