using Alak.App.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class InboxPage : ContentPage
{
    public InboxPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        if (BindingContext is InboxViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        await Shell.Current.GoToAsync("scan");
    }

    [RelayCommand]
    private async Task SettingsAsync()
    {
        await Shell.Current.GoToAsync("settings");
    }
}