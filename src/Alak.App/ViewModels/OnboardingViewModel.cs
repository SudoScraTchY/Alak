using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core.Model;
using Alak.Storage;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;

namespace Alak.App.ViewModels;

/// <summary>
/// Onboarding flow per spec §13.1 — 7 steps:
/// 0. Welcome (what app does + privacy)
/// 1. Language (UI language: en, fa, fa-latn)
/// 2. Calendar (Jalali / Gregorian)
/// 3. Notification Access (grant permission, handle PF-03 restricted settings)
/// 4. Battery Optimization Exemption (ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS)
/// 5. Background Activity & OEM Autostart Guidance (MIUI, Samsung, Huawei, etc.)
/// 6. First Capture-Depth Choices (common categories)
/// </summary>
public partial class OnboardingViewModel : ObservableObject
{
    private readonly IUserRulesRepository _userRulesRepo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle))]
    [NotifyPropertyChangedFor(nameof(StepDescription))]
    [NotifyPropertyChangedFor(nameof(NextButtonText))]
    [NotifyPropertyChangedFor(nameof(IsWelcomeStep))]
    [NotifyPropertyChangedFor(nameof(IsLanguageStep))]
    [NotifyPropertyChangedFor(nameof(IsCalendarStep))]
    [NotifyPropertyChangedFor(nameof(IsNotificationStep))]
    [NotifyPropertyChangedFor(nameof(IsBatteryOptimizationStep))]
    [NotifyPropertyChangedFor(nameof(IsBackgroundProcessStep))]
    [NotifyPropertyChangedFor(nameof(IsCaptureDepthStep))]
    [NotifyPropertyChangedFor(nameof(IsLastStep))]
    [NotifyPropertyChangedFor(nameof(ProgressText))]
    [NotifyPropertyChangedFor(nameof(ProgressValue))]
    private int _currentStep = 0;

    [ObservableProperty]
    private string _uiLanguage = "en"; // "en", "fa", "fa-latn"

    [ObservableProperty]
    private string _selectedLanguage = "en (English)";

    [ObservableProperty]
    private string _calendar = "jalali"; // "jalali" or "gregorian"

    [ObservableProperty]
    private string _selectedCalendar = "jalali (شمسی)";

    [ObservableProperty]
    private bool _usePersianDigits = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotificationAccessStatusText))]
    [NotifyPropertyChangedFor(nameof(NotificationAccessStatusColor))]
    private bool _notificationAccessGranted = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatteryOptimizationStatusText))]
    [NotifyPropertyChangedFor(nameof(BatteryOptimizationStatusColor))]
    private bool _batteryOptimizationExempted = false;

    public string NotificationAccessStatusText => NotificationAccessGranted
        ? "✓ Notification access granted"
        : "Notification access not granted yet";

    public Color NotificationAccessStatusColor => NotificationAccessGranted
        ? Color.FromArgb("#12895B")
        : Color.FromArgb("#8C9196");

    public string BatteryOptimizationStatusText => BatteryOptimizationExempted
        ? "✓ Battery optimization disabled (Unrestricted)"
        : "Battery optimization is currently active";

    public Color BatteryOptimizationStatusColor => BatteryOptimizationExempted
        ? Color.FromArgb("#12895B")
        : Color.FromArgb("#8C9196");

    [ObservableProperty]
    private string _detectedManufacturer = "";

    [ObservableProperty]
    private string _backgroundProcessGuidance = "";

    [ObservableProperty]
    private ObservableCollection<CaptureDepthChoice> _captureDepthChoices = [];

    public int TotalSteps => 7;
    public string ProgressText => $"{CurrentStep + 1} of {TotalSteps}";
    public double ProgressValue => (double)(CurrentStep + 1) / TotalSteps;

    private static readonly (string Title, string Description)[] StepCopy =
    [
        ("Welcome to Alak", "Alak reads your notifications, classifies them, and shows everything organized in one private inbox — all on-device."),
        ("Display Language", "Choose the display language for the app. You can change it later in Settings."),
        ("Calendar System", "Pick your preferred calendar system for dates shown throughout the app."),
        ("Notification Access", "Alak needs permission to read notifications to classify and organize them. No notification data ever leaves your device."),
        ("Battery Optimization", "Exempt Alak from battery optimization so it can process notifications in real time without being delayed or put to sleep."),
        ("Background Activity", "Allow background execution and autostart so device power management does not terminate Alak when the screen is off."),
        ("Capture Depth", "Choose how much of each app's notification content Alak should capture. You can fine-tune this later in Rules."),
    ];

    public string StepTitle => StepCopy[CurrentStep].Title;
    public string StepDescription => StepCopy[CurrentStep].Description;
    public string NextButtonText => IsLastStep ? "Get Started" : "Next";

    public bool IsWelcomeStep => CurrentStep == 0;
    public bool IsLanguageStep => CurrentStep == 1;
    public bool IsCalendarStep => CurrentStep == 2;
    public bool IsNotificationStep => CurrentStep == 3;
    public bool IsBatteryOptimizationStep => CurrentStep == 4;
    public bool IsBackgroundProcessStep => CurrentStep == 5;
    public bool IsCaptureDepthStep => CurrentStep == 6;
    public bool IsLastStep => CurrentStep == TotalSteps - 1;

    public IRelayCommand NextCommand { get; }
    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand BackCommand { get; }
    public IRelayCommand SkipCommand { get; }
    public IRelayCommand OpenNotificationSettingsCommand { get; }
    public IRelayCommand OpenAppInfoSettingsCommand { get; }
    public IRelayCommand OpenBatteryOptimizationCommand { get; }
    public IRelayCommand OpenBatterySettingsListCommand { get; }
    public IRelayCommand OpenAutostartSettingsCommand { get; }
    public IRelayCommand CheckNotificationAccessCommand { get; }
    public IRelayCommand CheckBatteryOptimizationCommand { get; }
    public IRelayCommand RefreshStatusCommand { get; }
    public IRelayCommand SelectLanguageCommand { get; }
    public IRelayCommand SelectCalendarCommand { get; }
    public IRelayCommand TogglePersianDigitsCommand { get; }

    public OnboardingViewModel(IUserRulesRepository userRulesRepo)
    {
        _userRulesRepo = userRulesRepo;

        NextCommand = new RelayCommand(NextStep);
        PreviousCommand = new RelayCommand(PreviousStep);
        BackCommand = new RelayCommand(PreviousStep);
        SkipCommand = new RelayCommand(SkipOnboarding);
        OpenNotificationSettingsCommand = new RelayCommand(OpenNotificationSettings);
        OpenAppInfoSettingsCommand = new RelayCommand(OpenAppInfoSettings);
        OpenBatteryOptimizationCommand = new RelayCommand(OpenBatteryOptimization);
        OpenBatterySettingsListCommand = new RelayCommand(OpenBatterySettingsList);
        OpenAutostartSettingsCommand = new RelayCommand(OpenAutostartSettings);
        CheckNotificationAccessCommand = new RelayCommand(CheckNotificationAccess);
        CheckBatteryOptimizationCommand = new RelayCommand(CheckBatteryOptimization);
        RefreshStatusCommand = new RelayCommand(RefreshStatus);
        SelectLanguageCommand = new RelayCommand<string>(SelectLanguage);
        SelectCalendarCommand = new RelayCommand<string>(SelectCalendar);
        TogglePersianDigitsCommand = new RelayCommand(() => UsePersianDigits = !UsePersianDigits);

        InitInitialSettings();
        InitializeCaptureDepthChoices();
        DetectOemGuidance();
        RefreshStatus();
    }

    private void InitInitialSettings()
    {
        var savedLang = Preferences.Get("selected_language", "en");
        UiLanguage = savedLang;
        SelectedLanguage = savedLang switch
        {
            "fa" => "fa (فارسی)",
            "fa-latn" => "fa-latn (Finglish)",
            _ => "en (English)"
        };

        var savedCal = Preferences.Get("selected_calendar", "jalali");
        Calendar = savedCal;
        SelectedCalendar = savedCal.Equals("gregorian", StringComparison.OrdinalIgnoreCase)
            ? "gregorian"
            : "jalali (شمسی)";
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (value.StartsWith("fa-latn", StringComparison.OrdinalIgnoreCase))
        {
            UiLanguage = "fa-latn";
            Services.LocalizationManager.Instance.SetLanguage("fa");
        }
        else if (value.StartsWith("fa", StringComparison.OrdinalIgnoreCase))
        {
            UiLanguage = "fa";
            Services.LocalizationManager.Instance.SetLanguage("fa");
        }
        else
        {
            UiLanguage = "en";
            Services.LocalizationManager.Instance.SetLanguage("en");
        }
        Preferences.Set("selected_language", UiLanguage);
    }

    partial void OnSelectedCalendarChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (value.Contains("jalali", StringComparison.OrdinalIgnoreCase) || value.Contains("شمسی"))
        {
            Calendar = "jalali";
        }
        else
        {
            Calendar = "gregorian";
        }
        Preferences.Set("selected_calendar", Calendar);
    }

    private void InitializeCaptureDepthChoices()
    {
        CaptureDepthChoices =
        [
            new("Chats", ["WhatsApp", "Telegram", "Signal", "Discord"], CaptureDepth.Full),
            new("Email", ["Gmail", "Outlook", "Proton Mail"], CaptureDepth.Full),
            new("Banking & Finance", ["Bank apps", "Payment apps", "Investment apps"], CaptureDepth.Full),
            new("Shopping", ["Amazon", "Digikala", "Snapp", "Local shopping apps"], CaptureDepth.Summary),
            new("Social", ["Instagram", "Twitter/X", "LinkedIn", "Reddit"], CaptureDepth.Summary),
            new("News", ["News apps", "RSS readers"], CaptureDepth.Summary),
            new("System & Other", ["System notifications", "Unknown apps"], CaptureDepth.None)
        ];
    }

    private void DetectOemGuidance()
    {
#if ANDROID
        var mfg = Android.OS.Build.Manufacturer?.Trim() ?? "";
        DetectedManufacturer = string.IsNullOrEmpty(mfg) ? "Android" : mfg;
        var lower = mfg.ToLowerInvariant();

        if (lower.Contains("xiaomi") || lower.Contains("redmi") || lower.Contains("poco"))
        {
            BackgroundProcessGuidance =
                "Xiaomi / MIUI / HyperOS:\n" +
                "1. Tap 'Open Autostart Settings' below.\n" +
                "2. Find 'Alak' and toggle Autostart to ON.\n" +
                "3. In App Info → Battery saver, select 'No restrictions'.";
        }
        else if (lower.Contains("samsung"))
        {
            BackgroundProcessGuidance =
                "Samsung One UI:\n" +
                "1. Tap below to open Device Care / Battery settings.\n" +
                "2. Navigate to 'Background usage limits' → 'Never sleeping apps'.\n" +
                "3. Add 'Alak' to the list so it is never paused.";
        }
        else if (lower.Contains("huawei") || lower.Contains("honor"))
        {
            BackgroundProcessGuidance =
                "Huawei / Honor:\n" +
                "1. Tap below to open App Launch settings.\n" +
                "2. Find 'Alak' and change to 'Manage manually'.\n" +
                "3. Enable 'Auto-launch', 'Secondary launch', and 'Run in background'.";
        }
        else if (lower.Contains("oppo") || lower.Contains("realme") || lower.Contains("oneplus"))
        {
            BackgroundProcessGuidance =
                "Oppo / Realme / OnePlus:\n" +
                "1. Tap below to open App Management / Startup settings.\n" +
                "2. Enable 'Auto-launch' for Alak.\n" +
                "3. Allow background activity in Battery settings.";
        }
        else if (lower.Contains("vivo"))
        {
            BackgroundProcessGuidance =
                "Vivo / iQOO:\n" +
                "1. Tap below to open Background Power settings.\n" +
                "2. Find 'Alak' and enable 'High background power consumption'.\n" +
                "3. Enable Autostart in permission management.";
        }
        else
        {
            BackgroundProcessGuidance =
                "Standard Android:\n" +
                "1. Tap 'Open App Info' below.\n" +
                "2. Go to 'App battery usage' / 'Battery'.\n" +
                "3. Select 'Unrestricted' and allow background execution.";
        }
#else
        DetectedManufacturer = "Generic";
        BackgroundProcessGuidance =
            "Background Execution:\n" +
            "Ensure background activity and autostart are enabled so Alak can continuously capture notifications and deliver promo code reminders.";
#endif
    }

    public void RefreshStatus()
    {
        CheckNotificationAccess();
        CheckBatteryOptimization();
    }

    private void NextStep()
    {
        if (CurrentStep < TotalSteps - 1)
        {
            CurrentStep++;
            NotifyStepChanged();
        }
        else
        {
            _ = CompleteOnboarding();
        }
    }

    private void PreviousStep()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
            NotifyStepChanged();
        }
    }

    private void NotifyStepChanged()
    {
        OnPropertyChanged(nameof(IsWelcomeStep));
        OnPropertyChanged(nameof(IsLanguageStep));
        OnPropertyChanged(nameof(IsCalendarStep));
        OnPropertyChanged(nameof(IsNotificationStep));
        OnPropertyChanged(nameof(IsBatteryOptimizationStep));
        OnPropertyChanged(nameof(IsBackgroundProcessStep));
        OnPropertyChanged(nameof(IsCaptureDepthStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepDescription));
        OnPropertyChanged(nameof(NextButtonText));

        RefreshStatus();
    }

    private async void SkipOnboarding()
    {
        await CompleteOnboarding();
    }

    public void OpenNotificationSettings()
    {
#if ANDROID
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var context = (Android.Content.Context?)activity ?? Android.App.Application.Context;
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionNotificationListenerSettings);

            // On Android 11+ (API 30+), pass component name so system highlights or directly opens Alak's listener
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R)
            {
                var componentName = new Android.Content.ComponentName(context.PackageName!, "com.alak.app.NotificationCaptureService");
                intent.PutExtra("android.provider.extra.NOTIFICATION_LISTENER_COMPONENT_NAME", componentName.FlattenToString());
            }

            if (activity != null)
            {
                activity.StartActivity(intent);
            }
            else
            {
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
        }
        catch
        {
            try
            {
                // Fallback to notification listener settings without extra
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                var context = (Android.Content.Context?)activity ?? Android.App.Application.Context;
                var fallbackIntent = new Android.Content.Intent(Android.Provider.Settings.ActionNotificationListenerSettings);
                if (activity != null)
                {
                    activity.StartActivity(fallbackIntent);
                }
                else
                {
                    fallbackIntent.AddFlags(Android.Content.ActivityFlags.NewTask);
                    context.StartActivity(fallbackIntent);
                }
            }
            catch
            {
                OpenAppInfoSettings();
            }
        }
#else
        Shell.Current?.DisplayAlert("Notification Access", "Opening Android Notification Access settings...", "OK");
#endif
    }

    public void OpenAppInfoSettings()
    {
#if ANDROID
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var context = (Android.Content.Context?)activity ?? Android.App.Application.Context;
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionApplicationDetailsSettings);
            intent.SetData(Android.Net.Uri.Parse($"package:{context.PackageName}"));

            if (activity != null)
            {
                activity.StartActivity(intent);
            }
            else
            {
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
        }
        catch
        {
            Shell.Current?.DisplayAlert("App Info", "Please open Android Settings → Apps → Alak.", "OK");
        }
#else
        Shell.Current?.DisplayAlert("App Info",
            "Opening Alak App Info page...\n\nIf the Notification Access switch is greyed out:\nTap the three-dot menu → Allow restricted settings", "OK");
#endif
    }

    public void OpenBatteryOptimization()
    {
#if ANDROID
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var context = (Android.Content.Context?)activity ?? Android.App.Application.Context;

            // Direct prompt dialog (requires REQUEST_IGNORE_BATTERY_OPTIMIZATIONS in manifest)
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionRequestIgnoreBatteryOptimizations);
            intent.SetData(Android.Net.Uri.Parse($"package:{context.PackageName}"));

            if (activity != null)
            {
                activity.StartActivity(intent);
            }
            else
            {
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
        }
        catch
        {
            // Fallback: Open system battery optimization list
            OpenBatterySettingsList();
        }
#else
        Shell.Current?.DisplayAlert("Battery Optimization", "Opening battery optimization settings...", "OK");
#endif
    }

    public void OpenBatterySettingsList()
    {
#if ANDROID
        try
        {
            var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
            var context = (Android.Content.Context?)activity ?? Android.App.Application.Context;
            var intent = new Android.Content.Intent(Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);

            if (activity != null)
            {
                activity.StartActivity(intent);
            }
            else
            {
                intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                context.StartActivity(intent);
            }
        }
        catch
        {
            OpenAppInfoSettings();
        }
#else
        Shell.Current?.DisplayAlert("Battery Settings", "Opening battery optimization list...", "OK");
#endif
    }

    public void OpenAutostartSettings()
    {
#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        var context = (Android.Content.Context?)activity ?? Android.App.Application.Context;
        var manufacturer = Android.OS.Build.Manufacturer?.ToLowerInvariant() ?? "";

        List<Android.Content.Intent> intents = [];

        if (manufacturer.Contains("xiaomi") || manufacturer.Contains("redmi") || manufacturer.Contains("poco"))
        {
            var i1 = new Android.Content.Intent();
            i1.SetComponent(new Android.Content.ComponentName("com.miui.securitycenter", "com.miui.permcenter.autostart.AutoStartManagementActivity"));
            intents.Add(i1);

            var i2 = new Android.Content.Intent("miui.intent.action.OP_AUTO_START");
            i2.AddCategory(Android.Content.Intent.CategoryDefault);
            intents.Add(i2);
        }
        else if (manufacturer.Contains("huawei") || manufacturer.Contains("honor"))
        {
            var i1 = new Android.Content.Intent();
            i1.SetComponent(new Android.Content.ComponentName("com.huawei.systemmanager", "com.huawei.systemmanager.startupmgr.ui.StartupNormalAppListActivity"));
            intents.Add(i1);

            var i2 = new Android.Content.Intent();
            i2.SetComponent(new Android.Content.ComponentName("com.huawei.systemmanager", "com.huawei.systemmanager.optimize.bootstart.BootStartActivity"));
            intents.Add(i2);
        }
        else if (manufacturer.Contains("samsung"))
        {
            var i1 = new Android.Content.Intent();
            i1.SetComponent(new Android.Content.ComponentName("com.samsung.android.lool", "com.samsung.android.sm.ui.battery.BatteryActivity"));
            intents.Add(i1);

            var i2 = new Android.Content.Intent();
            i2.SetComponent(new Android.Content.ComponentName("com.samsung.android.sm", "com.samsung.android.sm.battery.ui.BatteryActivity"));
            intents.Add(i2);
        }
        else if (manufacturer.Contains("oppo") || manufacturer.Contains("realme") || manufacturer.Contains("oneplus"))
        {
            var i1 = new Android.Content.Intent();
            i1.SetComponent(new Android.Content.ComponentName("com.coloros.safecenter", "com.coloros.safecenter.permission.startup.StartupAppListActivity"));
            intents.Add(i1);

            var i2 = new Android.Content.Intent();
            i2.SetComponent(new Android.Content.ComponentName("com.coloros.safecenter", "com.coloros.safecenter.startupapp.StartupAppListActivity"));
            intents.Add(i2);
        }
        else if (manufacturer.Contains("vivo"))
        {
            var i1 = new Android.Content.Intent();
            i1.SetComponent(new Android.Content.ComponentName("com.iqoo.secure", "com.iqoo.secure.ui.phoneoptimize.AddWhiteListActivity"));
            intents.Add(i1);

            var i2 = new Android.Content.Intent();
            i2.SetComponent(new Android.Content.ComponentName("com.vivo.permissionmanager", "com.vivo.permissionmanager.activity.PurviewTabActivity"));
            intents.Add(i2);
        }
        else if (manufacturer.Contains("transsion") || manufacturer.Contains("infinix") || manufacturer.Contains("tecno"))
        {
            var i1 = new Android.Content.Intent();
            i1.SetComponent(new Android.Content.ComponentName("com.transsion.phonemaster", "com.transsion.phonemaster.StartupManager"));
            intents.Add(i1);
        }

        // Always fallback to App Info where users can manage Background Activity / Battery / Permissions
        var fallback = new Android.Content.Intent(Android.Provider.Settings.ActionApplicationDetailsSettings);
        fallback.SetData(Android.Net.Uri.Parse($"package:{context.PackageName}"));
        intents.Add(fallback);

        foreach (var intent in intents)
        {
            try
            {
                if (activity != null)
                {
                    activity.StartActivity(intent);
                }
                else
                {
                    intent.AddFlags(Android.Content.ActivityFlags.NewTask);
                    context.StartActivity(intent);
                }
                return;
            }
            catch
            {
                // Continue to next candidate
            }
        }
#else
        Shell.Current?.DisplayAlert("Autostart Settings", "Opening device autostart / background management settings...", "OK");
#endif
    }

    public void CheckNotificationAccess()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var pkgName = context.PackageName;

            var packages = AndroidX.Core.App.NotificationManagerCompat.GetEnabledListenerPackages(context);
            if (packages != null && packages.Contains(pkgName))
            {
                NotificationAccessGranted = true;
                return;
            }

            var flat = Android.Provider.Settings.Secure.GetString(
                context.ContentResolver,
                "enabled_notification_listeners");
            NotificationAccessGranted = !string.IsNullOrEmpty(flat) && flat.Contains(pkgName!);
        }
        catch
        {
            NotificationAccessGranted = false;
        }
#else
        NotificationAccessGranted = true;
#endif
    }

    public void CheckBatteryOptimization()
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var powerManager = (Android.OS.PowerManager?)context.GetSystemService(Android.Content.Context.PowerService);
            BatteryOptimizationExempted = powerManager?.IsIgnoringBatteryOptimizations(context.PackageName) ?? false;
        }
        catch
        {
            BatteryOptimizationExempted = false;
        }
#else
        BatteryOptimizationExempted = true;
#endif
    }

    private void SelectLanguage(string? language)
    {
        if (!string.IsNullOrEmpty(language))
        {
            SelectedLanguage = language;
        }
    }

    private void SelectCalendar(string? cal)
    {
        if (!string.IsNullOrEmpty(cal))
        {
            SelectedCalendar = cal;
        }
    }

    private async Task CompleteOnboarding()
    {
        var globalSettings = await _userRulesRepo.GetGlobalSettingsAsync();
        globalSettings = globalSettings with
        {
            UiLanguage = UiLanguage,
            Calendar = Calendar,
            Digits = UsePersianDigits ? "persian" : "latin",
        };
        await _userRulesRepo.UpdateGlobalSettingsAsync(globalSettings);

        // Mark onboarding complete - store in preferences
        Preferences.Set("onboarding_completed", true);
        Preferences.Set("onboarding_completed_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Navigate to main app (Inbox)
        await Shell.Current?.GoToAsync("//inbox");
    }
}

public class CaptureDepthChoice
{
    public string Category { get; }
    public IReadOnlyList<string> ExampleApps { get; }
    public CaptureDepth SelectedDepth { get; set; }

    public CaptureDepthChoice(string category, IReadOnlyList<string> exampleApps, CaptureDepth defaultDepth)
    {
        Category = category;
        ExampleApps = exampleApps;
        SelectedDepth = defaultDepth;
    }

    public string ExampleAppsText => string.Join(", ", ExampleApps);
}

public enum CaptureDepth
{
    None = 0,
    Summary = 1,
    Full = 2
}
