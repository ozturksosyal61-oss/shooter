using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class GameSmokeTest
{
    Mouse mouse;

    [SetUp]
    public void SetUp()
    {
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        mouse = InputSystem.AddDevice<Mouse>();
        mouse.MakeCurrent();
        // Tests never touch the player's real save (selected weapon, currency...).
        SaveSystem.Store = new MemorySaveStore();
        SaveSystem.Data.tutorialDone = true;
        Store.UseFake = Ads.UseFake = true;
        Store.Backend = null; Ads.Backend = null;
        GameManager.TimeScale = 1f;
        GameMode.Set(RunMode.Endless);
    }

    [TearDown]
    public void TearDown()
    {
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        GameManager.TimeScale = 1f;
        GameMode.Set(RunMode.Endless);
        SaveSystem.Store = null;
    }

    static Vector2 ScreenPos(RectTransform rt) => RectTransformUtility.WorldToScreenPoint(null, rt.position);

    IEnumerator Click(Vector2 pos)
    {
        SetMouse(pos, true);
        yield return null;
        yield return null;
        SetMouse(pos, false);
        yield return null;
    }

    void SetMouse(Vector2 pos, bool left)
    {
        InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left, left));
    }

    static IEnumerator Wait(float seconds)
    {
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < seconds) yield return null;
    }

    static IEnumerator LoadGame()
    {
        SceneManager.LoadScene("Game");
        yield return null;
        yield return null;
        // Smoke tests don't pick cards: keep kills from opening the (game-pausing) picker.
        // Card tests trigger level-ups explicitly with AddXp(XpToNext).
        if (Progression.I) Progression.I.baseXp = 1000000;
    }

    static void Teleport(PlayerController p, Vector3 pos)
    {
        var cc = p.GetComponent<CharacterController>();
        cc.enabled = false;
        p.transform.position = pos;
        cc.enabled = true;
        Physics.SyncTransforms();
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator SpawnShootDieRestart()
    {
        GameManager.TimeScale = 3f; // waves make a run long; simulate faster
        yield return LoadGame();
        var gm = GameManager.I;
        Assert.NotNull(gm, "GameManager missing");
        Assert.NotNull(TouchControls.I, "TouchControls missing");
        Assert.AreEqual(25, ChunkManager.I.ActiveChunkCount, "5x5 chunks should be active");

        // Spawn points must be outside the camera view.
        var spawner = Object.FindAnyObjectByType<ZombieSpawner>();
        int outside = 0;
        for (int i = 0; i < 20; i++)
            if (spawner.TryFindSpawnPoint(gm.player.transform.position, out _) && spawner.LastSpawnWasOutsideView) outside++;
        Assert.GreaterOrEqual(outside, 18, "spawns should be just outside the view");

        // Hold left mouse on the right half: fire with auto-aim (no drag).
        var firePos = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
        SetMouse(firePos, true);
        yield return Wait(16f);
        Debug.Log($"SMOKE: zombies={Zombie.Active.Count} score={gm.Score} combo={gm.Combo} hp={gm.player.Health} fps~{1f / Time.smoothDeltaTime:0}");
        Assert.Greater(gm.Score, 0, "auto-aim should have killed zombies");

        // Joystick: press on the left half and drag right -> player should move.
        SetMouse(firePos, false);
        yield return null;
        var leftPos = new Vector2(Screen.width * 0.2f, Screen.height * 0.4f);
        SetMouse(leftPos, true);
        yield return null;
        Vector3 before = gm.player.transform.position;
        SetMouse(leftPos + new Vector2(Screen.height * 0.3f, 0f), true);
        yield return Wait(0.6f);
        Vector3 moved = gm.player.transform.position - before;
        Debug.Log($"SMOKE: moved={moved}");
        Assert.Greater(moved.magnitude, 0.5f, "joystick should move the player");
        SetMouse(leftPos, false);

        // Stand still and let the zombies win.
        float t0 = Time.realtimeSinceStartup;
        while (!gm.IsGameOver && Time.realtimeSinceStartup - t0 < 240f) yield return null;
        Debug.Log($"SMOKE: gameover={gm.IsGameOver} time={gm.GameTime:0.0} score={gm.Score}");
        Assert.IsTrue(gm.IsGameOver, "player should eventually die");

        // The game-over panel (with the TEKRAR button) must be usable within 1 s.
        yield return Wait(1f);
        var hud = Object.FindAnyObjectByType<HUD>();
        Assert.IsTrue(hud.gameOverPanel.activeSelf, "game over panel within 1 s");
        Debug.Log("SMOKE: gameover stats: " + hud.gameOverStats.text.Replace("\n", " | "));
        yield return Wait(1.5f);
        float cutoff = MusicDirector.I.calmFilter.cutoffFrequency;
        Debug.Log($"SMOKE: music cutoff after death={cutoff:0}");
        Assert.Less(cutoff, 3000f, "music should be low-passed on death");
        yield return Click(firePos); // tapping elsewhere must not restart
        Assert.AreSame(gm, GameManager.I, "only the TEKRAR button restarts");
        yield return Click(ScreenPos(hud.restartButton));
        yield return null;
        Assert.AreNotSame(gm, GameManager.I, "TEKRAR should restart the scene");
        Assert.IsFalse(GameManager.I.IsGameOver);
        Debug.Log("SMOKE: restart ok");
    }

    [UnityTest]
    public IEnumerator VaultOverObstacle()
    {
        yield return LoadGame();
        var gm = GameManager.I;
        var leftPos = new Vector2(Screen.width * 0.2f, Screen.height * 0.4f);
        bool success = false;
        int tried = 0;

        foreach (var col in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude))
        {
            if (col.gameObject.layer != GameLayers.Vaultable || tried >= 8) continue;
            var t = col.transform;
            // Approach along the obstacle's shorter horizontal axis.
            Vector3 size = Vector3.Scale(col.size, t.lossyScale);
            Vector3 dir = size.x < size.z ? t.right : t.forward;
            dir.y = 0f; dir.Normalize();
            Vector3 c = col.bounds.center; c.y = 0f;
            float half = Mathf.Min(size.x, size.z) * 0.5f;
            Vector3 start = c - dir * (half + 1.2f);
            Vector3 land = c + dir * (half + 1f);
            if (Physics.CheckCapsule(start + Vector3.up * 0.4f, start + Vector3.up * 1.4f, 0.4f, GameLayers.ObstacleMask) ||
                Physics.CheckCapsule(land + Vector3.up * 0.4f, land + Vector3.up * 1.4f, 0.4f, GameLayers.ObstacleMask)) continue;
            tried++;
            Teleport(gm.player, start);
            yield return null;

            SetMouse(leftPos, true);
            yield return null;
            SetMouse(leftPos + new Vector2(dir.x, dir.z) * Screen.height * 0.3f, true);
            bool vaulted = false;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.2f) { vaulted |= gm.player.IsVaulting; yield return null; }
            SetMouse(leftPos, false);
            yield return null;
            float side = Vector3.Dot(gm.player.transform.position - c, dir);
            Debug.Log($"SMOKE: vault {col.name} vaulted={vaulted} side={side:0.00}");
            if (vaulted && side > 0.5f) { success = true; break; }
        }
        Assert.IsTrue(success, "player should auto-vault a vaultable obstacle");
    }

    [UnityTest]
    public IEnumerator FloatingOriginAndChunks()
    {
        yield return LoadGame();
        var gm = GameManager.I;
        // Walk far away in steps so chunks stream in.
        for (int i = 1; i <= 8; i++)
        {
            Teleport(gm.player, gm.player.transform.position + new Vector3(30f, 0f, 0f));
            yield return null;
            yield return null;
        }
        Vector3 local = gm.player.transform.position;
        Vector3 global = FloatingOrigin.ToGlobal(local);
        Debug.Log($"SMOKE: local={local} global={global} chunks={ChunkManager.I.ActiveChunkCount}");
        Assert.Less(Mathf.Abs(local.x), 125f, "floating origin should keep the player near 0");
        Assert.Greater(global.x, 200f, "global position should keep growing");
        Assert.AreEqual(25, ChunkManager.I.ActiveChunkCount);
        Assert.IsTrue(Physics.Raycast(local + Vector3.up * 5f, Vector3.down, 10f), "ground under the player");
    }

    [UnityTest]
    public IEnumerator MagazineAndReload()
    {
        yield return LoadGame();
        var gm = GameManager.I;
        var w = gm.player.weapon;
        Assert.AreEqual(40, w.Ammo);
        var anim = gm.player.rig.Animator;
        Assert.IsTrue(anim.isHuman && anim.avatar.isValid, "soldier avatar should be a valid humanoid");
        Debug.Log($"SMOKE: humanScale={anim.humanScale:0.00} run={gm.player.rig.RunSpeed:0.00}");

        // Empty the magazine: reload must start automatically and no shots fire meanwhile.
        var firePos = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
        SetMouse(firePos, true);
        float t0 = Time.realtimeSinceStartup;
        while (!w.IsReloading && Time.realtimeSinceStartup - t0 < 6f) yield return null;
        Assert.IsTrue(w.IsReloading, "empty magazine should trigger a reload");
        Assert.AreEqual(0, w.Ammo);
        yield return Wait(0.5f);
        Assert.AreEqual(0, w.Ammo, "no firing during reload");
        SetMouse(firePos, false);
        t0 = Time.realtimeSinceStartup;
        while (w.IsReloading && Time.realtimeSinceStartup - t0 < 3f) yield return null;
        float took = Time.realtimeSinceStartup - t0 + 0.5f;
        Debug.Log($"SMOKE: reload took ~{took:0.00}s ammo={w.Ammo}");
        Assert.AreEqual(40, w.Ammo);

        // Tactical reload: fire until below half, release, wait > 1 s.
        SetMouse(firePos, true);
        while (w.Ammo > 15) yield return null;
        SetMouse(firePos, false);
        Assert.IsFalse(w.IsReloading);
        yield return Wait(1.3f);
        Assert.IsTrue(w.IsReloading || w.Ammo == 40, "tactical reload after 1 s idle with < half magazine");
        Debug.Log("SMOKE: tactical reload ok");
    }

    [UnityTest]
    public IEnumerator ManualAndActiveReload()
    {
        yield return LoadGame();
        var gm = GameManager.I;
        var w = gm.player.weapon;
        var hud = Object.FindAnyObjectByType<HUD>();
        var button = ScreenPos(hud.reloadButton);

        // Spend a few rounds, then tap the reload button.
        var firePos = new Vector2(Screen.width * 0.6f, Screen.height * 0.5f);
        SetMouse(firePos, true);
        while (w.Ammo > 34) yield return null;
        SetMouse(firePos, false);
        yield return Click(button);
        Assert.IsTrue(w.IsReloading, "reload button starts a reload");

        // Active reload: tap again inside the sweet spot.
        while (w.ReloadProgress < (w.sweetSpotStart + w.sweetSpotEnd) * 0.5f) yield return null;
        yield return Click(button);
        Debug.Log($"SMOKE: active reload result={w.ActiveReloadResult} ammo={w.Ammo} bonus={w.BonusShots}");
        Assert.AreEqual(1, w.ActiveReloadResult, "perfect active reload");
        Assert.IsFalse(w.IsReloading);
        Assert.AreEqual(40, w.Ammo);
        Assert.AreEqual(40, w.BonusShots);

        // A mistimed tap jams: the reload takes longer.
        SetMouse(firePos, true);
        while (w.Ammo > 30) yield return null;
        SetMouse(firePos, false);
        yield return Click(button);
        yield return Click(button); // immediately = too early
        Assert.AreEqual(0, w.ActiveReloadResult, "early tap jams");
        Debug.Log("SMOKE: manual/active reload ok");
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator WavesBreatherAndCombo()
    {
        GameManager.TimeScale = 4f;
        yield return LoadGame();
        var gm = GameManager.I;
        var waves = WaveDirector.I;
        Assert.NotNull(waves, "WaveDirector missing");
        SetMouse(new Vector2(Screen.width * 0.6f, Screen.height * 0.5f), true); // keep shooting to survive
        while (waves.Wave < 1) yield return null;
        Assert.IsFalse(waves.InBreak);
        float waveLen = waves.Params.duration;

        // Combo: kill-based steps, broken by damage.
        for (int i = 0; i < 5; i++) gm.RegisterKill(false);
        Assert.AreEqual(2, gm.Multiplier, "5 kills → x2");
        gm.player.TakeDamage(30f, Vector3.forward);
        Assert.AreEqual(0, gm.Combo, "damage breaks the combo");
        Assert.AreEqual(1, gm.Multiplier);

        // Breather: spawning stops and health regenerates.
        while (!waves.InBreak && !gm.IsGameOver) yield return null;
        Assert.IsFalse(gm.IsGameOver, "player should survive wave 1 while shooting");
        SetMouse(new Vector2(Screen.width * 0.6f, Screen.height * 0.5f), false);
        Debug.Log($"SMOKE: wave 1 lasted ~{waveLen}s, breather {waves.PhaseTimeLeft:0.0}s, hp={gm.player.Health:0}");
        foreach (var z in Zombie.Active.ToArray()) z.Despawn();
        float hp0 = gm.player.Health;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 1.2f) yield return null; // ~4.8 s game time
        Assert.AreEqual(0, Zombie.Active.Count, "no spawns during the breather");
        Assert.Greater(gm.player.Health, hp0, "health regenerates during the breather");
        while (waves.InBreak && !gm.IsGameOver) yield return null;
        Assert.AreEqual(2, waves.Wave);
        Debug.Log($"SMOKE: wave 2 started, hp={gm.player.Health:0}");
    }

    [UnityTest]
    public IEnumerator AudioHooks()
    {
        yield return LoadGame();
        var gm = GameManager.I;
        Assert.NotNull(GameAudio.I, "GameAudio missing");
        Assert.NotNull(GameAudio.I.mixer, "AudioMixer missing");
        Assert.IsTrue(GameAudio.I.mixer.GetFloat(AudioPrefs.Music, out _), "MusicVolume must be exposed");
        Assert.IsTrue(GameAudio.I.mixer.GetFloat(AudioPrefs.Sfx, out _), "SfxVolume must be exposed");
        Assert.NotNull(MusicDirector.I, "MusicDirector missing");
        Assert.NotNull(Object.FindAnyObjectByType<AudioListener>(), "listener missing");

        var heard = new System.Collections.Generic.HashSet<SfxId>();
        System.Action<SfxId, Vector3> h = (id, p) => heard.Add(id);
        GameAudio.OnSfx += h;
        try
        {
            // Shoot until the magazine empties (auto reload → animation events), then walk.
            var firePos = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
            SetMouse(firePos, true);
            float t0 = Time.realtimeSinceStartup;
            while (!gm.player.weapon.IsReloading && Time.realtimeSinceStartup - t0 < 8f) yield return null;
            yield return Wait(1.8f);
            SetMouse(firePos, false);
            var leftPos = new Vector2(Screen.width * 0.2f, Screen.height * 0.4f);
            SetMouse(leftPos, true);
            yield return null;
            SetMouse(leftPos + new Vector2(0f, Screen.height * 0.3f), true);
            yield return Wait(1.5f);
            SetMouse(leftPos, false);
            yield return Wait(4f);
        }
        finally { GameAudio.OnSfx -= h; }
        Debug.Log("SMOKE: heard " + string.Join(",", heard));
        foreach (var id in new[] { SfxId.Shoot, SfxId.MagOut, SfxId.MagIn, SfxId.Bolt, SfxId.Footstep, SfxId.ZombieScream })
            Assert.IsTrue(heard.Contains(id), "expected sound " + id);
    }

    /// <summary>Open ground at <paramref name="dist"/> from <paramref name="from"/> with a clear line (no obstacles).</summary>
    static bool TryClearSpot(Vector3 from, float dist, out Vector3 spot)
    {
        for (int i = 0; i < 24; i++)
        {
            Vector3 d = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
            Vector3 p = from + d * dist;
            if (Physics.CheckSphere(p + Vector3.up * 1f, 1.2f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) continue;
            if (Physics.SphereCast(from + Vector3.up * 1f, 0.5f, d, out _, dist, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) continue;
            spot = p;
            return true;
        }
        spot = from;
        return false;
    }

    static Vector3 ClearSpot(Vector3 from, float dist, float startAngle = 0f)
    {
        for (int i = 0; i < 24; i++)
        {
            Vector3 d = Quaternion.Euler(0f, startAngle + i * 15f, 0f) * Vector3.forward;
            Vector3 p = from + d * dist;
            if (Physics.CheckSphere(p + Vector3.up * 1f, 1.2f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) continue;
            if (Physics.SphereCast(from + Vector3.up * 1f, 0.5f, d, out _, dist, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) continue;
            return p;
        }
        Assert.Fail("no clear spot found");
        return from;
    }

    /// <summary>Moves the player to an open area (≥ 10 m clear around) so specials can be tested in isolation.</summary>
    static Vector3 OpenArea(PlayerController p)
    {
        for (int k = 0; k < 40; k++)
        {
            Vector3 c = new Vector3(Random.Range(-30f, 30f), 0f, Random.Range(-30f, 30f));
            if (Physics.CheckSphere(c + Vector3.up * 1f, 5f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) continue;
            Teleport(p, c);
            return c;
        }
        return p.transform.position;
    }

    static IEnumerator WaitUntil(System.Func<bool> cond, float timeout)
    {
        float t0 = Time.realtimeSinceStartup;
        while (!cond() && Time.realtimeSinceStartup - t0 < timeout) yield return null;
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator SpecialZombies()
    {
        GameManager.TimeScale = 2f;
        yield return LoadGame();
        var gm = GameManager.I;
        WaveDirector.I.enabled = false; // stays in the opening break: no regular spawns
        var spawner = Object.FindAnyObjectByType<ZombieSpawner>();
        var p = gm.player;
        var heard = new System.Collections.Generic.List<SfxId>();
        System.Action<SfxId, Vector3> h = (id, pos) => heard.Add(id);
        GameAudio.OnSfx += h;
        try
        {
            var w3 = Difficulty.ForWave(3);
            Assert.Greater(w3.specialChance, 0f, "specials from wave 3");
            Assert.AreEqual(0f, Difficulty.ForWave(2).specialChance, "no specials before wave 3");
            Assert.IsTrue(Difficulty.ForWave(Difficulty.SpecialWaveEvery).specialWave, "every Nth wave is a special wave");
            Assert.Greater(Difficulty.ForWave(Difficulty.SpecialWaveEvery).maxBomb, Difficulty.ForWave(Difficulty.SpecialWaveEvery - 1).maxBomb, "special wave raises limits");

            Vector3 home = OpenArea(p);
            yield return null;

            // --- Kalkanlı: front shots glance off, back shots hurt.
            var sh = (ShieldZombie)spawner.TrySpawnSpecial(ZombieKind.Shield, ClearSpot(home, 7f), w3);
            Assert.NotNull(sh);
            yield return WaitUntil(() => sh.Targetable, 8f);
            float hp0 = sh.Health;
            Vector3 toward = (sh.transform.position - p.transform.position); toward.y = 0f; toward.Normalize();
            yield return Wait(0.6f); // let it turn to face the player
            Assert.IsTrue(sh.FacesShot(toward), "shield should face the player");
            Assert.IsFalse(sh.TakeHit(30f, false, toward, sh.AimPoint), "front hit must be deflected");
            Assert.AreEqual(hp0, sh.Health, 0.01f);
            Assert.IsTrue(sh.TakeHit(30f, false, -toward, sh.AimPoint), "back hit lands");
            Assert.Less(sh.Health, hp0);
            // Real bullets from the front: ricochets, no damage.
            float hp1 = sh.Health;
            int blocks = 0;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 1.2f)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { position = (Vector2)Camera.main.WorldToScreenPoint(sh.AimPoint) }.WithButton(MouseButton.Right, true));
                yield return null;
            }
            InputSystem.QueueStateEvent(mouse, new MouseState());
            foreach (var id in heard) if (id == SfxId.ShieldBlock) blocks++;
            Debug.Log($"SMOKE: shield blocks={blocks} hp {hp1:0}->{sh.Health:0}");
            Assert.Greater(blocks, 0, "bullets should ricochet off the shield");
            Assert.AreEqual(hp1, sh.Health, 0.01f, "front bullets must not damage the shield zombie");
            Assert.AreEqual(1, Zombie.CountKind(ZombieKind.Shield));
            sh.Despawn();
            yield return Wait(0.5f);

            // --- Bombalı: reaches the player, swells, bursts; a second bomb nearby chains.
            Teleport(p, home);
            float hpBefore = p.Health;
            heard.Clear();
            Vector3 bp = ClearSpot(home, 5f);
            Vector3 side = Vector3.Cross(Vector3.up, bp - home).normalized;
            spawner.TrySpawnSpecial(ZombieKind.Bomb, bp, w3);
            spawner.TrySpawnSpecial(ZombieKind.Bomb, bp + side * 1.3f, w3);
            yield return WaitUntil(() => heard.Contains(SfxId.BombExplode), 12f);
            Assert.IsTrue(heard.Contains(SfxId.BombFuse), "bomb should swell before bursting");
            Assert.IsTrue(heard.Contains(SfxId.BombExplode), "bomb should burst");
            yield return Wait(0.6f);
            int bursts = 0; foreach (var id in heard) if (id == SfxId.BombExplode) bursts++;
            Debug.Log($"SMOKE: bomb bursts={bursts} player hp {hpBefore:0}->{p.Health:0}");
            Assert.Less(p.Health, hpBefore, "blast hurts the player");
            Assert.AreEqual(2, bursts, "the blast should set off the second bomb");
            Assert.AreEqual(0, Zombie.CountKind(ZombieKind.Bomb));

            // Shooting a bomb from range sets it off without hurting the player.
            yield return Wait(0.5f); // invulnerability window
            float hpShot = p.Health;
            heard.Clear();
            var b3 = spawner.TrySpawnSpecial(ZombieKind.Bomb, ClearSpot(home, 9f), w3);
            yield return WaitUntil(() => b3.Targetable, 8f);
            b3.TakeHit(9999f, false, Vector3.forward, b3.AimPoint);
            yield return null;
            Assert.IsTrue(heard.Contains(SfxId.BombExplode), "a killed bomb bursts");
            Assert.GreaterOrEqual(p.Health, hpShot - 0.01f, "a far bomb burst must not reach the player");

            // --- Dilci: telegraph, lash, pull; damage snaps the tongue.
            Teleport(p, home);
            heard.Clear();
            var tz = (TongueZombie)spawner.TrySpawnSpecial(ZombieKind.Tongue, ClearSpot(home, 8.5f), w3);
            yield return WaitUntil(() => p.IsGrabbed, 15f);
            Assert.IsTrue(heard.Contains(SfxId.TongueWarn), "telegraph before the lash");
            Assert.IsTrue(p.IsGrabbed, "tongue should grab a standing player");
            Assert.AreEqual(tz, p.Grabber);
            float d0 = Vector3.Distance(p.transform.position, tz.transform.position);
            yield return Wait(0.8f);
            float d1 = Vector3.Distance(p.transform.position, tz.transform.position);
            Debug.Log($"SMOKE: tongue pull {d0:0.0} -> {d1:0.0} m");
            Assert.Less(d1, d0 - 0.5f, "player should be reeled in");
            tz.TakeHit(tz.breakDamage + 1f, false, (tz.transform.position - p.transform.position).normalized, tz.AimPoint);
            yield return null;
            if (tz.Damageable) Assert.IsFalse(p.IsGrabbed, "enough damage snaps the tongue");
            Assert.IsFalse(p.IsGrabbed);
            Assert.IsTrue(heard.Contains(SfxId.TongueSnap) || !tz.Damageable);

            // Off-screen arrows for specials.
            var far = spawner.TrySpawnSpecial(ZombieKind.Shield, home + new Vector3(24f, 0f, 0f), w3);
            yield return WaitUntil(() => far.Targetable, 8f);
            yield return null;
            var thr = Object.FindAnyObjectByType<ThreatIndicators>();
            Assert.NotNull(thr);
            Assert.GreaterOrEqual(thr.ShownSpecial, 1, "an off-screen special gets an edge arrow");
        }
        finally
        {
            GameAudio.OnSfx -= h;
            InputSystem.QueueStateEvent(mouse, new MouseState());
        }
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator BossButcher()
    {
        GameManager.TimeScale = 2f;
        yield return LoadGame();
        var gm = GameManager.I;
        WaveDirector.I.enabled = false; // no regular spawns
        var p = gm.player;
        var bd = BossDirector.I;
        Assert.NotNull(bd, "BossDirector in the scene");
        Assert.NotNull(PropTosser.I, "PropTosser in the scene");
        var spawner = Object.FindAnyObjectByType<ZombieSpawner>();
        var heard = new System.Collections.Generic.List<SfxId>();
        System.Action<SfxId, Vector3> h = (id, pos) => heard.Add(id);
        GameAudio.OnSfx += h;
        var phases = new System.Collections.Generic.HashSet<ButcherZombie.Phase>();
        try
        {
            Vector3 home = OpenArea(p);
            yield return null;
            var boss = bd.Spawn(6, ClearSpot(home, 13f)) as ButcherZombie;
            Assert.NotNull(boss, "Kasap spawned");
            boss.PhaseChanged += ph => phases.Add(ph);
            Assert.AreEqual(ZombieKind.Boss, boss.Kind);
            Assert.IsTrue(bd.HoldsWave, "boss wave holds while the boss lives");
            Assert.Greater(bd.SpawnIntervalMul, 1f, "ordinary spawning thinned out");
            Assert.IsTrue(gm.cameraRig.IsFocusing, "intro camera swing");
            Assert.IsTrue(MusicDirector.I.BossLayerOn, "boss music layer");
            Assert.Greater(boss.MaxHealth, 2000f);
            Assert.AreEqual(2, boss.eyes.Length, "two eye glows");
            yield return WaitUntil(() => boss.IsWalking && boss.CurrentPhase == ButcherZombie.Phase.Walk, 10f);
            yield return Wait(0.5f);
            var hud = Object.FindAnyObjectByType<BossHUD>();
            Assert.NotNull(hud);
            Assert.IsTrue(hud.BarVisible, "boss health bar shown");
            Assert.IsTrue(heard.Contains(SfxId.BossRoar), "spawn roar");

            // --- Natural charge at range: wind-up (strip) -> charge -> winded (2x damage).
            p.Heal(1000f);
            yield return WaitUntil(() => boss.CurrentPhase == ButcherZombie.Phase.ChargeWind, 8f);
            Assert.AreEqual(ButcherZombie.Phase.ChargeWind, boss.CurrentPhase, "charges a player at range");
            Assert.IsTrue(boss.chargeStrip.gameObject.activeSelf, "red strip telegraph");
            yield return WaitUntil(() => boss.IsWeak, 8f);
            Assert.IsTrue(phases.Contains(ButcherZombie.Phase.Charge), "charged");
            Assert.IsTrue(boss.IsWeak, "winded after the charge");
            yield return null;
            Assert.IsTrue(hud.WeakShown, "ZAYIF! tag");
            float hp0 = boss.Health;
            boss.TakeHit(50f, false, Vector3.forward, boss.AimPoint);
            Assert.AreEqual(100f, hp0 - boss.Health, 0.5f, "double damage while winded");
            yield return WaitUntil(() => boss.CurrentPhase == ButcherZombie.Phase.Walk, 10f);
            hp0 = boss.Health;
            boss.TakeHit(50f, false, Vector3.forward, boss.AimPoint);
            Assert.AreEqual(50f, hp0 - boss.Health, 0.5f, "normal damage otherwise");

            // --- Smashes low obstacles in its path and flings zombies.
            int tossed0 = PropTosser.I.TotalTossed;
            Collider prop = null;
            foreach (var c in Physics.OverlapSphere(boss.transform.position, 35f, GameLayers.VaultableMask))
                if (c.GetComponentInParent<EnvProp>() && !c.GetComponentInParent<ExplosiveBarrel>()) { prop = c; break; }
            if (prop)
            {
                Vector3 pp = prop.bounds.center; pp.y = 0f;
                Vector3 dir = Vector3.forward;
                for (int i = 0; i < 12; i++)
                {
                    dir = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
                    if (!Physics.CheckSphere(pp - dir * 7f + Vector3.up * 1.5f, 1.3f, GameLayers.BlockerMask) &&
                        !Physics.SphereCast(pp - dir * 7f + Vector3.up, 1f, dir, out _, 6f, GameLayers.BlockerMask)) break;
                }
                boss.transform.position = pp - dir * 7f;
                Physics.SyncTransforms();
                var z = spawner.TrySpawnSpecial(ZombieKind.Bomb, pp - dir * 3.5f, Difficulty.ForWave(1));
                boss.ForceCharge(pp + dir * 4f);
                yield return WaitUntil(() => boss.CurrentPhase != ButcherZombie.Phase.ChargeWind && boss.CurrentPhase != ButcherZombie.Phase.Charge, 8f);
                Assert.Greater(PropTosser.I.TotalTossed, tossed0, "charge flings the car / crate in its way");
                Debug.Log($"SMOKE: boss tossed {PropTosser.I.TotalTossed - tossed0} props, zombie hit={(z ? (!z.Damageable || z.Health < z.MaxHealth).ToString() : "-")}");
                yield return WaitUntil(() => boss.CurrentPhase == ButcherZombie.Phase.Walk, 10f);
            }
            else Debug.Log("SMOKE: no vaultable prop near the boss, toss check skipped");

            // --- Charging into a building stuns it.
            Collider wall = null;
            Vector3 start = Vector3.zero, wc = Vector3.zero;
            foreach (var c in Physics.OverlapSphere(boss.transform.position, 45f, GameLayers.BlockerMask))
            {
                var ep = c.GetComponentInParent<EnvProp>();
                if ((ep && ep.radius < boss.wallMinRadius) || c.bounds.size.x < 4f || c.bounds.size.z < 4f) continue;
                wc = c.bounds.center; wc.y = 0f;
                for (int i = 0; i < 16 && !wall; i++)
                {
                    Vector3 d = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                    Vector3 s0 = c.ClosestPoint(wc + d * 30f + Vector3.up); s0.y = 0f;
                    s0 += d * 8f;
                    if (Physics.CheckSphere(s0 + Vector3.up * 1.5f, 1.4f, GameLayers.ObstacleMask)) continue;
                    if (!Physics.SphereCast(s0 + Vector3.up, 1f, -d, out var hh, 9f, GameLayers.ObstacleMask) || hh.collider != c) continue;
                    start = s0;
                    wc = s0 - d * 12f; // charge straight back into the wall
                    wall = c;
                }
                if (wall) break;
            }
            Assert.NotNull(wall, "a building with a clear run-up near the boss");
            boss.transform.position = start;
            Physics.SyncTransforms();
            boss.ForceCharge(wc);
            yield return WaitUntil(() => boss.CurrentPhase == ButcherZombie.Phase.Stunned, 8f);
            Assert.AreEqual(ButcherZombie.Phase.Stunned, boss.CurrentPhase, "stunned by the wall");
            Assert.IsTrue(boss.IsWeak);
            yield return WaitUntil(() => boss.CurrentPhase == ButcherZombie.Phase.Walk, 12f);

            // --- Ground slam: hurts inside the ring, leaving the ring dodges it.
            Teleport(p, boss.transform.position + boss.transform.forward * 3f);
            p.Heal(1000f);
            float php = p.Health;
            boss.ForceSlam();
            Assert.AreEqual(ButcherZombie.Phase.SlamWind, boss.CurrentPhase);
            Assert.IsTrue(boss.slamRing.gameObject.activeSelf, "expanding ring telegraph");
            yield return WaitUntil(() => boss.CurrentPhase != ButcherZombie.Phase.SlamWind, 5f);
            Assert.Less(p.Health, php, "slam hits a player inside the ring");
            Assert.IsTrue(heard.Contains(SfxId.BossSlam));
            Assert.IsTrue(boss.ShockActive, "shock ring stays after the slam");
            Teleport(p, boss.ShockCenter + Vector3.right * 2f);
            yield return null;
            yield return null;
            Assert.Less(p.SlowFactor, 1f, "shock ring slows the player");
            yield return WaitUntil(() => boss.CurrentPhase == ButcherZombie.Phase.Walk, 5f);
            yield return Wait(0.8f); // invulnerability window
            Teleport(p, boss.transform.position + boss.transform.forward * 3f);
            p.Heal(1000f);
            php = p.Health;
            boss.ForceSlam();
            yield return Wait(0.2f);
            Vector3 away = p.transform.position - boss.transform.position; away.y = 0f;
            Teleport(p, boss.transform.position + away.normalized * (boss.slamRadius + 3f));
            yield return WaitUntil(() => boss.CurrentPhase != ButcherZombie.Phase.SlamWind, 5f);
            Assert.AreEqual(php, p.Health, 0.01f, "leaving the ring dodges the slam");

            // --- Far player: debris throw with a landing marker.
            yield return WaitUntil(() => boss.CurrentPhase == ButcherZombie.Phase.Walk && !boss.ShockActive, 8f);
            Teleport(p, ClearSpot(boss.transform.position, 15f));
            p.Heal(1000f);
            yield return Wait(0.6f);
            php = p.Health;
            int landed0 = BossProjectile.Landed;
            boss.ForceThrow();
            Assert.AreEqual(ButcherZombie.Phase.Throw, boss.CurrentPhase, "throws at a far player");
            yield return WaitUntil(() => boss.projectiles[0].Flying, 4f);
            Assert.IsTrue(boss.projectiles[0].marker.gameObject.activeSelf, "landing marker on the ground");
            yield return WaitUntil(() => BossProjectile.Landed > landed0, 6f);
            Assert.Less(p.Health, php, "debris hits a player who stays on the marker");

            // --- Rage under 50 %.
            Assert.IsFalse(boss.Enraged);
            boss.TakeHit(boss.Health - boss.MaxHealth * 0.45f, false, Vector3.forward, boss.AimPoint, true);
            yield return null;
            yield return null;
            Assert.IsTrue(boss.Enraged, "enraged below half health");
            yield return WaitUntil(() => boss.CurrentPhase == ButcherZombie.Phase.Walk, 10f);
            p.Heal(1000f);
            boss.ForceCharge(p.transform.position);
            yield return WaitUntil(() => boss.CurrentPhase != ButcherZombie.Phase.ChargeWind && boss.CurrentPhase != ButcherZombie.Phase.Charge || boss.ChargeInCombo == 2, 8f);
            if (boss.CurrentPhase == ButcherZombie.Phase.Stunned) Debug.Log("SMOKE: rage charge hit a wall, double-charge check skipped");
            else Assert.AreEqual(2, boss.ChargeInCombo, "enraged: a second charge follows");

            // --- Kill: reward, slow-mo, epic card pick.
            int score0 = gm.Score;
            boss.TakeHit(boss.Health + 10f, false, Vector3.forward, boss.AimPoint, true);
            Assert.AreEqual(1, bd.BossesDefeated);
            Assert.IsFalse(bd.HoldsWave, "wave released after the kill");
            Assert.Greater(gm.Score - score0, 2000, "big bonus");
            Assert.Less(Time.timeScale, GameManager.TimeScale * 0.5f, "slow motion on the kill");
            Assert.IsFalse(MusicDirector.I.BossLayerOn);
            yield return WaitUntil(() => CardPicker.I.IsOpen, 8f);
            Assert.IsTrue(CardPicker.I.IsOpen, "boss reward card pick");
            Assert.AreEqual(Rarity.Epic, Upgrades.Get(CardPicker.I.OfferAt(0)).rarity, "guaranteed epic");
            Assert.AreEqual(Loc.T("cards.boss"), CardPicker.I.header.text);
            Debug.Log($"SMOKE: boss phases {string.Join(",", phases)} sfx boss={heard.FindAll(x => x >= SfxId.BossRoar).Count}");
        }
        finally
        {
            GameAudio.OnSfx -= h;
        }
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator MenuSettingsPauseFlow()
    {
        var mem = new MemorySaveStore();
        SaveSystem.Store = mem;
        SaveSystem.Data.tutorialDone = true;
        GameManager.TimeScale = 2f;
        try
        {
            SceneManager.LoadScene(LoadingScreen.MenuScene);
            yield return null;
            yield return null;
            var mm = MainMenu.I;
            Assert.NotNull(mm, "main menu");
            Assert.NotNull(Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(), "event system for buttons");
            Assert.NotNull(ChunkManager.I, "live city behind the menu");
            Assert.AreEqual("0", mm.gold.text);

            // Language switches at once (static labels via LocText).
            mm.settingsPanel.Open();
            mm.settingsPanel.Change(x => x.lang = "en");
            yield return null;
            Assert.AreEqual(Lang.En, Loc.Current);
            Assert.AreEqual("PLAY", mm.play.GetComponentInChildren<Text>().text);
            mm.settingsPanel.Change(x => x.lang = "tr");
            mm.settingsPanel.Change(x => x.quality = 0);
            Assert.Less(Settings.ParticleScale, 1f);
            mm.settingsPanel.Change(x => x.quality = 2);
            mm.settingsPanel.Close();
            Assert.IsNotNull(mem.json, "settings saved on close");
            Assert.AreEqual("OYNA", mm.play.GetComponentInChildren<Text>().text);

            // Endless → loading screen → game.
            mm.PlayEndless();
            yield return WaitUntil(() => GameManager.I && !LoadingScreen.IsLoading, 20f);
            var gm = GameManager.I;
            Assert.NotNull(gm, "game loaded");
            var pm = Object.FindAnyObjectByType<PauseMenu>();
            Assert.NotNull(pm);
            yield return Wait(0.3f);
            Assert.IsTrue(pm.pauseButton.gameObject.activeInHierarchy, "pause button");
            pm.Pause();
            Assert.IsTrue(gm.Paused);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(pm.panel.activeSelf, "pause menu");
            Assert.IsTrue(TouchControls.I.Paused, "no gameplay input while paused");
            pm.Resume();
            Assert.AreEqual(GameManager.TimeScale, Time.timeScale, 1e-4f);
            Assert.IsFalse(pm.panel.activeSelf);

            pm.ToMenu();
            yield return WaitUntil(() => MainMenu.I && !LoadingScreen.IsLoading, 20f);
            Assert.NotNull(MainMenu.I, "back in the menu");
        }
        finally
        {
            SaveSystem.Store = null;
        }
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator ShopLoadoutAndRunRewards()
    {
        GameManager.TimeScale = 2f;
        SceneManager.LoadScene(LoadingScreen.MenuScene);
        yield return null;
        yield return null;
        var mm = MainMenu.I;
        Assert.NotNull(mm.shopUI, "shop in the menu");
        Profile.Grant(6000, 100, "test");
        var shop = mm.shopUI;

        // Weapons: buy the SMG, equip it, one damage upgrade.
        shop.Open(ShopUI.Tab.Weapons, (int)WeaponId.Smg);
        yield return null;
        Assert.IsTrue(shop.previewImage.enabled, "3D preview for weapons");
        shop.ActNow();
        Assert.IsTrue(Profile.Owns(WeaponId.Smg), "bought with gold");
        shop.ActNow();
        Assert.AreEqual(WeaponId.Smg, Profile.SelectedWeapon, "equipped");
        shop.upgradeButtons[0].onClick.Invoke();
        Assert.IsTrue(shop.confirm.activeSelf, "purchase confirmation");
        shop.confirmYes.onClick.Invoke();
        Assert.AreEqual(1, Profile.Level(WeaponId.Smg, WeaponStat.Damage));

        // Costume.
        shop.Open(ShopUI.Tab.Costumes, (int)CostumeId.Woodland);
        shop.ActNow();
        Assert.AreEqual(CostumeId.Woodland, Profile.SelectedCostume);

        // Gems: fake store purchase.
        int gems0 = Profile.Gems;
        shop.Open(ShopUI.Tab.Gems, 1);
        shop.ActNow();
        yield return WaitUntil(() => Profile.Gems > gems0, 5f);
        Assert.AreEqual(gems0 + 80, Profile.Gems, "fake IAP delivered");
        shop.Close();

        // Loadout → game: weapon + costume applied.
        mm.loadout.Open();
        Assert.IsTrue(mm.loadout.root.activeSelf);
        mm.PlayEndless();
        yield return WaitUntil(() => GameManager.I && !LoadingScreen.IsLoading, 20f);
        var gm = GameManager.I;
        var p = gm.player;
        var lo = p.GetComponent<WeaponLoadout>();
        Assert.AreEqual(WeaponId.Smg, lo.Current);
        Assert.AreEqual(Economy.Magazine(Economy.Weapon(WeaponId.Smg), 0), p.weapon.magazineSize);
        Assert.AreEqual(SfxId.ShootSmg, p.weapon.shootSfx);
        Assert.AreEqual(CostumeId.Woodland, lo.Costume);
        yield return Wait(0.5f);

        // Death → gold, continue with gems, second death → no second continue, 2x gold with a (fake) ad.
        gm.AddBonus(2000);   // an instant death on wave 0 earns nothing by design
        int gold0 = Profile.Gold;
        p.TakeDamage(9999f, Vector3.forward);
        yield return WaitUntil(() => RunRewards.I.root.activeSelf, 5f);
        Assert.Greater(Profile.Gold, gold0, "run gold paid");
        Assert.IsTrue(RunRewards.I.continueGemButton.gameObject.activeSelf);
        int gemsBefore = Profile.Gems;
        RunRewards.I.ContinueWithGems();
        Assert.IsFalse(gm.IsGameOver, "continued");
        Assert.IsFalse(p.IsDead);
        Assert.AreEqual(gemsBefore - Economy.ContinueGems, Profile.Gems);
        yield return Wait(1f);
        p.TakeDamage(9999f, Vector3.forward);
        yield return WaitUntil(() => RunRewards.I.root.activeSelf, 5f);
        Assert.IsFalse(RunRewards.I.continueGemButton.gameObject.activeSelf, "one continue per run");
        int g1 = Profile.Gold;
        int last = RunRewards.I.LastGold;
        RunRewards.I.DoubleGold();
        yield return WaitUntil(() => RunRewards.I.Doubled, 5f);
        Assert.AreEqual(g1 + last, Profile.Gold, "2x gold");
        bool anyRuns = false;
        foreach (var q in Profile.Quests) if (q.progress > 0) anyRuns = true;
        Assert.IsTrue(anyRuns || Profile.Quests.Count == 0, "quests got the run");
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator CampaignAndHardMode()
    {
        // ---- Chapter 1 (flare → supply → reach): objectives in order, victory, stars, reward, chapter 2 unlocked.
        GameMode.Set(RunMode.Campaign, 0);
        GameManager.TimeScale = 2f;
        yield return LoadGame();
        var gm = GameManager.I;
        var p = gm.player;
        var od = ObjectiveDirector.I;
        WaveDirector.I.enabled = false;
        od.holdScale = 0.12f;
        od.issueDelay = 0.3f;
        Assert.IsInstanceOf<CampaignPlan>(od.Plan, "campaign plan");
        Assert.IsFalse(p.weapon.limitedAmmo, "campaign: normal ammo");
        int gold0 = SaveSystem.Data.wallet.gold;
        var def = Campaign.Get(0);
        for (int i = 0; i < def.steps.Length; i++)
        {
            yield return WaitUntil(() => od.HasActive, 8f);
            Assert.IsTrue(od.HasActive, "step " + i + " issued");
            Assert.AreEqual(def.steps[i], od.Step.kind);
            Assert.AreEqual(i, od.ChapterIndex);
            if (od.Step.kind == ObjectiveKind.Supply) p.TakeDamage(60f, Vector3.forward); // crate heals instead of a card pick
            Teleport(p, od.TargetLocal);
            yield return WaitUntil(() => !od.HasActive, 15f);
            Assert.IsFalse(od.HasActive, def.steps[i] + " should complete");
        }
        yield return WaitUntil(() => gm.hud.gameOverPanel.activeSelf, 6f);
        Assert.IsTrue(gm.Won && gm.IsGameOver, "chapter won");
        Assert.AreEqual(3, Campaign.StarsOf(SaveSystem.Data.campaign, 0), "fast + full health = 3 stars");
        Assert.IsTrue(Campaign.Unlocked(SaveSystem.Data.campaign, 1), "next chapter unlocked");
        Assert.GreaterOrEqual(SaveSystem.Data.wallet.gold, gold0 + def.gold, "first-clear gold");
        Assert.IsTrue(gm.hud.victoryStars[2].gameObject.activeSelf, "stars on the victory screen");
        float hp = p.Health;
        p.TakeDamage(500f, Vector3.forward);
        Assert.AreEqual(hp, p.Health, "safe after the chapter");

        // ---- Siteless steps: survive runs wherever you are.
        GameMode.Set(RunMode.Campaign, 1);
        yield return LoadGame();
        gm = GameManager.I; p = gm.player; od = ObjectiveDirector.I;
        WaveDirector.I.enabled = false;
        od.holdScale = 0.1f;
        Assert.AreEqual(Campaign.Get(1).startWave - 1, WaveDirector.I.Wave, "chapter start wave");
        od.ForceIssue(ObjectiveKind.Survive);
        Assert.IsTrue(od.HasActive && !od.Step.HasSite);
        yield return WaitUntil(() => !od.HasActive, 15f);
        Assert.IsFalse(od.HasActive, "survive completes on the clock");

        // ---- Hard: harder waves, smaller health pool, limited ammo + ammo boxes, no breather regen.
        GameMode.Set(RunMode.Hard);
        yield return LoadGame();
        gm = GameManager.I; p = gm.player;
        var w = p.weapon;
        Assert.IsTrue(w.limitedAmmo, "hard: limited ammo");
        Assert.AreEqual(w.magazineSize * ModeRules.StartReserveMags, w.Reserve, "start reserve");
        Assert.Less(p.maxHealth, 100f, "hard: lower health pool");
        Assert.AreEqual(Difficulty.ForWave(1, true).contactDamage, WaveDirector.I.Params.contactDamage, 0.01f);
        WaveDirector.I.JumpToBreakBefore(2);
        WaveDirector.I.enabled = false;
        w.SetAmmo(0, 0);
        yield return null;
        Assert.IsTrue(w.OutOfAmmo, "empty");
        w.StartReload();
        Assert.IsFalse(w.IsReloading, "no reload without reserve");
        Pickups.I.DropAmmo(p.transform.position + Vector3.right * 2f);
        yield return WaitUntil(() => w.Ammo > 0, 6f);
        Assert.AreEqual(w.magazineSize, w.Ammo, "ammo box → one magazine");
        Assert.AreEqual(0, w.Reserve);
        p.TakeDamage(20f, Vector3.forward);
        float hurt = p.Health;
        yield return Wait(3.5f);
        Assert.AreEqual(hurt, p.Health, 0.01f, "hard: no breather regeneration");
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator CoopLocalHostAndPuppets()
    {
        // ---- Host (direct local transport, no Relay): room -> start -> Game through the network scene manager.
        GameManager.TimeScale = 1f;
        SceneManager.LoadScene(LoadingScreen.MenuScene);
        yield return null; yield return null;
        try
        {
            Assert.IsTrue(CoopSession.StartLocalHost(7788), "local host");
            Assert.IsTrue(CoopSession.InRoom && CoopSession.IsHost);
            yield return WaitUntil(() => NetPlayer.Local != null, 5f);
            Assert.NotNull(NetPlayer.Local, "host's NetPlayer spawned");
            CoopSession.StartGame();
            Assert.IsTrue(GameMode.IsCoop && CoopSession.Started);
            yield return WaitUntil(() => GameManager.I != null && SceneManager.GetActiveScene().name == LoadingScreen.GameScene, 15f);
            yield return null; yield return null;
            var gm = GameManager.I;
            Assert.NotNull(gm, "Game scene loaded through Netcode");
            Assert.NotNull(NetPlayer.Local, "NetPlayer survives the scene load");
            Assert.AreEqual(1, Squad.Count, "only the local soldier in the squad");
            var nz = NetZombies.I;
            Assert.NotNull(nz); Assert.IsTrue(nz.IsHost && !nz.IsClient);
            yield return WaitUntil(() => Zombie.Active.Count > 0, 12f);
            Assert.Greater(Zombie.Active.Count, 0);
            foreach (var z in Zombie.Active) Assert.Greater(z.NetId, 0, "host zombies get network ids");
            Assert.AreEqual(0f, WaveDirector.I.Params.specialChance, "C1: no mutations in co-op");
            yield return Wait(0.5f);
            Assert.AreEqual(gm.player.transform.position.x, NetPlayer.Local.State.Value.pos.x, 0.5f, "owner writes its state");
            // A real snapshot of the host's zombies: the "network" input of the client below.
            int len = nz.BuildSnapshot();
            var snap = new byte[len];
            System.Array.Copy(nz.Buffer, snap, len);
            int hostZombies = Zombie.Active.Count;
            ushort firstId = Zombie.Active[0].NetId;
            Vector3 firstPos = Zombie.Active[0].transform.position;
            gm.player.Warp(new Vector3(150f, 0f, 0f));
            yield return null; yield return null;
            Assert.AreEqual(Vector3.zero, FloatingOrigin.Offset, "no origin shift in co-op");
            var leave = CoopSession.Leave();
            while (!leave.IsCompleted) yield return null;
            yield return null;
            Assert.IsFalse(CoopSession.InRoom); Assert.IsFalse(GameMode.IsCoop, "back to single player");

            // ---- Client side in-process: same snapshot -> puppets appear, move, die / vanish.
            GameMode.Set(RunMode.Coop);
            yield return LoadGame();
            nz = NetZombies.I;
            nz.BecomeClient();
            Assert.AreEqual(0, Zombie.Active.Count, "client: no zombies of its own");
            nz.ApplySnapshot(snap, len);
            Assert.AreEqual(hostZombies, nz.Puppets, "one puppet per host zombie");
            Zombie pz = null;
            foreach (var z in Zombie.Active) if (z.NetId == firstId) pz = z;
            Assert.NotNull(pz); Assert.IsTrue(pz.Puppet);
            Assert.AreEqual(firstPos.x, pz.transform.position.x, 0.01f);
            float hp = pz.Health;
            pz.TakeHit(1000f, false, Vector3.forward, pz.transform.position);
            Assert.AreEqual(hp, pz.Health, "client bullets don't kill: the host owns health");
            Assert.IsTrue(pz.Damageable);
            nz.ApplyKill(firstId, Vector3.forward);
            Assert.IsFalse(pz.Damageable, "host kill -> puppet dies");
            var empty = new byte[ZombieSnapshot.Size(0)];
            int elen = ZombieSnapshot.Write(empty, new WaveNetState { wave = 3 }, new ZombieNetState[0], 0);
            nz.ApplySnapshot(empty, elen);
            Assert.AreEqual(0, nz.Puppets, "missing from the snapshot -> gone");
            Assert.AreEqual(3, WaveDirector.I.Wave, "wave mirrored from the host");
        }
        finally
        {
            if (CoopSession.InRoom) _ = CoopSession.Leave();
            if (Unity.Netcode.NetworkManager.Singleton) Object.Destroy(Unity.Netcode.NetworkManager.Singleton.gameObject);
            GameMode.Set(RunMode.Endless);
        }
    }

    [UnityTest, Timeout(600000)]
    public IEnumerator CityAndObjectives()
    {
        GameManager.TimeScale = 2f;
        yield return LoadGame();
        var gm = GameManager.I;
        var p = gm.player;
        WaveDirector.I.enabled = false; // no zombies: objectives are driven directly
        var od = ObjectiveDirector.I;
        Assert.NotNull(od, "ObjectiveDirector missing");
        od.holdScale = 0.12f;
        od.issueDelay = 0.3f;
        var cm = ChunkManager.I;

        // City: atlas ground meshes, fog on, chunk types vary in the neighbourhood.
        Assert.IsTrue(RenderSettings.fog, "district fog");
        var types = new System.Collections.Generic.HashSet<ChunkType>();
        for (int x = -8; x <= 8; x++) for (int z = -8; z <= 8; z++) types.Add(cm.TypeAt(new Vector2Int(x, z)));
        Assert.GreaterOrEqual(types.Count, 4, "several chunk types around the spawn");
        foreach (var mf in cm.GetComponentsInChildren<MeshFilter>())
            if (mf.name == "Ground") Assert.Greater(mf.sharedMesh.vertexCount, 0, "chunk ground mesh");

        var hud = Object.FindAnyObjectByType<ObjectiveHUD>();
        Assert.NotNull(hud, "ObjectiveHUD missing");
        int radio = 0;
        System.Action<RadioMsg> onRadio = m => radio++;
        od.Radio += onRadio;
        try
        {
            foreach (var kind in new[] { ObjectiveKind.Flare, ObjectiveKind.Tower, ObjectiveKind.Supply })
            {
                int before = radio, score = gm.Score, xp = Progression.I.Xp + Progression.I.Level * 100000;
                od.ForceIssue(kind);
                Assert.IsTrue(od.HasActive);
                Assert.AreEqual(kind, od.Step.kind);
                Assert.Greater(radio, before, "radio briefing");
                Vector3 to = od.TargetLocal - p.transform.position; to.y = 0f;
                Debug.Log($"SMOKE: objective {kind} at {od.SiteChunk} ({cm.TypeAt(od.SiteChunk)}) {to.magnitude:0} m");
                Assert.AreEqual(od.Step.site, cm.TypeAt(od.SiteChunk), "site chunk has the right type");
                Assert.Greater(to.magnitude, 30f, "target is away from the player");
                yield return null; yield return null;
                Assert.IsTrue(hud.MarkerVisible && hud.MarkerOnEdge, "edge arrow toward an off-screen target");
                Assert.IsFalse(string.IsNullOrEmpty(hud.CurrentRadio), "radio text on screen");

                if (kind == ObjectiveKind.Supply) p.TakeDamage(60f, Vector3.forward);
                float hpBefore = p.Health;
                Vector3 spot = od.TargetLocal + (kind == ObjectiveKind.Tower ? new Vector3(2.6f, 0f, 0f) : Vector3.zero);
                Teleport(p, spot);
                yield return WaitUntil(() => !od.HasActive, 15f);
                Assert.IsFalse(od.HasActive, kind + " should complete");
                Assert.Greater(gm.Score, score, "bonus score");
                Assert.Greater(Progression.I.Xp + Progression.I.Level * 100000, xp, "bonus XP");
                if (kind == ObjectiveKind.Supply) Assert.Greater(p.Health, hpBefore, "supply crate heals when hurt");
            }

            // After three objectives the landing zone appears by itself.
            yield return WaitUntil(() => od.HasActive, 5f);
            Assert.IsTrue(od.HasActive && od.Step.kind == ObjectiveKind.Helicopter, "helicopter landing zone after 3 objectives");
            Assert.AreEqual(ChunkType.Helipad, cm.TypeAt(od.SiteChunk));
            int scoreH = gm.Score;
            Teleport(p, od.TargetLocal + new Vector3(1.5f, 0f, 1.5f));
            var vis = Object.FindAnyObjectByType<ObjectiveVisuals>();
            bool heliSeen = false;
            float t0 = Time.realtimeSinceStartup;
            while (od.HasActive && Time.realtimeSinceStartup - t0 < 15f)
            {
                heliSeen |= vis.helicopter && vis.helicopter.Visible;
                yield return null;
            }
            Assert.IsFalse(od.HasActive, "landing zone hold completes");
            Assert.IsTrue(heliSeen, "the helicopter flew in");
            Assert.AreEqual(1, od.Helicopters);
            Assert.Greater(gm.Score - scoreH, 999, "big helicopter reward");
            yield return WaitUntil(() => hud.CurrentRadio == Loc.T("radio.heli_done"), 25f);
            Assert.AreEqual(Loc.T("radio.heli_done"), hud.CurrentRadio, "story line on the radio");
        }
        finally { od.Radio -= onRadio; }
    }

    /// <summary>[Explicit] PNGs of every chunk type, the objective HUD and the helicopter (needs graphics). CAPTURE_DIR.</summary>
    [UnityTest, Explicit, Timeout(600000)]
    public IEnumerator CaptureCity()
    {
        string dir = System.Environment.GetEnvironmentVariable("CAPTURE_DIR") ?? Application.temporaryCachePath;
        ChunkManager.SeedOverride = 4242;
        yield return LoadGame();
        ChunkManager.SeedOverride = 0;
        var gm = GameManager.I;
        var p = gm.player;
        WaveDirector.I.enabled = false;
        var cm = ChunkManager.I;
        var od = ObjectiveDirector.I;
        od.holdScale = 0.5f;
        yield return Wait(0.5f);
        PlaytestBot.Shot(Path.Combine(dir, "city_00_spawn.png"));
        int n = 1;
        foreach (ChunkType t in System.Enum.GetValues(typeof(ChunkType)))
        {
            if (CityLayout.IsObjectiveSite(t)) continue;
            if (!cm.FindType(t, ChunkManager.ChunkOf(p.transform.position), 1, 14, out var c)) { Debug.Log("CAPTURE: no " + t); continue; }
            Vector3 g = CityLayout.ChunkOrigin(c) + new Vector3(10f, 0f, 3f);
            Teleport(p, FloatingOrigin.ToLocal(g));
            yield return Wait(0.6f);
            Debug.Log($"CAPTURE: {t} at {c} district={cm.DistrictAt(c)}");
            PlaytestBot.Shot(Path.Combine(dir, $"city_{n++:00}_{t}_{cm.DistrictAt(c)}.png"));
        }
        // Objective HUD: radio + edge arrow, then the tower zone, then the helicopter landing.
        od.ForceIssue(ObjectiveKind.Flare);
        yield return Wait(1.6f);
        PlaytestBot.Shot(Path.Combine(dir, "city_20_radio_arrow.png"));
        Teleport(p, od.TargetLocal + new Vector3(0f, 0f, -5f));
        yield return Wait(0.8f);
        PlaytestBot.Shot(Path.Combine(dir, "city_21_flare_site.png"));
        Teleport(p, od.TargetLocal);
        yield return Wait(0.5f);
        od.ForceIssue(ObjectiveKind.Tower);
        yield return null;
        Teleport(p, od.TargetLocal + new Vector3(2.8f, 0f, -1f));
        yield return Wait(4f);
        PlaytestBot.Shot(Path.Combine(dir, "city_22_tower_hold.png"));
        yield return WaitUntil(() => !od.HasActive, 20f);
        od.ForceIssue(ObjectiveKind.Helicopter);
        yield return null;
        Teleport(p, od.TargetLocal + new Vector3(3f, 0f, -3f));
        yield return WaitUntil(() => od.HeliApproach01 > 0.6f, 30f);
        PlaytestBot.Shot(Path.Combine(dir, "city_23_heli_arrive.png"));
        yield return WaitUntil(() => !od.HasActive, 30f);
        yield return Wait(1.2f);
        PlaytestBot.Shot(Path.Combine(dir, "city_24_heli_leaves.png"));
    }

    [UnityTest, Timeout(300000)]
    public IEnumerator CardsBarrelsReadability()
    {
        yield return LoadGame();
        var gm = GameManager.I;
        WaveDirector.I.enabled = false;
        var p = gm.player;
        var w = p.weapon;
        var heard = new System.Collections.Generic.List<SfxId>();
        System.Action<SfxId, Vector3> h = (id, pos) => heard.Add(id);
        GameAudio.OnSfx += h;
        try
        {
            // --- Readability setup.
            Assert.AreEqual(17.5f, gm.cameraRig.distance, 0.01f, "camera pulled back (F6.5)");
            Assert.NotNull(p.transform.Find("Ring"), "bright ring under the soldier");
            Assert.Greater(Object.FindAnyObjectByType<ThreatIndicators>().normalRange, 0f, "nearby off-screen zombies get arrows");
            var panel = gm.hud.gameOverPanel.GetComponent<UnityEngine.UI.Image>();
            Assert.GreaterOrEqual(panel.color.a, 0.75f, "death screen darkening");

            // --- XP → level-up → slowed card pick by tapping a card.
            var prog = Progression.I;
            Assert.AreEqual(1, prog.Level);
            prog.AddXp(prog.XpToNext);
            Assert.AreEqual(2, prog.Level);
            yield return null;
            yield return null;
            var picker = CardPicker.I;
            Assert.IsTrue(picker.IsOpen, "card picker opens on level-up");
            Assert.AreEqual(3, picker.OfferCount);
            Assert.Less(Time.timeScale, 0.2f * GameManager.TimeScale, "game slows during the pick");
            Assert.IsTrue(TouchControls.I.Blocked, "gameplay input blocked");
            Assert.IsTrue(heard.Contains(SfxId.LevelUp));
            var offered = picker.OfferAt(1);
            yield return Wait(0.5f);
            yield return Click(picker.CardScreenPoint(1));
            Assert.IsFalse(picker.IsOpen, "tapping a card closes the picker");
            Assert.AreEqual(1, Upgrades.I.Level(offered), "the tapped card is applied");
            Assert.AreEqual(GameManager.TimeScale, Time.timeScale, 0.001f, "time scale restored");
            Assert.IsFalse(TouchControls.I.Blocked);

            // --- Card effects.
            int mag = w.magazineSize;
            Upgrades.I.Take(CardId.BigMag);
            Assert.AreEqual(mag + 10, w.magazineSize);
            float rate = w.fireRate;
            Upgrades.I.Take(CardId.FireRate);
            Assert.Greater(w.fireRate, rate);
            float maxHp = p.maxHealth;
            Upgrades.I.Take(CardId.MaxHealth);
            Assert.AreEqual(maxHp + 25f, p.maxHealth, 0.01f);
            Upgrades.I.Take(CardId.Pierce);
            Assert.AreEqual(Upgrades.I.Level(CardId.Pierce), w.pierce);
            Upgrades.I.Take(CardId.Turret);
            var turret = Object.FindAnyObjectByType<Turret>();
            Assert.NotNull(turret, "turret deployed");
            Assert.IsTrue(turret.gameObject.activeSelf);
            Assert.Greater(turret.LifeLeft, 25f);
            Assert.NotNull(turret.status, "turret has a status display (health + timer)");
            Assert.AreEqual(1, Turret.Active.Count, "one turret at a time below level 3");
            // A zombie next to the turret (and far from the player) attacks the turret.
            {
                var sp = Object.FindAnyObjectByType<ZombieSpawner>();
                Vector3 tp = turret.transform.position;
                Vector3 away = tp - p.transform.position; away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
                Teleport(p, ClearSpot(tp, 6f));
                var zt = sp.TrySpawnSpecial(ZombieKind.Shield, tp + (tp - p.transform.position).normalized * 2f, Difficulty.ForWave(1));
                float th0 = turret.Health;
                yield return WaitUntil(() => turret.Health < th0 || !turret.IsDeployed, 15f);
                Assert.Less(turret.Health, th0, "zombies target the turret too");
                zt.Despawn();
                heard.Clear();
                turret.TakeDamage(9999f);
                Assert.IsFalse(turret.IsDeployed, "a destroyed turret folds away");
                Assert.IsTrue(heard.Contains(SfxId.Explosion), "with a small explosion");
                Assert.AreEqual(0, Turret.Active.Count);
            }
            for (int i = 0; i < 4; i++) Upgrades.I.Take(CardId.FireRate);
            Assert.AreEqual(3, Upgrades.I.Level(CardId.FireRate), "cards cap at level 3");
            Assert.GreaterOrEqual(Upgrades.I.Owned.Count, 5);

            // --- Explosive barrel: shot → blast hurts zombies (through shields too).
            // A barrel with open ground around it (the denser F8 city has barrels in alleys / gas stations too).
            ExplosiveBarrel barrel = null;
            Vector3 stand = default;
            for (int tries = 0; tries < 8 && barrel == null; tries++)
            {
                foreach (var b in ExplosiveBarrel.Active)
                    if (!b.Exploded && b.gameObject.activeInHierarchy && TryClearSpot(b.transform.position, 9f, out stand)) { barrel = b; break; }
                if (barrel == null) { Teleport(p, p.transform.position + new Vector3(60f, 0f, 0f)); yield return null; yield return null; }
            }
            Assert.NotNull(barrel, "the city should contain explosive barrels");
            Vector3 bpos = barrel.transform.position;
            Teleport(p, stand);
            yield return null;
            var spawner = Object.FindAnyObjectByType<ZombieSpawner>();
            var sh = spawner.TrySpawnSpecial(ZombieKind.Shield, bpos + (p.transform.position - bpos).normalized * 1.6f, Difficulty.ForWave(3));
            yield return WaitUntil(() => sh.Targetable, 8f);
            float hp0 = sh.Health;
            float php = p.Health;
            heard.Clear();
            barrel.Hit(10f);
            Assert.IsFalse(barrel.Exploded, "one weak hit is not enough");
            barrel.Hit(999f);
            Assert.IsTrue(barrel.Exploded);
            Assert.IsTrue(heard.Contains(SfxId.BarrelExplode));
            Assert.Less(sh.Health, hp0, "barrel blast damages zombies");
            Assert.GreaterOrEqual(p.Health, php - 0.01f, "player outside the radius is safe");
            Assert.IsFalse(Physics.CheckSphere(bpos + Vector3.up * 0.5f, 0.2f, GameLayers.BlockerMask, QueryTriggerInteraction.Ignore), "exploded barrel no longer blocks");
            Debug.Log($"SMOKE: barrel blast shield hp {hp0:0}->{sh.Health:0}, cards={Upgrades.I.Picks}");
        }
        finally { GameAudio.OnSfx -= h; }
    }

    /// <summary>F6 visuals with the HUD: card picker, building cut-out, barrels, threat arrows. Explicit; CAPTURE_DIR.</summary>
    [UnityTest, Explicit]
    public IEnumerator CaptureF6()
    {
        string dir = System.Environment.GetEnvironmentVariable("CAPTURE_DIR");
        if (string.IsNullOrEmpty(dir)) dir = Application.temporaryCachePath;
        yield return LoadGame();
        var gm = GameManager.I;
        var p = gm.player;
        foreach (var c in new[] { CardId.DoubleShot, CardId.Pierce, CardId.Turret, CardId.MaxHealth, CardId.Lifesteal }) Upgrades.I.Take(c);
        Upgrades.I.Take(CardId.DoubleShot);
        yield return Wait(6f);
        Progression.I.AddXp(Progression.I.XpToNext);
        yield return Wait(0.6f);
        PlaytestBot.Shot(Path.Combine(dir, "f6_cards.png"));
        yield return Click(CardPicker.I.CardScreenPoint(0));

        // Stand right behind (south of) a building so it hides the soldier → circular cut.
        Fadeable building = null;
        foreach (var f in Object.FindObjectsByType<Fadeable>(FindObjectsSortMode.None))
            if (f.gameObject.activeInHierarchy && f.GetComponent<EnvProp>() && f.GetComponent<EnvProp>().radius > 3f) { building = f; break; }
        if (building)
        {
            var b = building.GetComponent<Collider>().bounds;
            Teleport(p, new Vector3(b.center.x, 0f, b.max.z + 0.8f));
            yield return Wait(1.2f);
            PlaytestBot.Shot(Path.Combine(dir, "f6_cutout.png"));
        }
        ExplosiveBarrel barrel = null;
        foreach (var x in ExplosiveBarrel.Active) if (!x.Exploded) { barrel = x; break; }
        if (barrel)
        {
            Teleport(p, ClearSpot(barrel.transform.position, 6f));
            yield return Wait(1f);
            PlaytestBot.Shot(Path.Combine(dir, "f6_barrel.png"));
            barrel.Hit(999f);
            yield return Wait(0.12f);
            PlaytestBot.Shot(Path.Combine(dir, "f6_barrel_boom.png"));
        }
        var firePos = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
        SetMouse(firePos, true);
        yield return Wait(10f);
        PlaytestBot.Shot(Path.Combine(dir, "f6_play.png"));
        SetMouse(firePos, false);
        Debug.Log("CAPTURE: f6 " + dir);
    }

    /// <summary>Close-up PNGs of the three special zombies (scale, rings, telegraph). Explicit; CAPTURE_DIR.</summary>
    [UnityTest, Explicit]
    public IEnumerator CaptureSpecials()
    {
        string dir = System.Environment.GetEnvironmentVariable("CAPTURE_DIR");
        if (string.IsNullOrEmpty(dir)) dir = Application.temporaryCachePath;
        yield return LoadGame();
        var gm = GameManager.I;
        WaveDirector.I.enabled = false;
        var spawner = Object.FindAnyObjectByType<ZombieSpawner>();
        Vector3 home = OpenArea(gm.player);
        yield return null;
        var w = Difficulty.ForWave(3);
        var s = spawner.TrySpawnSpecial(ZombieKind.Shield, home + new Vector3(-3.2f, 0f, 4f), w);
        var b = spawner.TrySpawnSpecial(ZombieKind.Bomb, home + new Vector3(0f, 0f, 5f), w);
        var t = spawner.TrySpawnSpecial(ZombieKind.Tongue, home + new Vector3(3.2f, 0f, 8f), w);
        yield return Wait(3.2f);
        Shot(Path.Combine(dir, "special_game.png"));
        var c = home + new Vector3(0f, 1.6f, 0.3f);
        ShotFrom(Path.Combine(dir, "special_close.png"), c, home + new Vector3(0f, 1.1f, 5f));
        yield return WaitUntil(() => gm.player.IsGrabbed || (t as TongueZombie).CurrentPhase == TongueZombie.Phase.Aim, 10f);
        Shot(Path.Combine(dir, "special_tongue.png"));
        yield return Wait(0.5f);
        Shot(Path.Combine(dir, "special_tongue2.png"));
        Debug.Log("CAPTURE: specials " + dir);
    }

    /// <summary>[Explicit] Kasap: close-up (feet/eyes), charge telegraph in game view, winded tint. CAPTURE_DIR.</summary>
    [UnityTest, Explicit]
    public IEnumerator CaptureBoss()
    {
        string dir = System.Environment.GetEnvironmentVariable("CAPTURE_DIR");
        if (string.IsNullOrEmpty(dir)) dir = Application.temporaryCachePath;
        yield return LoadGame();
        var gm = GameManager.I;
        WaveDirector.I.enabled = false;
        Vector3 home = OpenArea(gm.player);
        yield return null;
        var boss = BossDirector.I.Spawn(6, home + new Vector3(0f, 0f, 9f)) as ButcherZombie;
        yield return WaitUntil(() => boss.IsWalking && boss.CurrentPhase == ButcherZombie.Phase.Walk, 10f);
        yield return null;
        Vector3 bp = boss.transform.position;
        ShotFrom(Path.Combine(dir, "boss_close.png"), bp + new Vector3(2.5f, 2.6f, -7.5f), bp + Vector3.up * 2.3f);
        gm.player.Heal(1000f);
        boss.ForceCharge(gm.player.transform.position);
        yield return Wait(0.6f);
        string St() => $"phase={boss.CurrentPhase} t={boss.PhaseTime:0.00} strip={boss.chargeStrip.gameObject.activeSelf} stripPos={boss.chargeStrip.position} scale={boss.chargeStrip.lossyScale} boss={boss.transform.position} player={gm.player.transform.position} ts={Time.timeScale:0.00} game={gm.GameTime:0.0}";
        Debug.Log("CAPTURE: charge " + St());
        Shot(Path.Combine(dir, "boss_charge.png"));
        yield return WaitUntil(() => boss.IsWeak, 6f);
        yield return Wait(0.3f);
        Debug.Log("CAPTURE: weak " + St());
        Shot(Path.Combine(dir, "boss_weak.png"));
        Debug.Log("CAPTURE: boss " + dir);
    }

    /// <summary>Renders gameplay frames to PNG. Run with -testFilter GameSmokeTest.CaptureScreens and CAPTURE_DIR set.</summary>
    [UnityTest, Explicit]
    public IEnumerator CaptureScreens()
    {
        string dir = System.Environment.GetEnvironmentVariable("CAPTURE_DIR");
        if (string.IsNullOrEmpty(dir)) dir = Application.temporaryCachePath;
        yield return LoadGame();
        var gm = GameManager.I;
        var firePos = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
        SetMouse(firePos, true);
        yield return Wait(12f);
        Shot(Path.Combine(dir, "play_1.png"));
        var pt = gm.player.transform;
        if (System.Environment.GetEnvironmentVariable("CAPTURE_DEBUG") == "1")
        {
            var rig = gm.player.rig;
            foreach (var r in rig.weaponRoot.GetComponentsInChildren<MeshRenderer>()) foreach (var m in r.materials) m.SetColor("_BaseColor", Color.red);
            var hand = rig.Animator.GetBoneTransform(HumanBodyBones.RightHand);
            Debug.Log($"CAPTURE: rifle pos={rig.weaponRoot.position - pt.position} fwd={rig.weaponRoot.forward} player fwd={pt.forward} hand={hand.position - pt.position} muzzle={gm.player.weapon.muzzle.position - pt.position}");
        }
        ShotFrom(Path.Combine(dir, "close_fire.png"), pt.position + pt.right * 1.6f + pt.forward * 2.2f + Vector3.up * 1.7f, pt.position + Vector3.up * 1.1f);
        ShotFrom(Path.Combine(dir, "close_side.png"), pt.position + pt.right * 2.6f + Vector3.up * 1.4f, pt.position + Vector3.up * 1.1f);
        SetMouse(firePos, false);
        gm.player.weapon.StartReload();
        yield return Wait(0.6f);
        ShotFrom(Path.Combine(dir, "close_reload.png"), pt.position + pt.right * 1.6f + pt.forward * 2.2f + Vector3.up * 1.7f, pt.position + Vector3.up * 1.1f);
        SetMouse(firePos, true);
        yield return Wait(2f);
        var z = Zombie.FindNearest(pt.position, 40f);
        if (z) ShotFrom(Path.Combine(dir, "close_zombie.png"), z.transform.position + z.transform.forward * 2.8f + Vector3.up * 1.6f, z.transform.position + Vector3.up * 1f);
        // Different district: jump far away.
        SetMouse(firePos, false);
        for (int k = 0; k < 4; k++)
        {
            Teleport(gm.player, gm.player.transform.position + new Vector3(0f, 0f, 60f));
            yield return null;
            yield return null;
        }
        yield return Wait(7f);
        Shot(Path.Combine(dir, "play_2.png"));
        SetMouse(firePos, true);
        yield return Wait(0.5f);
        Shot(Path.Combine(dir, "play_3.png"));
        Debug.Log($"CAPTURE: {dir} district={ChunkManager.I.DistrictAtPosition(gm.player.transform.position)}");
    }

    static void ShotFrom(string path, Vector3 from, Vector3 lookAt)
    {
        var cam = Camera.main;
        var p = cam.transform.position; var r = cam.transform.rotation;
        cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(lookAt - from));
        Shot(path);
        cam.transform.SetPositionAndRotation(p, r);
    }

    /// <summary>[Explicit] Menu, settings panel and pause screen with their UI (CAPTURE_DIR).</summary>
    [UnityTest, Explicit]
    public IEnumerator CaptureMenu()
    {
        string dir = System.Environment.GetEnvironmentVariable("CAPTURE_DIR");
        if (string.IsNullOrEmpty(dir)) dir = Application.temporaryCachePath;
        SaveSystem.Store = new MemorySaveStore();
        SaveSystem.Data.tutorialDone = true;
        GameManager.TimeScale = 1f;
        SceneManager.LoadScene(LoadingScreen.MenuScene);
        yield return null;
        var mm = MainMenu.I;
        yield return WaitUntil(() => !mm.SplashActive, 6f);
        yield return Wait(1.5f);
        ShotCanvas(Path.Combine(dir, "menu_main.png"), mm.GetComponent<Canvas>());
        mm.settingsPanel.Open();
        yield return null;
        ShotCanvas(Path.Combine(dir, "menu_settings.png"), mm.GetComponent<Canvas>());
        mm.settingsPanel.Close();
        if (mm.login) mm.login.root.SetActive(false); // the daily reward pops up on its own
        mm.ShowPlay(true);
        yield return null;
        ShotCanvas(Path.Combine(dir, "menu_modes.png"), mm.GetComponent<Canvas>());
        mm.campaignPanel.Open();
        yield return null;
        ShotCanvas(Path.Combine(dir, "menu_campaign.png"), mm.GetComponent<Canvas>());
        mm.campaignPanel.root.SetActive(false);
        mm.coopPanel.Open();
        yield return null;
        ShotCanvas(Path.Combine(dir, "menu_coop.png"), mm.GetComponent<Canvas>());
        mm.coopPanel.root.SetActive(false);
        mm.PlayEndless();
        yield return WaitUntil(() => GameManager.I && !LoadingScreen.IsLoading, 20f);
        yield return Wait(2f);
        Object.FindAnyObjectByType<PauseMenu>().Pause();
        yield return null;
        ShotCanvas(Path.Combine(dir, "game_pause.png"), Object.FindAnyObjectByType<HUD>().GetComponent<Canvas>());
        GameManager.I.SetPaused(false);
        SaveSystem.Store = null;
        Debug.Log("CAPTURE: menu " + dir);
    }

    static void ShotCanvas(string path, Canvas canvas)
    {
        var cam = Camera.main;
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
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.Destroy(rt);
        Object.Destroy(tex);
    }

    static void Shot(string path)
    {
        var cam = Camera.main;
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.Destroy(rt);
        Object.Destroy(tex);
    }
}
