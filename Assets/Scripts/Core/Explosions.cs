using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Area damage shared by bomb zombies, explosive barrels, grenades and the kill-explosion card:
/// hurts zombies (explosions ignore shields) and every soldier of the squad (falloff + knockback; co-op teammates get it
/// over the network), chains barrels, and drives the big feedback (particles, sound + music duck + haptic via the sound
/// bank, shake, hitstop). Co-op: the host's blasts are replayed on the clients by <see cref="Visual"/> (no damage there);
/// a client's own blasts (cards) only report their zombie hits to the host.
/// </summary>
public static class Explosions
{
    // One target list per nesting level: a bomb killed by a blast explodes from inside the loop.
    static readonly List<Zombie>[] Targets = { new List<Zombie>(64), new List<Zombie>(64), new List<Zombie>(64), new List<Zombie>(64) };
    static int depth;

    /// <summary>Raised for every explosion (position, radius) — barrels listen to chain.</summary>
    public static event System.Action<Vector3, float> OnExplosion;
    /// <summary>Host (co-op): a blast happened — sent to the clients to show.</summary>
    public static event System.Action<Vector3, float, SfxId, float, bool> Broadcast;

    /// <summary>Co-op client: the host's blast — particles, sound, shake only.</summary>
    public static void Visual(Vector3 pos, float radius, SfxId sfx, float shake, bool big)
    {
        pos.y = 0f;
        if (Fx.I) Fx.I.Explosion(pos, radius, big);
        GameAudio.Play(sfx, pos);
        var gm = GameManager.I;
        if (!gm || !gm.player) return;
        // Shake by distance: a far blast is a rumble.
        Vector3 d = gm.player.transform.position - pos; d.y = 0f;
        gm.Shake(shake * Mathf.Clamp01(1.2f - d.magnitude / 30f));
    }

    public static void Explode(Vector3 pos, float radius, float zombieDamage, float playerDamage, SfxId sfx,
        float shake = 0.8f, Zombie ignore = null, bool big = true)
    {
        pos.y = 0f;
        if (Fx.I) Fx.I.Explosion(pos, radius, big);
        GameAudio.Play(sfx, pos);
        var gm = GameManager.I;
        if (gm)
        {
            gm.Shake(shake);
            if (big) gm.HitStop(0.05f);
        }

        if (depth < Targets.Length)
        {
            var list = Targets[depth++];
            list.Clear();
            float r2 = radius * radius;
            foreach (var z in Zombie.Active)
            {
                if (z == ignore || !z.Damageable) continue;
                Vector3 d = z.transform.position - pos; d.y = 0f;
                if (d.sqrMagnitude <= r2) list.Add(z);
            }
            foreach (var z in list)
            {
                if (!z.Damageable) continue;
                Vector3 d = z.transform.position - pos; d.y = 0f;
                float m = d.magnitude;
                Vector3 dir = m > 0.05f ? d / m : Random.insideUnitSphere;
                dir.y = 0f;
                float k = 1f - 0.5f * Mathf.Clamp01(m / radius);
                z.TakeHit(zombieDamage * k, false, dir.normalized, z.AimPoint, true);
                z.Knock(dir.normalized * 9f * k);
            }
            list.Clear();
            depth--;
        }

        if (playerDamage > 0f && !CoopNet.IsClient)
        {
            if (Squad.Count > 0) Squad.HitArea(pos, radius, playerDamage, 0.6f, 14f, true, false, Vector3.forward);
            else
            {
                var p = gm ? gm.player : null;
                if (p && !p.IsDead)
                {
                    Vector3 d = p.transform.position - pos; d.y = 0f;
                    float m = d.magnitude;
                    if (m < radius)
                    {
                        float k = 1f - 0.6f * Mathf.Clamp01(m / radius);
                        p.TakeDamage(playerDamage * k, m > 0.05f ? d / m : Vector3.forward, 14f * k);
                    }
                }
            }
        }
        if (!CoopNet.IsClient) Broadcast?.Invoke(pos, radius, sfx, shake, big);
        OnExplosion?.Invoke(pos, radius);
    }
}
