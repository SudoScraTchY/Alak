using Alak.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class OnboardingPage : ContentPage
{
    public OnboardingPage() : this(AppServices.Current?.GetRequiredService<OnboardingViewModel>()!)
    {
    }

    public OnboardingPage(OnboardingViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is OnboardingViewModel vm)
        {
            vm.RefreshStatus();
        }
    }
}
