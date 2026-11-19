using UnityEngine;

/// <summary>
/// The lab mutations, in the order they show up (each bigger and harder than the last):
/// Butcher "Kasap" (charge / slam / debris) → Leaper "Sıçrayan" (leaps over anything, shockwave landing) →
/// Hive "Kovan Ana" (acid spit pools, broods of small runners) → Crusher "Ezici" (armoured front, crushes cover).
/// </summary>
public enum BossType { Butcher, Leaper, Hive, Crusher }

/// <summary>
/// Pure boss rules (EditMode-tested): which waves are boss waves, boss health, how much the ordinary
/// spawning is thinned out while the boss is alive, the reward, rage and weak-spot multipliers.
/// <see cref="BossDirector"/> owns the state; the boss components only read these.
/// </summary>
public static class BossRules
{
    public const int FirstBossWave = 6;
    public const int BossEvery = 5;

    /// <summary>Ordinary spawning while the boss is alive: interval × this, max alive × <see cref="AliveMul"/>.</summary>
    public const float SpawnIntervalMul = 4f;
    public const float AliveMul = 0.2f;

    /// <summary>Boss health = base zombie health × wave health multiplier × this (× growth per earlier boss).</summary>
    public static float HealthMul = 85f;
    public static float HealthGrowthPerBoss = 0.3f;

    public const float RageBelow = 0.5f;
    public const float WeakDamageMul = 2f;

    public static bool IsBossWave(int wave) => wave >= FirstBossWave && (wave - FirstBossWave) % BossEvery == 0;

    /// <summary>0 for the first boss (wave 6), 1 for wave 11, ...</summary>
    public static int BossIndex(int wave) => Mathf.Max(0, (wave - FirstBossWave) / BossEvery);

    public static float Health(float baseHealth, WaveParams wp, int bossIndex) =>
        Health(baseHealth, wp, bossIndex, TypeFor(bossIndex));

    public static float Health(float baseHealth, WaveParams wp, int bossIndex, BossType type) =>
        baseHealth * wp.healthMultiplier * HealthMul * TypeHealth[(int)type] * (1f + HealthGrowthPerBoss * bossIndex);

    public const int TypeCount = 4;
    /// <summary>Health on top of <see cref="HealthMul"/>: every later mutation is tougher.</summary>
    public static readonly float[] TypeHealth = { 1f, 1.15f, 1.35f, 1.65f };
    /// <summary>Model height in ordinary-zombie heights (1.8 m): each one towers over the previous.</summary>
    public static readonly float[] TypeScale = { 2.7f, 3.0f, 3.2f, 3.4f };

    /// <summary>Endless / hard: boss n is the (n mod 4)-th mutation; after Ezici the cycle comes back stronger.</summary>
    public static BossType TypeFor(int bossIndex) => (BossType)(Mathf.Max(0, bossIndex) % TypeCount);

    /// <summary>Loc key of the boss name (+ ".sub" subtitle).</summary>
    public static string NameKey(BossType t)
    {
        switch (t)
        {
            case BossType.Leaper: return "boss.leaper";
            case BossType.Hive: return "boss.hive";
            case BossType.Crusher: return "boss.crusher";
            default: return "boss.butcher";
        }
    }

    public static bool Enraged(float health01) => health01 < RageBelow;

    public static float DamageTaken(float amount, bool weak) => weak ? amount * WeakDamageMul : amount;

    public static void Reward(int wave, int bossIndex, out int score, out int xp)
    {
        score = 2000 + 1000 * bossIndex + 100 * wave;
        xp = 80 + 10 * wave;
    }
}
