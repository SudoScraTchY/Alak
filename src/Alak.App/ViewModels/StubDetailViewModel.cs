using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core;
using Alak.Storage;
using System.Windows.Input;

namespace Alak.App.ViewModels;

public partial class StubDetailViewModel : ObservableObject
{
    private readonly INotificationRepository _repository;

    [ObservableProperty]
    private NotificationViewModel? _notification;

    [ObservableProperty]
    private bool _isLoading;

    public ICommand CopyCodeCommand { get; }
    public ICommand ShareCommand { get; }
    public ICommand DismissCommand { get; }

    public StubDetailViewModel(INotificationRepository repository)
    {
        _repository = repository;

        CopyCodeCommand = new AsyncRelayCommand(CopyCodeAsync);
        ShareCommand = new AsyncRelayCommand(ShareAsync);
        DismissCommand = new AsyncRelayCommand(DismissAsync);
    }

    public async Task LoadAsync(string id)
    {
        IsLoading = true;
        try
        {
            var notification = await _repository.GetByIdAsync(id);
            if (notification != null)
            {
                Notification = new NotificationViewModel(notification);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CopyCodeAsync()
    {
        if (Notification?.HasCode == true)
        {
            await Clipboard.SetTextAsync(Notification.ExtractedCode);
        }
    }

    private async Task ShareAsync()
    {
        if (Notification == null) return;
        await Share.RequestAsync(new ShareTextRequest
        {
            Text = $"{Notification.Title}\n\n{Notification.Text}",
            Title = "Share"
        });
    }

    private async Task DismissAsync()
    {
        if (Notification == null) return;
        await _repository.MarkReadAsync(Notification.Id);
        await Shell.Current.GoToAsync("..");
    }
}