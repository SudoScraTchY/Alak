using Alak.Storage;

namespace Alak.Storage.Tests;

/// <summary>
/// M2 exit criteria (spec §16): 10k synthetic rows insert, keyset-paginate,
/// full-text search in both languages, and prune correctly under test.
/// </summary>
public class M2ExitCriteriaTests
{
    [Fact]
    public void M2_10kRows_InsertQueryFts_PruneCorrectly()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alak-m2-{Guid.NewGuid():N}.db");
        try
        {
            using var db = new StorageConnection($"Data Source={path}");
            var clock = new FixedStorageClock(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));
            var store = new NotificationStore(db, clock);
            var baseMs = clock.UtcNow().ToUnixTimeMilliseconds();

            // ---- insert 10,000 synthetic rows (mixed categories, bilingual text) ----
            var cats = new[] { "offers", "work", "people", "money", "orders", "security", "social", "updates", "other" };
            var inserted = 0;
            for (var i = 0; i < 10_000; i++)
            {
                var cat = cats[i % cats.Length];
                var title = (i % 2 == 0) ? $"Sale event {i}" : $"رویداد فروش {i}";
                var text  = (i % 2 == 0) ? "big sale today free shipping" : "حر فروش بزرگ امروز ارسال رایگان";
                store.Insert(new NotificationInsert(
                    Uid: Guid.NewGuid().ToString("N"),
                    SbnKey: "sbn-" + i,
                    Package: "com.test.app",
                    PostedAt: baseMs - i * 86_400_000L, // one per DAY: 10k days back; newest first
                    Category: cat,
                    Title: title,
                    Text: text,
                    Pinned: i % 500 == 0)); // 20 pinned rows scattered through history
                inserted++;
            }
            inserted.ShouldBe(10_000);
            db.ExecuteScalar("SELECT COUNT(*) FROM notifications").ShouldBe(10_000);

            // ---- keyset pagination walks ALL 10k rows with zero overlap ----
            long? cursor = null;
            var seen = new HashSet<long>();
            var pages = 0;
            while (true)
            {
                var page = store.Page(cursor, limit: 500);
                if (page.Count == 0) break;
                pages++;
                foreach (var item in page) seen.Add(item.Id);
                cursor = page[^1].PostedAt;
            }
            seen.Count.ShouldBe(10_000);
            pages.ShouldBe(20); // 10k / 500

            // ---- bilingual FTS: EN and FA phrases both match ----
            // (rows are inserted above without FTS sync; do it the way the pipeline would)
            using (var cmd = db.Underlying.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO notification_fts(rowid, title_n, body_n, sender_n)
                    SELECT id,
                           COALESCE(title, ''),
                           COALESCE(text, ''),
                           COALESCE(package, '')
                    FROM notifications
                    """;
                cmd.ExecuteNonQuery();
            }
            // sanity: the FTS insert actually landed rows
            db.ExecuteScalar("SELECT COUNT(*) FROM notification_fts").ShouldBe(10_000);
            db.ExecuteScalar(
                "SELECT COUNT(*) FROM notification_fts WHERE notification_fts MATCH 'sale'")
                .ShouldBe(5_000);
            // Persian text is normalized (Arabic-script folds via unicode61 remove_diacritics)
            db.ExecuteScalar(
                "SELECT COUNT(*) FROM notification_fts WHERE notification_fts MATCH 'فروش'")
                .ShouldBe(5_000);

            // ---- prune correctly: offers rows older than 30 days vanish, pinned survive ----
            var before = db.ExecuteScalar("SELECT COUNT(*) FROM notifications");
            var oldPinnedOffers = db.ExecuteScalar(
                "SELECT COUNT(*) FROM notifications WHERE category='offers' AND pinned=1 AND posted_at < $t",
                ("$t", baseMs - 30L * 86_400_000L));
            var removed = store.PruneExpired();
            removed.ShouldBeGreaterThan(0);

            // every surviving row is either pinned or within its category window
            var stray = db.ExecuteScalar(
                "SELECT COUNT(*) FROM notifications WHERE pinned=0 AND category='offers' AND posted_at < $t",
                ("$t", baseMs - 30L * 86_400_000L));
            stray.ShouldBe(0);

            // security rows (7-day window): nothing older than 7 days and non-pinned remains
            var straySec = db.ExecuteScalar(
                "SELECT COUNT(*) FROM notifications WHERE pinned=0 AND category='security' AND posted_at < $t",
                ("$t", baseMs - 7L * 86_400_000L));
            straySec.ShouldBe(0);

            // pinned rows all survived
            db.ExecuteScalar("SELECT COUNT(*) FROM notifications WHERE pinned=1").ShouldBe(20);
            db.ExecuteScalar("SELECT COUNT(*) FROM notifications").ShouldBeLessThan(before);
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(path + "-wal"); } catch { }
            try { File.Delete(path + "-shm"); } catch { }
        }
    }
}
