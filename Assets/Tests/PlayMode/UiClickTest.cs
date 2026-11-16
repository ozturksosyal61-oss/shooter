using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

// Real pointer clicks through EventSystem + InputSystemUIInputModule (the smoke tests call onClick directly,
// which hid that every button graphic was built with raycastTarget = false).
public class UiClickTest
{
    Mouse mouse;

    [TearDown]
    public void TearDown()
    {
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        mouse = null;
        GameManager.TimeScale = 1f;
    }

    [UnityTest]
    public IEnumerator ButtonsReceiveRealClicks()
    {
        SaveSystem.Store = new MemorySaveStore();
        SaveSystem.Data.tutorialDone = true;
        SceneManager.LoadScene(LoadingScreen.MenuScene);
        yield return null; yield return null;
        var mm = MainMenu.I;
        float t = 0f;
        while (mm.SplashActive && t < 5f) { t += Time.unscaledDeltaTime; yield return null; }
        yield return null;
        CloseOpenModals();
        yield return null;
        AssertAllButtonsRaycastable();
        mouse = InputSystem.AddDevice<Mouse>();

        bool clicked = false;
        mm.settings.onClick.AddListener(() => clicked = true);
        yield return Click(mm.settings);
        Assert.IsTrue(clicked, "real mouse click reaches the menu SETTINGS button");
        Assert.IsTrue(mm.settingsPanel.IsOpen, "settings panel opened");
        yield return Click(mm.settingsPanel.close);
        Assert.IsFalse(mm.settingsPanel.IsOpen, "settings CLOSE button works");

        // Game scene: every button can be hit, the pause button pauses.
        SceneManager.LoadScene("Game");
        yield return null; yield return null; yield return null;
        AssertAllButtonsRaycastable();
        var pm = Object.FindAnyObjectByType<PauseMenu>();
        yield return Click(pm.pauseButton);
        Assert.IsTrue(pm.panel.activeSelf, "pause button opens the pause menu");
        yield return Click(pm.resume);
        Assert.IsFalse(pm.panel.activeSelf, "DEVAM resumes");
    }

    static void CloseOpenModals()
    {
        foreach (var p in Object.FindObjectsByType<DailyLoginPanel>(FindObjectsSortMode.None)) p.root.SetActive(false);
        foreach (var p in Object.FindObjectsByType<QuestsPanel>(FindObjectsSortMode.None)) p.root.SetActive(false);
    }

    static void AssertAllButtonsRaycastable()
    {
        foreach (var s in Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var g = s.targetGraphic;
            Assert.IsTrue(g && g.raycastTarget, $"{Path(s.transform)}: target graphic must be a raycast target");
        }
    }

    IEnumerator Click(Button b)
    {
        Vector2 pos = RectTransformUtility.WorldToScreenPoint(null, b.transform.position);
        var es = EventSystem.current;
        var hits = new List<RaycastResult>();
        es.RaycastAll(new PointerEventData(es) { position = pos }, hits);
        Assert.IsTrue(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(b.transform),
            $"{Path(b.transform)} should be the top hit, got {(hits.Count > 0 ? Path(hits[0].gameObject.transform) : "nothing")}");
        InputSystem.QueueStateEvent(mouse, new MouseState { position = pos });
        yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left, true));
        yield return null; yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left, false));
        yield return null; yield return null;
    }

    static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name : t.name;
}
