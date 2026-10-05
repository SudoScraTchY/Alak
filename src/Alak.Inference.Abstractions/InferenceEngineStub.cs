using Alak.Inference.Abstractions;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Inference.Abstractions;

/// <summary>
/// Stub inference engine for development (M8 will swap with real implementation).
/// </summary>
public sealed class InferenceEngineStub : IInferenceEngine
{
    public string EngineId => "fake";

    public IReadOnlyCollection<string> SupportedFormats => new[] { "gguf" };

    public EngineCapabilities Capabilities => new EngineCapabilities(
        SupportsGrammar: false,
        SupportsJsonSchema: false,
        SupportsGpu: false,
        ExposesChatTemplate: false,
        MaxContext: 4096);

    public Task<ModelProbeResult> ProbeAsync(ModelFileRef file, CancellationToken ct)
    {
        return Task.FromResult(new ModelProbeResult(
            Format: "gguf",
            Architecture: "unknown",
            ParamCountBillions: 0f,
            Quantization: "unknown",
            ContextLength: 4096,
            HasChatTemplate: false,
            VocabSize: 0));
    }

    public Task LoadAsync(ModelFileRef file, ModelLoadOptions options, IProgress<double>? progress, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<GenerationChunk> GenerateAsync(GenerationRequest request, CancellationToken ct)
    {
        return GenerateCoreAsync(request, ct);
    }

    private async IAsyncEnumerable<GenerationChunk> GenerateCoreAsync(GenerationRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        yield return new GenerationChunk("stub", false, 0);
        await Task.CompletedTask;
    }

    public Task UnloadAsync() => Task.CompletedTask;

    public EngineResourceSnapshot GetResourceSnapshot() => new EngineResourceSnapshot(0, 0, 0);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}