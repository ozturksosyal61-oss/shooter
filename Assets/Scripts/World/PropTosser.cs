using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flings pooled street props (cars, crates, barricades) out of a charging boss's way: ballistic arc + tumble,
/// colliders off while airborne, dust/sparks on landing. The prop stays where it lands until its chunk is recycled
/// (ChunkManager resets the transform on reuse). Fixed-size slot array — no allocations per toss.
/// </summary>
public class PropTosser : MonoBehaviour
{
    public static PropTosser I { get; private set; }

    public int maxTosses = 12;
    public float gravity = 26f;
    public float maxFlight = 1.6f;

    struct Flight
    {
        public Transform t;
        public Transform parent;
        public Vector3 vel, axis;
        public float spin, time;
    }

    Flight[] tosses;
    List<Collider>[] cols;
    int count;

    public int Active => count;
    public int TotalTossed { get; private set; }

    void Awake()
    {
        I = this;
        tosses = new Flight[maxTosses];
        cols = new List<Collider>[maxTosses];
        for (int i = 0; i < maxTosses; i++) cols[i] = new List<Collider>(8);
    }

    public bool IsFlying(Transform t)
    {
        for (int i = 0; i < count; i++) if (tosses[i].t == t) return true;
        return false;
    }

    /// <summary>Throws <paramref name="prop"/> along <paramref name="dir"/> (speed of the hit).</summary>
    public void Toss(Transform prop, Vector3 dir, float speed)
    {
        if (!prop || count >= maxTosses || IsFlying(prop)) return;
        dir.y = 0f;
        dir.Normalize();
        var list = cols[count];
        prop.GetComponentsInChildren(false, list);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i].enabled) { list.RemoveAt(i); continue; } // leave deliberately disabled colliders alone
            list[i].enabled = false;
        }
        Vector3 side = Vector3.Cross(Vector3.up, dir) * Random.Range(-0.6f, 0.6f);
        float k = Mathf.Clamp(speed * 0.55f, 4f, 9f);
        tosses[count++] = new Flight
        {
            t = prop,
            parent = prop.parent,
            vel = (dir + side) * k + Vector3.up * Random.Range(5f, 8f),
            axis = Vector3.Cross(Vector3.up, dir + side * 0.5f).normalized,
            spin = Random.Range(260f, 480f),
            time = 0f,
        };
        TotalTossed++;
        Vector3 p = prop.position;
        if (Fx.I) { Fx.I.Spark(p + Vector3.up * 0.8f, -dir); Fx.I.Dust(p); Fx.I.Death(p + Vector3.up * 0.6f, dir); }
        GameAudio.Play(SfxId.HitMetal, p);
        GameAudio.Play(SfxId.BossSlam, p, 1.5f, 0.5f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = count - 1; i >= 0; i--)
        {
            ref var s = ref tosses[i];
            // Recycled by its chunk mid-flight: just give the colliders back.
            if (!s.t || !s.t.gameObject.activeInHierarchy || s.t.parent != s.parent) { Finish(i, false); continue; }
            s.time += dt;
            s.vel.y -= gravity * dt;
            Vector3 p = s.t.position + s.vel * dt;
            s.t.rotation = Quaternion.AngleAxis(s.spin * dt, s.axis) * s.t.rotation;
            s.spin *= Mathf.Exp(-0.8f * dt);
            if ((p.y <= 0f && s.vel.y < 0f) || s.time > maxFlight)
            {
                p.y = 0f;
                s.t.position = p;
                Finish(i, true);
                continue;
            }
            s.t.position = p;
        }
    }

    void Finish(int i, bool landed)
    {
        var s = tosses[i];
        if (s.t && landed)
        {
            // Lands back on its wheels/base (pivot is bottom-centre), turned to wherever the tumble left it.
            s.t.rotation = Quaternion.Euler(0f, s.t.eulerAngles.y, 0f);
            if (Fx.I) { Fx.I.Dust(s.t.position); Fx.I.Spark(s.t.position + Vector3.up * 0.3f, Vector3.up); }
            GameAudio.Play(SfxId.HitMetal, s.t.position, 0.8f);
        }
        var list = cols[i];
        for (int c = 0; c < list.Count; c++) if (list[c]) list[c].enabled = true;
        list.Clear();
        // Swap-remove (keep the collider list with its slot).
        int last = count - 1;
        tosses[i] = tosses[last];
        (cols[i], cols[last]) = (cols[last], cols[i]);
        tosses[last] = default;
        count--;
    }
}
