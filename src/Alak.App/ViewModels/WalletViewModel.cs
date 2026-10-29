using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core.Model;
using Alak.Storage;
using Alak.App.Services;
using System.Collections.ObjectModel;

namespace Alak.App.ViewModels;

/// <summary>
/// ViewModel for the Codes wallet screen (spec §10, §13.4).
/// Sections: Expiring soon (48h), Active, Used, Dismissed, Expired (collapsed).
/// Supports copy to clipboard, mark used, dismiss, reminders (WAL-006).
/// </summary>
public partial class WalletViewModel : ObservableObject
{
    private readonly IPromoCodeRepository _promoCodeRepo;
    private readonly IUserRulesRepository _userRulesRepo;
    private readonly INotificationRepository _notifRepo;
    private readonly Alak.App.Services.INotificationIngestionService _ingestionService;
    private bool _isLoaded;

    [ObservableProperty]
    private ObservableCollection<PromoCodeItem> _expiringSoonCodes = [];

    [ObservableProperty]
    private ObservableCollection<PromoCodeItem> _activeCodes = [];

    [ObservableProperty]
    private ObservableCollection<PromoCodeItem> _usedCodes = [];

    [ObservableProperty]
    private ObservableCollection<PromoCodeItem> _dismissedCodes = [];

    [ObservableProperty]
    private ObservableCollection<PromoCodeItem> _expiredCodes = [];

    [ObservableProperty]
    private bool _showExpiredSection = false;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _summaryText = "";

    public IRelayCommand<PromoCodeItem> CopyCodeCommand { get; }
    public IAsyncRelayCommand<PromoCodeItem> ViewDetailsCommand { get; }
    public IRelayCommand<PromoCodeItem> MarkUsedCommand { get; }
    public IRelayCommand<PromoCodeItem> DismissCommand { get; }
    public IRelayCommand<PromoCodeItem> UndoDismissCommand { get; }
    public IRelayCommand<PromoCodeItem> SetReminderCommand { get; }
    public IRelayCommand<PromoCodeItem> EditCodeCommand { get; }
    public IRelayCommand<PromoCodeItem> ToggleExpandedCommand { get; }
    public IRelayCommand<PromoCodeItem> ToggleBarcodeFormatCommand { get; }
    public IRelayCommand LoadCommand { get; }
    public IRelayCommand ToggleExpiredSectionCommand { get; }

    public WalletViewModel(
        IPromoCodeRepository promoCodeRepo,
        IUserRulesRepository userRulesRepo,
        INotificationRepository notifRepo,
        Alak.App.Services.INotificationIngestionService ingestionService)
    {
        _promoCodeRepo = promoCodeRepo;
        _userRulesRepo = userRulesRepo;
        _notifRepo = notifRepo;
        _ingestionService = ingestionService;
        _ingestionService.NotificationIngested += _ =>
        {
            MainThread.BeginInvokeOnMainThread(async () => await LoadCodes(force: true));
        };

        CopyCodeCommand = new RelayCommand<PromoCodeItem>(CopyCode);
        ViewDetailsCommand = new AsyncRelayCommand<PromoCodeItem>(ViewDetailsAsync);
        MarkUsedCommand = new RelayCommand<PromoCodeItem>(MarkUsed);
        DismissCommand = new RelayCommand<PromoCodeItem>(Dismiss);
        UndoDismissCommand = new RelayCommand<PromoCodeItem>(UndoDismiss);
        SetReminderCommand = new RelayCommand<PromoCodeItem>(SetReminder);
        EditCodeCommand = new RelayCommand<PromoCodeItem>(EditCode);
        ToggleExpandedCommand = new RelayCommand<PromoCodeItem>(item => { if (item != null) item.IsExpanded = !item.IsExpanded; });
        ToggleBarcodeFormatCommand = new RelayCommand<PromoCodeItem>(item => item?.ToggleBarcodeType());
        LoadCommand = new AsyncRelayCommand(() => LoadCodes(force: true));
        ToggleExpiredSectionCommand = new RelayCommand(() => ShowExpiredSection = !ShowExpiredSection);
    }

    public async Task LoadCodes(bool force = false)
    {
        if (_isLoaded && !force) return;
        IsLoading = true;
        try
        {
            var codes = await _promoCodeRepo.GetAllAsync();

            ExpiringSoonCodes.Clear();
            ActiveCodes.Clear();
            UsedCodes.Clear();
            DismissedCodes.Clear();
            ExpiredCodes.Clear();

            int activeCount = 0;
            PromoCodeItem? soonestExpiring = null;

            foreach (var code in codes)
            {
                var item = new PromoCodeItem(code);

                switch (code.Status)
                {
                    case PromoCodeStatus.ExpiringSoon:
                        ExpiringSoonCodes.Add(item);
                        activeCount++;
                        if (soonestExpiring == null || code.ValidUntil < soonestExpiring.Code.ValidUntil)
                            soonestExpiring = item;
                        break;
                    case PromoCodeStatus.Active:
                        ActiveCodes.Add(item);
                        activeCount++;
                        if (soonestExpiring == null || (code.ValidUntil.HasValue && code.ValidUntil < soonestExpiring.Code.ValidUntil))
                            soonestExpiring = item;
                        break;
                    case PromoCodeStatus.Used:
                        UsedCodes.Add(item);
                        break;
                    case PromoCodeStatus.Dismissed:
                        DismissedCodes.Add(item);
                        break;
                    case PromoCodeStatus.Expired:
                        ExpiredCodes.Add(item);
                        break;
                    case PromoCodeStatus.Maybe:
                        ActiveCodes.Add(item); // Show maybe codes in active
                        activeCount++;
                        break;
                }
            }

            // Update summary text per WAL-003
            var soonestText = soonestExpiring?.Code.ValidUntil.HasValue == true
                ? $" · 1 expires in {GetTimeUntil(soonestExpiring.Code.ValidUntil.Value)}"
                : "";
            SummaryText = $"{activeCount} active code{(activeCount != 1 ? "s" : "")}{soonestText}";

            OnPropertyChanged(nameof(ExpiringSoonCodes));
            OnPropertyChanged(nameof(ActiveCodes));
            OnPropertyChanged(nameof(UsedCodes));
            OnPropertyChanged(nameof(DismissedCodes));
            OnPropertyChanged(nameof(ExpiredCodes));
            OnPropertyChanged(nameof(SummaryText));
            _isLoaded = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string GetTimeUntil(DateTimeOffset until)
    {
        var diff = until - DateTimeOffset.UtcNow;
        if (diff.TotalHours < 1)
            return $"{(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} h";
        return $"{(int)diff.TotalDays} d";
    }

    private async void CopyCode(PromoCodeItem? item)
    {
        if (item == null) return;

        await Clipboard.SetTextAsync(item.Code.RawCode);
        await Shell.Current.DisplayAlertAsync("Copied", $"Code \"{item.Code.RawCode}\" copied to clipboard", "OK");
    }

    private async Task ViewDetailsAsync(PromoCodeItem? item)
    {
        if (item == null) return;
        try
        {
            Notification? notif = null;
            if (!string.IsNullOrEmpty(item.Code.NotificationId))
            {
                notif = await _notifRepo.GetByIdAsync(item.Code.NotificationId);
            }

            if (notif == null)
            {
                var all = await _notifRepo.GetAllAsync();
                notif = all.FirstOrDefault(n =>
                    (!string.IsNullOrEmpty(item.Code.RawCode) && string.Equals(n.ExtractedCode, item.Code.RawCode, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(item.Code.CodeNorm) && string.Equals(n.ExtractedCode, item.Code.CodeNorm, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(item.Code.RawCode) && (n.Text.Contains(item.Code.RawCode, StringComparison.OrdinalIgnoreCase) || n.Title.Contains(item.Code.RawCode, StringComparison.OrdinalIgnoreCase))));
            }

            if (notif == null)
            {
                var fallbackId = string.IsNullOrEmpty(item.Code.NotificationId)
                    ? $"promo_{item.Code.Id}"
                    : item.Code.NotificationId;
                var notifTitle = !string.IsNullOrEmpty(item.SourceDisplay) ? item.SourceDisplay : item.Code.Package;
                var notifText = $"{item.DiscountText}\nCode: {item.Code.RawCode}";
                var timestamp = item.Code.FirstSeenAt > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(item.Code.FirstSeenAt)
                    : DateTimeOffset.UtcNow;
                notif = new Notification(
                    Id: fallbackId,
                    Package: item.Code.Package,
                    ChannelName: "Promo Wallet",
                    Title: notifTitle,
                    Text: notifText,
                    Category: "promo",
                    Timestamp: timestamp,
                    IsRead: true,
                    IsPersian: false,
                    ExtractedCode: item.Code.RawCode
                );
                await _notifRepo.AddAsync(notif);
            }

            await Shell.Current.GoToAsync($"detail?id={Uri.EscapeDataString(notif.Id)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Wallet] ViewDetails error: {ex.Message}");
        }
    }

    private async void MarkUsed(PromoCodeItem? item)
    {
        if (item == null) return;

        item.Code = item.Code with { Status = PromoCodeStatus.Used };
        await _promoCodeRepo.UpdateAsync(item.Code);
        await LoadCodes();
    }

    private async void Dismiss(PromoCodeItem? item)
    {
        if (item == null) return;

        item.Code = item.Code with { Status = PromoCodeStatus.Dismissed };
        await _promoCodeRepo.UpdateAsync(item.Code);
        await LoadCodes();
    }

    private async void UndoDismiss(PromoCodeItem? item)
    {
        if (item == null) return;

        item.Code = item.Code with { Status = PromoCodeStatus.Active };
        await _promoCodeRepo.UpdateAsync(item.Code);
        await LoadCodes();
    }

    private async void SetReminder(PromoCodeItem? item)
            {
                if (item == null || !item.Code.HasValidUntil) return;

                var options = new[] { "24 hours before", "On expiry morning (09:00)", "Custom..." };
                var choice = await Shell.Current.DisplayActionSheetAsync("Set reminder", "Cancel", null, options);

                DateTimeOffset reminderTime = DateTimeOffset.MinValue;
                string? reminderField = null;

                switch (choice)
                {
                    case "24 hours before":
                        reminderTime = item.Code.ValidUntil!.Value.AddHours(-24);
                        reminderField = "reminder_24h_at";
                        break;
                    case "On expiry morning (09:00)":
                        var expiryDate = item.Code.ValidUntil!.Value.DateTime;
                        reminderTime = new DateTimeOffset(expiryDate.Year, expiryDate.Month, expiryDate.Day, 9, 0, 0, TimeSpan.Zero);
                        reminderField = "reminder_morning_at";
                        break;
                    case "Custom...":
                        // open time picker
                        await Shell.Current.DisplayAlertAsync("Custom Reminder", "Custom reminder picker open here", "OK");
                        return;
                }

                if (reminderTime > DateTimeOffset.UtcNow && !string.IsNullOrEmpty(reminderField))
                {
                    // Update specific reminder field
                    var code = item.Code;
                    if (reminderField == "reminder_24h_at")
                        code = code with { Reminder24hAt = reminderTime.ToUnixTimeMilliseconds() };
                    else if (reminderField == "reminder_morning_at")
                        code = code with { ReminderMorningAt = reminderTime.ToUnixTimeMilliseconds() };
                    else if (reminderField == "reminder_custom_at")
                        code = code with { ReminderCustomAt = reminderTime.ToUnixTimeMilliseconds() };

                    await _promoCodeRepo.UpdateAsync(code);
                    await LoadCodes();
                    await Shell.Current.DisplayAlertAsync("Reminder Set", $"Reminder set {reminderTime:g}", "OK");
                }
            }

    private async void EditCode(PromoCodeItem? item)
    {
        if (item == null) return;
        // Would navigate to edit page
        await Shell.Current.DisplayAlertAsync("Edit Code", "Edit code page would open here", "OK");
    }
}

/// <summary>
/// UI wrapper for PromoCode with additional display properties.
/// </summary>
public partial class PromoCodeItem : ObservableObject
{
    [ObservableProperty]
    private PromoCode _code;

    public PromoCodeItem(PromoCode code)
    {
        _code = code;
    }

    public string StatusText => Code.Status switch
    {
        PromoCodeStatus.ExpiringSoon => "Expiring soon",
        PromoCodeStatus.Active => "Active",
        PromoCodeStatus.Used => "Used",
        PromoCodeStatus.Dismissed => "Dismissed",
        PromoCodeStatus.Expired => "Expired",
        PromoCodeStatus.Maybe => "Maybe a code?",
        _ => ""
    };

    public string ValidUntilText => Code.ValidUntil.HasValue
        ? $"Expires {Code.ValidUntil.Value:MMM d, h:mm tt}"
        : "No expiry";

    public string KindText => Code.Kind switch
    {
        PromoCodeKind.Promo => "Promo",
        PromoCodeKind.Referral => "Referral",
        PromoCodeKind.Gift => "Gift",
        PromoCodeKind.Voucher => "Voucher",
        _ => "Code"
    };

    public string DiscountText
    {
        get
        {
            var parts = new List<string>();
            if (Code.DiscountValue.HasValue && Code.DiscountType.HasValue)
            {
                var discountStr = Code.DiscountType.Value switch
                {
                    DiscountType.Percent => $"{Code.DiscountValue.Value}% off",
                    DiscountType.Amount => Code.Currency == "IRT"
                        ? $"{Code.DiscountValue.Value:N0} تومان"
                        : $"{Code.Currency ?? ""} {Code.DiscountValue.Value:N0}".Trim(),
                    DiscountType.FreeShipping => "Free shipping",
                    DiscountType.Bogo => "Buy one get one",
                    DiscountType.Cashback => Code.Currency == "IRT"
                        ? $"Cashback {Code.DiscountValue.Value:N0} تومان"
                        : $"Cashback {Code.Currency ?? ""} {Code.DiscountValue.Value:N0}".Trim(),
                    DiscountType.Credit => Code.Currency == "IRT"
                        ? $"Credit {Code.DiscountValue.Value:N0} تومان"
                        : $"Credit {Code.Currency ?? ""} {Code.DiscountValue.Value:N0}".Trim(),
                    _ => ""
                };
                if (!string.IsNullOrEmpty(discountStr))
                    parts.Add(discountStr);
            }
            if (Code.MinSpend.HasValue && Code.MinSpend.Value > 0)
            {
                if (Code.Currency == "IRT")
                    parts.Add($"کف خرید {Code.MinSpend.Value:N0} تومان");
                else
                    parts.Add($"Min spend {Code.Currency ?? ""} {Code.MinSpend.Value:N0}".Trim());
            }
            if (!string.IsNullOrEmpty(Code.StoreHint))
            {
                parts.Add(Code.StoreHint);
            }
            return parts.Count > 0 ? string.Join(" · ", parts) : "";
        }
    }

    public string SourceDisplay => Code.SourceDisplay;
    public bool HasReminder => Code.Reminder24hAt.HasValue || Code.ReminderMorningAt.HasValue || Code.ReminderCustomAt.HasValue;
    public string ReminderText
    {
        get
        {
            var parts = new List<string>();
            if (Code.Reminder24hAt.HasValue)
                parts.Add("24h before");
            if (Code.ReminderMorningAt.HasValue)
                parts.Add("Morning of expiry");
            if (Code.ReminderCustomAt.HasValue)
                parts.Add("Custom");
            return parts.Count > 0 ? $"Reminders: {string.Join(", ", parts)}" : "";
        }
    }

    public bool IsExpiringSoon => Code.Status == PromoCodeStatus.ExpiringSoon;
    public bool IsActive => Code.Status == PromoCodeStatus.Active;
    public bool IsUsed => Code.Status == PromoCodeStatus.Used;
    public bool IsDismissed => Code.Status == PromoCodeStatus.Dismissed;
    public bool IsExpired => Code.Status == PromoCodeStatus.Expired;
    public bool IsMaybe => Code.Status == PromoCodeStatus.Maybe;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private Alak.App.Controls.BarcodeType _barcodeType = Alak.App.Controls.BarcodeType.Code128;

    [RelayCommand]
    public void ToggleExpanded() => IsExpanded = !IsExpanded;

    [RelayCommand]
    public void ToggleBarcodeType()
    {
        BarcodeType = BarcodeType == Alak.App.Controls.BarcodeType.Code128
            ? Alak.App.Controls.BarcodeType.QrCode
            : Alak.App.Controls.BarcodeType.Code128;
    }

    public double DrainFraction
    {
        get
        {
            if (!Code.ValidUntil.HasValue) return 1.0;
            var now = DateTimeOffset.UtcNow;
            var end = Code.ValidUntil.Value;
            if (now >= end) return 0.0;
            var start = Code.ValidFrom ?? (Code.FirstSeenAt > 0 ? DateTimeOffset.FromUnixTimeMilliseconds(Code.FirstSeenAt) : end.AddDays(-7));
            var totalMs = (end - start).TotalMilliseconds;
            if (totalMs <= 0) return 0.0;
            var remMs = (end - now).TotalMilliseconds;
            return Math.Clamp(remMs / totalMs, 0.0, 1.0);
        }
    }

    public Color ExpiryBarColor
    {
        get
        {
            if (!Code.ValidUntil.HasValue) return Color.FromArgb("#B77900");
            if (Code.ValidUntil.Value <= DateTimeOffset.UtcNow.AddHours(6))
            {
                return Color.FromArgb("#DC2626"); // SPEC §12.7 error/danger in final 6 hours
            }
            return Color.FromArgb("#B77900"); // Standard gold wallet accent
        }
    }
}
