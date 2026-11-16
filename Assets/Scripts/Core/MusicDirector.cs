using UnityEngine;

/// <summary>
/// Two looping layers (calm ambience + intense combat) cross-faded by an intensity value derived from
/// nearby zombies, combo and firing. Short ducking for big hits; on death the music is muffled
/// with a low-pass filter.
/// </summary>
public class MusicDirector : MonoBehaviour
{
    public static MusicDirector I { get; private set; }

    public AudioSource calm;
    public AudioSource intense;
    public AudioLowPassFilter calmFilter;
    public AudioLowPassFilter intenseFilter;
    [Tooltip("Boss layer (drums + drone) faded in over everything while a boss lives.")]
    public AudioSource boss;
    public AudioLowPassFilter bossFilter;
    [Range(0f, 1f)] public float baseVolume = 0.8f;
    public float nearRadius = 16f;
    public int zombiesForFullIntensity = 14;
    public float deathCutoff = 550f;

    public float Intensity { get; private set; }

    float duck = 1f, duckUntil, cutoff = 22000f, bossMix;
    bool bossOn;

    public bool BossLayerOn => bossOn;
    public void SetBoss(bool on) => bossOn = on;

    void Awake() => I = this;

    void Start()
    {
        calm.Play();
        intense.Play();
        intense.volume = 0f;
        if (boss) { boss.volume = 0f; boss.Play(); }
    }

    public void Duck(float to, float hold)
    {
        duck = Mathf.Min(duck, to);
        duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + hold);
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        var gm = GameManager.I;
        float target = 0f;
        if (gm && gm.player)
        {
            Vector3 p = gm.player.transform.position;
            int near = 0;
            float r2 = nearRadius * nearRadius;
            for (int i = 0; i < Zombie.Active.Count; i++)
                if ((Zombie.Active[i].transform.position - p).sqrMagnitude < r2) near++;
            target = Mathf.Clamp01((float)near / zombiesForFullIntensity);
            if (gm.Combo > 0) target += 0.15f;
            if (gm.player.weapon && gm.player.weapon.IsFiring) target += 0.15f;
            target = Mathf.Clamp01(target + Mathf.Clamp01(gm.GameTime / 300f) * 0.2f);
        }
        // Rise quickly, calm down slowly.
        Intensity = Mathf.MoveTowards(Intensity, target, dt * (target > Intensity ? 1.2f : 0.2f));

        if (Time.unscaledTime > duckUntil) duck = Mathf.MoveTowards(duck, 1f, dt * 2.5f);
        float layer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, Intensity));
        bossMix = Mathf.MoveTowards(bossMix, bossOn && !(gm && gm.IsGameOver) ? 1f : 0f, dt * (bossOn ? 0.8f : 0.35f));
        float under = 1f - 0.75f * bossMix;
        calm.volume = baseVolume * Mathf.Lerp(0.9f, 0.3f, layer) * duck * under;
        intense.volume = baseVolume * layer * duck * under;
        if (boss) boss.volume = baseVolume * bossMix * duck;

        float targetCutoff = gm && gm.IsGameOver ? deathCutoff : 22000f;
        cutoff = Mathf.Lerp(cutoff, targetCutoff, 1f - Mathf.Exp(-3f * dt));
        if (calmFilter) calmFilter.cutoffFrequency = cutoff;
        if (intenseFilter) intenseFilter.cutoffFrequency = cutoff;
        if (bossFilter) bossFilter.cutoffFrequency = cutoff;
    }
}
