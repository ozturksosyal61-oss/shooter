using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 1.1.0 cosmetics: gun finish materials (palette per role) + bullet tracer looks → <c>Resources/Cosmetics.asset</c>
/// (<see cref="CosmeticLibrary"/>), and the <see cref="GunSkin"/> component on every gun model. Materials live in
/// MatDir so <see cref="Warmup"/> pre-compiles them.
/// </summary>
public static partial class GameSceneBuilder
{
    const string CosmeticsPath = "Assets/Resources/Cosmetics.asset";

    struct SkinLook
    {
        public GunSkinId id;
        public Color dark, mid, light, accent;
        public float smooth;      // > 0: glossy specular (carbon, chrome, gold)
        public Color spec;
        public Color glow;        // emission on the light role (toxic)
    }

    static readonly SkinLook[] SkinLooks =
    {
        new SkinLook { id = GunSkinId.Desert, dark = C(0.33f, 0.26f, 0.18f), mid = C(0.66f, 0.55f, 0.38f), light = C(0.78f, 0.69f, 0.52f), accent = C(0.42f, 0.31f, 0.19f) },
        new SkinLook { id = GunSkinId.Woodland, dark = C(0.11f, 0.13f, 0.08f), mid = C(0.26f, 0.33f, 0.17f), light = C(0.44f, 0.41f, 0.26f), accent = C(0.31f, 0.22f, 0.12f) },
        new SkinLook { id = GunSkinId.Arctic, dark = C(0.5f, 0.54f, 0.58f), mid = C(0.86f, 0.88f, 0.9f), light = C(0.96f, 0.97f, 0.98f), accent = C(0.55f, 0.65f, 0.74f) },
        new SkinLook { id = GunSkinId.Crimson, dark = C(0.12f, 0.04f, 0.04f), mid = C(0.6f, 0.08f, 0.07f), light = C(0.82f, 0.18f, 0.12f), accent = C(0.18f, 0.18f, 0.2f) },
        new SkinLook { id = GunSkinId.Ocean, dark = C(0.04f, 0.09f, 0.17f), mid = C(0.1f, 0.3f, 0.58f), light = C(0.3f, 0.58f, 0.85f), accent = C(0.07f, 0.11f, 0.2f) },
        new SkinLook { id = GunSkinId.Carbon, dark = C(0.025f, 0.025f, 0.03f), mid = C(0.07f, 0.07f, 0.08f), light = C(0.13f, 0.13f, 0.15f), accent = C(0.95f, 0.45f, 0.08f), smooth = 0.75f, spec = C(0.5f, 0.5f, 0.55f) },
        new SkinLook { id = GunSkinId.Toxic, dark = C(0.05f, 0.07f, 0.04f), mid = C(0.18f, 0.62f, 0.12f), light = C(0.55f, 1f, 0.2f), accent = C(0.09f, 0.11f, 0.07f), glow = C(0.35f, 0.9f, 0.1f) },
        new SkinLook { id = GunSkinId.Chrome, dark = C(0.3f, 0.31f, 0.35f), mid = C(0.72f, 0.74f, 0.78f), light = C(0.92f, 0.93f, 0.96f), accent = C(0.18f, 0.18f, 0.2f), smooth = 0.9f, spec = C(1f, 1f, 1f) },
        new SkinLook { id = GunSkinId.Gold, dark = C(0.42f, 0.29f, 0.07f), mid = C(0.86f, 0.65f, 0.18f), light = C(1f, 0.86f, 0.42f), accent = C(0.25f, 0.18f, 0.06f), smooth = 0.8f, spec = C(1f, 0.85f, 0.45f) },
    };

    static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

    // Tracer colour (HDR-ish: additive), width multiplier, flicker — by TracerId.
    static readonly (Color color, float width, bool flicker)[] TracerLooks =
    {
        (new Color(1f, 0.85f, 0.45f, 1f), 1f, false),     // Standard (the old M_Bullet)
        (new Color(1f, 0.25f, 0.2f, 1f), 1.05f, false),   // Red
        (new Color(0.4f, 1f, 0.3f, 1f), 1.05f, false),    // Green
        (new Color(0.3f, 0.65f, 1f, 1f), 1.05f, false),   // Blue
        (new Color(0.8f, 0.4f, 1f, 1f), 1.1f, false),     // Purple
        (new Color(1f, 0.5f, 0.12f, 1f), 1.45f, true),    // Fire
        (new Color(0.45f, 0.95f, 1f, 1f), 1.8f, false),   // Plasma
        (new Color(1f, 0.82f, 0.3f, 1f), 1.5f, false),    // Golden
        (new Color(1f, 1f, 1f, 1f), 1.25f, false),        // Rainbow (per-hue materials below)
    };

    static void BuildCosmetics(Mats m)
    {
        System.IO.Directory.CreateDirectory("Assets/Resources");
        var lib = AssetDatabase.LoadAssetAtPath<CosmeticLibrary>(CosmeticsPath);
        if (!lib) { lib = ScriptableObject.CreateInstance<CosmeticLibrary>(); AssetDatabase.CreateAsset(lib, CosmeticsPath); }

        int skins = System.Enum.GetValues(typeof(GunSkinId)).Length;
        lib.skinMats = new Material[skins * GunSkin.Roles];
        lib.skinColors = new Color[skins];
        lib.skinColors[0] = new Color(0.22f, 0.23f, 0.25f);
        string[] roleNames = { "Dark", "Mid", "Light", "Accent" };
        foreach (var s in SkinLooks)
        {
            Color[] cols = { s.dark, s.mid, s.light, s.accent };
            lib.skinColors[(int)s.id] = s.mid;
            for (int r = 0; r < GunSkin.Roles; r++)
            {
                var look = s; int role = r;
                lib.skinMats[(int)s.id * GunSkin.Roles + r] = Mat($"M_GunSkin_{s.id}_{roleNames[r]}", "Universal Render Pipeline/Simple Lit", x =>
                {
                    x.SetTexture("_BaseMap", null);
                    x.SetColor("_BaseColor", cols[role]);
                    if (look.smooth > 0f)
                    {
                        x.SetFloat("_SpecularHighlights", 1f); x.SetColor("_SpecColor", look.spec); x.SetFloat("_Smoothness", look.smooth);
                        x.EnableKeyword("_SPECULAR_COLOR");
                    }
                    if (look.glow.maxColorComponent > 0f && role == 2)
                    {
                        x.EnableKeyword("_EMISSION");
                        x.SetColor("_EmissionColor", look.glow);
                        x.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                });
            }
        }

        var tex = m.bullet.GetTexture("_BaseMap");
        int tn = System.Enum.GetValues(typeof(TracerId)).Length;
        lib.tracerMats = new Material[tn];
        lib.tracerWidth = new float[tn];
        lib.tracerFlicker = new bool[tn];
        lib.tracerColors = new Color[tn];
        for (int i = 0; i < tn; i++)
        {
            var l = TracerLooks[i];
            lib.tracerMats[i] = i == 0 ? m.bullet : TracerMat("M_Tracer_" + (TracerId)i, tex, l.color);
            lib.tracerWidth[i] = l.width;
            lib.tracerFlicker[i] = l.flicker;
            lib.tracerColors[i] = l.color;
        }
        lib.rainbowMats = new Material[6];
        for (int h = 0; h < 6; h++) lib.rainbowMats[h] = TracerMat("M_Tracer_Rainbow" + h, tex, Color.HSVToRGB(h / 6f, 0.75f, 1f));
        lib.tracerColors[(int)TracerId.Rainbow] = Color.HSVToRGB(0.8f, 0.6f, 1f);
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
    }

    static Material TracerMat(string name, Texture tex, Color c) =>
        Mat(name, "Game/FxAdditive", x => { x.SetTexture("_BaseMap", tex); x.SetColor("_BaseColor", c); x.SetFloat("_VertexColor", 0f); });

    /// <summary>Gun-finish role of a source material (M_Gun_* / M_Rifle_*): 0 dark · 1 mid · 2 light · 3 accent · -1 keep.</summary>
    static int SkinRole(string matName)
    {
        if (matName.Contains("Glass")) return -1;
        if (matName.Contains("Wood") || matName.Contains("Green")) return 3;
        if (matName.Contains("Light")) return 2;
        if (matName.Contains("Black") || matName.Contains("Dark")) return 0;
        return 1;
    }

    /// <summary>Adds a <see cref="GunSkin"/> to a built gun (all its mesh renderers, roles from the material names).</summary>
    static void AttachGunSkin(Transform root)
    {
        var gs = root.gameObject.AddComponent<GunSkin>();
        var rs = new List<Renderer>(); var roles = new List<int>(); var orig = new List<Material>();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name == "MuzzleFlash" || r.transform.parent && r.transform.parent.name == "Muzzle") continue;
            rs.Add(r);
            foreach (var mat in r.sharedMaterials) { orig.Add(mat); roles.Add(mat ? SkinRole(mat.name) : -1); }
        }
        gs.renderers = rs.ToArray(); gs.roles = roles.ToArray(); gs.originals = orig.ToArray();
    }

    /// <summary>Shop tracer preview: three bullet quads (side-on to the camera), positioned by <see cref="PreviewStage"/>.</summary>
    static Renderer[] PreviewTracers(Transform stage, Mats m)
    {
        var qs = new Renderer[3];
        for (int i = 0; i < 3; i++)
        {
            var q = Quad("PreviewTracer" + i, stage, m.bullet);
            q.transform.localRotation = Quaternion.Euler(0f, 180f, -90f); // length (local +Y) → stage -X, facing the camera
            q.SetActive(false);
            qs[i] = q.GetComponent<Renderer>();
        }
        return qs;
    }
}
