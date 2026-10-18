using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Alak.App.Design;
using Alak.Core;
using Alak.Core.Model;
using Alak.Adaptive;
using Alak.Rules;
using Alak.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Alak.App.ViewModels;

public partial class InboxViewModel : ObservableObject
{
    private readonly INotificationRepository _repository;
    private readonly IRuleCompiler _ruleCompiler;
    private readonly IPackLoader _packLoader;
    private readonly IAdaptiveEngine _adaptiveEngine;
    private readonly Alak.App.Services.INotificationIngestionService _ingestionService;

    public ObservableCollection<NotificationViewModel> Notifications { get; } = new();
    public ObservableCollection<CategoryFilter> CategoryFilters { get; } = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private CategoryFilter? _selectedCategoryFilter;

    [ObservableProperty]
    private int _unreadCount;

    public ICommand RefreshCommand { get; }
    public ICommand LoadMoreCommand { get; }
    public ICommand OpenDetailCommand { get; }
    public ICommand OpenStubDetailCommand { get; }
    public ICommand CopyCodeCommand { get; }
    public ICommand DismissCommand { get; }
    public ICommand CategoryFilterChangedCommand { get; }

    public InboxViewModel(
        INotificationRepository repository,
        IRuleCompiler ruleCompiler,
        IPackLoader packLoader,
        IAdaptiveEngine adaptiveEngine,
        Alak.App.Services.INotificationIngestionService ingestionService)
    {
        _repository = repository;
        _ruleCompiler = ruleCompiler;
        _packLoader = packLoader;
        _adaptiveEngine = adaptiveEngine;
        _ingestionService = ingestionService;
        _ingestionService.NotificationIngested += OnNotificationIngested;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
        OpenDetailCommand = new AsyncRelayCommand<NotificationViewModel>(OpenDetailAsync);
        OpenStubDetailCommand = new AsyncRelayCommand<NotificationViewModel>(OpenStubDetailAsync);
        CopyCodeCommand = new AsyncRelayCommand<NotificationViewModel>(CopyCodeAsync);
        DismissCommand = new AsyncRelayCommand<NotificationViewModel>(DismissAsync);
        CategoryFilterChangedCommand = new AsyncRelayCommand(OnCategoryFilterChangedAsync);

        InitializeCategoryFilters();
    }

    private void OnNotificationIngested(Notification notification)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await RefreshAsync();
        });
    }

    private void InitializeCategoryFilters()
    {
        CategoryFilters.Add(new CategoryFilter { Key = "all", Label = "All", Count = 0 });
        CategoryFilters.Add(new CategoryFilter { Key = "promo", Label = "Promo", Count = 0, MarkColor = DesignTokens.CategoryMark.Promo });
        CategoryFilters.Add(new CategoryFilter { Key = "work", Label = "Work", Count = 0, MarkColor = DesignTokens.CategoryMark.Work });
        CategoryFilters.Add(new CategoryFilter { Key = "people", Label = "People", Count = 0, MarkColor = DesignTokens.CategoryMark.People });
        CategoryFilters.Add(new CategoryFilter { Key = "money", Label = "Money", Count = 0, MarkColor = DesignTokens.CategoryMark.Money });
        CategoryFilters.Add(new CategoryFilter { Key = "orders", Label = "Orders", Count = 0, MarkColor = DesignTokens.CategoryMark.Orders });
        CategoryFilters.Add(new CategoryFilter { Key = "security", Label = "Security", Count = 0, MarkColor = DesignTokens.CategoryMark.Security });
        CategoryFilters.Add(new CategoryFilter { Key = "social", Label = "Social", Count = 0, MarkColor = DesignTokens.CategoryMark.Social });
        CategoryFilters.Add(new CategoryFilter { Key = "updates", Label = "Updates", Count = 0, MarkColor = DesignTokens.CategoryMark.Updates });
        CategoryFilters.Add(new CategoryFilter { Key = "other", Label = "Other", Count = 0, MarkColor = DesignTokens.CategoryMark.Other });

        SelectedCategoryFilter = CategoryFilters[0];
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        IsRefreshing = true;

        try
        {
            var notifications = await _repository.GetAllAsync();
            await ApplyFiltersAndSort(notifications);
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
        }
    }

    private async Task LoadMoreAsync()
    {
        // Pagination not needed for MVP - all local
        await Task.CompletedTask;
    }

    private async Task ApplyFiltersAndSort(IEnumerable<Notification> notifications)
    {
        var filtered = notifications.AsEnumerable();

        // Category filter
        var selectedFilter = SelectedCategoryFilter;
        if (selectedFilter?.Key is { } key && key != "all")
        {
            filtered = filtered.Where(n => n.Category == key);
        }

        // Search query
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            filtered = filtered.Where(n => 
                n.Title.ToLowerInvariant().Contains(query) ||
                n.Text.ToLowerInvariant().Contains(query) ||
                n.ChannelName.ToLowerInvariant().Contains(query));
        }

        // Sort: unread first, then by timestamp desc
        var sorted = filtered
            .OrderByDescending(n => n.IsRead ? 0 : 1)
            .ThenByDescending(n => n.Timestamp)
            .ToList();

        Notifications.Clear();
        foreach (var n in sorted)
        {
            Notifications.Add(new NotificationViewModel(n));
        }

        UnreadCount = Notifications.Count(n => !n.IsRead);
        
        // Update category counts
        foreach (var cat in CategoryFilters.Where(c => c.Key != "all"))
        {
            cat.Count = notifications.Count(n => n.Category == cat.Key);
        }
        CategoryFilters[0].Count = notifications.Count();

        OnPropertyChanged(nameof(Notifications));
    }

    private async Task OnCategoryFilterChangedAsync()
    {
        var all = await _repository.GetAllAsync();
        await ApplyFiltersAndSort(all);
    }

    private async Task OpenDetailAsync(NotificationViewModel? vm)
    {
        if (vm == null) return;
        await Shell.Current.GoToAsync($"detail?id={vm.Id}");
    }

    private async Task OpenStubDetailAsync(NotificationViewModel? vm)
    {
        if (vm == null) return;
        await Shell.Current.GoToAsync($"stub-detail?id={vm.Id}");
    }

    private async Task CopyCodeAsync(NotificationViewModel? vm)
    {
        if (vm?.HasCode == true && !string.IsNullOrEmpty(vm.ExtractedCode))
        {
            await Clipboard.SetTextAsync(vm.ExtractedCode);
            // Toast handled by view
        }
    }

    private async Task DismissAsync(NotificationViewModel? vm)
    {
        if (vm == null) return;
        await _repository.MarkReadAsync(vm.Id);
        vm.IsRead = true;
        UnreadCount = Notifications.Count(n => !n.IsRead);
    }
}

public class NotificationViewModel : INotifyPropertyChanged
{
    private bool _isRead;
    private string _extractedCode = string.Empty;
    private bool _hasCode;

    public string Id { get; }
    public string Package { get; }
    public string ChannelName { get; }
    public string Title { get; }
    public string Text { get; }
    public string Category { get; }
    public DateTimeOffset Timestamp { get; }
    public string CategoryMarkColor { get; }
    public string CategoryTintColor { get; }
    public bool IsPersian { get; }

    public bool IsRead
    {
        get => _isRead;
        set { _isRead = value; OnPropertyChanged(); }
    }

    public string ExtractedCode
    {
        get => _extractedCode;
        set { _extractedCode = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasCode)); }
    }

    public bool HasCode
    {
        get => _hasCode;
        set { _hasCode = value; OnPropertyChanged(); }
    }

    public NotificationViewModel(Notification notification)
    {
        Id = notification.Id;
        Package = notification.Package;
        ChannelName = notification.ChannelName;
        Title = notification.Title;
        Text = notification.Text;
        Category = notification.Category;
        Timestamp = notification.Timestamp;
        IsRead = notification.IsRead;
        IsPersian = notification.IsPersian;

        CategoryMarkColor = GetMarkColor(notification.Category);
        CategoryTintColor = GetTintColor(notification.Category);

        if (notification.HasCode)
        {
            ExtractedCode = notification.ExtractedCode ?? string.Empty;
            HasCode = true;
        }
    }

    private static string GetMarkColor(string category) => category switch
    {
        // SPEC §12.2 category marks (light). Applied to spines/dots/chips ≥ 3px, never small body text.
        "money" => "#0C8397",
        "security" => "#D0325D",
        "people" => "#12895B",
        "work" => "#2F5BE0",
        "orders" => "#C4581A",
        "promo" or "offers" => "#8248D6",
        "updates" => "#5F6F76",
        "social" => "#6B8E00",
        _ => "#6B7280"
    };

    private static string GetTintColor(string category) => category switch
    {
        // Alpha washes of the mark (~14%) instead of opaque pastels: read correctly
        // on BOTH light (Salt/Paper) and dark (Night/Raised) surfaces.
        "money" => "#240C8397",
        "security" => "#24D0325D",
        "people" => "#2412895B",
        "work" => "#242F5BE0",
        "orders" => "#24C4581A",
        "promo" or "offers" => "#248248D6",
        "updates" => "#245F6F76",
        "social" => "#246B8E00",
        _ => "#246B7280"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class CategoryFilter : INotifyPropertyChanged
{
    private int _count;

    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string MarkColor { get; set; } = string.Empty;

    public int Count
    {
        get => _count;
        set { _count = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}