using System;
using UnityEngine;

/// <summary>
/// Boss waves (<see cref="BossRules"/>): on a boss wave the boss rises just outside the view a moment after the
/// wave banner, the camera swings over for a short intro, ordinary spawning thins out and the wave does not end
/// while the boss lives. Killing it pays a big bonus + XP, slow motion, and a guaranteed-epic card pick.
/// Owns the boss state; HUD / music / camera only listen to the events.
/// </summary>
public class BossDirector : MonoBehaviour
{
    public static BossDirector I { get; private set; }

    public ZombieSpawner spawner;
    public int seed = 4242;
    public float spawnDelay = 2.5f;
    public float introSeconds = 2.2f;
    public float rewardPickDelay = 2.2f;

    public Zombie Boss { get; private set; }
    public bool BossAlive => Boss && Boss.Damageable && Boss.gameObject.activeInHierarchy;
    public int BossesSpawned { get; private set; }
    public int BossesDefeated { get; private set; }
    /// <summary>Time from the boss spawn to its death (last boss), for bots / stats.</summary>
    public float LastFightTime { get; private set; }
    public int LastBonus { get; private set; }

    /// <summary>The current wave must not end yet (boss pending or alive).</summary>
    public bool HoldsWave => pendingWave > 0 || BossAlive;
    public float SpawnIntervalMul => HoldsWave ? BossRules.SpawnIntervalMul : 1f;
    public float AliveMul => HoldsWave ? BossRules.AliveMul : 1f;

    public event Action<Zombie> BossSpawned;
    public event Action<Zombie> BossDefeated;

    int pendingWave;
    float pendingT, spawnTime, pickT = -1f;
    bool subscribed;

    void Awake() => I = this;

    void Start() => Subscribe();

    void Subscribe()
    {
        if (subscribed || !WaveDirector.I || !GameManager.I) return;
        WaveDirector.I.WaveStarted += OnWaveStarted;
        GameManager.I.Killed += OnKilled;
        subscribed = true;
    }

    void OnDestroy()
    {
        if (!subscribed) return;
        if (WaveDirector.I) WaveDirector.I.WaveStarted -= OnWaveStarted;
        if (GameManager.I) GameManager.I.Killed -= OnKilled;
    }

    void OnWaveStarted(int wave)
    {
        if (!ModeRules.IsBossWave(GameMode.Current, wave) || BossAlive) return;
        pendingWave = wave;
        pendingT = spawnDelay;
    }

    /// <summary>Campaign objective: the boss comes now (after the usual delay), holding the current wave.</summary>
    public void Summon()
    {
        if (BossAlive || pendingWave > 0) return;
        pendingWave = Mathf.Max(1, WaveDirector.I ? WaveDirector.I.Wave : 1);
        pendingT = spawnDelay;
    }

    void Update()
    {
        Subscribe();
        var gm = GameManager.I;
        if (!gm || gm.IsGameOver) return;
        float dt = Time.deltaTime;
        if (pendingWave > 0)
        {
            pendingT -= dt;
            if (pendingT <= 0f) Spawn(pendingWave);
        }
        if (pickT >= 0f)
        {
            pickT -= Time.unscaledDeltaTime;
            if (pickT < 0f && Progression.I) Progression.I.GrantPick(true);
        }
    }

    /// <summary>Spawns the boss of <paramref name="wave"/> now (tests call this directly).</summary>
    public Zombie Spawn(int wave, Vector3? at = null)
    {
        pendingWave = 0;
        var gm = GameManager.I;
        if (!spawner || !gm) return null;
        Vector3 p = gm.player.transform.position;
        Vector3 pos = at ?? FindSpot(p);
        var wp = Difficulty.ForWave(Mathf.Max(1, wave));
        int index = GameMode.IsCampaign ? BossesSpawned : ModeRules.BossIndex(GameMode.Current, wave);
        Boss = spawner.SpawnBoss(pos, wp, index);
        if (!Boss) return null;
        if (Boss is ButcherZombie b) b.SetSeed(seed + wave);
        BossesSpawned++;
        spawnTime = gm.GameTime;
        if (gm.cameraRig) gm.cameraRig.Focus(Boss.transform, introSeconds);
        if (MusicDirector.I) MusicDirector.I.SetBoss(true);
        if (ObjectiveDirector.I) ObjectiveDirector.I.Say("radio.boss");
        BossSpawned?.Invoke(Boss);
        return Boss;
    }

    Vector3 FindSpot(Vector3 player)
    {
        for (int i = 0; i < 10; i++)
        {
            if (!spawner.TryFindSpawnPoint(player, out var p)) continue;
            if (!Physics.CheckSphere(p + Vector3.up * 1.5f, 1.6f, GameLayers.BlockerMask, QueryTriggerInteraction.Ignore)) return p;
        }
        Vector3 fallback;
        spawner.TryFindSpawnPoint(player, out fallback);
        return fallback == default ? player + Vector3.forward * 14f : fallback;
    }

    void OnKilled(Zombie z)
    {
        if (z.Kind != ZombieKind.Boss) return;
        var gm = GameManager.I;
        int wave = WaveDirector.I ? WaveDirector.I.Wave : BossRules.FirstBossWave;
        BossRules.Reward(wave, BossesDefeated, out int score, out int xp);
        BossesDefeated++;
        LastFightTime = gm.GameTime - spawnTime;
        LastBonus = score;
        gm.AddBonus(score);
        if (Progression.I) Progression.I.AddXp(xp);
        gm.SlowMo(0.25f, 1.4f);
        gm.Shake(1f);
        if (MusicDirector.I) { MusicDirector.I.SetBoss(false); MusicDirector.I.Duck(0.3f, 1.2f); }
        GameAudio.Play(SfxId.ObjectiveBig, gm.player.transform.position);
        if (ObjectiveDirector.I) ObjectiveDirector.I.Say("radio.boss_down");
        pickT = rewardPickDelay;
        BossDefeated?.Invoke(z);
    }
}
