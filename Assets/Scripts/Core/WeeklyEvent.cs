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

    // ---- 1.1.0 weekly rewards: reach a wave in the week's event once → its prize (once per week, granted at run end).
    public static readonly (int wave, int gold, int gems)[] Milestones = { (5, 250, 0), (10, 0, 10), (15, 600, 0), (20, 0, 25) };

    /// <summary>Pure: milestones reached with <paramref name="wave"/> and not yet in <paramref name="claimedMask"/>
    /// (bit i = Milestones[i]); returns the new mask and the prize total.</summary>
    public static int Claim(int wave, int claimedMask, out int gold, out int gems)
    {
        gold = gems = 0;
        for (int i = 0; i < Milestones.Length; i++)
        {
            if (wave < Milestones[i].wave || (claimedMask & (1 << i)) != 0) continue;
            claimedMask |= 1 << i;
            gold += Milestones[i].gold;
            gems += Milestones[i].gems;
        }
        return claimedMask;
    }

    /// <summary>The next unclaimed milestone index (-1 = all claimed this week).</summary>
    public static int NextMilestone(int claimedMask)
    {
        for (int i = 0; i < Milestones.Length; i++) if ((claimedMask & (1 << i)) == 0) return i;
        return -1;
    }
}
