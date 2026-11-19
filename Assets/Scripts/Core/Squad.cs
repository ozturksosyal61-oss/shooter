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
