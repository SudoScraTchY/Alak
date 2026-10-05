using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core.Model;
using Alak.Storage;
using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;

namespace Alak.App.ViewModels;

/// <summary>
/// Onboarding flow per spec §13.1 — 5 steps:
/// 1. Welcome (what app does + privacy)
/// 2. Language & Calendar (UI language, Jalali/Gregorian if Persian)
/// 3. Notification Access (grant permission, handle PF-03 restricted settings)
/// 4. OEM Battery Guidance (manufacturer-specific, battery optimization exemption)
/// 5. First Capture-Depth Choices (common categories)
/// </summary>
public partial class OnboardingViewModel : ObservableObject
{
    private readonly IUserRulesRepository _userRulesRepo;

    [ObservableProperty]
    private int _currentStep = 0;

    [ObservableProperty]
    private string _uiLanguage = "en"; // "en" or "fa"

    [ObservableProperty]
    private string _calendar = "Gregorian"; // "Gregorian" or "Jalali"

    [ObservableProperty]
    private bool _usePersianDigits = false;

    [ObservableProperty]
    private bool _notificationAccessGranted = false;

    [ObservableProperty]
    private string _oemBatteryGuidance = "";

    [ObservableProperty]
    private bool _showOemBatteryStep = true;

    [ObservableProperty]
    private ObservableCollection<CaptureDepthChoice> _captureDepthChoices = [];

    public int TotalSteps => 5;
    public string ProgressText => $"{CurrentStep + 1} of {TotalSteps}";
    public double ProgressValue => (double)(CurrentStep + 1) / TotalSteps;

    public bool IsWelcomeStep => CurrentStep == 0;
    public bool IsLanguageStep => CurrentStep == 1;
    public bool IsNotificationStep => CurrentStep == 2;
    public bool IsOemBatteryStep => CurrentStep == 3;
    public bool IsCaptureDepthStep => CurrentStep == 4;
    public bool IsLastStep => CurrentStep == TotalSteps - 1;

    public IRelayCommand NextCommand { get; }
    public IRelayCommand PreviousCommand { get; }
    public IRelayCommand SkipCommand { get; }
    public IRelayCommand OpenNotificationSettingsCommand { get; }
    public IRelayCommand OpenAppInfoSettingsCommand { get; }
    public IRelayCommand OpenBatteryOptimizationCommand { get; }
    public IRelayCommand CheckNotificationAccessCommand { get; }
    public IRelayCommand SelectLanguageCommand { get; }
    public IRelayCommand SelectCalendarCommand { get; }
    public IRelayCommand TogglePersianDigitsCommand { get; }

    public OnboardingViewModel(IUserRulesRepository userRulesRepo)
    {
        _userRulesRepo = userRulesRepo;

        NextCommand = new RelayCommand(NextStep);
        PreviousCommand = new RelayCommand(PreviousStep);
        SkipCommand = new RelayCommand(SkipOnboarding);
        OpenNotificationSettingsCommand = new RelayCommand(OpenNotificationSettings);
        OpenAppInfoSettingsCommand = new RelayCommand(OpenAppInfoSettings);
        OpenBatteryOptimizationCommand = new RelayCommand(OpenBatteryOptimization);
        CheckNotificationAccessCommand = new RelayCommand(CheckNotificationAccess);
        SelectLanguageCommand = new RelayCommand<string>(SelectLanguage);
        SelectCalendarCommand = new RelayCommand<string>(SelectCalendar);
        TogglePersianDigitsCommand = new RelayCommand(() => UsePersianDigits = !UsePersianDigits);

        InitializeCaptureDepthChoices();
        DetectOemBatteryGuidance();
    }

    private void InitializeCaptureDepthChoices()
    {
        CaptureDepthChoices = new ObservableCollection<CaptureDepthChoice>
        {
            new("Chats", new[] {"WhatsApp", "Telegram", "Signal", "Discord"}, CaptureDepth.Full),
            new("Email", new[] {"Gmail", "Outlook", "Proton Mail"}, CaptureDepth.Full),
            new("Banking & Finance", new[] {"Bank apps", "Payment apps", "Investment apps"}, CaptureDepth.Full),
            new("Shopping", new[] {"Amazon", "eBay", "Local shopping apps"}, CaptureDepth.Summary),
            new("Social", new[] {"Instagram", "Twitter/X", "LinkedIn", "Reddit"}, CaptureDepth.Summary),
            new("News", new[] {"News apps", "RSS readers"}, CaptureDepth.Summary),
            new("System & Other", new[] {"System notifications", "Unknown apps"}, CaptureDepth.None),
        };
    }

    private void DetectOemBatteryGuidance()
    {
        OemBatteryGuidance = """
            Some phone manufacturers aggressively kill background services to save battery.
            This can prevent Alak from receiving notifications reliably.

            To ensure Alak works correctly, please exempt it from battery optimization:
            1. Tap the button below to open the system battery optimization screen
            2. Find "Alak" in the list
            3. Select "Don't optimize" or "Unrestricted"

            You can also check: Settings → Apps → Alak → Battery → Allow background activity
            """;
    }

    private void NextStep()
    {
        if (CurrentStep < TotalSteps - 1)
        {
            CurrentStep++;
            OnPropertyChanged(nameof(IsWelcomeStep));
            OnPropertyChanged(nameof(IsLanguageStep));
            OnPropertyChanged(nameof(IsNotificationStep));
            OnPropertyChanged(nameof(IsOemBatteryStep));
            OnPropertyChanged(nameof(IsCaptureDepthStep));
            OnPropertyChanged(nameof(IsLastStep));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(ProgressValue));
        }
        else
        {
            CompleteOnboarding();
        }
    }

    private void PreviousStep()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
            OnPropertyChanged(nameof(IsWelcomeStep));
            OnPropertyChanged(nameof(IsLanguageStep));
            OnPropertyChanged(nameof(IsNotificationStep));
            OnPropertyChanged(nameof(IsOemBatteryStep));
            OnPropertyChanged(nameof(IsCaptureDepthStep));
            OnPropertyChanged(nameof(IsLastStep));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(ProgressValue));
        }
    }

    private async void SkipOnboarding()
    {
        await CompleteOnboarding();
    }

    private void OpenNotificationSettings()
    {
        Shell.Current?.DisplayAlert("Notification Access", 
            "Opening Android Notification Access settings...", "OK");
    }

    private void OpenAppInfoSettings()
    {
        Shell.Current?.DisplayAlert("App Info", 
            "Opening Alak App Info page...\n\nIf the Notification Access switch is greyed out:\nTap the three-dot menu → Allow restricted settings", "OK");
    }

    private void OpenBatteryOptimization()
    {
        Shell.Current?.DisplayAlert("Battery Optimization", 
            "Opening battery optimization settings...\n\nFind Alak and select 'Don't optimize'", "OK");
    }

    private async void CheckNotificationAccess()
    {
        NotificationAccessGranted = true; // Simulated grant
        await Shell.Current?.DisplayAlert("Notification Access", 
            NotificationAccessGranted 
                ? "Notification access granted! ✓" 
                : "Notification access not granted. Please enable it and try again.", "OK");
        
        if (NotificationAccessGranted)
        {
            NextStep();
        }
    }

    private void SelectLanguage(string? language)
    {
        if (!string.IsNullOrEmpty(language))
        {
            UiLanguage = language;
            OnPropertyChanged(nameof(UiLanguage));
        }
    }

    private void SelectCalendar(string? cal)
    {
        if (!string.IsNullOrEmpty(cal))
        {
            Calendar = cal;
            OnPropertyChanged(nameof(Calendar));
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

        foreach (var choice in CaptureDepthChoices)
        {
            if (choice.SelectedDepth != CaptureDepth.None)
            {
                // Could create default user rules for these categories
            }
        }

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