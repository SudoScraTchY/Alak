using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core;
using Alak.Storage;
using Alak.Adaptive;
using System.Windows.Input;

namespace Alak.App.ViewModels;

public partial class DetailViewModel : ObservableObject
{
    private readonly INotificationRepository _repository;
    private readonly IAdaptiveEngine _adaptiveEngine;

    [ObservableProperty]
    private NotificationViewModel? _notification;

    [ObservableProperty]
    private bool _isLoading;

    public ICommand MarkReadCommand { get; }
    public ICommand CopyCodeCommand { get; }
    public ICommand ShareCommand { get; }
    public ICommand DeleteCommand { get; }

    public DetailViewModel(INotificationRepository repository, IAdaptiveEngine adaptiveEngine)
    {
        _repository = repository;
        _adaptiveEngine = adaptiveEngine;

        MarkReadCommand = new AsyncRelayCommand(MarkReadAsync);
        CopyCodeCommand = new AsyncRelayCommand(CopyCodeAsync);
        ShareCommand = new AsyncRelayCommand(ShareAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync);
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

    private async Task MarkReadAsync()
    {
        if (Notification == null) return;
        await _repository.MarkReadAsync(Notification.Id);
        Notification.IsRead = true;
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
            Title = "Share Notification"
        });
    }

    private async Task DeleteAsync()
    {
        if (Notification == null) return;
        await _repository.DeleteAsync(Notification.Id);
        await Shell.Current.GoToAsync("..");
    }
}