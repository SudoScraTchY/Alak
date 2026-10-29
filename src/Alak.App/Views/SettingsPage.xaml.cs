using Alak.App.ViewModels;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is SettingsViewModel vm)
        {
            vm.RefreshPermissions();
        }
    }
}