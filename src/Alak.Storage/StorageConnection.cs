using Microsoft.Data.Sqlite;

namespace Alak.Storage;

/// <summary>
/// The single accessor for the Alak database (spec §6.6). Enforces WAL, foreign_keys ON,
/// and applies migrations. On-device this wraps a SQLCipher connection (SEC-002); the
/// class itself is transport-agnostic (plain SqliteConnection, so tests run anywhere).
/// </summary>
public sealed class StorageConnection : IDisposable
{
    private readonly SqliteConnection _con;

    public StorageConnection(string connectionString)
    {
        _con = new SqliteConnection(connectionString);
        _con.Open();
        using (var cmd = _con.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
        }
        Migrations.Apply(_con);
    }

    public SqliteConnection Underlying => _con;

    public long ExecuteScalar(string sql, params (string, object?)[] args)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public string ExecuteScalarText(string sql, params (string, object?)[] args)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    public void Execute(string sql, params (string, object?)[] args)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void Execute(string sql, SqliteTransaction tx, params (string, object?)[] args)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Execute a DELETE and return the number of affected rows.</summary>
    public int ExecuteDelete(string sql, params (string, object?)[] args)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    public void Dispose() => _con.Dispose();

    public long LastInsertRowId
    {
        get
        {
            using var cmd = _con.CreateCommand();
            cmd.CommandText = "SELECT last_insert_rowid();";
            return Convert.ToInt64(cmd.ExecuteScalar());
        }
    }

    public SqliteTransaction BeginTransaction()
    {
        return _con.BeginTransaction();
    }

    public SqliteCommand CreateCommand()
    {
        return _con.CreateCommand();
    }

    /// <summary>
    /// Database schema migrations.
    /// </summary>
    internal static class Migrations
    {
        private static IEnumerable<(long, string)> All() => new[]
        {
            (1L, @"
                CREATE TABLE apps (
                  package         TEXT PRIMARY KEY,
                  label           TEXT,
                  first_seen_at   INTEGER NOT NULL,
                  default_category TEXT,
                  depth           INTEGER,
                  muted           INTEGER NOT NULL DEFAULT 0,
                  auto_archive    INTEGER NOT NULL DEFAULT 0,
                  region_hint     TEXT,
                  notif_count     INTEGER NOT NULL DEFAULT 0,
                  last_seen_at    INTEGER,
                  icon_png        BLOB
                );

                CREATE TABLE notifications (
                  id              INTEGER PRIMARY KEY,
                  uid             TEXT NOT NULL UNIQUE,
                  sbn_key         TEXT NOT NULL,
                  package         TEXT NOT NULL REFERENCES apps(package),
                  posted_at       INTEGER NOT NULL,
                  captured_at     INTEGER NOT NULL,
                  updated_at      INTEGER NOT NULL,
                  removed_at      INTEGER,
                  removal_reason  INTEGER,
                  tz_offset_min   INTEGER NOT NULL,
                  channel_id      TEXT,
                  channel_name    TEXT,
                  android_category TEXT,
                  importance      INTEGER,
                  group_key       TEXT,
                  title           TEXT,
                  text            TEXT,
                  big_text        TEXT,
                  sub_text        TEXT,
                  lines_json      TEXT,
                  actions_json    TEXT,
                  depth           INTEGER,
                  category        TEXT,
                  content_hash    TEXT,
                                    pinned          INTEGER NOT NULL DEFAULT 0,
                                    state           INTEGER NOT NULL DEFAULT 0,
                                    account_email   TEXT,
                                    evidence_json   TEXT,
                                    needs_review    INTEGER NOT NULL DEFAULT 0
                                  );

                CREATE INDEX ix_notifications_package ON notifications(package);
                CREATE INDEX ix_notifications_posted ON notifications(posted_at);

                CREATE TABLE packs (
                  id TEXT PRIMARY KEY, version TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1,
                  source TEXT NOT NULL, sha256 TEXT NOT NULL, installed_at INTEGER NOT NULL
                );

                CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);

                                -- FTS5 virtual table for full-text search (PF-10)
                                CREATE VIRTUAL TABLE notification_fts USING fts5(
                                  title_n, body_n, sender_n,
                                  tokenize = 'unicode61 remove_diacritics 2'
                                );

                                -- Additional tables per spec §6.6
                                CREATE TABLE entities (
                                  id INTEGER PRIMARY KEY, notification_id INTEGER NOT NULL REFERENCES notifications(id) ON DELETE CASCADE,
                                  type INTEGER NOT NULL, value TEXT NOT NULL, span_start INTEGER, span_end INTEGER
                                );
                                CREATE INDEX ix_entities_notification ON entities(notification_id);

                                CREATE TABLE promo_codes (
                                  code_norm TEXT PRIMARY KEY, raw_code TEXT, valid_until_precision INTEGER,
                                  first_seen INTEGER, last_seen INTEGER, use_count INTEGER NOT NULL DEFAULT 0,
                                  source_package TEXT REFERENCES apps(package)
                                );

                                CREATE TABLE corrections (
                                  id INTEGER PRIMARY KEY, original_uid TEXT NOT NULL, corrected_uid TEXT,
                                  features_blob BLOB, confidence REAL, applied_at INTEGER
                                );
                                CREATE INDEX ix_corrections_original ON corrections(original_uid);

                                CREATE TABLE digests (
                                  id INTEGER PRIMARY KEY, period_start INTEGER NOT NULL, period_end INTEGER NOT NULL,
                                  category INTEGER NOT NULL, summary_json TEXT NOT NULL, generated_at INTEGER NOT NULL
                                );

                                CREATE TABLE slm_results (
                                  id INTEGER PRIMARY KEY, notification_uid TEXT NOT NULL,
                                  intent TEXT, confidence REAL, latency_ms INTEGER, model_sha TEXT,
                                  created_at INTEGER NOT NULL
                                );
                                CREATE INDEX ix_slm_results_notification ON slm_results(notification_uid);

                                CREATE TABLE models (
                                  sha256 TEXT PRIMARY KEY, variant TEXT, quant TEXT, bytes INTEGER,
                                  created_at INTEGER, source_url TEXT
                                );
                            "),

            // Migration 3: user_rules table + global_settings table (M6, §11.1–11.4, PER-001..004)
            (3L, @"
                CREATE TABLE user_rules (
                  id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                  priority            INTEGER NOT NULL,
                  enabled             INTEGER NOT NULL DEFAULT 1,
                  match_package       TEXT,
                  match_channel       TEXT,
                  match_title_regex   TEXT,
                  match_text_regex    TEXT,
                  match_sender_regex  TEXT,
                  match_mode          INTEGER NOT NULL DEFAULT 0,
                  target_category     INTEGER,
                  target_read_state   INTEGER,
                  target_importance   INTEGER,
                  action_label_override TEXT,
                  created_at          TEXT NOT NULL,
                  updated_at          TEXT NOT NULL
                );
                CREATE INDEX ix_user_rules_priority_enabled ON user_rules(priority, enabled);

                CREATE TABLE global_settings (
                  key   TEXT PRIMARY KEY,
                  value TEXT NOT NULL
                );
            "),

            // Migration 4: app-facing columns used by NotificationRepository (M8)
            (4L, @"
                ALTER TABLE notifications ADD COLUMN is_read INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE notifications ADD COLUMN is_persian INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE notifications ADD COLUMN extracted_code TEXT;
            "),

            // Migration 5: promo_codes schema from migration 3 predates the §13.8 wallet schema
            // PromoCodeRepository expects. Recreate with the normative wallet schema.
            (5L, @"
                DROP TABLE IF EXISTS promo_codes;
                CREATE TABLE promo_codes (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  code_norm TEXT NOT NULL,
                  raw_code TEXT NOT NULL,
                  package TEXT NOT NULL,
                  account_email TEXT,
                  channel_id TEXT,
                  status INTEGER NOT NULL DEFAULT 0,
                  confidence REAL NOT NULL,
                  kind INTEGER NOT NULL DEFAULT 0,
                  discount_value REAL,
                  discount_type INTEGER,
                  currency TEXT,
                  min_spend REAL,
                  cap REAL,
                  scope TEXT,
                  first_order INTEGER NOT NULL DEFAULT 0,
                  single_use INTEGER NOT NULL DEFAULT 0,
                  valid_from INTEGER,
                  valid_until INTEGER,
                  store_hint TEXT,
                  first_seen_at INTEGER NOT NULL,
                  last_seen_at INTEGER NOT NULL,
                  times_seen INTEGER NOT NULL DEFAULT 1,
                  reminder_24h_at INTEGER,
                  reminder_morning_at INTEGER,
                  reminder_custom_at INTEGER,
                  UNIQUE(code_norm, package)
                );
                CREATE INDEX IF NOT EXISTS idx_promo_codes_package ON promo_codes(package);
                CREATE INDEX IF NOT EXISTS idx_promo_codes_status ON promo_codes(status);
                CREATE INDEX IF NOT EXISTS idx_promo_codes_valid_until ON promo_codes(valid_until);
            "),

            // Migration 6: global_settings from migration 3 used a key/value draft schema;
            // UserRulesRepository expects the §11.4 single-row schema. Recreate normatively.
            (6L, @"
                DROP TABLE IF EXISTS global_settings;
                CREATE TABLE global_settings (
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
                INSERT INTO global_settings (id) VALUES (1);
            "),

            // Migration 7: reconcile user_rules schema with UserRulesRepository expectations (§11.1)
            (7L, @"
                DROP TABLE IF EXISTS user_rules;
                CREATE TABLE user_rules (
                  id INTEGER PRIMARY KEY AUTOINCREMENT,
                  priority INTEGER NOT NULL,
                  match_json TEXT NOT NULL,
                  action_json TEXT NOT NULL,
                  label TEXT,
                  enabled INTEGER NOT NULL DEFAULT 1,
                  created_at INTEGER NOT NULL,
                  updated_at INTEGER
                );
                CREATE INDEX IF NOT EXISTS idx_user_rules_priority ON user_rules(priority, id);
                CREATE INDEX IF NOT EXISTS idx_user_rules_enabled ON user_rules(enabled);
            "),
        };

        public static void Apply(SqliteConnection con)
        {
            con.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, applied_at INTEGER NOT NULL);");

            var applied = new HashSet<long>();
            using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "SELECT version FROM schema_migrations";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    applied.Add(reader.GetInt64(0));
            }

            foreach (var (version, sql) in All())
            {
                if (!applied.Contains(version))
                {
                    using var tx = con.BeginTransaction();
                    // Split by semicolon and execute each statement separately
                    foreach (var stmt in sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!string.IsNullOrWhiteSpace(stmt))
                        {
                            con.ExecuteNonQuery(stmt);
                        }
                    }
                    con.ExecuteNonQuery($"INSERT INTO schema_migrations (version, applied_at) VALUES ({version}, {DateTimeOffset.UtcNow.ToUnixTimeSeconds()});");
                    tx.Commit();
                }
            }
        }
    }
}

internal static class SqliteExtensions
{
    public static void ExecuteNonQuery(this SqliteConnection con, string sql)
    {
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}