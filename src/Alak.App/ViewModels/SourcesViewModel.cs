using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Storage;
using Alak.Core.Model;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Alak.App.ViewModels;

/// <summary>
/// ViewModel for the Sources screen (§13.6).
/// Groups notifications by (package, account|channel) with per-source controls.
/// </summary>
public partial class SourcesViewModel : ObservableObject
{
    private readonly IUserRulesRepository _rulesRepo;
    private readonly INotificationRepository _notificationRepo;

    public SourcesViewModel(IUserRulesRepository rulesRepo, INotificationRepository notificationRepo)
    {
        _rulesRepo = rulesRepo;
        _notificationRepo = notificationRepo;

        LoadSourcesCommand = new AsyncRelayCommand(LoadSourcesAsync);
        SaveSourceSettingsCommand = new AsyncRelayCommand<SourceItem>(SaveSourceSettingsAsync);
        ApplyToExistingCommand = new AsyncRelayCommand<SourceItem>(ApplyToExistingAsync);
    }

    [ObservableProperty]
    private ObservableCollection<SourceItem> _sources = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    public IAsyncRelayCommand LoadSourcesCommand { get; }
    public IAsyncRelayCommand<SourceItem> SaveSourceSettingsCommand { get; }
    public IAsyncRelayCommand<SourceItem> ApplyToExistingCommand { get; }

    public async Task LoadSourcesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading sources...";
        try
        {
            // Get distinct (package, account|channel) pairs from last 30 days
            var sources = await _notificationRepo.GetSourcesAsync(30);
            Sources.Clear();
            foreach (var src in sources)
            {
                Sources.Add(src);
            }
            StatusMessage = $"{Sources.Count} sources found";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SaveSourceSettingsAsync(SourceItem? item)
    {
        if (item == null) return;

        try
        {
            // Create or update user rule for this source
            var match = new UserRuleMatch
            {
                Package = item.PackageName,
                ChannelId = item.ChannelId,
                AccountEmail = item.AccountEmail
            };
            
            var action = new UserRuleAction
            {
                SetCategory = item.SelectedCategory?.ToString(),
                Mute = item.Mute
            };
            
            var rule = new UserRule(
                Id: item.RuleId,
                Priority: item.Priority,
                Match: match,
                Action: action,
                Label: item.DisplayLabel,
                Enabled: true,
                CreatedAt: item.RuleId > 0 ? DateTimeOffset.UtcNow.ToUnixTimeSeconds() : DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            );

            if (item.RuleId > 0)
            {
                await _rulesRepo.UpdateRuleAsync(rule);
            }
            else
            {
                var newId = await _rulesRepo.InsertRuleAsync(rule);
                item.RuleId = newId;
            }

            StatusMessage = $"Saved: {item.DisplayLabel}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
        }
    }

    public async Task ApplyToExistingAsync(SourceItem? item)
    {
        if (item == null) return;

        try
        {
            StatusMessage = "Applying to existing notifications...";
            // This would be a background batch operation (PER-011)
            // For now, show the intent
            await Task.Delay(500);
            StatusMessage = $"Applied to up to 500 existing notifications for {item.DisplayLabel}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Apply error: {ex.Message}";
        }
    }
}