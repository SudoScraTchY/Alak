using Alak.App.ViewModels;
using Alak.App.Views;
using Microsoft.Maui.Controls;

namespace Alak.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register detail routes for navigation
        Routing.RegisterRoute("detail", typeof(DetailPage));
        Routing.RegisterRoute("onboarding", typeof(OnboardingPage));
        Routing.RegisterRoute("scan", typeof(ScanPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("rules", typeof(RulesPage));
        Routing.RegisterRoute("stub-detail", typeof(StubDetailPage));
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        // Handle navigation events if needed
    }
}