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
/// env: PLAYTEST_STRATEGY = turret | kite | human | explore, PLAYTEST_TIMESCALE (default 1), PLAYTEST_SHOTS=0/1,
///      CAPTURE_DIR = output folder.
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
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(kb);
    }

    static string Env(string k, string d) => System.Environment.GetEnvironmentVariable(k) ?? d;

    [UnityTest, Explicit, Timeout(1800000)]
    public IEnumerator Play()
    {
        string strat = Env("PLAYTEST_STRATEGY", "kite");
        string dir = Env("CAPTURE_DIR", Application.temporaryCachePath);
        float ts = float.Parse(Env("PLAYTEST_TIMESCALE", "1"), System.Globalization.CultureInfo.InvariantCulture);
        bool shots = Env("PLAYTEST_SHOTS", "1") == "1";
        const float maxTime = 600f;

        GameManager.TimeScale = ts;
        PlayerPrefs.SetInt("tutorial_done", Env("PLAYTEST_TUTORIAL", "0") == "1" ? 0 : 1);
        SceneManager.LoadScene("Game");
        yield return null;
        yield return null;
        var gm = GameManager.I;
        var cam = Camera.main;
        var w = gm.player.weapon;

        int shotsFired = 0, hits = 0, kills = 0, hurts = 0, vaults = 0, reloads = 0, perfect = 0, jams = 0, packs = 0, breaks = 0;
        int blocks = 0, bombs = 0, barrels = 0, grabs = 0, snaps = 0, levelUps = 0;
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
        Vector3 wander = Vector3.forward;
        float wanderT = 0f, rPulse = 0f;
        Vector3 lastMove = Vector3.forward;
        bool rDown = false;

        try
        {
            while (!gm.IsGameOver && gm.GameTime < maxTime)
            {
                // Level-up cards: let go of everything and click the chosen card (real pointer path).
                var picker = CardPicker.I;
                if (picker && picker.IsOpen)
                {
                    InputSystem.QueueStateEvent(kb, new KeyboardState());
                    if (cardOpenAt < 0f) { cardOpenAt = Time.realtimeSinceStartup; cardClicked = false; }
                    var cms = new MouseState();
                    int idx = ChooseCard(picker, strat);
                    cms.position = picker.CardScreenPoint(idx);
                    if (!cardClicked && Time.realtimeSinceStartup - cardOpenAt > 0.5f)
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

                Vector3 threat = Vector3.zero;
                foreach (var z in Zombie.Active)
                {
                    Vector3 d = p - z.transform.position; d.y = 0f;
                    float m = d.magnitude;
                    if (m < 12f && m > 0.01f) threat += d / (m * m) * (z.Kind == ZombieKind.Bomb ? 3f : 1f);
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

                var keys = new List<Key>();
                if (move.z > 0.35f) keys.Add(Key.W);
                if (move.z < -0.35f) keys.Add(Key.S);
                if (move.x > 0.35f) keys.Add(Key.D);
                if (move.x < -0.35f) keys.Add(Key.A);
                rPulse -= Time.unscaledDeltaTime;
                if (wantReload && !rDown && rPulse <= 0f) { keys.Add(Key.R); rDown = true; rPulse = 0.1f; }
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
                    if (nearest && near < 15f && !w.IsReloading)
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
            foreach (var (_, r) in recs) r.Dispose();
        }

        string summary =
            $"PLAYTEST {strat}: survived={gm.GameTime:0}s wave={WaveDirector.I.Wave} dead={gm.IsGameOver} score={gm.Score} kills={kills} " +
            $"acc={(shotsFired > 0 ? 100f * hits / shotsFired : 0):0}% hurts={hurts} firstHurt={firstHurt:0}s packs={packs} comboBreaks={breaks} " +
            $"bestCombo={gm.BestCombo} reloads={reloads} perfect={perfect} jams={jams} vaults={vaults} maxAlive={maxAlive} hitches={hitchCount} " +
            $"level={Progression.I.Level} picks={Upgrades.I.Picks} specials={gm.SpecialKills} blocks={blocks} bombs={bombs} barrels={barrels} grabs={grabs} snaps={snaps}";
        log.AppendLine(summary);
        log.AppendLine("cards: " + cardLog);
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
