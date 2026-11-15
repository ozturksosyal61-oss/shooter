using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Deployable sentry (card: Taret). Sits for a limited time, turns its head to the nearest zombie in range and
/// fires pooled bullets through the player's weapon pool. Zombies that are closer to it than to the player attack
/// it; it has a health bar and a radial timer above it, blinks before it runs out and bursts in a small explosion
/// when its time or health is gone.
/// </summary>
public class Turret : MonoBehaviour
{
    /// <summary>Deployed turrets (zombies pick targets from here).</summary>
    public static readonly List<Turret> Active = new List<Turret>(2);

    public Transform head;
    public Transform muzzle;
    public Renderer[] blinkRenderers;
    [Tooltip("World-space status canvas (billboarded): health fill + radial timer.")]
    public Transform status;
    public Image healthFill;
    public Image timerFill;
    public float range = 12f;
    public float turnSpeed = 540f;
    public float maxHealth = 160f;
    [Tooltip("Zombies stop this far from the turret's centre to hit it.")]
    public float radius = 0.45f;

    float lifeLeft, life, fireRate, damage, cooldown, health, hurtT;
    Weapon weapon;
    Zombie target;
    float retargetT;
    Camera cam;

    public float LifeLeft => lifeLeft;
    public float Health => health;
    public bool IsDeployed => gameObject.activeSelf;

    public void Deploy(Vector3 pos, float lifetime, float rate, float dmg, float hp, Weapon w)
    {
        transform.SetPositionAndRotation(pos, Quaternion.identity);
        lifeLeft = life = lifetime;
        fireRate = rate;
        damage = dmg;
        weapon = w;
        maxHealth = hp;
        health = hp;
        cooldown = 0.4f;
        target = null;
        hurtT = 0f;
        gameObject.SetActive(true);
        SetVisible(true);
        if (!Active.Contains(this)) Active.Add(this);
        if (Fx.I) Fx.I.Dust(pos);
        GameAudio.Play(SfxId.VaultLand, pos);
        UpdateStatus();
    }

    void OnDisable() => Active.Remove(this);

    /// <summary>Hit by a zombie.</summary>
    public void TakeDamage(float amount)
    {
        if (!gameObject.activeSelf || health <= 0f) return;
        health -= amount;
        hurtT = 0.12f;
        GameAudio.Play(SfxId.HitMetal, transform.position + Vector3.up * 0.8f, 0.9f, 0.8f);
        if (Fx.I) Fx.I.Spark(transform.position + Vector3.up * 0.9f, Vector3.up);
        if (health <= 0f) Burst();
    }

    /// <summary>Small visual explosion (no damage) and fold away.</summary>
    void Burst()
    {
        Vector3 p = transform.position;
        if (Fx.I) Fx.I.Explosion(p, 1.1f, false);
        GameAudio.Play(SfxId.Explosion, p, 1.25f, 0.55f);
        if (GameManager.I) GameManager.I.Shake(0.15f);
        gameObject.SetActive(false);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        lifeLeft -= dt;
        if (GameManager.I && GameManager.I.IsGameOver) { gameObject.SetActive(false); return; }
        if (lifeLeft <= 0f) { Burst(); return; }
        // Blink during the last 3 s; flash white-ish when hit.
        hurtT -= dt;
        if (lifeLeft < 3f) SetVisible(Mathf.Repeat(lifeLeft * 5f, 1f) > 0.3f);
        UpdateStatus();

        retargetT -= dt;
        if (retargetT <= 0f || !target || !target.Targetable)
        {
            retargetT = 0.25f;
            target = Zombie.FindNearest(transform.position, range);
        }
        if (!target) return;

        Vector3 to = target.AimPoint - muzzle.position; to.y = 0f;
        if (to.sqrMagnitude < 0.01f) return;
        head.rotation = Quaternion.RotateTowards(head.rotation, Quaternion.LookRotation(to), turnSpeed * dt);
        cooldown -= dt;
        if (cooldown <= 0f && Vector3.Angle(head.forward, to) < 12f)
        {
            cooldown = 1f / fireRate;
            Vector3 d = Quaternion.Euler(0f, Random.Range(-2.5f, 2.5f), 0f) * to.normalized;
            Vector3 castFrom = transform.position + Vector3.up * muzzle.position.y;
            weapon.SpawnBullet(muzzle.position, castFrom, d, 50f, damage, Random.value < 0.08f, 0.45f, 0);
            if (Fx.I) Fx.I.Muzzle(muzzle.position, d);
            GameAudio.Play(SfxId.TurretShot, muzzle.position);
        }
    }

    void UpdateStatus()
    {
        if (!status) return;
        if (!cam) cam = Camera.main;
        if (cam) status.rotation = cam.transform.rotation;
        float h = Mathf.Clamp01(health / maxHealth);
        if (healthFill)
        {
            healthFill.fillAmount = h;
            healthFill.color = hurtT > 0f ? Color.white : Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.4f, 0.95f, 0.35f), h);
        }
        if (timerFill)
        {
            timerFill.fillAmount = Mathf.Clamp01(lifeLeft / Mathf.Max(0.01f, life));
            timerFill.color = lifeLeft < 3f ? new Color(1f, 0.35f, 0.25f) : new Color(1f, 0.6f, 0.15f);
        }
    }

    void SetVisible(bool v)
    {
        if (blinkRenderers == null) return;
        foreach (var r in blinkRenderers) if (r.enabled != v) r.enabled = v;
    }

    /// <summary>Nearest deployed turret within <paramref name="maxDist"/> of <paramref name="from"/> (or null).</summary>
    public static Turret Nearest(Vector3 from, float maxDist, out float dist)
    {
        Turret best = null;
        dist = maxDist;
        for (int i = 0; i < Active.Count; i++)
        {
            var t = Active[i];
            Vector3 d = t.transform.position - from; d.y = 0f;
            float m = d.magnitude;
            if (m < dist) { dist = m; best = t; }
        }
        return best;
    }
}
