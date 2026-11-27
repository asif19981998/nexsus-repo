namespace Nexus.Application.Common;

/// <summary>Time source. "Today" is the calendar date in Bangladesh (UTC+6, no daylight saving).</summary>
public interface IClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
    TimeOnly LocalTime { get; }
}

public sealed class BangladeshClock : IClock
{
    /// <summary>IANA zone id the mobile app is told to show times in.</summary>
    public const string ZoneId = "Asia/Dhaka";

    public static readonly TimeSpan Offset = TimeSpan.FromHours(6);

    /// <summary>A Bangladesh wall-clock time on a date, as a UTC instant.</summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly localTime) =>
        DateTime.SpecifyKind(date.ToDateTime(localTime) - Offset, DateTimeKind.Utc);

    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow + Offset);
    public TimeOnly LocalTime => TimeOnly.FromDateTime(DateTime.UtcNow + Offset);
}
