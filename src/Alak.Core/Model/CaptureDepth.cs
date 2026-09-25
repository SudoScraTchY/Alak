namespace Alak.Core.Model;

/// <summary>How much of a notification is stored (spec §6.4). Decided BEFORE persistence.</summary>
public enum CaptureDepth
{
    /// <summary>Title, text, big text, sub text, lines, action labels, entities.</summary>
    Full,
    /// <summary>App, title, time, category; body discarded. Default for chats.</summary>
    Headline,
    /// <summary>App, time, category only. Default for Security.</summary>
    Metadata,
    /// <summary>Nothing stored. User-excluded apps.</summary>
    Ignore,
}

/// <summary>Why a stored notification left the Inbox (spec CAP-012).</summary>
public enum RemovalReason
{
    /// <summary>The user clicked through (opened the source app).</summary>
    Click,
    /// <summary>The user dismissed it in the source app/shade.</summary>
    UserDismissed,
    /// <summary>The source app cancelled it.</summary>
    AppCancel,
    /// <summary>Retention timeout.</summary>
    Timeout,
    /// <summary>Anything else.</summary>
    Unknown,
}
