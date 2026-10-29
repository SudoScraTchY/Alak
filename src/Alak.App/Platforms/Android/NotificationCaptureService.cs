using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using Android.Util;
using Alak.App.Services;
using Alak.Core.Capture;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alak.App.Platforms.Android;

[Service(Name = "com.alak.app.NotificationCaptureService",
         Label = "Alak",
         Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE",
         Exported = true)]
[IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
public sealed class NotificationCaptureService : NotificationListenerService
{
    private const string Tag = "AlakCaptureService";

    public override void OnListenerConnected()
    {
        base.OnListenerConnected();
        Log.Info(Tag, "NotificationCaptureService connected to Android system");

        try
        {
            AppLaunchPromoReminderService.Instance.Start(this);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Error starting AppLaunchPromoReminderService: {ex.Message}");
        }

        // Backfill active notifications on connect (CAP-002) in the background to avoid main-thread jank
        Task.Run(() =>
        {
            try
            {
                var active = GetActiveNotifications();
                if (active != null)
                {
                    Log.Info(Tag, $"Backfilling {active.Length} active notifications in background");
                    foreach (var sbn in active)
                    {
                        ProcessNotification(sbn);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(Tag, $"Error backfilling active notifications: {ex}");
            }
        });
    }

    public override void OnListenerDisconnected()
    {
        base.OnListenerDisconnected();
        Log.Warn(Tag, "NotificationCaptureService disconnected from Android system; requesting rebind");
        try
        {
            AppLaunchPromoReminderService.Instance.Stop(this);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Error stopping AppLaunchPromoReminderService on disconnect: {ex.Message}");
        }

        try
        {
            RequestRebind(new ComponentName(this, Java.Lang.Class.FromType(typeof(NotificationCaptureService))));
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Error requesting rebind: {ex}");
        }
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        Log.Info(Tag, "NotificationCaptureService destroyed");
        try
        {
            AppLaunchPromoReminderService.Instance.Stop(this);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Error stopping AppLaunchPromoReminderService on destroy: {ex.Message}");
        }
    }

    public override void OnNotificationPosted(StatusBarNotification? sbn, RankingMap? rankingMap)
    {
        base.OnNotificationPosted(sbn, rankingMap);
        if (sbn == null) return;

        ProcessNotification(sbn, rankingMap);
    }

    private void ProcessNotification(StatusBarNotification sbn, RankingMap? rankingMap = null)
    {
        try
        {
            var n = sbn.Notification;
            if (n == null) return;

            var extras = n.Extras;
            var title = extras?.GetCharSequence(Notification.ExtraTitle)?.ToString();
            var text = extras?.GetCharSequence(Notification.ExtraText)?.ToString();
            var bigText = extras?.GetCharSequence(Notification.ExtraBigText)?.ToString();
            var subText = extras?.GetCharSequence(Notification.ExtraSubText)?.ToString();
            var convTitle = extras?.GetCharSequence(Notification.ExtraConversationTitle)?.ToString();

            var textLines = new List<string>();
            var linesSeq = extras?.GetCharSequenceArray(Notification.ExtraTextLines);
            if (linesSeq != null)
            {
                foreach (var line in linesSeq)
                {
                    if (line != null) textLines.Add(line.ToString()!);
                }
            }

            var actionLabels = new List<string>();
            if (n.Actions != null)
            {
                foreach (var action in n.Actions)
                {
                    if (action?.Title != null) actionLabels.Add(action.Title.ToString()!);
                }
            }

            var flags = n.Flags;
            var isOngoing = flags.HasFlag(NotificationFlags.OngoingEvent);
            var isForeground = flags.HasFlag(NotificationFlags.ForegroundService);
            var isGroupSummary = flags.HasFlag(NotificationFlags.GroupSummary);
            var isClearable = flags.HasFlag(NotificationFlags.AutoCancel) || !flags.HasFlag(NotificationFlags.NoClear);

            var importance = 3;
            if (rankingMap != null)
            {
                var ranking = new Ranking();
                if (rankingMap.GetRanking(sbn.Key, ranking) && ranking.Channel != null)
                {
                    importance = (int)ranking.Channel.Importance;
                }
            }

            var tzOffsetMin = (int)TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.UtcNow).TotalMinutes;
            var sbnKey = !string.IsNullOrWhiteSpace(sbn.Key) ? sbn.Key : $"sbn_{sbn.PackageName}_{sbn.Id}_{sbn.PostTime}";

            var raw = new RawNotification(
                SbnKey: sbnKey,
                Package: sbn.PackageName ?? "",
                Id: sbn.Id,
                Tag: sbn.Tag,
                PostedAtUtcMs: sbn.PostTime > 0 ? sbn.PostTime : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                TzOffsetMin: tzOffsetMin,
                ChannelId: n.ChannelId,
                ChannelName: n.ChannelId,
                AndroidCategory: n.Category,
                Importance: importance,
                IsOngoing: isOngoing,
                IsGroupSummary: isGroupSummary,
                IsForegroundService: isForeground,
                IsClearable: isClearable,
                GroupKey: sbn.GroupKey,
                Title: title,
                Text: text,
                BigText: bigText,
                SubText: subText,
                TextLines: textLines,
                ActionLabels: actionLabels,
                ConversationTitle: convTitle,
                LooksRedactedByOs: false,
                UserHandle: sbn.User != null ? sbn.User.GetHashCode() : 0
            );

            // Privacy check: exclude communication apps if setting is enabled
            if (Microsoft.Maui.Storage.Preferences.Get("exclude_communications", false) && CommunicationAppDetector.IsCommunicationNotification(raw))
            {
                Log.Info(Tag, $"Skipping communication notification from {raw.Package} per privacy setting.");
                return;
            }

            Log.Info(Tag, $"Captured notification: [{raw.Package}] '{raw.Title}': '{raw.Text}'");

            // Dispatch to ingestion service (fire & forget on background task)
            Task.Run(async () =>
            {
                try
                {
                    var services = AppServices.Current;
                    for (int i = 0; i < 15 && services == null; i++)
                    {
                        await Task.Delay(100);
                        services = AppServices.Current;
                    }

                    var ingestion = services?.GetService<INotificationIngestionService>();
                    if (ingestion != null)
                    {
                        await ingestion.IngestAsync(raw);
                    }
                    else
                    {
                        Log.Warn(Tag, "Ingestion service not yet available in DI container");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(Tag, $"Error in ingestion pipeline: {ex}");
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Error extracting notification: {ex}");
        }
    }
}
