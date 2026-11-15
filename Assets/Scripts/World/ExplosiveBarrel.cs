using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Red fuel barrel placed by the city generator. Shooting it (or a nearby blast) sets it off: area damage to
/// zombies AND the player, knockback, heavy shake, sound with music duck. Chains to other barrels in range.
/// Pooled with the chunk: <see cref="ResetBarrel"/> when it is placed again.
/// </summary>
public class ExplosiveBarrel : MonoBehaviour
{
    public static readonly List<ExplosiveBarrel> Active = new List<ExplosiveBarrel>(32);

    public float health = 40f;
    public float radius = 4.5f;
    public float zombieDamage = 220f;
    public float playerDamage = 40f;
    public float chainDelay = 0.18f;
    public GameObject model;
    public Collider[] colliders;
    public HitFlash flash;

    float hp, fuse = -1f;
    bool exploded;

    public bool Exploded => exploded;

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
        exploded = false;
        if (model) model.SetActive(true);
        if (colliders != null) foreach (var c in colliders) c.enabled = true;
    }

    public void Hit(float damage)
    {
        if (exploded) return;
        hp -= damage;
        if (flash) flash.Flash(0.06f);
        if (hp <= 0f) Explode();
    }

    void OnExplosion(Vector3 pos, float r)
    {
        if (exploded || fuse >= 0f || !gameObject.activeInHierarchy) return;
        Vector3 d = transform.position - pos; d.y = 0f;
        if (d.sqrMagnitude < 0.04f) return; // its own blast
        if (d.sqrMagnitude <= (r + 0.6f) * (r + 0.6f)) fuse = chainDelay;
    }

    void Update()
    {
        if (fuse < 0f || exploded) return;
        fuse -= Time.deltaTime;
        if (fuse <= 0f) Explode();
    }

    public void Explode()
    {
        if (exploded) return;
        exploded = true;
        fuse = -1f;
        if (model) model.SetActive(false);
        if (colliders != null) foreach (var c in colliders) c.enabled = false;
        Physics.SyncTransforms();
        Explosions.Explode(transform.position, radius, zombieDamage, playerDamage, SfxId.BarrelExplode, 1f);
    }
}
