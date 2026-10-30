using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core;
using Alak.Core.Linking;
using Alak.Core.Model;
using Alak.Core.Classification;
using Alak.Core.Capture;
using Alak.Rules;
using Alak.Storage;
using Alak.Adaptive;
using Alak.App.Services;
using Alak.Inference.Abstractions.Tasks;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Alak.App.ViewModels;

public sealed class CategoryOption
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string MarkColor { get; init; } = string.Empty;
    public string TintColor { get; init; } = string.Empty;
    public bool IsSelected { get; set; }
}

public partial class DetailViewModel : ObservableObject
{
    private readonly INotificationRepository _repository;
    private readonly IAdaptiveEngine _adaptiveEngine;
    private readonly IModelService _modelService;
    private readonly IPackLoader? _packLoader;
    private readonly IRuleCompiler? _ruleCompiler;
    private PackSet? _cachedPackSet;
    private CancellationTokenSource? _aiCts;

    [ObservableProperty]
    private NotificationViewModel? _notification;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _sourceLabel = string.Empty;

    [ObservableProperty]
    private ImageSource? _sourceIcon;

    [ObservableProperty]
    private bool _hasSourceIcon;

    [ObservableProperty]
    private bool _isViaSms;

    // Category Changing (Tier 2)
    [ObservableProperty]
    private bool _isCategoryPickerOpen;

    [ObservableProperty]
    private string _categoryUpdateMessage = string.Empty;

    public ObservableCollection<CategoryOption> AvailableCategories { get; } = new();

    // On-Device SLM Explanation (Tier 3 T3-01)
    [ObservableProperty]
    private bool _isAiSummaryLoading;

    [ObservableProperty]
    private bool _hasAiSummary;

    [ObservableProperty]
    private string _aiSummaryText = string.Empty;

    [ObservableProperty]
    private string _aiImportance = string.Empty;

    [ObservableProperty]
    private string? _aiAction;

    [ObservableProperty]
    private string? _aiErrorMessage;

    public ObservableCollection<string> AiTags { get; } = new();
    public ObservableCollection<T3Fact> AiFacts { get; } = new();

    public ICommand BackCommand { get; }
    public ICommand ViewInWalletCommand { get; }
    public ICommand MarkReadCommand { get; }
    public ICommand CopyCodeCommand { get; }
    public ICommand ShareCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenCategoryPickerCommand { get; }
    public ICommand CloseCategoryPickerCommand { get; }
    public ICommand ChangeCategoryCommand { get; }
    public ICommand ExplainWithAiCommand { get; }
    public ICommand CancelAiSummaryCommand { get; }

    public DetailViewModel(
        INotificationRepository repository,
        IAdaptiveEngine adaptiveEngine,
        IModelService modelService,
        IPackLoader? packLoader = null,
        IRuleCompiler? ruleCompiler = null)
    {
        _repository = repository;
        _adaptiveEngine = adaptiveEngine;
        _modelService = modelService;
        _packLoader = packLoader;
        _ruleCompiler = ruleCompiler;

        BackCommand = new AsyncRelayCommand(async () => await Shell.Current.GoToAsync(".."));
        ViewInWalletCommand = new AsyncRelayCommand(async () => await Shell.Current.GoToAsync("//codes"));
        MarkReadCommand = new AsyncRelayCommand(MarkReadAsync);
        CopyCodeCommand = new AsyncRelayCommand(CopyCodeAsync);
        ShareCommand = new AsyncRelayCommand(ShareAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);

        OpenCategoryPickerCommand = new RelayCommand(() =>
        {
            PopulateAvailableCategories();
            IsCategoryPickerOpen = true;
        });
        CloseCategoryPickerCommand = new RelayCommand(() => IsCategoryPickerOpen = false);
        ChangeCategoryCommand = new AsyncRelayCommand<string>(ChangeCategoryAsync);

        ExplainWithAiCommand = new AsyncRelayCommand(ExplainWithAiAsync);
        CancelAiSummaryCommand = new RelayCommand(CancelAiSummary);
    }

    public async Task LoadAsync(string id)
    {
        IsLoading = true;
        try
        {
            var notification = await _repository.GetByIdAsync(id);
            if (notification != null)
            {
                Notification = new NotificationViewModel(notification);
                await PopulateSourceInfoAsync(notification);
                PopulateAvailableCategories();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PopulateSourceInfoAsync(Notification notification)
    {
        IsViaSms = notification.IsAttributed;
        var package = notification.SourcePackage;
        SourceLabel = PackageNameHumanizer.Humanize(package);
        SourceIcon = null;
        HasSourceIcon = false;

        try
        {
            var infos = await _repository.GetAppInfosAsync(new[] { package });
            if (!infos.TryGetValue(package, out var info)) return;

            if (!string.IsNullOrWhiteSpace(info.Label)) SourceLabel = info.Label!;
            if (info.IconPng is { Length: > 0 })
            {
                var bytes = info.IconPng;
                SourceIcon = ImageSource.FromStream(() => new MemoryStream(bytes));
                HasSourceIcon = true;
            }
        }
        catch
        {
            // Identity is cosmetic; the notification still renders with the humanized name.
        }
    }

    private void PopulateAvailableCategories()
    {
        var loc = LocalizationManager.Instance;
        var current = Notification?.Category?.ToLowerInvariant() ?? "";

        AvailableCategories.Clear();
        var keys = new[]
        {
            ("promo", loc["Cat_Promo"]),
            ("work", loc["Cat_Work"]),
            ("people", loc["Cat_People"]),
            ("money", loc["Cat_Money"]),
            ("orders", loc["Cat_Orders"]),
            ("security", loc["Cat_Security"]),
            ("social", loc["Cat_Social"]),
            ("updates", loc["Cat_Updates"]),
            ("other", loc["Cat_Other"])
        };

        foreach (var (k, label) in keys)
        {
            AvailableCategories.Add(new CategoryOption
            {
                Key = k,
                Label = label,
                MarkColor = NotificationViewModel.GetMarkColor(k),
                TintColor = NotificationViewModel.GetTintColor(k),
                IsSelected = current.Equals(k, StringComparison.OrdinalIgnoreCase) || (k == "promo" && current == "offers")
            });
        }
    }

    private async ValueTask<PackSet> GetPackSetAsync()
    {
        if (_cachedPackSet != null) return _cachedPackSet;
        if (_packLoader != null && _ruleCompiler != null)
        {
            try
            {
                var packs = await _packLoader.LoadAsync();
                _cachedPackSet = await _ruleCompiler.CompileAsync(packs);
                return _cachedPackSet;
            }
            catch { }
        }
        return PackSet.BuiltInDefault;
    }

    private async Task ChangeCategoryAsync(string? targetCategoryKey)
    {
        if (string.IsNullOrEmpty(targetCategoryKey) || Notification == null) return;
        if (Notification.Category.Equals(targetCategoryKey, StringComparison.OrdinalIgnoreCase))
        {
            IsCategoryPickerOpen = false;
            return;
        }

        // 1. Update SQLite
        await _repository.UpdateCategoryAsync(Notification.Id, targetCategoryKey);

        // 2. Train AdaptiveEngine (online update x8 per ADP-004)
        if (Enum.TryParse<Category>(targetCategoryKey, true, out var catEnum) ||
            (targetCategoryKey == "promo" && Enum.TryParse<Category>("Offers", true, out catEnum)))
        {
            try
            {
                var raw = RawNotification.Create(
                    package: Notification.Package,
                    channelId: Notification.ChannelName,
                    channelName: Notification.ChannelName,
                    title: Notification.Title,
                    text: Notification.Text,
                    postedAtUtcMs: Notification.Timestamp.ToUnixTimeMilliseconds());

                var packSet = await GetPackSetAsync();
                var classification = RulesEngine.Classify(raw, packSet);
                var hits = classification.ConceptHits.ToArray();
                var entities = classification.Entities.ToArray();
                var text = Notification.Title + " " + Notification.Text;

                await _adaptiveEngine.LearnFromCorrectionAsync(
                    Notification.Package,
                    Notification.ChannelName,
                    text,
                    hits,
                    entities,
                    catEnum);
                await _adaptiveEngine.SaveAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DetailViewModel] AdaptiveEngine train error: {ex.Message}");
            }
        }

        // 3. Update reactive properties on NotificationViewModel
        Notification.UpdateCategory(targetCategoryKey);
        PopulateAvailableCategories();
        IsCategoryPickerOpen = false;
        CategoryUpdateMessage = LocalizationManager.Instance["Detail_CategoryUpdated"];
    }

    private async Task ExplainWithAiAsync()
    {
        if (Notification == null) return;

        // Check if model is downloaded
        if (!_modelService.IsModelDownloaded)
        {
            var loc = LocalizationManager.Instance;
            bool goToSettings = await Shell.Current.DisplayAlert(
                loc["Detail_NoModelTitle"],
                loc["Detail_NoModelDesc"],
                loc["Detail_GoToSettings"],
                loc["Common_Cancel"]
            );

            if (goToSettings)
            {
                await Shell.Current.GoToAsync("//settings");
            }
            return;
        }

        IsAiSummaryLoading = true;
        AiErrorMessage = null;
        _aiCts?.Cancel();
        _aiCts = new CancellationTokenSource();

        try
        {
            var result = await _modelService.ExplainNotificationAsync(
                Notification.Title,
                Notification.Text,
                Notification.Package,
                Notification.Category,
                _aiCts.Token);

            if (result != null)
            {
                AiSummaryText = result.Summary;
                AiImportance = result.Importance;
                AiAction = result.Action;
                AiTags.Clear();
                foreach (var t in result.Tags) AiTags.Add(t);
                AiFacts.Clear();
                foreach (var f in result.Facts) AiFacts.Add(f);
                HasAiSummary = true;
            }
            else
            {
                AiErrorMessage = LocalizationManager.Instance["Detail_AiFailed"];
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            AiErrorMessage = ex.Message;
        }
        finally
        {
            IsAiSummaryLoading = false;
        }
    }

    private void CancelAiSummary()
    {
        _aiCts?.Cancel();
        IsAiSummaryLoading = false;
    }

    private async Task MarkReadAsync()
    {
        if (Notification == null) return;
        await _repository.MarkReadAsync(Notification.Id);
        Notification.IsRead = true;
    }

    private async Task CopyCodeAsync()
    {
        if (Notification?.HasCode == true)
        {
            await Clipboard.SetTextAsync(Notification.ExtractedCode);
        }
    }

    private async Task ShareAsync()
    {
        if (Notification == null) return;
        await Share.RequestAsync(new ShareTextRequest
        {
            Text = $"{Notification.Title}\n\n{Notification.Text}",
            Title = "Share Notification"
        });
    }

    private async Task DeleteAsync()
    {
        if (Notification == null) return;
        await _repository.DeleteAsync(Notification.Id);
        await Shell.Current.GoToAsync("..");
    }
}
