using Alak.Core.Model;
using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.Storage;

/// <summary>
/// Repository interface for promo codes (Codes wallet, spec §10).
/// </summary>
public interface IPromoCodeRepository
{
    Task<List<PromoCode>> GetAllAsync();
    Task<List<PromoCode>> GetByStatusAsync(PromoCodeStatus status);
    Task<PromoCode?> GetByCodeNormAsync(string codeNorm, string package);
    Task<long> UpsertAsync(PromoCode code);
    Task UpdateAsync(PromoCode code);
    Task DeleteAsync(long id);
    Task<int> GetActiveCountAsync();
    Task<PromoCode?> GetSoonestExpiringAsync();
}

/// <summary>
/// Promo code status in the wallet (WAL-002, WAL-003).
/// </summary>
public enum PromoCodeStatus
{
    Active = 0,
    ExpiringSoon = 1,
    Used = 2,
    Dismissed = 3,
    Expired = 4,
    Maybe = 5 // WAL-005: possible code (confidence 0.40–0.60)
}

/// <summary>
/// Promo code wallet item (spec §10, WAL-001..007).
/// </summary>
public sealed record PromoCode(
    long Id,
    string CodeNorm,      // normalized for dedup key (lowercase, no spaces)
    string RawCode,       // original case for copy
    string Package,
    string? AccountEmail,
    string? ChannelId,
    PromoCodeStatus Status,
    float Confidence,
    PromoCodeKind Kind,
    decimal? DiscountValue,
    DiscountType? DiscountType,
    string? Currency,
    decimal? MinSpend,
    decimal? Cap,
    string? Scope,
    bool FirstOrder,
    bool SingleUse,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    string? StoreHint,
    long FirstSeenAt,
    long LastSeenAt,
    int TimesSeen,
    long? Reminder24hAt = null,
    long? ReminderMorningAt = null,
    long? ReminderCustomAt = null)
{
    public bool HasValidUntil => ValidUntil.HasValue;
    public bool IsExpiringSoon => ValidUntil.HasValue && ValidUntil.Value <= DateTimeOffset.UtcNow.AddHours(48);
    public string DisplayCode => RawCode;
    public string SourceDisplay => string.IsNullOrEmpty(AccountEmail) 
        ? (string.IsNullOrEmpty(ChannelId) ? Package : $"{Package} ({ChannelId})")
        : $"{Package} ({AccountEmail})";
}

/// <summary>
/// SQLite implementation of IPromoCodeRepository (spec §6.6/§10).
/// </summary>
public sealed class PromoCodeRepository : IPromoCodeRepository
{
    private readonly StorageConnection _db;

    public PromoCodeRepository(StorageConnection db)
    {
        _db = db;
        EnsureTable();
    }

    private void EnsureTable()
    {
        _db.Execute(@"
            CREATE TABLE IF NOT EXISTS promo_codes (
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
        ");
    }

    public async Task<List<PromoCode>> GetAllAsync()
    {
        var list = new List<PromoCode>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT id, code_norm, raw_code, package, account_email, channel_id, status, confidence,
                   kind, discount_value, discount_type, currency, min_spend, cap, scope,
                   first_order, single_use, valid_from, valid_until, store_hint,
                   first_seen_at, last_seen_at, times_seen,
                   reminder_24h_at, reminder_morning_at, reminder_custom_at
            FROM promo_codes
            ORDER BY 
                CASE status WHEN 1 THEN 0 WHEN 0 THEN 1 WHEN 2 THEN 2 WHEN 3 THEN 3 WHEN 4 THEN 4 ELSE 5 END,
                last_seen_at DESC";
        
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(ReadPromoCode(r));
        }
        return list;
    }

    public async Task<List<PromoCode>> GetByStatusAsync(PromoCodeStatus status)
    {
        var list = new List<PromoCode>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT id, code_norm, raw_code, package, account_email, channel_id, status, confidence,
                   kind, discount_value, discount_type, currency, min_spend, cap, scope,
                   first_order, single_use, valid_from, valid_until, store_hint,
                   first_seen_at, last_seen_at, times_seen,
                   reminder_24h_at, reminder_morning_at, reminder_custom_at
            FROM promo_codes
            WHERE status = @status
            ORDER BY last_seen_at DESC";
        cmd.Parameters.AddWithValue("@status", (int)status);
        
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(ReadPromoCode(r));
        }
        return list;
    }

    public async Task<PromoCode?> GetByCodeNormAsync(string codeNorm, string package)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT id, code_norm, raw_code, package, account_email, channel_id, status, confidence,
                   kind, discount_value, discount_type, currency, min_spend, cap, scope,
                   first_order, single_use, valid_from, valid_until, store_hint,
                   first_seen_at, last_seen_at, times_seen,
                   reminder_24h_at, reminder_morning_at, reminder_custom_at
            FROM promo_codes
            WHERE code_norm = @code_norm AND package = @package";
        cmd.Parameters.AddWithValue("@code_norm", codeNorm);
        cmd.Parameters.AddWithValue("@package", package);
        
        using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            return ReadPromoCode(r);
        }
        return null;
    }

    public async Task<long> UpsertAsync(PromoCode code)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO promo_codes (
                code_norm, raw_code, package, account_email, channel_id, status, confidence,
                kind, discount_value, discount_type, currency, min_spend, cap, scope,
                first_order, single_use, valid_from, valid_until, store_hint,
                first_seen_at, last_seen_at, times_seen,
                reminder_24h_at, reminder_morning_at, reminder_custom_at
            ) VALUES (
                @code_norm, @raw_code, @package, @account_email, @channel_id, @status, @confidence,
                @kind, @discount_value, @discount_type, @currency, @min_spend, @cap, @scope,
                @first_order, @single_use, @valid_from, @valid_until, @store_hint,
                @first_seen_at, @last_seen_at, @times_seen,
                @reminder_24h_at, @reminder_morning_at, @reminder_custom_at
            )
            ON CONFLICT(code_norm, package) DO UPDATE SET
                raw_code = @raw_code,
                account_email = @account_email,
                channel_id = @channel_id,
                status = @status,
                confidence = @confidence,
                kind = @kind,
                discount_value = @discount_value,
                discount_type = @discount_type,
                currency = @currency,
                min_spend = @min_spend,
                cap = @cap,
                scope = @scope,
                first_order = @first_order,
                single_use = @single_use,
                valid_from = @valid_from,
                valid_until = @valid_until,
                store_hint = @store_hint,
                last_seen_at = @last_seen_at,
                times_seen = @times_seen,
                reminder_24h_at = @reminder_24h_at,
                reminder_morning_at = @reminder_morning_at,
                reminder_custom_at = @reminder_custom_at;
            
            SELECT last_insert_rowid();";

        AddParameters(cmd, code);
        
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result);
    }

    public async Task UpdateAsync(PromoCode code)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            UPDATE promo_codes SET
                raw_code = @raw_code,
                account_email = @account_email,
                channel_id = @channel_id,
                status = @status,
                confidence = @confidence,
                kind = @kind,
                discount_value = @discount_value,
                discount_type = @discount_type,
                currency = @currency,
                min_spend = @min_spend,
                cap = @cap,
                scope = @scope,
                first_order = @first_order,
                single_use = @single_use,
                valid_from = @valid_from,
                valid_until = @valid_until,
                store_hint = @store_hint,
                last_seen_at = @last_seen_at,
                times_seen = @times_seen,
                reminder_24h_at = @reminder_24h_at,
                reminder_morning_at = @reminder_morning_at,
                reminder_custom_at = @reminder_custom_at
            WHERE id = @id";

        AddParameters(cmd, code);
        cmd.Parameters.AddWithValue("@id", code.Id);
        
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(long id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM promo_codes WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetActiveCountAsync()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM promo_codes WHERE status IN (0, 1)";
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<PromoCode?> GetSoonestExpiringAsync()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT id, code_norm, raw_code, package, account_email, channel_id, status, confidence,
                   kind, discount_value, discount_type, currency, min_spend, cap, scope,
                   first_order, single_use, valid_from, valid_until, store_hint,
                   first_seen_at, last_seen_at, times_seen,
                   reminder_24h_at, reminder_morning_at, reminder_custom_at
            FROM promo_codes
            WHERE status IN (0, 1) AND valid_until IS NOT NULL
            ORDER BY valid_until ASC
            LIMIT 1";
        
        using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            return ReadPromoCode(r);
        }
        return null;
    }

    private static PromoCode ReadPromoCode(SqliteDataReader r)
    {
        return new PromoCode(
            Id: r.GetInt64(0),
            CodeNorm: r.GetString(1),
            RawCode: r.GetString(2),
            Package: r.GetString(3),
            AccountEmail: r.IsDBNull(4) ? null : r.GetString(4),
            ChannelId: r.IsDBNull(5) ? null : r.GetString(5),
            Status: (PromoCodeStatus)r.GetInt32(6),
            Confidence: r.GetFloat(7),
            Kind: (PromoCodeKind)r.GetInt32(8),
            DiscountValue: r.IsDBNull(9) ? null : (decimal?)r.GetDouble(9),
            DiscountType: r.IsDBNull(10) ? null : (DiscountType?)r.GetInt32(10),
            Currency: r.IsDBNull(11) ? null : r.GetString(11),
            MinSpend: r.IsDBNull(12) ? null : (decimal?)r.GetDouble(12),
            Cap: r.IsDBNull(13) ? null : (decimal?)r.GetDouble(13),
            Scope: r.IsDBNull(14) ? null : r.GetString(14),
            FirstOrder: r.GetBoolean(15),
            SingleUse: r.GetBoolean(16),
            ValidFrom: r.IsDBNull(17) ? null : DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(17)),
            ValidUntil: r.IsDBNull(18) ? null : DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(18)),
            StoreHint: r.IsDBNull(19) ? null : r.GetString(19),
            FirstSeenAt: r.GetInt64(20),
            LastSeenAt: r.GetInt64(21),
            TimesSeen: r.GetInt32(22),
            Reminder24hAt: r.IsDBNull(23) ? null : r.GetInt64(23),
            ReminderMorningAt: r.IsDBNull(24) ? null : r.GetInt64(24),
            ReminderCustomAt: r.IsDBNull(25) ? null : r.GetInt64(25)
        );
    }

    private static void AddParameters(SqliteCommand cmd, PromoCode code)
    {
        cmd.Parameters.AddWithValue("@code_norm", code.CodeNorm);
        cmd.Parameters.AddWithValue("@raw_code", code.RawCode);
        cmd.Parameters.AddWithValue("@package", code.Package);
        cmd.Parameters.AddWithValue("@account_email", (object?)code.AccountEmail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@channel_id", (object?)code.ChannelId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", (int)code.Status);
        cmd.Parameters.AddWithValue("@confidence", code.Confidence);
        cmd.Parameters.AddWithValue("@kind", (int)code.Kind);
        cmd.Parameters.AddWithValue("@discount_value", (object?)code.DiscountValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@discount_type", code.DiscountType.HasValue ? (int)code.DiscountType.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@currency", (object?)code.Currency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@min_spend", (object?)code.MinSpend ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cap", (object?)code.Cap ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@scope", (object?)code.Scope ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@first_order", code.FirstOrder ? 1 : 0);
        cmd.Parameters.AddWithValue("@single_use", code.SingleUse ? 1 : 0);
        cmd.Parameters.AddWithValue("@valid_from", code.ValidFrom.HasValue ? code.ValidFrom.Value.ToUnixTimeMilliseconds() : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@valid_until", code.ValidUntil.HasValue ? code.ValidUntil.Value.ToUnixTimeMilliseconds() : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@store_hint", (object?)code.StoreHint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@first_seen_at", code.FirstSeenAt);
        cmd.Parameters.AddWithValue("@last_seen_at", code.LastSeenAt);
        cmd.Parameters.AddWithValue("@times_seen", code.TimesSeen);
        cmd.Parameters.AddWithValue("@reminder_24h_at", code.Reminder24hAt.HasValue ? code.Reminder24hAt.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@reminder_morning_at", code.ReminderMorningAt.HasValue ? code.ReminderMorningAt.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@reminder_custom_at", code.ReminderCustomAt.HasValue ? code.ReminderCustomAt.Value : (object)DBNull.Value);
    }
}