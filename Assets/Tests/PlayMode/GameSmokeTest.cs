using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
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
        PlayerPrefs.SetInt("tutorial_done", 1);
        GameManager.TimeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        GameManager.TimeScale = 1f;
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
            Assert.AreEqual(14f, gm.cameraRig.distance, 0.01f, "camera a bit closer");
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
            for (int i = 0; i < 4; i++) Upgrades.I.Take(CardId.FireRate);
            Assert.AreEqual(3, Upgrades.I.Level(CardId.FireRate), "cards cap at level 3");
            Assert.GreaterOrEqual(Upgrades.I.Owned.Count, 5);

            // --- Explosive barrel: shot → blast hurts zombies (through shields too).
            ExplosiveBarrel barrel = null;
            for (int tries = 0; tries < 6 && barrel == null; tries++)
            {
                foreach (var b in ExplosiveBarrel.Active)
                    if (!b.Exploded && b.gameObject.activeInHierarchy) { barrel = b; break; }
                if (barrel == null) { Teleport(p, p.transform.position + new Vector3(60f, 0f, 0f)); yield return null; yield return null; }
            }
            Assert.NotNull(barrel, "the city should contain explosive barrels");
            Vector3 bpos = barrel.transform.position;
            Teleport(p, ClearSpot(bpos, 9f));
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
