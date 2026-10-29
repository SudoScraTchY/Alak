namespace Alak.Core.Model;

/// <summary>
/// User-defined classification rule (spec §11.1, PER-001..004).
/// Evaluated before Tier 1; first match wins by priority asc, then id asc.
/// </summary>
public sealed record UserRule(
    long Id,
    int Priority,
    UserRuleMatch Match,
    UserRuleAction Action,
    string? Label = null,
    bool Enabled = true,
    long CreatedAt = 0,
    long UpdatedAt = 0)
{
    // UI-friendly flat properties (mapped to Match/Action in repo)
    public string? Package
    {
        get => Match.Package;
        init => Match = Match with { Package = value };
    }

    public string Pattern
    {
        get => Match.TextContains ?? "";
        init => Match = Match with { TextContains = value };
    }

    public Category Category
    {
        get => Action.SetCategory != null && Enum.TryParse<Category>(Action.SetCategory, out var c) ? c : Category.Other;
        init => Action = Action with { SetCategory = value.ToKebabId() };
    }

    public UserRuleActionType ActionType
    {
        get
        {
            if (Action.Mute) return UserRuleActionType.Mute;
            if (Action.ArchiveImmediately) return UserRuleActionType.Archive;
            if (Action.AddTags != null && Action.AddTags.Count > 0) return UserRuleActionType.Tag;
            return UserRuleActionType.Allow;
        }
        init
        {
            Action = value switch
            {
                UserRuleActionType.Mute => Action with { Mute = true },
                UserRuleActionType.Archive => Action with { ArchiveImmediately = true },
                UserRuleActionType.Tag => Action with { AddTags = new[] { "user-tag" } },
                _ => Action with { Mute = false, ArchiveImmediately = false }
            };
        }
    }

    public RuleMatchMode MatchMode
    {
        get => Match.MatchMode;
        init => Match = Match with { MatchMode = value };
    }

    public bool CaseSensitive
    {
        get => Match.CaseSensitive;
        init => Match = Match with { CaseSensitive = value };
    }
}

/// <summary>Match conditions — all must hold (AND).</summary>
public sealed record UserRuleMatch(
    string? Package = null,
    string? TitleContains = null,
    string? TextContains = null,
    string? SenderContains = null,
    string? ChannelId = null,
    string? AccountEmail = null,
    RuleMatchMode MatchMode = RuleMatchMode.Contains,
    bool CaseSensitive = false,
    string? TitleRegex = null,
    string? TextRegex = null,
    string? SenderRegex = null);

/// <summary>Action to take when match succeeds.</summary>
public sealed record UserRuleAction(
    string? SetCategory = null,
    IReadOnlyList<string>? AddTags = null,
    bool Mute = false,
    bool ArchiveImmediately = false,
    CaptureDepth? CaptureDepth = null);

/// <summary>UI-friendly action types for the Rules screen.</summary>
public enum UserRuleActionType
{
    Allow,
    Mute,
    Archive,
    Tag
}

/// <summary>UI-friendly match modes for the Rules screen.</summary>
public enum RuleMatchMode
{
    Contains,
    Exact,
    StartsWith,
    EndsWith,
    Regex
}

/// <summary>
/// Per-app settings (spec §11.3).
/// </summary>
public sealed record AppSettings(
    string Package,
    CaptureDepth? CaptureDepthOverride = null,
    bool Mute = false,
    bool AutoArchiveOnRead = false,
    string? DefaultCategoryOverride = null,
    int? RetentionOverrideDays = null,
    bool IgnorePromoCodeDetection = false,
    string? DisplayName = null);

/// <summary>Global app settings (spec §11.4).</summary>
public sealed record GlobalSettings(
    string Theme, // "system" | "light" | "dark"
    string UiLanguage, // "system" | "en" | "fa"
    string Calendar, // "auto" | "gregorian" | "jalali"
    string Digits, // "auto" | "latin" | "persian"
    Dictionary<string, int>? RetentionOverrides = null,
    int? GlobalSizeCap = null,
    bool Tier2Enabled = true,
    string SlmPerformanceProfile = "balanced", // "battery" | "balanced" | "quality"
    bool AppLockEnabled = false,
    bool HideAmountsInLists = false,
    bool NotificationRemindersEnabled = true,
    bool ExcludeCommunications = false);