using Alak.Core.Model;
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Alak.Core.Model;

/// <summary>
/// Represents a single notification source (package + account|channel) with its settings.
/// Used by the Sources screen (§13.6, PER-010).
/// </summary>
public partial class SourceItem : ObservableObject
{
    public string PackageName { get; set; } = "";
    public string? AccountEmail { get; set; }
    public string? ChannelId { get; set; }
    public string DisplayLabel { get; set; } = "";
    public int Count { get; set; }
    public Category? CurrentDefaultCategory { get; set; }
    public CaptureDepth? CurrentCaptureDepth { get; set; }
    public bool Mute { get; set; }
    public bool AutoArchive { get; set; }
    public int Priority { get; set; } = 100;
    public long RuleId { get; set; }

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private CaptureDepth? _selectedCaptureDepth;

    partial void OnSelectedCategoryChanged(Category? value)
    {
        CurrentDefaultCategory = value;
    }

    partial void OnSelectedCaptureDepthChanged(CaptureDepth? value)
    {
        CurrentCaptureDepth = value;
    }

    public string DisplayName => string.IsNullOrEmpty(DisplayLabel) 
        ? PackageName 
        : $"{DisplayLabel} ({PackageName})";

    public string SourceKey => $"{PackageName}|{AccountEmail ?? ChannelId ?? "default"}";
}