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

    public Task<IEnumerable<Notification>> GetAllAsync()
    {
        lock (_db.SyncRoot)
        {
            var results = new List<Notification>();
            
            using var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                SELECT uid, package, channel_name, title, text, category, posted_at, is_read, is_persian, extracted_code, attributed_package
                FROM notifications
                ORDER BY posted_at DESC";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
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
                    ExtractedCode: reader.IsDBNull(9) ? null : reader.GetString(9),
                    AttributedPackage: reader.IsDBNull(10) ? null : reader.GetString(10)
                ));
            }

            return Task.FromResult<IEnumerable<Notification>>(results);
        }
    }

    public Task<Notification?> GetByIdAsync(string id)
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                SELECT uid, package, channel_name, title, text, category, posted_at, is_read, is_persian, extracted_code, attributed_package
                FROM notifications
                WHERE uid = @id";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return Task.FromResult<Notification?>(new Notification(
                    Id: reader.GetString(0),
                    Package: reader.GetString(1),
                    ChannelName: reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Title: reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Text: reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Category: reader.IsDBNull(5) ? "other" : reader.GetString(5),
                    Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6)),
                    IsRead: reader.GetBoolean(7),
                    IsPersian: reader.GetBoolean(8),
                    ExtractedCode: reader.IsDBNull(9) ? null : reader.GetString(9),
                    AttributedPackage: reader.IsDBNull(10) ? null : reader.GetString(10)
                ));
            }

            return Task.FromResult<Notification?>(null);
        }
    }

    public Task AddAsync(Notification notification)
    {
        lock (_db.SyncRoot)
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
                    is_read, is_persian, extracted_code, attributed_package
                )
                VALUES (
                    @uid, @sbn_key, @package, @channel_name, @title, @text, @category,
                    @posted_at, @captured_at, @updated_at, 0, @content_hash,
                    @is_read, @is_persian, @extracted_code, @attributed_package
                )
                ON CONFLICT(uid) DO UPDATE SET
                    title = excluded.title,
                    text = excluded.text,
                    category = excluded.category,
                    is_read = excluded.is_read,
                    is_persian = excluded.is_persian,
                    extracted_code = excluded.extracted_code,
                    attributed_package = excluded.attributed_package,
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
            cmd.Parameters.AddWithValue("@attributed_package", notification.AttributedPackage ?? (object)DBNull.Value);

            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }
    }

    public Task MarkReadAsync(string id)
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "UPDATE notifications SET is_read = 1 WHERE uid = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }
    }

    public Task DeleteAsync(string id)
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "DELETE FROM notifications WHERE uid = @id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }
    }

    public Task ClearAllAsync()
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "DELETE FROM notifications";
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }
    }

    public Task<List<SourceItem>> GetSourcesAsync(int days)
    {
        lock (_db.SyncRoot)
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

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
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

            return Task.FromResult(results);
        }
    }

    public Task UpdateCategoryAsync(string id, string category)
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "UPDATE notifications SET category = @c, updated_at = @now WHERE uid = @id";
            cmd.Parameters.AddWithValue("@c", category);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@now", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }
    }

    public Task<int> UpdateCategoryForPackageAsync(string package, string category)
    {
        lock (_db.SyncRoot)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "UPDATE notifications SET category = @c WHERE package = @p OR package LIKE @likeP";
            cmd.Parameters.AddWithValue("@c", category);
            cmd.Parameters.AddWithValue("@p", package);
            cmd.Parameters.AddWithValue("@likeP", $"%{package}%");
            var affected = cmd.ExecuteNonQuery();
            return Task.FromResult(affected);
        }
    }

    public Task<IReadOnlyList<Notification>> GetPageAsync(long? beforeUtcMs, int limit, string? category = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_db.SyncRoot)
        {
            var results = new List<Notification>();
            using var cmd = _db.CreateCommand();

            bool isPromoOrOffers = !string.IsNullOrWhiteSpace(category) &&
                (category.Equals("promo", StringComparison.OrdinalIgnoreCase) || category.Equals("offers", StringComparison.OrdinalIgnoreCase));

            if (isPromoOrOffers)
            {
                cmd.CommandText = @"
                    SELECT uid, package, channel_name, title, text, category, posted_at, is_read, is_persian, extracted_code, attributed_package
                    FROM notifications
                    WHERE (@before IS NULL OR posted_at < @before)
                      AND (category = 'promo' OR category = 'offers')
                    ORDER BY posted_at DESC
                    LIMIT @limit";
            }
            else
            {
                cmd.CommandText = @"
                    SELECT uid, package, channel_name, title, text, category, posted_at, is_read, is_persian, extracted_code, attributed_package
                    FROM notifications
                    WHERE (@before IS NULL OR posted_at < @before)
                      AND (@category IS NULL OR category = @category)
                    ORDER BY posted_at DESC
                    LIMIT @limit";
                cmd.Parameters.AddWithValue("@category", string.IsNullOrWhiteSpace(category) ? DBNull.Value : category);
            }

            cmd.Parameters.AddWithValue("@before", (object?)beforeUtcMs ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
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
                    ExtractedCode: reader.IsDBNull(9) ? null : reader.GetString(9),
                    AttributedPackage: reader.IsDBNull(10) ? null : reader.GetString(10)
                ));
            }

            return Task.FromResult<IReadOnlyList<Notification>>(results);
        }
    }

    public Task<IReadOnlyList<Notification>> SearchFtsAsync(string query, int limit, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<Notification>>(Array.Empty<Notification>());

        lock (_db.SyncRoot)
        {
            var results = new List<Notification>();
            using var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                SELECT n.uid, n.package, n.channel_name, n.title, n.text, n.category, n.posted_at, n.is_read, n.is_persian, n.extracted_code, n.attributed_package
                FROM notification_fts f
                JOIN notifications n ON n.id = f.rowid
                WHERE notification_fts MATCH @query
                ORDER BY f.rank, n.posted_at DESC
                LIMIT @limit";

            var sanitizedQuery = query.Trim();
            cmd.Parameters.AddWithValue("@query", sanitizedQuery);
            cmd.Parameters.AddWithValue("@limit", limit);

            try
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
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
                        ExtractedCode: reader.IsDBNull(9) ? null : reader.GetString(9),
                        AttributedPackage: reader.IsDBNull(10) ? null : reader.GetString(10)
                    ));
                }
            }
            catch (SqliteException)
            {
                // Fallback for syntax errors in complex raw user input: quote the whole query as a phrase
                results.Clear();
                using var fallbackCmd = _db.CreateCommand();
                fallbackCmd.CommandText = @"
                    SELECT n.uid, n.package, n.channel_name, n.title, n.text, n.category, n.posted_at, n.is_read, n.is_persian, n.extracted_code, n.attributed_package
                    FROM notification_fts f
                    JOIN notifications n ON n.id = f.rowid
                    WHERE notification_fts MATCH @query
                    ORDER BY f.rank, n.posted_at DESC
                    LIMIT @limit";
                fallbackCmd.Parameters.AddWithValue("@query", $"\"{sanitizedQuery.Replace("\"", "\"\"")}\"");
                fallbackCmd.Parameters.AddWithValue("@limit", limit);

                try
                {
                    using var reader = fallbackCmd.ExecuteReader();
                    while (reader.Read())
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
                            ExtractedCode: reader.IsDBNull(9) ? null : reader.GetString(9),
                            AttributedPackage: reader.IsDBNull(10) ? null : reader.GetString(10)
                        ));
                    }
                }
                catch
                {
                    // Return empty on unresolvable syntax error
                }
            }

            return Task.FromResult<IReadOnlyList<Notification>>(results);
        }
    }

    public Task<int> PruneExpiredAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        lock (_db.SyncRoot)
        {
            var cutoff = DateTimeOffset.UtcNow.Subtract(maxAge).ToUnixTimeMilliseconds();
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "DELETE FROM notifications WHERE posted_at < @cutoff AND pinned = 0";
            cmd.Parameters.AddWithValue("@cutoff", cutoff);
            var affected = cmd.ExecuteNonQuery();
            return Task.FromResult(affected);
        }
    }

    public Task UpsertAppInfoAsync(string package, string? label, byte[]? iconPng)
    {
        if (string.IsNullOrWhiteSpace(package)) return Task.CompletedTask;

        lock (_db.SyncRoot)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO apps(package, label, first_seen_at, icon_png)
                VALUES (@p, @label, @t, @icon)
                ON CONFLICT(package) DO UPDATE SET
                    label = COALESCE(excluded.label, apps.label),
                    icon_png = COALESCE(excluded.icon_png, apps.icon_png)";
            cmd.Parameters.AddWithValue("@p", package);
            cmd.Parameters.AddWithValue("@label", (object?)label ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", now);
            cmd.Parameters.AddWithValue("@icon", (object?)iconPng ?? DBNull.Value);
            cmd.ExecuteNonQuery();
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyDictionary<string, AppIdentity>> GetAppInfosAsync(IReadOnlyCollection<string> packages)
    {
        var result = new Dictionary<string, AppIdentity>(StringComparer.Ordinal);
        if (packages.Count == 0) return Task.FromResult<IReadOnlyDictionary<string, AppIdentity>>(result);

        lock (_db.SyncRoot)
        {
            using var cmd = _db.CreateCommand();
            var names = new List<string>(packages.Count);
            for (var i = 0; i < packages.Count; i++)
            {
                var name = $"@p{i}";
                names.Add(name);
                cmd.Parameters.AddWithValue(name, packages.ElementAt(i));
            }

            cmd.CommandText = $"SELECT package, label, icon_png FROM apps WHERE package IN ({string.Join(", ", names)})";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var package = reader.GetString(0);
                var label = reader.IsDBNull(1) ? null : reader.GetString(1);
                var icon = reader.IsDBNull(2) ? null : (byte[])reader["icon_png"];
                result[package] = new AppIdentity(package, label, icon);
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, AppIdentity>>(result);
    }

    public Task<IReadOnlyList<string>> GetPackagesWithoutLabelAsync(int limit)
    {
        lock (_db.SyncRoot)
        {
            var packages = new List<string>();
            using var cmd = _db.CreateCommand();
            cmd.CommandText = @"
                SELECT package FROM apps
                WHERE label IS NULL OR label = ''
                ORDER BY last_seen_at DESC
                LIMIT @limit";
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                packages.Add(reader.GetString(0));
            }

            return Task.FromResult<IReadOnlyList<string>>(packages);
        }
    }
}