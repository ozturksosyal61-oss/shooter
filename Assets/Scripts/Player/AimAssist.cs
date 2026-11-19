using UnityEngine;

/// <summary>
/// Aim assist tuning — one fixed set for every player and every mode (fair scores / leaderboards; no user setting).
/// Drag on the right half: shoot where you drag, only a slight nudge toward a zombie within ±<see cref="DragCone"/>°.
/// Hold without dragging: shoot along the walk / facing direction, a soft magnet toward the nearest zombie within
/// ±<see cref="HoldCone"/>° of it, turning smoothly — never a full lock.
/// </summary>
public static class AimAssistConfig
{
    public const float Range = 18f;
    public const float DragCone = 5f;
    /// <summary>Share of the angle to the target that is corrected while dragging.</summary>
    public const float DragPull = 0.3f;
    public const float HoldCone = 8f;
    public const float HoldPull = 0.5f;
    /// <summary>Max aim turn (°/s) while holding without a drag.</summary>
    public const float HoldTurnSpeed = 300f;
}

/// <summary>Pure aim-assist math (EditMode-tested) + the zombie search.</summary>
public static class AimAssist
{
    /// <summary>
    /// Turns <paramref name="baseDir"/> by <paramref name="pull"/> × the angle to <paramref name="toTarget"/> when the target
    /// is inside ±<paramref name="coneDeg"/>; otherwise returns the base direction unchanged.
    /// </summary>
    public static Vector3 Bend(Vector3 baseDir, Vector3 toTarget, float coneDeg, float pull)
    {
        baseDir.y = 0f; toTarget.y = 0f;
        if (baseDir.sqrMagnitude < 1e-6f || toTarget.sqrMagnitude < 1e-6f) return baseDir.normalized;
        float a = Vector3.SignedAngle(baseDir, toTarget, Vector3.up);
        if (Mathf.Abs(a) > coneDeg) return baseDir.normalized;
        return (Quaternion.Euler(0f, a * Mathf.Clamp01(pull), 0f) * baseDir).normalized;
    }

    /// <summary>Nearest targetable zombie within <paramref name="range"/> and ±<paramref name="coneDeg"/> of <paramref name="dir"/>.</summary>
    public static Zombie InCone(Vector3 from, Vector3 dir, float coneDeg, float range)
    {
        Zombie best = null;
        float bd = range * range;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return null;
        var act = Zombie.Active;
        for (int i = 0; i < act.Count; i++)
        {
            var z = act[i];
            if (!z.Targetable) continue;
            Vector3 d = z.transform.position - from; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq >= bd || sq < 0.01f) continue;
            if (Vector3.Angle(dir, d) > coneDeg) continue;
            bd = sq;
            best = z;
        }
        return best;
    }

    /// <summary>Base direction with the assist of the given cone / pull applied.</summary>
    public static Vector3 Apply(Vector3 from, Vector3 baseDir, float coneDeg, float pull)
    {
        var z = InCone(from, baseDir, coneDeg, AimAssistConfig.Range);
        if (!z) return baseDir.normalized;
        return Bend(baseDir, z.transform.position - from, coneDeg, pull);
    }
}
