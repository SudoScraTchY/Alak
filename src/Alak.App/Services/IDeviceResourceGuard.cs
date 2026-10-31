namespace Alak.App.Services;

public sealed record DeviceResourceSnapshot(
    long AvailableMemoryBytes,
    long TotalMemoryBytes,
    int ThermalStatus,
    bool PowerSaveMode,
    int BatteryPercent,
    bool IsCharging,
    long PssKb);

public sealed record ResourceCheck(bool Allowed, string? Reason, string? Warning, DeviceResourceSnapshot Snapshot);

/// <summary>
/// Pre-load resource gate for the SLM (spec SLM-020/021): free-memory budget,
/// thermal status, battery saver / low battery warnings. Android implementation
/// uses ActivityManager/PowerManager; the fallback only enforces the size cap.
/// </summary>
public interface IDeviceResourceGuard
{
    DeviceResourceSnapshot Capture();
    ResourceCheck CheckCanLoad(long modelBytes);
}
