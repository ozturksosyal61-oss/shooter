using System.Collections.Generic;
using UnityEngine;

public enum ZombieKind { Walker, Runner, Shield, Bomb, Tongue }

/// <summary>
/// Pooled, animated zombie. Rises from the ground screaming, walks/runs toward the player and
/// steers around obstacles (never vaults — obstacle-crossing is reserved for future bosses),
/// attacks on contact, and dies by being thrown back first, then playing its dying animation.
/// Special zombies (lab mutations) derive from this and override the behaviour hooks.
/// </summary>
public class Zombie : MonoBehaviour
{
    public static readonly List<Zombie> Active = new List<Zombie>(128);

    public ZombieKind kind = ZombieKind.Walker;
    public Transform model;
    public Animator animator;
    public HitFlash flash;
    public CapsuleCollider body;
    public Renderer bodyRenderer;
    public Material walkerMaterial;
    public Material runnerMaterial;
    [Tooltip("Coloured ring under the feet (special zombies).")]
    public Transform ring;

    public float contactRange = 0.85f;
    public float contactCooldown = 0.9f;
    public float separationRadius = 0.75f;
    public float lookAhead = 1.4f;
    public float riseDuration = 0.55f;
    public float riseDepth = 1.9f;
    [Tooltip("How long the zombie screams after rising before it starts walking.")]
    public float screamDuration = 1.1f;
    [Tooltip("Raw humanoid root speed of the walk / run clips, measured by the builder (0 = in-place clip).")]
    public float walkClipSpeed;
    public float runClipSpeed;
    public float walkSpeedFallback = 1.2f;
    public float runSpeedFallback = 3.5f;
    [Tooltip("Length of the dying clip (s), measured by the builder.")]
    public float dyingLength = 2.2f;
    [Tooltip("Multiplier of the death throw (heavy mutants barely fly).")]
    public float deathThrow = 1f;
    public float turnSpeed = 300f;
    public int score = 10;
    public int xp = 1;

    protected static readonly int Gait = Animator.StringToHash("Gait");
    protected static readonly int AnimSpeed = Animator.StringToHash("AnimSpeed");
    protected static readonly int LocoState = Animator.StringToHash("Locomotion");
    protected static readonly int ScreamState = Animator.StringToHash("Scream");
    protected static readonly int AttackState = Animator.StringToHash("Attack");
    protected static readonly int PunchState = Animator.StringToHash("Punch");
    protected static readonly int HitState = Animator.StringToHash("Hit");
    protected static readonly int DyingState = Animator.StringToHash("Dying");

    protected enum State { Rising, Walking, Dying }

    protected State state;
    protected float hp, maxHp, speed, damage, stateT, attackCd, punch, moanTimer;
    protected bool runner;
    protected Vector3 knock;
    int avoidSide;          // sticky steering side (+1 / -1), 0 = none
    float avoidTimer;
    int activeIndex = -1;
    protected Pool<Zombie> pool;

    // death: thrown back first, dying animation once it lands
    Vector3 deathVel;
    float deathSpin, landedT;
    bool landed;

    protected float walkSpeed, runSpeed;
    protected Vector3 modelScale, modelPos;
    /// <summary>0 idle, 1 walk, 2 run — set by the movement code, read by <see cref="Animate"/>.</summary>
    protected float gait = 1f;

    protected virtual void Awake()
    {
        modelScale = model.localScale;
        modelPos = model.localPosition;
        float k = model.lossyScale.y; // clip root speeds are measured on the unscaled avatar
        walkSpeed = walkClipSpeed > 0f ? walkClipSpeed * k : walkSpeedFallback;
        runSpeed = runClipSpeed > 0f ? runClipSpeed * k : runSpeedFallback;
    }

    protected static readonly Collider[] Buffer = new Collider[12];
    static readonly float[] SteerAngles = { 30f, 55f, 80f, 110f };

    public bool Targetable => state != State.Dying && (state != State.Rising || stateT > 0.35f);
    /// <summary>Can take damage (explosions also hit zombies that are still rising).</summary>
    public bool Damageable => state != State.Dying;
    public bool IsRunner => runner;
    public bool IsSpecial => kind >= ZombieKind.Shield;
    public ZombieKind Kind => runner ? ZombieKind.Runner : kind;
    public float Health => hp;
    /// <summary>The killing blow was an explosion (kill-blast cards don't chain off these).</summary>
    public bool KilledByExplosion { get; private set; }
    public float MaxHealth => maxHp;
    public virtual Vector3 AimPoint => transform.position + Vector3.up * 1.1f;

    protected virtual SfxId SpawnSfx => SfxId.ZombieScream;

    public void Spawn(Vector3 pos, float health, float moveSpeed, float contactDamage, bool isRunner, Pool<Zombie> owner)
    {
        pool = owner;
        hp = maxHp = health;
        speed = moveSpeed;
        damage = contactDamage;
        runner = isRunner;
        turretTarget = null;
        knock = Vector3.zero;
        punch = 0f;
        attackCd = 0.5f;
        gait = runner ? 2f : 1f;
        moanTimer = Random.Range(2f, 8f);
        avoidSide = 0;
        state = State.Rising;
        stateT = 0f;
        var p = GameManager.I.player;
        Vector3 look = p ? p.transform.position - pos : Vector3.forward; look.y = 0f;
        transform.SetPositionAndRotation(pos, look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : Quaternion.identity);
        transform.localScale = Vector3.one * (IsSpecial ? 1f : runner ? 0.92f : Random.Range(0.97f, 1.06f));
        model.localPosition = modelPos + Vector3.down * riseDepth;
        model.localRotation = Quaternion.identity;
        model.localScale = modelScale;
        if (walkerMaterial) flash.SetBaseMaterial(runner && runnerMaterial ? runnerMaterial : walkerMaterial);
        body.enabled = true;
        if (ring) ring.gameObject.SetActive(true);
        activeIndex = Active.Count;
        Active.Add(this);

        animator.SetFloat(Gait, 0f);
        animator.SetFloat(AnimSpeed, 1f);
        animator.Play(ScreamState, 0, 0f);
        if (Fx.I) Fx.I.Dust(pos);
        GameAudio.Play(SfxId.ZombieSpawn, pos);
        GameAudio.Play(SpawnSfx, pos);
        OnSpawned();
    }

    protected virtual void OnSpawned() { }

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

    public static int CountKind(ZombieKind k)
    {
        int n = 0;
        for (int i = 0; i < Active.Count; i++) if (Active[i].Kind == k) n++;
        return n;
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
        OnRemoved();
        pool.Release(this);
    }

    /// <summary>Returns the zombie to its pool (special deaths that skip the ragdoll).</summary>
    protected void ReleaseToPool()
    {
        Unregister();
        OnRemoved();
        pool.Release(this);
    }

    /// <summary>Leaving play for any reason (death, despawn): drop grabs, telegraphs, etc.</summary>
    protected virtual void OnRemoved() { if (ring) ring.gameObject.SetActive(false); }

    void Update()
    {
        float dt = Time.deltaTime;
        stateT += dt;
        switch (state)
        {
            case State.Rising: UpdateRising(); break;
            case State.Walking: UpdateBehaviour(dt); Animate(dt); break;
            case State.Dying: UpdateDying(dt); break;
        }
    }

    void UpdateRising()
    {
        // Claw out of the ground while screaming, then start walking.
        float t = Mathf.Clamp01(stateT / riseDuration);
        float e = 1f - (1f - t) * (1f - t) * (1f - t);
        model.localPosition = modelPos + Vector3.down * riseDepth * (1f - e);
        if (stateT >= riseDuration + screamDuration)
        {
            state = State.Walking;
            stateT = 0f;
            animator.CrossFadeInFixedTime(LocoState, 0.25f, 0, Random.value);
            OnStartWalking();
        }
    }

    protected virtual void OnStartWalking() { }

    /// <summary>Per-frame AI while walking. Default: chase the player and attack on contact.</summary>
    protected virtual void UpdateBehaviour(float dt)
    {
        PickTarget(dt);
        Vector3 to = ChaseTargetPos - transform.position; to.y = 0f;
        float dist = to.magnitude - (turretTarget ? turretTarget.radius : 0f);
        gait = dist > contactRange * 0.8f ? (runner ? 2f : 1f) : 0f;
        Chase(dt, speed, dist > contactRange * 0.8f);
        Moan(dt, dist);
        TryContactAttack(dt, dist, to);
    }

    // ---- Target: the player, or a deployed turret that is clearly closer (ordinary zombies only).
    protected Turret turretTarget;
    float retargetT;
    const float TurretAggroRange = 7f;

    protected Vector3 ChaseTargetPos => turretTarget ? turretTarget.transform.position : GameManager.I.player.transform.position;

    void PickTarget(float dt)
    {
        if (turretTarget && !turretTarget.IsDeployed) turretTarget = null;
        retargetT -= dt;
        if (retargetT > 0f) return;
        retargetT = 0.4f + Random.value * 0.2f;
        var t = Turret.Nearest(transform.position, TurretAggroRange, out float dt2);
        if (!t) { turretTarget = null; return; }
        Vector3 toP = GameManager.I.player.transform.position - transform.position; toP.y = 0f;
        turretTarget = dt2 < toP.magnitude * 0.8f ? t : null;
    }

    protected Vector3 ToPlayer(out float dist)
    {
        Vector3 to = GameManager.I.player.transform.position - transform.position; to.y = 0f;
        dist = to.magnitude;
        return dist > 0.01f ? to / dist : transform.forward;
    }

    Vector3 ToChaseTarget(out float dist)
    {
        if (!turretTarget) return ToPlayer(out dist);
        Vector3 to = turretTarget.transform.position - transform.position; to.y = 0f;
        dist = to.magnitude;
        return dist > 0.01f ? to / dist : transform.forward;
    }

    /// <summary>Moves toward the player with steering, separation, sliding and knockback.</summary>
    protected void Chase(float dt, float moveSpeed, bool advance)
    {
        Vector3 pos = transform.position;
        Vector3 dir = ToChaseTarget(out float dist);
        Vector3 moveDir = advance ? Steer(pos, dir, dt) : Vector3.zero;
        Move(dt, moveDir * moveSpeed, moveSpeed);
        Vector3 face = moveDir.sqrMagnitude > 0.01f && dist > 2f ? moveDir : dir;
        Face(face, dt);
    }

    protected void Face(Vector3 face, float dt)
    {
        face.y = 0f;
        if (face.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), turnSpeed * dt);
    }

    /// <summary>Applies a desired velocity plus separation, obstacle sliding and knockback.</summary>
    protected void Move(float dt, Vector3 desired, float moveSpeed)
    {
        Vector3 pos = transform.position;
        int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up * 0.8f, separationRadius, Buffer, GameLayers.ZombieMask, QueryTriggerInteraction.Ignore);
        Vector3 sep = Vector3.zero;
        for (int i = 0; i < n; i++)
        {
            if (Buffer[i] == body || Buffer[i].isTrigger || Buffer[i].transform.IsChildOf(transform)) continue;
            Vector3 v = pos - Buffer[i].transform.position; v.y = 0f;
            float m = v.magnitude;
            if (m < 0.001f) { v = Random.insideUnitSphere; v.y = 0f; m = 0.001f; }
            sep += v / m * (1f - Mathf.Clamp01(m / separationRadius));
        }
        desired += sep * Mathf.Max(moveSpeed, 1f) * 1.6f;

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
    }

    protected void Moan(float dt, float dist)
    {
        moanTimer -= dt;
        if (moanTimer <= 0f)
        {
            moanTimer = Random.Range(4f, 10f);
            if (dist < 20f) GameAudio.Play(SfxId.ZombieMoan, transform.position + Vector3.up * 1.5f);
        }
    }

    protected void TryContactAttack(float dt, float dist, Vector3 to)
    {
        var player = GameManager.I.player;
        attackCd -= dt;
        if (dist < contactRange && attackCd <= 0f && (turretTarget || !player.IsDead))
        {
            attackCd = contactCooldown;
            // The attack animation starts at the moment of contact; damage lands now.
            animator.CrossFadeInFixedTime(Random.value < 0.5f ? AttackState : PunchState, 0.08f, 0);
            GameAudio.Play(SfxId.ZombieAttack, transform.position);
            if (turretTarget) turretTarget.TakeDamage(damage);
            else player.TakeDamage(damage, dist > 0.01f ? to / dist : transform.forward);
        }
    }

    /// <summary>Whisker steering: keep the direct line if clear, otherwise turn to a sticky side.</summary>
    protected Vector3 Steer(Vector3 pos, Vector3 dir, float dt)
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

    protected Vector3 Depenetrate(Vector3 pos)
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

    protected virtual void Animate(float dt)
    {
        punch = Mathf.MoveTowards(punch, 0f, dt * 6f);
        float s = 1f + punch * 0.12f;
        model.localScale = Vector3.Scale(modelScale, new Vector3(s, 1f / s, s));
        model.localPosition = modelPos;

        // Gait: 0 idle, 1 walk, 2 run; playback speed follows movement speed.
        animator.SetFloat(Gait, gait, 0.15f, dt);
        float clip = gait > 1.5f ? runSpeed : walkSpeed;
        animator.SetFloat(AnimSpeed, gait > 0.5f ? Mathf.Clamp(CurrentSpeed / Mathf.Max(0.1f, clip), 0.6f, 2f) : 1f);
    }

    /// <summary>Current ground speed used to sync the walk/run cycle.</summary>
    protected virtual float CurrentSpeed => speed;

    public void Knock(Vector3 v) => knock = Vector3.ClampMagnitude(knock + v, 12f);

    /// <summary>Bullet / explosion damage. Returns false when the hit was deflected (e.g. by a shield).</summary>
    public bool TakeHit(float amount, bool crit, Vector3 dir, Vector3 point, bool explosive = false)
    {
        if (state == State.Dying) return true;
        dir.y = 0f;
        if (!explosive && Deflects(dir, point)) return false;
        hp -= amount;
        flash.Flash(0.07f);
        knock = Vector3.ClampMagnitude(knock + dir * (crit ? 4.5f : 3f) * KnockScale, 7f);
        punch = 1f;
        var gm = GameManager.I;
        if (Fx.I) Fx.I.Hit(point, dir);
        // Readability: only critical hits pop a number.
        if (crit && DamageNumbers.I) DamageNumbers.I.Show(point + Vector3.up * 0.4f, Mathf.RoundToInt(amount), true);
        OnDamaged(amount, explosive);
        if (hp <= 0f) Die(dir, point, explosive);
        else if (!explosive)
        {
            gm.HitStop(crit ? 0.025f : 0.012f);
            gm.Shake(crit ? 0.08f : 0.04f);
            GameAudio.Play(crit ? SfxId.Crit : SfxId.Hit, point);
        }
        return true;
    }

    protected virtual float KnockScale => 1f;
    protected virtual bool Deflects(Vector3 dir, Vector3 point) => false;
    protected virtual void OnDamaged(float amount, bool explosive) { }

    void Die(Vector3 dir, Vector3 point, bool explosive)
    {
        state = State.Dying;
        stateT = 0f;
        KilledByExplosion = explosive;
        body.enabled = false;
        Unregister();
        OnRemoved();
        if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
        dir.Normalize();

        var gm = GameManager.I;
        gm.RegisterKill(this);
        if (Pickups.I) Pickups.I.MaybeDrop(transform.position);
        gm.HitStop(0.06f);
        gm.Shake(IsSpecial ? 0.45f : 0.3f);
        GameAudio.Play(SfxId.Hit, point);
        GameAudio.Play(SfxId.Kill, point); // death voice; the kill haptic fires with it
        if (Fx.I) Fx.I.Death(transform.position + Vector3.up * 0.9f, dir);
        deathVel = Vector3.zero;
        deathSpin = 0f;
        landed = true;
        landedT = 0f;
        if (OnKilled(dir, explosive)) return;

        // Face the shooter so the throw and the dying animation both fall away from the shot.
        transform.rotation = Quaternion.LookRotation(-dir);
        deathVel = (dir * Random.Range(6f, 9f) + Vector3.up * Random.Range(3f, 4.5f)) * deathThrow;
        deathSpin = Random.Range(-90f, 90f) * deathThrow;
        landed = false;
        landedT = 0f;
        model.localPosition = modelPos;
        model.localRotation = Quaternion.identity;
        model.localScale = modelScale;
        animator.SetFloat(AnimSpeed, 1f);
        animator.CrossFadeInFixedTime(HitState, 0.05f, 0);
    }

    /// <summary>Special death handling; return true to skip the default throw + dying animation.</summary>
    protected virtual bool OnKilled(Vector3 dir, bool explosive) => false;

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
        if (landedT > dyingLength) model.localPosition = modelPos + Vector3.down * (landedT - dyingLength) * 0.8f;
        if (landedT > dyingLength + 0.9f) pool.Release(this);
    }

    void Land()
    {
        // 2) Landed: the dying animation takes over from the thrown pose.
        deathVel.y = 0f;
        landed = true;
        animator.CrossFadeInFixedTime(DyingState, 0.15f, 0);
    }

    protected virtual void OnDisable()
    {
        Unregister();
    }
}
