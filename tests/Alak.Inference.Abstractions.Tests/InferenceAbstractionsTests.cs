using Alak.Inference.Abstractions.Fake;
using Alak.Inference.Abstractions.Tasks;

namespace Alak.Inference.Abstractions.Tests;

// ENG-001: fake engine implements IInferenceEngine; all unit tests run with no native code.
public class InterfaceShapeTests
{
    [Fact]
    public void ENG001_FakeEngine_ImplementsIInferenceEngine()
        => (typeof(FakeInferenceEngine).IsAssignableTo(typeof(IInferenceEngine))).ShouldBeTrue();

    [Fact]
    public void ENG001_EngineId_IsFake()
        => new FakeInferenceEngine().EngineId.ShouldBe("fake");

    [Fact]
    public void ENG001_SupportedFormats_IncludeGguf()
        => new FakeInferenceEngine().SupportedFormats.ShouldContain("gguf");

    [Fact]
    public void ResponseFormat_Subtypes_ExistPerSpec()
    {
        var text = new ResponseFormat.Text();
        var grammar = new ResponseFormat.Grammar("root ::= \"hi\"");
        var schema = new ResponseFormat.JsonSchema("{}");
        text.ShouldNotBeNull();
        grammar.Gbnf.ShouldBe("root ::= \"hi\"");
        schema.Schema.ShouldBe("{}");
        (text is ResponseFormat).ShouldBeTrue();
    }

    [Fact]
    public void GenerationRequest_ShapePerSpec()
    {
        var req = new GenerationRequest(
            new[] { new ChatMessage("user", "hello") }, 200, 0.2f, 0.9f, null,
            new ResponseFormat.JsonSchema("{}"));
        req.MaxTokens.ShouldBe(200);
        req.Temperature.ShouldBe(0.2f);
        req.TopP.ShouldBe(0.9f);
        req.Stop.ShouldBeNull();
        req.Format.ShouldBeOfType<ResponseFormat.JsonSchema>();
        req.Messages.Count.ShouldBe(1);
        req.Messages[0].Role.ShouldBe("user");
        req.Messages[0].Content.ShouldBe("hello");
    }

    [Fact]
    public void EngineCapabilities_ShapePerSpec()
    {
        var caps = new EngineCapabilities(
            SupportsGrammar: true, SupportsJsonSchema: true, SupportsGpu: false,
            ExposesChatTemplate: true, MaxContext: 2048);
        caps.SupportsGrammar.ShouldBeTrue();
        caps.MaxContext.ShouldBe(2048);
    }
}

// SLM-006: default sampling + per-task max tokens.
public class TaskSchemaTests
{
    [Fact]
    public void SLM006_MaxTokens_PerTask()
    {
        // explain 200, review 400, digest 500, code_details 200 (spec §9.2 / SLM-006)
        ExplainTask.MaxTokens.ShouldBe(200);
        ReviewTask.MaxTokens.ShouldBe(400);
        DigestTask.MaxTokens.ShouldBe(500);
        CodeDetailsTask.MaxTokens.ShouldBe(200);
    }

    [Fact]
    public void SLM006_DefaultSampling_IsLowTempHighTopP()
    {
        ExplainTask.Temperature.ShouldBe(0.2f);
        ExplainTask.TopP.ShouldBe(0.9f);
    }

    [Fact]
    public void T301Schema_DeserializesSampleOutput()
    {
        const string sample = """
            {"summary": "Order shipped with tracking", "importance": "normal",
             "action": "Track package", "category_suggestion": null,
             "tags": ["orders"], "facts": [{"label": "Carrier", "value": "Post"}]}
            """;
        var result = ExplainTask.ParseResult(sample);
        result.Summary.ShouldBe("Order shipped with tracking");
        result.Importance.ShouldBe("normal");
        result.Tags.ShouldNotBeEmpty();
        result.Facts[0].Label.ShouldBe("Carrier");
    }

    [Fact]
    public void T302Schema_ItemsDeserialize()
    {
        const string sample = """
            {"items": [{"id": 1, "category": "offers", "confidence": "medium"},
                       {"id": 2, "category": "money", "confidence": "high"}]}
            """;
        var result = ReviewTask.ParseResult(sample);
        result.Items.Length.ShouldBe(2);
        result.Items[1].Category.ShouldBe("money");
    }

    [Fact]
    public void T303Schema_DigestDeserializes()
    {
        const string sample = """
            {"headline": "Busy day", "sections": [{"category": "money", "bullets": ["Card charged $45"]}],
             "follow_ups": ["Check bank app"]}
            """;
        var result = DigestTask.ParseResult(sample);
        result.Headline.ShouldBe("Busy day");
        result.Sections[0].Category.ShouldBe("money");
        result.FollowUps.Length.ShouldBe(1);
    }

    [Fact]
    public void T304Schema_CodeDetailsDeserialize()
    {
        const string sample = """
            {"min_spend": 500000, "cap": null, "valid_until": "2026-10-01T00:00:00Z",
             "conditions": ["first order only"]}
            """;
        var result = CodeDetailsTask.ParseResult(sample);
        result.MinSpend.ShouldBe(500000d);
        result.ValidUntil.ShouldNotBeNull();
        result.Conditions.Length.ShouldBe(1);
    }
}

// SLM-008: cancellation must stop generation within 500 ms.
public class FakeEngineTests
{
    private static async Task<string> CollectAsync(
        IInferenceEngine engine, GenerationRequest request, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        await foreach (var chunk in engine.GenerateAsync(request, ct))
            sb.Append(chunk.TextDelta);
        return sb.ToString();
    }

    private static GenerationRequest Request(int maxTokens) => new(
        new[] { new ChatMessage("user", "test") }, maxTokens, 0.2f, 0.9f, null,
        new ResponseFormat.Text());

    [Fact]
    public async Task SLM008_Cancellation_StopsWithin500ms()
    {
        var engine = new FakeInferenceEngine { TokenDelayMs = 30 };
        using var cts = new CancellationTokenSource();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var gen = CollectAsync(engine, Request(1000), cts.Token);
        await Task.Delay(200, CancellationToken.None); // let it stream a few tokens
        cts.Cancel();
        try { await gen; } catch (OperationCanceledException) { }

        sw.Stop();
        sw.ElapsedMilliseconds.ShouldBeLessThanOrEqualTo(600); // 500ms budget + CI slack
    }

    [Fact]
    public async Task ScriptedOutput_DeliveredVerbatim()
    {
        var engine = new FakeInferenceEngine { TokenDelayMs = 0 };
        engine.QueueScripted(@"{""summary"": ""ok""}");
        var outText = await CollectAsync(engine, Request(100), CancellationToken.None);
        outText.ShouldBe(@"{""summary"": ""ok""}");
    }

    [Fact]
    public async Task GarbageMode_ThrowsCleanly()
    {
        var engine = new FakeInferenceEngine { TokenDelayMs = 0, Mode = FakeEngineMode.ThrowOnGenerate };
        await Should.ThrowAsync<InvalidOperationException>(
            () => CollectAsync(engine, Request(100), CancellationToken.None));
    }

    [Fact]
    public async Task MOD002_ProbeAsync_ReturnsPlausibleResult()
    {
        var engine = new FakeInferenceEngine();
        var probe = await engine.ProbeAsync(
            new ModelFileRef(Path: "model.gguf", Sha256: null, SizeBytes: 700_000_000),
            CancellationToken.None);
        probe.Architecture.ShouldBe("llama");
        probe.Quantization.ShouldNotBeNullOrWhiteSpace();
        probe.ParamCountBillions.ShouldBeGreaterThan(0f);
        probe.ContextLength.ShouldBeGreaterThan(0);
        probe.HasChatTemplate.ShouldNotBeNull();
    }
}

// SLM-002: tolerant extraction of JSON from noisy model output.
public class JsonValidatorTests
{
    [Fact]
    public void StripsCodeFences()
    {
        var json = JsonValidator.ExtractJsonObject(
            "Here you go:\n```json\n{\"summary\": \"x\"}\n```\nHope that helps!");
        json.ShouldBe("{\"summary\": \"x\"}");
    }

    [Fact]
    public void ExtractsFirstBalancedObject_FromNoisyOutput()
    {
        var json = JsonValidator.ExtractJsonObject(
            "Sure! {\"a\": 1} and some trailing chatter {\"b\": 2} ignored");
        json.ShouldBe("{\"a\": 1}");
    }

    [Fact]
    public void RepairsTrailingCommas()
    {
        var json = JsonValidator.ExtractJsonObject("{\"summary\": \"x\", \"tags\": [\"a\",],}");
        JsonValidator.TryParse(json!, out _).ShouldBeTrue();
    }

    [Fact]
    public void ReturnsNull_OnTrulyInvalidInput()
    {
        JsonValidator.ExtractJsonObject("no json here at all").ShouldBeNull();
        JsonValidator.TryParse("{broken", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryParse_FindsFirstBalancedObjectInsideText()
    {
        JsonValidator.TryParse("prefix {\"ok\": true} suffix", out var doc).ShouldBeTrue();
        doc.RootElement.GetProperty("ok").GetBoolean().ShouldBeTrue();
    }
}

