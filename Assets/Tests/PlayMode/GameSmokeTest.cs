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
    }

    [TearDown]
    public void TearDown()
    {
        if (mouse != null) InputSystem.RemoveDevice(mouse);
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

    [UnityTest]
    public IEnumerator SpawnShootDieRestart()
    {
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
        while (!gm.IsGameOver && Time.realtimeSinceStartup - t0 < 120f) yield return null;
        Debug.Log($"SMOKE: gameover={gm.IsGameOver} time={gm.GameTime:0.0} score={gm.Score}");
        Assert.IsTrue(gm.IsGameOver, "player should eventually die");

        yield return Wait(2.5f);
        float cutoff = MusicDirector.I.calmFilter.cutoffFrequency;
        Debug.Log($"SMOKE: music cutoff after death={cutoff:0}");
        Assert.Less(cutoff, 3000f, "music should be low-passed on death");
        SetMouse(firePos, true);
        yield return null;
        yield return null;
        SetMouse(firePos, false);
        yield return null;
        yield return null;
        Assert.AreNotSame(gm, GameManager.I, "tap should restart the scene");
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
        Assert.AreEqual(30, w.Ammo);
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
        Assert.AreEqual(30, w.Ammo);

        // Tactical reload: fire ~20 rounds, release, wait > 1 s.
        SetMouse(firePos, true);
        while (w.Ammo > 12) yield return null;
        SetMouse(firePos, false);
        Assert.IsFalse(w.IsReloading);
        yield return Wait(1.3f);
        Assert.IsTrue(w.IsReloading || w.Ammo == 30, "tactical reload after 1 s idle with < half magazine");
        Debug.Log("SMOKE: tactical reload ok");
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
