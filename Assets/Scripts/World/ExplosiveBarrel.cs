using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Red fuel barrel placed by the city generator. ~6 rifle rounds set it off (white flash per hit, smoke + sparks below
/// half health), or a nearby blast: ~3 m area damage (zombies high, soldiers medium), knockback, heavy shake, sound
/// with music duck. Chains to barrels close by after 0.2 s.
/// Pooled with the chunk: <see cref="ResetBarrel"/> when it is placed again.
/// </summary>
public class ExplosiveBarrel : MonoBehaviour
{
    public static readonly List<ExplosiveBarrel> Active = new List<ExplosiveBarrel>(32);

    public float health = 150f;
    public float radius = 3f;
    public float zombieDamage = 220f;
    public float playerDamage = 30f;
    public float chainDelay = 0.2f;
    [Tooltip("Barrels within radius + this get set off by a blast.")]
    public float chainReach = 0.8f;
    public GameObject model;
    public Collider[] colliders;
    public HitFlash flash;

    float hp, fuse = -1f, smokeT;
    bool exploded;

    public bool Exploded => exploded;
    public float Health01 => health > 0f ? Mathf.Clamp01(hp / health) : 0f;

    void Awake() => ResetBarrel();

    void OnEnable()
    {
        if (!Active.Contains(this)) Active.Add(this);
        Explosions.OnExplosion += OnExplosion;
    }

    void OnDisable()
    {
        Active.Remove(this);
        Explosions.OnExplosion -= OnExplosion;
    }

    public void ResetBarrel()
    {
        hp = health;
        fuse = -1f;
        smokeT = 0f;
        exploded = false;
        if (model) model.SetActive(true);
        if (colliders != null) foreach (var c in colliders) c.enabled = true;
    }

    public void Hit(float damage)
    {
        if (exploded) return;
        // Co-op client: the host's barrel is the real one (its blast comes back as an event).
        if (CoopNet.IsClient) { if (flash) flash.Flash(0.06f); CoopNet.I.ReportBarrelHit(transform.position, damage); return; }
        hp -= damage;
        if (flash) flash.Flash(0.06f);
        if (hp <= 0f) Explode();
    }

    void OnExplosion(Vector3 pos, float r)
    {
        if (exploded || fuse >= 0f || !gameObject.activeInHierarchy || CoopNet.IsClient) return;
        Vector3 d = transform.position - pos; d.y = 0f;
        if (d.sqrMagnitude < 0.04f) return; // its own blast
        if (d.sqrMagnitude <= (r + chainReach) * (r + chainReach)) fuse = chainDelay;
    }

    void Update()
    {
        if (exploded) return;
        // Damaged (below half): leaks smoke and sparks so it reads as "about to go".
        if ((hp < health * 0.5f || fuse >= 0f) && Fx.I)
        {
            smokeT -= Time.deltaTime;
            if (smokeT <= 0f)
            {
                smokeT = fuse >= 0f ? 0.05f : 0.22f;
                Vector3 top = transform.position + Vector3.up * 1.1f;
                Fx.I.Dust(top);
                if (Random.value < 0.6f) Fx.I.Spark(top + Random.insideUnitSphere * 0.25f, Vector3.up + Random.insideUnitSphere * 0.5f);
            }
        }
        if (fuse < 0f) return;
        fuse -= Time.deltaTime;
        if (fuse <= 0f) Explode();
    }

    /// <summary>Co-op client: the host's copy of this barrel blew up — just vanish (the blast is shown by the event).</summary>
    public void RemoteExploded()
    {
        if (exploded) return;
        exploded = true;
        fuse = -1f;
        if (model) model.SetActive(false);
        if (colliders != null) foreach (var c in colliders) c.enabled = false;
    }

    /// <summary>Nearest intact barrel within <paramref name="maxDist"/> of a point (co-op: barrels are matched by position).</summary>
    public static ExplosiveBarrel Near(Vector3 p, float maxDist)
    {
        ExplosiveBarrel best = null;
        float bd = maxDist * maxDist;
        foreach (var b in Active)
        {
            if (b.exploded) continue;
            Vector3 d = b.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = b; }
        }
        return best;
    }

    public void Explode()
    {
        if (exploded) return;
        if (CoopNet.IsClient) return;
        exploded = true;
        fuse = -1f;
        if (model) model.SetActive(false);
        if (colliders != null) foreach (var c in colliders) c.enabled = false;
        Physics.SyncTransforms();
        Explosions.Explode(transform.position, radius, zombieDamage, playerDamage, SfxId.BarrelExplode, 1f);
    }
}
