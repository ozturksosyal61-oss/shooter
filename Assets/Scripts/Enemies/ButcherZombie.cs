using UnityEngine;

/// <summary>
/// Kasap (Butcher) — first boss, a ~2.7× lab mutation in a bloody apron.
/// State machine (all decisions here, visuals only read <see cref="CurrentPhase"/>):
///   Walk → (far) ChargeWind 1 s: roar + red strip → Charge: straight line, flings cars/crates/barricades and zombies;
///          hitting a building → Stunned 2 s → Tired 3 s (2× damage, "ZAYIF!") → Walk
///        → (near) SlamWind 0.8 s: growing ring → shockwave (dodged by vaulting or leaving the ring) → Walk
/// Below 50 % health it is enraged: faster, shorter pauses between charges.
/// Unlike ordinary zombies it does not walk around low obstacles during a charge — it smashes through them.
/// </summary>
public class ButcherZombie : Zombie
{
    [Header("Charge")]
    public float chargeTelegraph = 1f;
    public float chargeSpeed = 15f;
    public float chargeMaxDistance = 24f;
    public float chargeMinDistance = 5.5f;
    public float chargeOvershoot = 6f;
    public float chargeCooldown = 6f;
    public float chargeDamage = 28f;
    public float chargeZombieDamage = 80f;
    public float stunTime = 2f;
    public float tiredTime = 3f;
    [Tooltip("First part of the run that still bends toward the player (limited turn rate).")]
    public float homingTime = 0.4f;
    public float homingTurn = 110f;
    [Tooltip("Enraged: a second charge follows right away with this shorter wind-up.")]
    public float secondTelegraph = 0.45f;
    [Tooltip("Blockers smaller than this (EnvProp radius) are smashed instead of stopping the charge.")]
    public float wallMinRadius = 1.6f;

    [Header("Ground slam")]
    public float slamTrigger = 4.5f;
    public float slamTelegraph = 0.8f;
    public float slamRadius = 5.5f;
    public float slamDamage = 26f;
    public float slamCooldown = 4f;
    [Tooltip("Impact moment in the slam clip (seconds at normal speed), measured once from the animation.")]
    public float slamImpactTime = 1.1f;
    public float slamRecover = 0.7f;

    [Tooltip("Shock ring left on the ground after the slam: slows the player inside.")]
    public float shockTime = 2f;
    public float shockSlow = 0.4f;

    [Header("Throw (player far away)")]
    public BossProjectile[] projectiles;
    public float throwMinDistance = 12f;
    public float throwWind = 0.7f;
    public float throwCooldown = 3.5f;
    public float throwLead = 0.5f;

    [Header("Rage (< 50 % health)")]
    public float rageSpeedMul = 1.35f;
    public float rageCooldownMul = 0.55f;

    [Header("Misc")]
    public float shoveDamageMul = 0.8f;
    public float catchUpDistance = 24f;
    public float stepInterval = 0.7f;

    [Header("Visuals")]
    public Transform chargeStrip;
    public Transform slamRing;
    public Transform slamZone;
    public Transform[] eyes;
    public Renderer ringRenderer;

    public enum Phase { Walk, ChargeWind, Charge, Stunned, Tired, SlamWind, SlamRecover, Throw, Dead }

    Phase phase;
    float phaseT, chargeCd, slamCd, shoveCd, stepT, curSpeed, chargeLen, traveled, windTime, throwCd, shockT;
    int chargeInCombo;
    Vector3 shockPos, lastPlayerPos, playerVel;
    Renderer zoneRenderer;
    Vector3 chargeDir;
    bool hitPlayer, lockedLine;
    System.Random rng = new System.Random(1);
    readonly Zombie[] flung = new Zombie[32];
    int flungCount;
    static readonly RaycastHit[] Hits = new RaycastHit[12];
    MaterialPropertyBlock mpb;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    Camera cam;
    Vector3[] eyeScale;

    public Phase CurrentPhase => phase;
    public float PhaseTime => phaseT;
    /// <summary>Winded or stunned: takes double damage.</summary>
    public bool IsWeak => phase == Phase.Tired || phase == Phase.Stunned;
    public bool Enraged { get; private set; }
    /// <summary>Hits on the player per attack type (bots / balance logs).</summary>
    public int ChargeHits { get; private set; }
    public int SlamHits { get; private set; }
    public int ShoveHits { get; private set; }
    public int Charges { get; private set; }
    public int Throws { get; private set; }
    /// <summary>Charge number inside a rage double charge (1 or 2).</summary>
    public int ChargeInCombo => chargeInCombo;
    public bool ShockActive => shockT > 0f;
    public Vector3 ShockCenter => shockPos;
    public Vector3 ChargeDirection => chargeDir;
    public float ChargeLength => chargeLen;
    /// <summary>Raised on every phase change (tests, bots, HUD).</summary>
    public event System.Action<Phase> PhaseChanged;

    protected override SfxId SpawnSfx => SfxId.BossRoar;
    protected override float KnockScale => 0.05f;
    protected override float DamageTakenMul => BossRules.DamageTaken(1f, IsWeak);
    protected override float CurrentSpeed => curSpeed;
    public override Vector3 AimPoint => transform.position + Vector3.up * 2.2f;

    public void SetSeed(int seed) => rng = new System.Random(seed);

    protected override void Awake()
    {
        base.Awake();
        mpb = new MaterialPropertyBlock();
        if (slamZone) zoneRenderer = slamZone.GetComponent<Renderer>();
        if (projectiles != null)
            foreach (var pr in projectiles) if (pr) { pr.transform.SetParent(null, true); pr.Hide(); }
        eyeScale = new Vector3[eyes != null ? eyes.Length : 0];
        for (int i = 0; i < eyeScale.Length; i++) eyeScale[i] = eyes[i] ? eyes[i].localScale : Vector3.one;
    }

    protected override void OnSpawned()
    {
        Enraged = false;
        chargeCd = 2.5f;
        slamCd = 1.5f;
        shoveCd = 0f;
        throwCd = 4f;
        shockT = 0f;
        chargeInCombo = 0;
        flungCount = 0;
        animator.speed = 1f;
        SetPhase(Phase.Walk);
        ShowTelegraphs(false, false);
        Tint(Color.white, ringColor);
    }

    static readonly Color ringColor = new Color(1f, 0.12f, 0.08f, 1f);
    static readonly Color weakColor = new Color(1f, 0.9f, 0.25f, 1f);

    void OnEnable() => FloatingOrigin.OnShift += Shift;
    protected override void OnDisable() { base.OnDisable(); FloatingOrigin.OnShift -= Shift; }
    void Shift(Vector3 d) { shockPos += d; lastPlayerPos += d; }

    void SetPhase(Phase p)
    {
        phase = p;
        phaseT = 0f;
        PhaseChanged?.Invoke(p);
    }

    float Cooldown(float baseCd) => baseCd * (Enraged ? rageCooldownMul : 1f) * (0.85f + 0.3f * (float)rng.NextDouble());

    protected override void UpdateBehaviour(float dt)
    {
        var player = GameManager.I.player;
        Vector3 dir = ToPlayer(out float dist);
        phaseT += dt;
        chargeCd -= dt;
        slamCd -= dt;
        shoveCd -= dt;
        throwCd -= dt;
        knock *= 0.2f; // explosions barely move it
        Vector3 pp = player.transform.position;
        playerVel = Vector3.Lerp(playerVel, (pp - lastPlayerPos) / Mathf.Max(dt, 0.0001f), 1f - Mathf.Exp(-6f * dt));
        lastPlayerPos = pp;
        UpdateShock(dt, player);

        if (!Enraged && BossRules.Enraged(hp / maxHp))
        {
            Enraged = true;
            GameAudio.Play(SfxId.BossRoar, transform.position, 0.85f);
            if (DamageNumbers.I) DamageNumbers.I.ShowText(transform.position + Vector3.up * 5f, Loc.T("boss.rage"), new Color(1f, 0.3f, 0.2f));
            GameManager.I.Shake(0.4f);
        }

        switch (phase)
        {
            case Phase.Walk: UpdateWalk(dt, player, dir, dist); break;
            case Phase.ChargeWind: UpdateChargeWind(dt, dir, dist); break;
            case Phase.Charge: UpdateCharge(dt, player); break;
            case Phase.Stunned:
                Hold(dt);
                if (phaseT >= stunTime) EnterTired();
                break;
            case Phase.Tired:
                Hold(dt);
                if (phaseT >= tiredTime) { chargeCd = Cooldown(chargeCooldown) * 0.5f; BackToWalk(); }
                break;
            case Phase.SlamWind: UpdateSlamWind(dt, player, dir); break;
            case Phase.SlamRecover:
                Hold(dt);
                if (phaseT >= slamRecover) BackToWalk();
                break;
            case Phase.Throw: UpdateThrow(dt, player, dir, dist); break;
        }
    }

    void Hold(float dt)
    {
        gait = 0f;
        curSpeed = 0f;
        Move(dt, Vector3.zero, 0f);
    }

    void BackToWalk()
    {
        animator.speed = 1f;
        animator.CrossFadeInFixedTime(LocoState, 0.25f, 0);
        ShowTelegraphs(false, false);
        SetPhase(Phase.Walk);
    }

    // ------------------------------------------------------------------ walk
    void UpdateWalk(float dt, PlayerController player, Vector3 dir, float dist)
    {
        bool far = dist > catchUpDistance;
        curSpeed = speed * (Enraged ? rageSpeedMul : 1f) * (far ? 2.2f : 1f);
        gait = far ? 2f : 1f;
        Chase(dt, curSpeed, dist > body.radius + 0.6f);
        Footsteps(dt, dist);
        if (player.IsDead) return;

        if (dist < slamTrigger && slamCd <= 0f) { StartSlam(); return; }
        if (chargeCd <= 0f && dist > chargeMinDistance && dist < chargeMaxDistance) { chargeInCombo = 1; StartChargeWind(dir, dist); return; }
        // Kiting from afar does not work: it hurls debris.
        if (throwCd <= 0f && dist >= throwMinDistance && FreeProjectile() != null) { StartThrow(); return; }
        if (dist < body.radius + 1f && shoveCd <= 0f)
        {
            // Too close for anything else: a heavy shove.
            shoveCd = 1.5f;
            ShoveHits++;
            GameAudio.Play(SfxId.ZombieAttack, transform.position, 0.7f);
            player.TakeDamage(damage * shoveDamageMul, dir, 12f);
        }
    }

    void Footsteps(float dt, float dist)
    {
        stepT -= dt * Mathf.Max(0.5f, curSpeed / Mathf.Max(0.1f, speed));
        if (stepT > 0f) return;
        stepT = stepInterval;
        GameAudio.Play(SfxId.BossStep, transform.position);
        if (dist < 12f) GameManager.I.Shake(0.08f);
    }

    // ------------------------------------------------------------------ charge
    /// <summary>Command: charge at a world point now (tests, scripted moments; later a host-issued command).</summary>
    public void ForceCharge(Vector3 point)
    {
        if (state != State.Walking || phase == Phase.Dead) return;
        Vector3 d = point - transform.position; d.y = 0f;
        chargeInCombo = 1;
        StartChargeWind(d.normalized, d.magnitude - chargeOvershoot);
        lockedLine = true;
    }

    /// <summary>Command: ground slam now.</summary>
    public void ForceSlam()
    {
        if (state != State.Walking || phase == Phase.Dead) return;
        StartSlam();
    }

    /// <summary>Command: throw debris at the player now.</summary>
    public void ForceThrow()
    {
        if (state != State.Walking || phase == Phase.Dead || FreeProjectile() == null) return;
        StartThrow();
    }

    void StartChargeWind(Vector3 dir, float dist, float wind = -1f)
    {
        lockedLine = false;
        windTime = wind > 0f ? wind : chargeTelegraph;
        chargeDir = dir;
        chargeLen = Mathf.Min(dist + chargeOvershoot, chargeMaxDistance);
        animator.CrossFadeInFixedTime(ScreamState, 0.15f, 0);
        GameAudio.Play(SfxId.BossRoar, transform.position);
        GameManager.I.Shake(0.25f);
        SetPhase(Phase.ChargeWind);
        ShowTelegraphs(true, false);
    }

    void UpdateChargeWind(float dt, Vector3 dir, float dist)
    {
        Hold(dt);
        // Tracks the player for most of the wind-up, then commits to the line.
        if (!lockedLine && phaseT < windTime * 0.7f)
        {
            chargeDir = dir;
            chargeLen = Mathf.Min(dist + chargeOvershoot, chargeMaxDistance);
        }
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(chargeDir), 400f * dt);
        UpdateStrip();
        if (phaseT >= windTime) StartCharge();
    }

    void UpdateStrip()
    {
        if (!chargeStrip) return;
        // Ends at the first wall on the line.
        float len = chargeLen;
        if (Physics.SphereCast(transform.position + Vector3.up, body.radius * 0.8f, chargeDir, out var h, chargeLen, GameLayers.BlockerMask, QueryTriggerInteraction.Ignore)
            && IsWall(h.collider)) len = h.distance + body.radius;
        chargeStrip.position = transform.position + chargeDir * (len * 0.5f) + Vector3.up * 0.06f;
        chargeStrip.rotation = Quaternion.LookRotation(Vector3.down, chargeDir);
        float pulse = 1f + 0.06f * Mathf.Sin(phaseT * 30f);
        chargeStrip.localScale = new Vector3(body.radius * 2.2f * pulse, len, 1f) / transform.localScale.x;
    }

    void StartCharge()
    {
        traveled = 0f;
        hitPlayer = false;
        flungCount = 0;
        ShowTelegraphs(false, false);
        animator.CrossFadeInFixedTime(LocoState, 0.1f, 0);
        GameAudio.Play(SfxId.BossCharge, transform.position);
        Charges++;
        SetPhase(Phase.Charge);
    }

    void UpdateCharge(float dt, PlayerController player)
    {
        gait = 2f;
        curSpeed = chargeSpeed * (Enraged ? 1.15f : 1f);
        if (phaseT < homingTime && !lockedLine && !player.IsDead)
        {
            // Bends toward the player at the start of the run (limited turn rate), then straight.
            Vector3 to = player.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 1f)
                chargeDir = Vector3.RotateTowards(chargeDir, to.normalized, homingTurn * Mathf.Deg2Rad * dt, 0f);
        }
        float step = curSpeed * dt;
        Vector3 pos = transform.position;
        Vector3 origin = pos + Vector3.up * 1f;
        float r = body.radius * 0.9f;

        int n = Physics.SphereCastNonAlloc(origin, r, chargeDir, Hits, step + 0.4f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var col = Hits[i].collider;
            if (!col || !col.enabled) continue;
            var barrel = col.GetComponentInParent<ExplosiveBarrel>();
            if (barrel) { barrel.Explode(); continue; }
            if (Hits[i].distance <= 0f && Vector3.Dot(col.ClosestPoint(origin) - origin, chargeDir) <= 0f) continue; // overlapping, behind us
            if (col.gameObject.layer == GameLayers.Blocker && IsWall(col))
            {
                transform.position = pos + chargeDir * Mathf.Max(0f, Hits[i].distance - 0.05f);
                HitWall(Hits[i].point);
                return;
            }
            var prop = col.GetComponentInParent<EnvProp>();
            if (PropTosser.I) PropTosser.I.Toss(prop ? prop.transform : col.transform, chargeDir, curSpeed);
        }

        pos += chargeDir * step;
        pos.y = 0f;
        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(chargeDir);
        traveled += step;
        Footsteps(dt * 1.8f, 0f);

        FlingZombies(pos);
        if (!hitPlayer && !player.IsDead)
        {
            Vector3 d = player.transform.position - pos; d.y = 0f;
            if (d.magnitude < body.radius + 0.6f)
            {
                hitPlayer = true;
                ChargeHits++;
                Vector3 side = Vector3.Cross(Vector3.up, chargeDir) * (Vector3.Dot(d, Vector3.Cross(Vector3.up, chargeDir)) >= 0f ? 1f : -1f);
                player.TakeDamage(chargeDamage, (chargeDir + side).normalized, 18f);
                GameAudio.Play(SfxId.BossSlam, player.transform.position, 1.3f, 0.7f);
                GameManager.I.Shake(0.6f);
            }
        }
        if (traveled >= chargeLen)
        {
            if (Enraged && chargeInCombo == 1 && !player.IsDead)
            {
                // Rage: straight into a second charge with a short wind-up.
                chargeInCombo = 2;
                Vector3 d2 = ToPlayer(out float dist2);
                StartChargeWind(d2, dist2, secondTelegraph);
                return;
            }
            EnterTired();
        }
    }

    bool IsWall(Collider c)
    {
        var prop = c.GetComponentInParent<EnvProp>();
        return !prop || prop.radius >= wallMinRadius;
    }

    void FlingZombies(Vector3 pos)
    {
        int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up, body.radius + 0.7f, Buffer, GameLayers.ZombieMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var z = Buffer[i].GetComponentInParent<Zombie>();
            if (!z || z == this || !z.Damageable || Flung(z)) continue;
            if (flungCount < flung.Length) flung[flungCount++] = z;
            Vector3 d = z.transform.position - pos; d.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, chargeDir);
            if (Vector3.Dot(d, side) < 0f) side = -side;
            Vector3 fling = (chargeDir + side * 1.2f).normalized;
            z.Knock(fling * 14f);
            z.TakeHit(chargeZombieDamage, false, fling, z.AimPoint, true);
        }
    }

    bool Flung(Zombie z)
    {
        for (int i = 0; i < flungCount; i++) if (flung[i] == z) return true;
        return false;
    }

    void HitWall(Vector3 point)
    {
        GameAudio.Play(SfxId.BossSlam, point, 0.8f);
        GameAudio.Play(SfxId.HitConcrete, point);
        GameManager.I.Shake(0.7f);
        if (Fx.I) { Fx.I.Dust(point); Fx.I.Spark(point, -chargeDir); Fx.I.Dust(transform.position); }
        if (DamageNumbers.I) DamageNumbers.I.ShowText(transform.position + Vector3.up * 5.2f, Loc.T("boss.stunned"), weakColor);
        animator.CrossFadeInFixedTime(LocoState, 0.1f, 0);
        SetPhase(Phase.Stunned);
    }

    void EnterTired()
    {
        if (phase != Phase.Stunned)
            if (DamageNumbers.I) DamageNumbers.I.ShowText(transform.position + Vector3.up * 5.2f, Loc.T("boss.weak"), weakColor);
        animator.CrossFadeInFixedTime(LocoState, 0.2f, 0);
        SetPhase(Phase.Tired);
    }

    // ------------------------------------------------------------------ slam
    void StartSlam()
    {
        animator.CrossFadeInFixedTime(AttackState, 0.1f, 0);
        // Line the clip's impact frame up with the end of the telegraph.
        animator.speed = Mathf.Clamp(slamImpactTime / slamTelegraph, 0.5f, 3f);
        GameAudio.Play(SfxId.BossRoar, transform.position, 1.15f, 0.6f);
        SetPhase(Phase.SlamWind);
        ShowTelegraphs(false, true);
    }

    void UpdateSlamWind(float dt, PlayerController player, Vector3 dir)
    {
        Hold(dt);
        Face(dir, dt);
        float k = Mathf.Clamp01(phaseT / slamTelegraph);
        if (slamRing)
        {
            float s = Mathf.Lerp(0.3f, 1f, k) * slamRadius * 2f / transform.localScale.x;
            slamRing.localScale = new Vector3(s, s, 1f);
        }
        if (phaseT < slamTelegraph) return;

        // Impact.
        Vector3 pos = transform.position;
        GameAudio.Play(SfxId.BossSlam, pos);
        GameManager.I.Shake(0.75f);
        if (Fx.I) Fx.I.Explosion(pos, slamRadius * 0.6f, false);
        if (!player.IsDead && !player.IsVaulting)
        {
            Vector3 d = player.transform.position - pos; d.y = 0f;
            float m = d.magnitude;
            if (m < slamRadius)
            {
                float f = 1f - 0.4f * Mathf.Clamp01(m / slamRadius);
                SlamHits++;
                player.TakeDamage(slamDamage * f, m > 0.05f ? d / m : transform.forward, 16f * f);
            }
        }
        int n = Physics.OverlapSphereNonAlloc(pos + Vector3.up, slamRadius, Buffer, GameLayers.ZombieMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var z = Buffer[i].GetComponentInParent<Zombie>();
            if (!z || z == this || !z.Damageable) continue;
            Vector3 d = z.transform.position - pos; d.y = 0f;
            z.Knock(d.normalized * 9f);
        }
        slamCd = Cooldown(slamCooldown);
        ShowTelegraphs(false, false);
        shockT = shockTime;
        shockPos = pos;
        if (slamZone) slamZone.gameObject.SetActive(true);
        SetPhase(Phase.SlamRecover);
    }

    void UpdateShock(float dt, PlayerController player)
    {
        if (shockT <= 0f) return;
        shockT -= dt;
        if (slamZone)
        {
            slamZone.position = shockPos + Vector3.up * 0.05f;
            float s = slamRadius * 2f / transform.localScale.x * (1f + 0.04f * Mathf.Sin(shockT * 25f));
            slamZone.localScale = new Vector3(s, s, 1f);
            if (zoneRenderer)
            {
                zoneRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, new Color(0.35f, 0.75f, 1f, 1f) * Mathf.Clamp01(shockT / shockTime + 0.2f));
                zoneRenderer.SetPropertyBlock(mpb);
            }
        }
        if (!player.IsDead)
        {
            Vector3 d = player.transform.position - shockPos; d.y = 0f;
            if (d.sqrMagnitude < slamRadius * slamRadius) player.Slow(shockSlow, 0.12f);
        }
        if (shockT <= 0f)
        {
            if (slamZone)
            {
                slamZone.localPosition = new Vector3(0f, 0.05f, 0f);
                slamZone.gameObject.SetActive(phase == Phase.SlamWind);
                if (zoneRenderer) { zoneRenderer.GetPropertyBlock(mpb); mpb.SetColor(BaseColorId, new Color(1f, 0.15f, 0.05f, 1f)); zoneRenderer.SetPropertyBlock(mpb); }
            }
        }
    }

    // ------------------------------------------------------------------ throw
    BossProjectile FreeProjectile()
    {
        if (projectiles == null) return null;
        foreach (var p in projectiles) if (p && !p.Flying) return p;
        return null;
    }

    void StartThrow()
    {
        animator.CrossFadeInFixedTime(ScreamState, 0.15f, 0);
        GameAudio.Play(SfxId.BossRoar, transform.position, 1.2f, 0.7f);
        SetPhase(Phase.Throw);
    }

    void UpdateThrow(float dt, PlayerController player, Vector3 dir, float dist)
    {
        Hold(dt);
        Face(dir, dt * 2f);
        if (phaseT < throwWind) return;
        int n = Enraged ? 2 : 1;
        for (int i = 0; i < n; i++)
        {
            var pr = FreeProjectile();
            if (!pr) break;
            Vector3 target = player.transform.position + Vector3.ClampMagnitude(playerVel, 7f) * (throwLead + 0.4f * i);
            if (i == 1) target += Vector3.Cross(Vector3.up, dir) * ((float)rng.NextDouble() < 0.5 ? -3f : 3f);
            float flight = 0.9f + dist * 0.025f;
            pr.Launch(transform.position + Vector3.up * 4f + transform.forward * 1.2f, target, flight);
            Throws++;
        }
        GameAudio.Play(SfxId.GrenadeThrow, transform.position, 0.6f);
        throwCd = Cooldown(throwCooldown);
        BackToWalk();
    }

    // ------------------------------------------------------------------ visuals (read-only on state)
    void ShowTelegraphs(bool strip, bool slam)
    {
        if (chargeStrip) chargeStrip.gameObject.SetActive(strip);
        if (slamRing) slamRing.gameObject.SetActive(slam);
        if (slamZone && shockT <= 0f)
        {
            slamZone.gameObject.SetActive(slam);
            float s = slamRadius * 2f / transform.localScale.x;
            slamZone.localScale = new Vector3(s, s, 1f);
        }
    }

    void Tint(Color body, Color ring)
    {
        if (bodyRenderer)
        {
            bodyRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, body);
            bodyRenderer.SetPropertyBlock(mpb);
        }
        if (ringRenderer)
        {
            ringRenderer.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, ring);
            ringRenderer.SetPropertyBlock(mpb);
        }
    }

    protected override void Animate(float dt)
    {
        base.Animate(dt);
        if (phase == Phase.Stunned)
        {
            animator.SetFloat(AnimSpeed, 0.5f);
            model.localRotation = Quaternion.Euler(0f, Mathf.Sin(phaseT * 3f) * 8f, Mathf.Sin(phaseT * 6f) * 5f);
        }
        else
        {
            if (phase == Phase.Tired) animator.SetFloat(AnimSpeed, 1.6f);
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.identity, 1f - Mathf.Exp(-10f * dt));
        }
        // Weak: pulsing yellow body + ring. Telegraphs blink.
        if (IsWeak)
        {
            float p = 0.5f + 0.5f * Mathf.Sin(phaseT * 10f);
            Tint(Color.Lerp(Color.white, weakColor * 1.6f, 0.35f + 0.35f * p), Color.Lerp(ringColor, weakColor, 0.6f + 0.4f * p));
        }
        else Tint(Color.white, ringColor);
    }

    void LateUpdate()
    {
        if (eyes == null || eyes.Length == 0) return;
        if (!cam) cam = Camera.main;
        if (!cam) return;
        float glow = (Enraged ? 1.35f : 1f) * (1f + 0.15f * Mathf.Sin(Time.time * 9f)) * (phase == Phase.Dead ? Mathf.Clamp01(1f - stateT * 0.5f) : 1f);
        var rot = cam.transform.rotation;
        for (int i = 0; i < eyes.Length; i++)
        {
            if (!eyes[i]) continue;
            eyes[i].rotation = rot;
            eyes[i].localScale = eyeScale[i] * glow;
        }
    }

    protected override bool OnKilled(Vector3 dir, bool explosive)
    {
        // No throw: it drops where it stands.
        animator.speed = 1f;
        ShowTelegraphs(false, false);
        Tint(Color.white, ringColor);
        SetPhase(Phase.Dead);
        model.localRotation = Quaternion.identity;
        animator.CrossFadeInFixedTime(DyingState, 0.15f, 0);
        GameAudio.Play(SfxId.BossRoar, transform.position, 0.7f);
        return true;
    }

    protected override void OnRemoved()
    {
        base.OnRemoved();
        shockT = 0f;
        if (slamZone) slamZone.localPosition = new Vector3(0f, 0.05f, 0f);
        ShowTelegraphs(false, false);
    }
}
