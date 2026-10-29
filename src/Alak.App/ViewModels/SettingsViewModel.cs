using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core.Model;
using Alak.Storage;
using Alak.App.Services;
using System.Windows.Input;
using System.IO;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Alak.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly INotificationRepository _repository;
    private readonly IUserRulesRepository _rulesRepo;
    private readonly IModelService _modelService;
    private readonly IBiometricAuthService _biometricService;
    private readonly IBackupService _backupService;

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
    private bool _appOpenPromoRemindersEnabled = true;

    [ObservableProperty]
    private bool _hasUsageAccess;

    [ObservableProperty]
    private string _usageAccessStatusText = "";

    [ObservableProperty]
    private bool _excludeCommunications = false;

    [ObservableProperty]
    private int _globalSizeCapMb = 0;

    [ObservableProperty]
    private int _notificationCount;

    [ObservableProperty]
    private string _storageSize = "0 MB";

    [ObservableProperty]
    private string _appVersion = "1.0.0";

    // SLM model properties
    [ObservableProperty]
    private bool _isModelDownloaded;

    [ObservableProperty]
    private bool _isModelDownloading;

    [ObservableProperty]
    private double _modelDownloadProgress;

    [ObservableProperty]
    private string _modelStatusText = "";

    [ObservableProperty]
    private string _benchmarkResult = "";

    public ICommand ClearDataCommand { get; }
    public ICommand ExportDataCommand { get; }
    public ICommand ExportBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand OpenOnboardingCommand { get; }
    public ICommand OpenSourcesCommand { get; }
    public ICommand OpenRulesCommand { get; }
    public ICommand OpenModelsCommand { get; }
    public ICommand SaveGlobalSettingsCommand { get; }
    public ICommand LoadGlobalSettingsCommand { get; }
    public ICommand ImportModelCommand { get; }
    public ICommand DownloadModelCommand { get; }
    public ICommand RunBenchmarkCommand { get; }
    public ICommand RequestUsageAccessCommand { get; }

    public SettingsViewModel(
        INotificationRepository repository,
        IUserRulesRepository rulesRepo,
        IModelService modelService,
        IBiometricAuthService biometricService,
        IBackupService backupService)
    {
        _repository = repository;
        _rulesRepo = rulesRepo;
        _modelService = modelService;
        _biometricService = biometricService;
        _backupService = backupService;

        ClearDataCommand = new AsyncRelayCommand(ClearDataAsync);
        ExportDataCommand = new AsyncRelayCommand(ExportDataAsync);
        ExportBackupCommand = new AsyncRelayCommand(ExportBackupAsync);
        RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);
        OpenOnboardingCommand = new AsyncRelayCommand(OpenOnboardingAsync);
        OpenSourcesCommand = new AsyncRelayCommand(OpenSourcesAsync);
        OpenRulesCommand = new AsyncRelayCommand(async () => await Shell.Current.GoToAsync("rules"));
        OpenModelsCommand = new AsyncRelayCommand(async () => await Shell.Current.GoToAsync("models"));
        SaveGlobalSettingsCommand = new AsyncRelayCommand(SaveGlobalSettingsAsync);
        LoadGlobalSettingsCommand = new AsyncRelayCommand(LoadGlobalSettingsAsync);
        ImportModelCommand = new AsyncRelayCommand(ImportModelAsync);
        DownloadModelCommand = OpenModelsCommand;
        RunBenchmarkCommand = new AsyncRelayCommand(RunBenchmarkAsync);
        RequestUsageAccessCommand = new RelayCommand(RequestUsageAccess);

        LoadSettings();
        _ = LoadGlobalSettingsAsync();
    }

    private void LoadSettings()
    {
        // Load from preferences
        NotificationsEnabled = Preferences.Get("notifications_enabled", true);
        AdaptiveLearningEnabled = Preferences.Get("adaptive_learning_enabled", true);
        AppOpenPromoRemindersEnabled = Preferences.Get("notify_on_app_open", true);
        RefreshPermissions();
        SelectedLanguage = Preferences.Get("selected_language", "fa");
        SelectedCalendar = Preferences.Get("selected_calendar", "jalali");
        DarkModeEnabled = Preferences.Get("dark_mode_enabled", false);
        RtlLayoutEnabled = Preferences.Get("rtl_layout_enabled", SelectedLanguage == "fa");
        ExcludeCommunications = Preferences.Get("exclude_communications", false);
        AppLockEnabled = Preferences.Get("app_lock_enabled", false);

        // Update model status
        UpdateModelStatus();

        // Update counts
        Task.Run(async () =>
        {
            var all = await _repository.GetAllAsync();
            NotificationCount = all.Count();
            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "alak.db");
            if (File.Exists(dbPath))
            {
                var dbSize = new FileInfo(dbPath).Length;
                StorageSize = $"{dbSize / 1024 / 1024} MB";
            }
        });
    }

    private void UpdateModelStatus()
    {
        IsModelDownloaded = _modelService.IsModelDownloaded;
        var loc = LocalizationManager.Instance;
        if (IsModelDownloaded)
        {
            ModelStatusText = loc["Settings_ModelReady"];
        }
        else
        {
            ModelStatusText = loc["Settings_NoModel"];
        }
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
            ExcludeCommunications = settings.ExcludeCommunications;
            Preferences.Set("exclude_communications", ExcludeCommunications);
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

    partial void OnAppOpenPromoRemindersEnabledChanged(bool value)
    {
        Preferences.Set("notify_on_app_open", value);
    }

    public void RefreshPermissions()
    {
#if ANDROID
        try
        {
            HasUsageAccess = Platforms.Android.AppLaunchPromoReminderService.HasUsageAccess(global::Android.App.Application.Context);
            UsageAccessStatusText = HasUsageAccess
                ? LocalizationManager.Instance["Settings_UsageAccessGranted"]
                : LocalizationManager.Instance["Settings_UsageAccessNeeded"];
        }
        catch
        {
            HasUsageAccess = false;
            UsageAccessStatusText = LocalizationManager.Instance["Settings_UsageAccessNeeded"];
        }
#else
        HasUsageAccess = true;
        UsageAccessStatusText = LocalizationManager.Instance["Settings_UsageAccessGranted"];
#endif
    }

    private void RequestUsageAccess()
    {
#if ANDROID
        try
        {
            Platforms.Android.AppLaunchPromoReminderService.OpenUsageAccessSettings(global::Android.App.Application.Context);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening usage access settings: {ex.Message}");
        }
#endif
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        LocalizationManager.Instance.SetLanguage(value);
        RtlLayoutEnabled = LocalizationManager.Instance.IsRtl;
        Preferences.Set("selected_language", value);
        UpdateModelStatus();
        RefreshPermissions();
        _ = SaveGlobalSettingsAsync();
    }

    partial void OnSelectedCalendarChanged(string value)
    {
        Preferences.Set("selected_calendar", value);
        _ = SaveGlobalSettingsAsync();
    }

    partial void OnSelectedDigitsChanged(string value)
    {
        Preferences.Set("selected_digits", value);
        _ = SaveGlobalSettingsAsync();
    }

    partial void OnSelectedThemeChanged(string value)
    {
        ThemeManager.Apply(value);
        _ = SaveGlobalSettingsAsync();
    }

    partial void OnRtlLayoutEnabledChanged(bool value)
    {
        Preferences.Set("rtl_layout_enabled", value);
        if (Shell.Current != null)
        {
            Shell.Current.FlowDirection = value ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }
    }

    partial void OnSelectedPerformanceProfileChanged(string value)
    {
        _ = SaveGlobalSettingsAsync();
    }

    partial void OnTier2EnabledChanged(bool value)
    {
        _ = SaveGlobalSettingsAsync();
    }

    private bool _isAuthenticatingLock;

    partial void OnAppLockEnabledChanged(bool value)
    {
        if (_isAuthenticatingLock) return;

        if (value)
        {
            _ = Task.Run(async () =>
            {
                _isAuthenticatingLock = true;
                try
                {
                    var authenticated = await _biometricService.AuthenticateAsync(
                        "Alak Lock",
                        "Authenticate to enable app lock protection");

                    if (!authenticated)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            AppLockEnabled = false;
                        });
                        return;
                    }

                    Preferences.Set("app_lock_enabled", true);
                    await SaveGlobalSettingsAsync();
                }
                finally
                {
                    _isAuthenticatingLock = false;
                }
            });
        }
        else
        {
            Preferences.Set("app_lock_enabled", false);
            _ = SaveGlobalSettingsAsync();
        }
    }

    partial void OnHideAmountsInListsChanged(bool value)
    {
        _ = SaveGlobalSettingsAsync();
    }

    partial void OnNotificationRemindersEnabledChanged(bool value)
    {
        _ = SaveGlobalSettingsAsync();
    }

    partial void OnExcludeCommunicationsChanged(bool value)
    {
        Preferences.Set("exclude_communications", value);
        _ = SaveGlobalSettingsAsync();
    }

    partial void OnGlobalSizeCapMbChanged(int value)
    {
        _ = SaveGlobalSettingsAsync();
    }

    private async Task ImportModelAsync()
    {
        if (IsModelDownloading) return;

        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
        if (page == null) return;
        var loc = LocalizationManager.Instance;

        try
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "*/*", "application/octet-stream" } },
                { DevicePlatform.WinUI, new[] { ".gguf", "*.*" } },
                { DevicePlatform.iOS, new[] { "public.data" } },
                { DevicePlatform.MacCatalyst, new[] { "public.data" } }
            });

            var pickResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = loc["Settings_ImportModelDesc"],
                FileTypes = customFileType
            });

            if (pickResult == null) return;

            IsModelDownloading = true;
            ModelDownloadProgress = 0.0;
            ModelStatusText = "Importing GGUF model...";

            var progress = new Progress<double>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    ModelDownloadProgress = p;
                    ModelStatusText = $"Importing: {p:P0}";
                });
            });

            bool success = await _modelService.ImportModelAsync(pickResult, progress, CancellationToken.None);
            UpdateModelStatus();

            if (success)
            {
                await page.DisplayAlertAsync(loc["Common_Done"], loc["Model_ImportSuccess"], loc["Common_OK"]);
            }
            else
            {
                await page.DisplayAlertAsync(loc["Common_Error"], loc["Model_InvalidHeader"], loc["Common_OK"]);
            }
        }
        catch (Exception ex)
        {
            ModelStatusText = $"Import failed: {ex.Message}";
            await page.DisplayAlertAsync(loc["Common_Error"], ex.Message, loc["Common_OK"]);
        }
        finally
        {
            IsModelDownloading = false;
        }
    }

    private async Task ExportBackupAsync()
    {
        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
        if (page == null) return;
        var loc = LocalizationManager.Instance;

        var passphrase = await page.DisplayPromptAsync(
            loc["Backup_ExportTitle"],
            loc["Backup_PassphrasePrompt"],
            loc["Common_OK"],
            loc["Common_Cancel"],
            placeholder: "Passphrase",
            maxLength: 64,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(passphrase)) return;

        try
        {
            using var mem = new MemoryStream();
            await _backupService.ExportBackupAsync(mem, passphrase);
            mem.Position = 0;

            var fileName = $"alak_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.alakbackup";

            try
            {
                var fileSaverResult = await CommunityToolkit.Maui.Storage.FileSaver.Default.SaveAsync(fileName, mem, CancellationToken.None);
                if (fileSaverResult.IsSuccessful)
                {
                    await page.DisplayAlertAsync(loc["Common_Done"], loc["Backup_ExportSuccess"], loc["Common_OK"]);
                    return;
                }
                else if (fileSaverResult.Exception == null)
                {
                    // User deliberately cancelled the file picker dialog
                    return;
                }
            }
            catch
            {
                // Fallback to cache + share
            }

            var cachePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllBytesAsync(cachePath, mem.ToArray());
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Alak Encrypted Backup",
                File = new ShareFile(cachePath)
            });
        }
        catch (Exception ex)
        {
            await page.DisplayAlertAsync(loc["Common_Error"], ex.Message, loc["Common_OK"]);
        }
    }

    private async Task RestoreBackupAsync()
    {
        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
        if (page == null) return;
        var loc = LocalizationManager.Instance;

        try
        {
            var customFileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.Android, new[] { "*/*", "application/octet-stream" } },
                { DevicePlatform.WinUI, new[] { ".alakbackup", "*.*" } },
                { DevicePlatform.iOS, new[] { "public.data" } },
                { DevicePlatform.MacCatalyst, new[] { "public.data" } }
            });

            var pickResult = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = loc["Backup_SelectFile"],
                FileTypes = customFileType
            });

            if (pickResult == null) return;

            var passphrase = await page.DisplayPromptAsync(
                loc["Backup_RestoreTitle"],
                loc["Backup_RestorePrompt"],
                loc["Common_OK"],
                loc["Common_Cancel"],
                placeholder: "Passphrase",
                maxLength: 64,
                keyboard: Keyboard.Text);

            if (string.IsNullOrWhiteSpace(passphrase)) return;

            byte[] backupBytes;
            await using (var rawStream = await pickResult.OpenReadAsync())
            {
                using var mem = new MemoryStream();
                await rawStream.CopyToAsync(mem);
                backupBytes = mem.ToArray();
            }

            BackupMetadata metadata;
            using (var inspectStream = new MemoryStream(backupBytes))
            {
                metadata = await _backupService.InspectBackupAsync(inspectStream, passphrase);
            }

            var modeChoice = await page.DisplayActionSheetAsync(
                $"{loc["Backup_MetadataFound"]}: {metadata.NotificationCount} notifications, {metadata.UserRuleCount} rules",
                loc["Common_Cancel"],
                null,
                loc["Backup_ModeMerge"],
                loc["Backup_ModeReplace"]);

            if (modeChoice == loc["Common_Cancel"] || string.IsNullOrEmpty(modeChoice))
                return;

            var mode = modeChoice == loc["Backup_ModeReplace"]
                ? RestoreMode.Replace
                : RestoreMode.Merge;

            using (var restoreStream = new MemoryStream(backupBytes))
            {
                await _backupService.RestoreBackupAsync(restoreStream, passphrase, mode);
            }

            LoadSettings();
            await page.DisplayAlertAsync(loc["Common_Done"], loc["Backup_RestoreSuccess"], loc["Common_OK"]);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            await page.DisplayAlertAsync(loc["Common_Error"], loc["Backup_WrongPassphraseOrCorrupted"], loc["Common_OK"]);
        }
        catch (Exception ex)
        {
            await page.DisplayAlertAsync(loc["Common_Error"], ex.Message, loc["Common_OK"]);
        }
    }

    private async Task RunBenchmarkAsync()
    {
        BenchmarkResult = "Running SLM benchmark...";
        try
        {
            var result = await _modelService.BenchmarkAsync(CancellationToken.None);
            BenchmarkResult = result;
        }
        catch (Exception ex)
        {
            BenchmarkResult = $"Error: {ex.Message}";
        }
    }

    private async Task SaveGlobalSettingsAsync()
    {
        var settings = new GlobalSettings(
            Theme: SelectedTheme,
            UiLanguage: SelectedLanguage,
            Calendar: SelectedCalendar,
            Digits: SelectedDigits,
            RetentionOverrides: null,
            GlobalSizeCap: GlobalSizeCapMb > 0 ? GlobalSizeCapMb * 1024 * 1024 : null,
            Tier2Enabled: Tier2Enabled,
            SlmPerformanceProfile: SelectedPerformanceProfile,
            AppLockEnabled: AppLockEnabled,
            HideAmountsInLists: HideAmountsInLists,
            NotificationRemindersEnabled: NotificationRemindersEnabled,
            ExcludeCommunications: ExcludeCommunications);

        await _rulesRepo.UpdateGlobalSettingsAsync(settings);
    }

    private async Task ClearDataAsync()
    {
        var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
        if (page == null) return;

        var loc = LocalizationManager.Instance;
        var confirm = await page.DisplayAlertAsync(
            loc["Settings_ClearAllData"],
            "This will delete all notifications and reset settings. Continue?",
            loc["Common_Yes"], loc["Common_No"]);

        if (confirm)
        {
            await _repository.ClearAllAsync();
            NotificationCount = 0;
            await page.DisplayAlertAsync(loc["Common_Done"], "All data cleared", loc["Common_OK"]);
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

    private async Task OpenSourcesAsync() => await Shell.Current.GoToAsync("sources");

    private async Task OpenOnboardingAsync() => await Shell.Current.GoToAsync("onboarding");
}