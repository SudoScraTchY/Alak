using Alak.App.ViewModels;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class InboxPage : ContentPage
{
    public InboxPage(InboxViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is InboxViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
