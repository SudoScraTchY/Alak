using Alak.App.ViewModels;

namespace Alak.App.Views;

public partial class WalletPage : ContentPage
{
    public WalletPage(WalletViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is WalletViewModel vm)
        {
            await vm.LoadCodes();
        }
    }
}