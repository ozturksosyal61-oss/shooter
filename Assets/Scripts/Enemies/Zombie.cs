using System.Collections.Generic;
using UnityEngine;

public class Zombie : MonoBehaviour
{
    public static readonly List<Zombie> Active = new List<Zombie>(128);

    public Transform model;
    public HitFlash flash;
    public CapsuleCollider body;

    public float contactDamage = 12f;
    public float contactRange = 0.85f;
    public float contactCooldown = 0.9f;
    public float separationRadius = 0.75f;
    public float riseDuration = 0.55f;
    public float deathDuration = 0.5f;

    enum State { Rising, Walking, Vaulting, Dying }

    State state;
    float hp, speed, stateT, swayPhase, attackCd, punch, lunge;
    Vector3 knock;
    Vector3 vStart, vEnd;
    float vPeak, vDuration;
    int activeIndex = -1;
    Pool<Zombie> pool;

    static readonly Collider[] Buffer = new Collider[12];

    public bool Targetable => state != State.Dying && (state != State.Rising || stateT > 0.35f);

    public void Spawn(Vector3 pos, float health, float moveSpeed, Pool<Zombie> owner)
    {
        pool = owner;
        hp = health;
        speed = moveSpeed;
        knock = Vector3.zero;
        punch = lunge = 0f;
        attackCd = 0.5f;
        swayPhase = Random.value * 10f;
        state = State.Rising;
        stateT = 0f;
        var p = GameManager.I.player;
        Vector3 look = p ? p.transform.position - pos : Vector3.forward; look.y = 0f;
        transform.SetPositionAndRotation(pos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        model.localPosition = Vector3.down * 1.9f;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        body.enabled = true;
        activeIndex = Active.Count;
        Active.Add(this);
        if (Fx.I) Fx.I.Dust(pos);
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

    void Update()
    {
        float dt = Time.deltaTime;
        stateT += dt;
        switch (state)
        {
            case State.Rising: UpdateRising(); break;
            case State.Walking: UpdateWalking(dt); break;
            case State.Vaulting: UpdateVault(dt); break;
            case State.Dying: UpdateDying(dt); return;
        }
        if (state != State.Rising) Animate(dt);
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

        Vector3 desired = dist > contactRange * 0.8f ? dir * speed : Vector3.zero;

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

        // Obstacles: vault crates, slide along walls.
        if (desired.sqrMagnitude > 0.01f)
        {
            Vector3 dd = desired.normalized;
            if (Vault.TryCompute(pos, dd, body.radius, body.radius + 0.25f, out var land, out var top))
            {
                StartVault(land, top);
                return;
            }
            if (Physics.SphereCast(pos + Vector3.up * 0.5f, body.radius * 0.8f, dd, out var hit, body.radius + 0.2f,
                    GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore))
            {
                Vector3 nrm = hit.normal; nrm.y = 0f;
                desired = Vector3.ProjectOnPlane(desired, nrm.normalized);
            }
        }

        pos += (desired + knock) * dt;
        knock *= Mathf.Exp(-9f * dt);
        pos = Arena.Clamp(pos, body.radius);
        pos = Depenetrate(pos);
        pos.y = 0f;
        transform.position = pos;

        Vector3 face = dir;
        if (face.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), 300f * dt);

        attackCd -= dt;
        if (dist < contactRange && attackCd <= 0f && !player.IsDead)
        {
            attackCd = contactCooldown;
            lunge = 1f;
            player.TakeDamage(contactDamage, dir);
        }
    }

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

    void StartVault(Vector3 land, float top)
    {
        state = State.Vaulting;
        stateT = 0f;
        vStart = transform.position;
        vEnd = land;
        vDuration = 0.35f + Vector3.Distance(vStart, vEnd) * 0.12f;
        vPeak = top + 0.1f;
        Vector3 f = vEnd - vStart; f.y = 0f;
        if (f.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(f);
    }

    void UpdateVault(float dt)
    {
        float t = Mathf.Clamp01(stateT / vDuration);
        Vector3 p = Vector3.Lerp(vStart, vEnd, t);
        p.y = Mathf.Sin(t * Mathf.PI) * vPeak;
        transform.position = p;
        if (t >= 1f) { state = State.Walking; stateT = 0f; punch = 0.5f; }
    }

    void Animate(float dt)
    {
        swayPhase += dt * (5f + speed);
        punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
        lunge = Mathf.MoveTowards(lunge, 0f, dt * 4f);
        float sway = Mathf.Sin(swayPhase);
        float bob = Mathf.Abs(Mathf.Cos(swayPhase)) * 0.06f;
        float lean = 12f + Mathf.Sin(swayPhase * 0.5f) * 4f + lunge * 20f;
        if (state == State.Vaulting) lean += Mathf.Sin(Mathf.Clamp01(stateT / vDuration) * Mathf.PI) * 30f;
        model.localPosition = new Vector3(0f, bob, lunge * 0.25f);
        model.localRotation = Quaternion.Euler(lean, sway * 6f, sway * 11f);
        float s = 1f + punch * 0.18f;
        model.localScale = new Vector3(s, 1f / s, s);
    }

    public void TakeHit(float damage, Vector3 dir, Vector3 point)
    {
        if (state == State.Dying) return;
        dir.y = 0f;
        hp -= damage;
        flash.Flash(0.07f);
        knock = Vector3.ClampMagnitude(knock + dir * 3.2f, 7f);
        punch = 1f;
        var gm = GameManager.I;
        gm.RegisterHit();
        if (Fx.I) Fx.I.Hit(point, dir);
        if (hp <= 0f) Die(dir);
        else gm.Shake(0.04f);
    }

    void Die(Vector3 dir)
    {
        state = State.Dying;
        stateT = 0f;
        body.enabled = false;
        Unregister();
        knock = dir * 9f;
        if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(-dir);
        var gm = GameManager.I;
        gm.RegisterKill();
        gm.HitStop(0.05f);
        gm.Shake(0.22f);
        Haptics.Play(Haptics.Kind.Medium);
        if (Fx.I) Fx.I.Death(transform.position + Vector3.up * 0.9f, dir);
    }

    void UpdateDying(float dt)
    {
        Vector3 pos = transform.position + knock * dt;
        knock *= Mathf.Exp(-7f * dt);
        transform.position = Arena.Clamp(pos, 0.3f);

        float t = Mathf.Clamp01(stateT / deathDuration);
        float fall = 1f - (1f - t) * (1f - t);
        float sink = Mathf.Clamp01((stateT - deathDuration) / 0.5f);
        model.localRotation = Quaternion.Euler(-88f * fall, 0f, 0f);
        model.localPosition = new Vector3(0f, 0.15f * fall - sink * 0.6f, 0f);
        model.localScale = Vector3.one;
        if (stateT >= deathDuration + 0.5f) pool.Release(this);
    }

    void OnDisable() => Unregister();
}
