using System;
using UnityEngine;

/// <summary>A radio line: Loc key + optional direction key ({0}) and number ({1}). Formatted by the HUD.</summary>
public struct RadioMsg
{
    public string key;
    public string argKey;
    public int num;
}

/// <summary>
/// Story objectives (state owner). A plan (<see cref="EndlessPlan"/> by default, a campaign chapter later) says what
/// comes next; the director picks a site in the city (a natural landmark nearby, or forces a chunk 4 chunks away),
/// tracks the player in the zone, pays bonus score/XP and talks over the radio. Visuals and HUD only read it /
/// listen to its events. Its randomness is its own seeded System.Random.
/// </summary>
public class ObjectiveDirector : MonoBehaviour
{
    public static ObjectiveDirector I { get; private set; }

    public IObjectivePlan Plan { get; set; } = new EndlessPlan();

    [Tooltip("Search ring (chunks) for an existing landmark of the needed type.")]
    public int searchMin = 3, searchMax = 6;
    [Tooltip("Distance (chunks) of a forced site when no landmark is around.")]
    public int forceDistance = 4;
    public float issueDelay = 4f;
    [Tooltip("Tests: multiplies every hold time.")]
    public float holdScale = 1f;

    System.Random rng;
    float cooldown, lostRadioT, nextBeep, pickAt = -1f;
    bool halfSaid, heliSaid, wasInside;

    public bool HasActive { get; private set; }
    public ObjectiveStep Step { get; private set; }
    public Vector3 TargetGlobal { get; private set; }
    public Vector2Int SiteChunk { get; private set; }
    public float Held { get; private set; }
    public float Hold => Step.hold * holdScale;
    public float Progress01 => Hold > 0f ? Mathf.Clamp01(Held / Hold) : 0f;
    public bool PlayerInZone { get; private set; }
    public int LastIssuedWave { get; private set; } = -1;
    public int CompletedCount { get; private set; }
    public int Helicopters { get; private set; }
    public float IssuedAt { get; private set; }
    public Vector3 TargetLocal => FloatingOrigin.ToLocal(TargetGlobal);

    /// <summary>Helicopter visual: 0 = not yet, 0..1 approach, 1 = landed.</summary>
    public float HeliApproach01
    {
        get
        {
            if (!HasActive || Step.kind != ObjectiveKind.Helicopter) return 0f;
            float before = Mathf.Min(Objectives.HeliArriveBefore * holdScale, Hold);
            return Mathf.Clamp01((Held - (Hold - before)) / Mathf.Max(0.01f, before));
        }
    }

    /// <summary>Spawn interval multiplier (&lt; 1 = more zombies) while holding a zone.</summary>
    public float SpawnPressure => HasActive && PlayerInZone && Step.hold >= Objectives.TowerHold ? Objectives.HoldPressure : 1f;

    public event Action<ObjectiveStep> Issued;
    public event Action<ObjectiveStep> Completed;
    public event Action<RadioMsg> Radio;

    void Awake()
    {
        I = this;
        int seed = ChunkManager.I ? ChunkManager.I.seed : 1;
        rng = new System.Random(seed ^ 0x0b1ec7);
    }

    void Start() => cooldown = 0f;

    void Update()
    {
        var gm = GameManager.I;
        if (!gm || gm.IsGameOver || !gm.player) return;
        // Helicopter reward card comes after the story beat, not on top of it.
        if (pickAt >= 0f && gm.GameTime >= pickAt) { pickAt = -1f; if (Progression.I) Progression.I.GrantPick(); }
        int wave = WaveDirector.I ? WaveDirector.I.Wave : 0;

        if (!HasActive)
        {
            cooldown -= Time.deltaTime;
            if (cooldown <= 0f && !Plan.Finished && Plan.ShouldIssue(wave, LastIssuedWave, false)) Issue(Plan.Next(rng));
            return;
        }

        Vector3 d = gm.player.transform.position - TargetLocal; d.y = 0f;
        PlayerInZone = d.sqrMagnitude < Step.radius * Step.radius && !gm.player.IsDead;
        float dt = Time.deltaTime;

        if (Step.hold <= 0f)
        {
            if (PlayerInZone) Complete();
            return;
        }
        if (PlayerInZone)
        {
            Held += dt;
            if (Step.kind != ObjectiveKind.Supply && Held >= nextBeep)
            {
                nextBeep += Step.kind == ObjectiveKind.Tower ? 2f * holdScale : 5f * holdScale;
                GameAudio.Play(SfxId.ObjectiveTick, TargetLocal, 1f + 0.5f * Progress01);
            }
            if (!halfSaid && Step.kind == ObjectiveKind.Tower && Progress01 >= 0.5f) { halfSaid = true; Say("radio.tower_half"); }
            if (!heliSaid && Step.kind == ObjectiveKind.Helicopter && HeliApproach01 > 0f) { heliSaid = true; Say("radio.heli_close"); }
            if (Held >= Hold) Complete();
        }
        else
        {
            // Supply crate: prying it open needs you to stay; the zone holds keep their progress.
            if (Step.kind == ObjectiveKind.Supply) Held = Mathf.Max(0f, Held - dt * 2f);
            else if (wasInside && Time.time > lostRadioT) { lostRadioT = Time.time + 12f; Say(Step.kind == ObjectiveKind.Tower ? "radio.tower_lost" : "radio.heli_lost"); }
        }
        wasInside = PlayerInZone;
    }

    /// <summary>Starts an objective of a given kind right now (tests, campaign scripting).</summary>
    public void ForceIssue(ObjectiveKind kind) => Issue(Objectives.Step(kind, rng));

    void Issue(ObjectiveStep step)
    {
        var gm = GameManager.I;
        var cm = ChunkManager.I;
        Vector3 pg = FloatingOrigin.ToGlobal(gm.player.transform.position);
        var from = new Vector2Int(Mathf.FloorToInt(pg.x / CityLayout.Size), Mathf.FloorToInt(pg.z / CityLayout.Size));
        Vector2Int site = default;
        bool found = cm && CityLayout.IsLandmark(step.site) && cm.FindType(step.site, from, searchMin, searchMax, out site);
        if (!found)
        {
            site = from;
            for (int tries = 0; tries < 8; tries++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                var c = new Vector2Int(from.x + Mathf.RoundToInt(Mathf.Cos(a) * forceDistance), from.y + Mathf.RoundToInt(Mathf.Sin(a) * forceDistance));
                if (cm && cm.IsActive(c) && tries < 7) continue;
                if (c.x >= -1 && c.x <= 0 && c.y >= -1 && c.y <= 0 && tries < 7) continue; // keep the spawn blocks
                if (cm && CityLayout.IsObjectiveSite(cm.TypeAt(c)) && tries < 7) continue;
                site = c;
                break;
            }
            if (cm) cm.ForceType(site, step.site);
        }
        SiteChunk = site;
        TargetGlobal = CityLayout.SiteGlobal(site, step.site);
        Step = step;
        HasActive = true;
        Held = 0f;
        nextBeep = 0f;
        halfSaid = heliSaid = wasInside = PlayerInZone = false;
        IssuedAt = gm.GameTime;
        LastIssuedWave = WaveDirector.I ? WaveDirector.I.Wave : 0;

        Vector3 delta = TargetGlobal - pg;
        Radio?.Invoke(new RadioMsg { key = step.briefKey, argKey = Objectives.DirectionKey(new Vector2(delta.x, delta.z)), num = Mathf.RoundToInt(step.hold * holdScale) });
        Issued?.Invoke(step);
    }

    void Complete()
    {
        var gm = GameManager.I;
        var step = Step;
        HasActive = false;
        PlayerInZone = false;
        CompletedCount++;
        Plan.Completed(step);
        int wave = WaveDirector.I ? WaveDirector.I.Wave : 0;
        Objectives.Reward(step.kind, wave, Helicopters, out int score, out int xp);
        gm.AddBonus(score);
        var player = gm.player;
        switch (step.kind)
        {
            case ObjectiveKind.Flare: Say("radio.flare_done"); break;
            case ObjectiveKind.Tower: Say("radio.tower_done"); break;
            case ObjectiveKind.Supply:
                if (Objectives.SupplyGivesHeal(player.Health / player.maxHealth)) { player.Heal(player.maxHealth); Say("radio.supply_heal"); }
                else { if (Progression.I) Progression.I.GrantPick(); Say("radio.supply_card"); }
                break;
            case ObjectiveKind.Helicopter:
                Helicopters++;
                player.Heal(player.maxHealth);
                pickAt = gm.GameTime + 3.5f;
                Say("radio.heli_done");
                break;
        }
        if (Progression.I) Progression.I.AddXp(xp);
        GameAudio.Play(step.kind == ObjectiveKind.Helicopter ? SfxId.ObjectiveBig : SfxId.ObjectiveDone, player.transform.position);
        cooldown = issueDelay;
        LastBonus = score;
        Completed?.Invoke(step);
        if (step.kind == ObjectiveKind.Helicopter) Say("radio.heli_after");
        else if (Plan is EndlessPlan ep && ep.HelicopterDue) Say("radio.heli_soon");
    }

    public int LastBonus { get; private set; }

    void Say(string key) => Radio?.Invoke(new RadioMsg { key = key });
}
