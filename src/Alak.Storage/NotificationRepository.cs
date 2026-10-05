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
                ChannelName: reader.GetString(2),
                Title: reader.GetString(3),
                Text: reader.GetString(4),
                Category: reader.GetString(5),
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
                ChannelName: reader.GetString(2),
                Title: reader.GetString(3),
                Text: reader.GetString(4),
                Category: reader.GetString(5),
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
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO notifications (uid, package, channel_name, title, text, category, posted_at, is_read, is_persian, extracted_code)
            VALUES (@uid, @package, @channel_name, @title, @text, @category, @posted_at, @is_read, @is_persian, @extracted_code)";
        
        cmd.Parameters.AddWithValue("@uid", notification.Id);
        cmd.Parameters.AddWithValue("@package", notification.Package);
        cmd.Parameters.AddWithValue("@channel_name", notification.ChannelName);
        cmd.Parameters.AddWithValue("@title", notification.Title);
        cmd.Parameters.AddWithValue("@text", notification.Text);
        cmd.Parameters.AddWithValue("@category", notification.Category);
        cmd.Parameters.AddWithValue("@posted_at", notification.Timestamp.ToUnixTimeMilliseconds());
        cmd.Parameters.AddWithValue("@is_read", notification.IsRead);
        cmd.Parameters.AddWithValue("@is_persian", notification.IsPersian);
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