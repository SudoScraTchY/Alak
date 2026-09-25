namespace Alak.Core.Abstractions;

/// <summary>
/// The only way logic obtains "now" (ARC-003). Never use DateTime.Now/DateTimeOffset.Now
/// in logic code — tests pin time with <see cref="FixedClock"/> and retention tests
/// simulate the passage of days.
/// </summary>
public interface IClock
{
    /// <summary>Current UTC time.</summary>
    DateTimeOffset UtcNow();

    /// <summary>Convert a UTC instant to a local offset (minutes) for calendar rendering.</summary>
    DateTimeOffset ToLocal(DateTimeOffset utc, int offsetMinutes);
}

/// <summary>Test/production clock pinned to a fixed instant (ARC-003).</summary>
public sealed class FixedClock(DateTimeOffset at) : IClock
{
    private readonly DateTimeOffset _at = at;

    public DateTimeOffset UtcNow() => _at;

    public DateTimeOffset ToLocal(DateTimeOffset utc, int offsetMinutes)
        => utc.ToOffset(TimeSpan.FromMinutes(offsetMinutes));
}

/// <summary>Real system clock for production wiring only (ARC-003).</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;

    public DateTimeOffset ToLocal(DateTimeOffset utc, int offsetMinutes)
        => utc.ToOffset(TimeSpan.FromMinutes(offsetMinutes));
}
