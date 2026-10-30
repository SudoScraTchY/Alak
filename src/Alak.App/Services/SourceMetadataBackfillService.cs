using Alak.Core.Capture;
using Alak.Core.Linking;
using Alak.Storage;
using Microsoft.Maui.Storage;

namespace Alak.App.Services;

/// <summary>
/// One-time startup pass: fills missing app labels/icons and seeds the domain→app
/// index from notifications already in the local database (ADR-0003). Purely local;
/// no network, no link resolution.
/// </summary>
public sealed class SourceMetadataBackfillService
{
    private const string CompletionFlag = "source_metadata_backfill_v1";
    private const int MaxPackages = 200;
    private const int MaxNotificationsToScan = 1000;

    private readonly INotificationRepository _notifications;
    private readonly IAppDomainRepository _domains;
    private readonly IAppInfoProvider _apps;

    public SourceMetadataBackfillService(
        INotificationRepository notifications,
        IAppDomainRepository domains,
        IAppInfoProvider apps)
    {
        _notifications = notifications;
        _domains = domains;
        _apps = apps;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        if (Preferences.Get(CompletionFlag, false)) return;

        await BackfillLabelsAsync(ct);
        await LearnDomainsAsync(ct);

        Preferences.Set(CompletionFlag, true);
    }

    private async Task BackfillLabelsAsync(CancellationToken ct)
    {
        var packages = await _notifications.GetPackagesWithoutLabelAsync(MaxPackages);
        foreach (var package in packages)
        {
            if (ct.IsCancellationRequested) return;

            var info = _apps.Resolve(package);
            if (info != null)
            {
                await _notifications.UpsertAppInfoAsync(package, info.Label, info.IconPng);
            }
        }
    }

    private async Task LearnDomainsAsync(CancellationToken ct)
    {
        var recent = await _notifications.GetPageAsync(null, MaxNotificationsToScan, null, ct);
        foreach (var notification in recent)
        {
            if (ct.IsCancellationRequested) return;
            if (CommunicationAppDetector.IsCommunicationApp(notification.Package)) continue;

            var domains = SafeLinkParser.ExtractDomains(notification.Title, notification.Text);
            if (domains.Count > 0)
            {
                await _domains.LearnAsync(domains, notification.Package, ct);
            }
        }
    }
}
