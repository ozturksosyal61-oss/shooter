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
    /// <summary>Finds a landing point on the far side of a vaultable obstacle in front of <paramref name="pos"/>.</summary>
    public static bool TryCompute(Vector3 pos, Vector3 dir, float radius, float probe, out Vector3 land, out float top)
    {
        land = default; top = 0f;
        Vector3 origin = pos + Vector3.up * 0.45f;
        if (!Physics.SphereCast(origin, radius * 0.8f, dir, out var hit, probe, GameLayers.VaultableMask, QueryTriggerInteraction.Ignore))
            return false;
        Vector3 n = hit.normal; n.y = 0f;
        if (n.sqrMagnitude < 0.01f || Vector3.Dot(-n.normalized, dir) < 0.5f) return false;

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

/// <summary>Difficulty as a function of survival time (seconds). Ramps hard: ~5 minutes is very dense.</summary>
public static class Difficulty
{
    static float Ramp(float t, float seconds) => Mathf.Pow(Mathf.Clamp01(t / seconds), 0.8f);

    public static float SpawnInterval(float t) => Mathf.Lerp(1.3f, 0.12f, Ramp(t, 240f));
    public static int MaxAlive(float t) => Mathf.RoundToInt(Mathf.Lerp(10f, 90f, Ramp(t, 300f)));
    public static float HealthMultiplier(float t) => 1f + t / 120f;                 // +50% per minute
    public static float SpeedMultiplier(float t) => 1f + Mathf.Min(t / 300f, 0.6f);  // up to +60%
    public static float RunnerChance(float t) => Mathf.Clamp01((t - 40f) / 240f) * 0.4f;
    public static int BurstSize(float t) => 1 + Mathf.FloorToInt(t / 60f) + (Random.value < 0.3f ? 1 : 0);
    public static float ContactDamage(float t) => 12f + t / 30f;
}
