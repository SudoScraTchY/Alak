namespace Alak.Core.Model;

/// <summary>
/// Cached display identity of an app (spec CAP-013): resolved label plus a small
/// PNG icon (≤ 96 px). Either field may be null when the platform cannot resolve it.
/// </summary>
public sealed record AppIdentity(string Package, string? Label, byte[]? IconPng);
