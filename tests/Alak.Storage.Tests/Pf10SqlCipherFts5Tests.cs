using Microsoft.Data.Sqlite;

namespace Alak.Storage.Tests;

// PF-10: prove that the chosen SQLCipher bundle (SQLitePCLRaw.bundle_e_sqlcipher)
// can open an encrypted database AND create/query an FTS5 full-text index inside it.
// The spec lists this as a hard VERIFY item before building the storage layer on it.
public class Pf10SqlCipherFts5Tests
{
    [Fact]
    public void Encrypted_database_is_created_and_opens_only_with_key()
    {
        var path = Path.Combine(Path.GetTempPath(), $"alak-pf10-{Guid.NewGuid():N}.db");

        try
        {
            // 1) Create encrypted DB with FTS5 table
            var cs = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()
                + ";Password=test-key-123";
            using (var create = new SqliteConnection(cs))
            {
                create.Open();

                using var cmd = create.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE apps (package TEXT PRIMARY KEY);
                    CREATE VIRTUAL TABLE notification_fts USING fts5(
                      title_n, body_n, sender_n,
                      tokenize = 'unicode61 remove_diacritics 2'
                    );
                    INSERT INTO notification_fts (rowid, title_n, body_n, sender_n)
                      VALUES (1, 'کد تخفیف spring sale', '25% off with code SPRING25', 'Snapp');
                    INSERT INTO notification_fts (rowid, title_n, body_n, sender_n)
                      VALUES (2, 'hello world', 'your order shipped', 'Amazon');
                    """;
                cmd.ExecuteNonQuery();

                // 2) FTS5 query works, Persian + English
                cmd.CommandText = "SELECT rowid FROM notification_fts WHERE notification_fts MATCH 'تخفیف'";
                Convert.ToInt32(cmd.ExecuteScalar()).ShouldBe(1);

                cmd.CommandText = "SELECT rowid FROM notification_fts WHERE notification_fts MATCH 'spring*'";
                Convert.ToInt32(cmd.ExecuteScalar()).ShouldBe(1);
            }

            // 3) Opening WITHOUT the key must fail (file is actually encrypted)
            Should.Throw<SqliteException>(() =>
            {
                using var wrong = new SqliteConnection(
                    new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
                wrong.Open();
                using var c = wrong.CreateCommand();
                c.CommandText = "SELECT count(*) FROM sqlite_master";
                c.ExecuteScalar();
            });

            // 4) The raw file must not be plaintext-readable (SEC check)
            var raw = File.ReadAllBytes(path);
            var head = System.Text.Encoding.ASCII.GetString(raw, 0, 16);
            head.ShouldNotContain("SQLite format 3");
        }
        finally
        {
            File.Delete(path);
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(path + suffix)) File.Delete(path + suffix);
        }
    }
}
