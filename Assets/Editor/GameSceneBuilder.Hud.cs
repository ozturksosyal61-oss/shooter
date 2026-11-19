using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Redesigned game HUD (docs/ui-ref 11–14): health + XP top-left, wave pill + boss bar top-centre, kills / run gold /
/// pause top-right, combo under it, ammo ring panel bottom-right (= reload button), joystick; revive offer, result
/// screens (death endless / hard, story failed / cleared), pause menu. Systems without a reference (card picker,
/// objective HUD, wave banner, tips, damage numbers, threat arrows) keep their 1080-p layouts in a ScaledRoot.
/// </summary>
public static partial class GameSceneBuilder
{
    static HUD BuildUI(Tex t, out TouchControls controls)
    {
        var canvas = KitCanvas("HUD", 10); // above world-space canvases (turret status)
        var root = canvas.transform;
        var hud = canvas.gameObject.AddComponent<HUD>();

        var vig = Img("DamageVignette", root, t.vignette, new Color(0.85f, 0.05f, 0.05f, 0f));
        Fill(vig, -4f); vig.raycastTarget = false;
        hud.damageVignette = vig;

        // ---- Legacy layer (1080-p layouts): damage numbers, threat arrows, banner, tips.
        var legacy = LegacyRoot(root);
        var dnRoot = Rect("DamageNumbers", legacy); Stretch(dnRoot);
        var dnTemplate = Txt("Number", dnRoot, 38, TextAnchor.MiddleCenter, Color.white);
        dnTemplate.font = UiFonts.Display; dnTemplate.fontSharedMaterial = UiFonts.DisplayShadow;
        Anchor(dnTemplate.rectTransform, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240f, 70f));
        dnRoot.gameObject.AddComponent<DamageNumbers>().template = dnTemplate;
        var thrRoot = Rect("Threats", legacy); Stretch(thrRoot);
        var thrTemplate = Img("Arrow", thrRoot, t.arrow, Color.white);
        Anchor(thrTemplate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
        var threats = thrRoot.gameObject.AddComponent<ThreatIndicators>();
        threats.template = thrTemplate; threats.normalRange = 11f; threats.edgeMargin = 72f;

        var bannerRoot = Rect("WaveBanner", legacy);
        Anchor(bannerRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(900f, 200f));
        var bannerGroup = bannerRoot.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false; bannerGroup.alpha = 0f;
        var banner = Txt("Title", bannerRoot, 110, TextAnchor.MiddleCenter, UiKit.Text);
        Anchor(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1200f, 130f));
        var bannerSub = Txt("Sub", bannerRoot, 38, TextAnchor.MiddleCenter, UiKit.Hazard);
        Anchor(bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(1200f, 50f));
        var breakTxt = Txt("Breather", legacy, 46, TextAnchor.UpperCenter, UiKit.Text);
        Anchor(breakTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(1200f, 120f));
        breakTxt.gameObject.SetActive(false);
        hud.waveBanner = banner; hud.waveSubBanner = bannerSub; hud.waveBannerGroup = bannerGroup; hud.breakText = breakTxt;
        var tips = Rect("Tutorial", legacy); Stretch(tips);
        var tipsGroup = tips.gameObject.AddComponent<CanvasGroup>(); tipsGroup.blocksRaycasts = false;
        var divider = Img("Divider", tips, t.white, new Color(1f, 1f, 1f, 0.3f));
        Anchor(divider.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(4f, 420f));
        var leftTip = Txt("Left", tips, 46, TextAnchor.MiddleCenter, UiKit.Text);
        Anchor(leftTip.rectTransform, new Vector2(0.25f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(800f, 200f));
        L(leftTip, "tut.left");
        var rightTip = Txt("Right", tips, 46, TextAnchor.MiddleCenter, UiKit.Text);
        Anchor(rightTip.rectTransform, new Vector2(0.75f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(800f, 200f));
        L(rightTip, "tut.right");
        hud.tutorialGroup = tipsGroup;

        // ---- Joystick + aim ring.
        var joyBase = KImg("JoystickBase", root, kit.ringThin, Color.white);
        Place(joyBase.rectTransform, Vector2.zero, 150f, 128f, 150f, 150f);
        var joyFill = KImg("Disc", joyBase.transform, kit.dot, new Color(1f, 1f, 1f, 0.08f)); Fill(joyFill, 2f);
        var joyGroup = joyBase.gameObject.AddComponent<CanvasGroup>();
        joyGroup.alpha = 0.25f; joyGroup.blocksRaycasts = false; joyGroup.interactable = false;
        var joyKnob = KImg("Knob", joyBase.transform, kit.dot, new Color(1f, 1f, 1f, 0.75f));
        Place(joyKnob.rectTransform, new Vector2(0.5f, 0.5f), 0f, 0f, 56f, 56f);
        var aimBase = KImg("AimBase", root, kit.ringThin, UiKit.Hazard);
        Place(aimBase.rectTransform, Vector2.zero, 880f, 128f, 110f, 110f);
        var aimGroup = aimBase.gameObject.AddComponent<CanvasGroup>();
        aimGroup.alpha = 0f; aimGroup.blocksRaycasts = false; aimGroup.interactable = false;
        var aimKnob = KImg("Knob", aimBase.transform, kit.dot, new Color(UiKit.Hazard.r, UiKit.Hazard.g, UiKit.Hazard.b, 0.85f));
        Place(aimKnob.rectTransform, new Vector2(0.5f, 0.5f), 0f, 0f, 44f, 44f);

        // ---- Health (top-left): heart box + bar; XP + level under it; owned cards row.
        var heartBox = KCard("HeartBox", root, 12, UiKit.Panel, UiKit.Line); TL(heartBox, Safe, 22f, 44f, 44f);
        var heart = KIcon("Heart", heartBox.transform, kit.heart, UiKit.Danger, 24f); In(heart, 10f, 10f, 24f, 24f);
        var hpBar = KCard("HealthBar", root, 12, UiKit.Hex(0x0E120C, 0.92f), UiKit.Line); TL(hpBar, 112f, 31f, 284f, 26f);
        var hpLag = KImg("Lag", hpBar.transform, kit.r12, UiKit.Hex(0x8A2A22), true, 4f);
        var hpFill = KImg("Fill", hpBar.transform, kit.r12, UiKit.Danger, true, 4f);
        foreach (var f in new[] { hpLag, hpFill })
        {
            var r = f.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0f, 0.5f);
            r.offsetMin = new Vector2(3f, 3f); r.offsetMax = new Vector2(-3f, -3f);
        }
        var hpGloss = KImg("Gloss", hpFill.transform, kit.gloss, Color.white, true, 2f); Fill(hpGloss);
        var hpText = KTxt("Value", hpBar.transform, F.Bold, 16f, UiKit.Text, TextAlignmentOptions.Center); Fill(hpText);
        hpText.text = "100 / 100";
        hud.healthFill = hpFill.rectTransform; hud.healthFillImage = hpFill; hud.healthLag = hpLag.rectTransform; hud.healthText = hpText;
        hud.healthHeart = heart.rectTransform;
        var xp = KBar("XpBar", root, UiKit.Hazard, out var xpTrough, 6f); TL(xpTrough, 112f, 62f, 230f, 6f);
        xp.anchorMax = new Vector2(0f, 1f);
        var lvl = KTxt("Level", root, F.Bold, 14f, UiKit.Hazard); TL(lvl, 350f, 55f, 80f, 20f);
        lvl.text = Loc.F("hud.level", 1);
        hud.xpFill = xp; hud.levelText = lvl;
        var row = Rect("Cards", root); TL(row, Safe, 80f, 600f, 34f);
        var badge = KCard("CardBadge", row, 12, UiKit.Panel, UiKit.Line);
        Place(badge.rectTransform, new Vector2(0f, 1f), 16f, -16f, 32f, 32f);
        var bIcon = KImg("Icon", badge.transform, ui.cards[0], Color.white); bIcon.preserveAspect = true; Fill(bIcon, 4f);
        var bLvBg = KImg("LevelBg", badge.transform, kit.dot, UiKit.Hazard); Place(bLvBg.rectTransform, new Vector2(1f, 0f), -2f, 3f, 14f, 14f);
        var bLv = KTxt("Level", bLvBg.transform, F.Display, 9f, UiKit.Ink, TextAlignmentOptions.Center); Fill(bLv); bLv.text = "1";
        var bFlash = KImg("Flash", badge.transform, kit.r12, new Color(1f, 1f, 1f, 0f), true); Fill(bFlash);
        hud.cardRow = row; hud.cardIconTemplate = badge;

        // ---- Wave pill (top-centre).
        var wave = KImg("WavePill", root, kit.pill, UiKit.Hex(0x10140E, 0.92f), true, 2f); TC(wave, 494f, 14f, 292f, 48f);
        var ws = KImg("Stroke", wave.transform, kit.pillStroke, UiKit.Line, true, 2f); Fill(ws);
        var wl = KCaps("Label", wave.transform, 12f, UiKit.Hazard, "hud.wave_label"); In(wl, 22f, 16f, 60f, 16f);
        hud.waveNumber = KTxt("Number", wave.transform, F.Display, 28f, UiKit.Text, TextAlignmentOptions.Center); In(hud.waveNumber, 78f, 6f, 40f, 36f);
        hud.waveNumber.text = "1";
        hud.waveProgress = KBar("Progress", wave.transform, UiKit.Hazard, out var wtr, 6f); In(wtr, 124f, 21f, 96f, 6f);
        hud.waveLeft = KTxt("Left", wave.transform, F.Semi, 14f, UiKit.Secondary, TextAlignmentOptions.Right); InR(hud.waveLeft, 18f, 14f, 60f, 20f);

        // ---- Kills / run gold / pause (top-right) + score under them.
        Image Pill(string name, Sprite icon, Color ic, float right, float w, out TMP_Text value)
        {
            var p = KImg(name, root, kit.pill, UiKit.Hex(0x10140E, 0.92f), true, 2f); TR(p, right, 22f, w, 40f);
            var s = KImg("Stroke", p.transform, kit.pillStroke, UiKit.Line, true, 2f); Fill(s);
            var i = KIcon("Icon", p.transform, icon, ic, 20f); In(i, 12f, 10f, 20f, 20f);
            value = KTxt("Value", p.transform, F.Display, 18f, UiKit.Text, TextAlignmentOptions.Right); Margins(value, 36f, 0f, 14f, 0f);
            value.text = "0";
            return p;
        }
        Pill("Kills", kit.skull, UiKit.Success, Safe + 140f, 96f, out hud.killsText);
        Pill("RunGold", kit.coin, Color.white, Safe + 50f, 82f, out hud.runGoldText);
        var scoreLbl = KCaps("ScoreLabel", root, 11f, UiKit.Secondary, "hud.score", TextAlignmentOptions.Right); TR(scoreLbl, Safe + 100f, 68f, 120f, 16f);
        var score = KTxt("Score", root, F.Display, 16f, UiKit.Text, TextAlignmentOptions.Right); TR(score, Safe, 66f, 96f, 20f);
        score.text = "0";
        hud.scoreText = score;

        // ---- Combo (right, under the pills).
        var comboRoot = Rect("Combo", root); TR(comboRoot, Safe, 88f, 230f, 80f);
        var ck = KCaps("Label", comboRoot, 13f, UiKit.Hazard, "hud.combo"); InR(ck, 92f, 22f, 120f, 18f);
        ((TextMeshProUGUI)ck).alignment = TextAlignmentOptions.Right;
        var mult = KTxt("Multiplier", comboRoot, F.Display, 40f, UiKit.Gold, TextAlignmentOptions.Right); InR(mult, 0f, 0f, 90f, 48f);
        mult.fontSharedMaterial = UiFonts.DisplayShadow; mult.text = "X1";
        var comboBar = KBar("Bar", comboRoot, UiKit.Hazard, out var ctr, 5f); InR(ctr, 0f, 50f, 150f, 5f);
        var comboTxt = KTxt("ComboCount", comboRoot, F.Semi, 12f, UiKit.Secondary, TextAlignmentOptions.Right); InR(comboTxt, 0f, 58f, 230f, 16f);
        comboRoot.gameObject.SetActive(false);
        var broken = KTxt("ComboBroken", root, F.Display, 34f, UiKit.DangerAlt, TextAlignmentOptions.Right); TR(broken, Safe, 92f, 120f, 44f);
        broken.gameObject.SetActive(false);
        hud.multiplierText = mult; hud.comboText = comboTxt; hud.comboRoot = comboRoot; hud.comboBar = comboBar; hud.comboBrokenText = broken;

        var fps = KTxt("FPS", root, F.Semi, 11f, new Color(1f, 1f, 1f, 0.4f), TextAlignmentOptions.Center);
        Place(fps.rectTransform, new Vector2(0.5f, 0f), 0f, 8f, 120f, 14f);
        fps.text = "-- FPS";
        hud.fpsText = fps;

        // ---- Ammo panel (bottom-right) = reload button; active-reload bar above it.
        var ammoPanel = KCard("AmmoPanel", root, 20, UiKit.Panel, UiKit.Line); BR(ammoPanel, Safe + 2f, 58f, 182f, 78f);
        var ringBg = KImg("RingBg", ammoPanel.transform, kit.ring, UiKit.Hex(0x2A3324)); In(ringBg, 14f, 13f, 52f, 52f);
        var ring = KImg("Ring", ammoPanel.transform, kit.ring, UiKit.Hazard); In(ring, 14f, 13f, 52f, 52f);
        ring.type = Image.Type.Filled; ring.fillMethod = Image.FillMethod.Radial360; ring.fillOrigin = 2; ring.fillClockwise = false; ring.fillAmount = 1f;
        hud.weaponName = KTxt("Weapon", ammoPanel.transform, F.Display, 11f, UiKit.Text, TextAlignmentOptions.Center); In(hud.weaponName, 14f, 13f, 52f, 52f);
        hud.weaponName.enableAutoSizing = true; hud.weaponName.fontSizeMin = 7f; hud.weaponName.fontSizeMax = 12f;
        var ammo = KTxt("Ammo", ammoPanel.transform, F.Display, 30f, UiKit.Text, TextAlignmentOptions.Left); In(ammo, 78f, 10f, 100f, 38f);
        ammo.text = "40<size=55%>/40</size>";
        var al = KCaps("Label", ammoPanel.transform, 11f, UiKit.Secondary, "hud.ammo_label"); In(al, 80f, 50f, 90f, 16f);
        hud.ammoText = ammo; hud.ammoPlate = ammoPanel.gameObject;
        hud.reloadButton = ammoPanel.rectTransform; hud.reloadButtonFill = ring;
        var reloadRoot = Rect("Reload", root); BR(reloadRoot, Safe + 2f, 142f, 182f, 34f);
        var reloadLabel = KTxt("Label", reloadRoot, F.Bold, 13f, UiKit.Hazard, TextAlignmentOptions.Right); InR(reloadLabel, 0f, 0f, 182f, 16f);
        var reloadBg = KImg("BarBg", reloadRoot, kit.r12, UiKit.Hex(0x10140E, 0.92f), true, 4f); InR(reloadBg, 0f, 18f, 182f, 14f);
        var sweet = KImg("SweetSpot", reloadBg.transform, kit.r12, new Color(UiKit.Hazard.r, UiKit.Hazard.g, UiKit.Hazard.b, 0.7f), true, 6f);
        sweet.rectTransform.anchorMin = new Vector2(0.4f, 0f); sweet.rectTransform.anchorMax = new Vector2(0.6f, 1f); sweet.rectTransform.offsetMin = new Vector2(0f, 2f); sweet.rectTransform.offsetMax = new Vector2(0f, -2f);
        var reloadBar = KImg("Bar", reloadBg.transform, kit.r12, Color.white, true, 8f);
        reloadBar.rectTransform.anchorMin = Vector2.zero; reloadBar.rectTransform.anchorMax = new Vector2(0f, 1f);
        reloadBar.rectTransform.offsetMin = new Vector2(2f, 4f); reloadBar.rectTransform.offsetMax = new Vector2(0f, -4f);
        reloadRoot.gameObject.SetActive(false);
        hud.reloadRoot = reloadRoot.gameObject; hud.reloadBar = reloadBar.rectTransform; hud.reloadBarImage = reloadBar;
        hud.reloadSweetSpot = sweet.rectTransform; hud.reloadLabel = reloadLabel;

        // Card picker (legacy layout, above the HUD).
        var picker = BuildCardPicker(legacy, t);

        // ---- Result screens.
        var go = Img("GameOver", root, null, new Color(0.035f, 0.04f, 0.03f, 0.95f));
        Fill(go, -4f); go.type = Image.Type.Simple; go.raycastTarget = true;
        var rs = go.gameObject.AddComponent<ResultScreen>();
        var hidden = Rect("Hidden", go.transform); Fill(hidden); hidden.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
        hud.gameOverTitle = KTxt("Title", hidden, F.Bold, 10f, Color.white);
        hud.gameOverStats = KTxt("Stats", hidden, F.Bold, 10f, Color.white);
        hud.recordText = KTxt("Record", hidden, F.Bold, 10f, Color.white);
        hud.recordText.gameObject.SetActive(false);
        var restart = KButton("RestartButton", go.transform, UiStyle.Yellow, "over.again", 266f, 72f, 34f, kit.refresh, true, 20);
        hud.restartButton = (RectTransform)restart.transform;
        BuildDeathLayout(go.transform, rs, hud);
        BuildStoryLayout(go.transform, rs, hud);
        hud.result = rs;
        rs.closeIcon = kit.close; rs.checkIcon = kit.check;
        go.gameObject.SetActive(false);
        hud.gameOverPanel = go.gameObject;

        controls = canvas.gameObject.AddComponent<TouchControls>();
        controls.canvas = canvas;
        controls.joyBase = joyBase.rectTransform; controls.joyKnob = joyKnob.rectTransform; controls.joyGroup = joyGroup;
        controls.aimBase = aimBase.rectTransform; controls.aimKnob = aimKnob.rectTransform; controls.aimGroup = aimGroup;
        controls.joyRadius = 60f; controls.joyIdlePos = new Vector2(150f, 128f);
        controls.reloadButton = ammoPanel.rectTransform;
        controls.restartButton = hud.restartButton;
        controls.cardRects = picker.cards;
        return hud;
    }

    static RectTransform ResultBody(Transform parent, string name) => Margins(Rect(name, parent), Safe, 36f, Safe, 38f);

    static UiText StatCard(Transform parent, float refW, float x0, float x1, float y, string key, Color valueColor)
    {
        var c = KCard("Stat_" + key, parent, 20, UiKit.Card, UiKit.Line);
        Prop(c, refW, x0, x1, y, 75f);
        var l = KCaps("Label", c.transform, 12f, UiKit.Secondary, key); In(l, 18f, 14f, 220f, 16f);
        var v = KTxt("Value", c.transform, F.Display, 28f, valueColor); In(v, 18f, 32f, 220f, 36f);
        return v;
    }

    static void BuildDeathLayout(Transform go, ResultScreen rs, HUD hud)
    {
        const float W = 1168f;
        var body = ResultBody(go, "Death");
        rs.deathLayout = body.gameObject;
        var tag = KChip("Mode", body, null, UiKit.Hex(0x1B2A12), UiKit.Success, 24f, 12f);
        rs.modeTag = tag; rs.modeTagBg = tag.transform.parent.GetComponent<Image>();
        In(rs.modeTagBg, 0f, 0f, 100f, 24f);
        var fell = KTxt("Fell", body, F.Display, 40f, UiKit.Secondary, TextAlignmentOptions.Left, "over.fell"); In(fell, 0f, 30f, 560f, 50f);
        rs.waveBig = KTxt("Wave", body, F.Display, 80f, UiKit.Gold, TextAlignmentOptions.Left); In(rs.waveBig, 0f, 80f, 600f, 96f);
        rs.waveBig.fontSharedMaterial = UiFonts.DisplayShadow;
        var rec = KChip("NewRecord", body, "over.record", UiKit.Success, UiKit.Ink, 30f, 14f);
        In(rec.transform.parent.GetComponent<Image>(), 0f, 172f, 120f, 30f);
        rs.newRecord = rec.transform.parent.gameObject;
        rs.prevRecord = KTxt("Prev", body, F.Semi, 15f, UiKit.Secondary); In(rs.prevRecord, 150f, 176f, 300f, 22f);
        rs.statKills = StatCard(body, W, 0f, 274f, 218f, "over.kills", UiKit.Text);
        rs.statBosses = StatCard(body, W, 286f, 558f, 218f, "over.bosses", UiKit.Text);
        rs.statCombo = StatCard(body, W, 0f, 274f, 304f, "over.best_combo", UiKit.Gold);
        rs.statTime = StatCard(body, W, 286f, 558f, 304f, "over.time", UiKit.Text);

        // Earnings + quest progress (right).
        var panel = KModal("Earnings", body);
        Prop(panel, W, 604f, 1168f, 0f, 398f);
        panel.raycastTarget = false;
        const float PW = 564f;
        var kl = KCaps("Label", panel.transform, 13f, UiKit.Hazard, "over.earned"); In(kl, 30f, 28f, 300f, 18f);
        var coin = KIcon("Coin", panel.transform, kit.coin, Color.white, 44f); In(coin, 30f, 58f, 44f, 44f);
        var rw = Rect("RunRewards", go);
        var gold = KTxt("Gold", panel.transform, F.Display, 44f, UiKit.Text); Prop(gold, PW, 86f, 330f, 54f, 52f);
        var dbl = KButton("Double", panel.transform, UiStyle.Green, null, 190f, 56f, 18f, kit.film);
        Prop(dbl, PW, 344f, 534f, 50f, 56f);
        var hard = KChip("HardBonus", panel.transform, "over.hard_bonus", UiKit.Hex(0x3A1512), UiKit.DangerAlt, 22f, 11f);
        In(hard.transform.parent.GetComponent<Image>(), 30f, 116f, 160f, 22f);
        rs.hardBonus = hard.transform.parent.gameObject;
        var msg = KTxt("Message", panel.transform, F.Bold, 14f, UiKit.DangerAlt, TextAlignmentOptions.Right); Prop(msg, PW, 300f, 534f, 112f, 20f);
        Divider(panel.transform, 148f, 30f);
        var ql = KCaps("QuestLabel", panel.transform, 13f, UiKit.Hazard, "over.quest_progress"); In(ql, 30f, 168f, 300f, 18f);
        rs.questRows = new GameObject[3]; rs.questTitles = new TMP_Text[3]; rs.questStates = new TMP_Text[3]; rs.questChecks = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            float y = 204f + i * 52f;
            var r = Rect("Quest" + i, panel.transform); Prop(r, PW, 30f, 534f, y, 32f);
            var ring = KImg("Ring", r, kit.ringThin, UiKit.Line2); Place(ring.rectTransform, new Vector2(0f, 0.5f), 15f, 0f, 30f, 30f);
            var fill = KImg("Fill", ring.transform, kit.dot, UiKit.Success); Fill(fill, 1f);
            var ci = KIcon("Check", fill.transform, kit.check, UiKit.Ink, 16f); Fill(ci, 7f);
            rs.questChecks[i] = fill;
            rs.questTitles[i] = KTxt("Title", r, F.Bold, 17f, UiKit.Text); Margins(rs.questTitles[i], 44f, 0f, 120f, 0f);
            rs.questStates[i] = KTxt("State", r, F.Bold, 15f, UiKit.Success, TextAlignmentOptions.Right); Margins(rs.questStates[i], 300f, 0f, 0f, 0f);
            rs.questRows[i] = r.gameObject;
        }

        var menu = KButton("Menu", body, UiStyle.Dark, "over.menu", 122f, 72f, 19f, null, false, 20); Prop(menu, W, 604f, 726f, 442f, 72f);
        var weapon = KButton("Weapon", body, UiStyle.Dark, "over.weapon", 150f, 72f, 19f, null, false, 20); Prop(weapon, W, 738f, 889f, 442f, 72f);
        var slot = Rect("RestartSlot", body); Prop(slot, W, 902f, 1168f, 442f, 72f);
        rs.deathRestartSlot = slot; rs.weaponButton = weapon;
        hud.restartButton.SetParent(slot, false); Fill(hud.restartButton);
        // Run rewards (gold count-up, 2× ad) + the revive offer.
        var rr = rw.gameObject.AddComponent<RunRewards>();
        Fill(rw);
        rr.root = rw.gameObject; rr.goldText = gold; rr.doubleButton = dbl; rr.doubleLabel = LabelOf(dbl); rr.message = msg; rr.hardTag = rs.hardBonus;
        rewardsHost = rr;
        menuButtonHost = menu;
    }

    static RunRewards rewardsHost;
    static Button menuButtonHost;

    static void BuildStoryLayout(Transform go, ResultScreen rs, HUD hud)
    {
        const float W = 1168f;
        var body = ResultBody(go, "Story");
        rs.storyLayout = body.gameObject;
        rs.storyKicker = KCaps("Kicker", body, 14f, UiKit.Hazard); In(rs.storyKicker, 0f, 22f, 500f, 20f);
        rs.storyTitle = KTxt("Title", body, F.Display, 52f, UiKit.Text); In(rs.storyTitle, 0f, 46f, 640f, 130f);
        rs.storyTitle.fontSharedMaterial = UiFonts.DisplayShadow; rs.storyTitle.lineSpacing = -14f;
        rs.storyTitle.textWrappingMode = TextWrappingModes.Normal;
        rs.storyTitle.alignment = TextAlignmentOptions.TopLeft;
        rs.storyChapter = KTxt("Chapter", body, F.Semi, 22f, UiKit.Secondary); In(rs.storyChapter, 0f, 180f, 560f, 28f);
        rs.storyChapter.fontStyle = FontStyles.Italic;
        rs.storyGoal = KTxt("Goal", body, F.Bold, 15f, UiKit.Text); Prop(rs.storyGoal, W, 0f, 400f, 222f, 20f);
        rs.storyPercent = KTxt("Percent", body, F.Bold, 15f, UiKit.Text, TextAlignmentOptions.Right); Prop(rs.storyPercent, W, 380f, 460f, 222f, 20f);
        rs.storyBar = KBar("Bar", body, UiKit.Hazard, out var btr, 8f); Prop(btr, W, 0f, 460f, 246f, 8f);
        rs.storyStars = new Image[3];
        for (int i = 0; i < 3; i++) { var s = KIcon("Star" + i, body, kit.starLine, UiKit.Line2, 30f); In(s, i * 42f, 274f, 30f, 30f); rs.storyStars[i] = s; }
        rs.storyStarsHint = KTxt("StarsHint", body, F.Semi, 15f, UiKit.Secondary); In(rs.storyStarsHint, 140f, 278f, 420f, 22f);
        hud.victoryStars = rs.storyStars;
        int n = Campaign.Count;
        rs.nodes = new Image[n]; rs.nodeLabels = new TMP_Text[n]; rs.nodeLines = new Image[n - 1];
        for (int i = 0; i < n; i++)
        {
            if (i < n - 1)
            {
                var line = KImg("Line" + i, body, ui.flat, UiKit.Line); line.type = Image.Type.Simple;
                In(line, i * 82f + 30f, 345f, 56f, 4f); rs.nodeLines[i] = line;
            }
            var node = KImg("Node" + i, body, kit.dot, UiKit.Card); In(node, i * 82f, 330f, 34f, 34f);
            var ns = KImg("Stroke", node.transform, kit.ringThin, UiKit.Line); Fill(ns);
            rs.nodes[i] = node;
            rs.nodeLabels[i] = KTxt("N", node.transform, F.Display, 15f, UiKit.Text, TextAlignmentOptions.Center); Fill(rs.nodeLabels[i]);
        }
        // Right column.
        var tip = KCard("Tip", body, 20, UiKit.Panel, UiKit.Line); Prop(tip, W, 720f, 1150f, 30f, 80f);
        var tl = KCaps("Label", tip.transform, 12f, UiKit.Hazard, "menu.tip"); In(tl, 24f, 14f, 200f, 16f);
        rs.storyTip = KPara("Text", tip.transform, 16f, UiKit.Text); Margins(rs.storyTip, 24f, 34f, 20f, 6f);
        rs.storyTip.font = UiFonts.Bold;
        var gc = KCard("Gold", body, 20, UiKit.Panel, UiKit.Line); Prop(gc, W, 720f, 1150f, 122f, 60f);
        var gi = KIcon("Coin", gc.transform, kit.coin, Color.white, 32f); In(gi, 24f, 14f, 32f, 32f);
        rs.storyGold = KTxt("Text", gc.transform, F.Semi, 14f, UiKit.Secondary); Margins(rs.storyGold, 70f, 0f, 16f, 0f);
        var slot = Rect("RestartSlot", body); Prop(slot, W, 720f, 1150f, 336f, 80f);
        rs.storyRestartSlot = slot;
        rs.mapButton = KButton("Map", body, UiStyle.Dark, "over.map", 200f, 56f, 18f, kit.map, false, 20); Prop(rs.mapButton, W, 720f, 929f, 428f, 56f);
        rs.weaponButton2 = KButton("Weapon", body, UiStyle.Dark, "over.change_weapon", 210f, 56f, 18f, null, false, 20); Prop(rs.weaponButton2, W, 941f, 1150f, 428f, 56f);
        body.gameObject.SetActive(false);
    }

    /// <summary>Full-screen "YERE DÜŞTÜN" continue offer (rewarded ad / gems, countdown).</summary>
    static void BuildRevive(Transform root, RunRewards rr)
    {
        var p = Img("Revive", rr.root.transform, null, new Color(0.17f, 0.03f, 0.025f, 0.88f));
        Fill(p, -4f); p.type = Image.Type.Simple; p.raycastTarget = true;
        var v = KImg("Vignette", p.transform, kit.soft, new Color(0f, 0f, 0f, 0f)); Fill(v);
        var title = KTxt("Title", p.transform, F.Display, 56f, UiKit.Text, TextAlignmentOptions.Center, "rew.revive_title");
        title.fontSharedMaterial = UiFonts.DisplayShadow; CC(title, 240f, 40f, 800f, 70f);
        rr.reviveSub = KTxt("Sub", p.transform, F.Bold, 18f, UiKit.Secondary, TextAlignmentOptions.Center); CC(rr.reviveSub, 340f, 106f, 600f, 24f);
        var disc = KImg("Disc", p.transform, kit.dot, UiKit.Hex(0x14110C)); CC(disc, 576f, 182f, 128f, 128f);
        var track = KImg("Track", p.transform, kit.ring, UiKit.Hex(0x3A2C10)); CC(track, 568f, 174f, 144f, 144f);
        rr.reviveRing = KImg("Ring", p.transform, kit.ring, UiKit.Hazard); CC(rr.reviveRing, 568f, 174f, 144f, 144f);
        rr.reviveRing.type = Image.Type.Filled; rr.reviveRing.fillMethod = Image.FillMethod.Radial360; rr.reviveRing.fillOrigin = 2; rr.reviveRing.fillClockwise = false;
        rr.reviveCount = KTxt("Count", p.transform, F.Display, 56f, UiKit.Gold, TextAlignmentOptions.Center); CC(rr.reviveCount, 568f, 200f, 144f, 90f);
        rr.continueAdButton = KButton("ContinueAd", p.transform, UiStyle.Green, null, 400f, 76f, 24f, kit.film, false, 20); CC(rr.continueAdButton, 440f, 352f, 400f, 76f);
        rr.continueAdLabel = LabelOf(rr.continueAdButton);
        var note = KTxt("Note", p.transform, F.Semi, 15f, UiKit.Secondary, TextAlignmentOptions.Center, "rew.revive_note"); CC(note, 340f, 440f, 600f, 22f);
        rr.continueGemButton = KButton("ContinueGems", p.transform, UiStyle.Dark, null, 260f, 44f, 17f, kit.gem); CC(rr.continueGemButton, 510f, 472f, 260f, 44f);
        KeepIcon(rr.continueGemButton);
        rr.continueGemLabel = LabelOf(rr.continueGemButton);
        rr.reviveSkip = KButton("Skip", p.transform, UiStyle.Ghost, "rew.revive_skip", 160f, 34f, 17f); CC(rr.reviveSkip, 560f, 530f, 160f, 34f);
        LabelOf(rr.reviveSkip).fontStyle = FontStyles.Underline;
        rr.revivePanel = p.gameObject;
    }

    static void BuildPauseUi(HUD hud, TouchControls controls)
    {
        var root = hud.transform;
        var pb = KIconButton("PauseButton", root, kit.pause, 42f);
        TR(pb, Safe + 2f, 22f, 42f, 42f);
        var panel = KDim("PausePanel", root, 0.8f);
        var box = KModal("Box", panel.transform); CC(box, 430f, 120f, 420f, 350f);
        var title = KTxt("Title", box.transform, F.Display, 34f, UiKit.Text, TextAlignmentOptions.Center, "pause.title"); In(title, 20f, 34f, 380f, 44f);
        var resume = KButton("Resume", box.transform, UiStyle.Yellow, "pause.resume", 340f, 68f, 30f, kit.play, true, 20); In(resume, 40f, 104f, 340f, 68f);
        var set = KButton("Settings", box.transform, UiStyle.Dark, "menu.settings", 340f, 54f, 19f, kit.gear); In(set, 40f, 190f, 340f, 54f);
        var menu = KButton("Menu", box.transform, UiStyle.Dark, "pause.menu", 340f, 54f, 19f, kit.back); In(menu, 40f, 260f, 340f, 54f);
        var sp = BuildSettingsPanel(root);
        var host = new GameObject("PauseMenu").AddComponent<PauseMenu>();
        host.transform.SetParent(root, false);
        host.pauseButton = pb; host.resume = resume; host.settings = set; host.mainMenu = menu; host.gameOverMenu = menuButtonHost;
        host.panel = panel.gameObject; host.settingsPanel = sp;

        // Run rewards + revive (above the result screen), result-screen buttons.
        var rr = rewardsHost;
        rr.transform.SetParent(root, false);
        rr.transform.SetAsLastSibling();
        BuildRevive(root, rr);
        panel.transform.SetAsLastSibling();
        sp.root.transform.SetAsLastSibling();
        var rs = hud.result;
        controls.uiButtons = new[]
        {
            (RectTransform)pb.transform, (RectTransform)menuButtonHost.transform, (RectTransform)rs.weaponButton.transform,
            (RectTransform)rs.mapButton.transform, (RectTransform)rs.weaponButton2.transform, (RectTransform)rr.doubleButton.transform,
            (RectTransform)rr.continueAdButton.transform, (RectTransform)rr.continueGemButton.transform, (RectTransform)rr.reviveSkip.transform,
        };
        BuildEventSystem();
    }

    static void BuildBossHud(HUD hud)
    {
        var root = hud.transform;
        var legacy = root.Find("Legacy");
        var host = Rect("Boss", root); Fill(host);
        host.SetSiblingIndex(legacy.GetSiblingIndex());
        var bh = host.gameObject.AddComponent<BossHUD>();
        var bar = Rect("Bar", host); TC(bar, 410f, 70f, 460f, 24f);
        var bg = bar.gameObject.AddComponent<CanvasGroup>(); bg.blocksRaycasts = false; bg.interactable = false;
        var skull = KIcon("Skull", bar, kit.skull, UiKit.DangerAlt, 20f); In(skull, 0f, 2f, 20f, 20f);
        var name = KTxt("Name", bar, F.Display, 16f, UiKit.DangerAlt, TextAlignmentOptions.Left); In(name, 26f, 0f, 120f, 24f);
        name.enableAutoSizing = true; name.fontSizeMin = 10f; name.fontSizeMax = 16f;
        var frame = KCard("Frame", bar, 12, UiKit.Hex(0x0E120C, 0.92f), UiKit.Line); In(frame, 132f, 3f, 328f, 18f);
        var lag = KImg("Lag", frame.transform, kit.r12, new Color(1f, 1f, 1f, 0.85f), true, 6f);
        var fill = KImg("Fill", frame.transform, kit.r12, UiKit.Purple, true, 6f);
        foreach (var f in new[] { lag, fill })
        {
            var r = f.rectTransform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0f, 0.5f);
            r.offsetMin = new Vector2(2f, 2f); r.offsetMax = new Vector2(-2f, -2f);
        }
        var weak = KCaps("Weak", bar, 13f, UiKit.Gold, "boss.weak", TextAlignmentOptions.Right); InR(weak, 0f, 24f, 160f, 18f);
        bh.barGroup = bg; bh.barFill = fill.rectTransform; bh.barLag = lag.rectTransform; bh.barFillImage = fill;
        bh.barName = name; bh.weakTag = weak;

        // Intro title (legacy layer, centre).
        var intro = Rect("BossIntro", legacy);
        Anchor(intro, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1400f, 260f));
        var ig = intro.gameObject.AddComponent<CanvasGroup>(); ig.blocksRaycasts = false; ig.interactable = false;
        var title = Txt("Title", intro, 140, TextAnchor.MiddleCenter, UiKit.DangerAlt);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1400f, 180f));
        var sub = Txt("Sub", intro, 40, TextAnchor.MiddleCenter, UiKit.Text);
        Anchor(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1400f, 60f));
        bh.introGroup = ig; bh.introTitle = title; bh.introSub = sub;
    }
}
