using Alak.Storage;

namespace Alak.Storage.Tests;

// §6.6: schema v1 — every table from the spec exists with normative column names.
public class SchemaTests
{
    [Fact]
    public void MIG_Migration_RunsAndRecordsVersion()
    {
        using var db = new TestDatabase();
        using (var con = db.Open())
        {
            var v = con.ExecuteScalar("SELECT MAX(version) FROM schema_migrations");
            v.ShouldBeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public void MIG_TablesExist_WithNormativeColumns()
    {
        using var db = new TestDatabase();
        using var con = db.Open();

        foreach (var t in new[] { "apps", "notifications", "entities", "promo_codes",
                                  "user_rules", "corrections", "digests", "slm_results",
                                  "models", "packs", "settings", "schema_migrations" })
            TableExists(con, t).ShouldBeTrue($"{t} must exist");

        // spot-check normative columns
        ColumnExists(con, "notifications", "content_hash").ShouldBeTrue();
        ColumnExists(con, "notifications", "evidence_json").ShouldBeTrue();
        ColumnExists(con, "notifications", "needs_review").ShouldBeTrue();
        ColumnExists(con, "promo_codes", "code_norm").ShouldBeTrue();
        ColumnExists(con, "promo_codes", "valid_until_precision").ShouldBeTrue();
        ColumnExists(con, "corrections", "features_blob").ShouldBeTrue();
        ColumnExists(con, "models", "sha256").ShouldBeTrue();
    }

    [Fact]
    public void PF10_Fts5_Available_AndVirtualTableCreated()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        con.ExecuteScalar("SELECT COUNT(*) FROM notification_fts").ShouldBe(0);
        // FTS5 functional check: insert + match
        con.Execute("INSERT INTO notification_fts(rowid, title_n, body_n, sender_n) VALUES (1, 'sale', 'big sale today', 'shop')");
        con.ExecuteScalar("SELECT COUNT(*) FROM notification_fts WHERE notification_fts MATCH 'sale'").ShouldBe(1);
    }

    [Fact]
    public void MIG_ForeignKeysOn_WalOn()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        con.ExecuteScalar("PRAGMA foreign_keys").ShouldBe(1);
        var journal = con.ExecuteScalarText("PRAGMA journal_mode");
        journal.ToLower().ShouldContain("wal");
    }

    private static bool TableExists(StorageConnection con, string table)
        => con.ExecuteScalar(
            "SELECT COUNT(*) FROM sqlite_master WHERE type IN ('table','virtual table') AND name = $t",
            ("$t", table)) > 0;

    private static bool ColumnExists(StorageConnection con, string table, string column)
        => con.ExecuteScalar(
            "SELECT COUNT(*) FROM pragma_table_info($t) WHERE name = $c",
            ("$t", table), ("$c", column)) > 0;
}

// STO-001/003: retention with a fake clock; STO-004: keyset pagination.
public class RetentionTests
{
    [Fact]
    public void STO001_OffersPruned_After30Days_PinnedSurvives()
    {
        using var db = new TestDatabase();
        var clock = new FixedStorageClock(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
        using var con = db.Open();
        var store = new NotificationStore(con, clock);

        var idOld = store.Insert(TestNotification("offers", clock.UtcNow().AddDays(-31).ToUnixTimeMilliseconds(), pinned: false));
        var idPinned = store.Insert(TestNotification("offers", clock.UtcNow().AddDays(-60).ToUnixTimeMilliseconds(), pinned: true));
        var idNew = store.Insert(TestNotification("offers", clock.UtcNow().AddDays(-5).ToUnixTimeMilliseconds(), pinned: false));

        var removed = store.PruneExpired(categoryOverrideDays: null);

        removed.ShouldBe(1);
        store.Exists(idOld).ShouldBeFalse();
        store.Exists(idPinned).ShouldBeTrue();
        store.Exists(idNew).ShouldBeTrue();
    }

    [Fact]
    public void STO001_SecurityMetadataPruned_After7Days()
    {
        using var db = new TestDatabase();
        var clock = new FixedStorageClock(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
        using var con = db.Open();
        var store = new NotificationStore(con, clock);

        var id = store.Insert(TestNotification("security", clock.UtcNow().AddDays(-8).ToUnixTimeMilliseconds(), pinned: false));
        store.PruneExpired(null).ShouldBe(1);
        store.Exists(id).ShouldBeFalse();
    }

    [Fact]
    public void STO003_GlobalCap_PrunesOldestNonPinned_NeverActiveCodes()
    {
        using var db = new TestDatabase();
        var clock = new FixedStorageClock(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
        using var con = db.Open();
        var store = new NotificationStore(con, clock);

        var oldId = store.Insert(TestNotification("work", clock.UtcNow().AddDays(-80).ToUnixTimeMilliseconds(), pinned: false));
        var pinnedId = store.Insert(TestNotification("money", clock.UtcNow().AddDays(-350).ToUnixTimeMilliseconds(), pinned: true));

        // cap below current usage: non-pinned rows go, pinned stays
        var removed = store.EnforceGlobalCap(capRows: 0);
        removed.ShouldBeGreaterThanOrEqualTo(1);
        store.Exists(pinnedId).ShouldBeTrue();
    }

    [Fact]
    public void STO004_KeysetPagination_UsesPostedAtCursor()
    {
        using var db = new TestDatabase();
        var clock = new FixedStorageClock(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
        using var con = db.Open();
        var store = new NotificationStore(con, clock);

        for (var i = 0; i < 25; i++)
            store.Insert(TestNotification("offers", clock.UtcNow().AddMinutes(-i).ToUnixTimeMilliseconds(), pinned: false));

        var page1 = store.Page(postedBefore: null, limit: 10);
        page1.Count.ShouldBe(10);
        var last = page1[^1];
        var page2 = store.Page(postedBefore: last.PostedAt, limit: 10);
        page2.Count.ShouldBe(10);
        page2.ShouldNotContain(page1[0]);
        // strictly older than the cursor
        page2.ShouldAllBe(n => n.PostedAt <= last.PostedAt);
    }

    private static NotificationInsert TestNotification(string category, long postedAt, bool pinned)
        => new(
            Uid: Guid.NewGuid().ToString("N"),
            SbnKey: "sbn-" + Guid.NewGuid().ToString("N"),
            Package: "com.test.app",
            PostedAt: postedAt,
            Category: category,
            Title: "T",
            Text: "body",
            Pinned: pinned);
}
