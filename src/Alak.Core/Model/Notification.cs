namespace Alak.Core.Model;

/// <summary>
/// Core notification model for the app layer (spec §6.6/§13).
/// Maps to the storage NotificationInsert/NotificationListItem.
/// </summary>
public sealed record Notification(
    string Id,
    string Package,
    string ChannelName,
    string Title,
    string Text,
    string Category,
    DateTimeOffset Timestamp,
    bool IsRead,
    bool IsPersian,
    string? ExtractedCode = null)
{
    public bool HasCode => !string.IsNullOrEmpty(ExtractedCode);
}