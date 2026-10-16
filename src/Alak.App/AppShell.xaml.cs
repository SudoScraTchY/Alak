using Alak.App.ViewModels;
using Alak.App.Views;
using Microsoft.Maui.Controls;

namespace Alak.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Register MODAL routes only — tab routes (inbox/scan/wallet/rules/settings)
        // are owned by the TabBar. Registering them again here (plus the stray
        // top-level ShellContents in XAML) caused route conflicts that collapsed
        // the tab bar to a single visible tab.
        Routing.RegisterRoute("detail", typeof(DetailPage));
        Routing.RegisterRoute("onboarding", typeof(OnboardingPage));
        Routing.RegisterRoute("stub-detail", typeof(StubDetailPage));
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        // Handle navigation events if needed
    }
}
