using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using UnityEngine.SocialPlatforms.GameCenter;
#endif

/// <summary>Leaderboard / achievement ids (App Store Connect → Game Center; same ids there).</summary>
public static class GameIds
{
    public const string Prefix = "com.oztdigital.holdout.";
    public const string LbEndless = Prefix + "endless_score";
    public const string LbHardWave = Prefix + "hard_wave";
    public const string LbStars = Prefix + "campaign_stars";
    /// <summary>Recurring leaderboard (weekly reset, Monday 00:00 UTC) for the weekly event.</summary>
    public const string LbWeekly = Prefix + "weekly_score";

    public const string AchWave5 = Prefix + "ach_wave5";
    public const string AchWave10 = Prefix + "ach_wave10";
    public const string AchWave20 = Prefix + "ach_wave20";
    public const string AchFirstBoss = Prefix + "ach_first_boss";
    public const string AchHelicopter = Prefix + "ach_helicopter";
    public const string AchCombo50 = Prefix + "ach_combo50";
    public const string AchSynergy = Prefix + "ach_synergy";
    public const string AchHard10 = Prefix + "ach_hard_wave10";
    public const string AchCampaign = Prefix + "ach_campaign";
    public const string AchAllStars = Prefix + "ach_all_stars";

    public static readonly string[] Achievements =
        { AchWave5, AchWave10, AchWave20, AchFirstBoss, AchHelicopter, AchCombo50, AchSynergy, AchHard10, AchCampaign, AchAllStars };
}

/// <summary>What a finished run reached (input of the pure achievement rules).</summary>
public struct RunSummary
{
    public RunMode mode;
    public bool weekly, won;
    public int score, wave, bestCombo, bosses, helicopters, chapter;
}

/// <summary>
/// Game Center (iOS): sign-in, leaderboards, achievements. Everything is a no-op when not signed in / not on iOS,
/// so the game never waits on it. Achievement rules are pure (<see cref="AchievementsFor"/>, EditMode-tested).
/// </summary>
public static class GameServices
{
    public static bool Signed { get; private set; }
    static bool started;
    static readonly HashSet<string> reported = new HashSet<string>();

    /// <summary>Starts sign-in once per app session (main menu, after the splash).</summary>
    public static void Init()
    {
        if (started) return;
        started = true;
#if UNITY_IOS && !UNITY_EDITOR
        GameCenterPlatform.ShowDefaultAchievementCompletionBanner(true);
        Social.localUser.Authenticate(ok => { Signed = ok; if (ok) { Flush(); CloudSync.Begin(); } else Debug.Log("GameCenter: not signed in"); });
#endif
    }

    /// <summary>Pure: achievements a run unlocks (campaign star total passed separately).</summary>
    public static void AchievementsFor(RunSummary r, int totalStars, int maxStars, List<string> into)
    {
        into.Clear();
        if (r.mode != RunMode.Campaign)
        {
            if (r.wave >= 5) into.Add(GameIds.AchWave5);
            if (r.wave >= 10) into.Add(GameIds.AchWave10);
            if (r.wave >= 20) into.Add(GameIds.AchWave20);
        }
        if (r.mode == RunMode.Hard && r.wave >= 10) into.Add(GameIds.AchHard10);
        if (r.bosses > 0) into.Add(GameIds.AchFirstBoss);
        if (r.helicopters > 0) into.Add(GameIds.AchHelicopter);
        if (r.bestCombo >= 50) into.Add(GameIds.AchCombo50);
        if (r.mode == RunMode.Campaign && r.won && r.chapter >= Campaign.Count - 1) into.Add(GameIds.AchCampaign);
        if (maxStars > 0 && totalStars >= maxStars) into.Add(GameIds.AchAllStars);
    }

    static readonly List<string> scratch = new List<string>(12);

    /// <summary>Run over: leaderboards + achievements (co-op runs don't count).</summary>
    public static void ReportRun(RunSummary r)
    {
        MetaEvents.RunFinished(r.mode, r.wave);
        if (r.mode == RunMode.Coop) return;
        var d = SaveSystem.Data;
        int stars = Campaign.TotalStars(d.campaign);
        if (r.weekly) Score(GameIds.LbWeekly, r.score);
        else if (r.mode == RunMode.Endless) Score(GameIds.LbEndless, r.score);
        else if (r.mode == RunMode.Hard) Score(GameIds.LbHardWave, r.wave);
        if (r.mode == RunMode.Campaign) Score(GameIds.LbStars, stars);
        AchievementsFor(r, stars, Campaign.Count * 3, scratch);
        foreach (var id in scratch) Unlock(id);
    }

    public static void Unlock(string id)
    {
        var list = SaveSystem.Data.achievements;
        if (!list.Contains(id)) { list.Add(id); SaveSystem.Write(); }
        if (!Signed || reported.Contains(id)) return;
        reported.Add(id);
        Social.ReportProgress(id, 100.0, ok => { if (!ok) reported.Remove(id); });
    }

    static void Score(string board, long value)
    {
        if (!Signed || value <= 0) return;
        Social.ReportScore(value, board, ok => { if (!ok) Debug.Log("GameCenter: score not sent " + board); });
    }

    /// <summary>After sign-in: re-send achievements earned while offline + current bests.</summary>
    static void Flush()
    {
        var d = SaveSystem.Data;
        foreach (var id in d.achievements) Unlock(id);
        Score(GameIds.LbEndless, d.records.bestScore);
        Score(GameIds.LbHardWave, d.records.hardWave);
        Score(GameIds.LbStars, Campaign.TotalStars(d.campaign));
        if (d.records.weeklyWeek == WeeklyEvent.CurrentWeek) Score(GameIds.LbWeekly, d.records.weeklyScore);
    }

    /// <summary>Settings → cloud button while not signed in: start Game Center sign-in again.</summary>
    public static void Retry() { started = false; Init(); }

    public static void ShowLeaderboards()
    {
        if (Signed) Social.ShowLeaderboardUI();
        else { started = false; Init(); } // not signed in: try again (Game Center shows its own sign-in sheet)
    }
}
