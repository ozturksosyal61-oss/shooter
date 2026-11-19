using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Redesigned menu screens (docs/ui-ref 01–10): main menu, game modes, weapon select, shop (weapons / characters /
/// bonuses / gems), daily reward, quests, settings, chapter select, co-op room, loading screen. Built at 1280×590;
/// multi-column screens use proportional anchors so 16:9 phones fit too (19.5:9 matches the references 1:1).
/// </summary>
public static partial class GameSceneBuilder
{
    /// <summary>Horizontal anchors as a fraction of a parent designed <paramref name="refW"/> wide; y from the top, fixed height.</summary>
    static RectTransform Prop(Component c, float refW, float x0, float x1, float y, float h)
    {
        var r = (RectTransform)c.transform;
        r.anchorMin = new Vector2(x0 / refW, 1f); r.anchorMax = new Vector2(x1 / refW, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(0f, h);
        r.anchoredPosition = new Vector2(0f, -y);
        return r;
    }

    /// <summary>Stretch inside the parent with fixed margins (left, top, right, bottom).</summary>
    static RectTransform Margins(Component c, float l, float t, float rgt, float b)
    {
        var r = (RectTransform)c.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rgt, -t);
        return r;
    }

    static RectTransform BodyRect(Transform parent, float top, float bottom, string name = "Body") => Margins(Rect(name, parent), Safe, top, Safe, bottom);

    static Image Divider(Transform parent, float y, float inset)
    {
        var d = KImg("Divider", parent, ui.flat, UiKit.Line);
        var r = d.rectTransform;
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f); r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(-inset * 2f, 1f); r.anchoredPosition = new Vector2(0f, -y);
        d.type = Image.Type.Simple;
        return d;
    }

    static void Pills(Transform root, bool plus, float y, float h, float w, out TMP_Text gold, out TMP_Text gems, out Button goldPlus, out Button gemPlus, float right = Safe)
    {
        goldPlus = gemPlus = null;
        if (plus)
        {
            gems = KCurrencyPlus("Gems", root, kit.gem, w, h, out gemPlus);
            gold = KCurrencyPlus("Gold", root, kit.coin, w, h, out goldPlus);
        }
        else
        {
            gems = KCurrency("Gems", root, kit.gem, w, h, out _);
            gold = KCurrency("Gold", root, kit.coin, w, h, out _);
        }
        TR(gems.transform.parent.GetComponent<Image>(), right, y, w, h);
        TR(gold.transform.parent.GetComponent<Image>(), right + w + 12f, y, w, h);
    }

    /// <summary>Full-screen menu page: opaque-ish backdrop that blocks clicks to the menu behind it.</summary>
    static Image Page(string name, Transform root, float alpha = 0.92f)
    {
        var p = Img(name, root, null, new Color(0.045f, 0.05f, 0.035f, alpha));
        Fill(p, -4f); p.type = Image.Type.Simple; p.raycastTarget = true;
        return p;
    }

    static Button CardButton(Image body)
    {
        body.raycastTarget = true;
        var b = body.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = body;
        return b;
    }

    static Image SelectedFrame(Transform card, int radius)
    {
        // Thick yellow outline (two strokes) — a glow would have to sit behind the card.
        var s = KImg("Selected", card, radius <= 12 ? kit.r12Stroke : kit.r20Stroke, UiKit.Hazard, true, 1f);
        Fill(s);
        var s2 = KImg("Inner", s.transform, radius <= 12 ? kit.r12Stroke : kit.r20Stroke, UiKit.Hazard, true, 1f);
        Fill(s2, 1.2f);
        return s;
    }

    static RawImage Preview(string name, Transform parent, Texture rt)
    {
        var r = new GameObject(name, typeof(RectTransform)).AddComponent<RawImage>();
        r.transform.SetParent(parent, false);
        r.texture = rt;
        r.raycastTarget = false;
        return r;
    }

    // ================================================================ menu scene
    static void BuildMenuUi(Mats m)
    {
        var c = KitCanvas("Menu", 10);
        var root = c.transform;
        KBackdrop(root, 0.5f);
        var mm = c.gameObject.AddComponent<MainMenu>();
        var stage = BuildPreviewStage(m, out var rt);
        stage.cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // composited over the live city
        mm.preview = stage;

        var main = Rect("MainButtons", root); Fill(main);
        mm.mainButtons = main.gameObject;

        // Logo.
        var logo = KTxt("Logo", main, F.Display, 64f, UiKit.Text, TextAlignmentOptions.TopLeft);
        logo.text = "HOLDOUT"; logo.fontSharedMaterial = UiFonts.DisplayShadow;
        TL(logo, Safe, 26f, 520f, 76f);
        mm.logo = logo.rectTransform;
        var hz = KHazard("Hazard", main, 46f, 9f); TL(hz, Safe, 104f, 46f, 9f);
        var lc = KCaps("City", main, 16f, UiKit.Hazard, "menu.tagline"); lc.characterSpacing = 70f;
        TL(lc, 114f, 96f, 400f, 24f);

        // Quests / daily / shop list.
        Button Row(string name, float y, Sprite icon, string titleKey, out TMP_Text sub, out RectTransform right)
        {
            var body = KCard(name, main, 12, UiKit.Hex(0x10140E, 0.94f), UiKit.Line);
            TL(body, Safe, y, 276f, 70f);
            var b = CardButton(body);
            var tile = KCard("Tile", body.transform, 12, UiKit.Hex(0x221C0C), UiKit.Hex(0x4A3A10));
            In(tile, 12f, 12f, 46f, 46f);
            var ic = KIcon("Icon", tile.transform, icon, UiKit.Hazard, 24f); In(ic, 11f, 11f, 24f, 24f);
            var t = KTxt("Title", body.transform, F.Bold, 22f, UiKit.Text, TextAlignmentOptions.Left, titleKey); In(t, 72f, 10f, 180f, 28f);
            sub = KTxt("Sub", body.transform, F.Semi, 15f, UiKit.Secondary); In(sub, 72f, 38f, 190f, 22f);
            right = Rect("Right", body.transform); InR(right, 14f, 23f, 40f, 24f);
            return b;
        }
        mm.questsButton = Row("Quests", 179f, kit.clipboard, "menu.quests", out mm.questSub, out var qr);
        var badge = KImg("Badge", qr, kit.dot, UiKit.Danger); Margins(badge, 9f, 0f, 7f, 0f);
        badge.rectTransform.sizeDelta = new Vector2(24f, 24f); badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        badge.rectTransform.anchoredPosition = new Vector2(-12f, 0f);
        mm.questBadgeText = KTxt("Count", badge.transform, F.Display, 13f, UiKit.Text, TextAlignmentOptions.Center); Fill(mm.questBadgeText);
        mm.questBadge = badge.gameObject;
        mm.loginButton = Row("Daily", 262f, kit.gift, "menu.daily", out mm.loginSub, out var lr);
        mm.loginTimer = KTxt("Timer", lr, F.Bold, 16f, UiKit.Text, TextAlignmentOptions.Right); Margins(mm.loginTimer, -40f, 0f, 0f, 0f);
        var ldot = KImg("Badge", mm.loginButton.transform.Find("Tile"), kit.dot, UiKit.Danger); InR(ldot, -4f, -4f, 12f, 12f);
        mm.loginBadge = ldot.gameObject;
        mm.shop = Row("Shop", 346f, kit.bag, "menu.shop", out var shopSub, out var sr);
        L((UiText)shopSub, "menu.shop_sub");
        var chev = KIcon("Chevron", sr, kit.chevronR, UiKit.Secondary, 20f); Margins(chev, 20f, 2f, 0f, 2f);

        // Soldier (live preview of the selected character).
        var platform = KImg("Platform", main, kit.ringThin, new Color(UiKit.Hazard.r, UiKit.Hazard.g, UiKit.Hazard.b, 0.55f));
        CC(platform, 470f, 398f, 340f, 92f);
        var pglow = KImg("PlatformGlow", main, kit.soft, new Color(UiKit.Hazard.r, UiKit.Hazard.g, UiKit.Hazard.b, 0.18f));
        CC(pglow, 480f, 400f, 320f, 90f);
        mm.soldierPreview = Preview("Soldier", main, rt);
        CC(mm.soldierPreview, 450f, 70f, 380f, 380f);
        mm.characterName = KTxt("Name", main, F.Display, 24f, UiKit.Text, TextAlignmentOptions.Center); CC(mm.characterName, 440f, 492f, 400f, 32f);
        var chips = Rect("Chips", main); CC(chips, 400f, 528f, 480f, 30f);
        var ch = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
        ch.childAlignment = TextAnchor.MiddleCenter; ch.spacing = 12f; ch.childControlWidth = true; ch.childControlHeight = true; ch.childForceExpandWidth = false;
        UiText Chip(string name, Sprite icon)
        {
            var b = KImg(name, chips, kit.pill, UiKit.Hex(0x0E120C, 0.92f), true, 4f);
            var s = KImg("Stroke", b.transform, kit.pillStroke, UiKit.Line, true, 4f); Fill(s);
            s.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var hl = b.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(14, 14, 0, 0); hl.spacing = 6f; hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false;
            if (icon) { var ic = KImg("Icon", b.transform, icon, UiKit.Hazard); ic.preserveAspect = true; var le = ic.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.preferredHeight = 16f; }
            var t = KTxt("Text", b.transform, F.Bold, 16f, UiKit.Text, TextAlignmentOptions.Center);
            return t;
        }
        mm.costumeChip = Chip("Costume", null);
        mm.weaponChip = Chip("Weapon", kit.crosshair);
        mm.weaponChip.color = UiKit.Hazard;

        // OYNA → game-mode screen (Story / Endless / Hard / Co-op).
        mm.play = KButton("Play", main, UiStyle.Yellow, "menu.play", 360f, 90f, 44f, kit.play, true, 20);
        TR(mm.play, Safe, 466f, 360f, 90f);

        // Tip.
        var tipChip = KChip("TipChip", main, "menu.tip", UiKit.Hex(0x2A2210), UiKit.Hazard, 22f, 12f);
        BL(tipChip.transform.parent.GetComponent<Image>(), Safe, 30f, 60f, 22f);
        mm.tip = KTxt("Tip", main, F.Semi, 16f, UiKit.Secondary); BL(mm.tip, 128f, 28f, 420f, 26f);

        // Currency + settings.
        Pills(main, true, 24f, 48f, 152f, out mm.gold, out mm.gems, out mm.goldPlus, out mm.gemPlus, Safe + 62f);
        mm.settings = KIconButton("Settings", main, kit.gear, 48f); TR(mm.settings, Safe, 24f, 48f, 48f);

        // Panels (order = draw order).
        mm.modePanel = BuildModePanel(root, mm);
        mm.shopUI = BuildShop(root, stage, rt);
        mm.loadout = BuildLoadout(root, mm.shopUI, stage, rt);
        mm.campaignPanel = BuildCampaign(root, mm.loadout);
        mm.loadout.root.transform.SetAsLastSibling();
        mm.shopUI.root.transform.SetAsLastSibling();
        mm.coopPanel = BuildCoopPanel(root);
        mm.login = BuildLogin(root);
        mm.quests = BuildQuests(root);
        mm.settingsPanel = BuildSettingsPanel(root);

        // Toast.
        var toast = KImg("Toast", root, kit.pill, UiKit.Hex(0x10140E, 0.96f), true, 2f);
        Place(toast.rectTransform, new Vector2(0.5f, 0f), 0f, 70f, 520f, 52f);
        var ts = KImg("Stroke", toast.transform, kit.pillStroke, UiKit.Hazard, true, 2f); Fill(ts);
        mm.toast = toast.gameObject.AddComponent<CanvasGroup>();
        mm.toast.blocksRaycasts = false;
        mm.toastText = KTxt("Text", toast.transform, F.Bold, 20f, UiKit.Gold, TextAlignmentOptions.Center); Fill(mm.toastText);

        // OZT Digital splash (once per session, after Unity's).
        var spl = Img("Splash", root, null, UiKit.Ground);
        Fill(spl, -4f); spl.type = Image.Type.Simple; spl.raycastTarget = true;
        mm.splash = spl.gameObject.AddComponent<CanvasGroup>();
        var sl = Rect("Logo", spl.transform); CC(sl, 240f, 220f, 800f, 150f);
        sl.gameObject.AddComponent<CanvasGroup>();
        var oz = KTxt("Name", sl, F.Display, 64f, UiKit.Text, TextAlignmentOptions.Center, "menu.splash"); In(oz, 0f, 0f, 800f, 90f);
        var ozs = KCaps("Sub", sl, 16f, UiKit.Hazard, "menu.splash_sub", TextAlignmentOptions.Center); ozs.characterSpacing = 40f; In(ozs, 0f, 96f, 800f, 26f);
        mm.splashLogo = sl;
    }

    // ================================================================ game modes
    static ModePanel BuildModePanel(Transform root, MainMenu mm)
    {
        var page = Page("ModePanel", root);
        var mp = page.gameObject.AddComponent<ModePanel>();
        mp.root = page.gameObject; mp.menu = mm;
        mp.back = KHeader(page.transform, "mode.title", "mode.sub", out _, out _);
        Pills(page.transform, false, 28f, 44f, 104f, out mp.gold, out mp.gems, out _, out _);
        var body = BodyRect(page.transform, 108f, 38f);
        string[] titles = { "menu.story", "menu.endless", "menu.hard", "menu.coop" };
        string[] descs = { "mode.story_desc", "mode.endless_desc", "mode.hard_desc", "mode.coop_desc" };
        Color[] tints = { UiKit.Hazard, UiKit.Success, UiKit.Danger, UiKit.Gem };
        Sprite[] icons = { kit.map, kit.infinity, kit.flame, kit.user };
        const int NC = 4;
        const float CW = 274f, CG = 24f; // 4 × 274 + 3 × 24 = 1168
        mp.cards = new Button[NC]; mp.starts = new Button[NC]; mp.selectedFrames = new GameObject[NC];
        for (int i = 0; i < NC; i++)
        {
            var card = KCard("Card" + i, body, 20, UiKit.Card, UiKit.Line);
            Prop(card, 1168f, i * (CW + CG), i * (CW + CG) + CW, 0f, 444f);
            mp.cards[i] = CardButton(card);
            var art = KImg("Art", card.transform, kit.r20, new Color(tints[i].r * 0.13f, tints[i].g * 0.13f, tints[i].b * 0.1f, 1f), true);
            Margins(art, 1.5f, 1.5f, 1.5f, 0f); art.rectTransform.anchorMin = new Vector2(0f, 1f); art.rectTransform.sizeDelta = new Vector2(-3f, 170f);
            art.rectTransform.pivot = new Vector2(0.5f, 1f); art.rectTransform.anchoredPosition = new Vector2(0f, -1.5f);
            art.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var stripes = KImg("Stripes", art.transform, kit.stripes, new Color(tints[i].r, tints[i].g, tints[i].b, 0.035f));
            stripes.type = Image.Type.Tiled; Fill(stripes);
            var glow = KImg("Glow", art.transform, kit.soft, new Color(tints[i].r, tints[i].g, tints[i].b, 0.09f)); Fill(glow, -40f);
            var cover = KImg("Cover", card.transform, ui.flat, UiKit.Card);
            cover.type = Image.Type.Simple;
            Margins(cover, 1.5f, 150f, 1.5f, 0f); cover.rectTransform.anchorMin = new Vector2(0f, 1f); cover.rectTransform.anchorMax = new Vector2(1f, 1f);
            cover.rectTransform.pivot = new Vector2(0.5f, 1f); cover.rectTransform.sizeDelta = new Vector2(-3f, 22f); cover.rectTransform.anchoredPosition = new Vector2(0f, -150f);
            var ic = KIcon("Icon", card.transform, icons[i], tints[i], 64f);
            var ir = ic.rectTransform; ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 1f); ir.anchoredPosition = new Vector2(0f, -76f);
            var title = KTxt("Title", card.transform, F.Display, 30f, UiKit.Text, TextAlignmentOptions.Left, titles[i]); Prop(title, CW, 22f, CW - 22f, 176f, 38f);
            var desc = KPara("Desc", card.transform, 16f, UiKit.Secondary, descs[i]); Prop(desc, CW, 22f, CW - 22f, 220f, 90f);
            var start = KButton("Start", card.transform, i == 1 ? UiStyle.Yellow : UiStyle.Dark, "mode.start", 300f, 54f, 18f);
            Prop(start, CW, 22f, CW - 22f, 372f, 54f);
            mp.starts[i] = start;
            var sel = SelectedFrame(card.transform, 20);
            mp.selectedFrames[i] = sel.gameObject;
            sel.gameObject.SetActive(i == 1);
            if (i == 0)
            {
                mp.storyTag = KChip("Tag", card.transform, null, UiKit.Hex(0x0B0D09, 0.85f), UiKit.Hazard);
                In(mp.storyTag.transform.parent.GetComponent<Image>(), 16f, 16f, 80f, 22f);
                mp.storyProgress = KTxt("Progress", card.transform, F.Bold, 16f, UiKit.Text); Prop(mp.storyProgress, CW, 22f, CW * 0.6f, 338f, 24f);
                mp.storyStars = KTxt("Stars", card.transform, F.Bold, 16f, UiKit.Text, TextAlignmentOptions.Right); Prop(mp.storyStars, CW, CW * 0.6f, CW - 22f, 338f, 24f);
                var st = KIcon("Star", card.transform, kit.star, UiKit.Gold, 18f);
                var sr = st.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(1f, 1f); sr.anchoredPosition = new Vector2(-72f, -350f);
            }
            else if (i == 1)
            {
                var tag = KChip("Tag", card.transform, "mode.selected", UiKit.Hazard, UiKit.Ink);
                In(tag.transform.parent.GetComponent<Image>(), 16f, 16f, 70f, 22f);
                tag.transform.parent.SetParent(sel.transform, true);
                var rec = KTxt("RecordLabel", card.transform, F.Bold, 16f, UiKit.Text, TextAlignmentOptions.Left, "mode.record"); Prop(rec, CW, 22f, CW * 0.55f, 338f, 24f);
                mp.endlessRecord = KTxt("Record", card.transform, F.Bold, 16f, UiKit.Success, TextAlignmentOptions.Right); Prop(mp.endlessRecord, CW, CW * 0.4f, CW - 22f, 338f, 24f);
            }
            else if (i == ModePanel.Coop)
            {
                var tag = KChip("Tag", card.transform, "mode.coop_tag", UiKit.Hex(0x0B0D09, 0.85f), UiKit.Gem);
                In(tag.transform.parent.GetComponent<Image>(), 16f, 16f, 90f, 22f);
                var tags = Rect("Tags", card.transform); Prop(tags, CW, 22f, CW - 22f, 332f, 30f);
                var hl = tags.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 8f; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childAlignment = TextAnchor.MiddleLeft;
                foreach (var k in new[] { "mode.tag_code", "mode.tag_online" })
                {
                    var tt = KChip("T", tags, k, UiKit.Hex(0x0E1A22), UiKit.Gem, 30f, 13f);
                    tt.font = UiFonts.Bold; tt.characterSpacing = 0f;
                }
            }
            else
            {
                var tag = KChip("Tag", card.transform, "mode.hard_tag", UiKit.Hex(0x0B0D09, 0.85f), UiKit.DangerAlt);
                In(tag.transform.parent.GetComponent<Image>(), 16f, 16f, 90f, 22f);
                var tags = Rect("Tags", card.transform); Prop(tags, CW, 22f, CW - 22f, 332f, 30f);
                var hl = tags.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 8f; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childAlignment = TextAnchor.MiddleLeft;
                foreach (var k in new[] { "mode.tag_ammo", "mode.tag_strong" })
                {
                    var tt = KChip("T", tags, k, UiKit.Hex(0x3A1512), UiKit.DangerAlt, 30f, 13f);
                    tt.font = UiFonts.Bold; tt.characterSpacing = 0f;
                }
                mp.hardRecord = KTxt("Record", card.transform, F.Bold, 15f, UiKit.DangerAlt, TextAlignmentOptions.Right); Prop(mp.hardRecord, CW, CW * 0.5f, CW - 22f, 306f, 22f);
            }
        }
        return mp;
    }

    // ================================================================ weapon select
    static LoadoutPanel BuildLoadout(Transform root, ShopUI shop, PreviewStage stage, RenderTexture rt)
    {
        var page = Page("LoadoutPanel", root);
        var lp = page.gameObject.AddComponent<LoadoutPanel>();
        lp.root = page.gameObject; lp.shop = shop; lp.preview = stage;
        lp.coinIcon = kit.coin; lp.lockIcon = kit.lockI; lp.crossIcon = kit.crosshair;
        lp.back = KHeader(page.transform, "loadout.title", null, out _, out lp.modeLabel);
        Pills(page.transform, false, 28f, 44f, 104f, out lp.gold, out lp.gems, out _, out _);
        var body = BodyRect(page.transform, 108f, 38f);

        // Detail (left).
        var det = KCard("Detail", body, 20, UiKit.Panel, UiKit.Line);
        Prop(det, 1168f, 0f, 558f, 0f, 444f);
        var glow = KImg("Glow", det.transform, kit.soft, new Color(UiKit.Hazard.r, UiKit.Hazard.g, UiKit.Hazard.b, 0.12f));
        Prop(glow, 558f, 80f, 480f, 10f, 220f);
        lp.previewImage = Preview("Preview", det.transform, rt);
        Prop(lp.previewImage, 558f, 160f, 400f, 4f, 240f);
        var tag = KChip("Equipped", det.transform, "shop.equipped", UiKit.Hazard, UiKit.Ink, 24f, 12f);
        In(tag.transform.parent.GetComponent<Image>(), 18f, 18f, 90f, 24f);
        lp.equippedTag = tag.transform.parent.gameObject;
        var hz = KHazard("Hazard", det.transform, 558f, 8f);
        Prop(hz, 558f, 0f, 558f, 192f, 8f);
        lp.detailName = KTxt("Name", det.transform, F.Display, 34f, UiKit.Text); Prop(lp.detailName, 558f, 26f, 300f, 214f, 44f);
        lp.detailDesc = KPara("Desc", det.transform, 16f, UiKit.Secondary); Prop(lp.detailDesc, 558f, 148f, 540f, 226f, 44f);
        string[] statKeys = { "stat.damage", "stat.rate", "stat.mag", "stat.reload" };
        lp.statBars = new RectTransform[4]; lp.statValues = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            float y = 272f + i * 37f;
            var l = KTxt("Stat" + i, det.transform, F.Bold, 17f, UiKit.Text, TextAlignmentOptions.Left, statKeys[i]); Prop(l, 558f, 26f, 140f, y, 24f);
            var bar = KBar("Bar" + i, det.transform, UiKit.Hazard, out var trough, 8f);
            Prop(trough, 558f, 134f, 460f, y + 8f, 8f);
            lp.statBars[i] = bar;
            lp.statValues[i] = KTxt("Value" + i, det.transform, F.Bold, 17f, UiKit.Text, TextAlignmentOptions.Right); Prop(lp.statValues[i], 558f, 470f, 534f, y, 24f);
        }

        // List (right).
        int n = System.Enum.GetValues(typeof(WeaponId)).Length;
        lp.rows = new Button[n]; lp.rowFrames = new Image[n]; lp.rowIcons = new Image[n]; lp.rowSelected = new GameObject[n]; lp.rowEquipped = new GameObject[n];
        lp.rowNames = new TMP_Text[n]; lp.rowSubs = new TMP_Text[n]; lp.rowActions = new Button[n];
        for (int i = 0; i < n; i++)
        {
            var row = KCard("Row" + i, body, 12, UiKit.Card, UiKit.Line);
            Prop(row, 1168f, 584f, 1168f, i * 72f, 62f);
            lp.rows[i] = CardButton(row); lp.rowFrames[i] = row;
            var box = KImg("IconBox", row.transform, kit.dash, UiKit.Line2, true, 2f); In(box, 10f, 8f, 96f, 46f);
            lp.rowIcons[i] = KIcon("Icon", box.transform, kit.crosshair, UiKit.Disabled, 22f); Fill(lp.rowIcons[i], 12f);
            lp.rowIcons[i].rectTransform.offsetMin = new Vector2(37f, 12f); lp.rowIcons[i].rectTransform.offsetMax = new Vector2(-37f, -12f);
            lp.rowNames[i] = KTxt("Name", row.transform, F.Bold, 21f, UiKit.Text); In(lp.rowNames[i], 122f, 8f, 260f, 26f);
            lp.rowSubs[i] = KTxt("Sub", row.transform, F.Semi, 15f, UiKit.Secondary); In(lp.rowSubs[i], 122f, 34f, 260f, 20f);
            var eq = Rect("Equipped", row.transform); InR(eq, 16f, 18f, 130f, 26f);
            var eqc = KIcon("Check", eq, kit.check, UiKit.Hazard, 18f); Place(eqc.rectTransform, new Vector2(0f, 0.5f), 9f, 0f, 18f, 18f);
            var eqt = KCaps("Text", eq, 14f, UiKit.Hazard, "shop.equipped", TextAlignmentOptions.Right); Margins(eqt, 24f, 0f, 0f, 0f);
            lp.rowEquipped[i] = eq.gameObject;
            var act = KButton("Action", row.transform, UiStyle.Dark, null, 100f, 44f, 17f, kit.coin);
            KeepIcon(act);
            InR(act, 14f, 9f, 100f, 44f);
            lp.rowActions[i] = act;
            var sel = SelectedFrame(row.transform, 12);
            lp.rowSelected[i] = sel.gameObject;
        }
        lp.upgrade = KButton("Upgrade", body, UiStyle.Dark, "loadout.upgrade", 200f, 72f, 20f, null, false, 20);
        Prop(lp.upgrade, 1168f, 584f, 786f, 372f, 72f);
        lp.start = KButton("Start", body, UiStyle.Yellow, "loadout.start", 370f, 72f, 34f, kit.play, true, 20);
        Prop(lp.start, 1168f, 798f, 1168f, 372f, 72f);
        return lp;
    }

    // ================================================================ shop
    static ShopUI BuildShop(Transform root, PreviewStage stage, RenderTexture rt)
    {
        var page = Page("Shop", root, 0.95f);
        var shop = page.gameObject.AddComponent<ShopUI>();
        shop.root = page.gameObject; shop.preview = stage;
        shop.coinIcon = kit.coin; shop.gemIcon = kit.gem; shop.checkIcon = kit.check; shop.lockIcon = kit.lockI; shop.userIcon = kit.user;
        shop.heartIcon = kit.heart; shop.cardIcon = kit.cardI; shop.goldIcon = kit.coin;
        var t = KTxt("Title", page.transform, F.Display, 30f, UiKit.Text, TextAlignmentOptions.Left, "shop.title"); TL(t, Safe, 26f, 400f, 40f);
        Pills(page.transform, true, 26f, 44f, 144f, out shop.gold, out shop.gems, out shop.goldPlus, out shop.gemPlus);

        // Sidebar.
        string[] tabKeys = { "shop.tab.weapons", "shop.tab.characters", "shop.tab.costumes", "shop.tab.bonuses", "shop.tab.gems" };
        Sprite[] tabIcons = { kit.crosshair, kit.user, kit.shirt, kit.bolt, kit.gemLine };
        shop.tabButtons = new Button[5];
        for (int i = 0; i < 5; i++)
        {
            var b = KButton("Tab" + i, page.transform, i == 0 ? UiStyle.Yellow : UiStyle.Ghost, tabKeys[i], 200f, 54f, 19f, tabIcons[i]);
            var hl = b.transform.Find("Face/Content").GetComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft; hl.padding = new RectOffset(18, 12, 0, 0); hl.spacing = 12f;
            TL(b, Safe, 92f + i * 64f, 200f, 54f);
            shop.tabButtons[i] = b;
        }
        var free = KChip("Free", shop.tabButtons[(int)ShopUI.Tab.Gems].transform, "shop.free", UiKit.Success, UiKit.Ink, 20f, 11f);
        var fr = (RectTransform)free.transform.parent; fr.anchorMin = fr.anchorMax = new Vector2(1f, 0.5f); fr.pivot = new Vector2(1f, 0.5f); fr.anchoredPosition = new Vector2(-12f, 0f);
        shop.freeChip = fr.gameObject;
        shop.close = KButton("Menu", page.transform, UiStyle.Dark, "shop.menu", 200f, 52f, 19f, kit.back);
        BL(shop.close, Safe, 38f, 200f, 52f);

        var content = Margins(Rect("Content", page.transform), 284f, 92f, Safe, 38f);
        // Characters + Costumes share the card pane (each tab fills it with its own cards).
        shop.panes = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            if (i == (int)ShopUI.Tab.Costumes) continue;
            var p = Rect("Pane" + i, content); Fill(p); shop.panes[i] = p.gameObject;
        }
        shop.panes[(int)ShopUI.Tab.Costumes] = shop.panes[(int)ShopUI.Tab.Characters];

        // ---- Weapons: list + detail.
        var wp = shop.panes[0].transform;
        int n = System.Enum.GetValues(typeof(WeaponId)).Length;
        shop.weaponRows = new ShopUI.Card[n];
        for (int i = 0; i < n; i++)
        {
            var row = KCard("W" + i, wp, 12, UiKit.Card, UiKit.Line);
            In(row, 0f, i * 84f, 220f, 75f);
            var c = new ShopUI.Card { button = CardButton(row), frame = row };
            c.title = KTxt("Name", row.transform, F.Bold, 20f, UiKit.Text); In(c.title, 18f, 12f, 190f, 26f);
            c.sub = KTxt("Sub", row.transform, F.Bold, 15f, UiKit.Hazard); In(c.sub, 18f, 40f, 190f, 20f);
            c.icon = KIcon("Coin", row.transform, kit.coin, Color.white, 15f); In(c.icon, 18f, 43f, 15f, 15f);
            c.selected = SelectedFrame(row.transform, 12);
            shop.weaponRows[i] = c;
        }
        var det = KCard("Detail", wp, 20, UiKit.Panel, UiKit.Line);
        Margins(det, 236f, 0f, 0f, 0f);
        const float DW = 704f;
        var pbox = KCard("PreviewBox", det.transform, 12, UiKit.Hex(0x0E120C), UiKit.Line2);
        Prop(pbox, DW, 30f, 300f, 34f, 150f);
        var pglow = KImg("Glow", pbox.transform, kit.soft, new Color(UiKit.Hazard.r, UiKit.Hazard.g, UiKit.Hazard.b, 0.14f)); Fill(pglow, -10f);
        shop.previewImage = Preview("Preview", pbox.transform, rt);
        Fill(shop.previewImage, 2f);
        var nameRow = Rect("NameRow", det.transform); Prop(nameRow, DW, 330f, 690f, 26f, 48f);
        var nhl = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        nhl.childAlignment = TextAnchor.MiddleLeft; nhl.spacing = 14f; nhl.childControlWidth = true; nhl.childControlHeight = false; nhl.childForceExpandWidth = false;
        shop.title = KTxt("Name", nameRow, F.Display, 36f, UiKit.Text);
        shop.title.rectTransform.sizeDelta = new Vector2(10f, 48f);
        shop.typeTag = KChip("Type", nameRow, null, UiKit.Hex(0x2A2210), UiKit.Hazard, 26f, 12f);
        shop.description = KPara("Desc", det.transform, 16f, UiKit.Secondary); Prop(shop.description, DW, 330f, 690f, 80f, 44f);
        string[] stats = { "stat.damage_caps", "stat.rate_caps", "stat.mag_caps", "stat.reload_caps" };
        shop.statValues = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
        {
            float x0 = 330f + i * 92f;
            var box = KCard("Stat" + i, det.transform, 12, UiKit.Hex(0x10140E), null);
            Prop(box, DW, x0, x0 + 84f, 126f, 58f);
            var l = KCaps("Label", box.transform, 10f, UiKit.Secondary, stats[i]); l.characterSpacing = 6f; In(l, 10f, 8f, 80f, 14f);
            shop.statValues[i] = KTxt("Value", box.transform, F.Display, 20f, UiKit.Text); In(shop.statValues[i], 10f, 24f, 80f, 28f);
        }
        Divider(det.transform, 206f, 0f);
        var upl = KCaps("UpgradesLabel", det.transform, 13f, UiKit.Hazard, "shop.upgrades"); Prop(upl, DW, 30f, 330f, 228f, 20f);
        var eq = Rect("Equipped", det.transform); Prop(eq, DW, 520f, 674f, 226f, 24f);
        var eqi = KIcon("Check", eq, kit.check, UiKit.Hazard, 16f); Place(eqi.rectTransform, new Vector2(0f, 0.5f), 8f, 0f, 16f, 16f);
        var eqt = KCaps("Text", eq, 13f, UiKit.Hazard, "shop.equipped", TextAlignmentOptions.Right); Margins(eqt, 22f, 0f, 0f, 0f);
        shop.equippedMark = eq.gameObject;
        shop.action = KButton("Action", det.transform, UiStyle.Yellow, null, 150f, 36f, 16f, kit.coin);
        KeepIcon(shop.action);
        Prop(shop.action, DW, 524f, 674f, 220f, 36f);
        string[] ups = { "stat.damage", "stat.mag", "stat.reload" };
        shop.upgradePips = new Image[15]; shop.upgradeLevels = new TMP_Text[3]; shop.upgradeButtons = new Button[3];
        for (int i = 0; i < 3; i++)
        {
            float y = 266f + i * 62f;
            var l = KTxt("Up" + i, det.transform, F.Bold, 19f, UiKit.Text, TextAlignmentOptions.Left, ups[i]); Prop(l, DW, 30f, 150f, y + 8f, 26f);
            var pipRoot = Rect("Pips" + i, det.transform); Prop(pipRoot, DW, 158f, 320f, y + 17f, 8f);
            var pips = KPips(pipRoot, 5, 26f, 8f, 6f);
            for (int p = 0; p < 5; p++) shop.upgradePips[i * 5 + p] = pips[p];
            shop.upgradeLevels[i] = KTxt("Lv" + i, det.transform, F.Semi, 16f, UiKit.Secondary); Prop(shop.upgradeLevels[i], DW, 330f, 420f, y + 8f, 24f);
            var ub = KButton("Buy" + i, det.transform, UiStyle.Yellow, null, 160f, 48f, 19f, kit.coin);
            KeepIcon(ub);
            Prop(ub, DW, 514f, 674f, y, 48f);
            shop.upgradeButtons[i] = ub;
        }

        // ---- Characters: horizontal cards.
        var cp = shop.panes[1].transform;
        var scroll = HScroll(cp, out var ccontent, 18f);
        Fill(scroll);
        shop.characterContent = ccontent;
        shop.characterTemplate = CharacterCard(ccontent, rt);

        // ---- Bonuses.
        var bp = shop.panes[(int)ShopUI.Tab.Bonuses].transform;
        var bsub = KTxt("Sub", bp, F.Semi, 16f, UiKit.Secondary, TextAlignmentOptions.Left, "shop.bonus_sub"); In(bsub, 0f, 0f, 700f, 22f);
        int nb = Economy.Bonuses.Length;
        shop.bonusRows = new ShopUI.Card[nb]; shop.bonusPips = new Image[nb * 3]; shop.bonusNext = new TMP_Text[nb]; shop.bonusMissing = new TMP_Text[nb];
        Color[] tileCols = { UiKit.Hex(0x3A1512), UiKit.Hex(0x2C1B45), UiKit.Hex(0x3A2A08) };
        Color[] iconCols = { UiKit.DangerAlt, UiKit.Hex(0xC9A2FF), UiKit.Gold };
        Sprite[] bicons = { kit.heart, kit.cardI, kit.coin };
        for (int i = 0; i < nb; i++)
        {
            var row = KCard("B" + i, bp, 20, UiKit.Card, UiKit.Line);
            var rr = Margins(row, 0f, 34f + i * 126f, 0f, 0f); rr.anchorMin = new Vector2(0f, 1f); rr.pivot = new Vector2(0.5f, 1f);
            rr.sizeDelta = new Vector2(0f, 112f); rr.anchoredPosition = new Vector2(0f, -(34f + i * 126f));
            var tile = KCard("Tile", row.transform, 20, tileCols[i % 3], UiKit.Line); In(tile, 18f, 16f, 80f, 80f);
            var ic = KIcon("Icon", tile.transform, bicons[i % 3], i == 2 ? Color.white : iconCols[i % 3], 44f); In(ic, 18f, 18f, 44f, 44f);
            var c = new ShopUI.Card();
            c.title = KTxt("Title", row.transform, F.Display, 24f, UiKit.Text); In(c.title, 120f, 14f, 500f, 32f);
            c.sub = KTxt("Desc", row.transform, F.Semi, 16f, UiKit.Secondary); In(c.sub, 120f, 46f, 520f, 22f);
            var pipRoot = Rect("Pips", row.transform); In(pipRoot, 120f, 80f, 160f, 10f);
            var pips = KPips(pipRoot, 3, 48f, 10f, 6f);
            for (int p = 0; p < 3; p++) shop.bonusPips[i * 3 + p] = pips[p];
            c.tag = KTxt("Level", row.transform, F.Semi, 15f, UiKit.Secondary); In(c.tag, 290f, 74f, 80f, 22f);
            shop.bonusNext[i] = KTxt("Next", row.transform, F.Bold, 15f, UiKit.Success); In(shop.bonusNext[i], 372f, 74f, 260f, 22f);
            c.action = KButton("Buy", row.transform, UiStyle.Yellow, null, 180f, 52f, 20f, kit.coin);
            KeepIcon(c.action);
            InR(c.action, 26f, 20f, 180f, 52f);
            shop.bonusMissing[i] = KTxt("Missing", row.transform, F.Bold, 14f, UiKit.DangerAlt, TextAlignmentOptions.Center); InR(shop.bonusMissing[i], 26f, 80f, 180f, 18f);
            c.button = c.action; c.frame = row;
            shop.bonusRows[i] = c;
        }

        // ---- Gems: free banner + packs.
        var gp = shop.panes[(int)ShopUI.Tab.Gems].transform;
        var banner = KCard("Free", gp, 20, UiKit.Hex(0x111C0C), UiKit.Hex(0x4F7A0C));
        var br = Margins(banner, 0f, 0f, 0f, 0f); br.anchorMin = new Vector2(0f, 1f); br.pivot = new Vector2(0.5f, 1f); br.sizeDelta = new Vector2(0f, 168f); br.anchoredPosition = Vector2.zero;
        var bglow = KImg("Glow", banner.transform, kit.soft, new Color(UiKit.Success.r, UiKit.Success.g, UiKit.Success.b, 0.08f)); Fill(bglow, -30f);
        var gtile = KCard("Tile", banner.transform, 20, UiKit.Hex(0x0E1A22), UiKit.Hex(0x2C6E92)); In(gtile, 28f, 26f, 116f, 116f);
        var gic = KIcon("Gem", gtile.transform, kit.gem, Color.white, 64f); In(gic, 26f, 26f, 64f, 64f);
        var plus5 = KChip("Plus", gtile.transform, null, UiKit.Success, UiKit.Ink, 30f, 16f);
        plus5.text = "+" + Economy.FreeGemAd; plus5.font = UiFonts.Display;
        var pr = (RectTransform)plus5.transform.parent; pr.anchorMin = pr.anchorMax = new Vector2(1f, 0f); pr.pivot = new Vector2(0.5f, 0.5f); pr.anchoredPosition = new Vector2(-4f, 6f);
        var daily = KCaps("Daily", banner.transform, 12f, UiKit.Success, "shop.daily_refresh"); In(daily, 168f, 44f, 400f, 18f);
        var ft = KTxt("FreeTitle", banner.transform, F.Display, 30f, UiKit.Text, TextAlignmentOptions.Left, "shop.free_title"); In(ft, 168f, 62f, 420f, 40f);
        shop.freeDesc = KTxt("FreeDesc", banner.transform, F.Semi, 16f, UiKit.Secondary); In(shop.freeDesc, 168f, 104f, 420f, 22f);
        shop.freeButton = KButton("Watch", banner.transform, UiStyle.Green, "shop.watch_ad", 220f, 64f, 22f, kit.film);
        InR(shop.freeButton, 28f, 52f, 220f, 64f);
        var pl = KCaps("PacksLabel", gp, 13f, UiKit.Hazard, "shop.packs"); In(pl, 0f, 190f, 400f, 20f);
        var note = KTxt("Note", gp, F.Semi, 13f, UiKit.Secondary, TextAlignmentOptions.Right); InR(note, 0f, 190f, 520f, 20f);
        shop.priceNote = note;
        var gs = HScroll(gp, out var gcontent, 18f);
        var gsr = Margins(gs, 0f, 218f, 0f, 0f);
        shop.productContent = gcontent;
        shop.productTemplate = ProductCard(gcontent);

        // ---- Confirm + message.
        var conf = KDim("Confirm", page.transform, 0.7f);
        var cm = KModal("Panel", conf.transform, true); CC(cm, 380f, 190f, 520f, 210f);
        shop.confirmText = KPara("Text", cm.transform, 20f, UiKit.Text); In(shop.confirmText, 32f, 34f, 456f, 80f);
        shop.confirmText.font = UiFonts.Bold; shop.confirmText.alignment = TextAlignmentOptions.Center;
        shop.confirmNo = KButton("No", cm.transform, UiStyle.Dark, "shop.no", 210f, 54f, 19f); In(shop.confirmNo, 32f, 128f, 210f, 54f);
        shop.confirmYes = KButton("Yes", cm.transform, UiStyle.Yellow, "shop.yes", 210f, 54f, 19f); InR(shop.confirmYes, 32f, 128f, 210f, 54f);
        shop.confirm = conf.gameObject;
        shop.message = KTxt("Message", page.transform, F.Bold, 18f, UiKit.Gold, TextAlignmentOptions.Center);
        Place(shop.message.rectTransform, new Vector2(0.5f, 0f), 120f, 12f, 600f, 24f);
        return shop;
    }

    static RectTransform HScroll(Transform parent, out RectTransform content, float spacing)
    {
        var view = Rect("Scroll", parent);
        var img = view.gameObject.AddComponent<Image>(); img.color = new Color(0f, 0f, 0f, 0f); img.raycastTarget = true;
        view.gameObject.AddComponent<RectMask2D>();
        var sr = view.gameObject.AddComponent<ScrollRect>();
        content = Rect("Content", view);
        content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, 0.5f);
        content.offsetMin = content.offsetMax = Vector2.zero;
        var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = spacing; hl.childControlWidth = false; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
        content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = content; sr.horizontal = true; sr.vertical = false; sr.movementType = ScrollRect.MovementType.Elastic;
        sr.viewport = view; sr.scrollSensitivity = 30f;
        return view;
    }

    static ShopUI.Card CharacterCard(Transform content, RenderTexture rt)
    {
        var body = KCard("Character", content, 20, UiKit.Card, UiKit.Line);
        body.rectTransform.sizeDelta = new Vector2(222f, 460f);
        var c = new ShopUI.Card { button = CardButton(body), frame = body };
        var art = KImg("Art", body.transform, kit.soft, new Color(UiKit.Hazard.r, UiKit.Hazard.g, UiKit.Hazard.b, 0.10f)); In(art, 6f, 60f, 210f, 230f);
        c.preview = Preview("Preview", body.transform, rt); In(c.preview, 6f, 30f, 210f, 250f);
        // Lock badge (top right) over the character still.
        c.icon = KIcon("Icon", body.transform, kit.user, UiKit.Secondary, 26f); In(c.icon, 180f, 14f, 26f, 26f);
        c.tag = KChip("Tag", body.transform, null, UiKit.Hazard, UiKit.Ink, 22f, 11f);
        c.tagBg = c.tag.transform.parent.GetComponent<Image>();
        In(c.tagBg, 16f, 16f, 80f, 22f);
        c.title = KTxt("Name", body.transform, F.Display, 21f, UiKit.Text); In(c.title, 18f, 290f, 190f, 30f);
        c.title.enableAutoSizing = true; c.title.fontSizeMin = 14f; c.title.fontSizeMax = 21f;
        c.sub = KPara("Desc", body.transform, 15f, UiKit.Secondary); In(c.sub, 18f, 324f, 188f, 64f);
        c.action = KButton("Action", body.transform, UiStyle.Dark, null, 186f, 46f, 18f, kit.coin);
        KeepIcon(c.action);
        In(c.action, 18f, 398f, 186f, 46f);
        c.selected = SelectedFrame(body.transform, 20);
        return c;
    }

    static ShopUI.Card ProductCard(Transform content)
    {
        var body = KCard("Product", content, 20, UiKit.Card, UiKit.Line);
        body.rectTransform.sizeDelta = new Vector2(224f, 242f);
        var c = new ShopUI.Card { button = CardButton(body), frame = body };
        var art = KCard("Art", body.transform, 20, UiKit.Hex(0x0E151A), null); In(art, 14f, 14f, 196f, 132f);
        var glow = KImg("Glow", art.transform, kit.soft, new Color(UiKit.Gem.r, UiKit.Gem.g, UiKit.Gem.b, 0.16f)); Fill(glow, -10f);
        c.icon = KIcon("Icon", art.transform, kit.gem, Color.white, 52f); In(c.icon, 72f, 24f, 52f, 52f);
        // What the pack gives ("150 ELMAS"), big under the icon; the buy button shows the TRY price.
        c.sub = KTxt("Amount", art.transform, F.Display, 24f, UiKit.Gem, TextAlignmentOptions.Center); In(c.sub, 6f, 84f, 184f, 34f);
        c.sub.enableAutoSizing = true; c.sub.fontSizeMin = 12f; c.sub.fontSizeMax = 24f;
        c.title = KTxt("Name", body.transform, F.Bold, 20f, UiKit.Text, TextAlignmentOptions.Center); In(c.title, 8f, 152f, 208f, 28f);
        c.title.enableAutoSizing = true; c.title.fontSizeMin = 13f; c.title.fontSizeMax = 20f;
        c.action = KButton("Buy", body.transform, UiStyle.Yellow, null, 196f, 46f, 19f, kit.gem);
        KeepIcon(c.action);
        In(c.action, 14f, 186f, 196f, 46f);
        var pop = KChip("Popular", body.transform, "shop.char_pack", UiKit.Hazard, UiKit.Ink, 22f, 11f);
        c.tagBg = pop.transform.parent.GetComponent<Image>();
        var pr = c.tagBg.rectTransform; pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 1f); pr.pivot = new Vector2(0.5f, 0.5f); pr.anchoredPosition = new Vector2(0f, -16f);
        c.selected = SelectedFrame(body.transform, 20);
        return c;
    }

    // ================================================================ daily reward
    static RectTransform ModalRect(Image m, float ax0, float ax1, float h)
    {
        var r = m.rectTransform;
        r.anchorMin = new Vector2(ax0, 0.5f); r.anchorMax = new Vector2(ax1, 0.5f); r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(0f, h); r.anchoredPosition = Vector2.zero;
        return r;
    }

    static DailyLoginPanel BuildLogin(Transform root)
    {
        var dim = KDim("LoginPanel", root);
        var dp = dim.gameObject.AddComponent<DailyLoginPanel>();
        dp.root = dim.gameObject;
        var p = KModal("Panel", dim.transform);
        ModalRect(p, 0.1f, 0.9f, 478f);
        const float MW = 1024f;
        var t = KTxt("Title", p.transform, F.Display, 36f, UiKit.Text, TextAlignmentOptions.Left, "login.title"); In(t, 40f, 26f, 600f, 46f);
        var s = KTxt("Sub", p.transform, F.Semi, 16f, UiKit.Secondary, TextAlignmentOptions.Left, "login.sub"); In(s, 40f, 72f, 700f, 22f);
        dp.close = KIconButton("Close", p.transform, kit.close, 46f); InR(dp.close, 40f, 27f, 46f, 46f);
        int n = Economy.LoginRewards.Length;
        dp.days = new Image[n]; dp.dayStrokes = new Image[n]; dp.dayChecks = new GameObject[n]; dp.dayIcons = new GameObject[n]; dp.dayDash = new GameObject[n];
        dp.dayRewards = new TMP_Text[n]; dp.dayLabels = new TMP_Text[n]; dp.dayTags = new TMP_Text[n]; dp.dayTagBgs = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            bool big = i == n - 1;
            float x0 = big ? 784f : 40f + i * 125.6f, x1 = big ? 984f : x0 + 112f;
            var d = KImg("Day" + i, p.transform, kit.r20, UiKit.Card, true);
            Prop(d, MW, x0, x1, 110f, 174f);
            var st = KImg("Stroke", d.transform, kit.r20Stroke, UiKit.Line, true); Fill(st);
            var dash = KImg("Dash", d.transform, kit.dash, UiKit.Hazard, true, 1f); Fill(dash);
            dp.days[i] = d; dp.dayStrokes[i] = st; dp.dayDash[i] = dash.gameObject;
            dp.dayLabels[i] = KCaps("Label", d.transform, 13f, UiKit.Secondary, null, TextAlignmentOptions.Center);
            Margins(dp.dayLabels[i], 4f, 14f, 4f, 0f); dp.dayLabels[i].rectTransform.anchorMin = new Vector2(0f, 1f); dp.dayLabels[i].rectTransform.sizeDelta = new Vector2(-8f, 18f);
            dp.dayLabels[i].rectTransform.pivot = new Vector2(0.5f, 1f); dp.dayLabels[i].rectTransform.anchoredPosition = new Vector2(0f, -14f);
            dp.dayLabels[i].characterSpacing = 6f;
            var icons = Rect("Icon", d.transform); Place(icons, new Vector2(0.5f, 1f), 0f, -82f, 64f, 64f);
            var r = Economy.LoginRewards[i];
            if (big)
            {
                foreach (var (x, y, sz) in new[] { (-30f, -6f, 40f), (30f, -6f, 40f), (0f, 4f, 60f) })
                { var g = KIcon("Gem", icons, kit.gem, Color.white, sz); g.rectTransform.anchoredPosition = new Vector2(x, y); }
            }
            else
            {
                var coin = KIcon("Coin", icons, kit.coin, Color.white, 48f); coin.rectTransform.anchoredPosition = r.gems > 0 ? new Vector2(-8f, 4f) : Vector2.zero;
                if (r.gems > 0) { var g = KIcon("Gem", icons, kit.gem, Color.white, 28f); g.rectTransform.anchoredPosition = new Vector2(16f, -8f); }
            }
            dp.dayIcons[i] = icons.gameObject;
            var chk = KImg("Check", d.transform, kit.dot, UiKit.Success); Place(chk.rectTransform, new Vector2(0.5f, 1f), 0f, -82f, 56f, 56f);
            var ci = KIcon("Icon", chk.transform, kit.check, UiKit.Ink, 30f); Fill(ci, 13f);
            dp.dayChecks[i] = chk.gameObject;
            dp.dayRewards[i] = KTxt("Reward", d.transform, F.Bold, 18f, UiKit.Text, TextAlignmentOptions.Bottom);
            Margins(dp.dayRewards[i], 4f, 0f, 4f, 14f); dp.dayRewards[i].rectTransform.anchorMax = new Vector2(1f, 0f); dp.dayRewards[i].rectTransform.sizeDelta = new Vector2(-8f, 46f);
            dp.dayRewards[i].rectTransform.pivot = new Vector2(0.5f, 0f); dp.dayRewards[i].rectTransform.anchoredPosition = new Vector2(0f, 14f);
            dp.dayRewards[i].lineSpacing = -10f;
            var tag = KChip("Tag", d.transform, null, UiKit.Hazard, UiKit.Ink, 20f, 11f);
            var tr = (RectTransform)tag.transform.parent; tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f); tr.pivot = new Vector2(0.5f, 0.5f); tr.anchoredPosition = Vector2.zero;
            dp.dayTags[i] = tag; dp.dayTagBgs[i] = tr.gameObject;
        }
        var sl = KCaps("Streak", p.transform, 13f, UiKit.Secondary, "login.streak"); In(sl, 40f, 308f, 60f, 20f);
        var sb = KBar("StreakBar", p.transform, UiKit.Success, out var strough, 8f);
        Prop(strough, MW, 100f, 950f, 314f, 8f);
        dp.streakFill = sb;
        dp.streakText = KTxt("StreakText", p.transform, F.Bold, 17f, UiKit.Text, TextAlignmentOptions.Right); InR(dp.streakText, 40f, 306f, 60f, 24f);
        var note = KTxt("Note", p.transform, F.Semi, 15f, UiKit.Secondary, TextAlignmentOptions.Left, "login.note"); In(note, 40f, 410f, 500f, 22f);
        dp.claim = KButton("Claim", p.transform, UiStyle.Yellow, null, 330f, 58f, 19f, kit.clock);
        InR(dp.claim, 40f, 392f, 330f, 58f);
        dp.claimLabel = LabelOf(dp.claim);
        return dp;
    }

    // ================================================================ quests
    static QuestsPanel BuildQuests(Transform root)
    {
        var dim = KDim("QuestsPanel", root);
        var qp = dim.gameObject.AddComponent<QuestsPanel>();
        qp.root = dim.gameObject;
        var p = KModal("Panel", dim.transform);
        ModalRect(p, 0.15625f, 0.84375f, 510f);
        var t = KTxt("Title", p.transform, F.Display, 36f, UiKit.Text, TextAlignmentOptions.Left, "quest.title"); In(t, 40f, 24f, 600f, 46f);
        var ck = KIcon("Clock", p.transform, kit.clock, UiKit.Secondary, 16f); In(ck, 40f, 72f, 16f, 16f);
        qp.timer = KTxt("Timer", p.transform, F.Semi, 16f, UiKit.Secondary); In(qp.timer, 62f, 68f, 400f, 22f);
        qp.close = KIconButton("Close", p.transform, kit.close, 46f); InR(qp.close, 40f, 27f, 46f, 46f);
        qp.kindIcons = new[] { kit.skull, kit.skull, kit.skull, kit.waves, kit.flag, kit.play, kit.bolt };
        qp.checkIcon = kit.check;
        const int n = 3;
        qp.rows = new GameObject[n]; qp.rowBgs = new Image[n]; qp.iconTiles = new Image[n]; qp.icons = new Image[n];
        qp.titles = new TMP_Text[n]; qp.progress = new TMP_Text[n]; qp.rewards = new TMP_Text[n]; qp.bars = new RectTransform[n]; qp.barFills = new Image[n]; qp.claims = new Button[n];
        for (int i = 0; i < n; i++)
        {
            var row = KCard("Row" + i, p.transform, 20, UiKit.Card, UiKit.Line);
            var rr = Margins(row, 40f, 0f, 40f, 0f); rr.anchorMin = new Vector2(0f, 1f); rr.pivot = new Vector2(0.5f, 1f);
            rr.sizeDelta = new Vector2(-80f, 100f); rr.anchoredPosition = new Vector2(0f, -(104f + i * 116f));
            qp.rows[i] = row.gameObject; qp.rowBgs[i] = row;
            var tile = KCard("Tile", row.transform, 12, UiKit.Hex(0x221C0C), UiKit.Line); In(tile, 20f, 18f, 64f, 64f);
            qp.iconTiles[i] = tile;
            qp.icons[i] = KIcon("Icon", tile.transform, kit.skull, UiKit.Hazard, 32f); In(qp.icons[i], 16f, 16f, 32f, 32f);
            qp.titles[i] = KTxt("Title", row.transform, F.Bold, 21f, UiKit.Text); In(qp.titles[i], 100f, 16f, 420f, 28f);
            var bar = KBar("Bar", row.transform, UiKit.Hazard, out var tr, 8f);
            var brr = tr.rectTransform; brr.anchorMin = new Vector2(0f, 1f); brr.anchorMax = new Vector2(1f, 1f); brr.pivot = new Vector2(0f, 1f);
            brr.offsetMin = new Vector2(100f, -66f); brr.offsetMax = new Vector2(-300f, -58f);
            qp.bars[i] = bar; qp.barFills[i] = bar.GetComponent<Image>();
            qp.progress[i] = KTxt("Progress", row.transform, F.Bold, 16f, UiKit.Text, TextAlignmentOptions.Left); InR(qp.progress[i], 236f, 50f, 54f, 22f);
            var pill = KImg("Reward", row.transform, kit.pill, UiKit.Hex(0x0E120C), true, 4f); InR(pill, 168f, 32f, 66f, 34f);
            var ps = KImg("Stroke", pill.transform, kit.pillStroke, UiKit.Line, true, 4f); Fill(ps);
            var pc = KIcon("Coin", pill.transform, kit.coin, Color.white, 18f); In(pc, 9f, 8f, 18f, 18f);
            qp.rewards[i] = KTxt("Value", pill.transform, F.Bold, 16f, UiKit.Text, TextAlignmentOptions.Right); Margins(qp.rewards[i], 26f, 0f, 10f, 0f);
            qp.claims[i] = KButton("Claim", row.transform, UiStyle.Dark, null, 132f, 54f, 19f, kit.check);
            InR(qp.claims[i], 20f, 22f, 132f, 54f);
        }
        qp.total = KTxt("Total", p.transform, F.Semi, 16f, UiKit.Secondary); In(qp.total, 40f, 466f, 700f, 22f);
        return qp;
    }

    // ================================================================ chapter select
    static CampaignPanel BuildCampaign(Transform root, LoadoutPanel loadout)
    {
        var page = Page("CampaignPanel", root);
        var cp = page.gameObject.AddComponent<CampaignPanel>();
        cp.root = page.gameObject; cp.loadout = loadout;
        cp.back = KHeader(page.transform, "camp.title", null, out _, out cp.total);
        var body = BodyRect(page.transform, 100f, 38f);
        int n = Campaign.Count;
        cp.chapterButtons = new Button[n]; cp.numbers = new TMP_Text[n]; cp.titles = new TMP_Text[n]; cp.descs = new TMP_Text[n]; cp.infos = new TMP_Text[n];
        cp.stars = new Image[n * 3]; cp.locks = new GameObject[n];
        for (int i = 0; i < n; i++)
        {
            int col = i % 3, row = i / 3;
            var card = KCard("Chapter" + i, body, 20, UiKit.Card, UiKit.Line);
            var r = Prop(card, 1168f, col * 396f, col * 396f + 376f, row * 236f, 216f);
            cp.chapterButtons[i] = CardButton(card);
            cp.numbers[i] = KCaps("Number", card.transform, 13f, UiKit.Hazard); In(cp.numbers[i], 20f, 16f, 200f, 18f);
            cp.titles[i] = KTxt("Title", card.transform, F.Display, 24f, UiKit.Text); Prop(cp.titles[i], 376f, 20f, 360f, 38f, 32f);
            cp.descs[i] = KPara("Desc", card.transform, 15f, UiKit.Secondary); Prop(cp.descs[i], 376f, 20f, 356f, 76f, 50f);
            cp.infos[i] = KTxt("Info", card.transform, F.Bold, 15f, UiKit.Success); Prop(cp.infos[i], 376f, 20f, 356f, 132f, 22f);
            for (int k = 0; k < 3; k++)
            {
                var st = KIcon("Star" + k, card.transform, kit.star, UiKit.Gold, 26f); In(st, 20f + k * 34f, 166f, 26f, 26f);
                cp.stars[i * 3 + k] = st;
            }
            var lk = KIcon("Lock", card.transform, kit.lockI, UiKit.Disabled, 24f); InR(lk, 20f, 16f, 24f, 24f);
            cp.locks[i] = lk.gameObject;
        }
        return cp;
    }

    // ================================================================ co-op room
    static CoopPanel BuildCoopPanel(Transform root)
    {
        var dim = KDim("CoopPanel", root);
        var cp = dim.gameObject.AddComponent<CoopPanel>();
        cp.root = dim.gameObject;
        var p = KModal("Panel", dim.transform);
        CC(p, 320f, 90f, 640f, 420f);
        var t = KTxt("Title", p.transform, F.Display, 34f, UiKit.Text, TextAlignmentOptions.Left, "coop.title"); In(t, 36f, 26f, 500f, 44f);
        cp.back = KIconButton("Close", p.transform, kit.close, 46f); InR(cp.back, 36f, 26f, 46f, 46f);

        var idle = Rect("Idle", p.transform); Fill(idle);
        cp.create = KButton("Create", idle, UiStyle.Yellow, "coop.create", 568f, 64f, 26f, null, true, 20); In(cp.create, 36f, 96f, 568f, 64f);
        var input = Rect("Code", idle); In(input, 36f, 186f, 380f, 60f);
        var ibg = KCard("Bg", input, 12, UiKit.Hex(0x0B0D09), UiKit.Line2); Fill(ibg); ibg.raycastTarget = true;
        var area = Rect("Text Area", input); Fill(area, 8f); area.gameObject.AddComponent<RectMask2D>();
        var ph = KTxt("Placeholder", area, F.Bold, 22f, UiKit.Disabled, TextAlignmentOptions.Center, "coop.code_hint"); Fill(ph);
        var it = KTxt("Text", area, F.Display, 30f, UiKit.Text, TextAlignmentOptions.Center); Fill(it);
        var tif = input.gameObject.AddComponent<TMP_InputField>();
        tif.textViewport = area; tif.textComponent = it; tif.placeholder = ph; tif.targetGraphic = ibg;
        tif.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
        cp.codeInput = tif;
        cp.join = KButton("Join", idle, UiStyle.Dark, "coop.join", 172f, 60f, 22f); InR(cp.join, 36f, 186f, 172f, 60f);

        var room = Rect("Room", p.transform); Fill(room);
        var rc = KTxt("CodeLabel", room, F.Semi, 16f, UiKit.Secondary, TextAlignmentOptions.Center, "coop.room_code"); In(rc, 36f, 90f, 568f, 22f);
        cp.codeText = KTxt("Code", room, F.Display, 64f, UiKit.Hazard, TextAlignmentOptions.Center); In(cp.codeText, 36f, 114f, 568f, 80f);
        cp.codeText.characterSpacing = 20f;
        cp.playersText = KTxt("Players", room, F.Bold, 20f, UiKit.Success, TextAlignmentOptions.Center); In(cp.playersText, 36f, 200f, 568f, 26f);
        cp.leave = KButton("Leave", room, UiStyle.Dark, "coop.leave", 200f, 60f, 20f); In(cp.leave, 36f, 250f, 200f, 60f);
        cp.start = KButton("Start", room, UiStyle.Yellow, "coop.start", 352f, 60f, 26f, kit.play, true, 20); InR(cp.start, 36f, 250f, 352f, 60f);

        cp.status = KTxt("Status", p.transform, F.Semi, 16f, UiKit.Gold, TextAlignmentOptions.Center); In(cp.status, 36f, 350f, 568f, 40f);
        cp.status.textWrappingMode = TextWrappingModes.Normal;
        cp.idleGroup = idle.gameObject; cp.roomGroup = room.gameObject;
        return cp;
    }

    // ================================================================ settings (menu + pause)
    static Slider KSlider(string name, Transform parent)
    {
        var rootR = Rect(name, parent);
        var bg = KImg("Background", rootR, kit.r12, UiKit.Hex(0x232B1E), true, 6f);
        var br = bg.rectTransform; br.anchorMin = new Vector2(0f, 0.5f); br.anchorMax = new Vector2(1f, 0.5f); br.sizeDelta = new Vector2(0f, 10f); br.anchoredPosition = Vector2.zero;
        bg.raycastTarget = true;
        var fillArea = Rect("Fill Area", rootR);
        fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f); fillArea.sizeDelta = new Vector2(0f, 10f); fillArea.anchoredPosition = Vector2.zero;
        var fill = KImg("Fill", fillArea, kit.r12, UiKit.Hazard, true, 6f);
        var fr = fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0.5f, 1f); fr.offsetMin = fr.offsetMax = Vector2.zero;
        var handleArea = Rect("Handle Slide Area", rootR); Fill(handleArea);
        handleArea.offsetMin = new Vector2(10f, 0f); handleArea.offsetMax = new Vector2(-10f, 0f);
        var handle = KImg("Handle", handleArea, kit.dot, UiKit.Hex(0x1F7BFF));
        var hr = handle.rectTransform; hr.anchorMin = new Vector2(0.5f, 0.5f); hr.anchorMax = new Vector2(0.5f, 0.5f); hr.sizeDelta = new Vector2(22f, 22f);
        handle.raycastTarget = true;
        var s = rootR.gameObject.AddComponent<Slider>();
        s.fillRect = fr; s.handleRect = hr; s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight; s.minValue = 0f; s.maxValue = 1f; s.value = 0.5f;
        return s;
    }

    static Button[] Segmented(Transform parent, string[] keys, string[] texts)
    {
        var bg = KCard("Segments", parent, 12, UiKit.Hex(0x0B0D09), UiKit.Line);
        Fill(bg);
        var hl = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(5, 5, 5, 5); hl.spacing = 6f; hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
        bg.transform.Find("Stroke").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        int n = keys != null ? keys.Length : texts.Length;
        var arr = new Button[n];
        for (int i = 0; i < n; i++)
        {
            var b = KButton("Seg" + i, bg.transform, i == 0 ? UiStyle.Yellow : UiStyle.Ghost, keys != null ? keys[i] : null, 80f, 40f, 17f);
            if (texts != null) LabelOf(b).text = texts[i];
            b.GetComponent<PressButton>().depth = 2f;
            arr[i] = b;
        }
        return arr;
    }

    static SettingsPanel BuildSettingsPanel(Transform root)
    {
        var dim = KDim("SettingsPanel", root);
        var sp = dim.gameObject.AddComponent<SettingsPanel>();
        sp.root = dim.gameObject;
        var p = KModal("Panel", dim.transform);
        Margins(p, Safe, 24f, Safe, 24f);
        const float W = 1168f;
        var t = KTxt("Title", p.transform, F.Display, 36f, UiKit.Text, TextAlignmentOptions.Left, "set.title"); In(t, 40f, 30f, 500f, 46f);
        sp.close = KIconButton("Close", p.transform, kit.close, 46f); InR(sp.close, 32f, 22f, 46f, 46f);

        Image Box(string name, float x0, float x1, float y, float h, Sprite icon, string key)
        {
            var b = KCard(name, p.transform, 20, UiKit.Hex(0x10140E), UiKit.Line);
            Prop(b, W, x0, x1, y, h);
            var ic = KIcon("Icon", b.transform, icon, UiKit.Hazard, 16f); In(ic, 22f, 19f, 16f, 16f);
            var l = KCaps("Label", b.transform, 13f, UiKit.Hazard, key); In(l, 46f, 18f, 300f, 18f);
            return b;
        }
        UiText RowLabel(Transform box, string key, float y) { var l = KTxt("L_" + key, box, F.Bold, 20f, UiKit.Text, TextAlignmentOptions.Left, key); In(l, 22f, y, 180f, 28f); return l; }
        Slider SliderRow(Transform box, float boxW, string key, float y, out TMP_Text value)
        {
            RowLabel(box, key, y);
            var s = KSlider("S_" + key, box); Prop(s, boxW, 202f, 459f, y + 2f, 24f);
            value = KTxt("V_" + key, box, F.Bold, 17f, UiKit.Text, TextAlignmentOptions.Right); InR(value, 22f, y, 60f, 28f);
            return s;
        }
        const float BW = 539f;
        var audio = Box("Audio", 32f, 571f, 84f, 185f, kit.speaker, "set.audio");
        sp.music = SliderRow(audio.transform, BW, "set.music", 48f, out sp.musicValue);
        sp.sfx = SliderRow(audio.transform, BW, "set.sfx", 94f, out sp.sfxValue);
        RowLabel(audio.transform, "set.vibration", 140f);
        var vib = Rect("Vibration", audio.transform); Prop(vib, BW, 202f, 266f, 138f, 32f);
        var track = KImg("Track", vib, kit.pill, UiKit.Hex(0x232B1E), true, 3f); Fill(track); track.raycastTarget = true;
        var knob = KImg("Knob", vib, kit.dot, UiKit.Hex(0xDAD6C8)); Place(knob.rectTransform, new Vector2(0.5f, 0.5f), -16f, 0f, 24f, 24f);
        sp.vibration = vib.gameObject.AddComponent<Button>(); sp.vibration.targetGraphic = track; sp.vibration.transition = Selectable.Transition.None;
        sp.vibrationTrack = track; sp.vibrationKnob = knob.rectTransform;
        sp.vibrationLabel = KTxt("VibValue", audio.transform, F.Bold, 17f, UiKit.Text, TextAlignmentOptions.Right); InR(sp.vibrationLabel, 22f, 140f, 90f, 28f);
        var ctrl = Box("Controls", 32f, 571f, 282f, 140f, kit.gamepad, "set.controls");
        sp.joySize = SliderRow(ctrl.transform, BW, "set.joy_size", 48f, out sp.joySizeValue);
        sp.joyAlpha = SliderRow(ctrl.transform, BW, "set.joy_alpha", 94f, out sp.joyAlphaValue);

        var gfx = Box("Display", 597f, 1136f, 84f, 145f, kit.monitor, "set.display");
        RowLabel(gfx.transform, "set.fps", 48f);
        var fr = Rect("Fps", gfx.transform); Prop(fr, BW, 152f, 520f, 38f, 46f);
        var fpsTexts = new string[Settings.FpsOptions.Length];
        for (int i = 0; i < fpsTexts.Length; i++) fpsTexts[i] = Settings.FpsOptions[i].ToString();
        sp.fpsButtons = Segmented(fr, null, fpsTexts);
        RowLabel(gfx.transform, "set.quality", 100f);
        var qr = Rect("Quality", gfx.transform); Prop(qr, BW, 152f, 520f, 90f, 44f);
        sp.qualityButtons = Segmented(qr, new[] { "set.q.low", "set.q.mid", "set.q.high" }, null);
        var lang = Box("Language", 597f, 1136f, 242f, 94f, kit.globe, "set.language");
        var lr = Rect("Lang", lang.transform); Prop(lr, BW, 18f, 521f, 40f, 44f);
        sp.langButtons = Segmented(lr, null, new[] { "TÜRKÇE", "ENGLISH" });

        var mail = KIcon("Mail", p.transform, kit.mail, UiKit.Secondary, 18f); Prop(mail, W, 605f, 623f, 360f, 18f);
        var mt = KTxt("Email", p.transform, F.Semi, 16f, UiKit.Secondary); Prop(mt, W, 633f, 900f, 356f, 24f);
        mt.text = SettingsPanel.SupportMail;
        sp.version = KTxt("Version", p.transform, F.Semi, 16f, UiKit.Secondary, TextAlignmentOptions.Right); Prop(sp.version, W, 900f, 1136f, 356f, 24f);
        Divider(p.transform, 458f, 32f);
        sp.support = KButton("Support", p.transform, UiStyle.Dark, "set.support", 90f, 44f, 17f); Prop(sp.support, W, 32f, 120f, 474f, 44f);
        sp.privacy = KButton("Privacy", p.transform, UiStyle.Dark, "set.privacy", 96f, 44f, 17f); Prop(sp.privacy, W, 132f, 228f, 474f, 44f);
        sp.terms = KButton("Terms", p.transform, UiStyle.Dark, "set.terms", 110f, 44f, 17f); Prop(sp.terms, W, 240f, 350f, 474f, 44f);
        sp.restore = KButton("Restore", p.transform, UiStyle.Dark, "set.restore", 230f, 44f, 17f); Prop(sp.restore, W, 362f, 600f, 474f, 44f);
        sp.reset = KButton("Reset", p.transform, UiStyle.Danger, "set.reset", 190f, 44f, 17f, kit.refresh); Prop(sp.reset, W, 930f, 1136f, 474f, 44f);
        sp.resetDone = KTxt("ResetDone", p.transform, F.Bold, 17f, UiKit.Success, TextAlignmentOptions.Center); Prop(sp.resetDone, W, 360f, 900f, 420f, 26f);

        var conf = KDim("Confirm", p.transform, 0.75f);
        Margins(conf, -200f, -200f, -200f, -200f);
        var cm = KModal("Box", conf.transform); CC(cm, 380f, 180f, 520f, 230f);
        var ct = KPara("Text", cm.transform, 20f, UiKit.Text, "set.reset_confirm"); In(ct, 32f, 34f, 456f, 100f);
        ct.font = UiFonts.Bold; ((TextMeshProUGUI)ct).alignment = TextAlignmentOptions.Center;
        sp.confirmNo = KButton("No", cm.transform, UiStyle.Dark, "set.no", 210f, 54f, 19f); In(sp.confirmNo, 32f, 148f, 210f, 54f);
        sp.confirmYes = KButton("Yes", cm.transform, UiStyle.Danger, "set.yes", 210f, 54f, 19f); InR(sp.confirmYes, 32f, 148f, 210f, 54f);
        sp.confirm = conf.gameObject;
        return sp;
    }

    // ================================================================ loading screen prefab (Resources)
    static void BuildLoadingPrefab()
    {
        System.IO.Directory.CreateDirectory("Assets/Resources");
        var c = KitCanvas("LoadingScreen", 1000);
        var ls = c.gameObject.AddComponent<LoadingScreen>();
        ls.group = c.gameObject.AddComponent<CanvasGroup>();
        var root = c.transform;
        var bg = Img("Background", root, null, UiKit.Ground); Fill(bg, -4f); bg.type = Image.Type.Simple; bg.raycastTarget = true;
        var glow = KImg("Glow", root, kit.soft, new Color(UiKit.Hazard.r, UiKit.Hazard.g, UiKit.Hazard.b, 0.06f)); CC(glow, 240f, 40f, 800f, 400f);
        var logo = KTxt("Logo", root, F.Display, 72f, UiKit.Text, TextAlignmentOptions.Center); logo.text = "HOLDOUT"; logo.fontSharedMaterial = UiFonts.DisplayShadow;
        CC(logo, 240f, 150f, 800f, 90f);
        var city = KCaps("City", root, 16f, UiKit.Hazard, "menu.tagline", TextAlignmentOptions.Center); city.characterSpacing = 70f; CC(city, 340f, 238f, 600f, 24f);
        var lbl = KCaps("Loading", root, 14f, UiKit.Secondary, "load.title", TextAlignmentOptions.Center); CC(lbl, 440f, 300f, 400f, 20f);
        ls.barFill = KBar("Bar", root, UiKit.Hazard, out var trough, 10f);
        CC(trough, 390f, 330f, 500f, 10f);
        var tipChip = KChip("TipChip", root, "menu.tip", UiKit.Hex(0x2A2210), UiKit.Hazard, 22f, 12f);
        var tr = (RectTransform)tipChip.transform.parent; Place(tr, new Vector2(0.5f, 0f), 0f, 112f, 60f, 22f);
        ls.tip = KPara("Tip", root, 18f, UiKit.Text);
        ((TextMeshProUGUI)ls.tip).alignment = TextAlignmentOptions.Top;
        Place(ls.tip.rectTransform, new Vector2(0.5f, 0f), 0f, 56f, 900f, 50f);
        PrefabUtility.SaveAsPrefabAsset(c.gameObject, "Assets/Resources/LoadingScreen.prefab");
        Object.DestroyImmediate(c.gameObject);
    }
}
