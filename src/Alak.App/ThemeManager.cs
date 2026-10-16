using Microsoft.Maui.Controls;

namespace Alak.App;

/// <summary>
/// Theme switching (SPEC §11.4 / §12.2). All themed colors live in XAML as
/// AppThemeBinding setters — the ONLY thing this class does is set
/// Application.UserAppTheme ("light" | "dark" | "system") and persist the choice.
///
/// (The previous approach — rewriting color resources at runtime — could never work:
/// StaticResource references are resolved once at XAML load and never re-read.)
/// </summary>
public static class ThemeManager
{
    /// <summary>Apply "light" | "dark" | "system". Call on startup and on change.</summary>
    public static void Apply(string theme)
    {
        Preferences.Set("selected_theme", theme);

        var app = Application.Current;
        if (app == null) return;

        app.UserAppTheme = theme switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified, // "system" follows the OS
        };
    }

    public static string Current => Preferences.Get("selected_theme", "system");
}
