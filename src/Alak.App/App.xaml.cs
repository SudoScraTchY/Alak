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
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        window.Title = "Alak";
        
#if ANDROID
        // Android-specific window configuration
        window.MinimumWidth = 360;
        window.MinimumHeight = 640;
#endif

        return window;
    }
}