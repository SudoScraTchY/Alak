namespace Alak.Core.Capture;

/// <summary>
/// The immutable DTO handed from the capture service to the pipeline (spec §7.1).
/// All values are copied on the binder thread; NO Android objects escape it (CAP-003).
/// </summary>
/// <param name="SbnKey">Stable key of the StatusBarNotification row.</param>
/// <param name="Package">Source app package name.</param>
/// <param name="Id">Notification id from the app.</param>
/// <param name="Tag">Optional notification tag.</param>
/// <param name="PostedAtUtcMs">Post time in UTC epoch milliseconds.</param>
/// <param name="TzOffsetMin">Timezone offset minutes at post time (Jalali date rendering).</param>
/// <param name="ChannelId">Channel id (API 26+).</param>
/// <param name="ChannelName">Channel name shown to users.</param>
/// <param name="AndroidCategory">Notification.CATEGORY_* string, if set.</param>
/// <param name="Importance">Ranking importance 0–5.</param>
/// <param name="IsOngoing">FLAG_ONGOING_EVENT.</param>
/// <param name="IsGroupSummary">Group summary node (children captured separately).</param>
/// <param name="IsForegroundService">Foreground-service notification.</param>
/// <param name="IsClearable">FLAG_AUTO_CANCEL / clearable.</param>
/// <param name="GroupKey">Group key, if grouped.</param>
/// <param name="Title">Notification title (usually the sender).</param>
/// <param name="Text">Main text body.</param>
/// <param name="BigText">Expanded style big text.</param>
/// <param name="SubText">Sub text.</param>
/// <param name="TextLines">Inbox-style text lines.</param>
/// <param name="ActionLabels">Labels of actions (NEVER PendingIntents).</param>
/// <param name="ConversationTitle">MessagingStyle conversation title.</param>
/// <param name="LooksRedactedByOs">True when the OS handed redacted/placeholder text (CAP-008/PF-02).</param>
/// <param name="UserHandle">Android user id (work profile etc.).</param>
public sealed record RawNotification(
    string SbnKey,
    string Package,
    int Id,
    string? Tag,
    long PostedAtUtcMs,
    int TzOffsetMin,
    string? ChannelId,
    string? ChannelName,
    string? AndroidCategory,
    int Importance,
    bool IsOngoing,
    bool IsGroupSummary,
    bool IsForegroundService,
    bool IsClearable,
    string? GroupKey,
    string? Title,
    string? Text,
    string? BigText,
    string? SubText,
    IReadOnlyList<string> TextLines,
    IReadOnlyList<string> ActionLabels,
    string? ConversationTitle,
    bool LooksRedactedByOs,
    int UserHandle)
{
    /// <summary>Validated: sbn_key is mandatory (CAP-003).</summary>
    public string SbnKey { get; } = GuardNotBlank(SbnKey, nameof(SbnKey));

    /// <summary>Validated: package is mandatory (CAP-003).</summary>
    public string Package { get; } = GuardNotBlank(Package, nameof(Package));

    private static string GuardNotBlank(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{name} is required.", name)
            : value;

    /// <summary>
    /// Factory creating a basic RawNotification instance for testing and pipelines.
    /// </summary>
    public static RawNotification Create(
        string package,
        string? channelId = null,
        string? channelName = null,
        string? title = null,
        string? text = null,
        long postedAtUtcMs = 0,
        string? androidCategory = null) =>
        new(
            SbnKey: Guid.NewGuid().ToString("N"),
            Package: package,
            Id: 0,
            Tag: null,
            PostedAtUtcMs: postedAtUtcMs > 0 ? postedAtUtcMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TzOffsetMin: 0,
            ChannelId: channelId,
            ChannelName: channelName,
            AndroidCategory: androidCategory,
            Importance: 3,
            IsOngoing: false,
            IsGroupSummary: false,
            IsForegroundService: false,
            IsClearable: true,
            GroupKey: null,
            Title: title,
            Text: text,
            BigText: null,
            SubText: null,
            TextLines: Array.Empty<string>(),
            ActionLabels: Array.Empty<string>(),
            ConversationTitle: null,
            LooksRedactedByOs: false,
            UserHandle: 0);
}

/// <summary>
/// The default ignore filter (spec CAP-005): own package, ongoing, foreground-service,
/// group summaries, and CATEGORY_TRANSPORT/PROGRESS/SERVICE/CALL/NAVIGATION/ALARM/STOPWATCH.
/// </summary>
public static class DefaultIgnoreFilter
{
    private static readonly IReadOnlySet<string> s_categories = new HashSet<string>(new[]
    {
        "transport", "progress", "service", "call", "navigation", "alarm", "stopwatch",
    }, StringComparer.OrdinalIgnoreCase);

    public static bool ShouldIgnore(RawNotification n) =>
        n.IsOngoing
        || n.IsForegroundService
        || n.IsGroupSummary
        || (n.AndroidCategory is not null && s_categories.Contains(n.AndroidCategory))
        || n.Package.StartsWith("dev.alak.", StringComparison.Ordinal)
        || n.Package.StartsWith("com.alak.", StringComparison.Ordinal); // own package (D-01 namespace)
}

/// <summary>
/// Detector for communication apps (SMS, instant messaging, email).
/// Used by the setting to exclude private communications.
/// </summary>
public static class CommunicationAppDetector
{
    private static readonly HashSet<string> s_knownPackages = new(StringComparer.OrdinalIgnoreCase)
    {
        // SMS
        "com.google.android.apps.messaging",
        "com.samsung.android.messaging",
        "com.android.mms",
        "com.sonyericsson.conversations",
        "com.motorola.messaging",
        "com.simplemobiletools.sms.messenger",

        // Messengers / Chat
        "org.telegram.messenger",
        "org.telegram.messenger.web",
        "org.telegram.plus",
        "org.thunderdog.challegram",
        "com.whatsapp",
        "com.whatsapp.w4b",
        "org.thoughtcrime.securesms",
        "com.discord",
        "com.facebook.orca",
        "com.viber.voip",
        "com.skype.raider",
        "com.microsoft.teams",
        "com.slack",
        "ir.resaneh.bale",
        "ir.bale.messenger",
        "com.bale.messenger",
        "org.rubika.app",
        "ir.resello.rubika",
        "ir.eitaa.messenger",
        "ir.soroush.messenger",
        "ir.gap.im",

        // Email
        "com.google.android.gm",
        "com.microsoft.office.outlook",
        "com.google.android.apps.inbox",
        "com.yahoo.mobile.client.android.mail",
        "com.samsung.android.email.provider",
        "ch.protonmail.android",
        "com.fsck.k9",
        "net.thunderbird.android"
    };

    private static readonly HashSet<string> s_communicationCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "msg",
        "email",
        "social"
    };

    public static bool IsCommunicationApp(string? package)
    {
        if (string.IsNullOrWhiteSpace(package)) return false;
        if (s_knownPackages.Contains(package)) return true;

        var pkg = package.ToLowerInvariant();
        return pkg.Contains(".messaging") ||
               pkg.Contains(".sms") ||
               pkg.Contains(".im.") ||
               pkg.Contains(".messenger") ||
               pkg.Contains(".email") ||
               pkg.EndsWith(".mail");
    }

    public static bool IsCommunicationNotification(RawNotification n)
    {
        if (IsCommunicationApp(n.Package)) return true;
        if (n.AndroidCategory != null && s_communicationCategories.Contains(n.AndroidCategory)) return true;
        if (!string.IsNullOrWhiteSpace(n.ConversationTitle)) return true;
        return false;
    }
}
