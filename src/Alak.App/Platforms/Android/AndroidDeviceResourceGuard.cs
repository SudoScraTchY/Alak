using Alak.App.Services;
using Android.App;
using Android.OS;
using Microsoft.Maui.ApplicationModel;

namespace Alak.App.Platforms.Android;

/// <summary>Android implementation of the SLM resource gate (SLM-020/021).</summary>
public sealed class AndroidDeviceResourceGuard : IDeviceResourceGuard
{
    private const long Megabyte = 1024L * 1024L;
    private const long HeadroomBytes = 350 * Megabyte;

    public DeviceResourceSnapshot Capture()
    {
        long avail = 0, total = 0, pssKb = 0;
        var thermal = 0;
        var powerSave = false;
        var batteryPercent = -1;
        var charging = false;

        try
        {
            var context = global::Android.App.Application.Context;
            var activityManager = (ActivityManager?)context.GetSystemService(global::Android.Content.Context.ActivityService);
            if (activityManager != null)
            {
                var info = new ActivityManager.MemoryInfo();
                activityManager.GetMemoryInfo(info);
                avail = info.AvailMem;
                total = info.TotalMem;

                var pids = new[] { Process.MyPid() };
                var memInfo = activityManager.GetProcessMemoryInfo(pids);
                if (memInfo is { Length: > 0 }) pssKb = memInfo[0].TotalPss;
            }

            var powerManager = (PowerManager?)context.GetSystemService(global::Android.Content.Context.PowerService);
            if (powerManager != null)
            {
                powerSave = powerManager.IsPowerSaveMode;
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
                {
                    thermal = (int)powerManager.CurrentThermalStatus;
                }
            }

            var batteryManager = (BatteryManager?)context.GetSystemService(global::Android.Content.Context.BatteryService);
            if (batteryManager != null)
            {
                batteryPercent = batteryManager.GetIntProperty((int)BatteryProperty.Capacity);
                charging = batteryManager.IsCharging;
            }
        }
        catch
        {
            // Metrics are advisory; loading continues with what we have.
        }

        return new DeviceResourceSnapshot(avail, total, thermal, powerSave, batteryPercent, charging, pssKb);
    }

    public ResourceCheck CheckCanLoad(long modelBytes)
    {
        var snapshot = Capture();

        if (modelBytes > 1536 * Megabyte)
        {
            return new ResourceCheck(false,
                $"Model file is {modelBytes / Megabyte} MB; the limit is 1536 MB. Choose a smaller model.",
                null, snapshot);
        }

        var required = (long)(modelBytes * 1.15) + HeadroomBytes;
        if (snapshot.AvailableMemoryBytes > 0 && snapshot.AvailableMemoryBytes < required)
        {
            return new ResourceCheck(false,
                $"Not enough free memory: {snapshot.AvailableMemoryBytes / Megabyte} MB free, {required / Megabyte} MB needed. Choose a smaller model.",
                null, snapshot);
        }

        if (snapshot.ThermalStatus >= 3)
        {
            return new ResourceCheck(false,
                "The phone is too hot to run the model right now. Let it cool down and try again.",
                null, snapshot);
        }

        string? warning = null;
        if (snapshot.PowerSaveMode)
        {
            warning = "Battery saver is on; generation may be slower.";
        }
        else if (snapshot.BatteryPercent is >= 0 and < 15 && !snapshot.IsCharging)
        {
            warning = "Battery is below 15% and not charging.";
        }

        return new ResourceCheck(true, null, warning, snapshot);
    }
}

/// <summary>Desktop/test fallback: only the file-size cap applies.</summary>
public sealed class FallbackDeviceResourceGuard : IDeviceResourceGuard
{
    public DeviceResourceSnapshot Capture() => new(0, 0, 0, false, -1, false, 0);

    public ResourceCheck CheckCanLoad(long modelBytes)
    {
        var snapshot = Capture();
        return modelBytes > 1536L * 1024 * 1024
            ? new ResourceCheck(false, "Model exceeds the 1536 MB limit.", null, snapshot)
            : new ResourceCheck(true, null, null, snapshot);
    }
}
