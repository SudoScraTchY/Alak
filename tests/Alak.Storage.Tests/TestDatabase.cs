namespace Alak.Storage.Tests;

/// <summary>Each test gets a fresh temp-file database with real migrations applied.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly string _path;

    public TestDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"alak-test-{Guid.NewGuid():N}.db");
    }

    public StorageConnection Open() => new($"Data Source={_path}");

    public void Dispose()
    {
        try { File.Delete(_path); } catch { /* temp */ }
        try { File.Delete(_path + "-wal"); } catch { }
        try { File.Delete(_path + "-shm"); } catch { }
    }
}
