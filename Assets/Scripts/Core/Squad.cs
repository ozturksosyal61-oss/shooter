using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Everyone zombies can chase: the local soldier and (co-op) teammates' avatars. Single player: just the local one.
/// Pure helpers (nearest, leash) are static and EditMode-tested.
/// </summary>
public static class Squad
{
    public static readonly List<SquadMember> Members = new List<SquadMember>(4);

    public static int Count => Members.Count;

    public static bool AnyAlive
    {
        get { for (int i = 0; i < Members.Count; i++) if (!Members[i].IsDead) return true; return false; }
    }

    /// <summary>Nearest living member (null when nobody is registered or everyone is down).</summary>
    public static SquadMember Nearest(Vector3 p)
    {
        SquadMember best = null;
        float bd = float.MaxValue;
        for (int i = 0; i < Members.Count; i++)
        {
            var m = Members[i];
            if (m.IsDead) continue;
            Vector3 d = m.Position - p; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bd) { bd = sq; best = m; }
        }
        return best;
    }

    /// <summary>Squared ground distance to the nearest member (any state) — straggler recycling.</summary>
    public static float NearestSqr(Vector3 p, float fallback)
    {
        if (Members.Count == 0) return fallback;
        float bd = float.MaxValue;
        for (int i = 0; i < Members.Count; i++)
        {
            Vector3 d = Members[i].Position - p; d.y = 0f;
            bd = Mathf.Min(bd, d.sqrMagnitude);
        }
        return bd;
    }

    /// <summary>
    /// Co-op leash: the part of a move that would take <paramref name="from"/> beyond <paramref name="leash"/> of a
    /// teammate is removed (moving back toward them is always allowed).
    /// </summary>
    public static Vector3 Leash(Vector3 from, Vector3 move, Vector3 mate, float leash)
    {
        Vector3 to = from + move;
        Vector3 d = to - mate; d.y = 0f;
        if (d.sqrMagnitude <= leash * leash) return move;
        Vector3 n = d.normalized;
        float outward = Vector3.Dot(move, n);
        return outward > 0f ? move - n * outward : move;
    }

    /// <summary>
    /// Host-authoritative area hit on every standing soldier (local or teammate avatar): damage falls off by
    /// <paramref name="falloff"/> toward the edge; knockback likewise when <paramref name="scaleKnock"/>.
    /// Vaulting soldiers dodge when <paramref name="vaultDodges"/>. Returns how many were hit.
    /// </summary>
    public static int HitArea(Vector3 c, float radius, float damage, float falloff, float knock, bool scaleKnock, bool vaultDodges, Vector3 fallbackDir)
    {
        int hits = 0;
        for (int i = Members.Count - 1; i >= 0; i--)
        {
            var m = Members[i];
            if (m.IsDead || (vaultDodges && m.IsVaulting)) continue;
            Vector3 d = m.Position - c; d.y = 0f;
            float dist = d.magnitude;
            if (dist >= radius) continue;
            float f = 1f - falloff * Mathf.Clamp01(dist / radius);
            m.TakeDamage(damage * f, dist > 0.05f ? d / dist : fallbackDir, scaleKnock ? knock * f : knock);
            hits++;
        }
        return hits;
    }

    /// <summary>Damage over time + slow for everyone standing in a circle (acid pools).</summary>
    public static float BurnArea(Vector3 c, float radius, float amount, float slowMul, float slowSeconds)
    {
        float dealt = 0f;
        for (int i = Members.Count - 1; i >= 0; i--)
        {
            var m = Members[i];
            if (m.IsDead) continue;
            Vector3 d = m.Position - c; d.y = 0f;
            if (d.sqrMagnitude >= radius * radius) continue;
            m.Burn(amount);
            if (slowMul < 1f) m.Slow(slowMul, slowSeconds);
            dealt += amount;
        }
        return dealt;
    }

    /// <summary>Slow for everyone in a circle (boss shock ring).</summary>
    public static void SlowArea(Vector3 c, float radius, float mul, float seconds)
    {
        for (int i = Members.Count - 1; i >= 0; i--)
        {
            var m = Members[i];
            if (m.IsDead) continue;
            Vector3 d = m.Position - c; d.y = 0f;
            if (d.sqrMagnitude < radius * radius) m.Slow(mul, seconds);
        }
    }

    /// <summary>The local soldier's member (null before the player exists).</summary>
    public static SquadMember Local
    {
        get { for (int i = 0; i < Members.Count; i++) if (Members[i].IsLocal) return Members[i]; return null; }
    }

    public static Vector3 LeashAll(SquadMember self, Vector3 move, float leash)
    {
        if (Members.Count < 2 || !self) return move;
        Vector3 from = self.Position;
        for (int i = 0; i < Members.Count; i++)
        {
            var m = Members[i];
            if (m == self || m.IsDead) continue;
            move = Leash(from, move, m.Position, leash);
        }
        return move;
    }
}
