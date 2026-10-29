using Android.App;
using Android.App.Usage;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Android.Widget;
using AndroidX.Core.App;
using Alak.Storage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Alak.App.Platforms.Android;

/// <summary>
/// Monitors foreground app launches using UsageStatsManager and posts an actionable
/// notification whenever an app with active, unexpired promo codes is opened.
/// </summary>
public sealed class AppLaunchPromoReminderService
{
    private const string Tag = "AlakAppLaunch";
    public const string ChannelId = "alak_promo_launch_channel";
    public const string ActionCopyCode = "com.alak.app.ACTION_COPY_CODE";

    private static readonly Lazy<AppLaunchPromoReminderService> s_instance = new(() => new AppLaunchPromoReminderService());
    public static AppLaunchPromoReminderService Instance => s_instance.Value;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _cooldowns = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _cts;
    private string? _lastForegroundPackage;
    private bool _isScreenOn = true;
    private ScreenStateReceiver? _screenReceiver;

    private AppLaunchPromoReminderService() { }

    public static bool HasUsageAccess(Context context)
    {
        try
        {
            var appOps = (AppOpsManager)context.GetSystemService(Context.AppOpsService)!;
            var mode = appOps.CheckOpNoThrow(AppOpsManager.OpstrGetUsageStats, global::Android.OS.Process.MyUid(), context.PackageName);
            return mode == AppOpsManagerMode.Allowed;
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Error checking usage access: {ex.Message}");
            return false;
        }
    }

    public static void OpenUsageAccessSettings(Context context)
    {
        try
        {
            var intent = new Intent(global::Android.Provider.Settings.ActionUsageAccessSettings);
            intent.AddFlags(ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch (Exception ex)
        {
            Log.Warn(Tag, $"Error opening usage access settings: {ex.Message}");
        }
    }

    public void Start(Context context)
    {
        if (_cts != null) return; // already running

        CreateNotificationChannel(context);

        // Register screen state receiver to save battery when screen is off
        _screenReceiver = new ScreenStateReceiver(
            onScreenOn: () => _isScreenOn = true,
            onScreenOff: () => _isScreenOn = false);
        var filter = new IntentFilter();
        filter.AddAction(Intent.ActionScreenOn);
        filter.AddAction(Intent.ActionScreenOff);
        context.ApplicationContext?.RegisterReceiver(_screenReceiver, filter);

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            Log.Info(Tag, "AppLaunchPromoReminderService background polling loop started");
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_isScreenOn && Microsoft.Maui.Storage.Preferences.Get("notify_on_app_open", true))
                    {
                        if (HasUsageAccess(context))
                        {
                            CheckForegroundApp(context);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Warn(Tag, $"Error in foreground app check: {ex.Message}");
                }

                try
                {
                    await Task.Delay(1500, token);
                }
                catch (System.OperationCanceledException)
                {
                    break;
                }
            }
            Log.Info(Tag, "AppLaunchPromoReminderService background polling loop stopped");
        }, token);
    }

    public void Stop(Context? context = null)
    {
        _cts?.Cancel();
        _cts = null;

        if (_screenReceiver != null && context?.ApplicationContext != null)
        {
            try
            {
                context.ApplicationContext.UnregisterReceiver(_screenReceiver);
            }
            catch { }
            _screenReceiver = null;
        }
    }

    private void CheckForegroundApp(Context context)
    {
        var usageManager = (UsageStatsManager)context.GetSystemService(Context.UsageStatsService)!;
        if (usageManager == null) return;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var events = usageManager.QueryEvents(now - 3500, now);
        if (events == null) return;

        var usageEvent = new UsageEvents.Event();
        string? latestResumedPackage = null;

        while (events.HasNextEvent)
        {
            events.GetNextEvent(usageEvent);
            if (usageEvent.EventType == UsageEventType.ActivityResumed)
            {
                latestResumedPackage = usageEvent.PackageName;
            }
        }

        if (string.IsNullOrEmpty(latestResumedPackage)) return;
        if (latestResumedPackage == context.PackageName) return; // Don't notify for Alak itself
        if (IsLauncherPackage(context, latestResumedPackage)) return; // Don't notify on launcher/home

        if (_lastForegroundPackage != latestResumedPackage)
        {
            _lastForegroundPackage = latestResumedPackage;
            Log.Info(Tag, $"New foreground app detected: {latestResumedPackage}");
            _ = Task.Run(async () => await CheckAndNotifyPromoCodesAsync(context, latestResumedPackage));
        }
    }

    private async Task CheckAndNotifyPromoCodesAsync(Context context, string packageName)
    {
        try
        {
            // Cooldown check: 15 minutes per package
            if (_cooldowns.TryGetValue(packageName, out var lastTime))
            {
                if ((DateTimeOffset.UtcNow - lastTime).TotalMinutes < 15)
                {
                    Log.Debug(Tag, $"Package {packageName} is within cooldown period, skipping");
                    return;
                }
            }

            var services = AppServices.Current;
            var repo = services?.GetService<IPromoCodeRepository>();
            if (repo == null) return;

            var storeHint = Alak.Rules.Extraction.StoreExtractor.DetectStoreFromPackage(packageName);
            var codes = await repo.GetActiveCodesForPackageAsync(packageName, storeHint);
            if (codes == null || codes.Count == 0) return;

            // Mark cooldown
            _cooldowns[packageName] = DateTimeOffset.UtcNow;

            // Resolve friendly app name
            string appName = storeHint ?? packageName;
            try
            {
                var pm = context.PackageManager;
                var appInfo = pm?.GetApplicationInfo(packageName, 0);
                if (appInfo != null && pm != null)
                {
                    var label = pm.GetApplicationLabel(appInfo)?.ToString();
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        appName = label;
                    }
                }
            }
            catch { }

            // Pick highest value code
            var bestCode = codes.OrderByDescending(c => c.DiscountValue ?? 0).First();
            string discountText = "";
            if (bestCode.DiscountValue.HasValue)
            {
                discountText = $" ({bestCode.DiscountValue}{(bestCode.DiscountType == Core.Model.DiscountType.Percent ? "%" : "")})";
            }

            int notifId = (packageName.GetHashCode() & 0x7FFFFFFF);

            // Action: Copy Code
            var copyIntent = new Intent(context, typeof(PromoCodeActionReceiver));
            copyIntent.SetAction(ActionCopyCode);
            copyIntent.PutExtra("code", bestCode.RawCode);
            copyIntent.PutExtra("notification_id", notifId);
            var copyPending = PendingIntent.GetBroadcast(
                context,
                notifId,
                copyIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            // Action: Open Wallet in Alak
            var openIntent = new Intent(context, typeof(MainActivity));
            openIntent.PutExtra("route", "//wallet");
            openIntent.SetFlags(ActivityFlags.NewTask | ActivityFlags.ClearTop);
            var openPending = PendingIntent.GetActivity(
                context,
                notifId + 1,
                openIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

            bool isPersian = Services.LocalizationManager.Instance.CurrentLanguage == "fa";
            string title = isPersian 
                ? $"🎟️ کد تخفیف موجود برای {appName}"
                : $"🎟️ Promo code available for {appName}";

            string body = isPersian
                ? $"کد: {bestCode.RawCode}{discountText} — برای استفاده کپی کنید"
                : $"Use code {bestCode.RawCode}{discountText} on checkout";

            string copyBtn = isPersian ? "کپی کد" : "Copy Code";
            string walletBtn = isPersian ? "کیف تخفیف" : "View Wallet";

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(global::Android.Resource.Drawable.StarOn)
                .SetContentTitle(title)
                .SetContentText(body)
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetDefaults(NotificationCompat.DefaultAll)
                .SetAutoCancel(true)
                .SetContentIntent(openPending)
                .AddAction(global::Android.Resource.Drawable.IcMenuShare, copyBtn, copyPending)
                .AddAction(global::Android.Resource.Drawable.IcMenuInfoDetails, walletBtn, openPending);

            var notifManager = NotificationManagerCompat.From(context);
            notifManager.Notify(notifId, builder.Build());

            Log.Info(Tag, $"Posted launch promo reminder for [{packageName}] with code '{bestCode.RawCode}'");
        }
        catch (Exception ex)
        {
            Log.Error(Tag, $"Failed to notify launch promo: {ex}");
        }
    }

    private static bool IsLauncherPackage(Context context, string packageName)
    {
        try
        {
            var intent = new Intent(Intent.ActionMain);
            intent.AddCategory(Intent.CategoryHome);
            var resolveInfo = context.PackageManager?.ResolveActivity(intent, PackageInfoFlags.MatchDefaultOnly);
            if (resolveInfo?.ActivityInfo?.PackageName != null)
            {
                return string.Equals(resolveInfo.ActivityInfo.PackageName, packageName, StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return false;
    }

    private static void CreateNotificationChannel(Context context)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                "Promo Code Reminders on App Open",
                NotificationImportance.High)
            {
                Description = "Notifies you with available promo codes when you open a supported app"
            };
            var manager = (NotificationManager)context.GetSystemService(Context.NotificationService)!;
            manager?.CreateNotificationChannel(channel);
        }
    }

    private sealed class ScreenStateReceiver : BroadcastReceiver
    {
        private readonly Action _onScreenOn;
        private readonly Action _onScreenOff;

        public ScreenStateReceiver(Action onScreenOn, Action onScreenOff)
        {
            _onScreenOn = onScreenOn;
            _onScreenOff = onScreenOff;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == Intent.ActionScreenOn)
            {
                _onScreenOn();
            }
            else if (intent?.Action == Intent.ActionScreenOff)
            {
                _onScreenOff();
            }
        }
    }
}

/// <summary>
/// BroadcastReceiver that handles the "Copy Code" notification action.
/// </summary>
[BroadcastReceiver(Name = "com.alak.app.PromoCodeActionReceiver", Exported = false)]
public sealed class PromoCodeActionReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        if (intent.Action == AppLaunchPromoReminderService.ActionCopyCode)
        {
            var code = intent.GetStringExtra("code");
            int notifId = intent.GetIntExtra("notification_id", -1);

            if (!string.IsNullOrEmpty(code))
            {
                try
                {
                    var clipboard = (ClipboardManager)context.GetSystemService(Context.ClipboardService)!;
                    var clip = ClipData.NewPlainText("Promo Code", code);
                    clipboard.PrimaryClip = clip;

                    bool isPersian = Services.LocalizationManager.Instance.CurrentLanguage == "fa";
                    string msg = isPersian 
                        ? $"کد '{code}' در کلیپ‌بورد کپی شد!" 
                        : $"Code '{code}' copied to clipboard!";
                    Toast.MakeText(context, msg, ToastLength.Short)?.Show();

                    if (notifId >= 0)
                    {
                        var notifManager = NotificationManagerCompat.From(context);
                        notifManager.Cancel(notifId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("PromoActionReceiver", $"Error copying promo code: {ex}");
                }
            }
        }
    }
}
