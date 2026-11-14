using UnityEngine;

public static class GameLayers
{
    public const int Wall = 8;
    public const int Crate = 9;
    public const int Zombie = 10;
    public const int Player = 11;

    public const int WallMask = 1 << Wall;
    public const int CrateMask = 1 << Crate;
    public const int ZombieMask = 1 << Zombie;
    public const int ObstacleMask = WallMask | CrateMask;
    // Bullets fly at chest height, above crates, so only walls + zombies block them.
    public const int BulletMask = WallMask | ZombieMask;
}

public static class Arena
{
    public const float HalfX = 20f;
    public const float HalfZ = 14f;

    public static Vector3 Clamp(Vector3 p, float margin)
    {
        p.x = Mathf.Clamp(p.x, -HalfX + margin, HalfX - margin);
        p.z = Mathf.Clamp(p.z, -HalfZ + margin, HalfZ - margin);
        return p;
    }

    public static bool Inside(Vector3 p, float margin) =>
        p.x > -HalfX + margin && p.x < HalfX - margin && p.z > -HalfZ + margin && p.z < HalfZ - margin;
}

public static class Vault
{
    /// <summary>Finds a landing point on the far side of a crate in front of <paramref name="pos"/>.</summary>
    public static bool TryCompute(Vector3 pos, Vector3 dir, float radius, float probe, out Vector3 land, out float top)
    {
        land = default; top = 0f;
        Vector3 origin = pos + Vector3.up * 0.45f;
        if (!Physics.SphereCast(origin, radius * 0.8f, dir, out var hit, probe, GameLayers.CrateMask, QueryTriggerInteraction.Ignore))
            return false;
        Vector3 n = hit.normal; n.y = 0f;
        if (n.sqrMagnitude < 0.01f || Vector3.Dot(-n.normalized, dir) < 0.5f) return false;

        var col = hit.collider;
        Vector3 far = hit.point + dir * 6f; far.y = 0.45f;
        if (!col.Raycast(new Ray(far, -dir), out var exit, 7f)) return false;

        land = exit.point + dir * (radius + 0.2f);
        land.y = pos.y;
        if (!Arena.Inside(land, radius + 0.1f)) return false;
        if (Physics.CheckCapsule(land + Vector3.up * (radius + 0.05f), land + Vector3.up * 1.4f, radius,
                GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) return false;
        top = col.bounds.max.y;
        return true;
    }
}
