using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Alak.App.Design;
using Alak.Core;
using Alak.Core.Linking;
using Alak.Core.Model;
using Alak.Adaptive;
using Alak.Rules;
using Alak.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace Alak.App.ViewModels;

public partial class InboxViewModel : ObservableObject
{
    private readonly INotificationRepository _repository;
    private readonly IRuleCompiler _ruleCompiler;
    private readonly IPackLoader _packLoader;
    private readonly IAdaptiveEngine _adaptiveEngine;
    private readonly Alak.App.Services.INotificationIngestionService _ingestionService;
    private readonly Alak.App.Services.IModelService _modelService;
    private List<Notification> _cachedNotifications = new();
    private bool _isInitialized;

    public ObservableCollection<NotificationGroup> GroupedNotifications { get; } = new();
    public ObservableRangeCollection<NotificationViewModel> Notifications { get; } = new();
    public ObservableCollection<CategoryFilter> CategoryFilters { get; } = new();

    private const int PageSize = 25;
    private long? _oldestTimestampMs;
    private bool _hasMore = true;
    private bool _isLoadingMore;
    private System.Threading.Timer? _searchDebounceTimer;
    private readonly Dictionary<string, ImageSource?> _sourceIcons = new(StringComparer.Ordinal);

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
    public ICommand OpenDailyDigestCommand { get; }
    public ICommand CloseDailyDigestCommand { get; }

    public InboxViewModel(
        INotificationRepository repository,
        IRuleCompiler ruleCompiler,
        IPackLoader packLoader,
        IAdaptiveEngine adaptiveEngine,
        Alak.App.Services.INotificationIngestionService ingestionService,
        Alak.App.Services.IModelService modelService)
    {
        _repository = repository;
        _ruleCompiler = ruleCompiler;
        _packLoader = packLoader;
        _adaptiveEngine = adaptiveEngine;
        _ingestionService = ingestionService;
        _modelService = modelService;
        _ingestionService.NotificationIngested += OnNotificationIngested;

        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
        OpenDetailCommand = new AsyncRelayCommand<NotificationViewModel>(OpenDetailAsync);
        OpenStubDetailCommand = new AsyncRelayCommand<NotificationViewModel>(OpenStubDetailAsync);
        CopyCodeCommand = new AsyncRelayCommand<NotificationViewModel>(CopyCodeAsync);
        DismissCommand = new AsyncRelayCommand<NotificationViewModel>(DismissAsync);
        CategoryFilterChangedCommand = new RelayCommand(OnCategoryFilterChanged);
        OpenDailyDigestCommand = new AsyncRelayCommand(OpenDailyDigestAsync);
        CloseDailyDigestCommand = new RelayCommand(() => { });

        InitializeCategoryFilters();
        Services.LocalizationManager.Instance.PropertyChanged += (_, _) =>
        {
            var selectedKey = SelectedCategoryFilter?.Key ?? "all";
            InitializeCategoryFilters();
            SelectedCategoryFilter = CategoryFilters.FirstOrDefault(c => c.Key == selectedKey) ?? CategoryFilters[0];
            SelectedCategoryFilter.IsSelected = true;

            foreach (var group in GroupedNotifications)
            {
                group.Header = FormatDateHeader(group.Date);
            }
        };
    }

    partial void OnSearchQueryChanged(string value)
    {
        _searchDebounceTimer?.Dispose();
        _searchDebounceTimer = new System.Threading.Timer(async _ =>
        {
            await MainThread.InvokeOnMainThreadAsync(RefreshAsync);
        }, null, 250, System.Threading.Timeout.Infinite);
    }

    partial void OnSelectedCategoryFilterChanged(CategoryFilter? oldValue, CategoryFilter? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
        _ = RefreshAsync();
    }

    private System.Threading.Timer? _debounceTimer;
    private readonly System.Threading.SemaphoreSlim _refreshSemaphore = new(1, 1);
    private volatile bool _refreshRequested;

    private void OnNotificationIngested(Notification notification)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // 1. Update in-memory cached notifications list and decrement old counts if existing
            var existingIndex = _cachedNotifications.FindIndex(n => n.Id == notification.Id);
            if (existingIndex >= 0)
            {
                var existing = _cachedNotifications[existingIndex];
                if (!existing.IsRead)
                {
                    UnreadCount = Math.Max(0, UnreadCount - 1);
                }
                foreach (var cat in CategoryFilters.Where(c => c.Key != "all"))
                {
                    if (MatchesCategory(existing.Category, cat.Key))
                    {
                        cat.Count = Math.Max(0, cat.Count - 1);
                    }
                }
                _cachedNotifications.RemoveAt(existingIndex);
            }

            _cachedNotifications.Insert(0, notification);

            // 2. Update category filter counts & unread count
            if (!notification.IsRead)
            {
                UnreadCount++;
            }
            foreach (var cat in CategoryFilters.Where(c => c.Key != "all"))
            {
                if (MatchesCategory(notification.Category, cat.Key))
                {
                    cat.Count++;
                }
            }
            if (CategoryFilters.Count > 0)
            {
                CategoryFilters[0].Count = _cachedNotifications.Count;
            }

            // 3. If it matches current category filter and search query, insert into collections
            var selectedFilter = SelectedCategoryFilter;
            bool matchesFilter = selectedFilter?.Key is not { } key || key == "all" || MatchesCategory(notification.Category, key);
            bool matchesSearch = string.IsNullOrWhiteSpace(SearchQuery) ||
                                 notification.Title.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                 notification.Text.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                 notification.ChannelName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase);

            if (matchesFilter && matchesSearch)
            {
                // Remove existing item if re-ingested
                foreach (var group in GroupedNotifications.ToList())
                {
                    var existingVm = group.FirstOrDefault(x => x.Id == notification.Id);
                    if (existingVm != null)
                    {
                        group.Remove(existingVm);
                        if (group.Count == 0)
                        {
                            GroupedNotifications.Remove(group);
                        }
                        break;
                    }
                }
                for (int i = 0; i < Notifications.Count; i++)
                {
                    if (Notifications[i].Id == notification.Id)
                    {
                        Notifications.RemoveAt(i);
                        break;
                    }
                }

                var vm = CreateNotificationViewModel(notification);
                var notifDate = notification.Timestamp.ToLocalTime().Date;

                if (!notification.IsRead)
                {
                    Notifications.Insert(0, vm);
                }
                else
                {
                    int insertIdx = 0;
                    while (insertIdx < Notifications.Count && !Notifications[insertIdx].IsRead)
                    {
                        insertIdx++;
                    }
                    Notifications.Insert(insertIdx, vm);
                }

                var targetGroup = GroupedNotifications.FirstOrDefault(g => g.Date == notifDate);
                if (targetGroup != null)
                {
                    if (!notification.IsRead)
                    {
                        targetGroup.Insert(0, vm);
                    }
                    else
                    {
                        int insertIdx = 0;
                        while (insertIdx < targetGroup.Count && !targetGroup[insertIdx].IsRead)
                        {
                            insertIdx++;
                        }
                        targetGroup.Insert(insertIdx, vm);
                    }
                }
                else
                {
                    var newGroup = new NotificationGroup(FormatDateHeader(notifDate), notifDate, new[] { vm });
                    int insertGroupIdx = 0;
                    while (insertGroupIdx < GroupedNotifications.Count && GroupedNotifications[insertGroupIdx].Date > notifDate)
                    {
                        insertGroupIdx++;
                    }
                    GroupedNotifications.Insert(insertGroupIdx, newGroup);
                }

                await PopulateSourceInfoAsync(new[] { vm });
            }
        });
    }

    private void InitializeCategoryFilters()
    {
        var loc = Services.LocalizationManager.Instance;
        CategoryFilters.Clear();
        CategoryFilters.Add(new CategoryFilter { Key = "all", Label = loc["Cat_All"], Count = 0 });
        CategoryFilters.Add(new CategoryFilter { Key = "promo", Label = loc["Cat_Promo"], Count = 0, MarkColor = DesignTokens.CategoryMark.Promo });
        CategoryFilters.Add(new CategoryFilter { Key = "work", Label = loc["Cat_Work"], Count = 0, MarkColor = DesignTokens.CategoryMark.Work });
        CategoryFilters.Add(new CategoryFilter { Key = "people", Label = loc["Cat_People"], Count = 0, MarkColor = DesignTokens.CategoryMark.People });
        CategoryFilters.Add(new CategoryFilter { Key = "money", Label = loc["Cat_Money"], Count = 0, MarkColor = DesignTokens.CategoryMark.Money });
        CategoryFilters.Add(new CategoryFilter { Key = "orders", Label = loc["Cat_Orders"], Count = 0, MarkColor = DesignTokens.CategoryMark.Orders });
        CategoryFilters.Add(new CategoryFilter { Key = "security", Label = loc["Cat_Security"], Count = 0, MarkColor = DesignTokens.CategoryMark.Security });
        CategoryFilters.Add(new CategoryFilter { Key = "social", Label = loc["Cat_Social"], Count = 0, MarkColor = DesignTokens.CategoryMark.Social });
        CategoryFilters.Add(new CategoryFilter { Key = "updates", Label = loc["Cat_Updates"], Count = 0, MarkColor = DesignTokens.CategoryMark.Updates });
        CategoryFilters.Add(new CategoryFilter { Key = "other", Label = loc["Cat_Other"], Count = 0, MarkColor = DesignTokens.CategoryMark.Other });

        SelectedCategoryFilter = CategoryFilters[0];
        SelectedCategoryFilter.IsSelected = true;
    }

    public async Task InitializeAsync(bool force = false)
    {
        if (_isInitialized && !force) return;
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _refreshRequested = true;
        if (!await _refreshSemaphore.WaitAsync(0))
        {
            return;
        }

        try
        {
            while (_refreshRequested)
            {
                _refreshRequested = false;
                IsLoading = true;

                // Sync category counts and total unread from repository
                var all = (await _repository.GetAllAsync()).ToList();
                _cachedNotifications = all;
                UnreadCount = _cachedNotifications.Count(n => !n.IsRead);
                foreach (var cat in CategoryFilters.Where(c => c.Key != "all"))
                {
                    cat.Count = _cachedNotifications.Count(n => MatchesCategory(n.Category, cat.Key));
                }
                if (CategoryFilters.Count > 0)
                {
                    CategoryFilters[0].Count = _cachedNotifications.Count;
                }

                _oldestTimestampMs = null;
                _hasMore = true;
                string? filterCategory = SelectedCategoryFilter?.Key is { } k && k != "all" ? k : null;

                IReadOnlyList<Notification> loaded;
                if (!string.IsNullOrWhiteSpace(SearchQuery))
                {
                    var ftsResults = await _repository.SearchFtsAsync(SearchQuery, 50);
                    if (!string.IsNullOrWhiteSpace(filterCategory))
                    {
                        ftsResults = ftsResults.Where(n => MatchesCategory(n.Category, filterCategory)).ToList();
                    }
                    loaded = ftsResults;
                    _hasMore = false; // Search returns all matches up to limit
                }
                else
                {
                    loaded = await _repository.GetPageAsync(null, PageSize, filterCategory);
                    if (loaded.Count < PageSize)
                    {
                        _hasMore = false;
                    }
                    if (loaded.Count > 0)
                    {
                        _oldestTimestampMs = loaded.Min(n => n.Timestamp.ToUnixTimeMilliseconds());
                    }
                }

                RebuildGroupedNotifications(loaded);
                await PopulateSourceInfoAsync(Notifications.ToList());
                _isInitialized = true;
            }
        }
        finally
        {
            IsLoading = false;
            IsRefreshing = false;
            _refreshSemaphore.Release();
        }
    }

    private async Task LoadMoreAsync()
    {
        if (!_hasMore || _isLoadingMore || !string.IsNullOrWhiteSpace(SearchQuery))
            return;

        _isLoadingMore = true;
        try
        {
            string? filterCategory = SelectedCategoryFilter?.Key is { } k && k != "all" ? k : null;
            var moreItems = await _repository.GetPageAsync(_oldestTimestampMs, PageSize, filterCategory);
            if (moreItems.Count == 0)
            {
                _hasMore = false;
                return;
            }

            if (moreItems.Count < PageSize)
            {
                _hasMore = false;
            }

            _oldestTimestampMs = Math.Min(
                _oldestTimestampMs ?? long.MaxValue,
                moreItems.Min(n => n.Timestamp.ToUnixTimeMilliseconds()));

            AppendNotifications(moreItems);
            await PopulateSourceInfoAsync(Notifications.ToList());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InboxViewModel] LoadMoreAsync error: {ex.Message}");
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    private void RebuildGroupedNotifications(IReadOnlyList<Notification> items)
    {
        GroupedNotifications.Clear();
        var vms = items.Select(CreateNotificationViewModel).ToList();
        Notifications.ReplaceRange(vms);

        var groups = vms
            .GroupBy(vm => vm.Timestamp.ToLocalTime().Date)
            .OrderByDescending(g => g.Key);

        foreach (var g in groups)
        {
            var header = FormatDateHeader(g.Key);
            GroupedNotifications.Add(new NotificationGroup(header, g.Key, g));
        }
    }

    private void AppendNotifications(IReadOnlyList<Notification> items)
    {
        var newVms = items.Select(CreateNotificationViewModel).ToList();
        Notifications.AddRange(newVms);

        var groups = newVms
            .GroupBy(vm => vm.Timestamp.ToLocalTime().Date)
            .OrderByDescending(g => g.Key);

        foreach (var g in groups)
        {
            var existing = GroupedNotifications.FirstOrDefault(x => x.Date == g.Key);
            if (existing != null)
            {
                foreach (var item in g)
                {
                    if (!existing.Any(x => x.Id == item.Id))
                    {
                        existing.Add(item);
                    }
                }
            }
            else
            {
                var header = FormatDateHeader(g.Key);
                int insertIdx = 0;
                while (insertIdx < GroupedNotifications.Count && GroupedNotifications[insertIdx].Date > g.Key)
                {
                    insertIdx++;
                }
                GroupedNotifications.Insert(insertIdx, new NotificationGroup(header, g.Key, g));
            }
        }
    }

    private static readonly System.Globalization.PersianCalendar PersianCal = new();
    private static readonly string[] PersianMonths =
    {
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    };

    public static string FormatDateHeader(DateTime date)
    {
        var today = DateTime.Today;
        var loc = Services.LocalizationManager.Instance;
        if (date == today)
        {
            return loc["Inbox_Today"];
        }
        if (date == today.AddDays(-1))
        {
            return loc["Inbox_Yesterday"];
        }

        if (loc.CurrentLanguage == "fa")
        {
            try
            {
                int y = PersianCal.GetYear(date);
                int m = PersianCal.GetMonth(date);
                int d = PersianCal.GetDayOfMonth(date);
                string mName = (m >= 1 && m <= 12) ? PersianMonths[m - 1] : m.ToString("00");
                return $"{d} {mName} {y}";
            }
            catch
            {
                return date.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);
            }
        }
        else
        {
            return date.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private static bool MatchesCategory(string? notifCategory, string filterKey)
    {
        if (string.IsNullOrEmpty(notifCategory)) return filterKey == "other";
        if (filterKey is "promo" or "offers")
            return notifCategory.Equals("promo", StringComparison.OrdinalIgnoreCase) ||
                   notifCategory.Equals("offers", StringComparison.OrdinalIgnoreCase);
        return notifCategory.Equals(filterKey, StringComparison.OrdinalIgnoreCase);
    }

    private void OnCategoryFilterChanged()
    {
        _ = RefreshAsync();
    }

    private async Task OpenDetailAsync(NotificationViewModel? vm)
    {
        if (vm == null || string.IsNullOrEmpty(vm.Id)) return;
        await Shell.Current.GoToAsync($"detail?id={Uri.EscapeDataString(vm.Id)}");
    }

    private async Task OpenStubDetailAsync(NotificationViewModel? vm)
    {
        if (vm == null || string.IsNullOrEmpty(vm.Id)) return;
        await Shell.Current.GoToAsync($"stub-detail?id={Uri.EscapeDataString(vm.Id)}");
    }

    private async Task CopyCodeAsync(NotificationViewModel? vm)
    {
        if (vm?.HasCode == true && !string.IsNullOrEmpty(vm.ExtractedCode))
        {
            await Clipboard.SetTextAsync(vm.ExtractedCode);
        }
    }

    private async Task DismissAsync(NotificationViewModel? vm)
    {
        if (vm == null) return;
        await _repository.MarkReadAsync(vm.Id);
        vm.IsRead = true;
        
        var cached = _cachedNotifications.FirstOrDefault(n => n.Id == vm.Id);
        if (cached != null)
        {
            var idx = _cachedNotifications.IndexOf(cached);
            _cachedNotifications[idx] = cached with { IsRead = true };
        }
        UnreadCount = _cachedNotifications.Count(n => !n.IsRead);
    }

    private async Task OpenDailyDigestAsync()
    {
        await Shell.Current.GoToAsync("//digest");
    }

    private NotificationViewModel CreateNotificationViewModel(Notification n) =>
        new(n)
        {
            CopyCodeCommand = CopyCodeCommand,
            DismissCommand = DismissCommand,
            OpenDetailCommand = OpenDetailCommand
        };

    private async Task PopulateSourceInfoAsync(IReadOnlyList<NotificationViewModel> vms)
    {
        if (vms.Count == 0) return;

        var packages = vms
            .SelectMany(v => new[] { v.Package, v.AttributedPackage })
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (packages.Count == 0) return;

        IReadOnlyDictionary<string, AppIdentity> infos;
        try
        {
            infos = await _repository.GetAppInfosAsync(packages);
        }
        catch
        {
            return;
        }

        foreach (var vm in vms)
        {
            infos.TryGetValue(vm.SourcePackage, out var info);
            vm.ApplyAppInfo(info?.Label, GetOrCreateIcon(vm.SourcePackage, info?.IconPng));
        }
    }

    private ImageSource? GetOrCreateIcon(string package, byte[]? png)
    {
        if (png is not { Length: > 0 }) return null;
        if (_sourceIcons.TryGetValue(package, out var cached)) return cached;

        var bytes = png;
        var source = ImageSource.FromStream(() => new MemoryStream(bytes));
        _sourceIcons[package] = source;
        return source;
    }
}

public class NotificationViewModel : INotifyPropertyChanged
{
    private bool _isRead;
    private string _extractedCode = string.Empty;
    private bool _hasCode;
    private string _category = string.Empty;
    private string _categoryMarkColor = string.Empty;
    private string _categoryTintColor = string.Empty;
    private string _sourceLabel = string.Empty;
    private ImageSource? _sourceIcon;

    public string Id { get; }
    public string Package { get; }
    public string? AttributedPackage { get; }
    public string SourcePackage => AttributedPackage ?? Package;
    public bool IsViaSms => !string.IsNullOrEmpty(AttributedPackage);
    public string ChannelName { get; }
    public string Title { get; }
    public string Text { get; }
    public DateTimeOffset Timestamp { get; }
    public bool IsPersian { get; }

    public string SourceLabel
    {
        get => _sourceLabel;
        private set
        {
            if (_sourceLabel == value) return;
            _sourceLabel = value;
            OnPropertyChanged();
        }
    }

    public ImageSource? SourceIcon
    {
        get => _sourceIcon;
        private set
        {
            if (ReferenceEquals(_sourceIcon, value)) return;
            _sourceIcon = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSourceIcon));
        }
    }

    public bool HasSourceIcon => _sourceIcon != null;

    public void ApplyAppInfo(string? label, ImageSource? icon)
    {
        SourceLabel = string.IsNullOrWhiteSpace(label)
            ? PackageNameHumanizer.Humanize(SourcePackage)
            : label!;
        SourceIcon = icon;
    }

    public string Category
    {
        get => _category;
        set { _category = value; OnPropertyChanged(); }
    }

    public string CategoryMarkColor
    {
        get => _categoryMarkColor;
        set { _categoryMarkColor = value; OnPropertyChanged(); }
    }

    public string CategoryTintColor
    {
        get => _categoryTintColor;
        set { _categoryTintColor = value; OnPropertyChanged(); }
    }

    public void UpdateCategory(string newCategory)
    {
        Category = newCategory;
        CategoryMarkColor = GetMarkColor(newCategory);
        CategoryTintColor = GetTintColor(newCategory);
    }

    public System.Windows.Input.ICommand? CopyCodeCommand { get; set; }
    public System.Windows.Input.ICommand? DismissCommand { get; set; }
    public System.Windows.Input.ICommand? OpenDetailCommand { get; set; }

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
        AttributedPackage = notification.AttributedPackage;
        ChannelName = notification.ChannelName;
        Title = notification.Title;
        Text = notification.Text;
        Category = notification.Category;
        Timestamp = notification.Timestamp;
        IsRead = notification.IsRead;
        IsPersian = notification.IsPersian;
        _sourceLabel = PackageNameHumanizer.Humanize(SourcePackage);

        CategoryMarkColor = GetMarkColor(notification.Category);
        CategoryTintColor = GetTintColor(notification.Category);

        if (notification.HasCode)
        {
            ExtractedCode = notification.ExtractedCode ?? string.Empty;
            HasCode = true;
        }
    }

    public string DisplayChannelName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ChannelName)) return string.Empty;
            var ch = ChannelName.Trim();
            if (ch.Equals("fcm_fallback_notification_channel", StringComparison.OrdinalIgnoreCase) ||
                ch.StartsWith("fcm_", StringComparison.OrdinalIgnoreCase) ||
                ch.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                ch.Equals("shell_cmd", StringComparison.OrdinalIgnoreCase) ||
                ch.Equals("we_wk_push_channel", StringComparison.OrdinalIgnoreCase) ||
                ch.Equals("remote-messages", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }
            return ch;
        }
    }

    public static string GetMarkColor(string category) => category?.ToLowerInvariant() switch
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

    public static string GetTintColor(string category) => category?.ToLowerInvariant() switch
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
    private bool _isSelected;

    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string MarkColor { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public int Count
    {
        get => _count;
        set { _count = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class NotificationGroup : ObservableCollection<NotificationViewModel>
{
    private string _header;
    public string Header
    {
        get => _header;
        set
        {
            if (_header != value)
            {
                _header = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Header)));
            }
        }
    }

    public DateTime Date { get; }

    public NotificationGroup(string header, DateTime date, IEnumerable<NotificationViewModel> items)
        : base(items)
    {
        _header = header;
        Date = date;
    }
}