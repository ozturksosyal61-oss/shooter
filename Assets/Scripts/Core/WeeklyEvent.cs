using System;

/// <summary>
/// Weekly event (pure, EditMode-tested): an endless run on the same seeded city for everyone that week, with one rule —
/// a forced weapon that rotates every week. Weeks start Monday 00:00 UTC (match the Game Center recurring leaderboard).
/// </summary>
public static class WeeklyEvent
{
    static readonly DateTime Epoch = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc); // a Monday

    public static readonly WeaponId[] Rotation = { WeaponId.Shotgun, WeaponId.Uzi, WeaponId.Smg, WeaponId.Hmg, WeaponId.M4 };

    public static int Week(DateTime utc) => (int)Math.Floor((utc - Epoch).TotalDays / 7.0);
    public static int CurrentWeek => Week(DateTime.UtcNow);

    public static WeaponId WeaponFor(int week) => Rotation[((week % Rotation.Length) + Rotation.Length) % Rotation.Length];

    /// <summary>Same city for everyone in a week; never 0 (0 = "no override").</summary>
    public static int SeedFor(int week)
    {
        unchecked
        {
            uint h = (uint)week * 2654435761u ^ 0x9E3779B9u;
            h ^= h >> 15; h *= 0x85EBCA6Bu; h ^= h >> 13;
            return (int)(h % 999_983u) + 1;
        }
    }

    /// <summary>Whole days left until the next Monday 00:00 UTC (1..7).</summary>
    public static int DaysLeft(DateTime utc)
    {
        double into = (utc - Epoch).TotalDays - Week(utc) * 7.0;
        return Math.Max(1, (int)Math.Ceiling(7.0 - into));
    }

    public static string RuleKey(int week) => "weekly.rule." + WeaponFor(week);
}
