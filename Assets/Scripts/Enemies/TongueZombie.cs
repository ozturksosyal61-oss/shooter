using UnityEngine;

/// <summary>
/// Dilci — a gaunt, long-limbed mutant. Sneaks up to ~8–10 m, stops, shows a red aim line for 0.6 s,
/// then lashes out its tongue. On a hit it reels the player in (the player can push back, slowly);
/// enough damage to the tongue or to the Dilci snaps it. Close up it just claws like any zombie.
/// </summary>
public class TongueZombie : Zombie
{
    [Header("Tongue")]
    public LineRenderer tongueLine;
    public LineRenderer telegraphLine;
    public CapsuleCollider tongueHitbox;
    public float mouthHeight = 2f;
    public float attackRange = 10f;
    public float holdRange = 8f;
    public float minRange = 3.5f;
    public float telegraphTime = 0.6f;
    public float tongueSpeed = 30f;
    public float maxLength = 12f;
    public float retractSpeed = 40f;
    [Tooltip("Pull speed toward the Dilci while grabbed (m/s).")]
    public float pullSpeed = 2.7f;
    [Tooltip("Damage (to the tongue or the Dilci) needed to snap the tongue while it holds the player.")]
    public float breakDamage = 70f;
    public float cooldown = 3.5f;
    public float stunTime = 1.1f;
    public float grabRadius = 0.75f;

    static readonly int ThrowState = Animator.StringToHash("Throw");
    static readonly int PullState = Animator.StringToHash("Pull");

    public enum Phase { Approach, Aim, Flying, Grab, Retract, Stunned }
    public Phase CurrentPhase => phase;
    public float BreakProgress => phase == Phase.Grab ? Mathf.Clamp01(breakMeter / breakDamage) : 0f;

    Phase phase;
    float phaseT, cd, breakMeter, curSpeed, tongueLen;
    Vector3 tip, lockDir;
    SquadMember held;
    SquadMember aimAt;

    protected override SfxId SpawnSfx => SfxId.TongueSpawn;
    protected override float CurrentSpeed => curSpeed;
    public override Vector3 AimPoint => transform.position + Vector3.up * 1.4f;

    Vector3 Mouth => transform.position + Vector3.up * mouthHeight + transform.forward * 0.3f;
    static Vector3 Chest(SquadMember p) => p.Position + Vector3.up * 1.1f;
    static Vector3 Chest(Transform t) => t.position + Vector3.up * 1.1f;

    // ---- co-op: phase + target slot for the clients' puppets
    public override byte NetAct => (byte)phase;
    public override byte NetAux => SlotOf(phase == Phase.Grab ? held : aimAt);
    byte shownAct = 255;
    float puppetT;

    protected override void ApplyNetAct(byte act, byte aux)
    {
        var ph = (Phase)act;
        var who = SoldierOfSlot(aux);
        if (act != shownAct)
        {
            shownAct = act;
            puppetT = 0f;
            phase = ph;
            switch (ph)
            {
                case Phase.Aim: telegraphLine.enabled = true; tongueLine.enabled = false; animator.CrossFadeInFixedTime(ThrowState, 0.1f, 0); GameAudio.Play(SfxId.TongueWarn, transform.position); break;
                case Phase.Flying: telegraphLine.enabled = false; tongueLine.enabled = true; tip = Mouth; tongueLen = 0f; GameAudio.Play(SfxId.TongueLash, transform.position); break;
                case Phase.Grab: tongueLine.enabled = true; if (tongueHitbox) tongueHitbox.enabled = true; animator.CrossFadeInFixedTime(PullState, 0.12f, 0); if (who) GameAudio.Play(SfxId.TongueGrab, Chest(who)); break;
                case Phase.Retract: telegraphLine.enabled = false; if (tongueHitbox) tongueHitbox.enabled = false; break;
                case Phase.Stunned: HideTongue(); animator.CrossFadeInFixedTime(HitState, 0.05f, 0); break;
                default: HideTongue(); animator.CrossFadeInFixedTime(LocoState, 0.2f, 0); break;
            }
        }
        puppetTarget = who;
    }

    Transform puppetTarget;

    /// <summary>Co-op client: draw the host's tongue from the replicated phase (every frame).</summary>
    void PuppetVisuals(float dt)
    {
        puppetT += dt;
        var who = puppetTarget;
        switch (phase)
        {
            case Phase.Aim:
                if (who) DrawTelegraph(Chest(who), puppetT);
                break;
            case Phase.Flying:
                if (!who) break;
                tongueLen += tongueSpeed * dt;
                Vector3 to = Chest(who) - Mouth;
                tip = Mouth + to.normalized * Mathf.Min(tongueLen, to.magnitude);
                DrawTongue();
                break;
            case Phase.Grab:
                if (who) { tip = Chest(who); DrawTongue(); }
                break;
            case Phase.Retract:
                tongueLen = Mathf.MoveTowards((tip - Mouth).magnitude, 0f, retractSpeed * dt);
                tip = Mouth + (tip - Mouth).normalized * tongueLen;
                DrawTongue();
                if (tongueLen <= 0.01f) HideTongue();
                break;
        }
    }

    protected override void Animate(float dt)
    {
        base.Animate(dt);
        if (Puppet) PuppetVisuals(dt);
    }

    protected override void OnSpawned()
    {
        cd = 1.5f;
        SetPhase(Phase.Approach);
        HideTongue();
    }

    void SetPhase(Phase p)
    {
        phase = p;
        phaseT = 0f;
    }

    protected override void UpdateBehaviour(float dt)
    {
        var player = phase == Phase.Grab && held ? held : PickSoldier(dt);
        if (!player) return;
        if (phase == Phase.Approach) aimAt = player;
        Vector3 dir = ToPlayer(out float dist);
        phaseT += dt;
        cd -= dt;

        switch (phase)
        {
            case Phase.Approach:
            {
                bool inRange = dist <= attackRange && dist >= minRange;
                if (inRange && cd <= 0f && !player.IsDead && !player.IsGrabbed && HasLineOfSight(player))
                {
                    StartAim();
                    break;
                }
                // Keep a lurking distance while the tongue recovers; otherwise sneak in.
                bool hold = dist > minRange && dist < holdRange && cd > 0f;
                curSpeed = hold ? 0f : speed;
                gait = hold ? 0f : 1f;
                Chase(dt, curSpeed, !hold);
                Moan(dt, dist);
                TryContactAttack(dt, dist, dir * dist);
                break;
            }
            case Phase.Aim:
                Stand(dt, dir);
                DrawTelegraph(Chest(player), phaseT);
                if (phaseT >= telegraphTime) Fire(player);
                break;
            case Phase.Flying:
                Stand(dt, lockDir);
                FlyTongue(dt, player);
                break;
            case Phase.Grab:
                Stand(dt, dir);
                HoldPlayer(dt, player, dist, dir);
                break;
            case Phase.Retract:
                Stand(dt, dir);
                tongueLen = Mathf.MoveTowards(tongueLen, 0f, retractSpeed * dt);
                tip = Mouth + (tip - Mouth).normalized * tongueLen;
                DrawTongue();
                if (tongueLen <= 0.01f) { HideTongue(); SetPhase(Phase.Approach); }
                break;
            case Phase.Stunned:
                Stand(dt, dir);
                if (phaseT >= stunTime)
                {
                    SetPhase(Phase.Approach);
                    animator.CrossFadeInFixedTime(LocoState, 0.2f, 0);
                }
                break;
        }
    }

    void Stand(float dt, Vector3 face)
    {
        curSpeed = 0f;
        gait = 0f;
        Move(dt, Vector3.zero, 0f);
        Face(face, dt);
    }

    bool HasLineOfSight(SquadMember p) =>
        !Physics.Linecast(Mouth, Chest(p), GameLayers.BlockerMask, QueryTriggerInteraction.Ignore);

    void StartAim()
    {
        SetPhase(Phase.Aim);
        telegraphLine.enabled = true;
        animator.CrossFadeInFixedTime(ThrowState, 0.1f, 0);
        GameAudio.Play(SfxId.TongueWarn, transform.position);
    }

    void DrawTelegraph(Vector3 chest, float t)
    {
        Vector3 a = Mouth, b = chest;
        Vector3 d = b - a;
        // Show the full reach so it reads as a lane to step out of.
        b = a + d.normalized * Mathf.Min(maxLength, d.magnitude + 2f);
        telegraphLine.SetPosition(0, a);
        telegraphLine.SetPosition(1, b);
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 40f);
        float w = Mathf.Lerp(0.05f, 0.14f, t / telegraphTime) * (0.8f + 0.2f * pulse);
        telegraphLine.startWidth = telegraphLine.endWidth = w;
    }

    void Fire(SquadMember p)
    {
        telegraphLine.enabled = false;
        lockDir = Chest(p) - Mouth;
        lockDir = lockDir.sqrMagnitude > 0.01f ? lockDir.normalized : transform.forward;
        tip = Mouth;
        tongueLen = 0f;
        SetPhase(Phase.Flying);
        tongueLine.enabled = true;
        GameAudio.Play(SfxId.TongueLash, transform.position);
    }

    void FlyTongue(float dt, SquadMember p)
    {
        Vector3 from = tip;
        tongueLen += tongueSpeed * dt;
        Vector3 to = Mouth + lockDir * tongueLen;
        tip = to;

        if (!p.IsDead && !p.IsVaulting && DistanceToSegment(Chest(p), from, to) < grabRadius)
        {
            tip = Chest(p);
            StartGrab(p);
            return;
        }
        if (tongueLen >= maxLength || Physics.Linecast(from, to, GameLayers.BlockerMask, QueryTriggerInteraction.Ignore))
        {
            SetPhase(Phase.Retract);
            cd = cooldown;
        }
        DrawTongue();
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector3.Distance(p, a + ab * t);
    }

    void StartGrab(SquadMember p)
    {
        SetPhase(Phase.Grab);
        held = p;
        breakMeter = 0f;
        p.Grab(this);
        animator.CrossFadeInFixedTime(PullState, 0.12f, 0);
        GameAudio.Play(SfxId.TongueGrab, Chest(p));
        GameManager.I.Shake(0.25f);
        if (tongueHitbox) tongueHitbox.enabled = true;
        DrawTongue();
    }

    void HoldPlayer(float dt, SquadMember p, float dist, Vector3 dir)
    {
        if (p.IsDead || held == null) { Snap(false); return; }
        tip = Chest(p);
        DrawTongue();
        if ((tip - Mouth).magnitude > maxLength + 2f) { Snap(true); return; }
        TryContactAttack(dt, dist, dir * dist);
    }

    /// <summary>Bullets hitting the tongue itself.</summary>
    public void HitTongue(float amount, bool crit, Vector3 dir, Vector3 point)
    {
        if (phase != Phase.Grab) return;
        if (Puppet)
        {
            // Co-op client: feedback here, the host snaps it.
            if (Fx.I) Fx.I.Hit(point, dir);
            GameAudio.Play(crit ? SfxId.Crit : SfxId.Hit, point);
            NetZombies.ReportHit(this, amount, crit, dir, point, NetPlayer.HitTongue);
            return;
        }
        if (Fx.I) Fx.I.Hit(point, dir);
        if (DamageNumbers.I && crit) DamageNumbers.I.Show(point + Vector3.up * 0.3f, Mathf.RoundToInt(amount), true);
        GameAudio.Play(crit ? SfxId.Crit : SfxId.Hit, point);
        AddBreak(amount);
    }

    protected override void OnDamaged(float amount, bool explosive)
    {
        if (phase == Phase.Grab) AddBreak(amount);
        else if (phase == Phase.Aim && explosive) Interrupt();
    }

    void AddBreak(float amount)
    {
        breakMeter += amount;
        if (breakMeter >= breakDamage) Snap(true);
    }

    void Interrupt()
    {
        telegraphLine.enabled = false;
        SetPhase(Phase.Stunned);
        cd = cooldown;
        animator.CrossFadeInFixedTime(HitState, 0.05f, 0);
    }

    void Snap(bool feedback)
    {
        ReleasePlayer();
        HideTongue();
        cd = cooldown + 1f;
        SetPhase(Phase.Stunned);
        animator.CrossFadeInFixedTime(HitState, 0.05f, 0);
        if (!feedback) return;
        GameAudio.Play(SfxId.TongueSnap, tip);
        if (Fx.I) Fx.I.Death(tip, (tip - Mouth).normalized);
        if (DamageNumbers.I) DamageNumbers.I.ShowText(tip + Vector3.up * 0.8f, "KOPTU!", new Color(1f, 0.55f, 0.85f));
        GameManager.I.Shake(0.2f);
    }

    void DrawTongue()
    {
        Vector3 a = Mouth;
        tongueLine.SetPosition(0, a);
        tongueLine.SetPosition(1, tip);
        if (tongueHitbox && tongueHitbox.enabled)
        {
            Vector3 d = tip - a;
            float len = d.magnitude;
            var t = tongueHitbox.transform;
            t.position = a + d * 0.5f;
            if (len > 0.01f) t.rotation = Quaternion.LookRotation(d / len);
            tongueHitbox.height = Mathf.Max(0.5f, len - 1f); // keep the ends off the player and the Dilci
        }
    }

    void ReleasePlayer()
    {
        if (held != null) held.ReleaseGrab(this);
        held = null;
        if (tongueHitbox) tongueHitbox.enabled = false;
    }

    void HideTongue()
    {
        if (tongueLine) tongueLine.enabled = false;
        if (telegraphLine) telegraphLine.enabled = false;
        if (tongueHitbox) tongueHitbox.enabled = false;
        tongueLen = 0f;
    }

    protected override void OnRemoved()
    {
        base.OnRemoved();
        ReleasePlayer();
        HideTongue();
        shownAct = 255;
        puppetTarget = null;
        aimAt = null;
        // Co-op client: a puppet holding the local soldier lets go when it leaves.
        var gm = GameManager.I;
        if (gm && gm.player && gm.player.Grabber == this) gm.player.ReleaseGrab(this);
    }

    protected override void OnDisable()
    {
        ReleasePlayer();
        var gm = GameManager.I;
        if (gm && gm.player && gm.player.Grabber == this) gm.player.ReleaseGrab(this);
        base.OnDisable();
    }
}
