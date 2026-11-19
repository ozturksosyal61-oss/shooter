using System;
using UnityEngine;

/// <summary>
/// App Store rating prompt (SKStoreReviewController via Unity): asked only after a good moment (new record, a cleared
/// chapter or a long run) once the player has finished a few runs, at most every <see cref="MinDaysBetween"/> days —
/// iOS itself caps it at 3 a year and may show nothing. Pure rule <see cref="ShouldAsk"/> is EditMode-tested.
/// </summary>
public static class StoreReview
{
    public const int MinRuns = 3;
    public const int MinDaysBetween = 60;
    public const int GoodWave = 6;

    public static bool ShouldAsk(int runsFinished, bool goodMoment, int today, int lastAskDay) =>
        goodMoment && runsFinished >= MinRuns && (lastAskDay < 0 || today - lastAskDay >= MinDaysBetween);

    /// <summary>Called when a run ends (result screen); counts the run and maybe asks.</summary>
    public static void RunFinished(bool goodMoment)
    {
        var e = SaveSystem.Data.economy;
        e.runsFinished++;
        int today = Economy.DayNumber(DateTime.UtcNow);
        if (ShouldAsk(e.runsFinished, goodMoment, today, e.reviewDay))
        {
            e.reviewDay = today;
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.RequestStoreReview();
#endif
        }
        SaveSystem.Write();
    }
}
