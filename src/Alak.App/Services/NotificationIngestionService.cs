using Alak.Adaptive;
using Alak.Core.Capture;
using Alak.Core.Classification;
using Alak.Core.Linking;
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
    private readonly IAdaptiveEngine _adaptiveEngine;
    private readonly IPackLoader _packLoader;
    private readonly IRuleCompiler _ruleCompiler;
    private readonly IAppInfoProvider _appInfoProvider;
    private readonly IAppDomainRepository _appDomainRepository;
    private readonly HashSet<string> _resolvedPackages = new(StringComparer.Ordinal);
    private PackSet? _activePackSet;

    public event Action<Notification>? NotificationIngested;

    public NotificationIngestionService(
        INotificationRepository notificationRepository,
        IUserRulesRepository userRulesRepository,
        IPromoCodeRepository promoCodeRepository,
        UserRulesEngine userRulesEngine,
        IAdaptiveEngine adaptiveEngine,
        IPackLoader packLoader,
        IRuleCompiler ruleCompiler,
        IAppInfoProvider appInfoProvider,
        IAppDomainRepository appDomainRepository)
    {
        _notificationRepository = notificationRepository;
        _userRulesRepository = userRulesRepository;
        _promoCodeRepository = promoCodeRepository;
        _userRulesEngine = userRulesEngine;
        _adaptiveEngine = adaptiveEngine;
        _packLoader = packLoader;
        _ruleCompiler = ruleCompiler;
        _appInfoProvider = appInfoProvider;
        _appDomainRepository = appDomainRepository;
    }

    private async ValueTask<PackSet> GetPackSetAsync()
    {
        if (_activePackSet != null) return _activePackSet;
        var packs = await _packLoader.LoadAsync();
        _activePackSet = await _ruleCompiler.CompileAsync(packs);
        return _activePackSet;
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

            // 1b. Exclude private communications filter (SMS, email, messengers)
            var globalSettings = await _userRulesRepository.GetGlobalSettingsAsync();
            if (globalSettings.ExcludeCommunications && CommunicationAppDetector.IsCommunicationNotification(raw))
            {
                Debug.WriteLine($"[Ingestion] Ignored private communication notification: {raw.Package} - {raw.Title}");
                return;
            }

            // 2. App settings check (Mute, Category override)
            var appSettings = await _userRulesRepository.GetAppSettingsAsync(raw.Package);
            if (appSettings?.Mute == true)
            {
                Debug.WriteLine($"[Ingestion] Muted by AppSettings: {raw.Package}");
                return;
            }

            // 2b. Cache the app's display identity (CAP-013)
            await EnsureAppInfoAsync(raw.Package, raw.AppLabel, raw.AppIconPng);

            // 2c. Learn link→app mappings from real apps; attribute SMS ads to the brand
            var attributedPackage = await LearnOrAttributeSourceAsync(raw);

            // 3. User rules check
            var userAction = await _userRulesEngine.EvaluateAsync(raw);
            if (userAction?.Mute == true)
            {
                Debug.WriteLine($"[Ingestion] Muted by UserRule: {raw.Package} - {raw.Title}");
                return;
            }

            // 4. Tier 1 classification using all active packs (WBP-02)
            var packSet = await GetPackSetAsync();
            var classification = RulesEngine.Classify(raw, packSet);
            
            var category = userAction?.SetCategory?.ToLowerInvariant()
                ?? appSettings?.DefaultCategoryOverride?.ToLowerInvariant()
                ?? classification.Category.ToKebabId();

            // 4b. Tier 2 Adaptive Engine inference & pseudo-label learning (spec §9.1)
            var fullText = (raw.Title ?? "") + " " + (raw.Text ?? "");
            if (globalSettings.Tier2Enabled && userAction?.SetCategory == null && appSettings?.DefaultCategoryOverride == null)
            {
                try
                {
                    var conceptHits = classification.ConceptHits.ToArray();

                    if (_adaptiveEngine.CorrectionCount > 0)
                    {
                        var adaptiveLogits = await _adaptiveEngine.InferAsync(
                            raw.Package,
                            raw.ChannelId ?? "",
                            fullText,
                            conceptHits,
                            classification.Entities,
                            raw.AndroidCategory);

                        if (adaptiveLogits != null && adaptiveLogits.Length == 9)
                        {
                            var blendedCategory = _adaptiveEngine.BlendDecision(
                                classification.Category,
                                classification.Confidence,
                                adaptiveLogits);
                            category = blendedCategory.ToKebabId();
                            Debug.WriteLine($"[Ingestion] Tier 2 Adaptive categorized as: {category} (beta={_adaptiveEngine.Beta:F2})");
                        }
                    }

                    // Pseudo-label learning from high-confidence Tier 1 results (>= 0.90)
                    if (classification.Confidence >= 0.90f)
                    {
                        await _adaptiveEngine.LearnFromHighConfidenceAsync(
                            raw.Package,
                            raw.ChannelId ?? "",
                            fullText,
                            conceptHits,
                            classification.Entities,
                            classification.Category,
                            classification.Confidence,
                            raw.AndroidCategory);
                    }
                }
                catch (Exception adaptiveEx)
                {
                    Debug.WriteLine($"[Ingestion] Tier 2 Adaptive error: {adaptiveEx.Message}");
                }
            }

            // 5. Promo code extraction & wallet upsert
            var promoEntity = classification.Entities.OfType<PromoCodeEntity>().OrderByDescending(p => p.Confidence).FirstOrDefault();
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
                        TimesSeen: 1,
                        NotificationId: raw.SbnKey
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
                ExtractedCode: extractedCode,
                AttributedPackage: attributedPackage
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

    private async Task EnsureAppInfoAsync(string package, string? knownLabel = null, byte[]? knownIconPng = null)
    {
        if (string.IsNullOrWhiteSpace(package)) return;

        var hasKnownIdentity = !string.IsNullOrWhiteSpace(knownLabel) || knownIconPng is { Length: > 0 };
        if (!hasKnownIdentity && !_resolvedPackages.Add(package)) return;
        if (hasKnownIdentity) _resolvedPackages.Add(package);

        try
        {
            var info = hasKnownIdentity ? null : _appInfoProvider.Resolve(package);
            var label = string.IsNullOrWhiteSpace(knownLabel) ? info?.Label : knownLabel;
            var icon = knownIconPng is { Length: > 0 } ? knownIconPng : info?.IconPng;

            if (label != null || icon != null)
            {
                await _notificationRepository.UpsertAppInfoAsync(package, label, icon);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Ingestion] App info resolution failed for {package}: {ex.Message}");
        }
    }

    /// <summary>
    /// Real apps teach the domain index (host → package). Notifications posted by an
    /// SMS app are matched against that index; the result is display-only and never
    /// replaces the original package. No link is ever fetched (no network permission).
    /// </summary>
    private async Task<string?> LearnOrAttributeSourceAsync(RawNotification raw)
    {
        try
        {
            var domains = SafeLinkParser.ExtractDomains(raw.Title, raw.Text, raw.BigText, raw.TextLines);
            if (domains.Count == 0) return null;

            if (CommunicationAppDetector.IsSmsApp(raw.Package))
            {
                var attributed = await _appDomainRepository.ResolvePackageAsync(domains);
                if (!string.IsNullOrEmpty(attributed))
                {
                    await EnsureAppInfoAsync(attributed);
                    Debug.WriteLine($"[Ingestion] Attributed SMS notification from {raw.Package} to {attributed}");
                }
                return attributed;
            }

            if (!CommunicationAppDetector.IsCommunicationApp(raw.Package))
            {
                await _appDomainRepository.LearnAsync(domains, raw.Package);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Ingestion] Source attribution failed: {ex.Message}");
        }

        return null;
    }
}
