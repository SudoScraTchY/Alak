using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace Alak.App.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _cameraDenied;

    [ObservableProperty]
    private string _statusText = "Position QR code in frame";

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _hasResult;

    public Task CheckCameraPermissionAsync()
    {
        CameraDenied = !IsCameraGranted();
        if (CameraDenied)
            StatusText = "Camera permission needed";
        return Task.CompletedTask;
    }

    private static bool IsCameraGranted()
    {
        var status = Permissions.CheckStatusAsync<Permissions.Camera>().GetAwaiter().GetResult();
        return status == PermissionStatus.Granted;
    }

    [RelayCommand]
    private async Task RequestCameraAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.Camera>();
        CameraDenied = status != PermissionStatus.Granted;
        StatusText = CameraDenied ? "Camera permission denied" : "Position QR code in frame";
    }

    /// <summary>Called by ScanPage when ZXing detects a barcode. UI thread.</summary>
    public void OnBarcodeDetected(string value)
    {
        if (HasResult) return;
        ResultText = value;
        HasResult = true;
        StatusText = "QR code detected";
    }

    [RelayCommand]
    private async Task ProcessResultAsync()
    {
        if (string.IsNullOrEmpty(ResultText)) return;

        // TODO M9+: parse alak:// payload and import rule/source (spec §13.5)
        await Application.Current!.MainPage!.DisplayAlert(
            "Import", $"Importing:\n{ResultText}", "OK");
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}
