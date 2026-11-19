using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// UI redesign kit (docs/ui-ref, StyleGuide): 1280×590 canvases (match height, 56 px side safe area), Bungee / Barlow
/// Condensed TMP text, rounded dark-glass panels, press-down buttons (yellow / green / dark / disabled / danger),
/// currency pills, tabs, chips, hazard stripes and 2.2 px line icons — all generated here.
/// Layout helpers take reference-image coordinates (1x, y downward from the top / up from the bottom).
/// </summary>
public static partial class GameSceneBuilder
{
    public const float RefW = 1280f, RefH = 590f, Safe = 56f;

    class Kit
    {
        public Sprite r12, r12Stroke, r20, r20Stroke, r26, r26Stroke, gloss, pill, pillStroke, hazard, glow, ring, ringThin, dot, soft, stripes, dash;
        public Sprite gear, clipboard, gift, bag, crosshair, shirt, bolt, gemLine, arrowL, arrowR, back, play, lockI, check, close, clock, map,
            infinity, flame, refresh, film, heart, skull, flag, waves, speaker, monitor, globe, gamepad, mail, user, star, starLine, plus, coin,
            gem, bullet, cardI, pause, chevronR, warn;
    }

    static Kit kit;

    static Kit MakeKit()
    {
        var k = new Kit();
        System.IO.Directory.CreateDirectory(UiArt + "/Kit");
        k.r12 = Round("K_R12", 26, false); k.r12Stroke = Round("K_R12s", 26, true);
        k.r20 = Round("K_R20", 40, false); k.r20Stroke = Round("K_R20s", 40, true);
        k.r26 = Round("K_R26", 52, false); k.r26Stroke = Round("K_R26s", 52, true);
        k.pill = Round("K_Pill", 48, false, 96); k.pillStroke = Round("K_Pills", 48, true, 96);
        k.gloss = KitTex("K_Gloss", 96, 96, (x, y) =>
        {
            float d = RoundBox(x - 48f, y - 48f, 46f, 46f, 26f);
            float a = Mathf.Clamp01(0.5f - d) * Mathf.Clamp01((y / 96f - 0.45f) * 2.2f) * 0.32f;
            return new Color(1f, 1f, 1f, a);
        }, new Vector4(30, 30, 30, 30));
        k.hazard = KitTex("K_Hazard", 32, 16, (x, y) =>
        {
            float v = Mathf.Repeat(x + y * 1.1f, 16f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Min(v, 9f - v) + 0.5f));
        }, Vector4.zero, TextureWrapMode.Repeat);
        k.stripes = KitTex("K_Stripes", 64, 64, (x, y) =>
        {
            float v = Mathf.Repeat(x + y, 32f);
            return new Color(1f, 1f, 1f, v < 14f ? 1f : 0f);
        }, Vector4.zero, TextureWrapMode.Repeat);
        k.glow = KitTex("K_Glow", 128, 128, (x, y) =>
        {
            float d = RoundBox(x - 64f, y - 64f, 34f, 34f, 22f);
            float a = Mathf.Clamp01(1f - Mathf.Max(0f, d) / 28f);
            return new Color(1f, 1f, 1f, a * a * 0.9f);
        }, new Vector4(60, 60, 60, 60));
        k.soft = KitTex("K_Soft", 128, 128, (x, y) =>
        {
            float r = new Vector2(x - 64f, y - 64f).magnitude / 64f;
            float a = Mathf.Clamp01(1f - r);
            return new Color(1f, 1f, 1f, a * a);
        }, Vector4.zero);
        k.ring = KitTex("K_Ring", 128, 128, (x, y) =>
        {
            float r = new Vector2(x - 64f, y - 64f).magnitude;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - (Mathf.Abs(r - 56f) - 6f)));
        }, Vector4.zero);
        k.ringThin = KitTex("K_RingThin", 128, 128, (x, y) =>
        {
            float r = new Vector2(x - 64f, y - 64f).magnitude;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - (Mathf.Abs(r - 60f) - 2f)));
        }, Vector4.zero);
        k.dot = KitTex("K_Dot", 64, 64, (x, y) =>
        {
            float r = new Vector2(x - 32f, y - 32f).magnitude;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(30.5f - r));
        }, Vector4.zero);
        k.dash = KitTex("K_Dash", 64, 64, (x, y) =>
        {
            float d = RoundBox(x - 32f, y - 32f, 30f, 30f, 14f);
            bool on = Mathf.Repeat((x + y) * 0.5f, 6f) < 3.4f;
            return new Color(1f, 1f, 1f, on ? Mathf.Clamp01(0.5f - (Mathf.Abs(d) - 1.2f)) : 0f);
        }, new Vector4(20, 20, 20, 20));

        // Line icons (2.2 px at 24 px), shape space [-1, 1].
        const float W = 0.1f;
        k.gear = LineIcon("gear", p =>
        {
            float r = p.magnitude, a = Mathf.Atan2(p.y, p.x);
            float sector = Mathf.Repeat(a + Mathf.PI / 8f, Mathf.PI / 4f) - Mathf.PI / 8f; // teeth every 45°
            var q = new Vector2(Mathf.Cos(sector), Mathf.Sin(sector)) * r;
            float body = r - 0.56f;
            float tooth = RoundBox(q.x - 0.66f, q.y, 0.14f, 0.13f, 0.03f);
            float outline = Mathf.Min(body, tooth);
            return Mathf.Min(Mathf.Abs(outline) - W, Mathf.Abs(r - 0.2f) - W);
        });
        k.clipboard = LineIcon("clipboard", p => Min(Stroke(RoundBox(p.x, p.y + 0.05f, 0.55f, 0.75f, 0.12f), W),
            Stroke(RoundBox(p.x, p.y - 0.78f, 0.25f, 0.1f, 0.05f), W), Seg(p, new Vector2(-0.25f, 0f), new Vector2(-0.05f, -0.2f), W), Seg(p, new Vector2(-0.05f, -0.2f), new Vector2(0.28f, 0.22f), W)));
        k.gift = LineIcon("gift", p => Min(Stroke(RoundBox(p.x, p.y + 0.2f, 0.65f, 0.5f, 0.06f), W), Seg(p, new Vector2(-0.75f, -0.32f), new Vector2(0.75f, -0.32f), W),
            Seg(p, new Vector2(0f, -0.32f), new Vector2(0f, 0.7f), W), Stroke(new Vector2(p.x + 0.22f, p.y + 0.48f).magnitude - 0.2f, W), Stroke(new Vector2(p.x - 0.22f, p.y + 0.48f).magnitude - 0.2f, W)), true);
        k.bag = LineIcon("bag", p => Min(Stroke(RoundBox(p.x, p.y + 0.2f, 0.62f, 0.55f, 0.12f), W), Mathf.Max(Stroke(new Vector2(p.x, p.y + 0.35f).magnitude - 0.3f, W), -(p.y + 0.35f))), true);
        k.crosshair = LineIcon("crosshair", p => Min(Stroke(p.magnitude - 0.55f, W), Seg(p, new Vector2(0f, 0.35f), new Vector2(0f, 0.85f), W), Seg(p, new Vector2(0f, -0.35f), new Vector2(0f, -0.85f), W),
            Seg(p, new Vector2(0.35f, 0f), new Vector2(0.85f, 0f), W), Seg(p, new Vector2(-0.35f, 0f), new Vector2(-0.85f, 0f), W)));
        k.shirt = LineIcon("shirt", p => Stroke(Poly(p, new[] { new Vector2(-0.3f, -0.75f), new Vector2(-0.75f, -0.5f), new Vector2(-0.55f, -0.05f), new Vector2(-0.4f, -0.15f),
            new Vector2(-0.4f, 0.75f), new Vector2(0.4f, 0.75f), new Vector2(0.4f, -0.15f), new Vector2(0.55f, -0.05f), new Vector2(0.75f, -0.5f), new Vector2(0.3f, -0.75f), new Vector2(0f, -0.55f) }), W), true);
        k.bolt = LineIcon("bolt", p => Stroke(Poly(p, new[] { new Vector2(0.15f, -0.85f), new Vector2(-0.5f, 0.1f), new Vector2(-0.05f, 0.1f), new Vector2(-0.15f, 0.85f),
            new Vector2(0.5f, -0.1f), new Vector2(0.05f, -0.1f) }), W), true);
        k.gemLine = LineIcon("gemline", p => Min(Stroke(Poly(p, new[] { new Vector2(-0.75f, -0.25f), new Vector2(-0.4f, -0.6f), new Vector2(0.4f, -0.6f), new Vector2(0.75f, -0.25f), new Vector2(0f, 0.7f) }), W),
            Seg(p, new Vector2(-0.75f, -0.25f), new Vector2(0.75f, -0.25f), W)), true);
        k.arrowL = LineIcon("chevl", p => Min(Seg(p, new Vector2(0.22f, -0.5f), new Vector2(-0.22f, 0f), W * 1.2f), Seg(p, new Vector2(-0.22f, 0f), new Vector2(0.22f, 0.5f), W * 1.2f)));
        k.arrowR = LineIcon("chevr", p => Min(Seg(p, new Vector2(-0.22f, -0.5f), new Vector2(0.22f, 0f), W * 1.2f), Seg(p, new Vector2(0.22f, 0f), new Vector2(-0.22f, 0.5f), W * 1.2f)));
        k.chevronR = k.arrowR;
        k.back = LineIcon("back", p => Min(Seg(p, new Vector2(-0.6f, 0f), new Vector2(0.6f, 0f), W), Seg(p, new Vector2(-0.6f, 0f), new Vector2(-0.15f, -0.45f), W), Seg(p, new Vector2(-0.6f, 0f), new Vector2(-0.15f, 0.45f), W)));
        k.play = FillIcon("play", p => Tri(p, new Vector2(-0.45f, -0.6f), new Vector2(-0.45f, 0.6f), new Vector2(0.6f, 0f)) - 0.04f);
        k.lockI = LineIcon("lock", p => Min(Stroke(RoundBox(p.x, p.y + 0.3f, 0.55f, 0.4f, 0.1f), W), Mathf.Max(Stroke(new Vector2(p.x, p.y - 0.12f).magnitude - 0.32f, W), -(p.y - 0.12f)), Seg(p, new Vector2(-0.32f, 0.12f), new Vector2(-0.32f, -0.1f), W), Seg(p, new Vector2(0.32f, 0.12f), new Vector2(0.32f, -0.1f), W)));
        k.check = LineIcon("check", p => Min(Seg(p, new Vector2(-0.6f, 0f), new Vector2(-0.18f, -0.42f), W * 1.3f), Seg(p, new Vector2(-0.18f, -0.42f), new Vector2(0.62f, 0.45f), W * 1.3f)));
        k.close = LineIcon("close", p => Min(Seg(p, new Vector2(-0.5f, -0.5f), new Vector2(0.5f, 0.5f), W * 1.2f), Seg(p, new Vector2(-0.5f, 0.5f), new Vector2(0.5f, -0.5f), W * 1.2f)));
        k.clock = LineIcon("clock", p => Min(Stroke(p.magnitude - 0.72f, W), Seg(p, Vector2.zero, new Vector2(0f, 0.42f), W), Seg(p, Vector2.zero, new Vector2(0.3f, -0.18f), W)));
        k.map = LineIcon("map", p => Min(Stroke(Poly(p, new[] { new Vector2(-0.8f, -0.6f), new Vector2(-0.8f, 0.7f), new Vector2(-0.28f, 0.5f), new Vector2(0.28f, 0.7f), new Vector2(0.8f, 0.5f),
            new Vector2(0.8f, -0.8f), new Vector2(0.28f, -0.6f), new Vector2(-0.28f, -0.8f) }), W), Seg(p, new Vector2(-0.28f, -0.8f), new Vector2(-0.28f, 0.5f), W), Seg(p, new Vector2(0.28f, -0.6f), new Vector2(0.28f, 0.7f), W)), true);
        k.infinity = LineIcon("infinity", p => Mathf.Min(Stroke(new Vector2(p.x + 0.42f, p.y).magnitude - 0.36f, W * 1.4f), Stroke(new Vector2(p.x - 0.42f, p.y).magnitude - 0.36f, W * 1.4f)));
        k.flame = LineIcon("flame", p => Stroke(Mathf.Min(new Vector2(p.x, p.y + 0.25f).magnitude - 0.5f, Tri(p, new Vector2(-0.46f, -0.1f), new Vector2(0.46f, -0.1f), new Vector2(0.05f, 0.85f))), W * 1.2f));
        k.refresh = LineIcon("refresh", p =>
        {
            float a = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            float arc = a > 20f && a < 80f ? 9f : Stroke(p.magnitude - 0.6f, W * 1.2f);
            return Mathf.Min(arc, Stroke(Tri(p, new Vector2(0.3f, 0.75f), new Vector2(0.7f, 0.85f), new Vector2(0.62f, 0.4f)), W * 0.6f));
        });
        k.film = LineIcon("film", p => Mathf.Min(Stroke(RoundBox(p.x, p.y, 0.75f, 0.55f, 0.12f), W), Tri(p, new Vector2(-0.18f, -0.28f), new Vector2(-0.18f, 0.28f), new Vector2(0.28f, 0f))), true);
        k.heart = FillIcon("heart", p => Heart(p * 1.05f, 0.95f));
        k.skull = LineIcon("skull", p => Min(Stroke(Mathf.Max(new Vector2(p.x, p.y - 0.12f).magnitude - 0.62f, -(p.y + 0.38f)), W), Stroke(RoundBox(p.x, p.y + 0.5f, 0.32f, 0.2f, 0.05f), W),
            new Vector2(p.x + 0.24f, p.y - 0.05f).magnitude - 0.13f, new Vector2(p.x - 0.24f, p.y - 0.05f).magnitude - 0.13f));
        k.flag = LineIcon("flag", p => Min(Seg(p, new Vector2(-0.5f, -0.85f), new Vector2(-0.5f, 0.75f), W), Stroke(Poly(p, new[] { new Vector2(-0.5f, 0.75f), new Vector2(0.6f, 0.75f), new Vector2(0.3f, 0.4f),
            new Vector2(0.6f, 0.05f), new Vector2(-0.5f, 0.05f) }), W)));
        k.waves = LineIcon("waves", p =>
        {
            float d = 9f;
            for (int i = -1; i <= 1; i++) { float yy = p.y - i * 0.42f - 0.12f * Mathf.Sin(p.x * 6f); if (Mathf.Abs(p.x) < 0.75f) d = Mathf.Min(d, Mathf.Abs(yy) - W); }
            return d;
        });
        k.speaker = LineIcon("speaker", p => Min(Stroke(Poly(p, new[] { new Vector2(-0.7f, -0.25f), new Vector2(-0.35f, -0.25f), new Vector2(0.05f, -0.6f), new Vector2(0.05f, 0.6f), new Vector2(-0.35f, 0.25f), new Vector2(-0.7f, 0.25f) }), W),
            Mathf.Max(Stroke(new Vector2(p.x - 0.05f, p.y).magnitude - 0.38f, W), -(p.x - 0.3f)), Mathf.Max(Stroke(new Vector2(p.x - 0.05f, p.y).magnitude - 0.68f, W), -(p.x - 0.45f))), true);
        k.monitor = LineIcon("monitor", p => Min(Stroke(RoundBox(p.x, p.y - 0.12f, 0.75f, 0.5f, 0.08f), W), Seg(p, new Vector2(-0.3f, 0.75f), new Vector2(0.3f, 0.75f), W), Seg(p, new Vector2(0f, 0.38f), new Vector2(0f, 0.75f), W)), true);
        k.globe = LineIcon("globe", p => Min(Stroke(p.magnitude - 0.72f, W), Stroke(new Vector2(p.x * 2.2f, p.y).magnitude - 0.72f, W), Seg(p, new Vector2(-0.72f, 0f), new Vector2(0.72f, 0f), W)));
        k.gamepad = LineIcon("gamepad", p => Min(Stroke(RoundBox(p.x, p.y, 0.8f, 0.42f, 0.3f), W), Seg(p, new Vector2(-0.5f, 0f), new Vector2(-0.2f, 0f), W), Seg(p, new Vector2(-0.35f, -0.15f), new Vector2(-0.35f, 0.15f), W),
            new Vector2(p.x - 0.4f, p.y).magnitude - 0.07f), true);
        k.mail = LineIcon("mail", p => Min(Stroke(RoundBox(p.x, p.y, 0.75f, 0.5f, 0.08f), W), Seg(p, new Vector2(-0.7f, 0.45f), new Vector2(0f, -0.05f), W), Seg(p, new Vector2(0f, -0.05f), new Vector2(0.7f, 0.45f), W)));
        k.user = LineIcon("user", p => Min(Stroke(new Vector2(p.x, p.y - 0.3f).magnitude - 0.3f, W), Mathf.Max(Stroke(new Vector2(p.x, p.y + 0.75f).magnitude - 0.6f, W), p.y + 0.45f)));
        k.star = FillIcon("star", p => Star(p, 5, 0.95f, 0.42f, 0f));
        k.starLine = LineIcon("starline", p => Stroke(Star(p, 5, 0.88f, 0.4f, 0f), W * 0.9f));
        k.plus = LineIcon("plus", p => Min(Seg(p, new Vector2(-0.5f, 0f), new Vector2(0.5f, 0f), W * 1.6f), Seg(p, new Vector2(0f, -0.5f), new Vector2(0f, 0.5f), W * 1.6f)));
        k.pause = FillIcon("pause", p => Min(RoundBox(p.x - 0.27f, p.y, 0.13f, 0.55f, 0.05f), RoundBox(p.x + 0.27f, p.y, 0.13f, 0.55f, 0.05f)));
        k.warn = FillIcon("warn", p => Min(RoundBox(p.x, p.y - 0.15f, 0.12f, 0.45f, 0.06f), new Vector2(p.x, p.y + 0.62f).magnitude - 0.13f));
        k.bullet = FillIcon("bullet", p => Min(RoundBox(p.x, p.y + 0.25f, 0.28f, 0.5f, 0.04f), Mathf.Max(new Vector2(p.x, p.y - 0.25f).magnitude - 0.28f, p.y - 0.25f > 0f ? -1f : 1f)));
        k.cardI = LineIcon("card", p => Min(Stroke(RoundBox(p.x, p.y, 0.5f, 0.75f, 0.1f), W), Star(p * 2.6f, 5, 0.9f, 0.4f, 0f) / 2.6f), true);
        // Coloured icons (own colours, not tinted): coin + gem.
        k.coin = ColorIcon("coin", p =>
        {
            float r = p.magnitude;
            if (r > 0.92f) return Color.clear;
            var c = r > 0.62f ? UiKit.Hex(0xFFC93C) : r > 0.5f ? UiKit.Hex(0xC98A12) : UiKit.Hex(0xFFB21E);
            if (r > 0.8f) c = UiKit.Hex(0xE09A10);
            return c;
        });
        k.gem = ColorIcon("gem", p =>
        {
            var q = new Vector2(p.x, -p.y);
            float d = Poly(q, new[] { new Vector2(-0.85f, -0.25f), new Vector2(-0.45f, -0.65f), new Vector2(0.45f, -0.65f), new Vector2(0.85f, -0.25f), new Vector2(0f, 0.75f) });
            if (d > 0f) return Color.clear;
            bool top = q.y < -0.25f;
            bool facet = Mathf.Abs(q.x) < 0.3f - (q.y + 0.25f) * 0.3f;
            return top ? UiKit.Hex(0x9BE3FF) : facet ? UiKit.Hex(0x7FD6FA) : UiKit.Hex(0x3FB0E6);
        });
        return k;
    }

    static float Stroke(float d, float w) => Mathf.Abs(d) - w;

    // ---------------------------------------------------------------- texture writers
    static Sprite Round(string name, int radius, bool stroke, int size = 0)
    {
        if (size == 0) size = radius * 2 + 32;
        float h = size * 0.5f;
        return KitTex(name, size, size, (x, y) =>
        {
            float d = RoundBox(x - h, y - h, h - 1.5f, h - 1.5f, radius);
            float a = stroke ? Mathf.Clamp01(0.5f - (Mathf.Abs(d + 1.4f) - 1.4f)) : Mathf.Clamp01(0.5f - d);
            return new Color(1f, 1f, 1f, a);
        }, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
    }

    delegate Color KitPx(float x, float y);

    static Sprite KitTex(string name, int w, int h, KitPx fn, Vector4 border, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var c = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++) c[y * w + x] = fn(x + 0.5f, y + 0.5f);
        tex.SetPixels(c);
        string path = $"{UiArt}/Kit/{name}.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var s = ImportSprite(path, border, Mathf.Max(w, h));
        if (wrap == TextureWrapMode.Repeat)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
            s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return s;
    }

    /// <summary>White anti-aliased line / shape icon (tinted at runtime). sdf: distance in [-1, 1] shape space, &lt; 0 = ink.</summary>
    static Sprite LineIcon(string name, Func<Vector2, float> sdf, bool flipY = false)
    {
        const int size = 96;
        float px = 2f / size;
        return KitTex("I_" + name, size, size, (x, y) =>
        {
            Vector2 p = new Vector2(x / size * 2f - 1f, y / size * 2f - 1f) * 1.08f;
            if (flipY) p.y = -p.y;
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - sdf(p) / px));
        }, Vector4.zero);
    }

    static Sprite FillIcon(string name, Func<Vector2, float> sdf) => LineIcon(name, sdf);

    static Sprite ColorIcon(string name, Func<Vector2, Color> fn)
    {
        const int size = 96;
        return KitTex("I_" + name, size, size, (x, y) =>
        {
            // 3×3 supersampling for clean edges.
            Color acc = Color.clear;
            for (int sy = 0; sy < 3; sy++)
                for (int sx = 0; sx < 3; sx++)
                {
                    var p = new Vector2((x - 0.5f + (sx + 0.5f) / 3f) / size * 2f - 1f, (y - 0.5f + (sy + 0.5f) / 3f) / size * 2f - 1f);
                    var c = fn(p);
                    acc += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
            acc /= 9f;
            return acc.a > 0f ? new Color(acc.r / acc.a, acc.g / acc.a, acc.b / acc.a, acc.a) : Color.clear;
        }, Vector4.zero);
    }

    // ---------------------------------------------------------------- canvas
    static Canvas KitCanvas(string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var sc = go.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(RefW, RefH);
        sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        sc.matchWidthOrHeight = 1f;
        go.AddComponent<GraphicRaycaster>();
        return c;
    }

    /// <summary>Host for layouts authored at 1920×1080 (card picker, objective HUD, banners).</summary>
    static RectTransform LegacyRoot(Transform canvas, string name = "Legacy")
    {
        var r = Rect(name, canvas);
        Stretch(r);
        r.gameObject.AddComponent<ScaledRoot>();
        return r;
    }

    // ---------------------------------------------------------------- layout (reference px; y measured from the top or bottom edge)
    static RectTransform Place(RectTransform r, Vector2 anchor, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
        r.anchoredPosition = new Vector2(x, y);
        return r;
    }

    /// <summary>Top-left anchored: (x, y) = top-left corner in reference coordinates.</summary>
    static RectTransform TL(Component c, float x, float y, float w, float h) => Place((RectTransform)c.transform, new Vector2(0f, 1f), x + w * 0.5f, -(y + h * 0.5f), w, h);
    /// <summary>Top-right anchored: right = distance of the right edge from the screen's right edge.</summary>
    static RectTransform TR(Component c, float right, float y, float w, float h) => Place((RectTransform)c.transform, new Vector2(1f, 1f), -(right + w * 0.5f), -(y + h * 0.5f), w, h);
    /// <summary>Top-centre anchored: x = left edge in reference coordinates (1280 wide).</summary>
    static RectTransform TC(Component c, float x, float y, float w, float h) => Place((RectTransform)c.transform, new Vector2(0.5f, 1f), x + w * 0.5f - RefW * 0.5f, -(y + h * 0.5f), w, h);
    static RectTransform BL(Component c, float x, float bottom, float w, float h) => Place((RectTransform)c.transform, new Vector2(0f, 0f), x + w * 0.5f, bottom + h * 0.5f, w, h);
    static RectTransform BR(Component c, float right, float bottom, float w, float h) => Place((RectTransform)c.transform, new Vector2(1f, 0f), -(right + w * 0.5f), bottom + h * 0.5f, w, h);
    /// <summary>Centred on the screen; (x, y) = top-left corner in reference coordinates.</summary>
    static RectTransform CC(Component c, float x, float y, float w, float h) => Place((RectTransform)c.transform, new Vector2(0.5f, 0.5f), x + w * 0.5f - RefW * 0.5f, RefH * 0.5f - (y + h * 0.5f), w, h);
    /// <summary>Inside a parent rect: (x, y) = top-left corner relative to the parent's top-left.</summary>
    static RectTransform In(Component c, float x, float y, float w, float h) => Place((RectTransform)c.transform, new Vector2(0f, 1f), x + w * 0.5f, -(y + h * 0.5f), w, h);
    /// <summary>Inside a parent: right-aligned (right = distance from the parent's right edge).</summary>
    static RectTransform InR(Component c, float right, float y, float w, float h) => Place((RectTransform)c.transform, new Vector2(1f, 1f), -(right + w * 0.5f), -(y + h * 0.5f), w, h);
    static RectTransform Fill(Component c, float inset = 0f)
    {
        var r = (RectTransform)c.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
        r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        return r;
    }

    // ---------------------------------------------------------------- primitives
    static Image KImg(string name, Transform parent, Sprite s, Color c, bool sliced = false, float ppu = 2f)
    {
        var i = Img(name, parent, s, c);
        if (sliced) { i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = ppu; }
        i.raycastTarget = false;
        return i;
    }

    enum F { Display, Bold, Semi }

    static UiText KTxt(string name, Transform parent, F font, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left, string key = null)
    {
        var r = Rect(name, parent);
        var t = r.gameObject.AddComponent<UiText>();
        t.font = font == F.Display ? UiFonts.Display : font == F.Bold ? UiFonts.Bold : UiFonts.Semi;
        t.fontSize = size;
        t.color = color;
        t.raycastTarget = false;
        ((TextMeshProUGUI)t).alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.richText = true;
        if (key != null) L(t, key);
        return t;
    }

    /// <summary>Wrapping description text.</summary>
    static UiText KPara(string name, Transform parent, float size, Color color, string key = null)
    {
        var t = KTxt(name, parent, F.Semi, size, color, TextAlignmentOptions.TopLeft, key);
        t.textWrappingMode = TextWrappingModes.Normal;
        t.lineSpacing = -8f;
        return t;
    }

    /// <summary>Letter-spaced small caps label (section headers, "OYUN MODU").</summary>
    static UiText KCaps(string name, Transform parent, float size, Color color, string key = null, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        var t = KTxt(name, parent, F.Display, size, color, align, key);
        t.characterSpacing = 18f;
        return t;
    }

    /// <summary>Rounded card: fill + 1.5 px stroke. radius 12 / 20 / 26.</summary>
    static Image KCard(string name, Transform parent, int radius, Color fill, Color? stroke = null)
    {
        var body = KImg(name, parent, radius <= 12 ? kit.r12 : radius <= 20 ? kit.r20 : kit.r26, fill, true);
        if (stroke.HasValue)
        {
            var s = KImg("Stroke", body.transform, radius <= 12 ? kit.r12Stroke : radius <= 20 ? kit.r20Stroke : kit.r26Stroke, stroke.Value, true);
            Fill(s);
        }
        return body;
    }

    /// <summary>Dark glass modal panel (radius 26) with the hazard stripe on its top edge.</summary>
    static Image KModal(string name, Transform parent, bool hazard = true)
    {
        var p = KCard(name, parent, 26, UiKit.Panel, UiKit.Line);
        p.raycastTarget = true;
        if (hazard)
        {
            var hz = KHazard("Hazard", p.transform, 110f, 8f);
            In(hz, 40f, -4f, 110f, 8f);
        }
        return p;
    }

    static Image KHazard(string name, Transform parent, float w, float h)
    {
        var i = KImg(name, parent, kit.hazard, UiKit.Hazard);
        i.type = Image.Type.Tiled; i.pixelsPerUnitMultiplier = 2f;
        i.rectTransform.sizeDelta = new Vector2(w, h);
        return i;
    }

    /// <summary>Full-screen dim behind a modal (blocks clicks).</summary>
    static Image KDim(string name, Transform parent, float alpha = 0.86f)
    {
        var d = Img(name, parent, null, new Color(0.02f, 0.025f, 0.015f, alpha));
        Fill(d, -4f);
        d.type = Image.Type.Simple;
        d.raycastTarget = true;
        return d;
    }

    static Image KIcon(string name, Transform parent, Sprite s, Color c, float size)
    {
        var i = KImg(name, parent, s, c);
        i.preserveAspect = true;
        i.rectTransform.sizeDelta = new Vector2(size, size);
        return i;
    }

    /// <summary>Small rounded tag (SEÇİLİ, KUŞANILDI, NADİR...): text colour on a tinted background.</summary>
    static UiText KChip(string name, Transform parent, string key, Color bg, Color text, float h = 22f, float size = 12f)
    {
        var b = KImg(name, parent, kit.r12, bg, true, 4f);
        b.rectTransform.sizeDelta = new Vector2(80f, h);
        var t = KCaps("Label", b.transform, size, text, key, TextAlignmentOptions.Center);
        t.characterSpacing = 8f;
        Fill(t);
        var fit = b.gameObject.AddComponent<HorizontalLayoutGroup>();
        fit.padding = new RectOffset(9, 9, 0, 0); fit.childAlignment = TextAnchor.MiddleCenter;
        fit.childControlWidth = true; fit.childControlHeight = true; fit.childForceExpandWidth = false;
        b.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        return t;
    }

    /// <summary>
    /// Press-down button: lip (4 px) + face (+ stroke, gloss) + label (+ optional icon on the left of the label).
    /// The face is the raycast target; <see cref="PressButton"/> paints it from <see cref="UiKit"/>.
    /// </summary>
    static Button KButton(string name, Transform parent, UiStyle style, string key, float w, float h, float fontSize = 18f, Sprite icon = null, bool display = false, int radius = 12)
    {
        var root = Rect(name, parent);
        root.sizeDelta = new Vector2(w, h);
        var lipS = radius <= 12 ? kit.r12 : kit.r20;
        var lip = KImg("Lip", root, lipS, Color.black, true);
        Fill(lip); lip.rectTransform.offsetMax = new Vector2(0f, -4f); lip.rectTransform.offsetMin = new Vector2(0f, -4f);
        var faceRt = Rect("Face", root);
        Fill(faceRt);
        var face = KImg("Bg", faceRt, lipS, Color.white, true);
        Fill(face);
        face.raycastTarget = true;
        var stroke = KImg("Stroke", faceRt, radius <= 12 ? kit.r12Stroke : kit.r20Stroke, Color.white, true);
        Fill(stroke);
        var gloss = KImg("Gloss", faceRt, kit.gloss, Color.white, true, 2f);
        Fill(gloss);
        var content = Rect("Content", faceRt);
        Fill(content);
        var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hl.childAlignment = TextAnchor.MiddleCenter; hl.spacing = fontSize * 0.45f;
        hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
        hl.padding = new RectOffset(12, 12, 0, 0);
        Image ic = null;
        if (icon)
        {
            ic = KImg("Icon", content, icon, Color.white);
            ic.preserveAspect = true;
            var le = ic.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = le.preferredHeight = fontSize * 1.15f;
        }
        var label = KTxt("Label", content, display ? F.Display : F.Bold, fontSize, Color.white, TextAlignmentOptions.Center, key);
        if (!display) label.characterSpacing = 4f;
        var b = root.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.None;
        b.targetGraphic = face;
        var pb = root.gameObject.AddComponent<PressButton>();
        pb.face = faceRt; pb.faceImage = face; pb.lip = lip; pb.stroke = stroke; pb.gloss = gloss; pb.label = label; pb.icon = ic; pb.style = style;
        // Paint once in the editor so the saved scene shows the right colours.
        var look = UiKit.For(style);
        face.color = look.face; lip.color = look.lip; lip.enabled = look.hasLip; stroke.color = look.stroke; stroke.enabled = look.hasStroke; gloss.enabled = look.gloss;
        label.color = look.text; if (ic) ic.color = look.text;
        return b;
    }

    /// <summary>Coloured icon (coin / gem) on a button: keep its own colours.</summary>
    static void KeepIcon(Button b)
    {
        var pb = b.GetComponent<PressButton>();
        pb.keepIconColor = true;
        if (pb.icon) pb.icon.color = Color.white;
    }

    static UiText LabelOf(Button b) => b.GetComponent<PressButton>().label as UiText;

    /// <summary>Square icon button (back arrow, close X, settings gear, pause).</summary>
    static Button KIconButton(string name, Transform parent, Sprite icon, float size, UiStyle style = UiStyle.Dark)
    {
        var b = KButton(name, parent, style, null, size, size, size * 0.42f, icon);
        var pb = b.GetComponent<PressButton>();
        Object.DestroyImmediate(pb.label.gameObject);
        pb.label = null;
        var le = pb.icon.GetComponent<LayoutElement>();
        le.preferredWidth = le.preferredHeight = size * 0.48f;
        pb.icon.color = UiKit.For(style).text;
        return b;
    }

    /// <summary>Currency pill: icon, right-aligned value, optional yellow "+" button.</summary>
    static UiText KCurrency(string name, Transform parent, Sprite icon, float w, float h, out Button plus)
    {
        var p = KImg(name, parent, kit.pill, UiKit.Hex(0x0E120C, 0.9f), true, 2f);
        p.rectTransform.sizeDelta = new Vector2(w, h);
        var s = KImg("Stroke", p.transform, kit.pillStroke, UiKit.Line, true, 2f);
        Fill(s);
        var ic = KIcon("Icon", p.transform, icon, Color.white, h * 0.56f);
        In(ic, h * 0.24f, h * 0.22f, h * 0.56f, h * 0.56f);
        var v = KTxt("Value", p.transform, F.Display, h * 0.42f, UiKit.Text, TextAlignmentOptions.Right);
        plus = null;
        const float right = 14f;
        Fill(v);
        v.rectTransform.offsetMin = new Vector2(h, 0f);
        v.rectTransform.offsetMax = new Vector2(-right, 0f);
        v.text = "0";
        return v;
    }

    static UiText KCurrencyPlus(string name, Transform parent, Sprite icon, float w, float h, out Button plus)
    {
        var v = KCurrency(name, parent, icon, w, h, out _);
        var p = v.transform.parent;
        plus = KIconButton("Plus", p, kit.plus, h - 10f, UiStyle.Yellow);
        InR(plus, 5f, 5f, h - 10f, h - 10f);
        plus.GetComponent<PressButton>().depth = 2f;
        v.rectTransform.offsetMax = new Vector2(-(h + 4f), 0f);
        return v;
    }

    /// <summary>Thin progress bar: trough + fill (anchored by anchorMax.x).</summary>
    static RectTransform KBar(string name, Transform parent, Color fill, out Image trough, float h = 8f)
    {
        trough = KImg(name, parent, kit.r12, UiKit.Hex(0x232B1E), true, 6f);
        trough.rectTransform.sizeDelta = new Vector2(100f, h);
        var f = KImg("Fill", trough.transform, kit.r12, fill, true, 6f);
        var r = f.rectTransform;
        r.anchorMin = Vector2.zero; r.anchorMax = new Vector2(0.5f, 1f); r.offsetMin = r.offsetMax = Vector2.zero; r.pivot = new Vector2(0f, 0.5f);
        return r;
    }

    /// <summary>Level pips (5 small bars) for upgrades / bonuses.</summary>
    static Image[] KPips(Transform parent, int n, float w, float h, float gap)
    {
        var arr = new Image[n];
        for (int i = 0; i < n; i++)
        {
            var p = KImg("Pip" + i, parent, kit.r12, UiKit.Hex(0x2A3324), true, 6f);
            In(p, i * (w + gap), 0f, w, h);
            arr[i] = p;
        }
        return arr;
    }

    /// <summary>Header used by full-screen menus: back button, title, subtitle.</summary>
    static Button KHeader(Transform parent, string titleKey, string subKey, out TMP_Text title, out TMP_Text sub)
    {
        var back = KIconButton("Back", parent, kit.back, 48f);
        TL(back, Safe, 26f, 48f, 48f);
        title = KTxt("Title", parent, F.Display, 30f, UiKit.Text, TextAlignmentOptions.Left, titleKey);
        TL(title, 120f, 20f, 600f, 40f);
        sub = KTxt("Sub", parent, F.Semi, 16f, UiKit.Secondary, TextAlignmentOptions.Left, subKey);
        TL(sub, 120f, 58f, 600f, 22f);
        return back;
    }

    /// <summary>Menu background: darkens / tints the live 3D scene ("blurred scene at 40 % brightness").</summary>
    static void KBackdrop(Transform parent, float alpha = 0.6f)
    {
        var d = Img("Backdrop", parent, null, new Color(0.04f, 0.035f, 0.025f, alpha));
        Fill(d, -4f); d.type = Image.Type.Simple; d.raycastTarget = false;
        var v = Img("Vignette", parent, kit.soft, new Color(0f, 0f, 0f, 0f));
        Fill(v);
    }
}
