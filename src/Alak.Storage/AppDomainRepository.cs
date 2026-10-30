using Alak.Core.Capture;

namespace Alak.Storage;

/// <summary>
/// Learn-and-match index from link domains to the app that posted them.
/// Used only for notifications that arrive through an SMS app: no network access,
/// no fetching, no redirects — matching is a local lookup over previously observed
/// domains (see ADR-0003).
/// </summary>
public interface IAppDomainRepository
{
    /// <summary>Records that <paramref name="package"/> posted links to these domains.</summary>
    Task LearnAsync(IReadOnlyCollection<string> domains, string package, CancellationToken ct = default);

    /// <summary>
    /// Returns the attributed package when exactly one non-communication app has
    /// been seen at least <paramref name="minHits"/> times for these domains.
    /// Ambiguous or unknown domains return null.
    /// </summary>
    Task<string?> ResolvePackageAsync(IReadOnlyCollection<string> domains, int minHits = 2, CancellationToken ct = default);
}

public sealed class AppDomainRepository : IAppDomainRepository
{
    private readonly StorageConnection _db;

    public AppDomainRepository(StorageConnection db)
    {
        _db = db;
    }

    public Task LearnAsync(IReadOnlyCollection<string> domains, string package, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(package) || domains.Count == 0) return Task.CompletedTask;
        if (CommunicationAppDetector.IsCommunicationApp(package)) return Task.CompletedTask;

        ct.ThrowIfCancellationRequested();

        lock (_db.SyncRoot)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var tx = _db.BeginTransaction();
            foreach (var host in domains.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(host)) continue;

                _db.Execute(
                    @"
                    INSERT INTO app_domains(host, package, hits, first_seen_at, last_seen_at)
                    VALUES (@h, @p, 1, @t, @t)
                    ON CONFLICT(host, package) DO UPDATE SET
                        hits = app_domains.hits + 1,
                        last_seen_at = excluded.last_seen_at;",
                    tx,
                    ("@h", host),
                    ("@p", package),
                    ("@t", now));
            }

            tx.Commit();
        }

        return Task.CompletedTask;
    }

    public Task<string?> ResolvePackageAsync(
        IReadOnlyCollection<string> domains,
        int minHits = 2,
        CancellationToken ct = default)
    {
        if (domains.Count == 0) return Task.FromResult<string?>(null);
        ct.ThrowIfCancellationRequested();

        lock (_db.SyncRoot)
        {
            var votes = new Dictionary<string, long>(StringComparer.Ordinal);

            foreach (var host in domains.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(host)) continue;

                using var cmd = _db.CreateCommand();
                cmd.CommandText = @"
                    SELECT package, SUM(hits) AS total
                    FROM app_domains
                    WHERE host = @h
                    GROUP BY package
                    HAVING SUM(hits) >= @min;";
                cmd.Parameters.AddWithValue("@h", host);
                cmd.Parameters.AddWithValue("@min", minHits);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    var package = reader.GetString(0);
                    if (CommunicationAppDetector.IsCommunicationApp(package)) continue;

                    var hits = reader.GetInt64(1);
                    votes[package] = votes.TryGetValue(package, out var existing)
                        ? Math.Max(existing, hits)
                        : hits;
                }
            }

            if (votes.Count != 1) return Task.FromResult<string?>(null);
            return Task.FromResult<string?>(votes.Keys.First());
        }
    }
}
