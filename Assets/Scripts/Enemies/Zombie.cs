using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pooled, animated zombie. Rises from the ground screaming, walks/runs toward the player and
/// steers around obstacles (never vaults — obstacle-crossing is reserved for future bosses),
/// attacks on contact, and dies by being thrown back first, then playing its dying animation.
/// </summary>
public class Zombie : MonoBehaviour
{
    public static readonly List<Zombie> Active = new List<Zombie>(128);

    public Transform model;
    public Animator animator;
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
    [Tooltip("How long the zombie screams after rising before it starts walking.")]
    public float screamDuration = 1.1f;
    [Tooltip("Raw humanoid root speed of the walk / run clips, measured by the builder (0 = in-place clip).")]
    public float walkClipSpeed;
    public float runClipSpeed;
    public float walkSpeedFallback = 1.2f;
    public float runSpeedFallback = 3.5f;
    [Tooltip("Length of the dying clip (s), measured by the builder.")]
    public float dyingLength = 2.2f;

    static readonly int Gait = Animator.StringToHash("Gait");
    static readonly int AnimSpeed = Animator.StringToHash("AnimSpeed");
    static readonly int LocoState = Animator.StringToHash("Locomotion");
    static readonly int ScreamState = Animator.StringToHash("Scream");
    static readonly int AttackState = Animator.StringToHash("Attack");
    static readonly int PunchState = Animator.StringToHash("Punch");
    static readonly int HitState = Animator.StringToHash("Hit");
    static readonly int DyingState = Animator.StringToHash("Dying");

    enum State { Rising, Walking, Dying }

    State state;
    float hp, speed, damage, stateT, attackCd, punch;
    bool runner;
    Vector3 knock;
    int avoidSide;          // sticky steering side (+1 / -1), 0 = none
    float avoidTimer;
    int activeIndex = -1;
    Pool<Zombie> pool;

    // death: thrown back first, dying animation once it lands
    Vector3 deathVel;
    float deathSpin, landedT;
    bool landed;

    float walkSpeed, runSpeed;

    void Awake()
    {
        float k = model.lossyScale.y; // clip root speeds are measured on the unscaled avatar
        walkSpeed = walkClipSpeed > 0f ? walkClipSpeed * k : walkSpeedFallback;
        runSpeed = runClipSpeed > 0f ? runClipSpeed * k : runSpeedFallback;
    }

    static readonly Collider[] Buffer = new Collider[12];
    static readonly float[] SteerAngles = { 30f, 55f, 80f, 110f };

    public bool Targetable => state != State.Dying && (state != State.Rising || stateT > 0.35f);
    public bool IsRunner => runner;
    public Vector3 AimPoint => transform.position + Vector3.up * 1.1f;

    public void Spawn(Vector3 pos, float health, float moveSpeed, float contactDamage, bool isRunner, Pool<Zombie> owner)
    {
        pool = owner;
        hp = health;
        speed = moveSpeed;
        damage = contactDamage;
        runner = isRunner;
        knock = Vector3.zero;
        punch = 0f;
        attackCd = 0.5f;
        avoidSide = 0;
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

        animator.SetFloat(Gait, 0f);
        animator.SetFloat(AnimSpeed, 1f);
        animator.Play(ScreamState, 0, 0f);
        if (Fx.I) Fx.I.Dust(pos);
        GameAudio.Play(SfxId.ZombieSpawn, pos);
        GameAudio.Play(SfxId.ZombieScream, pos);
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
        // Claw out of the ground while screaming, then start walking.
        float t = Mathf.Clamp01(stateT / riseDuration);
        float e = 1f - (1f - t) * (1f - t) * (1f - t);
        model.localPosition = Vector3.down * 1.9f * (1f - e);
        if (stateT >= riseDuration + screamDuration)
        {
            state = State.Walking;
            stateT = 0f;
            animator.CrossFadeInFixedTime(LocoState, 0.25f, 0, Random.value);
        }
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
            // The attack animation starts at the moment of contact; damage lands now.
            animator.CrossFadeInFixedTime(Random.value < 0.5f ? AttackState : PunchState, 0.08f, 0);
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
        punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
        float s = 1f + punch * 0.12f;
        model.localScale = new Vector3(s, 1f / s, s);
        model.localPosition = Vector3.zero;

        // Gait: 0 idle (next to the player), 1 walk, 2 run; playback speed follows movement speed.
        var p = GameManager.I.player;
        Vector3 d = p.transform.position - transform.position; d.y = 0f;
        bool moving = d.magnitude > contactRange * 0.8f;
        animator.SetFloat(Gait, !moving ? 0f : runner ? 2f : 1f, 0.15f, dt);
        float clip = runner ? runSpeed : walkSpeed;
        animator.SetFloat(AnimSpeed, moving ? Mathf.Clamp(speed / Mathf.Max(0.1f, clip), 0.6f, 2f) : 1f);
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
        // Face the shooter so the throw and the dying animation both fall away from the shot.
        transform.rotation = Quaternion.LookRotation(-dir);
        deathVel = dir * Random.Range(6f, 9f) + Vector3.up * Random.Range(3f, 4.5f);
        deathSpin = Random.Range(-90f, 90f);
        landed = false;
        landedT = 0f;
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        animator.SetFloat(AnimSpeed, 1f);
        animator.CrossFadeInFixedTime(HitState, 0.05f, 0);

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
        // 1) Thrown back by the killing shot (hit-reaction pose, leaning back in the air).
        Vector3 pos = transform.position + deathVel * dt;
        deathVel.y -= 22f * dt;
        if (pos.y <= 0f)
        {
            pos.y = 0f;
            if (deathVel.y < -2f && !landed)
            {
                deathVel.y = -deathVel.y * 0.25f; // one small bounce
                if (Fx.I) Fx.I.Dust(pos);
            }
            else if (!landed) Land();
            deathVel.x *= Mathf.Exp(-8f * dt);
            deathVel.z *= Mathf.Exp(-8f * dt);
        }
        pos = Depenetrate(pos);
        transform.position = pos;
        deathSpin *= Mathf.Exp(-4f * dt);
        transform.rotation *= Quaternion.Euler(0f, deathSpin * dt, 0f);

        if (!landed)
        {
            model.localRotation = Quaternion.Euler(-25f * Mathf.Clamp01(stateT / 0.25f), 0f, 0f);
            if (stateT > 1.5f) Land();
            return;
        }
        model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, 1f - Mathf.Exp(-10f * dt));

        // 3) Stay down for a moment, then sink into the ground and return to the pool.
        landedT += dt;
        if (landedT > dyingLength) model.localPosition = Vector3.down * (landedT - dyingLength) * 0.8f;
        if (landedT > dyingLength + 0.9f) pool.Release(this);
    }

    void Land()
    {
        // 2) Landed: the dying animation takes over from the thrown pose.
        deathVel.y = 0f;
        landed = true;
        animator.CrossFadeInFixedTime(DyingState, 0.15f, 0);
    }

    void OnDisable() => Unregister();
}
