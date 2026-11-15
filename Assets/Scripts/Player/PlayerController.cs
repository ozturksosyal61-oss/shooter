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

    public float Health { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsVaulting => vaulting;

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

    void Update()
    {
        float dt = Time.deltaTime;
        if (IsDead) return;

        var input = TouchControls.I;
        Vector2 mv = input ? input.Move : Vector2.zero;
        Vector3 move = new Vector3(mv.x, 0f, mv.y);
        float mag = Mathf.Clamp01(move.magnitude);
        Vector3 moveDir = mag > 0.01f ? move / mag : Vector3.zero;

        if (vaulting) UpdateVault(dt);
        else if (!(mag >= vaultMinInput && TryStartVault(moveDir)))
        {
            Vector3 vel = move * moveSpeed + knock;
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
        if (waves && waves.InBreak && Health < maxHealth && Time.time - lastDamageTime > 2f)
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

    public void Heal(float amount)
    {
        if (IsDead) return;
        float before = Health;
        Health = Mathf.Min(maxHealth, Health + amount);
        if (DamageNumbers.I) DamageNumbers.I.ShowText(transform.position + Vector3.up * 2.2f, "+" + Mathf.RoundToInt(Health - before), new Color(0.4f, 1f, 0.4f));
    }

    public void TakeDamage(float amount, Vector3 fromDir)
    {
        if (IsDead || Time.time < invulnUntil) return;
        invulnUntil = Time.time + invulnerableTime;
        lastDamageTime = Time.time;
        Health = Mathf.Max(0f, Health - amount);
        fromDir.y = 0f;
        lastHitDir = fromDir;
        knock += fromDir.normalized * 7f;
        if (flash) flash.Flash(0.1f);
        GameAudio.Play(SfxId.PlayerHurt, transform.position);
        GameManager.I.OnPlayerHurt();
        if (Health <= 0f) Die();
    }

    void Die()
    {
        IsDead = true;
        if (GameAudio.I) GameAudio.I.SetLowHealth(false, 0f);
        if (rig) rig.Die(lastHitDir);
        GameAudio.Play(SfxId.PlayerDeath, transform.position);
        GameManager.I.TriggerGameOver();
    }
}
