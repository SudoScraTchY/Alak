using System.Text.RegularExpressions;

namespace Alak.Core.Model;

/// <summary>
/// A lower-kebab-case ASCII tag (spec §6.2). Tags are validated at the boundary:
/// display names always come from app resources, never from pack strings.
/// </summary>
public readonly record struct Tag(string Id)
{
    private static readonly Regex s_valid =
        new("^[a-z][a-z0-9]*(-[a-z][a-z0-9]*)*$", RegexOptions.NonBacktracking | RegexOptions.Compiled);

    /// <summary>The initial vocabulary (spec §6.2); packs may extend it at load time.</summary>
    public static IReadOnlySet<string> Known { get; private set; } = new HashSet<string>(new[]
    {
        "promo-code", "discount", "free-shipping", "cashback", "flash-sale", "expiring",
        "referral", "gift-credit", "no-code-needed", "newsletter", "unsubscribe-available",
        "urgent", "meeting", "mention", "review-requested", "build-status", "incident",
        "invoice", "payment-sent", "payment-received", "bill-due", "low-balance", "tax",
        "delivery", "out-for-delivery", "delivered", "ride", "ride-pin", "flight", "booking",
        "verification-code", "login-alert", "password-change", "missed-call", "message",
        "group-message", "like", "follow", "update-available", "system", "weather",
        "news", "reminder",
    }, StringComparer.Ordinal);

    /// <summary>Register pack-provided tags (called at pack load; validated kebab ids only).</summary>
    public static void RegisterPackTags(IEnumerable<string> ids)
    {
        var set = new HashSet<string>(Known, StringComparer.Ordinal);
        foreach (var id in ids)
            if (IsValid(id)) set.Add(id);
        Known = set;
    }

    /// <summary>True when the string is a syntactically valid lower-kebab tag.</summary>
    public static bool IsValid(string id)
        => !string.IsNullOrEmpty(id) && id.Length <= 64 && s_valid.IsMatch(id);

    /// <summary>True when the tag is valid AND in the known vocabulary.</summary>
    public static bool IsKnown(string id) => IsValid(id) && Known.Contains(id);

    public override string ToString() => Id;
}

/// <summary>
/// Where a classification came from (spec §6.6): rules, adaptive, slm, user, or app-default.
/// </summary>
public enum CategorySource
{
    Rules,
    Adaptive,
    Slm,
    User,
    AppDefault,
}
