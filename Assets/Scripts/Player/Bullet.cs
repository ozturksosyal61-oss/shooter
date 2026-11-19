using UnityEngine;

/// <summary>Pooled projectile with a stretched tracer; hit detection by sphere-cast along the travelled segment.</summary>
public class Bullet : MonoBehaviour
{
    const float Radius = 0.14f;

    public Transform tracer;
    public float tracerLength = 2.4f;

    Vector3 vel;
    Vector3 castFrom;
    float life, damage, travelled;
    bool crit, first, cosmetic;
    int pierceLeft;
    Pool<Bullet> pool;

    public void Launch(Vector3 pos, Vector3 castOrigin, Vector3 dir, float speed, float dmg, bool isCrit, Pool<Bullet> p,
        float lifetime = 0.6f, int pierce = 0, bool cosmeticOnly = false)
    {
        cosmetic = cosmeticOnly;
        pierceLeft = pierce;
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
        // First cast starts from the shooter so zombies hugging the muzzle still get hit.
        castFrom = castOrigin;
        first = true;
        vel = dir * speed;
        life = lifetime;
        damage = dmg;
        crit = isCrit;
        pool = p;
        travelled = 0f;
        UpdateTracer();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 from = first ? castFrom : transform.position;
        first = false;
        Vector3 next = transform.position + vel * dt;
        Vector3 seg = next - from;
        float dist = seg.magnitude;
        if (dist > 0.0001f &&
            Physics.SphereCast(from, Radius, seg / dist, out var hit, dist, GameLayers.BulletMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = vel.normalized;
            if (cosmetic)
            {
                // A teammate's tracer: stops where it hits, no damage here.
                if (!hit.collider.TryGetComponent(out Zombie _) && Fx.I) Fx.I.Spark(hit.point, hit.normal);
                pool.Release(this);
                return;
            }
            if (hit.collider.TryGetComponent(out Zombie z))
            {
                // Pierce: keep flying past a zombie we actually damaged (a shield stops the bullet).
                if (z.TakeHit(damage, crit, dir, hit.point) && pierceLeft > 0)
                {
                    pierceLeft--;
                    damage *= 0.8f;
                    transform.position = hit.point + dir * 0.9f;
                    travelled += dist;
                    UpdateTracer();
                    return;
                }
            }
            else if (hit.collider.TryGetComponent(out TongueHitbox th)) th.owner.HitTongue(damage, crit, dir, hit.point);
            else
            {
                if (Fx.I) Fx.I.Spark(hit.point, hit.normal);
                var prop = hit.collider.GetComponentInParent<EnvProp>();
                var barrel = prop ? prop.GetComponent<ExplosiveBarrel>() : null;
                if (barrel) barrel.Hit(damage);
                GameAudio.Play(prop && prop.surface == Surface.Metal ? SfxId.HitMetal : SfxId.HitConcrete, hit.point);
            }
            pool.Release(this);
            return;
        }
        travelled += (next - transform.position).magnitude;
        transform.position = next;
        UpdateTracer();
        life -= dt;
        if (life <= 0f) pool.Release(this);
    }

    void UpdateTracer()
    {
        if (!tracer) return;
        // The tracer grows from the muzzle until it reaches its full length.
        float len = Mathf.Clamp(travelled, 0.3f, tracerLength);
        tracer.localScale = new Vector3(crit ? 0.22f : 0.15f, len, 1f);
        tracer.localPosition = new Vector3(0f, 0f, -len * 0.5f);
    }
}
