using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Alak.Storage;
using Shouldly;
using Xunit;

namespace Alak.Storage.Tests;

public class BackupServiceTests
{
    private const string TestPassphrase = "SuperSecretPassphrase123!@#";

    [Fact]
    public async Task ExportAndInspect_ReturnsAccurateMetadata()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new TestDatabase();
        using var con = db.Open();
        var tempWeightsPath = Path.Combine(Path.GetTempPath(), $"adaptive-test-{Guid.NewGuid():N}.bin");
        await File.WriteAllBytesAsync(tempWeightsPath, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, ct);

        try
        {
            SeedSampleData(con);

            var backupService = new BackupService(con, tempWeightsPath);
            using var mem = new MemoryStream();
            await backupService.ExportBackupAsync(mem, TestPassphrase, ct);

            mem.Position = 0;
            var metadata = await backupService.InspectBackupAsync(mem, TestPassphrase, ct);

            metadata.ShouldNotBeNull();
            metadata.Version.ShouldBe(1);
            metadata.NotificationCount.ShouldBe(2);
            metadata.UserRuleCount.ShouldBe(1);
            metadata.PromoCodeCount.ShouldBe(1);
            metadata.HasAdaptiveWeights.ShouldBeTrue();
        }
        finally
        {
            try { File.Delete(tempWeightsPath); } catch { }
        }
    }

    [Fact]
    public async Task WrongPassphrase_ThrowsCryptographicException()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new TestDatabase();
        using var con = db.Open();
        SeedSampleData(con);

        var backupService = new BackupService(con);
        using var mem = new MemoryStream();
        await backupService.ExportBackupAsync(mem, TestPassphrase, ct);

        mem.Position = 0;
        await Should.ThrowAsync<CryptographicException>(async () =>
        {
            await backupService.InspectBackupAsync(mem, "WrongPasswordTotallyInvalid", ct);
        });
    }

    [Fact]
    public async Task CorruptedCiphertext_ThrowsCryptographicException()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new TestDatabase();
        using var con = db.Open();
        SeedSampleData(con);

        var backupService = new BackupService(con);
        using var mem = new MemoryStream();
        await backupService.ExportBackupAsync(mem, TestPassphrase, ct);

        // Corrupt a byte in the ciphertext payload (after the 28-byte header + 12-byte nonce + 16-byte tag = 56 bytes)
        var backupBytes = mem.ToArray();
        backupBytes.Length.ShouldBeGreaterThan(60);
        backupBytes[^5] ^= 0xFF; // Flip bits in ciphertext

        using var corruptedStream = new MemoryStream(backupBytes);
        await Should.ThrowAsync<CryptographicException>(async () =>
        {
            await backupService.RestoreBackupAsync(corruptedStream, TestPassphrase, RestoreMode.Replace, ct);
        });
    }

    [Fact]
    public async Task InvalidHeaderMagic_ThrowsInvalidDataException()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new TestDatabase();
        using var con = db.Open();
        SeedSampleData(con);

        var backupService = new BackupService(con);
        using var mem = new MemoryStream();
        await backupService.ExportBackupAsync(mem, TestPassphrase, ct);

        var backupBytes = mem.ToArray();
        backupBytes[0] = (byte)'X'; // Invalidate magic header

        using var corruptedStream = new MemoryStream(backupBytes);
        await Should.ThrowAsync<InvalidDataException>(async () =>
        {
            await backupService.InspectBackupAsync(corruptedStream, TestPassphrase, ct);
        });
    }

    [Fact]
    public async Task FullRestoreRoundTrip_ReplaceMode_RestoresAllDataAndWeights()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sourceDb = new TestDatabase();
        using var sourceCon = sourceDb.Open();
        var weightsPath = Path.Combine(Path.GetTempPath(), $"adaptive-test-{Guid.NewGuid():N}.bin");
        var sampleWeights = Encoding.UTF8.GetBytes("SampleAdaptiveWeightsDataBinaryContent");
        await File.WriteAllBytesAsync(weightsPath, sampleWeights, ct);

        using var targetDb = new TestDatabase();
        using var targetCon = targetDb.Open();
        var restoredWeightsPath = Path.Combine(Path.GetTempPath(), $"adaptive-restored-{Guid.NewGuid():N}.bin");

        try
        {
            SeedSampleData(sourceCon);

            // Export from source
            var sourceBackup = new BackupService(sourceCon, weightsPath);
            using var backupStream = new MemoryStream();
            await sourceBackup.ExportBackupAsync(backupStream, TestPassphrase, ct);

            // Pre-seed target with conflicting/dummy data to verify Replace mode wipes it
            targetCon.Execute("INSERT INTO apps (package, first_seen_at) VALUES ('com.dummy.app', 12345);");
            targetCon.Execute(@"
                INSERT INTO notifications (uid, sbn_key, package, posted_at, captured_at, updated_at, tz_offset_min)
                VALUES ('dummy_uid', 'dummy_key', 'com.dummy.app', 99999, 99999, 99999, 0);");

            // Restore into target with Replace mode
            backupStream.Position = 0;
            var targetBackup = new BackupService(targetCon, restoredWeightsPath);
            await targetBackup.RestoreBackupAsync(backupStream, TestPassphrase, RestoreMode.Replace, ct);

            // Assertions on Target DB
            var notifCount = targetCon.ExecuteScalar("SELECT COUNT(*) FROM notifications;");
            notifCount.ShouldBe(2);

            var dummyExists = targetCon.ExecuteScalar("SELECT COUNT(*) FROM notifications WHERE uid = 'dummy_uid';");
            dummyExists.ShouldBe(0);

            var promoCount = targetCon.ExecuteScalar("SELECT COUNT(*) FROM promo_codes;");
            promoCount.ShouldBe(1);

            var ruleCount = targetCon.ExecuteScalar("SELECT COUNT(*) FROM user_rules;");
            ruleCount.ShouldBe(1);

            var entityCount = targetCon.ExecuteScalar("SELECT COUNT(*) FROM entities;");
            entityCount.ShouldBe(1);

            // Assert adaptive weights restored
            File.Exists(restoredWeightsPath).ShouldBeTrue();
            var restoredBytes = await File.ReadAllBytesAsync(restoredWeightsPath, ct);
            restoredBytes.ShouldBe(sampleWeights);
        }
        finally
        {
            try { File.Delete(weightsPath); } catch { }
            try { File.Delete(restoredWeightsPath); } catch { }
        }
    }

    [Fact]
    public async Task FullRestoreRoundTrip_MergeMode_PreservesExistingAndAddsNew()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sourceDb = new TestDatabase();
        using var sourceCon = sourceDb.Open();

        using var targetDb = new TestDatabase();
        using var targetCon = targetDb.Open();

        SeedSampleData(sourceCon);

        // Export from source (contains notif_1 and notif_2)
        var sourceBackup = new BackupService(sourceCon);
        using var backupStream = new MemoryStream();
        await sourceBackup.ExportBackupAsync(backupStream, TestPassphrase, ct);

        // Target already has notif_1 with customized title, and an existing notif_0
        targetCon.Execute("INSERT INTO apps (package, first_seen_at) VALUES ('com.snapp.taxi', 1000);");
        targetCon.Execute(@"
            INSERT INTO notifications (uid, sbn_key, package, posted_at, captured_at, updated_at, tz_offset_min, title)
            VALUES ('notif_0', 'key_0', 'com.snapp.taxi', 500, 500, 500, 0, 'Target Existing Item');");
        targetCon.Execute(@"
            INSERT INTO notifications (uid, sbn_key, package, posted_at, captured_at, updated_at, tz_offset_min, title)
            VALUES ('notif_1', 'key_1', 'com.snapp.taxi', 1000, 1000, 1000, 0, 'Target Modified Title');");

        // Restore into target with Merge mode
        backupStream.Position = 0;
        var targetBackup = new BackupService(targetCon);
        await targetBackup.RestoreBackupAsync(backupStream, TestPassphrase, RestoreMode.Merge, ct);

        // Target should now have 3 notifications: notif_0, notif_1 (preserved), and notif_2 (added)
        var totalNotifs = targetCon.ExecuteScalar("SELECT COUNT(*) FROM notifications;");
        totalNotifs.ShouldBe(3);

        // notif_1 title should remain what was in target (Merge does not overwrite existing uid)
        var title1 = targetCon.ExecuteScalarText("SELECT title FROM notifications WHERE uid = 'notif_1';");
        title1.ShouldBe("Target Modified Title");

        // notif_2 should exist
        var title2 = targetCon.ExecuteScalarText("SELECT title FROM notifications WHERE uid = 'notif_2';");
        title2.ShouldBe("Digikala Order Delivery");
    }

    [Fact]
    public async Task BAK001_ExcludeSlmResults_NeverExported()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sourceDb = new TestDatabase();
        using var sourceCon = sourceDb.Open();

        SeedSampleData(sourceCon);

        // Insert ephemeral slm_result
        sourceCon.Execute(@"
            INSERT INTO slm_results (notification_uid, intent, confidence, latency_ms, model_sha, created_at)
            VALUES ('notif_1', 'explain', 0.95, 120, 'sha256abc', 10000);");

        var sourceBackup = new BackupService(sourceCon);
        using var backupStream = new MemoryStream();
        await sourceBackup.ExportBackupAsync(backupStream, TestPassphrase, ct);

        using var targetDb = new TestDatabase();
        using var targetCon = targetDb.Open();
        backupStream.Position = 0;

        var targetBackup = new BackupService(targetCon);
        await targetBackup.RestoreBackupAsync(backupStream, TestPassphrase, RestoreMode.Replace, ct);

        var slmCount = targetCon.ExecuteScalar("SELECT COUNT(*) FROM slm_results;");
        slmCount.ShouldBe(0); // slm_results must not be restored / backed up
    }

    [Fact]
    public async Task ReplaceMode_ClearsStaleTargetSlmResults()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sourceDb = new TestDatabase();
        using var sourceCon = sourceDb.Open();
        SeedSampleData(sourceCon);

        var sourceBackup = new BackupService(sourceCon);
        using var backupStream = new MemoryStream();
        await sourceBackup.ExportBackupAsync(backupStream, TestPassphrase, ct);

        using var targetDb = new TestDatabase();
        using var targetCon = targetDb.Open();
        // Pre-seed target with old stale SLM cache entry
        targetCon.Execute(@"
            INSERT INTO slm_results (notification_uid, intent, confidence, latency_ms, model_sha, created_at)
            VALUES ('old_stale_uid', 'explain', 0.90, 80, 'sha256old', 5000);");

        backupStream.Position = 0;
        var targetBackup = new BackupService(targetCon);
        await targetBackup.RestoreBackupAsync(backupStream, TestPassphrase, RestoreMode.Replace, ct);

        // Replace must clear stale SLM cache
        var slmCount = targetCon.ExecuteScalar("SELECT COUNT(*) FROM slm_results;");
        slmCount.ShouldBe(0);
    }

    [Fact]
    public async Task MergeMode_RepeatedRestore_IsIdempotent()
    {
        var ct = TestContext.Current.CancellationToken;
        using var sourceDb = new TestDatabase();
        using var sourceCon = sourceDb.Open();
        SeedSampleData(sourceCon);

        var sourceBackup = new BackupService(sourceCon);
        using var backupStream = new MemoryStream();
        await sourceBackup.ExportBackupAsync(backupStream, TestPassphrase, ct);

        using var targetDb = new TestDatabase();
        using var targetCon = targetDb.Open();

        var targetBackup = new BackupService(targetCon);

        // First merge restore
        backupStream.Position = 0;
        await targetBackup.RestoreBackupAsync(backupStream, TestPassphrase, RestoreMode.Merge, ct);

        var rulesCountFirst = targetCon.ExecuteScalar("SELECT COUNT(*) FROM user_rules;");
        rulesCountFirst.ShouldBe(1);

        // Second merge restore (exact same backup)
        backupStream.Position = 0;
        await targetBackup.RestoreBackupAsync(backupStream, TestPassphrase, RestoreMode.Merge, ct);

        var rulesCountSecond = targetCon.ExecuteScalar("SELECT COUNT(*) FROM user_rules;");
        rulesCountSecond.ShouldBe(1); // Must not duplicate user rules!

        var promoCount = targetCon.ExecuteScalar("SELECT COUNT(*) FROM promo_codes;");
        promoCount.ShouldBe(1); // Must not duplicate promo codes!
    }

    [Fact]
    public async Task EmptyOrWhitespacePassphrase_ThrowsArgumentException()
    {
        var ct = TestContext.Current.CancellationToken;
        using var db = new TestDatabase();
        using var con = db.Open();
        var backup = new BackupService(con);
        using var mem = new MemoryStream();

        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await backup.ExportBackupAsync(mem, "   ", ct);
        });

        await Should.ThrowAsync<ArgumentException>(async () =>
        {
            await backup.InspectBackupAsync(mem, "", ct);
        });
    }

    private static void SeedSampleData(StorageConnection con)
    {
        con.Execute(@"
            INSERT INTO apps (package, label, first_seen_at, default_category, notif_count)
            VALUES ('com.snapp.taxi', 'Snapp', 1000, 'offers', 1),
                   ('com.digikala.app', 'Digikala', 2000, 'orders', 1);");

        con.Execute(@"
            INSERT INTO notifications (
                uid, sbn_key, package, posted_at, captured_at, updated_at, tz_offset_min,
                title, text, category, is_read, is_persian, extracted_code
            ) VALUES (
                'notif_1', 'sbn_1', 'com.snapp.taxi', 1000, 1000, 1000, 210,
                'کد تخفیف اسنپ', 'با کد SNAPP50 تخفیف بگیرید', 'offers', 0, 1, 'SNAPP50'
            ), (
                'notif_2', 'sbn_2', 'com.digikala.app', 2000, 2000, 2000, 210,
                'Digikala Order Delivery', 'Your order is arriving today', 'orders', 1, 0, NULL
            );");

        var notif1Id = con.ExecuteScalar("SELECT id FROM notifications WHERE uid = 'notif_1';");
        con.Execute(@"
            INSERT INTO entities (notification_id, type, value, span_start, span_end)
            VALUES (@notif_id, 1, 'SNAPP50', 10, 17);",
            ("@notif_id", notif1Id));

        con.Execute(@"
            INSERT INTO promo_codes (
                code_norm, raw_code, package, status, confidence, kind, first_seen_at, last_seen_at
            ) VALUES (
                'SNAPP50', 'SNAPP50', 'com.snapp.taxi', 0, 0.99, 1, 1000, 1000
            );");

        con.Execute(@"
            INSERT INTO user_rules (priority, match_json, action_json, label, enabled, created_at)
            VALUES (1, '{""package"":""com.snapp.taxi""}', '{""setCategory"":""offers""}', 'Snapp Rule', 1, 1000);");
    }
}
