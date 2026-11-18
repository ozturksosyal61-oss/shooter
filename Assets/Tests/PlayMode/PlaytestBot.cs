using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Plays full runs with a bot through the real input path (WASD + mouse + R) and records telemetry,
/// screenshots (with HUD) and frame hitches with profiler markers. Explicit — run with
///   -testFilter PlaytestBot.Play
/// All strategies run one after another in ONE Unity session (one test case each):
///   -testFilter PlaytestBot.Play   (or -testCategory Balance)
/// env: PLAYTEST_STRATEGIES = comma list (default turret,human,kite,explore), PLAYTEST_TIMESCALE (default 8),
///      PLAYTEST_MAXTIME (default 600 s game time — long enough to reach the wave-10 boss), PLAYTEST_SEED (fixed city/spawn seed, default random),
///      PLAYTEST_SHOTS=1 for screenshots (default off), CAPTURE_DIR = output folder.
/// High time scales stay physics-compatible and reproducible: Time.captureDeltaTime fixes every frame to exactly
/// 1/60 s of game time (below fixedDeltaTime 0.02, like a 60 fps device) whatever the machine's real frame rate, and
/// the frame cap is 60·timescale. So results don't depend on the time scale (same seed ⇒ same run); the time scale
/// only sets the upper bound of the speed-up. Bot timers use game/unscaled-capture time, never wall clock.
/// Runs fine with -nographics (much faster). The summary prints the achieved speed-up.
/// "kite" is the smart bot: backs off while circling, collects health packs, nails every active reload.
/// "human" moves like kite but hits the active reload only ~60% of the time, aims with a little jitter and reacts
/// to new targets with a short delay. Every bot picks level-up cards by clicking them (kite/human by priority).
/// </summary>
public class PlaytestBot
{
    Mouse mouse;
    Keyboard kb;

    [SetUp]
    public void SetUp()
    {
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        mouse = InputSystem.AddDevice<Mouse>();
        kb = InputSystem.AddDevice<Keyboard>();
        mouse.MakeCurrent();
        kb.MakeCurrent();
    }

    [TearDown]
    public void TearDown()
    {
        GameManager.TimeScale = 1f;
        GameMode.Set(RunMode.Endless);
        Time.maximumDeltaTime = 0.3333333f;
        Time.captureDeltaTime = 0f;
        ChunkManager.SeedOverride = 0;
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(kb);
    }

    static string Env(string k, string d) => System.Environment.GetEnvironmentVariable(k) ?? d;

    static IEnumerable<string> Strategies() =>
        Env("PLAYTEST_STRATEGIES", Env("PLAYTEST_STRATEGY", "turret,human,kite,explore"))
            .Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries);

    static float EnvF(string k, float d) =>
        float.Parse(Env(k, d.ToString(System.Globalization.CultureInfo.InvariantCulture)), System.Globalization.CultureInfo.InvariantCulture);

    [UnityTest, Explicit, Category("Balance"), Timeout(1800000)]
    public IEnumerator Play([ValueSource(nameof(Strategies))] string strat)
    {
        string dir = Env("CAPTURE_DIR", Application.temporaryCachePath);
        float ts = EnvF("PLAYTEST_TIMESCALE", 8f);
        bool shots = Env("PLAYTEST_SHOTS", "0") == "1";
        float maxTime = EnvF("PLAYTEST_MAXTIME", 600f);
        int seed = int.Parse(Env("PLAYTEST_SEED", "0"));

        GameManager.TimeScale = ts;
        ChunkManager.SeedOverride = seed;
        if (seed != 0) Random.InitState(seed);
        SaveSystem.Store = new MemorySaveStore();
        SaveSystem.Data.tutorialDone = Env("PLAYTEST_TUTORIAL", "0") != "1";
        string weapon = Env("PLAYTEST_WEAPON", "");
        WeaponLoadout.Override = System.Enum.TryParse(weapon, out WeaponId wid) ? wid : (WeaponId?)null;
        // PLAYTEST_MODE=hard → hard mode (limited ammo, tougher waves).
        GameMode.Set(Env("PLAYTEST_MODE", "") == "hard" ? RunMode.Hard : RunMode.Endless);
        SceneManager.LoadScene("Game");
        yield return null;
        // GameManager caps the game at 60 fps; at timescale N allow 60·N so a frame is still ~1/60 s of game time.
        Application.targetFrameRate = Mathf.RoundToInt(60f * Mathf.Max(1f, ts));
        Time.captureDeltaTime = 1f / (60f * Mathf.Max(1f, ts)); // × timeScale ⇒ exactly 1/60 s game time per frame
        Time.maximumDeltaTime = 0.05f;
        float realStart = Time.realtimeSinceStartup;
        yield return null;
        var gm = GameManager.I;
        var cam = Camera.main;
        var w = gm.player.weapon;

        int shotsFired = 0, hits = 0, kills = 0, hurts = 0, vaults = 0, reloads = 0, perfect = 0, jams = 0, packs = 0, breaks = 0;
        int blocks = 0, bombs = 0, barrels = 0, grabs = 0, snaps = 0, levelUps = 0;
        int objIssued = 0, objDone = 0, helis = 0;
        float objTimeSum = 0f;
        var objLog = new StringBuilder();
        var od = ObjectiveDirector.I;
        System.Action<ObjectiveStep> onIssued = st => { objIssued++; objLog.Append($"{gm.GameTime:0}s:+{st.kind} "); };
        System.Action<ObjectiveStep> onDone = st =>
        {
            objDone++; if (st.kind == ObjectiveKind.Helicopter) helis++;
            objTimeSum += gm.GameTime - od.IssuedAt;
            objLog.Append($"{gm.GameTime:0}s:✓{st.kind}({gm.GameTime - od.IssuedAt:0}s) ");
        };
        if (od) { od.Issued += onIssued; od.Completed += onDone; }
        Vector3 stuckRef = Vector3.zero; float stuckT = 0f, detourT = 0f; Vector3 detour = Vector3.zero;
        var cardLog = new StringBuilder();
        var rng = new System.Random(strat.GetHashCode());
        bool reloadPlanned = false, reloadWillHit = false;
        float reloadPressAt = 0f;
        Zombie aimTarget = null;
        float targetSince = 0f;
        Vector3 aimJitter = Vector3.zero;
        float cardOpenAt = -1f;
        bool cardClicked = false;
        var frameEvents = new List<SfxId>();
        int fontRebuilds = 0, fontRebuildsFrame = 0;
        System.Action<Font> onFont = f => { fontRebuilds++; };
        Font.textureRebuilt += onFont;
        System.Action<SfxId, Vector3> listen = (id, p) =>
        {
            frameEvents.Add(id);
            switch (id)
            {
                case SfxId.Shoot: shotsFired++; break;
                case SfxId.Hit: case SfxId.Crit: hits++; break;
                case SfxId.Kill: kills++; break;
                case SfxId.PlayerHurt: hurts++; break;
                case SfxId.Vault: vaults++; break;
                case SfxId.ReloadStart: reloads++; break;
                case SfxId.ActiveReloadPerfect: perfect++; break;
                case SfxId.ActiveReloadFail: jams++; break;
                case SfxId.Pickup: packs++; break;
                case SfxId.ComboBreak: breaks++; break;
                case SfxId.ShieldBlock: blocks++; break;
                case SfxId.BombExplode: bombs++; break;
                case SfxId.BarrelExplode: barrels++; break;
                case SfxId.TongueGrab: grabs++; break;
                case SfxId.TongueSnap: snaps++; break;
                case SfxId.LevelUp: levelUps++; break;
            }
        };
        GameAudio.OnSfx += listen;

        // Profiler recorders for hitch attribution.
        var recs = new (string name, ProfilerRecorder r)[]
        {
            ("ChunkBuild", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Game.ChunkBuild")),
            ("PoolGrow", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Game.PoolGrow")),
            ("GC.Collect", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Collect")),
            ("GCAlloc", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame")),
            ("ShaderCompile", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shader.CreateGPUProgram")),
            ("ShaderParse", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shader.Parse")),
            ("Instantiate", ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Instantiate")),
            ("Loading", ProfilerRecorder.StartNew(ProfilerCategory.Loading, "Loading.ReadObject")),
        };

        var log = new StringBuilder();
        var hitches = new StringBuilder();
        log.AppendLine($"strategy={strat} seed={ChunkManager.I.seed} timescale={ts}");
        foreach (var (name, r) in recs) log.Append($"{name}:{(r.Valid ? "ok" : "invalid")} ");
        log.AppendLine();
        log.AppendLine("t\twave\tphase\thp\tscore\tkills\talive\tnear\tammo\tcombo\tmult");
        float nextLog = 0f, nextShot = Env("PLAYTEST_TUTORIAL", "0") == "1" ? 2f : 6f, firstHurt = -1f;
        int maxAlive = 0, hitchCount = 0, shotIndex = 0;
        int ammoBoxes = 0, lastReserve = int.MaxValue;
        float dryTime = 0f;
        Vector3 wander = Vector3.forward;
        float wanderT = 0f, rPulse = 0f;
        Vector3 lastMove = Vector3.forward;
        bool rDown = false;

        bool bailing = false;
        var bossDir = BossDirector.I;
        // PLAYTEST_BOSS=1: boss scenario — level up to a typical first-boss level (the bot picks its own cards),
        // then jump to the breather before the first boss wave. The run ends a few seconds after the boss dies.
        bool bossScenario = Env("PLAYTEST_BOSS", "0") == "1";
        if (bossScenario)
        {
            int lvl = (int)EnvF("PLAYTEST_BOSS_LEVEL", 8f);
            while (Progression.I.Level < lvl) Progression.I.AddXp(Progression.I.XpToNext - Progression.I.Xp);
            WaveDirector.I.JumpToBreakBefore(BossRules.FirstBossWave);
        }
        float bossSeen = -1f, bossDown = -1f, bossHurtStart = 0f;
        int bossHurts = 0, chargeDodges = 0, chargeHits = 0;
        ButcherZombie.Phase lastBossPhase = ButcherZombie.Phase.Walk;
        try
        {
            while (!gm.IsGameOver && gm.GameTime < maxTime && !(bossScenario && bossDown >= 0f && gm.GameTime - bossDown > 5f))
            {
                if (bossDir && bossDown < 0f && bossDir.BossesDefeated > 0) bossDown = gm.GameTime;
                // Level-up cards: let go of everything and click the chosen card (real pointer path).
                var picker = CardPicker.I;
                if (picker && picker.IsOpen)
                {
                    InputSystem.QueueStateEvent(kb, new KeyboardState());
                    if (cardOpenAt < 0f) { cardOpenAt = Time.unscaledTime; cardClicked = false; }
                    var cms = new MouseState();
                    int idx = ChooseCard(picker, strat);
                    cms.position = picker.CardScreenPoint(idx);
                    if (!cardClicked && Time.unscaledTime - cardOpenAt > picker.inputDelay + 0.15f)
                    {
                        cms = cms.WithButton(MouseButton.Left, true);
                        cardClicked = true;
                        cardLog.Append($"{gm.GameTime:0}s:{picker.OfferAt(idx)} ");
                    }
                    InputSystem.QueueStateEvent(mouse, cms);
                    yield return null;
                    continue;
                }
                if (cardOpenAt >= 0f)
                {
                    // Lift the finger once after the pick so a held "fire" press starts again.
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    cardOpenAt = -1f;
                    yield return null;
                    continue;
                }

                var p = gm.player.transform.position;
                var nearest = Zombie.FindNearest(p, 40f);
                float near = nearest ? Vector3.Distance(nearest.transform.position, p) : 99f;
                if (strat == "kite" || strat == "human")
                {
                    // A good player shoots a bomb before it gets close (distance logic keeps using the true nearest).
                    foreach (var z in Zombie.Active)
                        if (z.Kind == ZombieKind.Bomb && z.Targetable && (z.transform.position - p).sqrMagnitude < 12f * 12f) { nearest = z; break; }
                }

                ButcherZombie boss = null;
                Vector3 threat = Vector3.zero;
                foreach (var z in Zombie.Active)
                {
                    if (z is ButcherZombie bz && z.Targetable) boss = bz;
                    Vector3 d = p - z.transform.position; d.y = 0f;
                    float m = d.magnitude;
                    bool isBoss = z.Kind == ZombieKind.Boss;
                    if (m < (isBoss ? 16f : 12f) && m > 0.01f) threat += d / (m * m) * (z.Kind == ZombieKind.Bomb ? 3f : isBoss ? 5f : 1f);
                }
                if (boss)
                {
                    if (bossSeen < 0f) { bossSeen = gm.GameTime; bossHurtStart = hurts; }
                    var ph = boss.CurrentPhase;
                    if (lastBossPhase == ButcherZombie.Phase.Charge && ph != ButcherZombie.Phase.Charge)
                    {
                        if (hurts > bossHurts) chargeHits++; else chargeDodges++;
                    }
                    if (ph == ButcherZombie.Phase.Charge && lastBossPhase != ButcherZombie.Phase.Charge) bossHurts = hurts;
                    lastBossPhase = ph;
                }
                wanderT -= Time.unscaledDeltaTime * ts;
                if (wanderT <= 0f) { wanderT = 3f; wander = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * wander; }

                Vector3 move = Vector3.zero;
                bool wantReload = false;
                if (strat == "kite")
                {
                    // Health pack within reach and no zombie on top of us → go get it.
                    Vector3 pack = Vector3.zero; bool hasPack = false;
                    foreach (Transform t in Pickups.I.transform)
                        if (t.gameObject.activeSelf && (t.position - p).sqrMagnitude < 144f) { pack = t.position; hasPack = true; break; }
                    if (hasPack && gm.player.NeedsHealth && near > 3f) { move = (pack - p); move.y = 0f; move.Normalize(); }
                    else if (threat.sqrMagnitude > 0.0004f) move = BestEscape(p, lastMove);
                    else move = wander;
                    lastMove = move;
                    // Reload in quiet moments; active reload at the sweet spot.
                    if (!w.IsReloading && w.Ammo < w.magazineSize * 0.6f && near > 9f) wantReload = true;
                    if (w.IsReloading && w.ActiveReloadResult == -1 && w.ReloadProgress > w.sweetSpotStart + 0.03f && w.ReloadProgress < w.sweetSpotEnd - 0.03f) wantReload = true;
                }
                else if (strat == "human")
                {
                    Vector3 pack = Vector3.zero; bool hasPack = false;
                    foreach (Transform t in Pickups.I.transform)
                        if (t.gameObject.activeSelf && (t.position - p).sqrMagnitude < 100f) { pack = t.position; hasPack = true; break; }
                    if (hasPack && gm.player.NeedsHealth && near > 4f) { move = (pack - p); move.y = 0f; move.Normalize(); }
                    else if (threat.sqrMagnitude > 0.0006f) move = BestEscape(p, lastMove);
                    else move = wander;
                    lastMove = move;
                    if (!w.IsReloading && w.Ammo < w.magazineSize * 0.4f && near > 7f) wantReload = true;
                    // Active reload: decide once per reload; ~60% land in the sweet spot, the rest press at a wrong moment.
                    if (w.IsReloading && w.ActiveReloadResult == -1)
                    {
                        if (!reloadPlanned)
                        {
                            reloadPlanned = true;
                            reloadWillHit = rng.NextDouble() < 0.6;
                            float mid = (w.sweetSpotStart + w.sweetSpotEnd) * 0.5f;
                            reloadPressAt = reloadWillHit ? mid + (float)(rng.NextDouble() - 0.5) * 0.08f
                                : rng.NextDouble() < 0.5 ? 0.2f + (float)rng.NextDouble() * 0.15f : w.sweetSpotEnd + 0.06f + (float)rng.NextDouble() * 0.15f;
                        }
                        if (w.ReloadProgress >= reloadPressAt) wantReload = true;
                    }
                    if (!w.IsReloading) reloadPlanned = false;
                }
                else if (strat == "explore")
                {
                    move = Vector3.forward;
                    if (near < 3.5f && threat.sqrMagnitude > 0f) move = (move + threat.normalized * 1.5f).normalized;
                }

                // Story objectives: every moving bot heads for the active target (smart bots keep dodging on the way)
                // and stays inside a hold zone until it completes.
                if (od && od.HasActive && strat != "turret")
                {
                    Vector3 to = od.TargetLocal - p; to.y = 0f;
                    float dist = to.magnitude;
                    float r = od.Step.radius;
                    Vector3 goal = dist > 0.01f ? to / dist : Vector3.zero;
                    bool inside = dist < r * 0.7f;
                    // A human player backs out of a hold zone when nearly dead and comes back after recovering a bit.
                    if (strat == "human" && od.Step.hold > 0f)
                    {
                        float h01 = gm.player.Health / gm.player.maxHealth;
                        if (h01 < 0.35f) bailing = true; else if (h01 > 0.6f || near > 12f) bailing = false;
                    }
                    else bailing = false;
                    if (bailing && threat.sqrMagnitude > 0.0004f) move = BestEscape(p, lastMove);
                    else if (strat == "explore") move = inside && od.Step.hold > 0f ? Vector3.zero : near < 3.5f && threat.sqrMagnitude > 0f ? (goal + threat.normalized * 1.5f).normalized : goal;
                    else if (!inside) move = GoalMove(p, goal, threat, lastMove, strat == "human" ? 0.9f : 1.2f);
                    else if (od.Step.hold <= 0f || od.Step.kind == ObjectiveKind.Supply) move = goal; // walk onto the item / crate
                    else if (threat.sqrMagnitude > 0.0004f) move = BestEscape(p, lastMove, od.TargetLocal, r * 0.8f);
                    else move = Vector3.zero;
                    // Unstick: no progress for 2.5 s → sidestep for a moment.
                    stuckT += Time.unscaledDeltaTime * ts;
                    if (stuckT > 2.5f)
                    {
                        if ((p - stuckRef).sqrMagnitude < 1.5f && !inside) { detourT = 1.4f; detour = Quaternion.Euler(0f, rng.Next(2) == 0 ? 90f : -90f, 0f) * goal; }
                        stuckRef = p; stuckT = 0f;
                    }
                    if (detourT > 0f) { detourT -= Time.unscaledDeltaTime * ts; move = (detour + goal * 0.3f).normalized; }
                    if (strat == "kite" || strat == "human") lastMove = move.sqrMagnitude > 0.01f ? move : lastMove;
                }

                if (boss && (strat == "kite" || strat == "human"))
                {
                    Vector3 bp = boss.transform.position;
                    Vector3 to = p - bp; to.y = 0f;
                    var ph = boss.CurrentPhase;
                    float react = strat == "human" ? 0.35f : 0.15f;
                    if ((ph == ButcherZombie.Phase.ChargeWind && boss.PhaseTime > react) || ph == ButcherZombie.Phase.Charge)
                    {
                        // Side-step out of the charge line.
                        Vector3 cd = boss.ChargeDirection;
                        float along = Vector3.Dot(to, cd);
                        Vector3 perp = to - cd * along;
                        if (along > -2f && along < boss.ChargeLength + 3f && perp.magnitude < 4.5f)
                            move = perp.sqrMagnitude > 0.04f ? perp.normalized : Vector3.Cross(Vector3.up, cd);
                    }
                    else if (ph == ButcherZombie.Phase.SlamWind && boss.PhaseTime > react * 0.6f && to.magnitude < boss.slamRadius + 1.5f)
                        move = to.normalized;
                    else if (!boss.IsWeak && to.magnitude < 7f && !(od && od.HasActive))
                        move = (to.normalized + move * 0.4f).normalized;
                    if (ph != ButcherZombie.Phase.ChargeWind && ph != ButcherZombie.Phase.Charge)
                    {
                        // Step off debris landing markers and out of the shock ring.
                        foreach (var pr in boss.projectiles)
                        {
                            if (!pr || !pr.Flying) continue;
                            Vector3 a = p - pr.Target; a.y = 0f;
                            if (a.magnitude < pr.Radius + 1f) move = a.sqrMagnitude > 0.01f ? a.normalized : Vector3.right;
                        }
                        if (boss.ShockActive)
                        {
                            Vector3 a = p - boss.ShockCenter; a.y = 0f;
                            if (a.magnitude < boss.slamRadius + 0.5f) move = a.sqrMagnitude > 0.01f ? a.normalized : Vector3.right;
                        }
                    }
                    lastMove = move.sqrMagnitude > 0.01f ? move : lastMove;
                    // Winded boss: everything into it.
                    if (boss.IsWeak && (bp - p).sqrMagnitude < 20f * 20f) nearest = boss;
                }

                var keys = new List<Key>();
                if (move.z > 0.35f) keys.Add(Key.W);
                if (move.z < -0.35f) keys.Add(Key.S);
                if (move.x > 0.35f) keys.Add(Key.D);
                if (move.x < -0.35f) keys.Add(Key.A);
                rPulse -= Time.unscaledDeltaTime * ts; // game time, so R pacing is the same at any time scale
                if (wantReload && !rDown && rPulse <= 0f) { keys.Add(Key.R); rDown = true; rPulse = 0.3f; }
                else rDown = false;
                InputSystem.QueueStateEvent(kb, new KeyboardState(keys.ToArray()));

                var ms = new MouseState();
                if (strat == "turret")
                {
                    ms.position = new Vector2(Screen.width * 0.6f, Screen.height * 0.5f);
                    ms = ms.WithButton(MouseButton.Left, true);
                }
                else if (strat == "human")
                {
                    // Reaction time on target switches + small aim error.
                    if (nearest && (near < 15f || nearest == boss) && !w.IsReloading)
                    {
                        if (nearest != aimTarget) { aimTarget = nearest; targetSince = gm.GameTime; aimJitter = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.9f; }
                        if (gm.GameTime - targetSince > 0.22f)
                        {
                            ms.position = cam.WorldToScreenPoint(nearest.AimPoint + aimJitter);
                            ms = ms.WithButton(MouseButton.Right, true);
                        }
                    }
                }
                else if (nearest && near < 16f && !(strat == "kite" && w.IsReloading))
                {
                    ms.position = cam.WorldToScreenPoint(nearest.AimPoint);
                    ms = ms.WithButton(MouseButton.Right, true);
                }
                InputSystem.QueueStateEvent(mouse, ms);

                frameEvents.Clear();
                fontRebuildsFrame = fontRebuilds;
                yield return null;

                if (hurts > 0 && firstHurt < 0f) firstHurt = gm.GameTime;
                maxAlive = Mathf.Max(maxAlive, Zombie.Active.Count);
                var bw = gm.player.weapon;
                if (bw.Reserve > lastReserve) ammoBoxes++;
                lastReserve = bw.Reserve;
                if (bw.OutOfAmmo) dryTime += Time.deltaTime;
                float frameMs = Time.unscaledDeltaTime * 1000f;
                if (gm.GameTime > 2f && frameMs > 100f && hitchCount < 30)
                {
                    hitchCount++;
                    hitches.Append($"HITCH t={gm.GameTime:0.0} frame={frameMs:0}ms alive={Zombie.Active.Count} kills={kills}");
                    hitches.Append($" fontRebuild={fontRebuilds - fontRebuildsFrame} events=[{string.Join(",", frameEvents)}]");
                    foreach (var (name, r) in recs)
                        if (r.Valid && r.LastValue > 0) hitches.Append($" {name}={(name == "GCAlloc" ? r.LastValue / 1024 + "KB" : (r.LastValue / 1e6).ToString("0.0") + "ms")}");
                    hitches.AppendLine();
                }

                var waves = WaveDirector.I;
                if (gm.GameTime >= nextLog)
                {
                    nextLog += 10f;
                    log.AppendLine($"{gm.GameTime:0}\t{waves.Wave}\t{(waves.InBreak ? "break" : "wave")}\t{gm.player.Health:0}\t{gm.Score}\t{kills}\t{Zombie.Active.Count}\t{near:0.0}\t{w.Ammo}\t{gm.Combo}\tx{gm.Multiplier}");
                }
                if (shots && gm.GameTime >= nextShot)
                {
                    nextShot += 45f;
                    Shot(Path.Combine(dir, $"pt_{strat}_{shotIndex++:00}_{gm.GameTime:000}s.png"));
                }
            }
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState());
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.5f) yield return null;
            if (shots) Shot(Path.Combine(dir, $"pt_{strat}_gameover.png"));
        }
        finally
        {
            GameAudio.OnSfx -= listen;
            Font.textureRebuilt -= onFont;
            if (od) { od.Issued -= onIssued; od.Completed -= onDone; }
            foreach (var (_, r) in recs) r.Dispose();
        }

        float realSec = Time.realtimeSinceStartup - realStart;
        string summary =
            $"PLAYTEST {strat}{(GameMode.IsHard ? "[hard]" : "")}: ts={ts:0} speedup={gm.GameTime / Mathf.Max(0.01f, realSec):0.0}x real={realSec:0}s survived={gm.GameTime:0}s wave={WaveDirector.I.Wave} dead={gm.IsGameOver} score={gm.Score} kills={kills} " +
            $"acc={(shotsFired > 0 ? 100f * hits / shotsFired : 0):0}% hurts={hurts} firstHurt={firstHurt:0}s packs={packs} comboBreaks={breaks} " +
            $"bestCombo={gm.BestCombo} reloads={reloads} perfect={perfect} jams={jams} vaults={vaults} maxAlive={maxAlive} hitches={hitchCount} " +
            $"level={Progression.I.Level} picks={Upgrades.I.Picks} specials={gm.SpecialKills} blocks={blocks} bombs={bombs} barrels={barrels} grabs={grabs} snaps={snaps} " +
            $"objIssued={objIssued} objDone={objDone} helis={helis} objAvg={(objDone > 0 ? objTimeSum / objDone : 0):0}s " +
            $"boss={(bossDir ? bossDir.BossesSpawned : 0)}/{(bossDir ? bossDir.BossesDefeated : 0)} bossAt={bossSeen:0}s " +
            $"bossFight={(bossDir && bossDir.BossesDefeated > 0 ? bossDir.LastFightTime : bossSeen >= 0f ? gm.GameTime - bossSeen : 0):0}s " +
            $"bossHpLeft={(bossDir && bossDir.Boss && bossDir.BossAlive ? 100f * bossDir.Boss.Health / bossDir.Boss.MaxHealth : 0):0}% " +
            $"bossHurts={(bossSeen >= 0f ? hurts - bossHurtStart : 0):0} " +
            (gm.player.weapon.limitedAmmo ? $"reserve={gm.player.weapon.Reserve} ammoBoxes={ammoBoxes} dryTime={dryTime:0}s " : "") +
            (bossDir && bossDir.Boss is ButcherZombie bb ? $"charges={bb.Charges} throws={bb.Throws} chargeHits={bb.ChargeHits} slamHits={bb.SlamHits} shoves={bb.ShoveHits}" : "");
        log.AppendLine(summary);
        log.AppendLine("cards: " + cardLog);
        log.AppendLine("objectives: " + objLog);
        log.Append(hitches);
        File.WriteAllText(Path.Combine(dir, $"playtest_{strat}.txt"), log.ToString());
        Debug.Log(summary);
        Debug.Log(hitches.ToString());
    }

    static readonly CardId[] Priority =
    {
        CardId.DoubleShot, CardId.Pierce, CardId.FireRate, CardId.Turret, CardId.Shotgun, CardId.Grenade, CardId.KillBlast,
        CardId.MaxHealth, CardId.Lifesteal, CardId.BreatherHeal, CardId.Crit, CardId.BigMag, CardId.FastReload, CardId.MoveSpeed, CardId.Magnet,
    };

    /// <summary>Smart bots take the best card by a fixed priority; the simple bots take the first one.</summary>
    static int ChooseCard(CardPicker p, string strat)
    {
        if (strat != "kite" && strat != "human") return 0;
        int best = 0, bestRank = int.MaxValue;
        for (int i = 0; i < p.OfferCount; i++)
        {
            int rank = System.Array.IndexOf(Priority, p.OfferAt(i));
            if (rank < bestRank) { bestRank = rank; best = i; }
        }
        return best;
    }

    /// <summary>Head for a goal while avoiding zombies and buildings: 16 directions scored by goal alignment, zombie
    /// proximity after a 2.5 m step (weighted by <paramref name="caution"/>) and openness.</summary>
    static Vector3 GoalMove(Vector3 p, Vector3 goal, Vector3 threat, Vector3 last, float caution)
    {
        Vector3 best = goal;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 16; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
            Vector3 q = p + d * 2.5f;
            float score = Vector3.Dot(d, goal) * 0.6f + 0.05f * Vector3.Dot(d, last);
            foreach (var z in Zombie.Active)
            {
                Vector3 v = z.transform.position - q; v.y = 0f;
                float m = Mathf.Max(0.5f, v.magnitude);
                if (m < 10f) score -= caution / (m * m) * (z.Kind == ZombieKind.Bomb ? 3f : 1f);
            }
            if (Physics.SphereCast(p + Vector3.up * 0.6f, 0.4f, d, out var hit, 3.5f, GameLayers.BlockerMask))
                score -= 0.8f * (1f - hit.distance / 3.5f);
            if (score > bestScore) { bestScore = score; best = d; }
        }
        return best;
    }

    /// <summary>Escape scoring that keeps the bot inside a circle (hold zones).</summary>
    static Vector3 BestEscape(Vector3 p, Vector3 last, Vector3 center, float radius)
    {
        Vector3 best = last;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 16; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
            Vector3 q = p + d * 2f;
            float score = 0f;
            foreach (var z in Zombie.Active)
            {
                Vector3 v = z.transform.position - q; v.y = 0f;
                float m = Mathf.Max(0.5f, v.magnitude);
                if (m < 12f) score -= 1f / (m * m) * (z.Kind == ZombieKind.Bomb ? 3f : 1f);
            }
            Vector3 c = q - center; c.y = 0f;
            if (c.magnitude > radius) score -= 2f * (c.magnitude - radius);
            if (Physics.SphereCast(p + Vector3.up * 0.6f, 0.4f, d, out var hit, 3f, GameLayers.BlockerMask)) score -= 0.4f;
            score += 0.03f * Vector3.Dot(d, last);
            if (score > bestScore) { bestScore = score; best = d; }
        }
        return best;
    }

    /// <summary>
    /// "Good player" movement: score 16 directions by how close zombies would be after a 2.5 m step,
    /// how open the way is (no buildings within 4 m) and continuity with the previous direction.
    /// </summary>
    static Vector3 BestEscape(Vector3 p, Vector3 last)
    {
        Vector3 best = last;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 16; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
            Vector3 q = p + d * 2.5f;
            float score = 0f;
            foreach (var z in Zombie.Active)
            {
                Vector3 v = z.transform.position - q; v.y = 0f;
                float m = Mathf.Max(0.5f, v.magnitude);
                if (m < 14f) score -= 1f / (m * m) * (z.Kind == ZombieKind.Bomb ? 3f : 1f);
            }
            if (Physics.SphereCast(p + Vector3.up * 0.6f, 0.4f, d, out var hit, 4f, GameLayers.BlockerMask))
                score -= 0.4f * (1f - hit.distance / 4f);
            score += 0.03f * Vector3.Dot(d, last);
            if (score > bestScore) { bestScore = score; best = d; }
        }
        return best;
    }

    /// <summary>Renders the game camera with the HUD composited (canvas temporarily in camera space).</summary>
    public static void Shot(string path)
    {
        var cam = Camera.main;
        var canvas = Object.FindAnyObjectByType<HUD>().GetComponent<Canvas>();
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1.2f;
        Canvas.ForceUpdateCanvases();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.ForceUpdateCanvases();
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.Destroy(rt);
        Object.Destroy(tex);
    }
}
