namespace Alak.Inference.Abstractions;

/// <summary>
/// The abstraction every Tier 3 (SLM) consumer depends on (ENG-001). All inference
/// engines — real (llama.cpp, LiteRT-LM) and the in-repo fake — implement this.
/// </summary>
public interface IInferenceEngine : IAsyncDisposable
{
    /// <summary>Stable engine identifier, e.g. "llamacpp", "litertlm", "fake".</summary>
    string EngineId { get; }

    /// <summary>Model file formats this engine can load, e.g. "gguf", "litertlm".</summary>
    IReadOnlyCollection<string> SupportedFormats { get; }

    /// <summary>Static capability flags used by the model manager (MOD-004).</summary>
    EngineCapabilities Capabilities { get; }

    /// <summary>
    /// Inspect a model file without loading it: format, architecture, parameter count,
    /// quantization, context length, chat-template presence, vocabulary size (MOD-002).
    /// Engines are registered by format; a wrong format fails HERE, not deep in native code (ENG-002).
    /// </summary>
    Task<ModelProbeResult> ProbeAsync(ModelFileRef file, CancellationToken ct);

    /// <summary>Load a model. Reports copy/load progress; honours cancellation.</summary>
    Task LoadAsync(ModelFileRef file, ModelLoadOptions options,
                   IProgress<double>? progress, CancellationToken ct);

    /// <summary>Stream generation token-by-token; cancellation stops within 500 ms (SLM-008).</summary>
    IAsyncEnumerable<GenerationChunk> GenerateAsync(GenerationRequest request, CancellationToken ct);

    /// <summary>Unload the current model and release native resources.</summary>
    Task UnloadAsync();

    /// <summary>Live resource usage: PSS (KB), loaded model bytes, worker threads.</summary>
    EngineResourceSnapshot GetResourceSnapshot();
}

/// <summary>Static capability set of an engine (spec §9.5).</summary>
public sealed record EngineCapabilities(
    bool SupportsGrammar,
    bool SupportsJsonSchema,
    bool SupportsGpu,
    bool ExposesChatTemplate,
    int MaxContext);

/// <summary>One message in a chat-style prompt.</summary>
public sealed record ChatMessage(string Role, string Content);

/// <summary>A generation request with sampling parameters (defaults per SLM-006).</summary>
public sealed record GenerationRequest(
    IReadOnlyList<ChatMessage> Messages,
    int MaxTokens,
    float Temperature,
    float TopP,
    IReadOnlyList<string>? Stop,
    ResponseFormat Format);

/// <summary>How the engine must constrain its output (spec §9.5).</summary>
public abstract record ResponseFormat
{
    /// <summary>Plain text, no constraint.</summary>
    public sealed record Text : ResponseFormat;

    /// <summary>Constrain to a GBNF grammar string.</summary>
    public sealed record Grammar(string Gbnf) : ResponseFormat;

    /// <summary>Constrain to a JSON schema string.</summary>
    public sealed record JsonSchema(string Schema) : ResponseFormat;
}

/// <summary>A model file on disk plus what is known about it (MOD-002/MOD-003).</summary>
public sealed record ModelFileRef(string Path, string? Sha256, long SizeBytes);

/// <summary>Load-time options for an engine.</summary>
public sealed record ModelLoadOptions(
    int ContextTokens = 2048,
    int Threads = 0,
    bool PreferGpu = false);

/// <summary>Parsed metadata of a model file (MOD-002).</summary>
public sealed record ModelProbeResult(
    string Format,
    string Architecture,
    float ParamCountBillions,
    string Quantization,
    int ContextLength,
    bool? HasChatTemplate,
    int VocabSize);

/// <summary>One streamed piece of a generation.</summary>
public sealed record GenerationChunk(
    string? TextDelta,
    bool IsFinal,
    int? PromptTokens = null,
    int? GeneratedTokens = null);

/// <summary>Point-in-time resource usage of a loaded engine (ARC/SLM-011 budget UI).</summary>
public sealed record EngineResourceSnapshot(
    long PssKb,
    long ModelBytes,
    int WorkerThreads);
