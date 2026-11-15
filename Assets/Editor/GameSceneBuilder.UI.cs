using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD canvas: controls, score/combo, health, waves, weapon + reload button, game over, first-run tips.</summary>
public static partial class GameSceneBuilder
{
    static readonly Color UiGold = new Color(1f, 0.85f, 0.2f);

    static HUD BuildUI(Tex t, out TouchControls controls)
    {
        var canvasGo = new GameObject("HUD", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        var root = canvasGo.transform;
        var hud = canvasGo.AddComponent<HUD>();

        // Damage vignette
        var vig = Img("DamageVignette", root, t.vignette, new Color(0.85f, 0.05f, 0.05f, 0f));
        Stretch(vig.rectTransform);
        hud.damageVignette = vig;

        // Damage numbers (under the rest of the HUD)
        var dnRoot = Rect("DamageNumbers", root);
        Stretch(dnRoot);
        var dnTemplate = Txt("Number", dnRoot, 38, TextAnchor.MiddleCenter, Color.white);
        Anchor(dnTemplate.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 70f));
        dnRoot.gameObject.AddComponent<DamageNumbers>().template = dnTemplate;

        // Off-screen threat arrows (specials always; nearby ordinary zombies too)
        var thrRoot = Rect("Threats", root);
        Stretch(thrRoot);
        var thrTemplate = Img("Arrow", thrRoot, t.arrow, Color.white);
        Anchor(thrTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
        var threats = thrRoot.gameObject.AddComponent<ThreatIndicators>();
        threats.template = thrTemplate;

        // Joystick + aim ring
        var joyBase = Img("JoystickBase", root, t.ring, Color.white);
        Anchor(joyBase.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(260f, 240f), new Vector2(260f, 260f));
        var joyGroup = joyBase.gameObject.AddComponent<CanvasGroup>();
        joyGroup.alpha = 0.25f; joyGroup.blocksRaycasts = false; joyGroup.interactable = false;
        var joyKnob = Img("Knob", joyBase.transform, t.disc, new Color(1f, 1f, 1f, 0.8f));
        Anchor(joyKnob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
        var aimBase = Img("AimBase", root, t.ring, new Color(1f, 0.45f, 0.35f, 1f));
        Anchor(aimBase.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(1600f, 240f), new Vector2(200f, 200f));
        var aimGroup = aimBase.gameObject.AddComponent<CanvasGroup>();
        aimGroup.alpha = 0f; aimGroup.blocksRaycasts = false; aimGroup.interactable = false;
        var aimKnob = Img("Knob", aimBase.transform, t.disc, new Color(1f, 0.5f, 0.4f, 0.85f));
        Anchor(aimKnob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));

        // Health bar + wave label (top-left)
        var hpBg = Img("HealthBar", root, t.white, new Color(0f, 0f, 0f, 0.55f));
        Anchor(hpBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(440f, 36f));
        var hpFill = Img("Fill", hpBg.transform, t.white, new Color(0.3f, 0.9f, 0.3f));
        var fr = hpFill.rectTransform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(4f, 4f); fr.offsetMax = new Vector2(-4f, -4f);
        fr.pivot = new Vector2(0f, 0.5f);
        var hpLabel = Txt("Label", hpBg.transform, 24, TextAnchor.MiddleLeft, Color.white);
        Stretch(hpLabel.rectTransform); hpLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
        hpLabel.text = "CAN";
        hud.healthFill = fr; hud.healthFillImage = hpFill;
        var waveLabel = Txt("WaveLabel", root, 34, TextAnchor.UpperLeft, UiGold);
        Anchor(waveLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(42f, -86f), new Vector2(400f, 44f));
        waveLabel.text = "";
        hud.waveLabel = waveLabel;

        // Score + combo (top-centre)
        var score = Txt("Score", root, 72, TextAnchor.UpperCenter, Color.white);
        Anchor(score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(600f, 90f));
        score.text = "0";
        hud.scoreText = score;
        var comboRoot = Rect("Combo", root);
        Anchor(comboRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(400f, 110f));
        var mult = Txt("Multiplier", comboRoot, 60, TextAnchor.UpperCenter, UiGold);
        Anchor(mult.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(400f, 64f));
        mult.text = "x1";
        var comboTxt = Txt("ComboCount", comboRoot, 28, TextAnchor.UpperCenter, Color.white);
        Anchor(comboTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(400f, 32f));
        var barBg = Img("ComboBarBg", comboRoot, t.white, new Color(0f, 0f, 0f, 0.5f));
        Anchor(barBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(220f, 10f));
        var bar = Img("Bar", barBg.transform, t.white, UiGold);
        bar.rectTransform.anchorMin = Vector2.zero; bar.rectTransform.anchorMax = Vector2.one;
        bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
        comboRoot.gameObject.SetActive(false);
        var broken = Txt("ComboBroken", root, 60, TextAnchor.UpperCenter, new Color(1f, 0.3f, 0.25f));
        Anchor(broken.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(400f, 64f));
        broken.gameObject.SetActive(false);
        hud.multiplierText = mult; hud.comboText = comboTxt; hud.comboRoot = comboRoot; hud.comboBar = bar.rectTransform; hud.comboBrokenText = broken;

        // Wave banner (centre) + breather countdown (upper centre)
        var bannerRoot = Rect("WaveBanner", root);
        Anchor(bannerRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(900f, 200f));
        var bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false; bannerGroup.alpha = 0f;
        var banner = Txt("Title", bannerRoot, 110, TextAnchor.MiddleCenter, Color.white);
        Anchor(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 130f));
        var bannerSub = Txt("Sub", bannerRoot, 36, TextAnchor.MiddleCenter, UiGold);
        Anchor(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(900f, 50f));
        var breakTxt = Txt("Breather", root, 48, TextAnchor.UpperCenter, Color.white);
        Anchor(breakTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(900f, 120f));
        breakTxt.gameObject.SetActive(false);
        hud.waveBanner = banner; hud.waveSubBanner = bannerSub; hud.waveBannerGroup = bannerGroup; hud.breakText = breakTxt;

        // FPS (top-right)
        var fps = Txt("FPS", root, 26, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.7f));
        Anchor(fps.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(240f, 40f));
        fps.text = "-- FPS";
        hud.fpsText = fps;

        // Weapon cluster (bottom-right): reload button, ammo, active-reload bar
        var rb = Img("ReloadButton", root, t.ring, new Color(1f, 1f, 1f, 0.85f));
        Anchor(rb.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 150f), new Vector2(190f, 190f));
        var rbFill = Img("Fill", rb.transform, t.disc, new Color(1f, 0.85f, 0.2f, 0.35f));
        Anchor(rbFill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 160f));
        rbFill.type = Image.Type.Filled; rbFill.fillMethod = Image.FillMethod.Radial360; rbFill.fillOrigin = 2; rbFill.fillAmount = 0f;
        var rbLabel = Txt("Label", rb.transform, 30, TextAnchor.MiddleCenter, Color.white);
        Stretch(rbLabel.rectTransform);
        rbLabel.text = "DOLDUR";
        hud.reloadButton = rb.rectTransform; hud.reloadButtonFill = rbFill;

        var ammo = Txt("Ammo", root, 64, TextAnchor.LowerRight, Color.white);
        Anchor(ammo.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 260f), new Vector2(360f, 80f));
        ammo.text = "40<size=28> / 40</size>";
        hud.ammoText = ammo;

        var reloadRoot = Rect("Reload", root);
        Anchor(reloadRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 345f), new Vector2(460f, 80f));
        var reloadLabel = Txt("Label", reloadRoot, 28, TextAnchor.LowerRight, UiGold);
        Anchor(reloadLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(460f, 34f));
        var reloadBg = Img("BarBg", reloadRoot, t.white, new Color(0f, 0f, 0f, 0.6f));
        Anchor(reloadBg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 6f), new Vector2(460f, 26f));
        var sweet = Img("SweetSpot", reloadBg.transform, t.white, new Color(1f, 0.85f, 0.2f, 0.55f));
        sweet.rectTransform.offsetMin = sweet.rectTransform.offsetMax = Vector2.zero;
        var reloadBar = Img("Bar", reloadBg.transform, t.white, Color.white);
        reloadBar.rectTransform.anchorMin = Vector2.zero; reloadBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        reloadBar.rectTransform.offsetMin = new Vector2(0f, 6f); reloadBar.rectTransform.offsetMax = new Vector2(0f, -6f);
        reloadRoot.gameObject.SetActive(false);
        hud.reloadRoot = reloadRoot.gameObject; hud.reloadBar = reloadBar.rectTransform; hud.reloadBarImage = reloadBar;
        hud.reloadSweetSpot = sweet.rectTransform; hud.reloadLabel = reloadLabel;

        // First-run control tips (left / right halves)
        var tips = Rect("Tutorial", root);
        Stretch(tips);
        var tipsGroup = tips.gameObject.AddComponent<CanvasGroup>();
        tipsGroup.blocksRaycasts = false;
        var divider = Img("Divider", tips, t.white, new Color(1f, 1f, 1f, 0.35f));
        Anchor(divider.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(4f, 420f));
        var leftTip = Txt("Left", tips, 46, TextAnchor.MiddleCenter, Color.white);
        Anchor(leftTip.rectTransform, new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(800f, 200f));
        leftTip.text = "SOL TARAF\n<size=34>parmağını koy ve sürükle → HAREKET</size>";
        var rightTip = Txt("Right", tips, 46, TextAnchor.MiddleCenter, Color.white);
        Anchor(rightTip.rectTransform, new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(800f, 200f));
        rightTip.text = "SAĞ TARAF\n<size=34>basılı tut → ATEŞ (en yakına nişan)\nsürükle → istediğin yöne</size>";
        hud.tutorialGroup = tipsGroup;

        // Game over panel
        var go = Img("GameOver", root, t.white, new Color(0.05f, 0f, 0f, 0.72f));
        Stretch(go.rectTransform);
        var title = Txt("Title", go.transform, 120, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.25f));
        Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1000f, 150f));
        var record = Txt("Record", go.transform, 70, TextAnchor.MiddleCenter, UiGold);
        Anchor(record.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(1000f, 90f));
        record.gameObject.SetActive(false);
        var stats = Txt("Stats", go.transform, 64, TextAnchor.UpperCenter, Color.white);
        Anchor(stats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 110f), new Vector2(1600f, 220f));
        var again = Img("RestartButton", go.transform, t.white, new Color(1f, 0.82f, 0.2f, 1f));
        Anchor(again.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(560f, 170f));
        var againLabel = Txt("Label", again.transform, 84, TextAnchor.MiddleCenter, new Color(0.15f, 0.08f, 0.05f));
        Stretch(againLabel.rectTransform);
        againLabel.text = "TEKRAR";
        againLabel.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.3f);
        go.gameObject.SetActive(false);
        hud.gameOverPanel = go.gameObject; hud.gameOverTitle = title; hud.gameOverStats = stats; hud.recordText = record;
        hud.restartButton = again.rectTransform;

        controls = canvasGo.AddComponent<TouchControls>();
        controls.canvas = canvas;
        controls.joyBase = joyBase.rectTransform; controls.joyKnob = joyKnob.rectTransform; controls.joyGroup = joyGroup;
        controls.aimBase = aimBase.rectTransform; controls.aimKnob = aimKnob.rectTransform; controls.aimGroup = aimGroup;
        controls.reloadButton = rb.rectTransform;
        controls.restartButton = again.rectTransform;
        return hud;
    }
}
