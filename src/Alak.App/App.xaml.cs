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

        // Follow the OS theme; Maui resolves AppThemeBindings from the platform.
        // (Forcing UserAppTheme here broke theme-bound colors on Android ≤ API 30 —
        //  content painted black.)

        // Shell-based navigation requires MainPage to be the AppShell
        MainPage = new AppShell();
    }
}
