using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD canvas: controls, score/combo, health + XP bars, waves, weapon + reload button, card picker, game over,
/// first-run tips. Shared cartoon/military style: rounded 9-slice panels with a thick dark contour and inner
/// shadow, soft drop shadows, khaki green with orange accents, Rubik ExtraBold text with outline + shadow.
/// </summary>
public static partial class GameSceneBuilder
{
    static readonly Color UiGold = new Color(1f, 0.85f, 0.2f);

    static Image Sliced(string name, Transform parent, Sprite sprite, Color color)
    {
        var img = Img(name, parent, sprite, color);
        img.type = Image.Type.Sliced;
        return img;
    }

    /// <summary>Themed panel with its drop shadow (the shadow is a sibling drawn first).</summary>
    static Image Panel(string name, Transform parent, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, float shadow = 8f)
    {
        if (shadow > 0f)
        {
            var sh = Sliced(name + "Shadow", parent, ui.shadow, new Color(1f, 1f, 1f, 0.55f));
            Anchor(sh.rectTransform, anchor, pivot, pos + new Vector2(0f, -shadow), size + new Vector2(24f, 24f));
        }
        var p = Sliced(name, parent, ui.panel, color);
        Anchor(p.rectTransform, anchor, pivot, pos, size);
        return p;
    }

    /// <summary>Bar inside a framed panel: dark trough, optional delayed (white) bar, fill. Fills are anchored by anchorMax.x.</summary>
    static RectTransform BarFill(string name, Transform frame, Color color, float inset)
    {
        var f = Sliced(name, frame, ui.flat, color);
        var r = f.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        r.pivot = new Vector2(0f, 0.5f);
        return r;
    }

    static CardPicker BuildCardPicker(Transform root, Tex t)
    {
        // The picker lives on an always-active host; only the panel (bg) is toggled.
        var host = Rect("CardPickerHost", root);
        Stretch(host);
        var bg = Img("CardPicker", host, t.white, new Color(0f, 0f, 0f, 0f));
        Stretch(bg.rectTransform);
        var group = bg.gameObject.AddComponent<CanvasGroup>();
        var dim = Img("Dim", bg.transform, t.vignette, new Color(0.02f, 0.03f, 0.01f, 0.95f));
        Stretch(dim.rectTransform);
        dim.rectTransform.offsetMin = new Vector2(-300f, -300f); dim.rectTransform.offsetMax = new Vector2(300f, 300f);
        var dimFlat = Img("DimFlat", bg.transform, t.white, new Color(0.02f, 0.03f, 0.01f, 0.72f));
        Stretch(dimFlat.rectTransform);

        var titleRoot = Rect("Title", bg.transform);
        Anchor(titleRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(1200f, 160f));
        var header = Txt("Header", titleRoot, 92, TextAnchor.MiddleCenter, UiOrange);
        Anchor(header.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 22f), new Vector2(1200f, 110f));
        L(header, "cards.levelup");
        var sub = Txt("Sub", titleRoot, 36, TextAnchor.MiddleCenter, UiCream);
        Anchor(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -52f), new Vector2(1200f, 50f));

        var p = host.gameObject.AddComponent<CardPicker>();
        p.group = group; p.dim = dimFlat; p.titleRoot = titleRoot; p.header = header; p.subHeader = sub;
        p.dotFull = ui.dot; p.dotEmpty = ui.dotEmpty;
        const int n = 3;
        const float W = 460f, H = 640f;
        p.cards = new RectTransform[n]; p.backgrounds = new Image[n]; p.rarityStrips = new Image[n]; p.glows = new Image[n];
        p.flashes = new Image[n]; p.shines = new RectTransform[n]; p.newBadges = new GameObject[n];
        p.titles = new Text[n]; p.descriptions = new Text[n]; p.icons = new Image[n]; p.rarityTexts = new Text[n];
        p.dots = new Image[n * 3];
        for (int i = 0; i < n; i++)
        {
            // Card root (scaled/rotated as a whole); the glow sits behind everything.
            var card = Rect("Card" + i, bg.transform);
            Anchor(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 520f, -50f), new Vector2(W, H));
            var hit = card.gameObject.AddComponent<Image>(); hit.color = new Color(0, 0, 0, 0); hit.raycastTarget = false;
            var glow = Img("Glow", card, ui.glow, Color.white);
            Anchor(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W * 1.75f, H * 1.5f));
            var shadow = Sliced("Shadow", card, ui.shadow, new Color(1f, 1f, 1f, 0.7f));
            Anchor(shadow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(W + 40f, H + 40f));
            var body = Sliced("Body", card, ui.panel, UiKhakiDark);
            Stretch(body.rectTransform);

            // Rarity strip on top (masked so the shine sweep stays inside), rarity name on it.
            var stripRoot = Rect("Strip", card);
            Anchor(stripRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(W - 20f, 78f));
            stripRoot.gameObject.AddComponent<RectMask2D>();
            var strip = Sliced("Rarity", stripRoot, ui.flat, Color.white);
            Stretch(strip.rectTransform);
            var shine = Img("Shine", stripRoot, ui.shine, new Color(1f, 1f, 1f, 0.9f));
            Anchor(shine.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-80f, 0f), new Vector2(90f, 160f));
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var rar = Txt("RarityName", stripRoot, 34, TextAnchor.MiddleCenter, UiInk);
            Stretch(rar.rectTransform);
            rar.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.35f);
            rar.GetComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
            rar.GetComponent<Shadow>().enabled = false;

            // Big icon on a soft disc.
            var disc = Img("IconDisc", card, t.disc, new Color(0f, 0f, 0f, 0.28f));
            Anchor(disc.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -232f), new Vector2(250f, 250f));
            var ico = Img("Icon", card, ui.cards[0], Color.white);
            Anchor(ico.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -232f), new Vector2(230f, 230f));
            ico.preserveAspect = true;
            var icoSh = ico.gameObject.AddComponent<Shadow>(); icoSh.effectColor = new Color(0f, 0f, 0f, 0.45f); icoSh.effectDistance = new Vector2(0f, -8f);

            var title = Txt("Title", card, 46, TextAnchor.MiddleCenter, UiCream);
            Anchor(title.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 236f), new Vector2(W - 30f, 60f));
            var desc = Txt("Desc", card, 30, TextAnchor.UpperCenter, new Color(0.88f, 0.9f, 0.78f));
            desc.horizontalOverflow = HorizontalWrapMode.Wrap;
            Anchor(desc.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 196f), new Vector2(W - 60f, 110f));
            desc.GetComponent<Shadow>().enabled = false;
            for (int d = 0; d < 3; d++)
            {
                var dot = Img("Dot" + d, card, ui.dotEmpty, UiOrange);
                Anchor(dot.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2((d - 1) * 52f, 52f), new Vector2(40f, 40f));
                p.dots[i * 3 + d] = dot;
            }
            // "YENİ" badge in the top-right corner.
            var badge = Sliced("New", card, ui.panel, UiOrange);
            Anchor(badge.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-46f, -108f), new Vector2(118f, 50f));
            badge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            var badgeTxt = Txt("Label", badge.transform, 26, TextAnchor.MiddleCenter, UiInk);
            Stretch(badgeTxt.rectTransform);
            badgeTxt.GetComponent<Outline>().enabled = false; badgeTxt.GetComponent<Shadow>().enabled = false;
            L(badgeTxt, "cards.new");

            var flash = Sliced("Flash", card, ui.flat, new Color(1f, 1f, 1f, 0f));
            Stretch(flash.rectTransform);

            p.cards[i] = card; p.backgrounds[i] = body; p.rarityStrips[i] = strip; p.rarityTexts[i] = rar; p.glows[i] = glow;
            p.flashes[i] = flash; p.shines[i] = shine.rectTransform; p.newBadges[i] = badge.gameObject;
            p.icons[i] = ico; p.titles[i] = title; p.descriptions[i] = desc;
        }
        return p;
    }

    static HUD BuildUI(Tex t, out TouchControls controls)
    {
        var canvasGo = new GameObject("HUD", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10; // above world-space canvases (turret status) in every render mode
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

        // Off-screen threat arrows (specials always; nearby ordinary zombies too). Sized for the 17.5 m camera.
        var thrRoot = Rect("Threats", root);
        Stretch(thrRoot);
        var thrTemplate = Img("Arrow", thrRoot, t.arrow, Color.white);
        Anchor(thrTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
        var thrSh = thrTemplate.gameObject.AddComponent<Outline>(); thrSh.effectColor = new Color(UiInk.r, UiInk.g, UiInk.b, 0.9f); thrSh.effectDistance = new Vector2(3f, -3f);
        var threats = thrRoot.gameObject.AddComponent<ThreatIndicators>();
        threats.template = thrTemplate;
        threats.normalRange = 11f;
        threats.edgeMargin = 72f;

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

        // ---- Health bar (top-left): framed, dark trough, delayed white bar, coloured fill, heart + numbers.
        var hpFrame = Panel("HealthBar", root, UiKhaki, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -36f), new Vector2(470f, 52f));
        var hpTrough = BarFill("Trough", hpFrame.transform, new Color(0f, 0f, 0f, 0.55f), 8f);
        hpTrough.pivot = new Vector2(0.5f, 0.5f);
        var hpLag = BarFill("Lag", hpFrame.transform, new Color(1f, 1f, 1f, 0.9f), 8f);
        var hpFill = BarFill("Fill", hpFrame.transform, new Color(0.35f, 0.85f, 0.3f), 8f);
        var hpShine = Sliced("Shine", hpFill, ui.flat, new Color(1f, 1f, 1f, 0.22f));
        hpShine.rectTransform.anchorMin = new Vector2(0f, 0.55f); hpShine.rectTransform.anchorMax = Vector2.one;
        hpShine.rectTransform.offsetMin = new Vector2(4f, 0f); hpShine.rectTransform.offsetMax = new Vector2(-4f, -3f);
        var hpText = Txt("Value", hpFrame.transform, 28, TextAnchor.MiddleRight, UiCream);
        Stretch(hpText.rectTransform); hpText.rectTransform.offsetMax = new Vector2(-18f, 0f);
        hpText.text = "100";
        var hpLabel = Txt("Label", hpFrame.transform, 24, TextAnchor.MiddleLeft, UiCream);
        Stretch(hpLabel.rectTransform); hpLabel.rectTransform.offsetMin = new Vector2(46f, 0f);
        L(hpLabel, "hud.health");
        var heart = Img("Heart", hpFrame.transform, ui.heart, new Color(1f, 0.3f, 0.28f));
        Anchor(heart.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(76f, 76f));
        hud.healthFill = hpFill; hud.healthFillImage = hpFill.GetComponent<Image>(); hud.healthLag = hpLag; hud.healthText = hpText;
        hud.healthHeart = heart.rectTransform;

        // ---- XP bar + level badge (under the health bar), glows on level-up.
        var xpFrame = Panel("XpBar", root, UiKhaki, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -96f), new Vector2(400f, 34f), 6f);
        BarFill("Trough", xpFrame.transform, new Color(0f, 0f, 0f, 0.55f), 7f).pivot = new Vector2(0.5f, 0.5f);
        var xpFill = BarFill("Fill", xpFrame.transform, UiOrange, 7f);
        xpFill.anchorMax = new Vector2(0f, 1f);
        var xpGlow = Img("Glow", xpFrame.transform, ui.glow, new Color(1f, 0.75f, 0.3f, 0f));
        Anchor(xpGlow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 150f));
        var lvBadge = Img("LevelBadge", root, ui.star, UiOrange);
        Anchor(lvBadge.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(478f, -112f), new Vector2(84f, 84f));
        var lvlNum = Txt("Num", lvBadge.transform, 30, TextAnchor.MiddleCenter, UiInk);
        Stretch(lvlNum.rectTransform); lvlNum.rectTransform.offsetMin = new Vector2(0f, -6f);
        lvlNum.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.4f); lvlNum.GetComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
        lvlNum.GetComponent<Shadow>().enabled = false;
        lvlNum.text = "1";
        var lvl = Txt("Level", root, 26, TextAnchor.MiddleLeft, UiCream);
        Anchor(lvl.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(528f, -112f), new Vector2(160f, 40f));
        lvl.text = Loc.F("hud.level", 1);
        hud.xpFill = xpFill; hud.levelText = lvl; hud.levelNumber = lvlNum; hud.levelBadge = lvBadge.rectTransform; hud.xpGlow = xpGlow;

        var waveLabel = Txt("WaveLabel", root, 32, TextAnchor.UpperLeft, UiOrange);
        Anchor(waveLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(66f, -138f), new Vector2(400f, 44f));
        waveLabel.text = "";
        hud.waveLabel = waveLabel;

        // ---- Owned cards: framed badges, icon, level number in the corner, flash when new/levelled.
        var row = Rect("Cards", root);
        Anchor(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -186f), new Vector2(1000f, 70f));
        var badge = Sliced("CardBadge", row, ui.panel, UiKhakiDark);
        Anchor(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(34f, -34f), new Vector2(68f, 68f));
        var bIcon = Img("Icon", badge.transform, ui.cards[0], Color.white);
        Anchor(bIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(50f, 50f));
        bIcon.preserveAspect = true;
        var bLvBg = Img("LevelBg", badge.transform, ui.dot, UiOrange);
        Anchor(bLvBg.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-4f, 6f), new Vector2(30f, 30f));
        var bLv = Txt("Level", bLvBg.transform, 20, TextAnchor.MiddleCenter, UiInk);
        Stretch(bLv.rectTransform);
        bLv.GetComponent<Outline>().enabled = false; bLv.GetComponent<Shadow>().enabled = false;
        bLv.text = "1";
        var bFlash = Sliced("Flash", badge.transform, ui.flat, new Color(1f, 1f, 1f, 0f));
        Stretch(bFlash.rectTransform);
        hud.cardRow = row; hud.cardIconTemplate = badge;

        // ---- Score + combo (top-centre)
        var score = Txt("Score", root, 76, TextAnchor.UpperCenter, UiCream);
        Anchor(score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(600f, 90f));
        score.text = "0";
        hud.scoreText = score;
        var comboRoot = Rect("Combo", root);
        Anchor(comboRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(400f, 110f));
        var mult = Txt("Multiplier", comboRoot, 62, TextAnchor.UpperCenter, UiOrange);
        Anchor(mult.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(400f, 64f));
        mult.text = "x1";
        var comboTxt = Txt("ComboCount", comboRoot, 28, TextAnchor.UpperCenter, UiCream);
        Anchor(comboTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(400f, 32f));
        var barBg = Sliced("ComboBarBg", comboRoot, ui.panel, UiKhakiDark);
        Anchor(barBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -102f), new Vector2(240f, 22f));
        var bar = BarFill("Bar", barBg.transform, UiOrange, 5f);
        bar.pivot = new Vector2(0.5f, 0.5f);
        comboRoot.gameObject.SetActive(false);
        var broken = Txt("ComboBroken", root, 60, TextAnchor.UpperCenter, new Color(1f, 0.3f, 0.25f));
        Anchor(broken.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(400f, 64f));
        broken.gameObject.SetActive(false);
        hud.multiplierText = mult; hud.comboText = comboTxt; hud.comboRoot = comboRoot; hud.comboBar = bar; hud.comboBrokenText = broken;

        // ---- Wave banner (centre) + breather countdown (upper centre)
        var bannerRoot = Rect("WaveBanner", root);
        Anchor(bannerRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(900f, 200f));
        var bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false; bannerGroup.alpha = 0f;
        var banner = Txt("Title", bannerRoot, 116, TextAnchor.MiddleCenter, UiCream);
        Anchor(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(900f, 130f));
        var bannerSub = Txt("Sub", bannerRoot, 38, TextAnchor.MiddleCenter, UiOrange);
        Anchor(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(900f, 50f));
        var breakTxt = Txt("Breather", root, 48, TextAnchor.UpperCenter, UiCream);
        Anchor(breakTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(900f, 120f));
        breakTxt.gameObject.SetActive(false);
        hud.waveBanner = banner; hud.waveSubBanner = bannerSub; hud.waveBannerGroup = bannerGroup; hud.breakText = breakTxt;

        // FPS (top-right)
        var fps = Txt("FPS", root, 24, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.7f));
        Anchor(fps.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(240f, 40f));
        fps.text = "-- FPS";
        hud.fpsText = fps;

        // ---- Weapon cluster (bottom-right): reload button, ammo plate, active-reload bar
        var rbShadow = Img("ReloadButtonShadow", root, ui.glow, new Color(0f, 0f, 0f, 0.5f));
        Anchor(rbShadow.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 140f), new Vector2(240f, 240f));
        var rb = Img("ReloadButton", root, ui.dot, UiKhaki);
        Anchor(rb.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-150f, 150f), new Vector2(190f, 190f));
        var rbFill = Img("Fill", rb.transform, ui.ringThick, UiOrange);
        Anchor(rbFill.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(168f, 168f));
        rbFill.type = Image.Type.Filled; rbFill.fillMethod = Image.FillMethod.Radial360; rbFill.fillOrigin = 2; rbFill.fillAmount = 0f;
        var rbLabel = Txt("Label", rb.transform, 30, TextAnchor.MiddleCenter, UiCream);
        Stretch(rbLabel.rectTransform);
        L(rbLabel, "hud.reload_button");
        hud.reloadButton = rb.rectTransform; hud.reloadButtonFill = rbFill;

        var ammoPlate = Panel("AmmoPlate", root, UiKhaki, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 262f), new Vector2(250f, 84f), 6f);
        var ammo = Txt("Ammo", root, 62, TextAnchor.MiddleRight, UiCream);
        Anchor(ammo.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-62f, 264f), new Vector2(360f, 80f));
        ammo.text = "40<size=28> / 40</size>";
        hud.ammoText = ammo; hud.ammoPlate = ammoPlate.gameObject;

        var reloadRoot = Rect("Reload", root);
        Anchor(reloadRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 360f), new Vector2(460f, 80f));
        var reloadLabel = Txt("Label", reloadRoot, 28, TextAnchor.LowerRight, UiOrange);
        Anchor(reloadLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(460f, 34f));
        var reloadBg = Sliced("BarBg", reloadRoot, ui.panel, UiKhakiDark);
        Anchor(reloadBg.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(460f, 38f));
        var sweet = Sliced("SweetSpot", reloadBg.transform, ui.flat, new Color(1f, 0.6f, 0.15f, 0.7f));
        sweet.rectTransform.offsetMin = new Vector2(0f, 7f); sweet.rectTransform.offsetMax = new Vector2(0f, -7f);
        var reloadBar = Sliced("Bar", reloadBg.transform, ui.flat, Color.white);
        reloadBar.rectTransform.anchorMin = Vector2.zero; reloadBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        reloadBar.rectTransform.offsetMin = new Vector2(7f, 11f); reloadBar.rectTransform.offsetMax = new Vector2(0f, -11f);
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
        var leftTip = Txt("Left", tips, 46, TextAnchor.MiddleCenter, UiCream);
        Anchor(leftTip.rectTransform, new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(800f, 200f));
        L(leftTip, "tut.left");
        var rightTip = Txt("Right", tips, 46, TextAnchor.MiddleCenter, UiCream);
        Anchor(rightTip.rectTransform, new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(800f, 200f));
        L(rightTip, "tut.right");
        hud.tutorialGroup = tipsGroup;

        // Level-up card picker (above the HUD, below the game-over panel)
        var picker = BuildCardPicker(root, t);

        // Game over panel
        var go = Img("GameOver", root, t.white, new Color(0.02f, 0.02f, 0f, 0.86f));
        Stretch(go.rectTransform);
        var title = Txt("Title", go.transform, 124, TextAnchor.MiddleCenter, new Color(1f, 0.32f, 0.24f));
        Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(1000f, 150f));
        var record = Txt("Record", go.transform, 72, TextAnchor.MiddleCenter, UiOrange);
        Anchor(record.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(1000f, 90f));
        record.gameObject.SetActive(false);
        var stats = Txt("Stats", go.transform, 62, TextAnchor.UpperCenter, UiCream);
        Anchor(stats.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 110f), new Vector2(1600f, 220f));
        var again = Panel("RestartButton", go.transform, UiOrange, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -230f), new Vector2(560f, 170f), 12f);
        var againLabel = Txt("Label", again.transform, 84, TextAnchor.MiddleCenter, UiInk);
        Stretch(againLabel.rectTransform);
        L(againLabel, "over.again");
        againLabel.GetComponent<Outline>().effectColor = new Color(1f, 1f, 1f, 0.35f);
        againLabel.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
        againLabel.GetComponent<Shadow>().enabled = false;
        go.gameObject.SetActive(false);
        hud.gameOverPanel = go.gameObject; hud.gameOverTitle = title; hud.gameOverStats = stats; hud.recordText = record;
        hud.restartButton = again.rectTransform;

        controls = canvasGo.AddComponent<TouchControls>();
        controls.canvas = canvas;
        controls.joyBase = joyBase.rectTransform; controls.joyKnob = joyKnob.rectTransform; controls.joyGroup = joyGroup;
        controls.aimBase = aimBase.rectTransform; controls.aimKnob = aimKnob.rectTransform; controls.aimGroup = aimGroup;
        controls.reloadButton = rb.rectTransform;
        controls.restartButton = again.rectTransform;
        controls.cardRects = picker.cards;
        return hud;
    }

    /// <summary>Static label bound to a Loc key (text set now and on language change).</summary>
    static void L(Text t, string key)
    {
        t.text = Loc.T(key);
        t.gameObject.AddComponent<LocText>().key = key;
    }
}
