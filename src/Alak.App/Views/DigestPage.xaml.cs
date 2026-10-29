using Alak.App.ViewModels;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class DigestPage : ContentPage
{
    private readonly DigestViewModel _viewModel;

    public DigestPage(DigestViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.OnAppearing();
    }
}
