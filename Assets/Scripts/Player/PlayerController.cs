using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform model;
    public Weapon weapon;
    public HitFlash flash;

    [Header("Movement")]
    public float moveSpeed = 6.2f;
    public float turnSpeed = 1080f;

    [Header("Vault")]
    public float vaultBaseDuration = 0.2f;
    public float vaultDurationPerMeter = 0.06f;

    [Header("Health")]
    public float maxHealth = 100f;
    public float invulnerableTime = 0.4f;

    public float Health { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsVaulting => vaulting;

    CharacterController cc;
    Vector3 knock;
    float invulnUntil;

    // visuals
    float bobPhase, moveBlend, squash = 1f, deathT;
    Vector3 modelBasePos;

    // vault
    bool vaulting;
    Vector3 vStart, vEnd;
    float vT, vDuration, vPeak;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        Health = maxHealth;
        modelBasePos = model.localPosition;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (IsDead) { DeathAnim(dt); return; }

        var input = TouchControls.I;
        Vector2 mv = input ? input.Move : Vector2.zero;
        Vector3 move = new Vector3(mv.x, 0f, mv.y);
        float mag = Mathf.Clamp01(move.magnitude);
        Vector3 moveDir = mag > 0.01f ? move / mag : Vector3.zero;

        if (vaulting) UpdateVault(dt);
        else if (!(mag > 0.5f && TryStartVault(moveDir)))
        {
            Vector3 vel = move * moveSpeed + knock;
            vel.y = -6f;
            cc.Move(vel * dt);
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

        AnimateModel(dt, vaulting ? 0f : mag);
    }

    bool TryStartVault(Vector3 dir)
    {
        if (!Vault.TryCompute(transform.position, dir, cc.radius, cc.radius + 0.3f, out var land, out var top)) return false;
        vaulting = true;
        vStart = transform.position;
        vEnd = land;
        vT = 0f;
        vDuration = vaultBaseDuration + Vector3.Distance(vStart, vEnd) * vaultDurationPerMeter;
        vPeak = top + 0.15f - vStart.y;
        cc.enabled = false;
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
            squash = 0.72f;
            GameManager.I.Shake(0.12f);
        }
    }

    void AnimateModel(float dt, float moveAmount)
    {
        moveBlend = Mathf.MoveTowards(moveBlend, moveAmount, dt * 6f);
        bobPhase += dt * (9f + 6f * moveBlend);
        squash = Mathf.MoveTowards(squash, 1f, dt * 3.5f);

        float bob = Mathf.Abs(Mathf.Sin(bobPhase)) * 0.08f * moveBlend;
        float roll = Mathf.Sin(bobPhase) * 5f * moveBlend;
        float lean = 8f * moveBlend;
        float recoil = weapon ? weapon.Recoil : 0f;

        if (vaulting)
        {
            float t = Mathf.Clamp01(vT);
            lean = Mathf.Sin(t * Mathf.PI) * 25f;
        }

        model.localPosition = modelBasePos + new Vector3(0f, bob, -0.08f * recoil);
        model.localRotation = Quaternion.Euler(lean - 6f * recoil, 0f, roll);
        float s = squash;
        float inv = 1f / Mathf.Sqrt(s);
        model.localScale = new Vector3(inv, s, inv);
    }

    public void TakeDamage(float amount, Vector3 fromDir)
    {
        if (IsDead || Time.time < invulnUntil) return;
        invulnUntil = Time.time + invulnerableTime;
        Health = Mathf.Max(0f, Health - amount);
        fromDir.y = 0f;
        knock += fromDir.normalized * 7f;
        if (flash) flash.Flash(0.1f);
        GameManager.I.OnPlayerHurt();
        if (Health <= 0f) Die();
    }

    void Die()
    {
        IsDead = true;
        deathT = 0f;
        GameManager.I.TriggerGameOver();
    }

    void DeathAnim(float dt)
    {
        deathT = Mathf.Min(1f, deathT + dt / 0.45f);
        float e = 1f - (1f - deathT) * (1f - deathT);
        model.localRotation = Quaternion.Euler(-85f * e, 0f, 0f);
        model.localPosition = modelBasePos + Vector3.up * 0.15f * e;
        model.localScale = Vector3.one;
    }
}
