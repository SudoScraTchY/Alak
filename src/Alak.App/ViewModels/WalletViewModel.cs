using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Alak.Core.Model;
using Alak.Storage;
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
    public IRelayCommand<PromoCodeItem> MarkUsedCommand { get; }
    public IRelayCommand<PromoCodeItem> DismissCommand { get; }
    public IRelayCommand<PromoCodeItem> UndoDismissCommand { get; }
    public IRelayCommand<PromoCodeItem> SetReminderCommand { get; }
    public IRelayCommand<PromoCodeItem> EditCodeCommand { get; }
    public IRelayCommand LoadCommand { get; }
    public IRelayCommand ToggleExpiredSectionCommand { get; }

    public WalletViewModel(IPromoCodeRepository promoCodeRepo, IUserRulesRepository userRulesRepo)
    {
        _promoCodeRepo = promoCodeRepo;
        _userRulesRepo = userRulesRepo;

        CopyCodeCommand = new RelayCommand<PromoCodeItem>(CopyCode);
        MarkUsedCommand = new RelayCommand<PromoCodeItem>(MarkUsed);
        DismissCommand = new RelayCommand<PromoCodeItem>(Dismiss);
        UndoDismissCommand = new RelayCommand<PromoCodeItem>(UndoDismiss);
        SetReminderCommand = new RelayCommand<PromoCodeItem>(SetReminder);
        EditCodeCommand = new RelayCommand<PromoCodeItem>(EditCode);
        LoadCommand = new AsyncRelayCommand(LoadCodes);
        ToggleExpiredSectionCommand = new RelayCommand(() => ShowExpiredSection = !ShowExpiredSection);
    }

    public async Task LoadCodes()
    {
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
            if (Code.DiscountValue.HasValue && Code.DiscountType.HasValue)
            {
                return Code.DiscountType.Value switch
                {
                    DiscountType.Percent => $"{Code.DiscountValue.Value}% off",
                    DiscountType.Amount => $"{Code.Currency ?? ""}{Code.DiscountValue.Value}",
                    DiscountType.FreeShipping => "Free shipping",
                    DiscountType.Bogo => "Buy one get one",
                    DiscountType.Cashback => $"Cashback {Code.Currency ?? ""}{Code.DiscountValue.Value}",
                    DiscountType.Credit => $"Credit {Code.Currency ?? ""}{Code.DiscountValue.Value}",
                    _ => ""
                };
            }
            return "";
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
}
