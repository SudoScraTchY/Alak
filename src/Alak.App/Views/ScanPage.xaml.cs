using Alak.App.ViewModels;
using Microsoft.Maui.Controls;
using ZXing.Net.Maui;

namespace Alak.App.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanViewModel _vm;

    public ScanPage(ScanViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.CheckCameraPermissionAsync();
    }

    private void OnBarcodeDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        // ZXing fires this repeatedly; stop once we have a result.
        if (_vm.HasResult) return;

        var result = e.Results?.FirstOrDefault();
        if (result?.Value is string value && !string.IsNullOrWhiteSpace(value))
        {
            _vm.OnBarcodeDetected(value);
        }
    }
}
