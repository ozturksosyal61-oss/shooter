using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform model;
    public SoldierRig rig;
    public Weapon weapon;
    public HitFlash flash;

    [Header("Movement")]
    public float moveSpeed = 6.2f;
    public float turnSpeed = 1080f;

    [Header("Vault")]
    [Tooltip("Vault only when the joystick is pushed at least this far…")]
    public float vaultMinInput = 0.9f;
    [Tooltip("…and the move direction is within this angle of the obstacle face normal.")]
    public float vaultMaxAngle = 35f;
    public float vaultBaseDuration = 0.2f;
    public float vaultDurationPerMeter = 0.06f;

    [Header("Audio")]
    public float strideLength = 1.25f;

    [Header("Health")]
    [Tooltip("HP per second during the breather between waves (paused right after taking damage).")]
    public float breatherRegen = 5f;
    public float maxHealth = 100f;
    public float invulnerableTime = 0.4f;

    [Header("Grabbed (Dilci tongue)")]
    [Tooltip("Fraction of the normal move speed left while held by a tongue (pushing away resists the pull).")]
    public float grabbedMoveFactor = 0.4f;

    public float Health { get; private set; }
    /// <summary>Stick input this frame on the ground plane (0..1) — co-op replication reads it.</summary>
    public Vector3 MoveInput { get; private set; }
    SquadMember member;
    public bool IsDead { get; private set; }
    public bool IsVaulting => vaulting;
    float slowFactor = 1f, slowUntil;
    /// <summary>Movement multiplier from area effects (boss shock ring); 1 = none.</summary>
    public float SlowFactor => Time.time < slowUntil ? slowFactor : 1f;

    /// <summary>Slows movement to <paramref name="factor"/> for <paramref name="seconds"/> (refreshed each frame by area effects).</summary>
    public void Slow(float factor, float seconds)
    {
        slowFactor = Time.time < slowUntil ? Mathf.Min(slowFactor, factor) : factor;
        slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
    }
    public TongueZombie Grabber { get; private set; }
    public bool IsGrabbed => Grabber != null;

    CharacterController cc;
    Vector3 knock, lastHitDir = Vector3.forward;
    float invulnUntil, stride, lastDamageTime = -10f;

    // vault
    bool vaulting;
    Vector3 vStart, vEnd;
    float vT, vDuration, vPeak;

    void OnEnable() => FloatingOrigin.OnShift += OnShift;
    void OnDisable() => FloatingOrigin.OnShift -= OnShift;
    void OnShift(Vector3 d) { vStart += d; vEnd += d; }

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Health = maxHealth;
    }

    /// <summary>Teleports the soldier (co-op spawn spread, tests).</summary>
    public void Warp(Vector3 pos)
    {
        if (cc) cc.enabled = false;
        transform.position = pos;
        if (cc) cc.enabled = true;
        Physics.SyncTransforms();
    }

    // WeaponLoadout (Awake) changes maxHealth (start-health bonus, hard mode): start full on that.
    void Start() => Health = maxHealth;

    void Update()
    {
        float dt = Time.deltaTime;
        if (IsDead) return;

        var input = TouchControls.I;
        Vector2 mv = input ? input.Move : Vector2.zero;
        Vector3 move = new Vector3(mv.x, 0f, mv.y);
        // Co-op: teammates stay within the leash (one shared city window).
        if (GameMode.IsCoop && Squad.Count > 1)
        {
            if (!member) member = GetComponent<SquadMember>();
            move = Squad.LeashAll(member, move * moveSpeed * dt, ModeRules.CoopLeash) / Mathf.Max(0.0001f, moveSpeed * dt);
        }
        MoveInput = Vector3.ClampMagnitude(move, 1f);
        float mag = Mathf.Clamp01(move.magnitude);
        Vector3 moveDir = mag > 0.01f ? move / mag : Vector3.zero;

        if (vaulting) UpdateVault(dt);
        else if (!(mag >= vaultMinInput && !IsGrabbed && TryStartVault(moveDir)))
        {
            if (Time.time >= slowUntil) slowFactor = 1f;
            Vector3 vel = move * moveSpeed * slowFactor + knock;
            if (IsGrabbed)
            {
                // Reeled in by the tongue; the stick still pushes back (slowly).
                Vector3 toG = Grabber.transform.position - transform.position; toG.y = 0f;
                vel = move * moveSpeed * grabbedMoveFactor + knock + (toG.sqrMagnitude > 0.01f ? toG.normalized * Grabber.pullSpeed : Vector3.zero);
            }
            vel.y = -6f;
            cc.Move(vel * dt);
            // Footsteps by distance travelled.
            stride += mag * moveSpeed * dt;
            if (stride >= strideLength)
            {
                stride = 0f;
                GameAudio.Play(SfxId.Footstep, transform.position, 1f, Mathf.Lerp(0.5f, 1f, mag));
            }
        }
        var waves = WaveDirector.I;
        if (waves && waves.InBreak && ModeRules.BreatherRegen(GameMode.Current) && Health < maxHealth && Time.time - lastDamageTime > 2f)
            Health = Mathf.Min(maxHealth, Health + breatherRegen * dt);
        if (GameAudio.I)
        {
            float h = Health / maxHealth;
            GameAudio.I.SetLowHealth(h < 0.3f, 1f - h / 0.3f);
        }
        knock *= Mathf.Exp(-10f * dt);

        weapon.Tick(dt, moveDir);

        // Facing: aim while firing, otherwise movement / vault direction.
        Vector3 face = transform.forward;
        if (weapon.IsFiring && weapon.AimDir.sqrMagnitude > 0.01f) face = weapon.AimDir;
        else if (vaulting) face = (vEnd - vStart).normalized;
        else if (mag > 0.1f) face = moveDir;
        face.y = 0f;
        if (face.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(face), turnSpeed * dt);

        if (rig) rig.Tick(dt, vaulting ? Vector3.zero : move, vaulting ? 0f : mag * moveSpeed, weapon.IsFiring || weapon.IsReloading, vaulting);
    }

    bool TryStartVault(Vector3 dir)
    {
        if (!Vault.TryCompute(transform.position, dir, cc.radius, cc.radius + 0.3f, out var land, out var top, vaultMaxAngle)) return false;
        vaulting = true;
        vStart = transform.position;
        vEnd = land;
        vT = 0f;
        vDuration = vaultBaseDuration + Vector3.Distance(vStart, vEnd) * vaultDurationPerMeter;
        vPeak = top + 0.15f - vStart.y;
        cc.enabled = false;
        GameAudio.Play(SfxId.Vault, transform.position);
        return true;
    }

    void UpdateVault(float dt)
    {
        vT += dt / vDuration;
        float t = Mathf.Clamp01(vT);
        Vector3 p = Vector3.Lerp(vStart, vEnd, t);
        p.y = vStart.y + Mathf.Sin(t * Mathf.PI) * vPeak;
        transform.position = p;
        if (t >= 1f)
        {
            vaulting = false;
            cc.enabled = true;
            GameAudio.Play(SfxId.VaultLand, transform.position);
            GameManager.I.Shake(0.12f);
        }
    }

    public bool NeedsHealth => !IsDead && Health < maxHealth - 1f;

    /// <summary>Damage over time (acid pools): no knockback, no invulnerability window, breaks the combo once a second.</summary>
    public void Burn(float amount)
    {
        if (IsDead || amount <= 0f) return;
        if (GameManager.I && GameManager.I.Won) return;
        Health = Mathf.Max(0f, Health - amount);
        if (Time.time - lastDamageTime > 1f) { lastDamageTime = Time.time; if (GameManager.I) GameManager.I.OnPlayerHurt(); }
        if (Health <= 0f) { lastHitDir = Vector3.forward; Die(); }
    }

    /// <summary>Silent healing (life steal, regen cards) — no popup.</summary>
    public void Regen(float amount)
    {
        if (IsDead) return;
        Health = Mathf.Min(maxHealth, Health + amount);
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        float before = Health;
        Health = Mathf.Min(maxHealth, Health + amount);
        if (DamageNumbers.I) DamageNumbers.I.ShowText(transform.position + Vector3.up * 2.2f, "+" + Mathf.RoundToInt(Health - before), new Color(0.4f, 1f, 0.4f));
    }

    public void Grab(TongueZombie by) => Grabber = by;

    public void ReleaseGrab(TongueZombie by)
    {
        if (Grabber == by) Grabber = null;
    }

    public void TakeDamage(float amount, Vector3 fromDir) => TakeDamage(amount, fromDir, 7f);

    public void TakeDamage(float amount, Vector3 fromDir, float knockForce)
    {
        if (IsDead || Time.time < invulnUntil) return;
        if (GameManager.I && GameManager.I.Won) return; // chapter cleared: the soldier is safe
        invulnUntil = Time.time + invulnerableTime;
        // 1.1.0 Armour plate card: a charge soaks the whole hit (and keeps the combo).
        if (Upgrades.I && Upgrades.I.AbsorbHit(transform.position + Vector3.up * 1.2f, fromDir)) return;
        lastDamageTime = Time.time;
        Health = Mathf.Max(0f, Health - amount);
        fromDir.y = 0f;
        lastHitDir = fromDir;
        knock += fromDir.normalized * knockForce;
        if (flash) flash.Flash(0.1f);
        GameAudio.Play(SfxId.PlayerHurt, transform.position);
        GameManager.I.OnPlayerHurt();
        if (Health <= 0f) Die();
    }

    /// <summary>Continue after death (rewarded ad / gems) or a co-op revive: back on its feet with a share of health.</summary>
    public void Revive(float healthFraction = 1f)
    {
        if (!IsDead) return;
        IsDead = false;
        Health = Mathf.Max(1f, maxHealth * Mathf.Clamp01(healthFraction));
        knock = Vector3.zero;
        invulnUntil = Time.time + 1.5f;
        if (Grabber) Grabber = null;
        if (rig) rig.Revive();
    }

    void Die()
    {
        IsDead = true;
        Grabber = null;
        if (GameAudio.I) GameAudio.I.SetLowHealth(false, 0f);
        if (rig) rig.Die(lastHitDir);
        GameAudio.Play(SfxId.PlayerDeath, transform.position);
        // Co-op: down, not out — the team carries on (CoopTeam: revive / bleed-out / team over).
        if (GameMode.IsCoop) GameManager.I.OnLocalDown();
        else GameManager.I.TriggerGameOver();
    }
}
