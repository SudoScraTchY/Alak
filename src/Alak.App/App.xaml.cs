using Alak.App.Design;
using Alak.App.Views;
using Microsoft.Maui.Controls;

namespace Alak.App;

public partial class App : Application
{
    public static string? PendingRoute { get; set; }

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
