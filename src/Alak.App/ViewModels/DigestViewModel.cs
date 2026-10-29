using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Alak.App.Design;
using Alak.App.Services;
using Alak.Core.Model;
using Alak.Inference.Abstractions.Tasks;
using Alak.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;

namespace Alak.App.ViewModels;

public sealed class DigestInsightCard : ObservableObject
{
    public string Title { get; }
    public string Summary { get; }
    public string Icon { get; }
    public Color BadgeColor { get; }
    public Color TintColor { get; }

    public DigestInsightCard(string title, string summary, string icon, Color badgeColor, Color tintColor)
    {
        Title = title;
        Summary = summary;
        Icon = icon;
        BadgeColor = badgeColor;
        TintColor = tintColor;
    }
}

public sealed class DigestSectionItem : ObservableObject
{
    public string Category { get; }
    public string CategoryLabel { get; }
    public Color CategoryMarkColor { get; }
    public Color CategoryTintColor { get; }
    public ObservableCollection<string> Bullets { get; }

    public ICommand CopySectionCommand { get; }

    public DigestSectionItem(string category, string categoryLabel, IEnumerable<string> bullets, Color markColor, Color tintColor)
    {
        Category = category;
        CategoryLabel = categoryLabel;
        Bullets = new ObservableCollection<string>(bullets);
        CategoryMarkColor = markColor;
        CategoryTintColor = tintColor;

        CopySectionCommand = new AsyncRelayCommand(async () =>
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{CategoryLabel}]");
            foreach (var b in Bullets)
            {
                sb.AppendLine($"• {b}");
            }
            await Clipboard.SetTextAsync(sb.ToString().TrimEnd());
            var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
            if (page != null)
            {
                await page.DisplayAlertAsync("Alak", LocalizationManager.Instance["Digest_Copied"], LocalizationManager.Instance["Common_OK"]);
            }
        });
    }
}

public partial class DigestViewModel : ObservableObject
{
    private readonly INotificationRepository _repository;
    private readonly IModelService _modelService;
    private CancellationTokenSource? _generationCts;

    [ObservableProperty]
    private bool _isAiMode = true;

    partial void OnIsAiModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsRulesMode));
    }

    public bool IsRulesMode => !IsAiMode;

    [ObservableProperty]
    private string _selectedPeriod = "today"; // "today", "yesterday", "week"

    partial void OnSelectedPeriodChanged(string value)
    {
        OnPropertyChanged(nameof(IsPeriodToday));
        OnPropertyChanged(nameof(IsPeriodYesterday));
        OnPropertyChanged(nameof(IsPeriodWeek));
    }

    public bool IsPeriodToday => SelectedPeriod == "today";
    public bool IsPeriodYesterday => SelectedPeriod == "yesterday";
    public bool IsPeriodWeek => SelectedPeriod == "week";

    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private string _streamingText = string.Empty;

    [ObservableProperty]
    private string _headline = string.Empty;

    [ObservableProperty]
    private int _notificationCount;

    partial void OnNotificationCountChanged(int value)
    {
        OnPropertyChanged(nameof(NotificationCountSummary));
    }

    public string NotificationCountSummary
    {
        get
        {
            var loc = LocalizationManager.Instance;
            return loc.CurrentLanguage == "fa"
                ? $"{NotificationCount} اعلان بررسی شد"
                : $"{NotificationCount} notifications analyzed";
        }
    }

    [ObservableProperty]
    private bool _hasNotifications;

    [ObservableProperty]
    private bool _isModelInstalled;

    [ObservableProperty]
    private bool _hasFollowUps;

    [ObservableProperty]
    private bool _hasInsightCards;

    public ObservableCollection<DigestSectionItem> Sections { get; } = new();
    public ObservableCollection<string> FollowUps { get; } = new();
    public ObservableCollection<DigestInsightCard> InsightCards { get; } = new();

    public ICommand SelectRulesModeCommand { get; }
    public ICommand SelectAiModeCommand { get; }
    public ICommand SelectPeriodCommand { get; }
    public ICommand GenerateDigestCommand { get; }
    public ICommand CancelGenerationCommand { get; }
    public ICommand OpenModelsCommand { get; }
    public ICommand CopyAllCommand { get; }

    public DigestViewModel(INotificationRepository repository, IModelService modelService)
    {
        _repository = repository;
        _modelService = modelService;

        SelectRulesModeCommand = new RelayCommand(() =>
        {
            if (IsAiMode)
            {
                CancelGeneration();
                IsAiMode = false;
                _ = RefreshDigestAsync();
            }
        });

        SelectAiModeCommand = new RelayCommand(() =>
        {
            if (!IsAiMode)
            {
                IsAiMode = true;
                UpdateModelStatus();
                _ = RefreshDigestAsync();
            }
        });

        SelectPeriodCommand = new RelayCommand<string>(period =>
        {
            if (!string.IsNullOrEmpty(period) && SelectedPeriod != period)
            {
                CancelGeneration();
                SelectedPeriod = period;
                _ = RefreshDigestAsync();
            }
        });

        GenerateDigestCommand = new AsyncRelayCommand(GenerateAiDigestAsync);
        CancelGenerationCommand = new RelayCommand(CancelGeneration);
        OpenModelsCommand = new AsyncRelayCommand(async () =>
        {
            await Shell.Current.GoToAsync("models");
        });

        CopyAllCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrEmpty(Headline) && Sections.Count == 0) return;
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Headline))
            {
                sb.AppendLine(Headline);
                sb.AppendLine();
            }
            if (FollowUps.Count > 0)
            {
                var loc = LocalizationManager.Instance;
                sb.AppendLine($"--- {loc["Digest_ActionItems"]} ---");
                foreach (var f in FollowUps)
                {
                    sb.AppendLine($"✓ {f}");
                }
                sb.AppendLine();
            }
            foreach (var s in Sections)
            {
                sb.AppendLine($"--- {s.CategoryLabel} ---");
                foreach (var b in s.Bullets)
                {
                    sb.AppendLine($"• {b}");
                }
                sb.AppendLine();
            }
            await Clipboard.SetTextAsync(sb.ToString().TrimEnd());
            var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
            if (page != null)
            {
                await page.DisplayAlertAsync("Alak", LocalizationManager.Instance["Digest_Copied"], LocalizationManager.Instance["Common_OK"]);
            }
        });

        LocalizationManager.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SelectedPeriod));
            OnPropertyChanged(nameof(IsPeriodToday));
            OnPropertyChanged(nameof(IsPeriodYesterday));
            OnPropertyChanged(nameof(IsPeriodWeek));
            OnPropertyChanged(nameof(IsRulesMode));
            OnPropertyChanged(nameof(NotificationCountSummary));
            UpdateModelStatus();
            _ = RefreshDigestAsync();
        };

        UpdateModelStatus();
    }

    public void OnAppearing()
    {
        UpdateModelStatus();
        _ = RefreshDigestAsync();
    }

    private void UpdateModelStatus()
    {
        IsModelInstalled = _modelService.IsModelDownloaded;
    }

    public async Task RefreshDigestAsync()
    {
        var notifs = await LoadPeriodNotificationsAsync();
        NotificationCount = notifs.Count;
        HasNotifications = notifs.Count > 0;

        if (!IsAiMode)
        {
            GenerateRulesDigest(notifs);
        }
        else
        {
            // Instant Smart AI Synthesis (Always provides rich intelligence)
            GenerateSmartSynthesis(notifs);
        }
    }

    private async Task<List<Notification>> LoadPeriodNotificationsAsync()
    {
        var all = await _repository.GetAllAsync();
        var now = DateTimeOffset.Now;
        var todayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);
        var yesterdayStart = todayStart.AddDays(-1);
        var weekStart = todayStart.AddDays(-7);

        return SelectedPeriod switch
        {
            "yesterday" => all.Where(n => n.Timestamp >= yesterdayStart && n.Timestamp < todayStart).ToList(),
            "week" => all.Where(n => n.Timestamp >= weekStart).ToList(),
            _ => all.Where(n => n.Timestamp >= todayStart).ToList(), // today
        };
    }

    private void GenerateRulesDigest(List<Notification> notifs)
    {
        Sections.Clear();
        FollowUps.Clear();
        InsightCards.Clear();
        HasFollowUps = false;
        HasInsightCards = false;

        var total = notifs.Count;
        var groups = notifs.GroupBy(n => n.Category ?? "other").ToList();
        var loc = LocalizationManager.Instance;
        var isFa = loc.CurrentLanguage == "fa";

        string periodText = SelectedPeriod switch
        {
            "yesterday" => loc["Digest_PeriodYesterday"],
            "week" => loc["Digest_Period7Days"],
            _ => loc["Digest_PeriodToday"]
        };

        if (total == 0)
        {
            Headline = loc["Digest_NoNotifications"];
        }
        else if (isFa)
        {
            Headline = $"{total} اعلان مربوط به {periodText} در {groups.Count} دسته خلاصه‌سازی شد.";
        }
        else
        {
            Headline = total == 1
                ? $"1 notification from {periodText.ToLowerInvariant()} summarized."
                : $"{total} notifications from {periodText.ToLowerInvariant()} organized across {groups.Count} categories.";
        }

        foreach (var g in groups)
        {
            var bullets = g.Take(4).Select(n => $"{n.Title}: {n.Text}").ToArray();
            var markColor = GetCategoryMarkColor(g.Key);
            var tintColor = GetCategoryTintColor(g.Key);
            string locLabel = GetCategoryLabel(g.Key);

            Sections.Add(new DigestSectionItem(g.Key, locLabel, bullets, markColor, tintColor));
        }
    }

    private static readonly System.Text.RegularExpressions.Regex AmountPattern = new(
        @"((?:\$|€|£)\s*[\d,]+(?:\.\d+)?|[\d,]+(?:\.\d+)?\s*(?:ریال|تومان|تومن|ت|IRR|USD|EUR))",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex PercentPattern = new(
        @"(\d{1,2}\s*٪|\d{1,2}%\s*(?:off|تخفیف)?)",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string? ExtractAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = AmountPattern.Match(text);
        return m.Success ? m.Value.Trim() : null;
    }

    private static string? ExtractDiscount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var m = PercentPattern.Match(text);
        return m.Success ? m.Value.Trim() : null;
    }

    private void PopulateInsightCards(List<Notification> notifs, bool isFa, LocalizationManager loc)
    {
        InsightCards.Clear();

        var moneyNotifs = notifs.Where(n => string.Equals(n.Category, "money", StringComparison.OrdinalIgnoreCase)).ToList();
        var ordersNotifs = notifs.Where(n => string.Equals(n.Category, "orders", StringComparison.OrdinalIgnoreCase)).ToList();
        var promoNotifs = notifs.Where(n => string.Equals(n.Category, "promo", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(n.Category, "offers", StringComparison.OrdinalIgnoreCase) ||
                                            !string.IsNullOrEmpty(n.ExtractedCode)).ToList();
        var securityNotifs = notifs.Where(n => string.Equals(n.Category, "security", StringComparison.OrdinalIgnoreCase)).ToList();

        if (moneyNotifs.Count > 0)
        {
            var firstAmount = moneyNotifs.Select(m => ExtractAmount(m.Text) ?? ExtractAmount(m.Title)).FirstOrDefault(a => a != null);
            var hasDeposit = moneyNotifs.Any(m => m.Text.Contains("واریز", StringComparison.OrdinalIgnoreCase) || m.Title.Contains("واریز", StringComparison.OrdinalIgnoreCase) || m.Text.Contains("deposit", StringComparison.OrdinalIgnoreCase));
            string summary;
            if (firstAmount != null)
            {
                if (hasDeposit)
                    summary = isFa ? $"واریز {firstAmount} {(moneyNotifs.Count > 1 ? $"(+{moneyNotifs.Count - 1} تراکنش دیگر)" : "")}" : $"Deposit of {firstAmount} {(moneyNotifs.Count > 1 ? $"(+{moneyNotifs.Count - 1} more)" : "")}";
                else
                    summary = isFa ? $"{firstAmount} تراکنش بانکی ثبت شده" : $"{firstAmount} transaction recorded";
            }
            else
            {
                summary = isFa ? $"{moneyNotifs.Count} رویداد مالی و بانکی ثبت شده" : $"{moneyNotifs.Count} financial & banking events";
            }

            InsightCards.Add(new DigestInsightCard(
                loc["Digest_FinancialActivity"],
                summary,
                "💳",
                Color.FromArgb(DesignTokens.CategoryMark.Money),
                Color.FromArgb(DesignTokens.Light.MoneyTint)));
        }

        if (ordersNotifs.Count > 0)
        {
            var firstOrder = ordersNotifs.First();
            var isDelivery = ordersNotifs.Any(o => o.Text.Contains("تحویل", StringComparison.OrdinalIgnoreCase) || o.Text.Contains("ارسال", StringComparison.OrdinalIgnoreCase) || o.Text.Contains("delivery", StringComparison.OrdinalIgnoreCase) || o.Text.Contains("shipped", StringComparison.OrdinalIgnoreCase));
            var vendor = !string.IsNullOrEmpty(firstOrder.Title) ? firstOrder.Title : firstOrder.Package;
            string summary;
            if (isDelivery)
                summary = isFa ? $"مرسوله {vendor} در مسیر ارسال/تحویل" : $"{vendor} package out for delivery";
            else
                summary = isFa ? $"{ordersNotifs.Count} به‌روزرسانی سفارش از {vendor}" : $"{ordersNotifs.Count} shipment updates from {vendor}";

            InsightCards.Add(new DigestInsightCard(
                loc["Digest_OrdersDeliveries"],
                summary,
                "📦",
                Color.FromArgb(DesignTokens.CategoryMark.Orders),
                Color.FromArgb(DesignTokens.Light.OrdersTint)));
        }

        if (promoNotifs.Count > 0)
        {
            var codeItem = promoNotifs.FirstOrDefault(p => !string.IsNullOrEmpty(p.ExtractedCode));
            var disc = promoNotifs.Select(p => ExtractDiscount(p.Text) ?? ExtractDiscount(p.Title)).FirstOrDefault(d => d != null);
            string summary;
            if (codeItem != null && disc != null)
                summary = isFa ? $"کد «{codeItem.ExtractedCode}» ({disc}) در کیف پول" : $"Code \"{codeItem.ExtractedCode}\" ({disc}) in wallet";
            else if (codeItem != null)
                summary = isFa ? $"کد «{codeItem.ExtractedCode}» ذخیره در کیف پول" : $"Code \"{codeItem.ExtractedCode}\" saved in wallet";
            else if (disc != null)
                summary = isFa ? $"پیشنهاد تخفیف {disc} فعال" : $"{disc} discount deal active";
            else
                summary = isFa ? $"{promoNotifs.Count} پیشنهاد تخفیف و فروش ویژه" : $"{promoNotifs.Count} promotional deals active";

            InsightCards.Add(new DigestInsightCard(
                loc["Digest_PromoDeals"],
                summary,
                "🏷️",
                Color.FromArgb(DesignTokens.CategoryMark.Promo),
                Color.FromArgb(DesignTokens.Light.PromoTint)));
        }

        if (securityNotifs.Count > 0)
        {
            var summary = isFa
                ? $"{securityNotifs.Count} اعلان رمز یکبار مصرف (محفوظ و ایمن)"
                : $"{securityNotifs.Count} OTP / 2FA code (protected on-device)";
            InsightCards.Add(new DigestInsightCard(
                loc["Digest_SecurityAlerts"],
                summary,
                "🛡️",
                Color.FromArgb(DesignTokens.CategoryMark.Security),
                Color.FromArgb(DesignTokens.Light.SecurityTint)));
        }

        HasInsightCards = InsightCards.Count > 0;
    }

    private void GenerateSmartSynthesis(List<Notification> notifs)
    {
        Sections.Clear();
        FollowUps.Clear();
        InsightCards.Clear();

        var total = notifs.Count;
        var loc = LocalizationManager.Instance;
        var isFa = loc.CurrentLanguage == "fa";

        string periodText = SelectedPeriod switch
        {
            "yesterday" => loc["Digest_PeriodYesterday"],
            "week" => loc["Digest_Period7Days"],
            _ => loc["Digest_PeriodToday"]
        };

        if (total == 0)
        {
            Headline = loc["Digest_NoNotifications"];
            HasFollowUps = false;
            HasInsightCards = false;
            return;
        }

        // 1. Generate Intelligent Insight Cards
        PopulateInsightCards(notifs, isFa, loc);

        // 2. Semantic grouping & counts
        var moneyNotifs = notifs.Where(n => string.Equals(n.Category, "money", StringComparison.OrdinalIgnoreCase)).ToList();
        var ordersNotifs = notifs.Where(n => string.Equals(n.Category, "orders", StringComparison.OrdinalIgnoreCase)).ToList();
        var promoNotifs = notifs.Where(n => string.Equals(n.Category, "promo", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(n.Category, "offers", StringComparison.OrdinalIgnoreCase) ||
                                            !string.IsNullOrEmpty(n.ExtractedCode)).ToList();
        var securityNotifs = notifs.Where(n => string.Equals(n.Category, "security", StringComparison.OrdinalIgnoreCase)).ToList();

        // 3. Intelligent Executive Headline with synthesized highlights
        var highlights = new List<string>();
        if (ordersNotifs.Count > 0)
        {
            var o = ordersNotifs.First();
            var vendor = !string.IsNullOrEmpty(o.Title) ? o.Title : o.Package;
            highlights.Add(isFa ? $"سفارش {vendor}" : $"{vendor} delivery");
        }
        if (moneyNotifs.Count > 0)
        {
            var amt = moneyNotifs.Select(m => ExtractAmount(m.Text) ?? ExtractAmount(m.Title)).FirstOrDefault(a => a != null);
            if (amt != null)
                highlights.Add(isFa ? $"واریز/تراکنش {amt}" : $"transaction {amt}");
            else
                highlights.Add(isFa ? $"{moneyNotifs.Count} تراکنش مالی" : $"{moneyNotifs.Count} financial event{(moneyNotifs.Count > 1 ? "s" : "")}");
        }
        if (promoNotifs.Count > 0)
        {
            var code = promoNotifs.FirstOrDefault(p => !string.IsNullOrEmpty(p.ExtractedCode))?.ExtractedCode;
            if (code != null)
                highlights.Add(isFa ? $"کد تخفیف {code}" : $"code \"{code}\"");
            else
                highlights.Add(isFa ? $"{promoNotifs.Count} تخفیف" : $"{promoNotifs.Count} promo deal{(promoNotifs.Count > 1 ? "s" : "")}");
        }

        if (highlights.Count > 0)
        {
            Headline = isFa
                ? $"خلاصه هوشمند {periodText}: {string.Join("، ", highlights)}."
                : $"AI Summary for {periodText}: {string.Join(", ", highlights)}.";
        }
        else
        {
            Headline = isFa
                ? $"{total} اعلان مربوط به {periodText} بررسی و تحلیل هوشمند شد."
                : $"{total} notification{(total > 1 ? "s" : "")} from {periodText.ToLowerInvariant()} synthesized.";
        }

        // 4. Action Items & Follow-ups
        foreach (var p in promoNotifs.Where(n => !string.IsNullOrEmpty(n.ExtractedCode)).Take(2))
        {
            var disc = ExtractDiscount(p.Text) ?? ExtractDiscount(p.Title);
            var discStr = disc != null ? $" ({disc})" : "";
            FollowUps.Add(isFa
                ? $"کد تخفیف «{p.ExtractedCode}»{discStr} از {p.Title ?? p.Package} آماده استفاده در کیف پول است."
                : $"Promo code \"{p.ExtractedCode}\"{discStr} from {p.Title ?? p.Package} is saved in your wallet.");
        }

        foreach (var o in ordersNotifs.Take(2))
        {
            var textSnippet = string.IsNullOrEmpty(o.Text) ? o.Title : o.Text;
            FollowUps.Add(isFa
                ? $"پیگیری سفارش: {o.Title} - {textSnippet}"
                : $"Track order: {o.Title} - {textSnippet}");
        }

        if (moneyNotifs.Count > 0)
        {
            FollowUps.Add(isFa
                ? "بررسی تراکنش‌های بانکی جهت تایید موجودی و مبالغ"
                : "Verify transaction details and account balance");
        }

        HasFollowUps = FollowUps.Count > 0;

        // 5. Categorized Synthesized Sections
        var allGroups = notifs.GroupBy(n => n.Category ?? "other").ToList();
        foreach (var g in allGroups)
        {
            var markColor = GetCategoryMarkColor(g.Key);
            var tintColor = GetCategoryTintColor(g.Key);
            string locLabel = GetCategoryLabel(g.Key);

            var bullets = new List<string>();
            var subGroups = g.GroupBy(n => string.IsNullOrEmpty(n.ChannelName) ? n.Package : n.ChannelName);
            foreach (var sg in subGroups)
            {
                var count = sg.Count();
                var first = sg.First();
                if (count == 1)
                {
                    bullets.Add($"{first.Title}: {first.Text}");
                }
                else
                {
                    var sender = string.IsNullOrEmpty(first.ChannelName) ? first.Title : first.ChannelName;
                    bullets.Add(isFa
                        ? $"{sender} ({count} اعلان): آخرین پیام: {first.Text}"
                        : $"{sender} ({count} updates): Latest: {first.Text}");
                }
            }

            Sections.Add(new DigestSectionItem(g.Key, locLabel, bullets.Take(5), markColor, tintColor));
        }
    }

    private async Task GenerateAiDigestAsync()
    {
        if (IsGenerating) return;

        UpdateModelStatus();
        if (!IsModelInstalled)
        {
            var page = Shell.Current?.CurrentPage ?? Application.Current?.Windows[0]?.Page;
            if (page != null)
            {
                await page.DisplayAlertAsync("Alak", LocalizationManager.Instance["Digest_NoModel"], LocalizationManager.Instance["Common_OK"]);
            }
            return;
        }

        var notifs = await LoadPeriodNotificationsAsync();
        if (notifs.Count == 0)
        {
            Headline = LocalizationManager.Instance["Digest_NoNotifications"];
            Sections.Clear();
            FollowUps.Clear();
            HasFollowUps = false;
            return;
        }

        _generationCts = new CancellationTokenSource();
        var ct = _generationCts.Token;

        var loc = LocalizationManager.Instance;
        var isFa = loc.CurrentLanguage == "fa";

        IsGenerating = true;
        IsStreaming = true;
        StreamingText = string.Empty;
        Sections.Clear();
        Headline = loc["Digest_Generating"];

        var rawBuilder = new StringBuilder();

        try
        {
            await foreach (var token in _modelService.GenerateDigestStreamAsync(notifs, ct))
            {
                if (ct.IsCancellationRequested || !IsAiMode) break;
                rawBuilder.Append(token);
                StreamingText = rawBuilder.ToString();
            }

            if (ct.IsCancellationRequested || !IsAiMode)
            {
                return;
            }

            var fullText = rawBuilder.ToString().Trim();
            if (!string.IsNullOrEmpty(fullText))
            {
                try
                {
                    var parsed = DigestTask.ParseResult(fullText);
                    Headline = parsed.Headline;
                    Sections.Clear();
                    foreach (var sec in parsed.Sections)
                    {
                        var markColor = GetCategoryMarkColor(sec.Category);
                        var tintColor = GetCategoryTintColor(sec.Category);
                        var locLabel = GetCategoryLabel(sec.Category);

                        Sections.Add(new DigestSectionItem(sec.Category ?? "other", locLabel, sec.Bullets, markColor, tintColor));
                    }

                    FollowUps.Clear();
                    foreach (var fu in parsed.FollowUps)
                    {
                        if (!string.IsNullOrWhiteSpace(fu))
                        {
                            FollowUps.Add(fu);
                        }
                    }
                    HasFollowUps = FollowUps.Count > 0;
                    PopulateInsightCards(notifs, isFa, loc);
                }
                catch
                {
                    if (IsAiMode && !ct.IsCancellationRequested)
                    {
                        GenerateSmartSynthesis(notifs);
                    }
                }
            }
            else if (IsAiMode && !ct.IsCancellationRequested)
            {
                GenerateSmartSynthesis(notifs);
            }
        }
        catch (OperationCanceledException)
        {
            if (IsAiMode)
            {
                Headline = LocalizationManager.Instance["Digest_EmptyState"];
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DigestViewModel] AI generation error: {ex.Message}");
            if (IsAiMode && !ct.IsCancellationRequested)
            {
                GenerateSmartSynthesis(notifs);
            }
        }
        finally
        {
            IsGenerating = false;
            IsStreaming = false;
            try { _generationCts?.Dispose(); } catch { }
            _generationCts = null;
        }
    }

    private void CancelGeneration()
    {
        if (_generationCts != null && !_generationCts.IsCancellationRequested)
        {
            try
            {
                _generationCts.Cancel();
            }
            catch { }
        }
        IsGenerating = false;
        IsStreaming = false;
    }

    private static string GetCategoryLabel(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return LocalizationManager.Instance["Cat_Other"];

        var key = $"Cat_{char.ToUpperInvariant(category[0]) + (category.Length > 1 ? category[1..] : "")}";
        var loc = LocalizationManager.Instance.GetString(key);
        if (string.IsNullOrEmpty(loc) || loc == key)
        {
            return char.ToUpperInvariant(category[0]) + (category.Length > 1 ? category[1..] : "");
        }
        return loc;
    }

    private static Color GetCategoryMarkColor(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Color.FromArgb(DesignTokens.CategoryMark.Other);

        var hex = category.ToLowerInvariant() switch
        {
            "promo" or "offers" => DesignTokens.CategoryMark.Promo,
            "work" => DesignTokens.CategoryMark.Work,
            "people" => DesignTokens.CategoryMark.People,
            "money" => DesignTokens.CategoryMark.Money,
            "orders" => DesignTokens.CategoryMark.Orders,
            "security" => DesignTokens.CategoryMark.Security,
            "social" => DesignTokens.CategoryMark.Social,
            "updates" => DesignTokens.CategoryMark.Updates,
            _ => DesignTokens.CategoryMark.Other
        };
        return Color.FromArgb(hex);
    }

    private static Color GetCategoryTintColor(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return Color.FromArgb(DesignTokens.Light.OtherTint);

        var hex = category.ToLowerInvariant() switch
        {
            "promo" or "offers" => DesignTokens.Light.PromoTint,
            "work" => DesignTokens.Light.WorkTint,
            "people" => DesignTokens.Light.PeopleTint,
            "money" => DesignTokens.Light.MoneyTint,
            "orders" => DesignTokens.Light.OrdersTint,
            "security" => DesignTokens.Light.SecurityTint,
            "social" => DesignTokens.Light.SocialTint,
            "updates" => DesignTokens.Light.UpdatesTint,
            _ => DesignTokens.Light.OtherTint
        };
        return Color.FromArgb(hex);
    }
}
