using UnityEngine;

public class Bullet : MonoBehaviour
{
    const float Radius = 0.14f;

    Vector3 vel;
    Vector3 castFrom;
    float life, damage;
    Pool<Bullet> pool;

    public void Launch(Vector3 pos, Vector3 castOrigin, Vector3 dir, float speed, float dmg, Pool<Bullet> p)
    {
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
        // First cast starts from the shooter so zombies hugging the muzzle still get hit.
        castFrom = castOrigin;
        vel = dir * speed;
        life = 0.6f;
        damage = dmg;
        pool = p;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 next = transform.position + vel * dt;
        Vector3 seg = next - castFrom;
        float dist = seg.magnitude;
        if (dist > 0.0001f &&
            Physics.SphereCast(castFrom, Radius, seg / dist, out var hit, dist, GameLayers.BulletMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 dir = vel.normalized;
            if (hit.collider.TryGetComponent(out Zombie z)) z.TakeHit(damage, dir, hit.point);
            else if (Fx.I) Fx.I.Spark(hit.point, hit.normal);
            pool.Release(this);
            return;
        }
        transform.position = next;
        castFrom = next;
        life -= dt;
        if (life <= 0f) pool.Release(this);
    }
}
