using Alak.Core.Model;
using Alak.Storage;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Storage;

/// <summary>
/// SQLite implementation of INotificationRepository (spec §6.6/§13).
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly StorageConnection _db;

    public NotificationRepository(StorageConnection db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Notification>> GetAllAsync()
    {
        var results = new List<Notification>();
        
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT uid, package, channel_name, title, text, category, posted_at, is_read, is_persian, extracted_code
            FROM notifications
            ORDER BY posted_at DESC";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new Notification(
                Id: reader.GetString(0),
                Package: reader.GetString(1),
                ChannelName: reader.IsDBNull(2) ? "" : reader.GetString(2),
                Title: reader.IsDBNull(3) ? "" : reader.GetString(3),
                Text: reader.IsDBNull(4) ? "" : reader.GetString(4),
                Category: reader.IsDBNull(5) ? "other" : reader.GetString(5),
                Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6)),
                IsRead: reader.GetBoolean(7),
                IsPersian: reader.GetBoolean(8),
                ExtractedCode: reader.IsDBNull(9) ? null : reader.GetString(9)
            ));
        }

        return results;
    }

    public async Task<Notification?> GetByIdAsync(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT uid, package, channel_name, title, text, category, posted_at, is_read, is_persian, extracted_code
            FROM notifications
            WHERE uid = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Notification(
                Id: reader.GetString(0),
                Package: reader.GetString(1),
                ChannelName: reader.IsDBNull(2) ? "" : reader.GetString(2),
                Title: reader.IsDBNull(3) ? "" : reader.GetString(3),
                Text: reader.IsDBNull(4) ? "" : reader.GetString(4),
                Category: reader.IsDBNull(5) ? "other" : reader.GetString(5),
                Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6)),
                IsRead: reader.GetBoolean(7),
                IsPersian: reader.GetBoolean(8),
                ExtractedCode: reader.IsDBNull(9) ? null : reader.GetString(9)
            );
        }

        return null;
    }

    public async Task AddAsync(Notification notification)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 1. Ensure the referenced app row exists
        _db.Execute(
            "INSERT OR IGNORE INTO apps(package, first_seen_at) VALUES ($p, $t)",
            ("$p", notification.Package), ("$t", now));

        var contentHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes((notification.Title ?? "") + "|" + (notification.Text ?? ""))));

        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO notifications (
                uid, sbn_key, package, channel_name, title, text, category,
                posted_at, captured_at, updated_at, tz_offset_min, content_hash,
                is_read, is_persian, extracted_code
            )
            VALUES (
                @uid, @sbn_key, @package, @channel_name, @title, @text, @category,
                @posted_at, @captured_at, @updated_at, 0, @content_hash,
                @is_read, @is_persian, @extracted_code
            )
            ON CONFLICT(uid) DO UPDATE SET
                title = excluded.title,
                text = excluded.text,
                category = excluded.category,
                is_read = excluded.is_read,
                is_persian = excluded.is_persian,
                extracted_code = excluded.extracted_code,
                updated_at = excluded.updated_at";
        
        cmd.Parameters.AddWithValue("@uid", notification.Id);
        cmd.Parameters.AddWithValue("@sbn_key", notification.Id);
        cmd.Parameters.AddWithValue("@package", notification.Package);
        cmd.Parameters.AddWithValue("@channel_name", (object?)notification.ChannelName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@title", (object?)notification.Title ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@text", (object?)notification.Text ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@category", notification.Category);
        cmd.Parameters.AddWithValue("@posted_at", notification.Timestamp.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("@captured_at", now);
        cmd.Parameters.AddWithValue("@updated_at", now);
        cmd.Parameters.AddWithValue("@content_hash", contentHash);
        cmd.Parameters.AddWithValue("@is_read", notification.IsRead ? 1 : 0);
        cmd.Parameters.AddWithValue("@is_persian", notification.IsPersian ? 1 : 0);
        cmd.Parameters.AddWithValue("@extracted_code", notification.ExtractedCode ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkReadAsync(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE notifications SET is_read = 1 WHERE uid = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM notifications WHERE uid = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ClearAllAsync()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM notifications";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<SourceItem>> GetSourcesAsync(int days)
    {
        var results = new List<SourceItem>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds();

        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                package,
                channel_name,
                COALESCE(NULLIF(TRIM(account_email), ''), 'default') as account_channel,
                COUNT(*) as count,
                MIN(category) as default_category
            FROM notifications
            WHERE posted_at >= $cutoff
            GROUP BY package, channel_name, account_email
            ORDER BY count DESC";

        cmd.Parameters.AddWithValue("$cutoff", cutoff);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var source = new SourceItem
            {
                PackageName = reader.GetString(0),
                ChannelId = reader.GetString(1),
                AccountEmail = reader.GetString(2),
                Count = reader.GetInt32(3),
                DisplayLabel = reader.GetString(2) != "default" ? reader.GetString(2) : ""
            };

            // Try to parse the default category
            var catStr = reader.GetString(4);
            if (Enum.TryParse<Category>(catStr, out var cat))
            {
                source.CurrentDefaultCategory = cat;
                source.SelectedCategory = cat;
            }

            results.Add(source);
        }

        return results;
    }
}