namespace Alak.Storage;

/// <summary>Local model registry and benchmark cache (MOD-003/MOD-006).</summary>
public interface IModelRepository
{
    Task UpsertModelAsync(
        string sha256,
        string? path,
        string? name,
        string? quant,
        long bytes,
        string? sourceUrl,
        CancellationToken ct = default);

    Task<string?> GetShaForPathAsync(string path, CancellationToken ct = default);

    Task SaveBenchmarkAsync(string sha256, string benchmarkJson, CancellationToken ct = default);

    Task<string?> GetBenchmarkAsync(string sha256, CancellationToken ct = default);
}

public sealed class ModelRepository : IModelRepository
{
    private readonly StorageConnection _db;

    public ModelRepository(StorageConnection db)
    {
        _db = db;
    }

    public Task UpsertModelAsync(
        string sha256,
        string? path,
        string? name,
        string? quant,
        long bytes,
        string? sourceUrl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sha256)) return Task.CompletedTask;
        ct.ThrowIfCancellationRequested();

        lock (_db.SyncRoot)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _db.Execute(
                @"
                INSERT INTO models(sha256, path, name, quant, bytes, created_at, source_url, last_used_at)
                VALUES (@sha, @path, @name, @quant, @bytes, @t, @url, @t)
                ON CONFLICT(sha256) DO UPDATE SET
                    path = COALESCE(excluded.path, models.path),
                    name = COALESCE(excluded.name, models.name),
                    quant = COALESCE(excluded.quant, models.quant),
                    bytes = excluded.bytes,
                    source_url = COALESCE(excluded.source_url, models.source_url),
                    last_used_at = excluded.last_used_at;",
                ("@sha", sha256),
                ("@path", path),
                ("@name", name),
                ("@quant", quant),
                ("@bytes", bytes),
                ("@t", now),
                ("@url", sourceUrl));
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetShaForPathAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path)) return Task.FromResult<string?>(null);
        ct.ThrowIfCancellationRequested();

        lock (_db.SyncRoot)
        {
            var sha = _db.ExecuteScalarText("SELECT sha256 FROM models WHERE path = @p LIMIT 1;", ("@p", path));
            return Task.FromResult(string.IsNullOrEmpty(sha) ? null : sha);
        }
    }

    public Task SaveBenchmarkAsync(string sha256, string benchmarkJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sha256)) return Task.CompletedTask;
        ct.ThrowIfCancellationRequested();

        lock (_db.SyncRoot)
        {
            _db.Execute(
                "UPDATE models SET benchmark_json = @json, last_used_at = @t WHERE sha256 = @sha;",
                ("@json", benchmarkJson),
                ("@t", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                ("@sha", sha256));
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetBenchmarkAsync(string sha256, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sha256)) return Task.FromResult<string?>(null);
        ct.ThrowIfCancellationRequested();

        lock (_db.SyncRoot)
        {
            var json = _db.ExecuteScalarText("SELECT benchmark_json FROM models WHERE sha256 = @sha;", ("@sha", sha256));
            return Task.FromResult(string.IsNullOrEmpty(json) ? null : json);
        }
    }
}
