namespace BotControl.Models;

public class CaptureInfo
{
    public required string FileName { get; init; }
    public required string FilePath { get; init; }

    /// <summary>
    /// Monotonically increasing capture sequence number.
    /// Always reliable regardless of clock state.
    /// </summary>
    public int Sequence { get; init; }

    /// <summary>
    /// Seconds since the camera process started (monotonic clock).
    /// Not affected by wall clock jumps or missing RTC.
    /// </summary>
    public double UptimeSeconds { get; init; }

    /// <summary>
    /// Wall clock timestamp. Unreliable until GPS or NTP has synced the clock.
    /// Check <see cref="IsTimeSynced"/> before trusting this value.
    /// </summary>
    public DateTimeOffset WallClockUtc { get; init; }

    /// <summary>
    /// True if the system clock has been confirmed accurate (e.g. via GPS or NTP).
    /// </summary>
    public bool IsTimeSynced { get; set; }

    // GPS data to be populated later
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? Altitude { get; set; }
    public double? Heading { get; set; }
    public double? Speed { get; set; }
}
