using UnityEngine;

/// <summary>One story chapter: its objectives in order, the wave it starts at, the par time for the second star.</summary>
public struct ChapterDef
{
    public string key;            // Loc: chapter.<key>.title / .desc
    public ObjectiveKind[] steps;
    public int startWave;
    public float surviveSeconds;  // length of a Survive step
    public float parSeconds;      // finish under this → ★★
    public int gold, gems;        // first-clear reward
    public BossType boss;         // the mutation a Boss step summons
}

/// <summary>
/// "Son Devriye" campaign (pure rules + save helpers; EditMode-tested). Each chapter is a fixed objective list fed to
/// <see cref="ObjectiveDirector"/> through <see cref="CampaignPlan"/>; the waves keep coming as in endless.
/// Stars: ★ finish · ★★ under par time · ★★★ finish with at least half health. Chapter n unlocks when n−1 is cleared.
/// </summary>
public static class Campaign
{
    const ObjectiveKind F = ObjectiveKind.Flare, T = ObjectiveKind.Tower, S = ObjectiveKind.Supply, H = ObjectiveKind.Helicopter,
        R = ObjectiveKind.Reach, V = ObjectiveKind.Survive, B = ObjectiveKind.Boss;

    public static readonly ChapterDef[] Chapters =
    {
        new ChapterDef { key = "c1", steps = new[] { F, S, R }, startWave = 1, surviveSeconds = 45f, parSeconds = 300f, gold = 150, gems = 3 },
        new ChapterDef { key = "c2", steps = new[] { T, F, V, R }, startWave = 2, surviveSeconds = 50f, parSeconds = 420f, gold = 250, gems = 3 },
        new ChapterDef { key = "c3", steps = new[] { S, V, B }, startWave = 3, surviveSeconds = 60f, parSeconds = 420f, gold = 400, gems = 5, boss = BossType.Butcher },
        new ChapterDef { key = "c4", steps = new[] { F, V, B, R }, startWave = 3, surviveSeconds = 70f, parSeconds = 540f, gold = 500, gems = 5, boss = BossType.Leaper },
        new ChapterDef { key = "c5", steps = new[] { T, B, S, R }, startWave = 4, surviveSeconds = 60f, parSeconds = 600f, gold = 650, gems = 8, boss = BossType.Hive },
        new ChapterDef { key = "c6", steps = new[] { S, T, V, B, H }, startWave = 5, surviveSeconds = 80f, parSeconds = 780f, gold = 1000, gems = 15, boss = BossType.Crusher },
    };

    public static int Count => Chapters.Length;
    public static ChapterDef Get(int i) => Chapters[Mathf.Clamp(i, 0, Chapters.Length - 1)];

    public const float ThreeStarHealth = 0.5f;
    /// <summary>Replaying a cleared chapter pays this share of the first-clear gold (no gems).</summary>
    public const float ReplayGoldShare = 0.25f;

    public static int Stars(float seconds, float par, float health01)
    {
        int s = 1;
        if (seconds <= par) s++;
        if (health01 >= ThreeStarHealth) s++;
        return s;
    }

    public static void Reward(int chapter, bool firstClear, out int gold, out int gems)
    {
        var c = Get(chapter);
        gold = firstClear ? c.gold : Mathf.RoundToInt(c.gold * ReplayGoldShare);
        gems = firstClear ? c.gems : 0;
    }

    // ---- save (SaveData.campaign.stars[i])
    public static int StarsOf(CampaignData d, int i) => d != null && i >= 0 && i < d.stars.Count ? d.stars[i] : 0;
    public static bool Unlocked(CampaignData d, int i) => i == 0 || StarsOf(d, i - 1) > 0;
    public static int TotalStars(CampaignData d) { int n = 0; for (int i = 0; i < Count; i++) n += StarsOf(d, i); return n; }

    /// <summary>Keeps the best star count; returns true on the first clear.</summary>
    public static bool Record(CampaignData d, int i, int stars)
    {
        while (d.stars.Count <= i) d.stars.Add(0);
        bool first = d.stars[i] == 0;
        d.stars[i] = Mathf.Max(d.stars[i], stars);
        return first;
    }
}

/// <summary>A chapter's objectives one after another (a short pause between them, no wave gating).</summary>
public class CampaignPlan : IObjectivePlan
{
    readonly ChapterDef def;
    public int Index { get; private set; }
    public CampaignPlan(ChapterDef d) => def = d;

    public ChapterDef Def => def;
    public int Total => def.steps.Length;
    public bool Finished => Index >= def.steps.Length;
    public bool IsLast => Index == def.steps.Length - 1;

    public bool ShouldIssue(int wave, int lastIssuedWave, bool hasActive) => !hasActive && !Finished;

    public ObjectiveStep Next(System.Random rng)
    {
        var s = Objectives.Step(def.steps[Mathf.Min(Index, def.steps.Length - 1)], rng);
        if (s.kind == ObjectiveKind.Survive) s.hold = def.surviveSeconds;
        return s;
    }

    public void Completed(ObjectiveStep s) => Index++;
}
