using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Alak.Storage;

/// <summary>
/// Implements §11.5 / BAK-001..003: Encrypted backup and restore (.alakbackup)
/// with Argon2id / PBKDF2 (SHA-256, 100k+ iterations) and AES-256-GCM.
/// </summary>
public sealed class BackupService : IBackupService
{
    private static readonly byte[] MagicHeader = Encoding.ASCII.GetBytes("ALAKBKUP");
    private const int CurrentFormatVersion = 1;
    private const int SaltSizeBytes = 16;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int KeyDerivationIterations = 100_000;
    private const int KeySizeBytes = 32; // 256 bits for AES-256

    private readonly StorageConnection _connection;
    private readonly string? _adaptiveModelPath;

    public BackupService(StorageConnection connection, string? adaptiveModelPath = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _adaptiveModelPath = adaptiveModelPath;
    }

    public async Task ExportBackupAsync(Stream destination, string passphrase, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("Passphrase cannot be empty or whitespace.", nameof(passphrase));

        var payload = CollectBackupPayload();

        // 1. Serialize payload to JSON and compress with GZip
        byte[] plaintext;
        using (var mem = new MemoryStream())
        {
            using (var gzip = new GZipStream(mem, CompressionLevel.Optimal, leaveOpen: true))
            {
                await JsonSerializer.SerializeAsync(gzip, payload, cancellationToken: ct);
            }
            plaintext = mem.ToArray();
        }

        // 2. Generate Salt and Nonce
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);

        // 3. Derive 256-bit encryption key
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            KeyDerivationIterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);

        // 4. Construct 28-byte associatedData (Magic + Version + Salt)
        byte[] associatedData = new byte[MagicHeader.Length + 4 + SaltSizeBytes];
        Buffer.BlockCopy(MagicHeader, 0, associatedData, 0, MagicHeader.Length);
        BinaryPrimitivesWriter.WriteInt32LittleEndian(associatedData.AsSpan(MagicHeader.Length, 4), CurrentFormatVersion);
        Buffer.BlockCopy(salt, 0, associatedData, MagicHeader.Length + 4, SaltSizeBytes);

        // 5. Encrypt with AES-256-GCM
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSizeBytes];

        using (var aes = new AesGcm(key, TagSizeBytes))
        {
            aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        }

        // 6. Write out wire format:
        // [8 bytes: Magic][4 bytes: Version][16 bytes: Salt][12 bytes: Nonce][16 bytes: Tag][Ciphertext...]
        await destination.WriteAsync(associatedData, ct);
        await destination.WriteAsync(nonce, ct);
        await destination.WriteAsync(tag, ct);
        await destination.WriteAsync(ciphertext, ct);
        await destination.FlushAsync(ct);
    }

    public async Task<BackupMetadata> InspectBackupAsync(Stream source, string passphrase, CancellationToken ct = default)
    {
        var payload = await DecryptAndDecompressPayloadAsync(source, passphrase, ct);
        return payload.Metadata;
    }

    public async Task RestoreBackupAsync(Stream source, string passphrase, RestoreMode mode = RestoreMode.Replace, CancellationToken ct = default)
    {
        var payload = await DecryptAndDecompressPayloadAsync(source, passphrase, ct);

        lock (_connection.SyncRoot)
        {
            using var tx = _connection.BeginTransaction();
            try
            {
                if (mode == RestoreMode.Replace)
                {
                    ExecuteReplaceRestore(payload, tx);
                }
                else
                {
                    ExecuteMergeRestore(payload, tx);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        // Restore adaptive weights if present
        if (payload.AdaptiveWeights != null && payload.AdaptiveWeights.Length > 0 && !string.IsNullOrEmpty(_adaptiveModelPath))
        {
            if (mode == RestoreMode.Replace || !File.Exists(_adaptiveModelPath))
            {
                var dir = Path.GetDirectoryName(_adaptiveModelPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var tempPath = _adaptiveModelPath + ".tmp";
                await File.WriteAllBytesAsync(tempPath, payload.AdaptiveWeights, ct);
                if (File.Exists(_adaptiveModelPath))
                    File.Replace(tempPath, _adaptiveModelPath, null);
                else
                    File.Move(tempPath, _adaptiveModelPath);
            }
        }
    }

    private async Task<BackupPayload> DecryptAndDecompressPayloadAsync(Stream source, string passphrase, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(passphrase))
            throw new ArgumentException("Passphrase cannot be empty or whitespace.", nameof(passphrase));

        // Read wire header: 8 bytes Magic + 4 bytes Version + 16 bytes Salt
        byte[] magic = new byte[MagicHeader.Length];
        await ReadExactAsync(source, magic, ct);
        if (!CryptographicOperations.FixedTimeEquals(magic, MagicHeader))
            throw new InvalidDataException("Invalid backup format header.");

        byte[] versionBytes = new byte[4];
        await ReadExactAsync(source, versionBytes, ct);
        int version = BinaryPrimitivesReader.ReadInt32LittleEndian(versionBytes);
        if (version != CurrentFormatVersion)
            throw new InvalidDataException($"Unsupported backup format version: {version}.");

        byte[] salt = new byte[SaltSizeBytes];
        await ReadExactAsync(source, salt, ct);

        byte[] nonce = new byte[NonceSizeBytes];
        await ReadExactAsync(source, nonce, ct);

        byte[] tag = new byte[TagSizeBytes];
        await ReadExactAsync(source, tag, ct);

        // Read remaining stream as ciphertext
        byte[] ciphertext;
        using (var mem = new MemoryStream())
        {
            await source.CopyToAsync(mem, ct);
            ciphertext = mem.ToArray();
        }

        if (ciphertext.Length == 0)
            throw new InvalidDataException("Backup ciphertext payload is empty.");

        // Construct 28-byte associatedData
        byte[] associatedData = new byte[MagicHeader.Length + 4 + SaltSizeBytes];
        Buffer.BlockCopy(MagicHeader, 0, associatedData, 0, MagicHeader.Length);
        Buffer.BlockCopy(versionBytes, 0, associatedData, MagicHeader.Length, 4);
        Buffer.BlockCopy(salt, 0, associatedData, MagicHeader.Length + 4, SaltSizeBytes);

        // Derive key
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passphrase),
            salt,
            KeyDerivationIterations,
            HashAlgorithmName.SHA256,
            KeySizeBytes);

        // Decrypt ciphertext
        byte[] plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(key, TagSizeBytes))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
        }

        // Decompress GZip
        using (var pMem = new MemoryStream(plaintext))
        using (var gzip = new GZipStream(pMem, CompressionMode.Decompress))
        {
            var payload = await JsonSerializer.DeserializeAsync<BackupPayload>(gzip, cancellationToken: ct);
            if (payload == null || payload.Metadata == null)
                throw new InvalidDataException("Corrupted backup payload.");
            return payload;
        }
    }

    private static async Task ReadExactAsync(Stream s, byte[] buffer, CancellationToken ct)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await s.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (read == 0)
                throw new EndOfStreamException($"Unexpected end of stream while reading backup header. Needed {buffer.Length} bytes, got {total}.");
            total += read;
        }
    }

    private BackupPayload CollectBackupPayload()
    {
        var payload = new BackupPayload();

        lock (_connection.SyncRoot)
        {
            // 1. Apps (excluding icon_png as per BAK-001)
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT package, label, first_seen_at, default_category, depth, muted, auto_archive, region_hint, notif_count, last_seen_at FROM apps;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    payload.Apps.Add(new AppRowDto
                    {
                        Package = reader.GetString(0),
                        Label = reader.IsDBNull(1) ? null : reader.GetString(1),
                        FirstSeenAt = reader.GetInt64(2),
                        DefaultCategory = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Depth = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                        Muted = reader.GetInt32(5),
                        AutoArchive = reader.GetInt32(6),
                        RegionHint = reader.IsDBNull(7) ? null : reader.GetString(7),
                        NotifCount = reader.GetInt32(8),
                        LastSeenAt = reader.IsDBNull(9) ? null : reader.GetInt64(9)
                    });
                }
            }

            // 2. Notifications
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT uid, sbn_key, package, posted_at, captured_at, updated_at, removed_at, removal_reason,
                           tz_offset_min, channel_id, channel_name, android_category, importance, group_key,
                           title, text, big_text, sub_text, lines_json, actions_json, depth, category,
                           content_hash, pinned, state, account_email, evidence_json, needs_review,
                           is_read, is_persian, extracted_code
                    FROM notifications ORDER BY posted_at ASC;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    payload.Notifications.Add(new NotificationRowDto
                    {
                        Uid = reader.GetString(0),
                        SbnKey = reader.GetString(1),
                        Package = reader.GetString(2),
                        PostedAt = reader.GetInt64(3),
                        CapturedAt = reader.GetInt64(4),
                        UpdatedAt = reader.GetInt64(5),
                        RemovedAt = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                        RemovalReason = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        TzOffsetMin = reader.GetInt32(8),
                        ChannelId = reader.IsDBNull(9) ? null : reader.GetString(9),
                        ChannelName = reader.IsDBNull(10) ? null : reader.GetString(10),
                        AndroidCategory = reader.IsDBNull(11) ? null : reader.GetString(11),
                        Importance = reader.IsDBNull(12) ? null : reader.GetInt32(12),
                        GroupKey = reader.IsDBNull(13) ? null : reader.GetString(13),
                        Title = reader.IsDBNull(14) ? null : reader.GetString(14),
                        Text = reader.IsDBNull(15) ? null : reader.GetString(15),
                        BigText = reader.IsDBNull(16) ? null : reader.GetString(16),
                        SubText = reader.IsDBNull(17) ? null : reader.GetString(17),
                        LinesJson = reader.IsDBNull(18) ? null : reader.GetString(18),
                        ActionsJson = reader.IsDBNull(19) ? null : reader.GetString(19),
                        Depth = reader.IsDBNull(20) ? null : reader.GetInt32(20),
                        Category = reader.IsDBNull(21) ? null : reader.GetString(21),
                        ContentHash = reader.IsDBNull(22) ? null : reader.GetString(22),
                        Pinned = reader.GetInt32(23),
                        State = reader.GetInt32(24),
                        AccountEmail = reader.IsDBNull(25) ? null : reader.GetString(25),
                        EvidenceJson = reader.IsDBNull(26) ? null : reader.GetString(26),
                        NeedsReview = reader.GetInt32(27),
                        IsRead = reader.GetInt32(28),
                        IsPersian = reader.GetInt32(29),
                        ExtractedCode = reader.IsDBNull(30) ? null : reader.GetString(30)
                    });
                }
            }

            // 3. Entities
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT n.uid, e.type, e.value, e.span_start, e.span_end
                    FROM entities e
                    JOIN notifications n ON e.notification_id = n.id;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    payload.Entities.Add(new EntityRowDto
                    {
                        NotificationUid = reader.GetString(0),
                        Type = reader.GetInt32(1),
                        Value = reader.GetString(2),
                        SpanStart = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        SpanEnd = reader.IsDBNull(4) ? null : reader.GetInt32(4)
                    });
                }
            }

            // 4. Promo Codes
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT code_norm, raw_code, package, account_email, channel_id, status, confidence, kind,
                           discount_value, discount_type, currency, min_spend, cap, scope, first_order,
                           single_use, valid_from, valid_until, store_hint, first_seen_at, last_seen_at,
                           times_seen, reminder_24h_at, reminder_morning_at, reminder_custom_at
                    FROM promo_codes;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    payload.PromoCodes.Add(new PromoCodeRowDto
                    {
                        CodeNorm = reader.GetString(0),
                        RawCode = reader.GetString(1),
                        Package = reader.GetString(2),
                        AccountEmail = reader.IsDBNull(3) ? null : reader.GetString(3),
                        ChannelId = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Status = reader.GetInt32(5),
                        Confidence = reader.GetDouble(6),
                        Kind = reader.GetInt32(7),
                        DiscountValue = reader.IsDBNull(8) ? null : reader.GetDouble(8),
                        DiscountType = reader.IsDBNull(9) ? null : reader.GetInt32(9),
                        Currency = reader.IsDBNull(10) ? null : reader.GetString(10),
                        MinSpend = reader.IsDBNull(11) ? null : reader.GetDouble(11),
                        Cap = reader.IsDBNull(12) ? null : reader.GetDouble(12),
                        Scope = reader.IsDBNull(13) ? null : reader.GetString(13),
                        FirstOrder = reader.GetInt32(14),
                        SingleUse = reader.GetInt32(15),
                        ValidFrom = reader.IsDBNull(16) ? null : reader.GetInt64(16),
                        ValidUntil = reader.IsDBNull(17) ? null : reader.GetInt64(17),
                        StoreHint = reader.IsDBNull(18) ? null : reader.GetString(18),
                        FirstSeenAt = reader.GetInt64(19),
                        LastSeenAt = reader.GetInt64(20),
                        TimesSeen = reader.GetInt32(21),
                        Reminder24hAt = reader.IsDBNull(22) ? null : reader.GetInt64(22),
                        ReminderMorningAt = reader.IsDBNull(23) ? null : reader.GetInt64(23),
                        ReminderCustomAt = reader.IsDBNull(24) ? null : reader.GetInt64(24)
                    });
                }
            }

            // 5. User Rules
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT priority, match_json, action_json, label, enabled, created_at, updated_at FROM user_rules ORDER BY priority ASC, id ASC;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    payload.UserRules.Add(new UserRuleRowDto
                    {
                        Priority = reader.GetInt32(0),
                        MatchJson = reader.GetString(1),
                        ActionJson = reader.GetString(2),
                        Label = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Enabled = reader.GetInt32(4),
                        CreatedAt = reader.GetInt64(5),
                        UpdatedAt = reader.IsDBNull(6) ? null : reader.GetInt64(6)
                    });
                }
            }

            // 6. Corrections
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT original_uid, corrected_uid, features_blob, confidence, applied_at FROM corrections;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    payload.Corrections.Add(new CorrectionRowDto
                    {
                        OriginalUid = reader.GetString(0),
                        CorrectedUid = reader.IsDBNull(1) ? null : reader.GetString(1),
                        FeaturesBlob = reader.IsDBNull(2) ? null : (byte[])reader["features_blob"],
                        Confidence = reader.IsDBNull(3) ? null : reader.GetDouble(3),
                        AppliedAt = reader.IsDBNull(4) ? null : reader.GetInt64(4)
                    });
                }
            }

            // 7. Digests
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT period_start, period_end, category, summary_json, generated_at FROM digests;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    payload.Digests.Add(new DigestRowDto
                    {
                        PeriodStart = reader.GetInt64(0),
                        PeriodEnd = reader.GetInt64(1),
                        Category = reader.GetInt32(2),
                        SummaryJson = reader.GetString(3),
                        GeneratedAt = reader.GetInt64(4)
                    });
                }
            }

            // 8. Global Settings
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "SELECT theme, ui_language, calendar, digits, retention_overrides_json, global_size_cap, tier2_enabled, slm_performance_profile, app_lock_enabled, hide_amounts_in_lists, notification_reminders_enabled FROM global_settings WHERE id = 1;";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    payload.GlobalSettings = new GlobalSettingsRowDto
                    {
                        Theme = reader.GetString(0),
                        UiLanguage = reader.GetString(1),
                        Calendar = reader.GetString(2),
                        Digits = reader.GetString(3),
                        RetentionOverridesJson = reader.IsDBNull(4) ? null : reader.GetString(4),
                        GlobalSizeCap = reader.IsDBNull(5) ? null : reader.GetInt64(5),
                        Tier2Enabled = reader.GetInt32(6),
                        SlmPerformanceProfile = reader.GetString(7),
                        AppLockEnabled = reader.GetInt32(8),
                        HideAmountsInLists = reader.GetInt32(9),
                        NotificationRemindersEnabled = reader.GetInt32(10)
                    };
                }
            }
        }

        // 9. Adaptive weights file snapshot
        if (!string.IsNullOrEmpty(_adaptiveModelPath) && File.Exists(_adaptiveModelPath))
        {
            try
            {
                payload.AdaptiveWeights = File.ReadAllBytes(_adaptiveModelPath);
            }
            catch { }
        }

        payload.Metadata = new BackupMetadata(
            Version: CurrentFormatVersion,
            CreatedAt: DateTimeOffset.UtcNow,
            NotificationCount: payload.Notifications.Count,
            UserRuleCount: payload.UserRules.Count,
            PromoCodeCount: payload.PromoCodes.Count,
            HasAdaptiveWeights: payload.AdaptiveWeights != null && payload.AdaptiveWeights.Length > 0
        );

        return payload;
    }

    private void ExecuteReplaceRestore(BackupPayload payload, SqliteTransaction tx)
    {
        // Delete in foreign-key dependency order
        _connection.Execute("DELETE FROM entities;", tx);
        _connection.Execute("DELETE FROM notifications;", tx);
        _connection.Execute("DELETE FROM promo_codes;", tx);
        _connection.Execute("DELETE FROM user_rules;", tx);
        _connection.Execute("DELETE FROM corrections;", tx);
        _connection.Execute("DELETE FROM digests;", tx);
        _connection.Execute("DELETE FROM slm_results;", tx);
        _connection.Execute("DELETE FROM notification_fts;", tx);
        _connection.Execute("DELETE FROM apps;", tx);

        InsertApps(payload.Apps, tx);
        var uidToNewId = InsertNotifications(payload.Notifications, tx);
        InsertEntities(payload.Entities, uidToNewId, tx);
        InsertPromoCodes(payload.PromoCodes, tx);
        InsertUserRules(payload.UserRules, tx);
        InsertCorrections(payload.Corrections, tx);
        InsertDigests(payload.Digests, tx);
        if (payload.GlobalSettings != null)
        {
            UpdateGlobalSettings(payload.GlobalSettings, tx);
        }
    }

    private void ExecuteMergeRestore(BackupPayload payload, SqliteTransaction tx)
    {
        // 1. Merge Apps
        var existingApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = _connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT package FROM apps;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) existingApps.Add(r.GetString(0));
        }

        var appsToInsert = new List<AppRowDto>();
        foreach (var app in payload.Apps)
        {
            if (!existingApps.Contains(app.Package))
            {
                appsToInsert.Add(app);
                existingApps.Add(app.Package);
            }
        }
        InsertApps(appsToInsert, tx);

        // 2. Merge Notifications (skip if uid exists)
        var existingUids = new HashSet<string>(StringComparer.Ordinal);
        using (var cmd = _connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT uid FROM notifications;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) existingUids.Add(r.GetString(0));
        }

        var notifsToInsert = new List<NotificationRowDto>();
        foreach (var notif in payload.Notifications)
        {
            if (!existingUids.Contains(notif.Uid))
            {
                notifsToInsert.Add(notif);
                existingUids.Add(notif.Uid);
            }
        }

        var uidToNewId = InsertNotifications(notifsToInsert, tx);

        // 3. Merge Entities for newly inserted notifications
        var entitiesToInsert = new List<EntityRowDto>();
        foreach (var ent in payload.Entities)
        {
            if (uidToNewId.ContainsKey(ent.NotificationUid))
            {
                entitiesToInsert.Add(ent);
            }
        }
        InsertEntities(entitiesToInsert, uidToNewId, tx);

        // 4. Merge Promo Codes (skip if code_norm, package exists)
        var existingCodes = new HashSet<(string code, string pkg)>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT code_norm, package FROM promo_codes;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) existingCodes.Add((r.GetString(0), r.GetString(1)));
        }

        var codesToInsert = new List<PromoCodeRowDto>();
        foreach (var promo in payload.PromoCodes)
        {
            if (!existingCodes.Contains((promo.CodeNorm, promo.Package)))
            {
                codesToInsert.Add(promo);
                existingCodes.Add((promo.CodeNorm, promo.Package));
            }
        }
        InsertPromoCodes(codesToInsert, tx);

        // 5. Merge User Rules (skip duplicate match_json, action_json)
        var existingRules = new HashSet<(string match, string action)>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT match_json, action_json FROM user_rules;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) existingRules.Add((r.GetString(0), r.GetString(1)));
        }
        var rulesToInsert = new List<UserRuleRowDto>();
        foreach (var r in payload.UserRules)
        {
            if (!existingRules.Contains((r.MatchJson, r.ActionJson)))
            {
                rulesToInsert.Add(r);
                existingRules.Add((r.MatchJson, r.ActionJson));
            }
        }
        InsertUserRules(rulesToInsert, tx);

        // 6. Merge Corrections (skip duplicate original_uid)
        var existingCorrections = new HashSet<string>(StringComparer.Ordinal);
        using (var cmd = _connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT original_uid FROM corrections;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) existingCorrections.Add(r.GetString(0));
        }
        var correctionsToInsert = new List<CorrectionRowDto>();
        foreach (var c in payload.Corrections)
        {
            if (!existingCorrections.Contains(c.OriginalUid))
            {
                correctionsToInsert.Add(c);
                existingCorrections.Add(c.OriginalUid);
            }
        }
        InsertCorrections(correctionsToInsert, tx);

        // 7. Merge Digests (skip duplicate period_start, period_end, category)
        var existingDigests = new HashSet<(long start, long end, int cat)>();
        using (var cmd = _connection.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT period_start, period_end, category FROM digests;";
            using var r = cmd.ExecuteReader();
            while (r.Read()) existingDigests.Add((r.GetInt64(0), r.GetInt64(1), r.GetInt32(2)));
        }
        var digestsToInsert = new List<DigestRowDto>();
        foreach (var d in payload.Digests)
        {
            if (!existingDigests.Contains((d.PeriodStart, d.PeriodEnd, d.Category)))
            {
                digestsToInsert.Add(d);
                existingDigests.Add((d.PeriodStart, d.PeriodEnd, d.Category));
            }
        }
        InsertDigests(digestsToInsert, tx);
    }

    private void InsertApps(List<AppRowDto> apps, SqliteTransaction tx)
    {
        foreach (var app in apps)
        {
            _connection.Execute(@"
                INSERT OR REPLACE INTO apps (package, label, first_seen_at, default_category, depth, muted, auto_archive, region_hint, notif_count, last_seen_at)
                VALUES (@package, @label, @first_seen, @cat, @depth, @muted, @archive, @region, @count, @last_seen);",
                tx,
                ("@package", app.Package),
                ("@label", app.Label),
                ("@first_seen", app.FirstSeenAt),
                ("@cat", app.DefaultCategory),
                ("@depth", app.Depth),
                ("@muted", app.Muted),
                ("@archive", app.AutoArchive),
                ("@region", app.RegionHint),
                ("@count", app.NotifCount),
                ("@last_seen", app.LastSeenAt));
        }
    }

    private Dictionary<string, long> InsertNotifications(List<NotificationRowDto> notifications, SqliteTransaction tx)
    {
        var uidToNewId = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var n in notifications)
        {
            _connection.Execute(@"
                INSERT INTO notifications (
                    uid, sbn_key, package, posted_at, captured_at, updated_at, removed_at, removal_reason,
                    tz_offset_min, channel_id, channel_name, android_category, importance, group_key,
                    title, text, big_text, sub_text, lines_json, actions_json, depth, category,
                    content_hash, pinned, state, account_email, evidence_json, needs_review,
                    is_read, is_persian, extracted_code
                ) VALUES (
                    @uid, @sbn_key, @package, @posted_at, @captured_at, @updated_at, @removed_at, @removal_reason,
                    @tz_offset_min, @channel_id, @channel_name, @android_category, @importance, @group_key,
                    @title, @text, @big_text, @sub_text, @lines_json, @actions_json, @depth, @category,
                    @content_hash, @pinned, @state, @account_email, @evidence_json, @needs_review,
                    @is_read, @is_persian, @extracted_code
                );",
                tx,
                ("@uid", n.Uid),
                ("@sbn_key", n.SbnKey),
                ("@package", n.Package),
                ("@posted_at", n.PostedAt),
                ("@captured_at", n.CapturedAt),
                ("@updated_at", n.UpdatedAt),
                ("@removed_at", n.RemovedAt),
                ("@removal_reason", n.RemovalReason),
                ("@tz_offset_min", n.TzOffsetMin),
                ("@channel_id", n.ChannelId),
                ("@channel_name", n.ChannelName),
                ("@android_category", n.AndroidCategory),
                ("@importance", n.Importance),
                ("@group_key", n.GroupKey),
                ("@title", n.Title),
                ("@text", n.Text),
                ("@big_text", n.BigText),
                ("@sub_text", n.SubText),
                ("@lines_json", n.LinesJson),
                ("@actions_json", n.ActionsJson),
                ("@depth", n.Depth),
                ("@category", n.Category),
                ("@content_hash", n.ContentHash),
                ("@pinned", n.Pinned),
                ("@state", n.State),
                ("@account_email", n.AccountEmail),
                ("@evidence_json", n.EvidenceJson),
                ("@needs_review", n.NeedsReview),
                ("@is_read", n.IsRead),
                ("@is_persian", n.IsPersian),
                ("@extracted_code", n.ExtractedCode));

            uidToNewId[n.Uid] = _connection.LastInsertRowId;
        }
        return uidToNewId;
    }

    private void InsertEntities(List<EntityRowDto> entities, Dictionary<string, long> uidToNewId, SqliteTransaction tx)
    {
        foreach (var e in entities)
        {
            if (uidToNewId.TryGetValue(e.NotificationUid, out var notifId))
            {
                _connection.Execute(@"
                    INSERT INTO entities (notification_id, type, value, span_start, span_end)
                    VALUES (@notif_id, @type, @val, @start, @end);",
                    tx,
                    ("@notif_id", notifId),
                    ("@type", e.Type),
                    ("@val", e.Value),
                    ("@start", e.SpanStart),
                    ("@end", e.SpanEnd));
            }
        }
    }

    private void InsertPromoCodes(List<PromoCodeRowDto> promoCodes, SqliteTransaction tx)
    {
        foreach (var p in promoCodes)
        {
            _connection.Execute(@"
                INSERT OR REPLACE INTO promo_codes (
                    code_norm, raw_code, package, account_email, channel_id, status, confidence, kind,
                    discount_value, discount_type, currency, min_spend, cap, scope, first_order,
                    single_use, valid_from, valid_until, store_hint, first_seen_at, last_seen_at,
                    times_seen, reminder_24h_at, reminder_morning_at, reminder_custom_at
                ) VALUES (
                    @code_norm, @raw_code, @package, @account_email, @channel_id, @status, @confidence, @kind,
                    @discount_value, @discount_type, @currency, @min_spend, @cap, @scope, @first_order,
                    @single_use, @valid_from, @valid_until, @store_hint, @first_seen_at, @last_seen_at,
                    @times_seen, @reminder_24h_at, @reminder_morning_at, @reminder_custom_at
                );",
                tx,
                ("@code_norm", p.CodeNorm),
                ("@raw_code", p.RawCode),
                ("@package", p.Package),
                ("@account_email", p.AccountEmail),
                ("@channel_id", p.ChannelId),
                ("@status", p.Status),
                ("@confidence", p.Confidence),
                ("@kind", p.Kind),
                ("@discount_value", p.DiscountValue),
                ("@discount_type", p.DiscountType),
                ("@currency", p.Currency),
                ("@min_spend", p.MinSpend),
                ("@cap", p.Cap),
                ("@scope", p.Scope),
                ("@first_order", p.FirstOrder),
                ("@single_use", p.SingleUse),
                ("@valid_from", p.ValidFrom),
                ("@valid_until", p.ValidUntil),
                ("@store_hint", p.StoreHint),
                ("@first_seen_at", p.FirstSeenAt),
                ("@last_seen_at", p.LastSeenAt),
                ("@times_seen", p.TimesSeen),
                ("@reminder_24h_at", p.Reminder24hAt),
                ("@reminder_morning_at", p.ReminderMorningAt),
                ("@reminder_custom_at", p.ReminderCustomAt));
        }
    }

    private void InsertUserRules(List<UserRuleRowDto> userRules, SqliteTransaction tx)
    {
        foreach (var r in userRules)
        {
            _connection.Execute(@"
                INSERT INTO user_rules (priority, match_json, action_json, label, enabled, created_at, updated_at)
                VALUES (@priority, @match_json, @action_json, @label, @enabled, @created_at, @updated_at);",
                tx,
                ("@priority", r.Priority),
                ("@match_json", r.MatchJson),
                ("@action_json", r.ActionJson),
                ("@label", r.Label),
                ("@enabled", r.Enabled),
                ("@created_at", r.CreatedAt),
                ("@updated_at", r.UpdatedAt));
        }
    }

    private void InsertCorrections(List<CorrectionRowDto> corrections, SqliteTransaction tx)
    {
        foreach (var c in corrections)
        {
            _connection.Execute(@"
                INSERT INTO corrections (original_uid, corrected_uid, features_blob, confidence, applied_at)
                VALUES (@orig, @corr, @feat, @conf, @app);",
                tx,
                ("@orig", c.OriginalUid),
                ("@corr", c.CorrectedUid),
                ("@feat", c.FeaturesBlob),
                ("@conf", c.Confidence),
                ("@app", c.AppliedAt));
        }
    }

    private void InsertDigests(List<DigestRowDto> digests, SqliteTransaction tx)
    {
        foreach (var d in digests)
        {
            _connection.Execute(@"
                INSERT INTO digests (period_start, period_end, category, summary_json, generated_at)
                VALUES (@start, @end, @cat, @json, @gen);",
                tx,
                ("@start", d.PeriodStart),
                ("@end", d.PeriodEnd),
                ("@cat", d.Category),
                ("@json", d.SummaryJson),
                ("@gen", d.GeneratedAt));
        }
    }

    private void UpdateGlobalSettings(GlobalSettingsRowDto s, SqliteTransaction tx)
    {
        _connection.Execute(@"
            INSERT OR REPLACE INTO global_settings (
                id, theme, ui_language, calendar, digits, retention_overrides_json, global_size_cap,
                tier2_enabled, slm_performance_profile, app_lock_enabled, hide_amounts_in_lists, notification_reminders_enabled
            ) VALUES (
                1, @theme, @ui_lang, @calendar, @digits, @ret_json, @size_cap,
                @tier2, @slm_profile, @app_lock, @hide_amounts, @notif_reminders
            );",
            tx,
            ("@theme", s.Theme),
            ("@ui_lang", s.UiLanguage),
            ("@calendar", s.Calendar),
            ("@digits", s.Digits),
            ("@ret_json", s.RetentionOverridesJson),
            ("@size_cap", s.GlobalSizeCap),
            ("@tier2", s.Tier2Enabled),
            ("@slm_profile", s.SlmPerformanceProfile),
            ("@app_lock", s.AppLockEnabled),
            ("@hide_amounts", s.HideAmountsInLists),
            ("@notif_reminders", s.NotificationRemindersEnabled));
    }

    private static class BinaryPrimitivesWriter
    {
        public static void WriteInt32LittleEndian(Span<byte> destination, int value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }
    }

    private static class BinaryPrimitivesReader
    {
        public static int ReadInt32LittleEndian(ReadOnlySpan<byte> source)
        {
            return source[0] | (source[1] << 8) | (source[2] << 16) | (source[3] << 24);
        }
    }
}

internal sealed class BackupPayload
{
    public BackupMetadata Metadata { get; set; } = null!;
    public List<AppRowDto> Apps { get; set; } = new();
    public List<NotificationRowDto> Notifications { get; set; } = new();
    public List<EntityRowDto> Entities { get; set; } = new();
    public List<PromoCodeRowDto> PromoCodes { get; set; } = new();
    public List<UserRuleRowDto> UserRules { get; set; } = new();
    public List<CorrectionRowDto> Corrections { get; set; } = new();
    public List<DigestRowDto> Digests { get; set; } = new();
    public GlobalSettingsRowDto? GlobalSettings { get; set; }
    public byte[]? AdaptiveWeights { get; set; }
}

internal sealed class AppRowDto
{
    public string Package { get; set; } = "";
    public string? Label { get; set; }
    public long FirstSeenAt { get; set; }
    public string? DefaultCategory { get; set; }
    public int? Depth { get; set; }
    public int Muted { get; set; }
    public int AutoArchive { get; set; }
    public string? RegionHint { get; set; }
    public int NotifCount { get; set; }
    public long? LastSeenAt { get; set; }
}

internal sealed class NotificationRowDto
{
    public string Uid { get; set; } = "";
    public string SbnKey { get; set; } = "";
    public string Package { get; set; } = "";
    public long PostedAt { get; set; }
    public long CapturedAt { get; set; }
    public long UpdatedAt { get; set; }
    public long? RemovedAt { get; set; }
    public int? RemovalReason { get; set; }
    public int TzOffsetMin { get; set; }
    public string? ChannelId { get; set; }
    public string? ChannelName { get; set; }
    public string? AndroidCategory { get; set; }
    public int? Importance { get; set; }
    public string? GroupKey { get; set; }
    public string? Title { get; set; }
    public string? Text { get; set; }
    public string? BigText { get; set; }
    public string? SubText { get; set; }
    public string? LinesJson { get; set; }
    public string? ActionsJson { get; set; }
    public int? Depth { get; set; }
    public string? Category { get; set; }
    public string? ContentHash { get; set; }
    public int Pinned { get; set; }
    public int State { get; set; }
    public string? AccountEmail { get; set; }
    public string? EvidenceJson { get; set; }
    public int NeedsReview { get; set; }
    public int IsRead { get; set; }
    public int IsPersian { get; set; }
    public string? ExtractedCode { get; set; }
}

internal sealed class EntityRowDto
{
    public string NotificationUid { get; set; } = "";
    public int Type { get; set; }
    public string Value { get; set; } = "";
    public int? SpanStart { get; set; }
    public int? SpanEnd { get; set; }
}

internal sealed class PromoCodeRowDto
{
    public string CodeNorm { get; set; } = "";
    public string RawCode { get; set; } = "";
    public string Package { get; set; } = "";
    public string? AccountEmail { get; set; }
    public string? ChannelId { get; set; }
    public int Status { get; set; }
    public double Confidence { get; set; }
    public int Kind { get; set; }
    public double? DiscountValue { get; set; }
    public int? DiscountType { get; set; }
    public string? Currency { get; set; }
    public double? MinSpend { get; set; }
    public double? Cap { get; set; }
    public string? Scope { get; set; }
    public int FirstOrder { get; set; }
    public int SingleUse { get; set; }
    public long? ValidFrom { get; set; }
    public long? ValidUntil { get; set; }
    public string? StoreHint { get; set; }
    public long FirstSeenAt { get; set; }
    public long LastSeenAt { get; set; }
    public int TimesSeen { get; set; }
    public long? Reminder24hAt { get; set; }
    public long? ReminderMorningAt { get; set; }
    public long? ReminderCustomAt { get; set; }
}

internal sealed class UserRuleRowDto
{
    public int Priority { get; set; }
    public string MatchJson { get; set; } = "";
    public string ActionJson { get; set; } = "";
    public string? Label { get; set; }
    public int Enabled { get; set; }
    public long CreatedAt { get; set; }
    public long? UpdatedAt { get; set; }
}

internal sealed class CorrectionRowDto
{
    public string OriginalUid { get; set; } = "";
    public string? CorrectedUid { get; set; }
    public byte[]? FeaturesBlob { get; set; }
    public double? Confidence { get; set; }
    public long? AppliedAt { get; set; }
}

internal sealed class DigestRowDto
{
    public long PeriodStart { get; set; }
    public long PeriodEnd { get; set; }
    public int Category { get; set; }
    public string SummaryJson { get; set; } = "";
    public long GeneratedAt { get; set; }
}

internal sealed class GlobalSettingsRowDto
{
    public string Theme { get; set; } = "system";
    public string UiLanguage { get; set; } = "system";
    public string Calendar { get; set; } = "auto";
    public string Digits { get; set; } = "auto";
    public string? RetentionOverridesJson { get; set; }
    public long? GlobalSizeCap { get; set; }
    public int Tier2Enabled { get; set; } = 1;
    public string SlmPerformanceProfile { get; set; } = "balanced";
    public int AppLockEnabled { get; set; }
    public int HideAmountsInLists { get; set; }
    public int NotificationRemindersEnabled { get; set; } = 1;
}
