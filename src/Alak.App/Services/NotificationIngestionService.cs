using Alak.Core.Capture;
using Alak.Core.Classification;
using Alak.Core.Model;
using Alak.Rules;
using Alak.Storage;
using Alak.Text;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Alak.App.Services;

public interface INotificationIngestionService
{
    Task IngestAsync(RawNotification raw);
    event Action<Notification>? NotificationIngested;
}

public sealed class NotificationIngestionService : INotificationIngestionService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRulesRepository _userRulesRepository;
    private readonly IPromoCodeRepository _promoCodeRepository;
    private readonly UserRulesEngine _userRulesEngine;

    public event Action<Notification>? NotificationIngested;

    public NotificationIngestionService(
        INotificationRepository notificationRepository,
        IUserRulesRepository userRulesRepository,
        IPromoCodeRepository promoCodeRepository,
        UserRulesEngine userRulesEngine)
    {
        _notificationRepository = notificationRepository;
        _userRulesRepository = userRulesRepository;
        _promoCodeRepository = promoCodeRepository;
        _userRulesEngine = userRulesEngine;
    }

    public async Task IngestAsync(RawNotification raw)
    {
        try
        {
            // 1. Default ignore filter (CAP-005)
            if (DefaultIgnoreFilter.ShouldIgnore(raw))
            {
                Debug.WriteLine($"[Ingestion] Ignored by DefaultIgnoreFilter: {raw.Package} - {raw.Title}");
                return;
            }

            // 2. App settings check (Mute, Category override)
            var appSettings = await _userRulesRepository.GetAppSettingsAsync(raw.Package);
            if (appSettings?.Mute == true)
            {
                Debug.WriteLine($"[Ingestion] Muted by AppSettings: {raw.Package}");
                return;
            }

            // 3. User rules check
            var userAction = await _userRulesEngine.EvaluateAsync(raw);
            if (userAction?.Mute == true)
            {
                Debug.WriteLine($"[Ingestion] Muted by UserRule: {raw.Package} - {raw.Title}");
                return;
            }

            // 4. Tier 1 classification
            var classification = RulesEngine.Classify(raw, PackSet.BuiltInDefault);
            
            var category = userAction?.SetCategory
                ?? appSettings?.DefaultCategoryOverride
                ?? classification.Category.ToKebabId();

            // 5. Promo code extraction & wallet upsert
            var promoEntity = classification.Entities.OfType<PromoCodeEntity>().FirstOrDefault();
            string? extractedCode = promoEntity?.Code;

            if (promoEntity != null && (category == "offers" || category == "promo"))
            {
                try
                {
                    var promoCode = new PromoCode(
                        Id: 0,
                        CodeNorm: promoEntity.Code.ToLowerInvariant().Trim(),
                        RawCode: promoEntity.Code,
                        Package: raw.Package,
                        AccountEmail: null,
                        ChannelId: raw.ChannelId,
                        Status: PromoCodeStatus.Active,
                        Confidence: promoEntity.Confidence,
                        Kind: promoEntity.Kind,
                        DiscountValue: promoEntity.Discount?.Value,
                        DiscountType: promoEntity.Discount?.Type,
                        Currency: promoEntity.Discount?.Currency,
                        MinSpend: promoEntity.MinSpend,
                        Cap: promoEntity.Cap,
                        Scope: promoEntity.Scope,
                        FirstOrder: promoEntity.FirstOrder,
                        SingleUse: promoEntity.SingleUse,
                        ValidFrom: promoEntity.ValidFrom,
                        ValidUntil: promoEntity.ValidUntil,
                        StoreHint: promoEntity.StoreHint,
                        FirstSeenAt: raw.PostedAtUtcMs,
                        LastSeenAt: raw.PostedAtUtcMs,
                        TimesSeen: 1
                    );

                    await _promoCodeRepository.UpsertAsync(promoCode);
                    Debug.WriteLine($"[Ingestion] Upserted PromoCode: {promoEntity.Code} for {raw.Package}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Ingestion] Error upserting promo code: {ex.Message}");
                }
            }

            // 6. Persian script detection
            var fullText = (raw.Title ?? "") + " " + (raw.Text ?? "");
            var isPersian = ScriptLanguage.FirstStrongDirection(fullText) == TextDirection.Rtl ||
                            ScriptLanguage.Guess(fullText).Language == LanguageFa.Persian;

            // 7. Store notification
            var notification = new Notification(
                Id: raw.SbnKey,
                Package: raw.Package,
                ChannelName: raw.ChannelName ?? raw.ChannelId ?? "",
                Title: raw.Title ?? "",
                Text: raw.Text ?? "",
                Category: category,
                Timestamp: DateTimeOffset.FromUnixTimeMilliseconds(raw.PostedAtUtcMs),
                IsRead: userAction?.ArchiveImmediately == true || appSettings?.AutoArchiveOnRead == true,
                IsPersian: isPersian,
                ExtractedCode: extractedCode
            );

            await _notificationRepository.AddAsync(notification);
            Debug.WriteLine($"[Ingestion] Saved notification: {notification.Id} ({category}) from {notification.Package}");

            // 8. Notify listeners (e.g. InboxViewModel)
            try
            {
                NotificationIngested?.Invoke(notification);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Ingestion] Listener event exception: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Ingestion] Failed to ingest notification: {ex}");
        }
    }
}
