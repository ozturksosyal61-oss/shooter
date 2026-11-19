using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>F10 — name + identity, main menu scene, settings panel, pause menu, loading screen.</summary>
public static partial class GameSceneBuilder
{
    const string MenuScenePath = "Assets/Scenes/Menu.unity";
    const string BundleId = "com.oztdigital.holdout";
    static readonly Color UiToxic = new Color(0.651f, 0.886f, 0.18f);

    static Sprite coinIcon, gemIcon, pauseIcon, lockIcon;

    // ---------------------------------------------------------------- identity
    static void ConfigurePlayer() => ProjectSetup.Apply();

    static void MenuIcons()
    {
        coinIcon = Icon("Icon_Coin", p => Mathf.Max(p.magnitude - 0.82f, -(Mathf.Abs(p.magnitude - 0.52f) - 0.07f)));
        gemIcon = Icon("Icon_Gem", p =>
        {
            float x = Mathf.Abs(p.x), y = p.y + 0.05f;
            return Mathf.Max(y - 0.55f, Mathf.Max((x - 0.85f * (y + 0.85f)) / 1.31f, (x + 0.75f * (y - 0.15f) - 0.85f) / 1.25f));
        });
        pauseIcon = Icon("Icon_Pause", p => Min(RoundBox(p.x - 0.3f, p.y, 0.16f, 0.62f, 0.07f), RoundBox(p.x + 0.3f, p.y, 0.16f, 0.62f, 0.07f)));
        lockIcon = Icon("Icon_Lock", p => Min(
            RoundBox(p.x, p.y + 0.25f, 0.62f, 0.48f, 0.1f),
            Mathf.Max(Mathf.Abs((p - new Vector2(0f, 0.3f)).magnitude - 0.38f) - 0.1f, -(p.y - 0.3f))));
    }

    // ---------------------------------------------------------------- widgets
    static UiText Label(string name, Transform parent, int size, TextAnchor align, Color c, string key)
    {
        var t = Txt(name, parent, size, align, c);
        if (key != null) L(t, key);
        return t;
    }

    /// <summary>Older call sites: a redesign press-button (orange → primary, toxic → reward, else dark).</summary>
    static Button MakeButton(string name, Transform parent, string key, Color color, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color? textColor = null, float shadow = 8f)
    {
        var style = color == UiOrange ? UiStyle.Yellow : color == UiToxic ? UiStyle.Green : UiStyle.Dark;
        var b = KButton(name, parent, style, key, size.x, size.y, fontSize);
        Anchor((RectTransform)b.transform, anchor, new Vector2(0.5f, 0.5f), pos, size);
        return b;
    }

    static Slider MakeSlider(string name, Transform parent, Vector2 pos, float width)
    {
        var root = Rect(name, parent);
        Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), pos, new Vector2(width, 44f));
        var bg = Sliced("Background", root, ui.flat, new Color(0f, 0f, 0f, 0.5f));
        Stretch(bg.rectTransform); bg.raycastTarget = true;
        var fillArea = Rect("Fill Area", root);
        Stretch(fillArea); fillArea.offsetMin = new Vector2(8f, 8f); fillArea.offsetMax = new Vector2(-8f, -8f);
        var fill = Sliced("Fill", fillArea, ui.flat, UiOrange);
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        var handleArea = Rect("Handle Slide Area", root);
        Stretch(handleArea); handleArea.offsetMin = new Vector2(24f, 0f); handleArea.offsetMax = new Vector2(-24f, 0f);
        var handle = Img("Handle", handleArea, ui.dot, UiCream);
        handle.rectTransform.sizeDelta = new Vector2(58f, 0f);
        handle.rectTransform.anchorMin = new Vector2(0.5f, 0f); handle.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        handle.rectTransform.offsetMin = new Vector2(-29f, -10f); handle.rectTransform.offsetMax = new Vector2(29f, 10f);
        var s = root.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle; handle.raycastTarget = true;
        s.direction = Slider.Direction.LeftToRight; s.minValue = 0f; s.maxValue = 1f; s.value = 0.5f;
        return s;
    }

    static void BuildEventSystem()
    {
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    static Canvas OverlayCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920f, 1080f);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 1f;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    // ---------------------------------------------------------------- menu scene
    static void BuildMenuScene(Env env, CityMats cm, CityKit kit, Mats m)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // A quiet corner of the same city, a few chunks away from the spawn block.
        var focus = new GameObject("MenuFocus").transform;
        focus.position = new Vector3(30f, 0f, 50f);
        BuildCity(focus, env, cm, kit, out var light, out var atmo);
        atmo.focus = focus;

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f; cam.nearClipPlane = 1f; cam.farClipPlane = 90f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.21f, 0.23f);
        cam.cullingMask = ~(1 << GameLayers.Preview);
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        camGo.AddComponent<MenuCamera>().focus = focus;
        atmo.cam = cam;
        BuildAudio(camGo.transform);

        BuildMenuUi(m);
        BuildEventSystem();
        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }
}
