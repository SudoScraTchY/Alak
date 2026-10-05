using Alak.Core.Model;
using Alak.Storage;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Storage;

/// <summary>
/// SQLite implementation of IUserRulesRepository (spec §11).
/// </summary>
public sealed class UserRulesRepository : IUserRulesRepository
{
    private readonly StorageConnection _db;

    public UserRulesRepository(StorageConnection db)
    {
        _db = db;
        EnsureTables();
    }

    private void EnsureTables()
    {
        _db.Execute(@"
            CREATE TABLE IF NOT EXISTS user_rules (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                priority INTEGER NOT NULL,
                match_json TEXT NOT NULL,
                action_json TEXT NOT NULL,
                label TEXT,
                enabled INTEGER NOT NULL DEFAULT 1,
                created_at INTEGER NOT NULL DEFAULT (strftime('%s','now') * 1000),
                updated_at INTEGER
            );
            
            CREATE INDEX IF NOT EXISTS idx_user_rules_priority ON user_rules(priority, id);
            CREATE INDEX IF NOT EXISTS idx_user_rules_enabled ON user_rules(enabled);
        ");

        _db.Execute(@"
            CREATE TABLE IF NOT EXISTS app_settings (
                package TEXT PRIMARY KEY,
                capture_depth_override INTEGER,
                mute INTEGER NOT NULL DEFAULT 0,
                auto_archive_on_read INTEGER NOT NULL DEFAULT 0,
                default_category_override TEXT,
                retention_override_days INTEGER,
                ignore_promo_code_detection INTEGER NOT NULL DEFAULT 0,
                display_name TEXT
            );
        ");

        _db.Execute(@"
            CREATE TABLE IF NOT EXISTS global_settings (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                theme TEXT NOT NULL DEFAULT 'system',
                ui_language TEXT NOT NULL DEFAULT 'system',
                calendar TEXT NOT NULL DEFAULT 'auto',
                digits TEXT NOT NULL DEFAULT 'auto',
                retention_overrides_json TEXT,
                global_size_cap INTEGER,
                tier2_enabled INTEGER NOT NULL DEFAULT 1,
                slm_performance_profile TEXT NOT NULL DEFAULT 'balanced',
                app_lock_enabled INTEGER NOT NULL DEFAULT 0,
                hide_amounts_in_lists INTEGER NOT NULL DEFAULT 0,
                notification_reminders_enabled INTEGER NOT NULL DEFAULT 1
            );
            
            INSERT OR IGNORE INTO global_settings (id) VALUES (1);
        ");
    }

    // User Rules
    public Task<List<UserRule>> GetAllRulesAsync()
    {
        var list = new List<UserRule>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id, priority, match_json, action_json, label, enabled, created_at, updated_at FROM user_rules ORDER BY priority, id";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var match = System.Text.Json.JsonSerializer.Deserialize<UserRuleMatch>(r.GetString(2))!;
            var action = System.Text.Json.JsonSerializer.Deserialize<UserRuleAction>(r.GetString(3))!;
            list.Add(new UserRule(
                r.GetInt64(0), r.GetInt32(1), match, action,
                r.IsDBNull(4) ? null : r.GetString(4),
                r.GetInt64(5) != 0,
                r.GetInt64(6),
                r.IsDBNull(7) ? 0 : r.GetInt64(7)));
        }
        return Task.FromResult(list);
    }

    public Task<UserRule?> GetRuleAsync(long id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id, priority, match_json, action_json, label, enabled, created_at, updated_at FROM user_rules WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return Task.FromResult<UserRule?>(null);
        
        var match = System.Text.Json.JsonSerializer.Deserialize<UserRuleMatch>(r.GetString(2))!;
        var action = System.Text.Json.JsonSerializer.Deserialize<UserRuleAction>(r.GetString(3))!;
        return Task.FromResult<UserRule?>(new UserRule(
            r.GetInt64(0), r.GetInt32(1), match, action,
            r.IsDBNull(4) ? null : r.GetString(4),
            r.GetInt64(5) != 0,
            r.GetInt64(6),
            r.IsDBNull(7) ? 0 : r.GetInt64(7)));
    }

    public Task<long> InsertRuleAsync(UserRule rule)
    {
        var matchJson = System.Text.Json.JsonSerializer.Serialize(rule.Match);
        var actionJson = System.Text.Json.JsonSerializer.Serialize(rule.Action);
        
        _db.Execute(
            "INSERT INTO user_rules (priority, match_json, action_json, label, enabled, created_at, updated_at) VALUES ($p, $m, $a, $l, $e, $t, $u)",
            ("$p", rule.Priority), ("$m", matchJson), ("$a", actionJson),
            ("$l", (object?)rule.Label ?? DBNull.Value), ("$e", rule.Enabled ? 1 : 0),
            ("$t", rule.CreatedAt == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : rule.CreatedAt),
            ("$u", rule.UpdatedAt == 0 ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() : rule.UpdatedAt));
        
        return Task.FromResult(_db.LastInsertRowId);
    }

    public Task UpdateRuleAsync(UserRule rule)
    {
        var matchJson = System.Text.Json.JsonSerializer.Serialize(rule.Match);
        var actionJson = System.Text.Json.JsonSerializer.Serialize(rule.Action);
        
        _db.Execute(
            "UPDATE user_rules SET priority=$p, match_json=$m, action_json=$a, label=$l, enabled=$e, updated_at=$u WHERE id=$id",
            ("$p", rule.Priority), ("$m", matchJson), ("$a", actionJson),
            ("$l", (object?)rule.Label ?? DBNull.Value), ("$e", rule.Enabled ? 1 : 0), 
            ("$u", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()), ("$id", rule.Id));
        return Task.CompletedTask;
    }

    public Task DeleteRuleAsync(long id)
    {
        _db.Execute("DELETE FROM user_rules WHERE id=$id", ("$id", id));
        return Task.CompletedTask;
    }

    public Task ReorderRulesAsync(IReadOnlyList<long> ruleIdsInOrder)
    {
        using var tx = _db.BeginTransaction();
        try
        {
            for (int i = 0; i < ruleIdsInOrder.Count; i++)
            {
                _db.Execute("UPDATE user_rules SET priority=$p WHERE id=$id", tx, ("$p", i), ("$id", ruleIdsInOrder[i]));
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        return Task.CompletedTask;
    }

    // App Settings
    public Task<List<AppSettings>> GetAllAppSettingsAsync()
    {
        var list = new List<AppSettings>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT package, capture_depth_override, mute, auto_archive_on_read, default_category_override, retention_override_days, ignore_promo_code_detection, display_name FROM app_settings";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new AppSettings(
                r.GetString(0),
                r.IsDBNull(1) ? null : (CaptureDepth?)r.GetInt32(1),
                r.GetInt64(2) != 0,
                r.GetInt64(3) != 0,
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetInt32(5),
                r.GetInt64(6) != 0));
        }
        return Task.FromResult(list);
    }

    public Task<AppSettings?> GetAppSettingsAsync(string package)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT package, capture_depth_override, mute, auto_archive_on_read, default_category_override, retention_override_days, ignore_promo_code_detection, display_name FROM app_settings WHERE package=$p";
        cmd.Parameters.AddWithValue("$p", package);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return Task.FromResult<AppSettings?>(null);
        return Task.FromResult<AppSettings?>(new AppSettings(
            r.GetString(0),
            r.IsDBNull(1) ? null : (CaptureDepth?)r.GetInt32(1),
            r.GetInt64(2) != 0,
            r.GetInt64(3) != 0,
            r.IsDBNull(4) ? null : r.GetString(4),
            r.IsDBNull(5) ? null : r.GetInt32(5),
            r.GetInt64(6) != 0));
    }

    public Task UpsertAppSettingsAsync(AppSettings settings)
    {
        _db.Execute(@"
            INSERT INTO app_settings (package, capture_depth_override, mute, auto_archive_on_read, default_category_override, retention_override_days, ignore_promo_code_detection, display_name)
            VALUES ($p, $c, $m, $a, $d, $r, $i, $n)
            ON CONFLICT(package) DO UPDATE SET
                capture_depth_override=$c, mute=$m, auto_archive_on_read=$a,
                default_category_override=$d, retention_override_days=$r, ignore_promo_code_detection=$i, display_name=$n",
            ("$p", settings.Package),
            ("$c", (object?)settings.CaptureDepthOverride ?? DBNull.Value),
            ("$m", settings.Mute ? 1 : 0),
            ("$a", settings.AutoArchiveOnRead ? 1 : 0),
            ("$d", (object?)settings.DefaultCategoryOverride ?? DBNull.Value),
            ("$r", (object?)settings.RetentionOverrideDays ?? DBNull.Value),
            ("$i", settings.IgnorePromoCodeDetection ? 1 : 0),
            ("$n", (object?)settings.DisplayName ?? DBNull.Value));
        return Task.CompletedTask;
    }

    // Global settings
    public Task<GlobalSettings> GetGlobalSettingsAsync()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT theme, ui_language, calendar, digits, retention_overrides_json, global_size_cap, tier2_enabled, slm_performance_profile, app_lock_enabled, hide_amounts_in_lists, notification_reminders_enabled FROM global_settings WHERE id=1";
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return Task.FromResult(new GlobalSettings("system", "system", "auto", "auto", null, null, true, "balanced", false, false, true));
        
        var retJson = r.IsDBNull(4) ? null : r.GetString(4);
        var ret = retJson == null ? null : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(retJson);
        
        return Task.FromResult(new GlobalSettings(
            r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), ret,
            r.IsDBNull(5) ? null : r.GetInt32(5),
            r.GetInt64(6) != 0, r.GetString(7),
            r.GetInt64(8) != 0, r.GetInt64(9) != 0,
            r.GetInt64(10) != 0));
    }

    public Task UpdateGlobalSettingsAsync(GlobalSettings settings)
    {
        var retJson = settings.RetentionOverrides == null ? null : System.Text.Json.JsonSerializer.Serialize(settings.RetentionOverrides);
        
        _db.Execute(@"
            UPDATE global_settings SET
                theme=$theme, ui_language=$lang, calendar=$cal, digits=$dig,
                retention_overrides_json=$ret, global_size_cap=$cap, tier2_enabled=$t2,
                slm_performance_profile=$slm, app_lock_enabled=$lock, hide_amounts_in_lists=$hide,
                notification_reminders_enabled=$remind
            WHERE id=1",
            ("$theme", settings.Theme), ("$lang", settings.UiLanguage), ("$cal", settings.Calendar), ("$dig", settings.Digits),
            ("$ret", (object?)retJson ?? DBNull.Value), ("$cap", (object?)settings.GlobalSizeCap ?? DBNull.Value),
            ("$t2", settings.Tier2Enabled ? 1 : 0), ("$slm", settings.SlmPerformanceProfile),
            ("$lock", settings.AppLockEnabled ? 1 : 0), ("$hide", settings.HideAmountsInLists ? 1 : 0),
            ("$remind", settings.NotificationRemindersEnabled ? 1 : 0));
        return Task.CompletedTask;
    }

    // Sources
    public Task<List<SourceGroup>> GetSourceGroupsAsync(int days = 30)
    {
        var list = new List<SourceGroup>();
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days).ToUnixTimeMilliseconds();
        
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT n.package, COALESCE(a.display_name, n.package) as display_name, 
                   COUNT(*) as cnt, 
                   COALESCE(s.default_category_override, 'other') as def_cat
            FROM notifications n
            LEFT JOIN app_settings a ON a.package = n.package
            LEFT JOIN app_settings s ON s.package = n.package
            WHERE n.received_at >= $cutoff
            GROUP BY n.package
            ORDER BY cnt DESC";
        cmd.Parameters.AddWithValue("$cutoff", cutoff);
        
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new SourceGroup(
                r.GetString(0), r.GetString(1), r.GetInt64(2), r.GetString(3)));
        }
        return Task.FromResult(list);
    }

    public Task UpsertSourceGroupAsync(string package, string? displayName, string? defaultCategory)
    {
        // Update app_settings with display_name and default_category_override
        _db.Execute(@"
            INSERT INTO app_settings (package, default_category_override, display_name)
            VALUES ($p, $d, $n)
            ON CONFLICT(package) DO UPDATE SET
                default_category_override=$d, display_name=$n",
            ("$p", package), ("$d", (object?)defaultCategory ?? DBNull.Value), ("$n", (object?)displayName ?? DBNull.Value));
        return Task.CompletedTask;
    }
}