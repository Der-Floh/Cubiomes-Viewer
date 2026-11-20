namespace Cubiomes_Viewer.Tests;

/// <summary>A clock that stands at one moment in its own time zone; its timestamps still run, so speeds are still measured.</summary>
internal sealed class StoppedClock(DateTimeOffset now, TimeZoneInfo localTimeZone) : TimeProvider
{
    /// <summary>Stands at 15:45:12 UTC on 20 November 2026 in a time zone two hours ahead, so its local time reads 17:45:12.</summary>
    public static StoppedClock TwoHoursAhead { get; } = new(
        new DateTimeOffset(2026, 11, 20, 15, 45, 12, TimeSpan.Zero),
        TimeZoneInfo.CreateCustomTimeZone("Two hours ahead", TimeSpan.FromHours(2), "Two hours ahead", "Two hours ahead"));

    public override TimeZoneInfo LocalTimeZone => localTimeZone;

    public override DateTimeOffset GetUtcNow() => now;
}
