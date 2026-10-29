using Alak.App.ViewModels;
using Microsoft.Maui.Controls;

namespace Alak.App.Views;

public partial class ModelsPage : ContentPage
{
    private readonly ModelsViewModel _viewModel;

    public ModelsPage(ModelsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadInstalledModels();
    }
}
