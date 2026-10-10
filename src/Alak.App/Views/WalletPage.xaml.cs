using Alak.App.ViewModels;

namespace Alak.App.Views;

public partial class WalletPage : ContentPage
{
    public WalletPage(WalletViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}