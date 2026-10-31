using Alak.App.Design;
using Alak.App.Services;
using Alak.App.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Alak.App;

public partial class App : Application
{
    public static string? PendingRoute { get; set; }
    public static string? PendingDiagnostics { get; set; }

    public static async Task RunDiagnosticsAsync(string kind)
    {
        try
        {
#if ANDROID
            global::Android.Util.Log.Info("AlakDiag", $"RunDiagnostics request: {kind}");
#endif
            var service = AppServices.Current?.GetService<SlmDiagnosticsService>();
            if (service == null)
            {
#if ANDROID
                global::Android.Util.Log.Warn("AlakDiag", "SlmDiagnosticsService not resolvable");
#endif
                return;
            }

            await service.RunAsync(kind);
        }
        catch (Exception ex)
        {
#if ANDROID
            global::Android.Util.Log.Error("AlakDiag", $"RunDiagnostics failed: {ex}");
#endif
            System.Diagnostics.Debug.WriteLine($"[AlakDiag] run failed: {ex}");
        }
    }

    public App()
    {
        InitializeComponent();

        // Apply persisted theme choice ("light" | "dark" | "system").
        ThemeManager.Apply(ThemeManager.Current);

        // Apply localization and RTL layout
        var lang = Preferences.Get("selected_language", "fa");
        Services.LocalizationManager.Instance.SetLanguage(lang);

        // Start with the full animated sieve loading screen
        MainPage = new LoadingPage();
    }
}
