using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pooled zombie. Walks toward the player and steers around obstacles (never vaults —
/// obstacle-crossing is reserved for future bosses). Dies with a ragdoll-like topple.
/// </summary>
public class Zombie : MonoBehaviour
{
    public static readonly List<Zombie> Active = new List<Zombie>(128);

    public Transform model;
    public HitFlash flash;
    public CapsuleCollider body;
    public Renderer bodyRenderer;
    public Material walkerMaterial;
    public Material runnerMaterial;

    public float contactRange = 0.85f;
    public float contactCooldown = 0.9f;
    public float separationRadius = 0.75f;
    public float lookAhead = 1.4f;
    public float riseDuration = 0.55f;

    enum State { Rising, Walking, Dying }

    State state;
    float hp, speed, damage, stateT, swayPhase, attackCd, punch, lunge;
    bool runner;
    Vector3 knock;
    int avoidSide;          // sticky steering side (+1 / -1), 0 = none
    float avoidTimer;
    int activeIndex = -1;
    Pool<Zombie> pool;

    // death "ragdoll"
    Vector3 deathVel;
    Vector3 tumbleAxis;
    float tumbleAngle, tumbleVel, deathSpin;
    bool landed;

    static readonly Collider[] Buffer = new Collider[12];
    static readonly float[] SteerAngles = { 30f, 55f, 80f, 110f };

    public bool Targetable => state != State.Dying && (state != State.Rising || stateT > 0.35f);
    public Vector3 AimPoint => transform.position + Vector3.up * 1.1f;

    public void Spawn(Vector3 pos, float health, float moveSpeed, float contactDamage, bool isRunner, Pool<Zombie> owner)
    {
        pool = owner;
        hp = health;
        speed = moveSpeed;
        damage = contactDamage;
        runner = isRunner;
        knock = Vector3.zero;
        punch = lunge = 0f;
        attackCd = 0.5f;
        avoidSide = 0;
        swayPhase = Random.value * 10f;
        state = State.Rising;
        stateT = 0f;
        var p = GameManager.I.player;
        Vector3 look = p ? p.transform.position - pos : Vector3.forward; look.y = 0f;
        transform.SetPositionAndRotation(pos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        transform.localScale = Vector3.one * (runner ? 0.92f : Random.Range(0.97f, 1.06f));
        model.localPosition = Vector3.down * 1.9f;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        flash.SetBaseMaterial(runner ? runnerMaterial : walkerMaterial);
        body.enabled = true;
        activeIndex = Active.Count;
        Active.Add(this);
        if (Fx.I) Fx.I.Dust(pos);
        GameAudio.Play(SfxId.ZombieSpawn, pos);
    }

    public static Zombie FindNearest(Vector3 p, float range)
    {
        Zombie best = null;
        float bestD = range * range;
        for (int i = 0; i < Active.Count; i++)
        {
            var z = Active[i];
            if (!z.Targetable) continue;
            Vector3 d = z.transform.position - p; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestD) { bestD = sq; best = z; }
        }
        return best;
    }

    void Unregister()
    {
        if (activeIndex < 0) return;
        int last = Active.Count - 1;
        var moved = Active[last];
        Active[activeIndex] = moved;
        moved.activeIndex = activeIndex;
        Active.RemoveAt(last);
        activeIndex = -1;
    }

    /// <summary>Removes zombies that fell far behind the player (they will be respawned near the view).</summary>
    public void Despawn()
    {
        Unregister();
        pool.Release(this);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        stateT += dt;
        switch (state)
        {
            case State.Rising: UpdateRising(); break;
            case State.Walking: UpdateWalking(dt); Animate(dt); break;
            case State.Dying: UpdateDying(dt); break;
        }
    }

    void UpdateRising()
    {
        float t = Mathf.Clamp01(stateT / riseDuration);
        float e = 1f - (1f - t) * (1f - t) * (1f - t);
        model.localPosition = Vector3.down * 1.9f * (1f - e);
        model.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 25f) * 6f * (1f - t));
        if (t >= 1f) { state = State.Walking; stateT = 0f; }
    }

    void UpdateWalking(float dt)
    {
        var player = GameManager.I.player;
        Vector3 pos = transform.position;
        Vector3 to = player.transform.position - pos; to.y = 0f;
        float dist = to.magnitude;
        Vector3 dir = dist > 0.01f ? to / dist : transform.forward;

        Vector3 moveDir = dist > contactRange * 0.8f ? Steer(pos, dir, dt) : Vector3.zero;
        Vector3 desired = moveDir * speed;

        // Separation from other zombies.
        int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up * 0.8f, separationRadius, Buffer, GameLayers.ZombieMask, QueryTriggerInteraction.Ignore);
        Vector3 sep = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            if (Buffer[i] == body) continue;
            Vector3 v = pos - Buffer[i].transform.position; v.y = 0f;
            float m = v.magnitude;
            if (m < 0.001f) { v = Random.insideUnitSphere; v.y = 0f; m = 0.001f; }
            sep += v / m * (1f - Mathf.Clamp01(m / separationRadius));
        }
        desired += sep * speed * 1.6f;

        // Slide along anything we still touch.
        if (desired.sqrMagnitude > 0.0001f &&
            Physics.SphereCast(pos + Vector3.up * 0.5f, body.radius * 0.8f, desired.normalized, out var hit, body.radius + 0.15f,
                GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 nrm = hit.normal; nrm.y = 0f;
            if (nrm.sqrMagnitude > 0.001f) desired = Vector3.ProjectOnPlane(desired, nrm.normalized);
        }

        pos += (desired + knock) * dt;
        knock *= Mathf.Exp(-9f * dt);
        pos = Depenetrate(pos);
        pos.y = 0f;
        transform.position = pos;

        Vector3 face = moveDir.sqrMagnitude > 0.01f && dist > 2f ? moveDir : dir;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 300f * dt);

        attackCd -= dt;
        if (dist < contactRange && attackCd <= 0f && !player.IsDead)
        {
            attackCd = contactCooldown;
            lunge = 1f;
            GameAudio.Play(SfxId.ZombieAttack, pos);
            player.TakeDamage(damage, dir);
        }
    }

    /// <summary>Whisker steering: keep the direct line if clear, otherwise turn to a sticky side.</summary>
    Vector3 Steer(Vector3 pos, Vector3 dir, float dt)
    {
        Vector3 origin = pos + Vector3.up * 0.6f;
        float r = body.radius * 0.9f;
        avoidTimer -= dt;
        if (!Blocked(origin, dir, r))
        {
            if (avoidTimer <= 0f) avoidSide = 0;
            return dir;
        }
        if (avoidSide == 0)
        {
            // Pick the side whose first free angle is smaller; fall back to a random side.
            avoidSide = Random.value < 0.5f ? 1 : -1;
            foreach (float a in SteerAngles)
            {
                bool right = !Blocked(origin, Quaternion.Euler(0f, a, 0f) * dir, r);
                bool left = !Blocked(origin, Quaternion.Euler(0f, -a, 0f) * dir, r);
                if (right != left) { avoidSide = right ? 1 : -1; break; }
                if (right) break;
            }
        }
        avoidTimer = 0.6f;
        foreach (float a in SteerAngles)
        {
            Vector3 d = Quaternion.Euler(0f, a * avoidSide, 0f) * dir;
            if (!Blocked(origin, d, r)) return d;
        }
        return Quaternion.Euler(0f, 120f * avoidSide, 0f) * dir;
    }

    bool Blocked(Vector3 origin, Vector3 d, float r) =>
        Physics.SphereCast(origin, r, d, out _, lookAhead, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore);

    Vector3 Depenetrate(Vector3 pos)
    {
        int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up * 0.6f, body.radius + 0.4f, Buffer, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var o = Buffer[i];
            if (Physics.ComputePenetration(body, pos, transform.rotation, o, o.transform.position, o.transform.rotation, out var d, out var dist))
            {
                d.y = 0f;
                pos += d * dist;
            }
        }
        return pos;
    }

    void Animate(float dt)
    {
        swayPhase += dt * (5f + speed * (runner ? 1.6f : 1f));
        punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
        lunge = Mathf.MoveTowards(lunge, 0f, dt * 4f);
        float sway = Mathf.Sin(swayPhase);
        float bob = Mathf.Abs(Mathf.Cos(swayPhase)) * (runner ? 0.1f : 0.06f);
        float lean = (runner ? 22f : 12f) + Mathf.Sin(swayPhase * 0.5f) * 4f + lunge * 20f;
        model.localPosition = new Vector3(0f, bob, lunge * 0.25f);
        model.localRotation = Quaternion.Euler(lean, sway * 6f, sway * (runner ? 7f : 11f));
        float s = 1f + punch * 0.18f;
        model.localScale = new Vector3(s, 1f / s, s);
    }

    public void TakeHit(float amount, bool crit, Vector3 dir, Vector3 point)
    {
        if (state == State.Dying) return;
        dir.y = 0f;
        hp -= amount;
        flash.Flash(0.07f);
        knock = Vector3.ClampMagnitude(knock + dir * (crit ? 4.5f : 3f), 7f);
        punch = 1f;
        var gm = GameManager.I;
        gm.RegisterHit();
        if (Fx.I) Fx.I.Hit(point, dir);
        if (DamageNumbers.I) DamageNumbers.I.Show(point + Vector3.up * 0.4f, Mathf.RoundToInt(amount), crit);
        if (hp <= 0f) Die(dir, point);
        else
        {
            gm.HitStop(crit ? 0.025f : 0.012f);
            gm.Shake(crit ? 0.08f : 0.04f);
            GameAudio.Play(crit ? SfxId.Crit : SfxId.Hit, point);
        }
    }

    void Die(Vector3 dir, Vector3 point)
    {
        state = State.Dying;
        stateT = 0f;
        body.enabled = false;
        Unregister();
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
        dir.Normalize();
        // Fall away from the shot: face the shooter, tumble backwards.
        transform.rotation = Quaternion.LookRotation(-dir);
        deathVel = dir * Random.Range(6f, 9f) + Vector3.up * Random.Range(3f, 4.5f);
        tumbleAxis = Vector3.right;
        tumbleAngle = 0f;
        tumbleVel = -Random.Range(380f, 520f);
        deathSpin = Random.Range(-90f, 90f);
        landed = false;
        model.localPosition = Vector3.zero;
        model.localScale = Vector3.one;

        var gm = GameManager.I;
        gm.RegisterKill();
        gm.HitStop(0.06f);
        gm.Shake(0.3f);
        Haptics.Play(Haptics.Kind.Medium);
        GameAudio.Play(SfxId.Kill, point);
        if (Fx.I) Fx.I.Death(transform.position + Vector3.up * 0.9f, dir);
    }

    void UpdateDying(float dt)
    {
        const float lieAngle = -90f;
        Vector3 pos = transform.position + deathVel * dt;
        deathVel.y -= 22f * dt;
        if (pos.y <= 0f)
        {
            pos.y = 0f;
            if (deathVel.y < -2f && !landed)
            {
                deathVel.y = -deathVel.y * 0.3f; // one small bounce
                if (Fx.I) Fx.I.Dust(pos);
            }
            else { deathVel.y = 0f; landed = true; }
            deathVel.x *= Mathf.Exp(-8f * dt);
            deathVel.z *= Mathf.Exp(-8f * dt);
        }
        pos = Depenetrate(pos);
        transform.position = pos;

        // Tumble backwards past horizontal, then settle flat with a damped spring.
        if (!landed && tumbleAngle > lieAngle - 15f) tumbleAngle += tumbleVel * dt;
        else
        {
            float k = 120f, c = 14f;
            tumbleVel += (-(tumbleAngle - lieAngle) * k - tumbleVel * c) * dt;
            tumbleAngle += tumbleVel * dt;
        }
        deathSpin *= Mathf.Exp(-4f * dt);
        transform.rotation *= Quaternion.Euler(0f, deathSpin * dt, 0f);
        model.localRotation = Quaternion.AngleAxis(tumbleAngle, tumbleAxis);
        model.localPosition = new Vector3(0f, 0.2f * Mathf.Clamp01(-tumbleAngle / 90f), 0f);

        float sinkStart = 1.4f;
        if (stateT > sinkStart) model.localPosition += Vector3.down * (stateT - sinkStart) * 0.8f;
        if (stateT >= sinkStart + 0.8f) pool.Release(this);
    }

    void OnDisable() => Unregister();
}
