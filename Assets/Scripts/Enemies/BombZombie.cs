using UnityEngine;

/// <summary>
/// Bombalı — a bloated mutant full of volatile gas. Staggers toward the player (drunk walk), breaks into a
/// run when close, then swells up for ~1 s and bursts (area damage that also hits zombies).
/// Killing it also sets it off — best done from a distance, ideally inside a crowd.
/// </summary>
public class BombZombie : Zombie
{
    public float runDistance = 6f;
    public float runSpeedMul = 1.7f;
    public float triggerDistance = 2f;
    public float swellTime = 1f;
    public float blastRadius = 3.6f;
    public float blastZombieDamage = 160f;
    public float blastPlayerDamage = 26f;
    [Tooltip("Delay before a bomb killed by another blast goes off (makes chains readable).")]
    public float chainDelay = 0.15f;

    static readonly int SwellState = Animator.StringToHash("Swell");

    enum Phase { Approach, Swelling, Burst }
    Phase phase;
    float phaseT, fuse, curSpeed;

    protected override SfxId SpawnSfx => SfxId.BombSpawn;

    protected override void OnSpawned()
    {
        phase = Phase.Approach;
        phaseT = 0f;
        fuse = -1f;
    }

    protected override float CurrentSpeed => curSpeed;

    // ---- co-op: the clients' puppets swell with the host's bomb (the blast itself comes as an explosion event)
    public override byte NetAct => (byte)(phase == Phase.Swelling ? 1 : 0);

    protected override void ApplyNetAct(byte act, byte aux)
    {
        bool swell = act == 1;
        if (swell && phase != Phase.Swelling) StartSwell();
        else if (!swell && phase == Phase.Swelling) { phase = Phase.Approach; model.localScale = modelScale; }
    }

    protected override bool PuppetKilled(Vector3 dir)
    {
        if (Fx.I) Fx.I.Death(transform.position + Vector3.up * 0.9f, Vector3.up);
        return true;
    }

    protected override void UpdateBehaviour(float dt)
    {
        var who = PickSoldier(dt);
        Vector3 dir = ToPlayer(out float dist);
        if (phase == Phase.Approach)
        {
            bool run = dist < runDistance;
            curSpeed = speed * (run ? runSpeedMul : 1f);
            gait = run ? 2f : 1f;
            Chase(dt, curSpeed, true);
            Moan(dt, dist);
            if (dist < triggerDistance && who && !who.IsDead) StartSwell();
            return;
        }

        // Swelling: stand still (still pushed by knockback), puff up, blink, then burst.
        curSpeed = 0f;
        gait = 0f;
        Move(dt, Vector3.zero, 0f);
        Face(dir, dt);
        if (phaseT >= swellTime) Burst(true);
    }

    void StartSwell()
    {
        phase = Phase.Swelling;
        phaseT = 0f;
        animator.CrossFadeInFixedTime(SwellState, 0.12f, 0);
        GameAudio.Play(SfxId.BombFuse, transform.position);
    }

    protected override void Animate(float dt)
    {
        base.Animate(dt);
        if (phase != Phase.Swelling) return;
        phaseT += dt;
        float k = Mathf.Clamp01(phaseT / swellTime);
        float wobble = 1f + 0.06f * Mathf.Sin(phaseT * (14f + 30f * k));
        float s = Mathf.Lerp(1f, 1.4f, k * k) * wobble;
        model.localScale = Vector3.Scale(modelScale, new Vector3(s, Mathf.Lerp(1f, 1.12f, k), s));
        // Blink faster and faster.
        if (Mathf.Repeat(phaseT * (4f + 16f * k), 1f) < 0.12f) flash.Flash(0.04f);
    }

    protected override bool OnKilled(Vector3 dir, bool explosive)
    {
        // Shot or caught in a blast: goes off (a beat later when chained) instead of falling over.
        phase = Phase.Burst;
        fuse = explosive ? chainDelay : 0f;
        if (fuse <= 0f) Burst(false);
        else flash.Flash(chainDelay);
        return true;
    }

    void LateUpdate()
    {
        if (phase != Phase.Burst || fuse < 0f) return;
        fuse -= Time.deltaTime;
        if (fuse <= 0f) Burst(false);
    }

    void Burst(bool selfTriggered)
    {
        if (!gameObject.activeSelf) return;
        fuse = -1f;
        phase = Phase.Burst;
        Vector3 pos = transform.position;
        if (selfTriggered)
        {
            // Reached the player: the blast counts as its attack — no kill credit.
            body.enabled = false;
        }
        if (Fx.I) Fx.I.Death(pos + Vector3.up * 0.9f, Vector3.up);
        ReleaseToPool();
        Explosions.Explode(pos, blastRadius, blastZombieDamage, blastPlayerDamage, SfxId.BombExplode, 0.9f, this);
    }
}
