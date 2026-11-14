using System.Collections;
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

    [UnityTest]
    public IEnumerator SpawnShootDieRestart()
    {
        SceneManager.LoadScene("Game");
        yield return null;
        yield return null;
        var gm = GameManager.I;
        Assert.NotNull(gm, "GameManager missing");
        Assert.NotNull(TouchControls.I, "TouchControls missing");
        Assert.Greater(Object.FindObjectsByType<ArenaGenerator>()[0].transform.childCount, 5, "crates not generated");

        // Hold left mouse on the right half: fire with auto-aim (no drag).
        var firePos = new Vector2(Screen.width * 0.75f, Screen.height * 0.5f);
        SetMouse(firePos, true);
        float t0 = Time.realtimeSinceStartup;
        yield return null; yield return null;
        Debug.Log($"SMOKE: fireHeld={TouchControls.I.FireHeld} screen={Screen.width}x{Screen.height}");
        while (Time.realtimeSinceStartup - t0 < 14f) yield return null;
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
        t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 0.6f) yield return null;
        Vector3 moved = gm.player.transform.position - before;
        Debug.Log($"SMOKE: moved={moved}");
        Assert.Greater(moved.x, 0.5f, "joystick should move player to the right");
        SetMouse(leftPos, false);

        // Stand still and let the zombies win.
        t0 = Time.realtimeSinceStartup;
        while (!gm.IsGameOver && Time.realtimeSinceStartup - t0 < 90f) yield return null;
        Debug.Log($"SMOKE: gameover={gm.IsGameOver} time={gm.GameTime:0.0} score={gm.Score}");
        Assert.IsTrue(gm.IsGameOver, "player should eventually die");

        t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 2.5f) yield return null;
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
    public IEnumerator VaultOverCrate()
    {
        SceneManager.LoadScene("Game");
        yield return null;
        yield return null;
        var gm = GameManager.I;
        var crate = Object.FindObjectsByType<ArenaGenerator>()[0].transform.GetChild(0);
        var box = crate.GetComponentInChildren<BoxCollider>();
        Vector3 c = box.bounds.center; c.y = 0f;
        // Approach along the crate's local X axis (perpendicular to a face).
        Vector3 dir = crate.right; dir.y = 0f; dir.Normalize();
        float half = box.bounds.extents.magnitude;
        var cc = gm.player.GetComponent<CharacterController>();
        cc.enabled = false;
        gm.player.transform.position = c - dir * (half + 1.2f);
        cc.enabled = true;
        yield return null;

        var leftPos = new Vector2(Screen.width * 0.2f, Screen.height * 0.4f);
        SetMouse(leftPos, true);
        yield return null;
        SetMouse(leftPos + new Vector2(dir.x, dir.z) * Screen.height * 0.3f, true);
        bool vaulted = false;
        float t0 = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - t0 < 2f)
        {
            vaulted |= gm.player.IsVaulting;
            yield return null;
        }
        SetMouse(leftPos, false);
        float side = Vector3.Dot(gm.player.transform.position - c, dir);
        Debug.Log($"SMOKE: vaulted={vaulted} side={side:0.00}");
        Assert.IsTrue(vaulted, "player should auto-vault");
        Assert.Greater(side, 0.5f, "player should land on the far side of the crate");
    }
}
