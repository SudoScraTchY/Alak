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
        || n.Package.StartsWith("dev.alak.", StringComparison.Ordinal); // own package (D-01 namespace)
}
