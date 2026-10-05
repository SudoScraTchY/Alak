using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.App.Design;

namespace Alak.App.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = "Position QR code in frame";

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _hasResult;

    [RelayCommand]
    private async Task StartScanAsync()
    {
        IsScanning = true;
        HasResult = false;
        StatusText = "Scanning...";

        try
        {
            // TODO: Integrate with ZXing or ML Kit for QR scanning
            // For now, simulate
            await Task.Delay(2000);
            
            // Simulate result
            ResultText = "alak://notification?pkg=com.example&title=Test&text=Hello%20World&cat=promo";
            HasResult = true;
            StatusText = "QR code detected";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    [RelayCommand]
    private async Task ProcessResultAsync()
    {
        if (string.IsNullOrEmpty(ResultText)) return;
        
        // Parse alak:// URI and create notification
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}