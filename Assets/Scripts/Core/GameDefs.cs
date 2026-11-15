using UnityEngine;

public static class GameLayers
{
    public const int Blocker = 8;    // buildings, containers, burning barrels: not vaultable
    public const int Vaultable = 9;  // crates, cars, barricades: the player vaults these
    public const int Zombie = 10;
    public const int Player = 11;

    public const int BlockerMask = 1 << Blocker;
    public const int VaultableMask = 1 << Vaultable;
    public const int ZombieMask = 1 << Zombie;
    public const int ObstacleMask = BlockerMask | VaultableMask;
    // Bullets fly at chest height: buildings/containers stop them, low cover does not.
    public const int BulletMask = BlockerMask | ZombieMask;
}

public static class Vault
{
    /// <summary>
    /// Finds a landing point on the far side of a vaultable obstacle in front of <paramref name="pos"/>.
    /// Only when <paramref name="dir"/> is within <paramref name="maxAngle"/> degrees of the obstacle's face normal.
    /// </summary>
    public static bool TryCompute(Vector3 pos, Vector3 dir, float radius, float probe, out Vector3 land, out float top, float maxAngle = 60f)
    {
        land = default; top = 0f;
        Vector3 origin = pos + Vector3.up * 0.45f;
        if (!Physics.SphereCast(origin, radius * 0.8f, dir, out var hit, probe, GameLayers.VaultableMask, QueryTriggerInteraction.Ignore))
            return false;
        Vector3 n = hit.normal; n.y = 0f;
        if (n.sqrMagnitude < 0.01f || Vector3.Dot(-n.normalized, dir) < Mathf.Cos(maxAngle * Mathf.Deg2Rad)) return false;

        var col = hit.collider;
        Vector3 far = hit.point + dir * 8f; far.y = 0.45f;
        if (!col.Raycast(new Ray(far, -dir), out var exit, 9f)) return false;

        land = exit.point + dir * (radius + 0.2f);
        land.y = pos.y;
        if (Physics.CheckCapsule(land + Vector3.up * (radius + 0.05f), land + Vector3.up * 1.4f, radius,
                GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) return false;
        top = col.bounds.max.y;
        return true;
    }
}

/// <summary>Tuning for one wave. Everything scales with the wave number — no time-based walls.</summary>
public struct WaveParams
{
    public int wave;
    public float duration;
    public float spawnInterval;
    public int maxAlive;
    public int burst;
    public float healthMultiplier;
    public float speedMultiplier;
    public float runnerChance;
    public float contactDamage;
    /// <summary>Chance that a spawn is a special (lab mutation) zombie; 0 before wave 3.</summary>
    public float specialChance;
    /// <summary>Every few waves: a "special wave" with many more mutations.</summary>
    public bool specialWave;
    public int maxShield, maxBomb, maxTongue;
}

public static class Difficulty
{
    // Balanced with PlaytestBot (see docs/balance). Tweak here, re-run the bots.
    public static float BaseInterval = 1.3f;
    public static float IntervalDecay = 0.83f;
    public static float MinInterval = 0.3f;
    public static int BaseAlive = 12;
    public static int AlivePerWave = 6;
    public static int MaxAliveCap = 70;
    public static float HealthPerWave = 0.18f;
    public static float SpeedPerWave = 0.035f;
    public static float BaseDamage = 9f;
    public static float DamagePerWave = 1.2f;

    public static WaveParams ForWave(int n)
    {
        int k = Mathf.Max(0, n - 1);
        return new WaveParams
        {
            wave = n,
            duration = Mathf.Min(30f + k * 3f, 45f),
            spawnInterval = Mathf.Max(MinInterval, BaseInterval * Mathf.Pow(IntervalDecay, k)),
            maxAlive = Mathf.Min(BaseAlive + AlivePerWave * k, MaxAliveCap),
            burst = 1 + k / 2,
            healthMultiplier = 1f + HealthPerWave * k,
            speedMultiplier = Mathf.Min(1f + SpeedPerWave * k, 1.45f),
            runnerChance = Mathf.Clamp((n - 1) * 0.08f, 0f, 0.45f),
            contactDamage = BaseDamage + DamagePerWave * k,
            specialChance = SpecialChance(n),
            specialWave = IsSpecialWave(n),
            maxShield = SpecialLimit(n, 1, 3, 4),
            maxBomb = SpecialLimit(n, 2, 2, 6),
            maxTongue = SpecialLimit(n, 1, 4, 3),
        };
    }

    // Special zombies: mixed in from wave 3, more every wave; every 5th wave is a special wave.
    public static int FirstSpecialWave = 3;
    public static float SpecialBase = 0.05f;
    public static float SpecialPerWave = 0.025f;
    public static float SpecialMax = 0.25f;
    public static int SpecialWaveEvery = 7;

    public static bool IsSpecialWave(int n) => n >= SpecialWaveEvery && n % SpecialWaveEvery == 0;

    static float SpecialChance(int n)
    {
        if (n < FirstSpecialWave) return 0f;
        float c = Mathf.Min(SpecialBase + SpecialPerWave * (n - FirstSpecialWave), SpecialMax);
        return IsSpecialWave(n) ? Mathf.Min(c * 2.5f, 0.55f) : c;
    }

    static int SpecialLimit(int n, int start, int wavesPerExtra, int cap)
    {
        if (n < FirstSpecialWave) return 0;
        int l = Mathf.Min(start + (n - FirstSpecialWave) / wavesPerExtra, cap);
        return IsSpecialWave(n) ? l * 2 : l;
    }
}
