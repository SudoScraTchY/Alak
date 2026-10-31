using Alak.Storage;

namespace Alak.Storage.Tests;

public class ModelRepositoryTests : IDisposable
{
    private readonly TestDatabase _db;
    private readonly StorageConnection _connection;
    private readonly ModelRepository _models;

    public ModelRepositoryTests()
    {
        _db = new TestDatabase();
        _connection = _db.Open();
        _models = new ModelRepository(_connection);
    }

    public void Dispose()
    {
        _connection.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task Upsert_TracksPathAndSha()
    {
        await _models.UpsertModelAsync("abc123", "/models/q.gguf", "Qwen", "Q4_K_M", 397_808_192, null);

        (await _models.GetShaForPathAsync("/models/q.gguf")).ShouldBe("abc123");
        (await _models.GetShaForPathAsync("/models/other.gguf")).ShouldBeNull();
    }

    [Fact]
    public async Task Benchmark_RoundTripsAsJson()
    {
        await _models.UpsertModelAsync("abc123", "/models/q.gguf", "Qwen", "Q4_K_M", 1, null);
        await _models.SaveBenchmarkAsync("abc123", "{\"loadMs\":1046,\"label\":\"Good\"}");

        var json = await _models.GetBenchmarkAsync("abc123");
        json.ShouldNotBeNull();
        json!.ShouldContain("\"label\":\"Good\"");
    }
}
