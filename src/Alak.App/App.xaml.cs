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

        // Apply theme (light by default; user preference from Settings in M6)
        UserAppTheme = AppTheme.Light;

        // Shell-based navigation requires MainPage to be the AppShell
        MainPage = new AppShell();
    }
}
