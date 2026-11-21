using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// UI art generated as simple vector shapes (signed distance fields): rounded 9-slice panels with a thick dark
/// contour and inner shadow, glow, dots, and one icon per upgrade card (white two-tone fill + dark outline, tinted
/// at runtime). Kenney's CC0 icon packs could not be downloaded in this environment, so every icon is drawn here.
/// </summary>
public static partial class GameSceneBuilder
{
    const string UiArt = "Assets/Art/UI";

    /// <summary>Theme: cartoon / military — khaki green with orange accents, cream text, near-black contours.</summary>
    // Redesign palette (docs/ui-ref StyleGuide) under the old names, so older layouts follow the new look.
    static readonly Color UiKhaki = new Color(0.11f, 0.14f, 0.098f);
    static readonly Color UiKhakiDark = new Color(0.067f, 0.082f, 0.059f);
    static readonly Color UiOrange = new Color(1f, 0.698f, 0.118f);
    static readonly Color UiCream = new Color(0.949f, 0.929f, 0.882f);
    static readonly Color UiInk = new Color(0.043f, 0.051f, 0.035f);

    class UiSprites
    {
        public Sprite panel, flat, shadow, glow, dot, dotEmpty, shine, ringThick;
        public Sprite heart, star, skull;
        public Sprite[] cards;
    }

    static UiSprites GenerateUiSprites()
    {
        Directory.CreateDirectory(UiArt);
        Directory.CreateDirectory(UiArt + "/Icons");
        var s = new UiSprites();
        // Rounded panel: dark contour, inner shadow along the top, white body (tinted by Image.color).
        s.panel = WriteSliced("UI_Panel", 96, 30, (x, y) =>
        {
            float d = RoundBox(x - 48f, y - 48f, 44f, 44f, 20f);
            float outer = Mathf.Clamp01(0.5f - d);
            float inner = Mathf.Clamp01(0.5f - (d + 6f));
            float topShadow = Mathf.Clamp01(1f - (92f - 6f - y) / 10f) * 0.35f; // inner shadow under the top contour
            float bottomShade = Mathf.Clamp01(1f - (y - 6f) / 14f) * 0.12f;
            float body = 1f - topShadow - bottomShade;
            Color c = Color.Lerp(new Color(UiInk.r, UiInk.g, UiInk.b, 1f), new Color(body, body, body, 1f), inner);
            c.a = outer;
            return c;
        });
        s.flat = WriteSliced("UI_Flat", 64, 20, (x, y) =>
        {
            float d = RoundBox(x - 32f, y - 32f, 30f, 30f, 14f);
            return new Color(1, 1, 1, Mathf.Clamp01(0.5f - d));
        });
        s.shadow = WriteSliced("UI_Shadow", 96, 40, (x, y) =>
        {
            float d = RoundBox(x - 48f, y - 48f, 30f, 30f, 18f);
            float a = Mathf.Clamp01(1f - (d + 6f) / 16f);
            return new Color(0, 0, 0, a * a * 0.8f);
        });
        s.glow = WriteSpriteAt("UI_Glow", 128, (u, v) =>
        {
            float r = Dist(u, v) * 2f; float a = Mathf.Clamp01(1f - r); return new Color(1, 1, 1, a * a);
        }, Vector4.zero);
        s.dot = WriteSpriteAt("UI_Dot", 64, (u, v) =>
        {
            float r = Dist(u, v) * 64f;
            float outer = Mathf.Clamp01(30.5f - r), inner = Mathf.Clamp01(25f - r);
            float hi = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.4f, 0.62f)) * 6f) * 0.35f;
            float b = Mathf.Min(1f, 0.9f + hi);
            return new Color(Mathf.Lerp(UiInk.r, b, inner), Mathf.Lerp(UiInk.g, b, inner), Mathf.Lerp(UiInk.b, b, inner), outer);
        }, Vector4.zero);
        s.dotEmpty = WriteSpriteAt("UI_DotEmpty", 64, (u, v) =>
        {
            float r = Dist(u, v) * 64f;
            float outer = Mathf.Clamp01(30.5f - r), inner = Mathf.Clamp01(25f - r);
            return new Color(UiInk.r, UiInk.g, UiInk.b, Mathf.Max(outer - inner, inner * 0.35f));
        }, Vector4.zero);
        s.shine = WriteSpriteAt("UI_Shine", 64, (u, v) =>
        {
            float a = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f); return new Color(1, 1, 1, a * a * 0.8f);
        }, Vector4.zero);
        s.ringThick = WriteSpriteAt("UI_RingThick", 128, (u, v) =>
        {
            float r = Dist(u, v) * 128f;
            return new Color(1, 1, 1, Mathf.Clamp01(62.5f - r) * Mathf.Clamp01(r - 46f));
        }, Vector4.zero);

        s.heart = Icon("Icon_Heart", p => Heart(p, 0.9f));
        s.star = Icon("Icon_Star", p => Star(p, 5, 0.92f, 0.42f, 0f));
        s.skull = Icon("Icon_Skull", Skull);

        var ids = (CardId[])Enum.GetValues(typeof(CardId));
        s.cards = new Sprite[ids.Length];
        foreach (var id in ids) s.cards[(int)id] = Icon("Icon_" + id, CardIcon(id));
        return s;
    }

    // ---------------------------------------------------------------- card icons (p in [-1,1]², y up; < 0 = inside)
    static Func<Vector2, float> CardIcon(CardId id)
    {
        switch (id)
        {
            case CardId.Pierce:
                // A big round flying right through two thin plates.
                return p => Min(Bullet(Rot(p - new Vector2(0.02f, 0f), -90f), 0.27f, 1.75f),
                    Mathf.Max(RoundBox(p.x + 0.42f, p.y, 0.05f, 0.82f, 0.03f), -(Mathf.Abs(p.y) - 0.36f)),
                    Mathf.Max(RoundBox(p.x - 0.12f, p.y, 0.05f, 0.82f, 0.03f), -(Mathf.Abs(p.y) - 0.36f)));
            case CardId.FireRate:
                return p => Min(Bullet(Rot(p - new Vector2(0.28f, 0f), -90f), 0.24f, 1.2f),
                    Seg(p, new Vector2(-0.9f, 0.32f), new Vector2(-0.45f, 0.32f), 0.08f),
                    Seg(p, new Vector2(-0.95f, 0f), new Vector2(-0.4f, 0f), 0.08f),
                    Seg(p, new Vector2(-0.9f, -0.32f), new Vector2(-0.45f, -0.32f), 0.08f));
            case CardId.BigMag:
                return p =>
                {
                    Vector2 q = Rot(p - new Vector2(-0.18f, -0.05f), 12f);
                    float mag = RoundBox(q.x, q.y + 0.1f, 0.3f, 0.68f, 0.1f);
                    float tip = Bullet(q - new Vector2(0f, 0.62f), 0.15f, 0.55f);
                    float plus = Min(RoundBox(p.x - 0.58f, p.y - 0.5f, 0.3f, 0.09f, 0.04f), RoundBox(p.x - 0.58f, p.y - 0.5f, 0.09f, 0.3f, 0.04f));
                    return Min(mag, tip, plus);
                };
            case CardId.FastReload:
                return p =>
                {
                    float ring = Mathf.Abs(p.magnitude - 0.6f) - 0.13f;
                    float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
                    float gap = (ang > 55f && ang < 115f) ? 1f : -1f;           // opening at the top
                    ring = Mathf.Max(ring, gap * 0.2f);
                    // Arrow head at the gap's right end, pointing along the circle (counter-clockwise).
                    float head = Tri(p, new Vector2(0.0f, 0.72f), new Vector2(0.52f, 0.74f), new Vector2(0.17f, 0.24f));
                    return Min(ring, head);
                };
            case CardId.DoubleShot:
                return p => Min(Bullet(p - new Vector2(-0.33f, 0f), 0.22f, 1.5f), Bullet(p - new Vector2(0.33f, 0f), 0.22f, 1.5f));
            case CardId.Crit:
                return p => Min(Mathf.Abs(p.magnitude - 0.6f) - 0.1f, p.magnitude - 0.14f,
                    Seg(p, new Vector2(0f, 0.45f), new Vector2(0f, 0.95f), 0.08f), Seg(p, new Vector2(0f, -0.45f), new Vector2(0f, -0.95f), 0.08f),
                    Seg(p, new Vector2(0.45f, 0f), new Vector2(0.95f, 0f), 0.08f), Seg(p, new Vector2(-0.45f, 0f), new Vector2(-0.95f, 0f), 0.08f));
            case CardId.KillBlast:
                return p => Star(p, 9, 0.95f, 0.52f, 10f);
            case CardId.MaxHealth:
                return p => Mathf.Max(Shield(p), -Plus(p - new Vector2(0f, 0.08f), 0.38f, 0.12f));
            case CardId.Lifesteal:
                return p => Min(Heart(p - new Vector2(-0.12f, 0.08f), 0.75f), Drop(p - new Vector2(0.58f, -0.45f), 0.26f));
            case CardId.MoveSpeed:
                return p => Poly(p, new[] { new Vector2(0.2f, 0.95f), new Vector2(-0.55f, -0.05f), new Vector2(-0.05f, -0.05f), new Vector2(-0.25f, -0.95f), new Vector2(0.55f, 0.15f), new Vector2(0.05f, 0.15f) }) - 0.02f;
            case CardId.Magnet:
                return p =>
                {
                    Vector2 q = p - new Vector2(0f, 0.15f);
                    float arc = Mathf.Max(Mathf.Abs(q.magnitude - 0.5f) - 0.2f, -q.y);
                    float legs = Min(RoundBox(q.x + 0.5f, q.y + 0.45f, 0.2f, 0.45f, 0.03f), RoundBox(q.x - 0.5f, q.y + 0.45f, 0.2f, 0.45f, 0.03f));
                    float body = Min(arc, legs);
                    float cut = Min(RoundBox(q.x + 0.5f, q.y + 0.72f, 0.3f, 0.03f, 0f), RoundBox(q.x - 0.5f, q.y + 0.72f, 0.3f, 0.03f, 0f));
                    return Mathf.Max(body, -cut);
                };
            case CardId.Shotgun:
                // A shell on the left, a fan of pellets flying right.
                return p => Min(Shell(Rot(p - new Vector2(-0.5f, -0.05f), -90f) * 0.95f),
                    (p - new Vector2(0.38f, 0.48f)).magnitude - 0.15f,
                    (p - new Vector2(0.55f, 0f)).magnitude - 0.15f,
                    (p - new Vector2(0.38f, -0.48f)).magnitude - 0.15f,
                    (p - new Vector2(0.82f, 0.28f)).magnitude - 0.11f,
                    (p - new Vector2(0.82f, -0.28f)).magnitude - 0.11f);
            case CardId.Turret:
                return p => Min(Poly(p, new[] { new Vector2(-0.7f, -0.92f), new Vector2(0.7f, -0.92f), new Vector2(0.38f, -0.6f), new Vector2(-0.38f, -0.6f) }),
                    RoundBox(p.x, p.y + 0.35f, 0.12f, 0.3f, 0.03f),
                    RoundBox(p.x + 0.12f, p.y - 0.1f, 0.48f, 0.3f, 0.12f),
                    Seg(p, new Vector2(0.3f, 0.02f), new Vector2(0.95f, 0.02f), 0.08f),
                    Seg(p, new Vector2(0.3f, -0.22f), new Vector2(0.95f, -0.22f), 0.08f));
            case CardId.Grenade:
                return p => Min((p - new Vector2(0f, -0.2f)).magnitude - 0.6f,
                    RoundBox(p.x, p.y - 0.5f, 0.2f, 0.14f, 0.04f),
                    Seg(p, new Vector2(0.12f, 0.6f), new Vector2(0.55f, 0.25f), 0.07f),
                    Mathf.Abs((p - new Vector2(-0.35f, 0.68f)).magnitude - 0.16f) - 0.06f);
            case CardId.BreatherHeal:
                return p => Min(Mathf.Max(RoundBox(p.x, p.y + 0.12f, 0.82f, 0.6f, 0.18f), -Plus(p - new Vector2(0f, -0.12f), 0.34f, 0.11f)),
                    Mathf.Max(Mathf.Abs(RoundBox(p.x, p.y - 0.55f, 0.32f, 0.22f, 0.12f)) - 0.07f, -(p.y - 0.5f)));
            // ---- 1.1.0
            case CardId.Ricochet:
                // A round coming in from the bottom left, glancing off a wall, flying off up-right.
                return p => Min(RoundBox(p.x + 0.05f, p.y + 0.78f, 0.82f, 0.07f, 0.03f),
                    Seg(p, new Vector2(-0.8f, 0.55f), new Vector2(-0.1f, -0.6f), 0.08f),
                    Bullet(Rot(p - new Vector2(0.42f, 0.12f), -32f), 0.2f, 0.95f),
                    (p - new Vector2(-0.1f, -0.62f)).magnitude - 0.14f);
            case CardId.Shock:
                // Lightning bolt between two small nodes.
                return p => Min(Poly(p, new[] { new Vector2(0.18f, 0.9f), new Vector2(-0.42f, 0.02f), new Vector2(-0.02f, 0.02f), new Vector2(-0.2f, -0.9f), new Vector2(0.45f, 0.06f), new Vector2(0.05f, 0.06f) }) - 0.02f,
                    Mathf.Abs((p - new Vector2(-0.68f, 0.62f)).magnitude - 0.16f) - 0.05f,
                    Mathf.Abs((p - new Vector2(0.68f, -0.62f)).magnitude - 0.16f) - 0.05f);
            case CardId.Frost:
                // Six-armed snowflake with little branches.
                return p =>
                {
                    float d = 9f;
                    for (int k = 0; k < 3; k++)
                    {
                        Vector2 q = Rot(p, k * 60f);
                        d = Min(d, Seg(q, new Vector2(0f, -0.88f), new Vector2(0f, 0.88f), 0.08f),
                            Seg(q, new Vector2(0f, 0.5f), new Vector2(0.25f, 0.75f), 0.06f), Seg(q, new Vector2(0f, 0.5f), new Vector2(-0.25f, 0.75f), 0.06f),
                            Seg(q, new Vector2(0f, -0.5f), new Vector2(0.25f, -0.75f), 0.06f), Seg(q, new Vector2(0f, -0.5f), new Vector2(-0.25f, -0.75f), 0.06f));
                    }
                    return d;
                };
            case CardId.Mines:
                // Flat mine: wide base, dome, trigger pin.
                return p => Min(RoundBox(p.x, p.y + 0.45f, 0.85f, 0.16f, 0.08f),
                    Mathf.Max((p - new Vector2(0f, -0.32f)).magnitude - 0.6f, -(p.y + 0.3f)),
                    RoundBox(p.x, p.y - 0.42f, 0.09f, 0.2f, 0.03f),
                    (p - new Vector2(0f, 0.68f)).magnitude - 0.11f);
            case CardId.Drone:
                // Quad-copter from above: body, four arms, rotor rings.
                return p =>
                {
                    float d = RoundBox(p.x, p.y, 0.24f, 0.24f, 0.08f);
                    d = Min(d, Seg(p, new Vector2(-0.55f, -0.55f), new Vector2(0.55f, 0.55f), 0.07f), Seg(p, new Vector2(-0.55f, 0.55f), new Vector2(0.55f, -0.55f), 0.07f));
                    foreach (var c in new[] { new Vector2(0.6f, 0.6f), new Vector2(-0.6f, 0.6f), new Vector2(0.6f, -0.6f), new Vector2(-0.6f, -0.6f) })
                        d = Min(d, Mathf.Abs((p - c).magnitude - 0.27f) - 0.05f);
                    return d;
                };
            case CardId.Adrenaline:
                // Syringe, needle up-right.
                return p =>
                {
                    Vector2 q = Rot(p, 45f);
                    return Min(RoundBox(q.x, q.y + 0.05f, 0.2f, 0.42f, 0.05f), Seg(q, new Vector2(0f, 0.45f), new Vector2(0f, 0.95f), 0.04f),
                        RoundBox(q.x, q.y + 0.55f, 0.32f, 0.05f, 0.02f), RoundBox(q.x, q.y + 0.75f, 0.06f, 0.2f, 0.02f), RoundBox(q.x, q.y + 0.95f, 0.22f, 0.05f, 0.02f));
                };
            case CardId.Armor:
                // Shield with two horizontal plate bands cut out.
                return p => Mathf.Max(Shield(p), -Min(RoundBox(p.x, p.y - 0.18f, 0.6f, 0.06f, 0.02f), RoundBox(p.x, p.y + 0.2f, 0.5f, 0.06f, 0.02f)));
        }
        return p => p.magnitude - 0.7f;
    }

    static float Bullet(Vector2 p, float r, float len)
    {
        // Pointing +Y: casing box, rounded ogive tip, rim at the base.
        float half = len * 0.5f;
        float body = RoundBox(p.x, p.y + 0.1f * len, r, half * 0.62f, 0.03f);
        float tip = (p - new Vector2(0f, half * 0.48f)).magnitude - r;
        float nose = Tri(p, new Vector2(-r, half * 0.48f), new Vector2(r, half * 0.48f), new Vector2(0f, half));
        float rim = RoundBox(p.x, p.y + half * 0.78f, r * 1.15f, 0.06f, 0.02f);
        return Min(body, Mathf.Min(tip, nose) - 0.01f, rim);
    }

    static float Shell(Vector2 p)
    {
        float body = RoundBox(p.x, p.y + 0.05f, 0.22f, 0.62f, 0.12f);
        float brass = RoundBox(p.x, p.y + 0.68f, 0.26f, 0.14f, 0.04f);
        return Min(body, brass);
    }

    static float Heart(Vector2 p, float s)
    {
        // iq's heart spans x ±0.6, y 0..1.1 → map [-s,s] onto it, centred.
        const float k = 0.62f;
        p = p * (k / s) + new Vector2(0f, 0.52f);
        p.x = Mathf.Abs(p.x);
        float d;
        if (p.y + p.x > 1f) d = (p - new Vector2(0.25f, 0.75f)).magnitude - Mathf.Sqrt(2f) / 4f;
        else d = Mathf.Min((p - new Vector2(0f, 1f)).magnitude, (p - 0.5f * Mathf.Max(p.x + p.y, 0f) * Vector2.one).magnitude) * Mathf.Sign(p.x - p.y);
        return d * s / k;
    }

    static float Drop(Vector2 p, float r) => Min(p.magnitude - r, Tri(p, new Vector2(-r * 0.92f, r * 0.3f), new Vector2(r * 0.92f, r * 0.3f), new Vector2(0f, r * 2.3f)));

    static float Shield(Vector2 p)
    {
        float top = RoundBox(p.x, p.y - 0.3f, 0.72f, 0.55f, 0.12f);
        float bottom = Tri(p, new Vector2(-0.72f, -0.18f), new Vector2(0.72f, -0.18f), new Vector2(0f, -0.95f)) - 0.04f;
        return Mathf.Min(top, bottom);
    }

    static float Skull(Vector2 p)
    {
        float head = (p - new Vector2(0f, 0.15f)).magnitude - 0.68f;
        float jaw = RoundBox(p.x, p.y + 0.55f, 0.38f, 0.3f, 0.12f);
        float s = Mathf.Min(head, jaw);
        float eyes = Mathf.Min((p - new Vector2(-0.26f, 0.12f)).magnitude - 0.18f, (p - new Vector2(0.26f, 0.12f)).magnitude - 0.18f);
        float nose = Tri(p, new Vector2(-0.08f, -0.2f), new Vector2(0.08f, -0.2f), new Vector2(0f, -0.05f));
        return Mathf.Max(s, -Mathf.Min(eyes, nose));
    }

    static float Plus(Vector2 p, float len, float w) => Mathf.Min(RoundBox(p.x, p.y, len, w, 0.03f), RoundBox(p.x, p.y, w, len, 0.03f));

    static float Star(Vector2 p, int n, float ro, float ri, float rotDeg)
    {
        var v = new Vector2[n * 2];
        for (int i = 0; i < n * 2; i++)
        {
            float a = (90f + rotDeg + i * 180f / n) * Mathf.Deg2Rad;
            float r = i % 2 == 0 ? ro : ri;
            v[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return Poly(p, v);
    }

    // ---------------------------------------------------------------- SDF primitives
    static float Min(params float[] d) { float m = d[0]; for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]); return m; }

    static Vector2 Rot(Vector2 p, float deg)
    {
        float a = -deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
        return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
    }

    static float RoundBox(float x, float y, float hx, float hy, float r)
    {
        float qx = Mathf.Abs(x) - hx + r, qy = Mathf.Abs(y) - hy + r;
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }

    static float Tri(Vector2 p, Vector2 a, Vector2 b, Vector2 c) => Poly(p, new[] { a, b, c });

    /// <summary>Exact signed distance to a simple polygon (Inigo Quilez).</summary>
    static float Poly(Vector2 p, Vector2[] v)
    {
        float d = Vector2.Dot(p - v[0], p - v[0]);
        float s = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            Vector2 e = v[j] - v[i], w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
        }
        return s * Mathf.Sqrt(d);
    }

    // ---------------------------------------------------------------- writers
    /// <summary>128 px icon: white fill with a lighter top / darker bottom (two-tone cartoon) and a thick dark outline.</summary>
    static Sprite Icon(string name, Func<Vector2, float> sdf)
    {
        const int size = 128;
        const float outline = 0.13f;
        float px = 2f / size;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var c = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Leave room for the outline: the shape space is shrunk slightly.
                Vector2 p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f) * 1.16f;
                float d = sdf(p);
                float fill = Mathf.Clamp01(0.5f - d / px);
                float all = Mathf.Clamp01(0.5f - (d - outline) / px);
                float shade = p.y < -0.05f + 0.25f * p.x ? 0.8f : 1f;
                float rim = Mathf.Clamp01(0.5f - (d + 0.09f) / px); // inner highlight band inset
                shade = Mathf.Lerp(shade * 0.92f, shade, rim);
                Color col = Color.Lerp(UiInk, new Color(shade, shade, shade), fill);
                col.a = all;
                c[y * size + x] = col;
            }
        tex.SetPixels(c);
        string path = $"{UiArt}/Icons/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return ImportSprite(path, Vector4.zero, 128);
    }

    delegate Color PixelPx(float x, float y);

    static Sprite WriteSliced(string name, int size, int border, PixelPx fn)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var c = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) c[y * size + x] = fn(x + 0.5f, y + 0.5f);
        tex.SetPixels(c);
        string path = $"{UiArt}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return ImportSprite(path, new Vector4(border, border, border, border), size);
    }

    static Sprite WriteSpriteAt(string name, int size, PixelFn fn, Vector4 border)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var c = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++) c[y * size + x] = fn((x + 0.5f) / size, (y + 0.5f) / size);
        tex.SetPixels(c);
        string path = $"{UiArt}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return ImportSprite(path, border, size);
    }

    static Sprite ImportSprite(string path, Vector4 border, int maxSize)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.spriteBorder = border;
        imp.maxTextureSize = maxSize;
        SetMobileCompression(imp, maxSize);
        // UI sprites: ASTC 4x4 keeps the thin contours clean.
        foreach (var platform in new[] { "Android", "iPhone" })
        {
            var ps = imp.GetPlatformTextureSettings(platform);
            ps.format = TextureImporterFormat.ASTC_4x4;
            imp.SetPlatformTextureSettings(ps);
        }
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
