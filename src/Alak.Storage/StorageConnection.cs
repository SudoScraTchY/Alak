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

    internal SqliteConnection Underlying => _con;

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

    /// <summary>Execute a DELETE and return the number of affected rows.</summary>
    public int ExecuteDelete(string sql, params (string, object?)[] args)
    {
        using var cmd = _con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd.ExecuteNonQuery();
    }

    public void Dispose() => _con.Dispose();
}

/// <summary>Versioned schema migrations (spec §6.6): schema_migrations table is normative.</summary>
internal static class Migrations
{
    public static void Apply(SqliteConnection con)
    {
        con.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS schema_migrations (version INTEGER PRIMARY KEY, applied_at INTEGER NOT NULL);");

        var applied = new HashSet<long>();
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText = "SELECT version FROM schema_migrations";
            using var r = cmd.ExecuteReader();
            while (r.Read()) applied.Add(r.GetInt64(0));
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var kv in All().OrderBy(kv => kv.Item1))
        {
            var version = kv.Item1;
            var sql = kv.Item2;
            if (applied.Contains(version)) continue;
            using var tx = con.BeginTransaction();
            using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
            cmd.CommandText = "INSERT INTO schema_migrations(version, applied_at) VALUES ($v, $t)";
            cmd.Parameters.AddWithValue("$v", version);
            cmd.Parameters.AddWithValue("$t", now);
            cmd.ExecuteNonQuery();
            tx.Commit();
        }
    }

    private static IEnumerable<(long, string)> All() => new[]
    {
        (1L, """
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
              depth           INTEGER NOT NULL,
              redacted        INTEGER NOT NULL DEFAULT 0,
              langs           TEXT,
              category        TEXT NOT NULL DEFAULT 'other',
              category_source TEXT NOT NULL DEFAULT 'rules',
              confidence      REAL NOT NULL DEFAULT 0,
              needs_review    INTEGER NOT NULL DEFAULT 0,
              tags_json       TEXT,
              evidence_json   TEXT,
              content_hash    TEXT NOT NULL,
              dup_of          INTEGER REFERENCES notifications(id),
              dup_count       INTEGER NOT NULL DEFAULT 1,
              state           INTEGER NOT NULL DEFAULT 0,
              pinned          INTEGER NOT NULL DEFAULT 0,
              rules_version   TEXT,
              enriched_at     INTEGER
            );
            CREATE INDEX ix_n_posted   ON notifications(posted_at DESC);
            CREATE INDEX ix_n_sbn     ON notifications(sbn_key);
            CREATE INDEX ix_n_review  ON notifications(posted_at DESC) WHERE needs_review = 1;
            CREATE INDEX ix_n_unenriched ON notifications(id) WHERE enriched_at IS NULL;

            CREATE TABLE entities (
              id              INTEGER PRIMARY KEY,
              notification_id INTEGER NOT NULL REFERENCES notifications(id) ON DELETE CASCADE,
              type            TEXT NOT NULL,
              value_json      TEXT NOT NULL,
              field           TEXT NOT NULL,
              span_start      INTEGER,
              span_end        INTEGER,
              confidence      REAL NOT NULL,
              rule_id         TEXT
            );
            CREATE INDEX ix_e_notif ON entities(notification_id);
            CREATE INDEX ix_e_type  ON entities(type);

            CREATE TABLE promo_codes (
              id              INTEGER PRIMARY KEY,
              code            TEXT NOT NULL,
              code_norm       TEXT NOT NULL,
              package         TEXT NOT NULL REFERENCES apps(package),
              store_name      TEXT,
              kind            TEXT NOT NULL DEFAULT 'promo',
              discount_json   TEXT,
              min_spend_json  TEXT,
              cap_json        TEXT,
              conditions_json TEXT,
              first_seen_at   INTEGER NOT NULL,
              last_seen_at    INTEGER NOT NULL,
              times_seen      INTEGER NOT NULL DEFAULT 1,
              notification_id INTEGER REFERENCES notifications(id) ON DELETE SET NULL,
              valid_until     INTEGER,
              valid_until_precision INTEGER,
              status          INTEGER NOT NULL DEFAULT 0,
              used_at         INTEGER,
              note            TEXT,
              reminder_at     INTEGER,
              UNIQUE (code_norm, package)
            );

            CREATE VIRTUAL TABLE notification_fts USING fts5(
              title_n, body_n, sender_n,
              tokenize = 'unicode61 remove_diacritics 2'
            );

            CREATE TABLE user_rules (
              id INTEGER PRIMARY KEY, name TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1,
              priority INTEGER NOT NULL DEFAULT 100, match_json TEXT NOT NULL, action_json TEXT NOT NULL,
              hit_count INTEGER NOT NULL DEFAULT 0, created_at INTEGER NOT NULL
            );

            CREATE TABLE corrections (
              id INTEGER PRIMARY KEY, notification_id INTEGER, package TEXT NOT NULL,
              features_blob BLOB NOT NULL,
              from_category TEXT, to_category TEXT NOT NULL, created_at INTEGER NOT NULL
            );

            CREATE TABLE digests (
              id INTEGER PRIMARY KEY, day TEXT NOT NULL, kind TEXT NOT NULL,
              content_json TEXT NOT NULL, model_id INTEGER, created_at INTEGER NOT NULL,
              UNIQUE (day, kind)
            );

            CREATE TABLE slm_results (
              id INTEGER PRIMARY KEY, notification_id INTEGER REFERENCES notifications(id) ON DELETE CASCADE,
              task TEXT NOT NULL, model_id INTEGER, input_hash TEXT NOT NULL, output_json TEXT,
              valid INTEGER NOT NULL DEFAULT 0, tokens_in INTEGER, tokens_out INTEGER,
              duration_ms INTEGER, created_at INTEGER NOT NULL
            );

            CREATE TABLE models (
              id INTEGER PRIMARY KEY, display_name TEXT NOT NULL, path TEXT NOT NULL, format TEXT NOT NULL,
              engine TEXT NOT NULL, size_bytes INTEGER NOT NULL, sha256 TEXT NOT NULL, metadata_json TEXT,
              benchmark_json TEXT, added_at INTEGER NOT NULL, last_used_at INTEGER, is_active INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE packs (
              id TEXT PRIMARY KEY, version TEXT NOT NULL, enabled INTEGER NOT NULL DEFAULT 1,
              source TEXT NOT NULL, sha256 TEXT NOT NULL, installed_at INTEGER NOT NULL
            );

            CREATE TABLE settings (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            """),
    };
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
