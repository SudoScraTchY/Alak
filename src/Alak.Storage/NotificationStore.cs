using Microsoft.Data.Sqlite;

namespace Alak.Storage;

/// <summary>Storage-side clock port (ARC-003); retention tests pin time with FixedStorageClock.</summary>
public interface IStorageClock
{
    DateTimeOffset UtcNow();
}

public sealed class FixedStorageClock(DateTimeOffset at) : IStorageClock
{
    public DateTimeOffset UtcNow() => at;
}

public sealed class SystemStorageClock : IStorageClock
{
    public DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;
}

/// <summary>Projection of one notifications row for list queries (STO-004).</summary>
public sealed record NotificationListItem(
    long Id, string Uid, string Package, long PostedAt, string Category,
    bool Pinned, int State);

/// <summary>Insert payload for tests and the capture pipeline (§6.6).</summary>
public sealed record NotificationInsert(
    string Uid, string SbnKey, string Package, long PostedAt,
    string Category, string? Title, string? Text, bool Pinned);

/// <summary>
/// Typed operations on the notifications tables (spec §6.6/§6.7):
/// retention (STO-001/002/003) and keyset pagination (STO-004).
/// </summary>
public sealed class NotificationStore
{
    private readonly StorageConnection _db;
    private readonly IStorageClock _clock;

    public NotificationStore(StorageConnection db, IStorageClock clock)
    {
        _db = db;
        _clock = clock;
    }

    // STO-001 defaults per category (days). Money 365, Security 7, People 90, Offers 30 …
    private static readonly Dictionary<string, int> s_retentionDays = new()
    {
        ["offers"] = 30, ["social"] = 30, ["updates"] = 30, ["other"] = 30,
        ["work"] = 90, ["orders"] = 90, ["people"] = 90,
        ["money"] = 365, ["security"] = 7,
    };

    public long Insert(NotificationInsert n)
    {
        // ensure the apps row exists (FK target)
        _db.Execute(
            "INSERT OR IGNORE INTO apps(package, first_seen_at) VALUES ($p, $t)",
            ("$p", n.Package), ("$t", _clock.UtcNow().ToUnixTimeMilliseconds()));

        var now = _clock.UtcNow().ToUnixTimeMilliseconds();
        var contentHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes((n.Title ?? "") + "|" + (n.Text ?? ""))));

        using var cmd = _db.Underlying.CreateCommand();
        cmd.CommandText = """
            INSERT INTO notifications(uid, sbn_key, package, posted_at, captured_at, updated_at,
                                      tz_offset_min, depth, title, text, category, content_hash,
                                      pinned, state)
            VALUES ($uid, $key, $pkg, $posted, $now, $now, 0, 0, $title, $text, $cat, $hash, $pin, 0);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$uid", n.Uid);
        cmd.Parameters.AddWithValue("$key", n.SbnKey);
        cmd.Parameters.AddWithValue("$pkg", n.Package);
        cmd.Parameters.AddWithValue("$posted", n.PostedAt);
        cmd.Parameters.AddWithValue("$now", now);
        cmd.Parameters.AddWithValue("$title", (object?)n.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$text", (object?)n.Text ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cat", n.Category);
        cmd.Parameters.AddWithValue("$hash", contentHash);
        cmd.Parameters.AddWithValue("$pin", n.Pinned ? 1 : 0);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public bool Exists(long id)
        => _db.ExecuteScalar("SELECT COUNT(*) FROM notifications WHERE id = $i", ("$i", id)) > 0;

    /// <summary>STO-001: prune rows past their category's retention window (pinned never expire).</summary>
    public int PruneExpired(int? categoryOverrideDays = null)
    {
        var now = _clock.UtcNow().ToUnixTimeMilliseconds();
        var removed = 0;
        foreach (var (category, days) in s_retentionDays)
        {
            var d = categoryOverrideDays ?? days;
            var cutoff = now - d * 86_400_000L;
            removed += _db.ExecuteDelete(
                "DELETE FROM notifications WHERE pinned = 0 AND category = $c AND posted_at < $t",
                ("$c", category), ("$t", cutoff));
        }
        return removed;
    }

    /// <summary>STO-003: enforce a global cap by pruning oldest non-pinned rows first.</summary>
    public int EnforceGlobalCap(int capRows)
    {
        var total = _db.ExecuteScalar("SELECT COUNT(*) FROM notifications WHERE pinned = 0");
        if (total <= capRows) return 0;

        var toRemove = total - capRows;
        _db.Execute(
            """
            DELETE FROM notifications WHERE id IN (
              SELECT id FROM notifications WHERE pinned = 0
              ORDER BY posted_at ASC LIMIT $n)
            """,
            ("$n", toRemove));
        return (int)toRemove;
    }

    /// <summary>STO-004: keyset pagination — cursor is posted_at, never OFFSET.</summary>
    public List<NotificationListItem> Page(long? postedBefore, int limit)
    {
        var result = new List<NotificationListItem>();
        using var cmd = _db.Underlying.CreateCommand();
        cmd.CommandText = """
            SELECT id, uid, package, posted_at, category, pinned, state
            FROM notifications
            WHERE ($cursor IS NULL OR posted_at < $cursor)
            ORDER BY posted_at DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$cursor", (object?)postedBefore ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$limit", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            result.Add(new NotificationListItem(
                r.GetInt64(0), r.GetString(1), r.GetString(2),
                r.GetInt64(3), r.GetString(4), r.GetInt64(5) != 0, r.GetInt32(6)));
        return result;
    }
}
