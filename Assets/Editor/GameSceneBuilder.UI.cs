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
        p.titles = new UiText[n]; p.descriptions = new UiText[n]; p.icons = new Image[n]; p.rarityTexts = new UiText[n];
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
            L(badgeTxt, "cards.new");

            var flash = Sliced("Flash", card, ui.flat, new Color(1f, 1f, 1f, 0f));
            Stretch(flash.rectTransform);

            p.cards[i] = card; p.backgrounds[i] = body; p.rarityStrips[i] = strip; p.rarityTexts[i] = rar; p.glows[i] = glow;
            p.flashes[i] = flash; p.shines[i] = shine.rectTransform; p.newBadges[i] = badge.gameObject;
            p.icons[i] = ico; p.titles[i] = title; p.descriptions[i] = desc;
        }
        return p;
    }

    /// <summary>Static label bound to a Loc key (text set now and on language change).</summary>
    static void L(UiText t, string key)
    {
        t.text = Loc.T(key);
        t.gameObject.AddComponent<LocText>().key = key;
    }
}
