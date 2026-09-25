namespace Alak.Inference.Abstractions.Fake;

/// <summary>Selectable failure/behaviour mode of the fake engine (ENG-001).</summary>
public enum FakeEngineMode
{
    /// <summary>Deliver queued scripted outputs verbatim, token by token.</summary>
    Normal,

    /// <summary>Throw InvalidOperationException on the first GenerateAsync call.</summary>
    ThrowOnGenerate,

    /// <summary>Emit non-JSON garbage (exercises SLM-002 retry-once paths).</summary>
    Garbage,
}

/// <summary>
/// The in-repo fake engine (ENG-001): scripted outputs, configurable latency, configurable
/// failure modes, cancellation that lands within 500 ms (SLM-008). Powers every unit test;
/// no native code involved.
/// </summary>
public sealed class FakeInferenceEngine : IInferenceEngine
{
    private readonly Queue<string> _scripted = new();
    private int _generatedTokens;

    /// <summary>Delay between streamed "tokens" — the cancellation budget knob (SLM-008).</summary>
    public int TokenDelayMs { get; init; } = 10;

    /// <summary>Behaviour mode; default Normal.</summary>
    public FakeEngineMode Mode { get; init; } = FakeEngineMode.Normal;

    public string EngineId => "fake";
    public IReadOnlyCollection<string> SupportedFormats { get; } = new[] { "gguf", "litertlm", "txt" };
    public EngineCapabilities Capabilities { get; } = new(
        SupportsGrammar: true, SupportsJsonSchema: true, SupportsGpu: false,
        ExposesChatTemplate: true, MaxContext: 2048);

    /// <summary>Queue the exact text the next GenerateAsync call must emit.</summary>
    public void QueueScripted(string text) => _scripted.Enqueue(text);

    public Task<ModelProbeResult> ProbeAsync(ModelFileRef file, CancellationToken ct)
    {
        var ext = Path.GetExtension(file.Path).ToLowerInvariant().TrimStart('.');
        if (!SupportedFormats.Contains(ext))
            throw new InvalidOperationException(
                $"ENG-002: format '{ext}' is not supported by this engine.");
        // MOD-002-shaped plausible metadata for a small fixture model.
        return Task.FromResult(new ModelProbeResult(
            Format: ext,
            Architecture: "llama",
            ParamCountBillions: 0.56f,
            Quantization: "Q4_K_M",
            ContextLength: 2048,
            HasChatTemplate: true,
            VocabSize: 32_000));
    }

    public Task LoadAsync(ModelFileRef file, ModelLoadOptions options,
                          IProgress<double>? progress, CancellationToken ct)
    {
        progress?.Report(1.0);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<GenerationChunk> GenerateAsync(
        GenerationRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (Mode == FakeEngineMode.ThrowOnGenerate)
            throw new InvalidOperationException("FakeInferenceEngine: scripted generate failure.");

        var payload = Mode == FakeEngineMode.Garbage
            ? "Sorry, I cannot answer that right now... please try later."
            : _scripted.Count > 0 ? _scripted.Dequeue()
                                  : """{"summary": "ok"}""";

        // Stream in ~10-char "tokens"; check cancellation between tokens (SLM-008).
        for (var i = 0; i < payload.Length; i += 10)
        {
            ct.ThrowIfCancellationRequested();
            if (TokenDelayMs > 0)
                await Task.Delay(TokenDelayMs, ct);
            var delta = payload[i..Math.Min(i + 10, payload.Length)];
            _generatedTokens++;
            yield return new GenerationChunk(delta, IsFinal: false,
                GeneratedTokens: _generatedTokens);
        }
        yield return new GenerationChunk(null, IsFinal: true,
            PromptTokens: request.Messages.Count, GeneratedTokens: _generatedTokens);
    }

    public Task UnloadAsync() => Task.CompletedTask;

    public EngineResourceSnapshot GetResourceSnapshot()
        => new(PssKb: 48_000, ModelBytes: 700_000_000, WorkerThreads: 2);

    public ValueTask DisposeAsync()
    {
        _scripted.Clear();
        return ValueTask.CompletedTask;
    }
}
