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
    static readonly Color UiToxic = new Color(0.55f, 0.95f, 0.2f);

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
    static Text Label(string name, Transform parent, int size, TextAnchor align, Color c, string key)
    {
        var t = Txt(name, parent, size, align, c);
        if (key != null) L(t, key);
        return t;
    }

    static Button MakeButton(string name, Transform parent, string key, Color color, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, Color? textColor = null, float shadow = 8f)
    {
        // Buttons that get hidden must not have a shadow: Panel's shadow is a sibling and would stay visible.
        var img = Panel(name, parent, color, anchor, new Vector2(0.5f, 0.5f), pos, size, shadow);
        img.raycastTarget = true; // Img/Sliced default to non-raycast (HUD lets touches through); buttons must catch clicks
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var cb = b.colors; cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); b.colors = cb;
        var lbl = Label("Label", img.transform, fontSize, TextAnchor.MiddleCenter, textColor ?? UiCream, key);
        Stretch(lbl.rectTransform);
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

    static RectTransform Logo(Transform parent, Vector2 pos, int size)
    {
        var root = Rect("Logo", parent);
        Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(size * 5f, size * 1.4f));
        var t = Txt("Holdout", root, size, TextAnchor.MiddleCenter, new Color(0.86f, 0.84f, 0.62f));
        Stretch(t.rectTransform);
        t.text = "HOLDOUT";
        foreach (var o in t.GetComponents<Shadow>()) Object.DestroyImmediate(o);
        float k = size / 200f;
        void Out(Color c, float d, bool shadow = false)
        {
            var o = shadow ? t.gameObject.AddComponent<Shadow>() : t.gameObject.AddComponent<Outline>();
            o.effectColor = c; o.effectDistance = shadow ? new Vector2(10f, -12f) * k : new Vector2(d, -d) * k;
        }
        Out(UiOrange, 5f);
        Out(UiInk, 10f);
        Out(new Color(UiToxic.r, UiToxic.g, UiToxic.b, 0.9f), 0f, true);
        return root;
    }

    // ---------------------------------------------------------------- settings panel (menu + pause)
    static SettingsPanel BuildSettingsPanel(Transform canvas)
    {
        var host = Rect("Settings", canvas);
        Stretch(host);
        var sp = host.gameObject.AddComponent<SettingsPanel>();
        var root = Img("Root", host, ui.flat, new Color(0f, 0f, 0f, 0.7f));
        Stretch(root.rectTransform); root.raycastTarget = true; // modal: blocks clicks behind
        var panel = Panel("Panel", root.transform, UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 1000f), 12f);
        var p = panel.transform;
        var title = Label("Title", p, 64, TextAnchor.MiddleCenter, UiOrange, "set.title");
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 90f));

        float lx = -620f, cx = -120f, y = 330f, dy = 82f;
        void Row(string key) { var l = Label(key, p, 38, TextAnchor.MiddleLeft, UiCream, key); Anchor(l.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(lx, y), new Vector2(480f, 60f)); }
        Button[] Segment(string name, string[] keys, string[] texts, float w)
        {
            var arr = new Button[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                arr[i] = MakeButton(name + i, p, keys[i], UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(cx + w * 0.5f + i * (w + 16f), y), new Vector2(w, 64f), 32, null, 0f);
                if (keys[i] == null) arr[i].GetComponentInChildren<Text>().text = texts[i];
            }
            return arr;
        }
        Row("set.music"); sp.music = MakeSlider("Music", p, new Vector2(cx, y), 700f); y -= dy;
        Row("set.sfx"); sp.sfx = MakeSlider("Sfx", p, new Vector2(cx, y), 700f); y -= dy;
        Row("set.vibration");
        sp.vibration = MakeButton("Vibration", p, "set.on", UiOrange, new Vector2(0.5f, 0.5f), new Vector2(cx + 110f, y), new Vector2(220f, 64f), 32, null, 0f);
        sp.vibrationLabel = sp.vibration.GetComponentInChildren<Text>();
        Object.DestroyImmediate(sp.vibrationLabel.GetComponent<LocText>());
        y -= dy;
        Row("set.fps"); sp.fpsButtons = Segment("Fps", new string[4], Settings.FpsOptions.Select(f => f.ToString()).ToArray(), 160f); y -= dy;
        Row("set.quality"); sp.qualityButtons = Segment("Quality", new[] { "set.q0", "set.q1", "set.q2" }, null, 220f); y -= dy;
        Row("set.joy_size"); sp.joySize = MakeSlider("JoySize", p, new Vector2(cx, y), 700f); y -= dy;
        Row("set.joy_alpha"); sp.joyAlpha = MakeSlider("JoyAlpha", p, new Vector2(cx, y), 700f); y -= dy;
        Row("set.lang"); sp.langButtons = Segment("Lang", new string[2], new[] { "TÜRKÇE", "ENGLISH" }, 260f); y -= dy + 20f;

        sp.support = MakeButton("Support", p, "set.support", UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(-500f, y), new Vector2(250f, 76f), 30);
        sp.privacy = MakeButton("Privacy", p, "set.privacy", UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(-235f, y), new Vector2(250f, 76f), 30);
        sp.terms = MakeButton("Terms", p, "set.terms", UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(30f, y), new Vector2(250f, 76f), 30);
        sp.reset = MakeButton("Reset", p, "set.reset", new Color(0.55f, 0.16f, 0.12f), new Vector2(0.5f, 0.5f), new Vector2(395f, y), new Vector2(440f, 76f), 28);
        sp.close = MakeButton("Close", p, "set.close", UiOrange, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(380f, 96f), 44, UiInk);
        sp.restore = MakeButton("Restore", p, "set.restore", UiKhakiDark, new Vector2(0.5f, 0f), new Vector2(450f, 70f), new Vector2(440f, 80f), 26);
        var mail = Txt("Mail", p, 24, TextAnchor.MiddleLeft, new Color(UiCream.r, UiCream.g, UiCream.b, 0.7f));
        Anchor(mail.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 22f), new Vector2(600f, 36f));
        mail.text = SettingsPanel.SupportMail;
        sp.resetDone = Label("ResetDone", p, 34, TextAnchor.MiddleCenter, UiGold, "set.reset_done");
        Anchor(sp.resetDone.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(800f, 50f));

        // Confirmation for the reset.
        var conf = Img("Confirm", root.transform, ui.flat, new Color(0f, 0f, 0f, 0.6f));
        Stretch(conf.rectTransform);
        var cp = Panel("Panel", conf.transform, UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 460f), 12f);
        var q = Label("Question", cp.transform, 42, TextAnchor.MiddleCenter, UiCream, "set.reset_q");
        Anchor(q.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(880f, 220f));
        q.horizontalOverflow = HorizontalWrapMode.Wrap;
        sp.confirmYes = MakeButton("Yes", cp.transform, "set.yes", new Color(0.7f, 0.18f, 0.14f), new Vector2(0.5f, 0f), new Vector2(-230f, 90f), new Vector2(400f, 100f), 38);
        sp.confirmNo = MakeButton("No", cp.transform, "set.no", UiOrange, new Vector2(0.5f, 0f), new Vector2(230f, 90f), new Vector2(400f, 100f), 38, UiInk);
        sp.confirm = conf.gameObject;
        sp.root = root.gameObject;
        sp.onColor = UiOrange; sp.offColor = UiKhakiDark;
        return sp;
    }

    // ---------------------------------------------------------------- game scene: pause button + menu, game-over menu button
    static void BuildPauseUi(HUD hud, TouchControls controls)
    {
        var root = hud.transform;
        var pb = Panel("PauseButton", root, UiKhakiDark, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(96f, 96f), 0f);
        pb.raycastTarget = true;
        var pbtn = pb.gameObject.AddComponent<Button>();
        pbtn.targetGraphic = pb;
        var pi = Img("Icon", pb.transform, pauseIcon, UiCream);
        Anchor(pi.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));

        var go = root.Find("GameOver");
        var goMenu = MakeButton("MenuButton", go, "pause.menu", UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(0f, -400f), new Vector2(420f, 100f), 40);

        var panel = Img("PausePanel", root, ui.flat, new Color(0.02f, 0.02f, 0f, 0.92f));
        Stretch(panel.rectTransform); panel.raycastTarget = true;
        var title = Label("Title", panel.transform, 110, TextAnchor.MiddleCenter, UiOrange, "pause.title");
        Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1200f, 150f));
        var resume = MakeButton("Resume", panel.transform, "pause.resume", UiOrange, new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(560f, 140f), 64, UiInk);
        var set = MakeButton("Settings", panel.transform, "menu.settings", UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(560f, 120f), 52);
        var menu = MakeButton("Menu", panel.transform, "pause.menu", UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(560f, 120f), 52);

        var sp = BuildSettingsPanel(root);
        var pm = panel.gameObject.AddComponent<PauseMenu>();
        pm.pauseButton = pbtn; pm.resume = resume; pm.settings = set; pm.mainMenu = menu; pm.gameOverMenu = goMenu;
        pm.panel = panel.gameObject; pm.settingsPanel = sp;
        // PauseMenu lives on the panel it hides: keep it on a separate always-active host instead.
        Object.DestroyImmediate(pm);
        var host = new GameObject("PauseMenu").AddComponent<PauseMenu>();
        host.transform.SetParent(root, false);
        host.pauseButton = pbtn; host.resume = resume; host.settings = set; host.mainMenu = menu; host.gameOverMenu = goMenu;
        host.panel = panel.gameObject; host.settingsPanel = sp;
        controls.uiButtons = new[] { pb.rectTransform, goMenu.GetComponent<RectTransform>() };
        if (!root.GetComponent<GraphicRaycaster>()) root.gameObject.AddComponent<GraphicRaycaster>();
        BuildEventSystem();
    }

    // ---------------------------------------------------------------- loading screen prefab (Resources)
    static void BuildLoadingPrefab()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources");
        var c = OverlayCanvas("LoadingScreen", 1000);
        var ls = c.gameObject.AddComponent<LoadingScreen>();
        ls.group = c.gameObject.AddComponent<CanvasGroup>();
        var root = c.transform;
        var bg = Img("Background", root, ui.flat, new Color(0.08f, 0.09f, 0.06f, 1f));
        Stretch(bg.rectTransform);
        Logo(root, new Vector2(0f, 230f), 150);
        var lbl = Label("Loading", root, 44, TextAnchor.MiddleCenter, UiOrange, "load.title");
        Anchor(lbl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(800f, 60f));
        var frame = Panel("Bar", root, UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(900f, 44f), 6f);
        BarFill("Trough", frame.transform, new Color(0f, 0f, 0f, 0.55f), 7f).pivot = new Vector2(0.5f, 0.5f);
        ls.barFill = BarFill("Fill", frame.transform, UiOrange, 7f);
        var tipHead = Label("TipHead", root, 34, TextAnchor.MiddleCenter, UiToxic, "load.tip");
        Anchor(tipHead.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(800f, 50f));
        ls.tip = Txt("Tip", root, 40, TextAnchor.UpperCenter, UiCream);
        Anchor(ls.tip.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1500f, 130f));
        ls.tip.horizontalOverflow = HorizontalWrapMode.Wrap;
        PrefabUtility.SaveAsPrefabAsset(c.gameObject, "Assets/Resources/LoadingScreen.prefab");
        Object.DestroyImmediate(c.gameObject);
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

        var c = OverlayCanvas("Menu", 10);
        var root = c.transform;
        var vig = Img("Vignette", root, ui.flat, new Color(0f, 0f, 0f, 0.25f));
        Stretch(vig.rectTransform);
        var mm = c.gameObject.AddComponent<MainMenu>();
        mm.logo = Logo(root, new Vector2(0f, 250f), 220);
        var tag = Label("Tagline", root, 46, TextAnchor.MiddleCenter, UiCream, "menu.tagline");
        Anchor(tag.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1000f, 70f));

        // Currency (top right).
        Text Pill(string name, Sprite icon, Color ic, float x)
        {
            var pill = Panel(name, root, UiKhakiDark, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(x, -30f), new Vector2(250f, 80f), 6f);
            var i = Img("Icon", pill.transform, icon, ic);
            Anchor(i.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(42f, 0f), new Vector2(62f, 62f));
            var v = Txt("Value", pill.transform, 40, TextAnchor.MiddleRight, UiCream);
            Stretch(v.rectTransform); v.rectTransform.offsetMax = new Vector2(-24f, 0f);
            v.text = "0";
            return v;
        }
        mm.gold = Pill("Gold", coinIcon, UiGold, -300f);
        mm.gems = Pill("Gems", gemIcon, new Color(0.45f, 0.85f, 1f), -30f);

        // Buttons.
        var main = Rect("MainButtons", root); Stretch(main);
        mm.play = MakeButton("Play", main, "menu.play", UiOrange, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(600f, 150f), 76, UiInk);
        mm.shop = MakeButton("Shop", main, "menu.shop", UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(-160f, -235f), new Vector2(290f, 110f), 46);
        mm.settings = MakeButton("Settings", main, "menu.settings", UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(160f, -235f), new Vector2(290f, 110f), 46);
        var shopLock = Img("Lock", mm.shop.transform, lockIcon, UiCream);
        Anchor(shopLock.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-10f, -10f), new Vector2(46f, 46f));
        var play = Rect("PlayButtons", root); Stretch(play);
        // Play: Story (chapter select) / Endless / Hard.
        Text ModeButton(ref Button btn, string name, string key, Color color, Color text, float x, string subKey)
        {
            btn = MakeButton(name, play, key, color, new Vector2(0.5f, 0.5f), new Vector2(x, -110f), new Vector2(440f, 210f), 60, text);
            var lbl = btn.GetComponentInChildren<Text>(); lbl.rectTransform.offsetMin = new Vector2(0f, 50f);
            var sub = Label("Sub", btn.transform, 28, TextAnchor.MiddleCenter, text == UiInk ? UiInk : UiGold, subKey);
            Stretch(sub.rectTransform); sub.rectTransform.offsetMin = new Vector2(12f, 0f); sub.rectTransform.offsetMax = new Vector2(-12f, -110f);
            return sub;
        }
        mm.storySub = ModeButton(ref mm.campaign, "Campaign", "menu.story", UiKhaki, UiCream, -470f, null);
        ModeButton(ref mm.endless, "Endless", "menu.endless", UiOrange, UiInk, 0f, "menu.endless_sub");
        mm.hardSub = ModeButton(ref mm.hard, "Hard", "menu.hard", new Color(0.55f, 0.13f, 0.1f), UiCream, 470f, null);
        var skull = Img("Skull", mm.hard.transform, ui.skull, UiCream);
        Anchor(skull.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-34f, -34f), new Vector2(54f, 54f));
        mm.back = MakeButton("Back", play, "menu.back", UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(0f, -320f), new Vector2(320f, 100f), 44);
        mm.mainButtons = main.gameObject; mm.playButtons = play.gameObject;

        // Toast.
        var toast = Panel("Toast", root, UiKhakiDark, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(620f, 100f), 0f);
        mm.toast = toast.gameObject.AddComponent<CanvasGroup>();
        mm.toast.blocksRaycasts = false;
        mm.toastText = Txt("Text", toast.transform, 46, TextAnchor.MiddleCenter, UiGold);
        Stretch(mm.toastText.rectTransform);

        mm.settingsPanel = BuildSettingsPanel(root);
        BuildEconomyMenu(root, mm, m);

        // OZT Digital splash (once per session, after Unity's).
        var spl = Img("Splash", root, ui.flat, new Color(0.03f, 0.03f, 0.03f, 1f));
        Stretch(spl.rectTransform);
        mm.splash = spl.gameObject.AddComponent<CanvasGroup>();
        var sl = Rect("Logo", spl.transform);
        Anchor(sl, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200f, 300f));
        sl.gameObject.AddComponent<CanvasGroup>();
        var oz = Label("Name", sl, 130, TextAnchor.MiddleCenter, UiCream, "menu.splash");
        Anchor(oz.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1200f, 170f));
        var ozs = Label("Sub", sl, 44, TextAnchor.MiddleCenter, UiOrange, "menu.splash_sub");
        Anchor(ozs.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(800f, 60f));
        mm.splashLogo = sl;

        BuildEventSystem();
        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }
}
