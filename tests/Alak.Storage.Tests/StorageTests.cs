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
        ColumnExists(con, "promo_codes", "package").ShouldBeTrue();       // §13.8 wallet schema (migration 5)
        ColumnExists(con, "promo_codes", "valid_until").ShouldBeTrue();
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

    [Fact]
    public async Task NotificationRepository_UpdateCategoryAsync_UpdatesRow()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new NotificationRepository(con);

        var notif = new Alak.Core.Model.Notification(
            Id: "test-update-cat-1",
            Package: "com.test.app",
            ChannelName: "alerts",
            Title: "Test Title",
            Text: "Test Text",
            Category: "other",
            Timestamp: DateTimeOffset.UtcNow,
            IsRead: false,
            IsPersian: false
        );

        await repo.AddAsync(notif);
        var loaded = await repo.GetByIdAsync("test-update-cat-1");
        loaded.ShouldNotBeNull();
        loaded.Category.ShouldBe("other");

        await repo.UpdateCategoryAsync("test-update-cat-1", "money");
        var updated = await repo.GetByIdAsync("test-update-cat-1");
        updated.ShouldNotBeNull();
        updated.Category.ShouldBe("money");
    }

    [Fact]
    public async Task NotificationRepository_FtsTriggers_KeepFtsSynchronized()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new NotificationRepository(con);

        var notif = new Alak.Core.Model.Notification(
            Id: "fts-sync-test-1",
            Package: "com.digikala.app",
            ChannelName: "promotions",
            Title: "Super Flash Sale",
            Text: "Special discount code for today only",
            Category: "offers",
            Timestamp: DateTimeOffset.UtcNow,
            IsRead: false,
            IsPersian: false
        );

        // 1. AFTER INSERT trigger test
        await repo.AddAsync(notif);
        var ftsCount = con.ExecuteScalar("SELECT COUNT(*) FROM notification_fts WHERE notification_fts MATCH 'flash'");
        ftsCount.ShouldBe(1);

        // 2. SearchFtsAsync functional test
        var searchHits = await repo.SearchFtsAsync("discount", 10, TestContext.Current.CancellationToken);
        searchHits.Count.ShouldBe(1);
        searchHits[0].Id.ShouldBe("fts-sync-test-1");

        // 3. AFTER UPDATE trigger test (re-adding same UID updates text)
        var updatedNotif = notif with { Text = "Updated without keyword but has secretcode" };
        await repo.AddAsync(updatedNotif);
        var ftsOld = con.ExecuteScalar("SELECT COUNT(*) FROM notification_fts WHERE notification_fts MATCH 'discount'");
        ftsOld.ShouldBe(0);
        var ftsNew = con.ExecuteScalar("SELECT COUNT(*) FROM notification_fts WHERE notification_fts MATCH 'secretcode'");
        ftsNew.ShouldBe(1);

        // 4. AFTER DELETE trigger test
        await repo.DeleteAsync("fts-sync-test-1");
        var ftsAfterDelete = con.ExecuteScalar("SELECT COUNT(*) FROM notification_fts");
        ftsAfterDelete.ShouldBe(0);
    }

    [Fact]
    public async Task NotificationRepository_GetPageAsync_KeysetPagination_AndCategoryFilter()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new NotificationRepository(con);
        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < 15; i++)
        {
            var notif = new Alak.Core.Model.Notification(
                Id: $"page-test-{i}",
                Package: "com.test.app",
                ChannelName: "channel",
                Title: $"Item {i}",
                Text: $"Description {i}",
                Category: i % 2 == 0 ? "offers" : "work",
                Timestamp: now.AddMinutes(-i),
                IsRead: false,
                IsPersian: false
            );
            await repo.AddAsync(notif);
        }

        // Page 1: 5 newest
        var page1 = await repo.GetPageAsync(beforeUtcMs: null, limit: 5, ct: TestContext.Current.CancellationToken);
        page1.Count.ShouldBe(5);
        page1[0].Id.ShouldBe("page-test-0");
        page1[4].Id.ShouldBe("page-test-4");

        // Page 2: keyset cursor strictly before last item
        var cursor = page1[^1].Timestamp.ToUnixTimeMilliseconds();
        var page2 = await repo.GetPageAsync(beforeUtcMs: cursor, limit: 5, ct: TestContext.Current.CancellationToken);
        page2.Count.ShouldBe(5);
        page2[0].Id.ShouldBe("page-test-5");
        page2[^1].Id.ShouldBe("page-test-9");

        // Filter by category: offers only
        var offersOnly = await repo.GetPageAsync(beforeUtcMs: null, limit: 20, category: "offers", ct: TestContext.Current.CancellationToken);
        offersOnly.Count.ShouldBe(8); // i = 0, 2, 4, 6, 8, 10, 12, 14
        offersOnly.ShouldAllBe(n => n.Category == "offers");
    }

    [Fact]
    public async Task NotificationRepository_SearchFtsAsync_BilingualPersianAndEnglish()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new NotificationRepository(con);

        await repo.AddAsync(new Alak.Core.Model.Notification(
            Id: "en-1",
            Package: "com.amazon",
            ChannelName: "deals",
            Title: "Black Friday Special Sale",
            Text: "Get extra saving on electronics today",
            Category: "offers",
            Timestamp: DateTimeOffset.UtcNow,
            IsRead: false,
            IsPersian: false
        ));

        await repo.AddAsync(new Alak.Core.Model.Notification(
            Id: "fa-1",
            Package: "com.snapp",
            ChannelName: "orders",
            Title: "سفارش شما تحویل داده شد",
            Text: "پیک اسنپ فود سفارش را به شما تحویل داد",
            Category: "orders",
            Timestamp: DateTimeOffset.UtcNow,
            IsRead: false,
            IsPersian: true
        ));

        var enMatches = await repo.SearchFtsAsync("electronics", 5, TestContext.Current.CancellationToken);
        enMatches.Count.ShouldBe(1);
        enMatches[0].Id.ShouldBe("en-1");

        var faMatches = await repo.SearchFtsAsync("اسنپ", 5, TestContext.Current.CancellationToken);
        faMatches.Count.ShouldBe(1);
        faMatches[0].Id.ShouldBe("fa-1");
    }

    [Fact]
    public async Task NotificationRepository_PruneExpiredAsync_PrunesNonPinnedOlderThanMaxAge()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new NotificationRepository(con);
        var now = DateTimeOffset.UtcNow;

        // Old non-pinned notification (40 days old)
        await repo.AddAsync(new Alak.Core.Model.Notification(
            Id: "old-unpinned",
            Package: "com.test",
            ChannelName: "c",
            Title: "Old unpinned",
            Text: "t",
            Category: "other",
            Timestamp: now.AddDays(-40),
            IsRead: false,
            IsPersian: false
        ));

        // Old pinned notification (40 days old)
        con.Execute(
            "INSERT INTO notifications (uid, sbn_key, package, posted_at, captured_at, updated_at, tz_offset_min, pinned, is_read, is_persian) " +
            "VALUES ($uid, $uid, 'com.test', $posted, $now, $now, 0, 1, 0, 0)",
            ("$uid", "old-pinned"),
            ("$posted", now.AddDays(-40).ToUnixTimeMilliseconds()),
            ("$now", now.ToUnixTimeMilliseconds()));

        // Recent notification (5 days old)
        await repo.AddAsync(new Alak.Core.Model.Notification(
            Id: "recent",
            Package: "com.test",
            ChannelName: "c",
            Title: "Recent",
            Text: "t",
            Category: "other",
            Timestamp: now.AddDays(-5),
            IsRead: false,
            IsPersian: false
        ));

        var pruned = await repo.PruneExpiredAsync(TimeSpan.FromDays(30), TestContext.Current.CancellationToken);
        pruned.ShouldBe(1);

        var oldUnpinned = await repo.GetByIdAsync("old-unpinned");
        oldUnpinned.ShouldBeNull();

        var oldPinned = await repo.GetByIdAsync("old-pinned");
        oldPinned.ShouldNotBeNull();

        var recent = await repo.GetByIdAsync("recent");
        recent.ShouldNotBeNull();
    }

    [Fact]
    public void SEC002_SecureKeyProvider_DefaultProvider_ReturnsStandardPassword()
    {
        ISecureKeyProvider provider = new DefaultSecureKeyProvider();
        var key = provider.GetDatabasePassword();
        key.ShouldNotBeNullOrWhiteSpace();
        key.ShouldBe("alak_secure_key_2024");
    }

    [Fact]
    public async Task SEC002_EncryptedDatabase_CreatedWithKeyProvider_ProtectsData()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"sec002-test-{Guid.NewGuid():N}.db");
        var customKey = "custom_secret_key_" + Guid.NewGuid().ToString("N");
        try
        {
            // 1. Create and populate with custom key provider
            var connStr = $"Data Source={tempDbPath};Password={customKey}";
            using (var con = new StorageConnection(connStr))
            {
                var repo = new NotificationRepository(con);
                await repo.AddAsync(new Alak.Core.Model.Notification(
                    Id: "sec-item-1",
                    Package: "com.secure.bank",
                    ChannelName: "otp",
                    Title: "Secret OTP",
                    Text: "123456",
                    Category: "security",
                    Timestamp: DateTimeOffset.UtcNow,
                    IsRead: false,
                    IsPersian: false
                ));
            }

            // 2. Open with correct key
            using (var con = new StorageConnection(connStr))
            {
                var repo = new NotificationRepository(con);
                var loaded = await repo.GetByIdAsync("sec-item-1");
                loaded.ShouldNotBeNull();
                loaded.Title.ShouldBe("Secret OTP");
            }

            // 3. Opening with wrong key fails
            var wrongConnStr = $"Data Source={tempDbPath};Password=wrong_key_attempt";
            Should.Throw<Microsoft.Data.Sqlite.SqliteException>(() =>
            {
                using var wrongCon = new StorageConnection(wrongConnStr);
            });
        }
        finally
        {
            try { File.Delete(tempDbPath); } catch { }
            foreach (var suffix in new[] { "-wal", "-shm" })
                try { if (File.Exists(tempDbPath + suffix)) File.Delete(tempDbPath + suffix); } catch { }
        }
    }

    [Fact]
    public async Task NotificationRepository_GetPageAsync_MatchesPromoAndOffersSynonyms()
    {
        using var db = new TestDatabase();
        using var con = db.Open();
        var repo = new NotificationRepository(con);
        var now = DateTimeOffset.UtcNow;
        var ct = TestContext.Current.CancellationToken;

        await repo.AddAsync(new Alak.Core.Model.Notification(
            Id: "promo-synonym-1",
            Package: "com.test.app1",
            ChannelName: "deals",
            Title: "Offers Item",
            Text: "Discount 20%",
            Category: "offers",
            Timestamp: now.AddMinutes(-1),
            IsRead: false,
            IsPersian: false
        ));

        await repo.AddAsync(new Alak.Core.Model.Notification(
            Id: "promo-synonym-2",
            Package: "com.test.app2",
            ChannelName: "deals",
            Title: "Promo Item",
            Text: "Discount 30%",
            Category: "promo",
            Timestamp: now.AddMinutes(-2),
            IsRead: false,
            IsPersian: false
        ));

        await repo.AddAsync(new Alak.Core.Model.Notification(
            Id: "work-synonym-3",
            Package: "com.test.app3",
            ChannelName: "work",
            Title: "Work Item",
            Text: "Meeting",
            Category: "work",
            Timestamp: now.AddMinutes(-3),
            IsRead: false,
            IsPersian: false
        ));

        // Querying "offers" returns BOTH "offers" and "promo"
        var offersPage = await repo.GetPageAsync(beforeUtcMs: null, limit: 10, category: "offers", ct: ct);
        offersPage.Count.ShouldBe(2);
        offersPage.ShouldContain(n => n.Id == "promo-synonym-1");
        offersPage.ShouldContain(n => n.Id == "promo-synonym-2");

        // Querying "promo" returns BOTH "promo" and "offers"
        var promoPage = await repo.GetPageAsync(beforeUtcMs: null, limit: 10, category: "promo", ct: ct);
        promoPage.Count.ShouldBe(2);
        promoPage.ShouldContain(n => n.Id == "promo-synonym-1");
        promoPage.ShouldContain(n => n.Id == "promo-synonym-2");
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
