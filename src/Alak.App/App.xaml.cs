using Alak.App.Design;
using Alak.App.Views;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Alak.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Apply persisted theme choice ("light" | "dark" | "system").
        // ThemeManager rewrites semantic color tokens; UserAppTheme drives any
        // point-of-use AppThemeBindings.
        Alak.App.ThemeManager.Apply(Alak.App.ThemeManager.Current);

        // Shell-based navigation requires MainPage to be the AppShell
        MainPage = new AppShell();

        // First run: launch onboarding (spec §13.1) once.
        // Route to a ShellContent ("//inbox") first so the shell has a valid
        // current page before we push "onboarding" on top; GoToAsync from
        // within the App constructor runs before the shell is loaded and
        // silently no-ops.
        if (!Preferences.Get("onboarding_completed", false))
        {
            _ = MainPage.Dispatcher.DispatchAsync(async () =>
            {
                await Shell.Current.GoToAsync("//inbox");
                await Shell.Current.GoToAsync("onboarding");
            });
        }
    }
}
