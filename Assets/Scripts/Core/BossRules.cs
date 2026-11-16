using UnityEngine;

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
        baseHealth * wp.healthMultiplier * HealthMul * (1f + HealthGrowthPerBoss * bossIndex);

    public static bool Enraged(float health01) => health01 < RageBelow;

    public static float DamageTaken(float amount, bool weak) => weak ? amount * WeakDamageMul : amount;

    public static void Reward(int wave, int bossIndex, out int score, out int xp)
    {
        score = 2000 + 1000 * bossIndex + 100 * wave;
        xp = 80 + 10 * wave;
    }
}
