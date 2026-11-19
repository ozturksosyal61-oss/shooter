using UnityEngine;

/// <summary>Coop: endless together (2–4 players, host-authoritative; see Assets/Scripts/Net).</summary>
public enum RunMode { Endless, Campaign, Hard, Coop }

/// <summary>
/// Which mode the next Game scene runs (set by the menu before loading; static so it survives the scene change).
/// Tests set it directly. <see cref="ModeRules"/> holds the pure per-mode tuning.
/// </summary>
public static class GameMode
{
    public static RunMode Current = RunMode.Endless;
    /// <summary>Campaign chapter index (0-based) when <see cref="Current"/> is Campaign.</summary>
    public static int Chapter;

    public static bool IsHard => Current == RunMode.Hard;
    public static bool IsCampaign => Current == RunMode.Campaign;
    public static bool IsCoop => Current == RunMode.Coop;

    public static void Set(RunMode m, int chapter = 0)
    {
        Current = m;
        Chapter = Mathf.Clamp(chapter, 0, Campaign.Count - 1);
    }
}

/// <summary>
/// Pure mode rules (EditMode-tested). Hard mode: tougher, faster, harder-hitting zombies, more of them, earlier
/// mutations and bosses, no breather regeneration, fewer medkits, a lower health pool and limited ammo — reserve
/// magazines refilled only by ammo boxes dropped by zombies (mutations always drop one) and supply crates.
/// </summary>
public static class ModeRules
{
    // ---- Hard: zombies / waves
    public static float HardHealthMul = 1.3f;
    public static float HardSpeedMul = 1.12f;
    public static float HardSpeedCap = 1.6f;
    public static float HardDamageMul = 1.4f;
    public static float HardIntervalMul = 0.8f;
    public static float HardAliveMul = 1.25f;
    public static int HardAliveCap = 80;
    public static float HardRunnerBonus = 0.12f;
    public static float HardSpecialMul = 1.4f;
    public const int HardFirstSpecialWave = 2;
    public const int HardFirstBossWave = 5, HardBossEvery = 4;

    // ---- Hard: player
    public static float HardPlayerHealthMul = 0.85f;
    public static float HardMedkitMul = 0.5f;
    public static float HardXpMul = 0.85f;
    public static float HardGoldMul = 1.75f;

    // ---- Hard: ammo
    public const int StartReserveMags = 5;
    public const int MaxReserveMags = 8;
    public static float AmmoDropChance = 0.16f;
    /// <summary>Drop chance while the reserve is below one magazine (pity — keeps a skilled run going).</summary>
    public static float AmmoDropChanceLow = 0.45f;
    public static float AmmoDropMinInterval = 1.5f;
    public const int SupplyAmmoMags = 3;
    /// <summary>Ammo boxes are pulled in from further away than medkits.</summary>
    public static float AmmoMagnetRadius = 8f;

    public static bool LimitedAmmo(RunMode m) => m == RunMode.Hard;
    public static bool BreatherRegen(RunMode m) => m != RunMode.Hard;

    public static bool IsBossWave(RunMode m, int wave)
    {
        switch (m)
        {
            case RunMode.Hard: return wave >= HardFirstBossWave && (wave - HardFirstBossWave) % HardBossEvery == 0;
            case RunMode.Campaign: return false; // chapters summon the boss as an objective
            default: return BossRules.IsBossWave(wave);
        }
    }

    public static int BossIndex(RunMode m, int wave) =>
        m == RunMode.Hard ? Mathf.Max(0, (wave - HardFirstBossWave) / HardBossEvery) : BossRules.BossIndex(wave);

    /// <summary>Hard-mode modifiers on top of the endless wave tuning.</summary>
    public static WaveParams Harden(WaveParams w)
    {
        w.healthMultiplier *= HardHealthMul;
        w.speedMultiplier = Mathf.Min(w.speedMultiplier * HardSpeedMul, HardSpeedCap);
        w.contactDamage *= HardDamageMul;
        w.spawnInterval *= HardIntervalMul;
        w.maxAlive = Mathf.Min(Mathf.RoundToInt(w.maxAlive * HardAliveMul), HardAliveCap);
        w.runnerChance = Mathf.Min(w.runnerChance + HardRunnerBonus, 0.6f);
        if (w.wave >= HardFirstSpecialWave)
        {
            float baseChance = w.specialChance > 0f ? w.specialChance : Difficulty.SpecialBase;
            w.specialChance = Mathf.Min(baseChance * HardSpecialMul, 0.6f);
            w.maxShield = Mathf.Max(w.maxShield, 1);
            w.maxBomb = Mathf.Max(w.maxBomb, 2);
            w.maxTongue = Mathf.Max(w.maxTongue, 1);
        }
        return w;
    }

    /// <summary>Ammo box drop chance for a kill (mutations always drop; low reserve raises it).</summary>
    public static float AmmoDrop(bool special, int reserve, int magazine) =>
        special ? 1f : reserve < magazine ? AmmoDropChanceLow : AmmoDropChance;

    public static int ReserveCap(int magazine) => magazine * MaxReserveMags;

    // ---- Co-op
    /// <summary>Teammates can't get further apart than this (one shared city / chunk window, no floating origin).</summary>
    public const float CoopLeash = 40f;
    /// <summary>Mutations in co-op waves (C2: on; tests can switch them off).</summary>
    public static bool CoopSpecials = true;
    /// <summary>Soldiers in the run (host sets it at the start): more players, more and tougher zombies.</summary>
    public static int CoopPlayers = 1;
    public const float CoopAlivePerPlayer = 0.35f, CoopIntervalPerPlayer = 0.3f, CoopHealthPerPlayer = 0.15f;
    public const int CoopAliveCap = 90;
    /// <summary>Playing together pays a little more gold.</summary>
    public const float CoopGoldMul = 1.2f;

    public static WaveParams ForCoop(WaveParams w) => ForCoop(w, CoopPlayers);

    public static WaveParams ForCoop(WaveParams w, int players)
    {
        int extra = Mathf.Clamp(players, 1, 4) - 1;
        w.maxAlive = Mathf.Min(Mathf.RoundToInt(w.maxAlive * (1f + CoopAlivePerPlayer * extra)), CoopAliveCap);
        w.spawnInterval /= 1f + CoopIntervalPerPlayer * extra;
        w.healthMultiplier *= 1f + CoopHealthPerPlayer * extra;
        if (CoopSpecials) return w;
        w.specialChance = 0f;
        w.specialWave = false;
        w.maxShield = w.maxBomb = w.maxTongue = 0;
        return w;
    }

    public static float GoldMul(RunMode m) => m == RunMode.Hard ? HardGoldMul : m == RunMode.Coop ? CoopGoldMul : 1f;
    public static float XpMul(RunMode m) => m == RunMode.Hard ? HardXpMul : 1f;
}
