using Microsoft.Maui.Controls;
using Alak.App.ViewModels;

namespace Alak.App.Views;

public partial class SourcesPage : ContentPage
{
    private readonly SourcesViewModel _viewModel;

    public SourcesPage(SourcesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadSourcesCommand.ExecuteAsync(null);
    }
}