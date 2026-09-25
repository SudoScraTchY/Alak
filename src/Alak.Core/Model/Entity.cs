namespace Alak.Core.Model;

/// <summary>
/// Entities are structured facts pulled from text (spec §6.3). All carry field, span,
/// confidence and rule id via the base record; spans point into the ORIGINAL text
/// through the normalization span map (§8.2).
/// </summary>
public abstract record Entity(
    string Field,
    (int Start, int End) Span,
    float Confidence,
    string RuleId);

public enum PromoCodeKind { Promo, Referral, Gift, Voucher, Unknown }

public enum DiscountType { Percent, Amount, FreeShipping, Bogo, Cashback, Credit, Unknown }

public enum MoneyDirection { Debit, Credit, Balance, Unknown }

public enum DatetimeRole { Expiry, Start, Event, Due }

public enum DatetimePrecision { Date, Time }

public enum DatetimeCalendar { Gregorian, Jalali }

public enum OrderRefKind { Order, Tracking, Invoice, Ticket, Booking, Pin }

public enum SenderKind { Person, Brand, System }

/// <summary>Promo/discount code — central to the Codes wallet (spec §6.3, E-01).</summary>
public sealed record PromoCodeEntity(
    string Code,
    PromoCodeKind Kind,
    DiscountEntity? Discount,
    decimal? MinSpend,
    decimal? Cap,
    string? Scope,
    bool FirstOrder,
    bool SingleUse,
    DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidUntil,
    string? StoreHint,
    string Field = "text",
    (int Start, int End) Span = default,
    float Confidence = 1f,
    string RuleId = "E-01") : Entity(Field, Span, Confidence, RuleId);

/// <summary>A discount may exist without a code (spec §6.3).</summary>
public sealed record DiscountEntity(
    DiscountType Type,
    decimal? Value,
    string? Currency,
    decimal? UpTo,
    string Field = "text",
    (int Start, int End) Span = default,
    float Confidence = 1f,
    string RuleId = "E-02") : Entity(Field, Span, Confidence, RuleId);

/// <summary>
/// Money amount. Toman (IRT) and Rial (IRR) are distinct units: 10 Rial = 1 Toman.
/// Store the unit the text used plus the normalized value (spec §6.3).
/// </summary>
public sealed record MoneyEntity(
    decimal Value,
    string Currency,
    MoneyDirection Direction,
    string? AccountHint,
    string Field = "text",
    (int Start, int End) Span = default,
    float Confidence = 1f,
    string RuleId = "E-03") : Entity(Field, Span, Confidence, RuleId);

/// <summary>A datetime; relative expressions resolve against posted_at (spec §6.3).</summary>
public sealed record DatetimeEntity(
    DatetimeRole Role,
    DateTimeOffset Instant,
    DatetimePrecision Precision,
    DatetimeCalendar Calendar,
    bool Ambiguous,
    string Field = "text",
    (int Start, int End) Span = default,
    float Confidence = 1f,
    string RuleId = "E-04") : Entity(Field, Span, Confidence, RuleId);

/// <summary>URL — never fetched, the app has no network (spec §6.3, R2).</summary>
public sealed record UrlEntity(
    string Url,
    string Host,
    string Field = "text",
    (int Start, int End) Span = default,
    float Confidence = 1f,
    string RuleId = "E-05") : Entity(Field, Span, Confidence, RuleId);

/// <summary>Phone number (spec §6.3).</summary>
public sealed record PhoneEntity(
    string Number,
    string Field = "text",
    (int Start, int End) Span = default,
    float Confidence = 1f,
    string RuleId = "E-06") : Entity(Field, Span, Confidence, RuleId);

/// <summary>
/// Order/tracking reference — prevents tracking numbers being mistaken for promo codes (spec §6.3).
/// </summary>
public sealed record OrderRefEntity(
    OrderRefKind Kind,
    string Value,
    string Field = "text",
    (int Start, int End) Span = default,
    float Confidence = 1f,
    string RuleId = "E-07") : Entity(Field, Span, Confidence, RuleId);

/// <summary>Sender identity from title/conversation/app (spec §6.3).</summary>
public sealed record SenderEntity(
    string Name,
    SenderKind Kind,
    string Field = "title",
    (int Start, int End) Span = default,
    float Confidence = 1f,
    string RuleId = "E-08") : Entity(Field, Span, Confidence, RuleId);

/// <summary>
/// OTP presence (spec §6.3): the VALUE IS NEVER PERSISTED — only presence and length.
/// The record deliberately has no member that can carry the code.
/// </summary>
public sealed record OtpEntity(
    int Length,
    string Field = "text",
    (int Start, int End) Span = default,
    float Confidence = 1f,
    string RuleId = "E-09") : Entity(Field, Span, Confidence, RuleId);
