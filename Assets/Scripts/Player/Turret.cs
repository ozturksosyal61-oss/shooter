using UnityEngine;

/// <summary>
/// Deployable sentry (card: Taret). Sits for a limited time, turns its head to the nearest zombie in range and
/// fires pooled bullets through the player's weapon pool. Blinks before it folds away.
/// </summary>
public class Turret : MonoBehaviour
{
    public Transform head;
    public Transform muzzle;
    public Renderer[] blinkRenderers;
    public float range = 12f;
    public float turnSpeed = 540f;

    float lifeLeft, fireRate, damage, cooldown;
    Weapon weapon;
    Zombie target;
    float retargetT;

    public float LifeLeft => lifeLeft;

    public void Deploy(Vector3 pos, float life, float rate, float dmg, Weapon w)
    {
        transform.SetPositionAndRotation(pos, Quaternion.identity);
        lifeLeft = life;
        fireRate = rate;
        damage = dmg;
        weapon = w;
        cooldown = 0.4f;
        target = null;
        gameObject.SetActive(true);
        SetVisible(true);
        if (Fx.I) Fx.I.Dust(pos);
        GameAudio.Play(SfxId.VaultLand, pos);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        lifeLeft -= dt;
        if (lifeLeft <= 0f || (GameManager.I && GameManager.I.IsGameOver))
        {
            if (Fx.I) Fx.I.Dust(transform.position);
            gameObject.SetActive(false);
            return;
        }
        // Blink during the last 3 s.
        if (lifeLeft < 3f) SetVisible(Mathf.Repeat(lifeLeft * 5f, 1f) > 0.3f);

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

    void SetVisible(bool v)
    {
        if (blinkRenderers == null) return;
        foreach (var r in blinkRenderers) if (r.enabled != v) r.enabled = v;
    }
}
