using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core.Model;
using Alak.Storage;
using System.Windows.Input;
using System.IO;
using Microsoft.Maui.Storage;

namespace Alak.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly INotificationRepository _repository;
    private readonly IUserRulesRepository _rulesRepo;

    [ObservableProperty]
    private bool _notificationsEnabled = true;

    [ObservableProperty]
    private bool _adaptiveLearningEnabled = true;

    [ObservableProperty]
    private string _selectedLanguage = "fa";

    [ObservableProperty]
    private string _selectedCalendar = "jalali";

    [ObservableProperty]
    private string _selectedDigits = "auto";

    [ObservableProperty]
    private string _selectedTheme = "system";

    [ObservableProperty]
    private string _selectedPerformanceProfile = "balanced";

    [ObservableProperty]
    private bool _darkModeEnabled;

    [ObservableProperty]
    private bool _rtlLayoutEnabled = true;

    [ObservableProperty]
    private bool _tier2Enabled = true;

    [ObservableProperty]
    private bool _appLockEnabled;

    [ObservableProperty]
    private bool _hideAmountsInLists;

    [ObservableProperty]
    private bool _notificationRemindersEnabled = true;

    [ObservableProperty]
    private int _globalSizeCapMb = 0;

    [ObservableProperty]
    private int _notificationCount;

    [ObservableProperty]
    private string _storageSize = "0 MB";

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    public ICommand ClearDataCommand { get; }
    public ICommand ExportDataCommand { get; }
    public ICommand OpenOnboardingCommand { get; }
    public ICommand SaveGlobalSettingsCommand { get; }
    public ICommand LoadGlobalSettingsCommand { get; }

    public SettingsViewModel(INotificationRepository repository, IUserRulesRepository rulesRepo)
    {
        _repository = repository;
        _rulesRepo = rulesRepo;

        ClearDataCommand = new AsyncRelayCommand(ClearDataAsync);
        ExportDataCommand = new AsyncRelayCommand(ExportDataAsync);
        OpenOnboardingCommand = new AsyncRelayCommand(OpenOnboardingAsync);
        SaveGlobalSettingsCommand = new AsyncRelayCommand(SaveGlobalSettingsAsync);
        LoadGlobalSettingsCommand = new AsyncRelayCommand(LoadGlobalSettingsAsync);

        LoadSettings();
        _ = LoadGlobalSettingsAsync();
    }

    private void LoadSettings()
    {
        // Load from preferences
        NotificationsEnabled = Preferences.Get("notifications_enabled", true);
        AdaptiveLearningEnabled = Preferences.Get("adaptive_learning_enabled", true);
        SelectedLanguage = Preferences.Get("selected_language", "fa");
        SelectedCalendar = Preferences.Get("selected_calendar", "jalali");
        DarkModeEnabled = Preferences.Get("dark_mode_enabled", false);
        RtlLayoutEnabled = Preferences.Get("rtl_layout_enabled", true);

        // Update counts
        Task.Run(async () =>
        {
            var all = await _repository.GetAllAsync();
            NotificationCount = all.Count();
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "alak.db");
            var dbSize = new FileInfo(dbPath).Length;
            StorageSize = $"{dbSize / 1024 / 1024} MB";
        });
    }

    private async Task LoadGlobalSettingsAsync()
    {
        try
        {
            var settings = await _rulesRepo.GetGlobalSettingsAsync();
            SelectedTheme = settings.Theme;
            SelectedLanguage = settings.UiLanguage;
            SelectedCalendar = settings.Calendar;
            SelectedDigits = settings.Digits;
            SelectedPerformanceProfile = settings.SlmPerformanceProfile;
            Tier2Enabled = settings.Tier2Enabled;
            AppLockEnabled = settings.AppLockEnabled;
            HideAmountsInLists = settings.HideAmountsInLists;
            NotificationRemindersEnabled = settings.NotificationRemindersEnabled;
            GlobalSizeCapMb = settings.GlobalSizeCap ?? 0;

            // Apply theme
            var app = Application.Current;
            if (app != null)
            {
                app.UserAppTheme = settings.Theme switch
                {
                    "dark" => AppTheme.Dark,
                    "light" => AppTheme.Light,
                    _ => AppTheme.Unspecified
                };
            }
        }
        catch
        {
            // Ignore load errors, use defaults
        }
    }

    partial void OnNotificationsEnabledChanged(bool value)
    {
        Preferences.Set("notifications_enabled", value);
    }

    partial void OnAdaptiveLearningEnabledChanged(bool value)
    {
        Preferences.Set("adaptive_learning_enabled", value);
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        Preferences.Set("selected_language", value);
    }

    partial void OnSelectedCalendarChanged(string value)
    {
        Preferences.Set("selected_calendar", value);
    }

    partial void OnSelectedDigitsChanged(string value)
    {
        Preferences.Set("selected_digits", value);
    }

    partial void OnSelectedThemeChanged(string value)
    {
        Preferences.Set("selected_theme", value);
        var app = Application.Current;
        if (app != null)
        {
            app.UserAppTheme = value switch
            {
                "dark" => AppTheme.Dark,
                "light" => AppTheme.Light,
                _ => AppTheme.Unspecified
            };
        }
    }

    partial void OnDarkModeEnabledChanged(bool value)
    {
        Preferences.Set("dark_mode_enabled", value);
        var app = Application.Current;
        if (app != null)
        {
            app.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
        }
    }

    partial void OnRtlLayoutEnabledChanged(bool value)
    {
        Preferences.Set("rtl_layout_enabled", value);
    }

    private async Task SaveGlobalSettingsAsync()
        {
            var settings = new GlobalSettings(
                Theme: SelectedTheme,
                UiLanguage: SelectedLanguage,
                Calendar: SelectedCalendar,
                Digits: SelectedDigits,
                RetentionOverrides: null, // Not editable in UI yet
                GlobalSizeCap: GlobalSizeCapMb > 0 ? GlobalSizeCapMb * 1024 * 1024 : null,
                Tier2Enabled: Tier2Enabled,
                SlmPerformanceProfile: SelectedPerformanceProfile,
                AppLockEnabled: AppLockEnabled,
                HideAmountsInLists: HideAmountsInLists,
                NotificationRemindersEnabled: NotificationRemindersEnabled);

        await _rulesRepo.UpdateGlobalSettingsAsync(settings);

        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
        if (page != null)
        {
            await page.DisplayAlertAsync("Saved", "Global settings saved", "OK");
        }
    }

    private async Task ClearDataAsync()
    {
        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
        if (page == null) return;

        var confirm = await page.DisplayAlertAsync(
            "Clear All Data",
            "This will delete all notifications and reset settings. Continue?",
            "Yes", "No");

        if (confirm)
        {
            await _repository.ClearAllAsync();
            NotificationCount = 0;
            await page.DisplayAlertAsync("Done", "All data cleared", "OK");
        }
    }

    private async Task ExportDataAsync()
    {
        var all = await _repository.GetAllAsync();
        var json = System.Text.Json.JsonSerializer.Serialize(all, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        var filePath = Path.Combine(FileSystem.CacheDirectory, "alak-export.json");
        await File.WriteAllTextAsync(filePath, json);

        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
        if (page == null) return;

        await Share.RequestAsync(new ShareFileRequest
        {
            Title = "Export Alak Data",
            File = new ShareFile(filePath)
        });
    }

    private async Task OpenOnboardingAsync()
    {
        await Shell.Current.GoToAsync("onboarding");
    }
}