using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// F8 city variety + story objectives: the ground atlas (roads, crossings, curbs, district / park / plaza / rubble
/// interiors), chunk-type prefabs (trees, benches, fences, statue, fountain, bus stop, gas station, police station,
/// hospital, signal tower, helipad, wrecks, ruins), objective props (flare, supply crate, helicopter), district moods
/// and the objective HUD.
/// </summary>
public static partial class GameSceneBuilder
{
    // ---------------------------------------------------------------- ground atlas
    static float PNoise(float x, float y, int p)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        int W(int v) => ((v % p) + p) % p;
        float a = Hash(W(xi), W(yi)), b = Hash(W(xi + 1), W(yi)), c = Hash(W(xi), W(yi + 1)), d = Hash(W(xi + 1), W(yi + 1));
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Grain(float a, float b) => 0.93f + 0.07f * Hash(Mathf.FloorToInt(a * 46f), Mathf.FloorToInt(b * 46f) + 9173);

    static Color Asphalt(float a, float b, bool cracked)
    {
        float blot = PNoise(a * 0.8f, b * 0.8f, 4);
        var c = new Color(0.23f, 0.24f, 0.27f) * (0.9f + 0.15f * blot) * Grain(a, b);
        if (blot > 0.78f) c *= 0.85f;
        if (cracked) c = Crack(c, a, b);
        return c;
    }

    static Color Crack(Color c, float a, float b)
    {
        float n = PNoise(a * 1.2f, b * 1.2f, 6);
        if (Mathf.Abs(n - 0.5f) < 0.022f) c *= 0.45f;
        float pot = PNoise(a * 0.6f + 7f, b * 0.6f + 3f, 3);
        if (pot > 0.72f) c = Color.Lerp(c, new Color(0.3f, 0.26f, 0.22f), 0.6f) * (0.85f + 0.15f * Hash(Mathf.FloorToInt(a * 20f), Mathf.FloorToInt(b * 20f)));
        return c;
    }

    static Color Curb(float a, float b) => new Color(0.72f, 0.72f, 0.7f) * Grain(a, b);

    static Color SidewalkTile(float a, float b)
    {
        var c = new Color(0.62f, 0.6f, 0.57f) * Grain(a, b);
        if (Mathf.Repeat(a, 1f) < 0.05f || Mathf.Repeat(b, 1f) < 0.05f) c *= 0.82f;
        return c;
    }

    /// <summary>Road half (centre line at b = 0) + curb + sidewalk. a = along (5 m period), b = across 0..5.5.</summary>
    static Color StripPixel(float a, float b, bool zebra, bool cracked)
    {
        if (b >= 3.75f) return SidewalkTile(a, b);
        if (b >= 3.5f) return Curb(a, b);
        var c = Asphalt(a, b, cracked);
        bool inZebra = zebra && (a >= 3.7f || a < 0.2f);
        if (b < 0.1f && Mathf.Repeat(a, 2.5f) < 1.4f && !inZebra) c = new Color(0.95f, 0.78f, 0.2f) * (cracked ? 0.8f : 1f);
        if (b > 3.12f && b < 3.27f && !inZebra) c = new Color(0.85f, 0.85f, 0.82f) * (cracked ? 0.75f : 1f);
        if (inZebra && b < 3.0f && Mathf.Repeat(b, 0.9f) < 0.45f) c = new Color(0.88f, 0.88f, 0.85f);
        return c;
    }

    static Color CornerPixel(float a, float b, bool node, bool cracked)
    {
        bool road = node ? a < 3.5f && b < 3.5f : a < 3.5f || b < 3.5f;
        if (road) return Asphalt(a, b, cracked);
        bool curb = node ? a < 3.75f && b < 3.75f : a < 3.75f || b < 3.75f;
        return curb ? Curb(a, b) : SidewalkTile(a, b);
    }

    static Color InteriorPixel(int cell, float a, float b)
    {
        float g = Grain(a, b);
        switch (cell)
        {
            case GroundAtlas.Cadde:
            {
                var c = new Color(0.56f, 0.56f, 0.54f) * g;
                if (Mathf.Repeat(a, 2.5f) < 0.06f || Mathf.Repeat(b, 2.5f) < 0.06f) c *= 0.85f;
                return c;
            }
            case GroundAtlas.Carsi:
            {
                float row = Mathf.Floor(b / 0.3125f);
                float bx = a + (row % 2f) * 0.3125f;
                var c = Color.Lerp(new Color(0.66f, 0.38f, 0.28f), new Color(0.58f, 0.32f, 0.24f), Hash(Mathf.FloorToInt(Mathf.Repeat(bx, 5f) / 0.625f), (int)row)) * g;
                if (Mathf.Repeat(b, 0.3125f) < 0.03f || Mathf.Repeat(bx, 0.625f) < 0.03f) c = new Color(0.5f, 0.45f, 0.4f);
                return c;
            }
            case GroundAtlas.Sanayi:
            {
                var c = new Color(0.43f, 0.43f, 0.41f) * (0.85f + 0.25f * PNoise(a * 0.8f, b * 0.8f, 4)) * g;
                if (PNoise(a * 0.8f + 10f, b * 0.8f, 4) > 0.72f) c *= 0.65f; // oil stains
                return c;
            }
            case GroundAtlas.Liman:
            {
                var c = new Color(0.52f, 0.52f, 0.5f) * (0.9f + 0.15f * PNoise(a * 0.8f, b * 0.8f, 4)) * g;
                if (Mathf.Repeat(a, 2.5f) < 0.1f) c = new Color(0.9f, 0.75f, 0.2f);
                if (PNoise(a * 0.6f, b * 0.6f + 30f, 3) > 0.75f) c = Color.Lerp(c, new Color(0.55f, 0.35f, 0.2f), 0.5f); // rust
                return c;
            }
            case GroundAtlas.Park:
            {
                float n = PNoise(a * 0.8f, b * 0.8f, 4), m = PNoise(a * 2.4f + 3f, b * 2.4f, 12);
                var c = Color.Lerp(new Color(0.3f, 0.47f, 0.2f), new Color(0.38f, 0.56f, 0.24f), n) * (0.9f + 0.15f * m) * g;
                if (Hash(Mathf.FloorToInt(a * 9f), Mathf.FloorToInt(b * 9f) + 31) > 0.985f) c = new Color(0.95f, 0.85f, 0.35f); // flowers
                if (PNoise(a * 0.4f + 9f, b * 0.4f, 2) > 0.7f) c = Color.Lerp(c, new Color(0.45f, 0.4f, 0.28f), 0.45f);    // worn patches
                return c;
            }
            case GroundAtlas.Plaza:
            {
                float ta = Mathf.Floor(a / 1.25f), tb = Mathf.Floor(b / 1.25f);
                bool alt = ((int)(ta + tb)) % 2 == 0;
                var c = (alt ? new Color(0.76f, 0.71f, 0.62f) : new Color(0.66f, 0.62f, 0.55f)) * g;
                if (Mathf.Repeat(a, 1.25f) < 0.04f || Mathf.Repeat(b, 1.25f) < 0.04f) c *= 0.78f;
                return c;
            }
            case GroundAtlas.Rubble:
            {
                float n = PNoise(a * 0.8f, b * 0.8f, 4);
                var c = Color.Lerp(new Color(0.42f, 0.37f, 0.31f), new Color(0.5f, 0.5f, 0.47f), Mathf.SmoothStep(0.35f, 0.65f, n)) * g;
                c = Crack(c, a, b);
                if (PNoise(a * 0.4f + 5f, b * 0.4f + 5f, 2) > 0.68f) c *= 0.6f; // scorch
                if (Hash(Mathf.FloorToInt(a * 6f), Mathf.FloorToInt(b * 6f) + 77) > 0.93f) c = new Color(0.58f, 0.56f, 0.52f) * g; // grit
                return c;
            }
            case GroundAtlas.Alley:
            {
                float sa = a / 0.5f, sb = b / 0.5f;
                float row = Mathf.Floor(sb);
                float fa = Mathf.Repeat(sa + (row % 2f) * 0.5f, 1f), fb = sb - row;
                float edge = Mathf.Min(Mathf.Min(fa, 1f - fa), Mathf.Min(fb, 1f - fb));
                var c = new Color(0.42f, 0.4f, 0.38f) * (0.85f + 0.25f * Hash(Mathf.FloorToInt(Mathf.Repeat(sa + (row % 2f) * 0.5f, 10f)), (int)row)) * g;
                if (edge < 0.09f) c *= 0.6f;
                return c;
            }
            default: // Lot
            {
                var c = new Color(0.6f, 0.6f, 0.58f) * (0.92f + 0.1f * PNoise(a * 0.8f, b * 0.8f, 4)) * g;
                if (Mathf.Repeat(a, 2.5f) < 0.04f || Mathf.Repeat(b, 2.5f) < 0.04f) c *= 0.8f;
                if (PNoise(a * 1.2f + 4f, b * 1.2f, 6) > 0.76f) c *= 0.72f; // oil
                return c;
            }
        }
    }

    static Color AtlasPixel(int cell, float a, float b)
    {
        switch (cell)
        {
            case GroundAtlas.Strip: return StripPixel(a, b, false, false);
            case GroundAtlas.StripZebra: return StripPixel(a, b, true, false);
            case GroundAtlas.StripCracked: return StripPixel(a, b, false, true);
            case GroundAtlas.Corner: return CornerPixel(a, b, false, false);
            case GroundAtlas.CornerNode: return CornerPixel(a, b, true, false);
            case GroundAtlas.CornerCracked: return CornerPixel(a, b, false, true);
            case GroundAtlas.NodeCracked: return CornerPixel(a, b, true, true);
            default: return InteriorPixel(cell, a, b);
        }
    }

    /// <summary>1024² atlas, 4x4 cells with 8 px gutters (wrapped for tiling cells, clamped for the rest).</summary>
    static Texture2D PaintGroundAtlas()
    {
        const int N = GroundAtlas.AtlasPx, C = GroundAtlas.CellPx, G = GroundAtlas.Gutter;
        const float inner = C - 2 * G;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color[N * N];
        for (int cell = 0; cell < GroundAtlas.Grid * GroundAtlas.Grid; cell++)
        {
            int cx = cell % GroundAtlas.Grid, cy = cell / GroundAtlas.Grid;
            bool strip = cell <= GroundAtlas.StripCracked;
            bool corner = cell == GroundAtlas.Corner || cell == GroundAtlas.CornerNode || cell >= GroundAtlas.CornerCracked;
            for (int y = 0; y < C; y++)
                for (int x = 0; x < C; x++)
                {
                    float tu = (x - G + 0.5f) / inner, tv = (y - G + 0.5f) / inner;
                    float a, b;
                    if (corner) { a = Mathf.Clamp01(tu) * 5.5f; b = Mathf.Clamp01(tv) * 5.5f; }
                    else if (strip) { a = Mathf.Repeat(tu, 1f) * 5f; b = Mathf.Clamp01(tv) * 5.5f; }
                    else { a = Mathf.Repeat(tu, 1f) * 5f; b = Mathf.Repeat(tv, 1f) * 5f; }
                    var c = AtlasPixel(cell, a, b);
                    c.a = 1f;
                    px[(cy * C + y) * N + cx * C + x] = c;
                }
        }
        tex.SetPixels(px);
        string path = $"{ArtTex}/Ground_Atlas.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.mipmapEnabled = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.anisoLevel = 4;
        imp.maxTextureSize = 1024;
        SetMobileCompression(imp, 1024);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void GenerateCityTextures(Tex t)
    {
        foreach (District d in System.Enum.GetValues(typeof(District)))
            AssetDatabase.DeleteAsset($"{ArtTex}/Ground_{d}.png");
        t.groundAtlas = PaintGroundAtlas();
        // Light pillar over the objective: bright at the base, fading up, soft sides.
        t.beam = WriteTex("FX_Beam", 64, (u, v) =>
        {
            float across = Mathf.Clamp01(1f - Mathf.Abs(u - 0.5f) * 2f);
            float a = Mathf.Pow(across, 1.6f) * Mathf.Pow(1f - v, 1.4f);
            return new Color(1, 1, 1, a);
        }, false, TextureWrapMode.Clamp, 64);
        // Helipad: dark disc, yellow ring, white H.
        t.helipad = WriteTex("Env_Helipad", 256, (u, v) =>
        {
            float r = Dist(u, v) * 2f;
            if (r > 0.98f) return new Color(0, 0, 0, 0);
            var c = new Color(0.22f, 0.26f, 0.24f);
            if (r > 0.82f && r < 0.92f) c = new Color(0.98f, 0.8f, 0.15f);
            float x = (u - 0.5f) * 2f, y = (v - 0.5f) * 2f;
            bool h = (Mathf.Abs(x) > 0.26f && Mathf.Abs(x) < 0.4f && Mathf.Abs(y) < 0.5f) || (Mathf.Abs(x) < 0.4f && Mathf.Abs(y) < 0.07f);
            if (h) c = new Color(0.95f, 0.95f, 0.92f);
            return new Color(c.r, c.g, c.b, Mathf.Clamp01((0.98f - r) * 60f));
        }, true, TextureWrapMode.Clamp, 256);
        t.redCross = WriteTex("Env_RedCross", 64, (u, v) =>
        {
            float x = Mathf.Abs(u - 0.5f), y = Mathf.Abs(v - 0.5f);
            bool cross = (x < 0.12f && y < 0.36f) || (y < 0.12f && x < 0.36f);
            return cross ? new Color(0.85f, 0.1f, 0.1f) : new Color(0.96f, 0.96f, 0.94f);
        }, true, TextureWrapMode.Clamp, 64);
    }

    // ---------------------------------------------------------------- materials
    class CityMats
    {
        public Material ground, white, red, steel, dark, glass, concrete, water, khaki, stone, yellow, blue, orange;
        public Material burntCar, burntShop, policeShop, hospitalShop, helipad, redCross, beam, zoneRing, zoneFill, spray, canopyCut, stoneCut, bulb;
    }

    static CityMats CreateCityMaterials(Mats m, Tex t)
    {
        const string simpleLit = "Universal Render Pipeline/Simple Lit";
        foreach (District d in System.Enum.GetValues(typeof(District))) AssetDatabase.DeleteAsset($"{MatDir}/M_Ground_{d}.mat");
        var c = new CityMats();
        Material Solid(string n, Color col) => Mat(n, simpleLit, x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", col); });
        Material Tinted(string n, string kit, Color col) => Mat(n, simpleLit, x => { x.SetTexture("_BaseMap", m.kit[kit].GetTexture("_BaseMap")); x.SetColor("_BaseColor", col); });
        Material Add(string n, Texture tex, Color col) => Mat(n, "Game/FxAdditive", x => { x.SetTexture("_BaseMap", tex); x.SetColor("_BaseColor", col); x.SetFloat("_VertexColor", 0f); });
        c.ground = Mat("M_Ground", simpleLit, x => { x.SetTexture("_BaseMap", t.groundAtlas); x.SetColor("_BaseColor", Color.white); });
        c.white = Solid("M_City_White", new Color(0.92f, 0.92f, 0.9f));
        c.red = Solid("M_City_Red", new Color(0.82f, 0.16f, 0.12f));
        c.steel = Solid("M_City_Steel", new Color(0.55f, 0.57f, 0.6f));
        c.dark = Solid("M_City_Dark", new Color(0.15f, 0.15f, 0.16f));
        c.glass = Solid("M_City_Glass", new Color(0.32f, 0.42f, 0.5f));
        c.concrete = Solid("M_City_Concrete", new Color(0.7f, 0.68f, 0.64f));
        c.water = Mat("M_City_Water", simpleLit, x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.3f, 0.6f, 0.8f)); x.SetFloat("_Smoothness", 0.9f); });
        c.khaki = Solid("M_Heli_Khaki", new Color(0.33f, 0.4f, 0.22f));
        c.stone = Solid("M_City_Stone", new Color(0.64f, 0.62f, 0.58f));
        c.yellow = Solid("M_City_Yellow", new Color(0.95f, 0.74f, 0.15f));
        c.blue = Solid("M_City_Blue", new Color(0.2f, 0.36f, 0.78f));
        c.orange = Solid("M_City_Orange", new Color(0.95f, 0.5f, 0.12f));
        c.burntCar = Tinted("M_Env_BurntCar", "Cars", new Color(0.3f, 0.27f, 0.25f));
        c.burntShop = Tinted("M_Env_BurntShop", "Commercial", new Color(0.46f, 0.43f, 0.4f));
        c.policeShop = Tinted("M_Env_Police", "Commercial", new Color(0.62f, 0.74f, 1f));
        c.hospitalShop = Tinted("M_Env_Hospital", "Commercial", new Color(0.92f, 1f, 0.95f));
        c.helipad = Mat("M_Env_Helipad", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", t.helipad); x.SetColor("_BaseColor", Color.white); x.SetFloat("_VertexColor", 0f); });
        c.redCross = Mat("M_Env_RedCross", simpleLit, x => { x.SetTexture("_BaseMap", t.redCross); x.SetColor("_BaseColor", Color.white); });
        c.beam = Add("M_Obj_Beam", t.beam, Color.white);
        c.zoneRing = Add("M_Obj_ZoneRing", t.ringTex, Color.white);
        c.zoneFill = Add("M_Obj_ZoneFill", t.soft, Color.white);
        c.spray = Add("M_Obj_Spray", t.soft, new Color(0.6f, 0.85f, 1f, 0.6f));
        c.bulb = Add("M_City_Bulb", t.soft, new Color(1f, 0.25f, 0.15f, 1f));
        c.canopyCut = Mat("M_EnvCut_Canopy", "Game/EnvCutout", x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.9f)); x.SetColor("_RimColor", new Color(1f, 0.9f, 0.55f)); });
        c.stoneCut = Mat("M_EnvCut_Stone", "Game/EnvCutout", x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.64f, 0.62f, 0.58f)); x.SetColor("_RimColor", new Color(1f, 0.9f, 0.55f)); });
        return c;
    }

    // ---------------------------------------------------------------- prefabs
    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3 euler = default) =>
        Prim(PrimitiveType.Cube, name, parent, pos, size, mat, euler);

    static GameObject Cyl(string name, Transform parent, Vector3 pos, float radius, float height, Material mat, Vector3 euler = default) =>
        Prim(PrimitiveType.Cylinder, name, parent, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat, euler);

    static void Col(GameObject root, Vector3 center, Vector3 size)
    {
        var b = root.AddComponent<BoxCollider>();
        b.center = center; b.size = size;
    }

    static GameObject Procedural(string name, float radius, int layer, Surface surface, System.Action<GameObject> build, bool shadows = true)
    {
        var root = new GameObject(name);
        build(root);
        root.AddComponent<EnvProp>().radius = radius;
        root.GetComponent<EnvProp>().surface = surface;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
            if (r.shadowCastingMode != ShadowCastingMode.Off) r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        SetLayer(root, layer);
        return SavePrefab(root, $"{EnvPrefabDir}/{name}.prefab");
    }

    static void TrunkCollider(GameObject root, float height)
    {
        Object.DestroyImmediate(root.GetComponent<BoxCollider>());
        Col(root, new Vector3(0f, height * 0.5f, 0f), new Vector3(0.55f, height, 0.55f));
    }

    /// <summary>A static stone statue of the soldier (idle pose baked from the rigged mesh), or null if baking fails.</summary>
    static Mesh BakeSoldierStatue()
    {
        try
        {
            string clipPath = "Assets/Art/Characters/Soldier/Anim/idle.fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            var src = LoadGo(SoldierRigged);
            var inst = (GameObject)Object.Instantiate(src);
            var anim = inst.GetComponent<Animator>() ?? inst.AddComponent<Animator>();
            if (clip) { AnimationMode.StartAnimationMode(); AnimationMode.SampleAnimationClip(inst, clip, 0.4f); }
            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh { name = "StatueSoldier" };
            smr.BakeMesh(mesh);
            if (clip) AnimationMode.StopAnimationMode();
            // Bake into the root's space (BakeMesh uses the renderer's transform).
            var mtx = inst.transform.worldToLocalMatrix * Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            var v = mesh.vertices; var n = mesh.normals;
            for (int i = 0; i < v.Length; i++) { v[i] = mtx.MultiplyPoint3x4(v[i]); if (i < n.Length) n[i] = mtx.MultiplyVector(n[i]).normalized; }
            mesh.vertices = v; mesh.normals = n;
            mesh.RecalculateBounds();
            Debug.Log($"BUILD: statue mesh {mesh.vertexCount} verts, bounds {mesh.bounds.size}");
            Object.DestroyImmediate(inst);
            string path = "Assets/Art/Environment/StatueSoldier.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
        catch (System.Exception e)
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            Debug.LogWarning("BUILD: statue bake failed, plaza uses the obelisk only: " + e.Message);
            return null;
        }
    }

    static CityKit BuildCityPrefabs(Mats m, CityMats c, Env env)
    {
        const int V = GameLayers.Vaultable, B = GameLayers.Blocker;
        var k = new CityKit();
        k.dumpster = env.dumpster;
        k.crate = env.crate;
        k.policeCar = env.cars.First(x => x.name == "Car_police");

        // Park
        k.trees = new[]
        {
            EnvPrefab(m, "Suburban", "tree-large", "Tree_Large", Fit.Height, 5.6f, 1f, B, true, fadeable: true, radiusScale: 0.55f, post: (r, rs) => TrunkCollider(r, 5f)),
            EnvPrefab(m, "Suburban", "tree-small", "Tree_Small", Fit.Height, 4.3f, 1f, B, true, fadeable: true, radiusScale: 0.55f, post: (r, rs) => TrunkCollider(r, 4f)),
            EnvPrefab(m, "Survival", "tree-tall", "Tree_Pine", Fit.Height, 5.8f, 1f, B, true, fadeable: true, radiusScale: 0.5f, post: (r, rs) => TrunkCollider(r, 5f)),
            EnvPrefab(m, "Survival", "tree", "Tree_Round", Fit.Height, 4.8f, 1f, B, true, fadeable: true, radiusScale: 0.5f, post: (r, rs) => TrunkCollider(r, 4.5f)),
        };
        k.benches = new[]
        {
            EnvPrefab(m, "Graveyard", "bench", "Bench", Fit.Length, 1.9f, 1f, V, true),
            EnvPrefab(m, "Graveyard", "bench-damaged", "BenchBroken", Fit.Length, 1.9f, 1f, V, true),
        };
        k.fences = new[] { EnvPrefab(m, "Suburban", "fence", "ParkFence", Fit.Length, 1.85f, 1f, V, true) };
        k.planters = new[] { EnvPrefab(m, "Suburban", "planter", "Planter", Fit.Length, 1.6f, 1f, V, true) };
        k.lamps = new[] { EnvPrefab(m, "Graveyard", "lightpost-single", "ParkLamp", Fit.Height, 3.6f, 1f, 0, false, radiusScale: 0.3f), env.streetLight };

        // Rubble
        k.rocks = new[]
        {
            EnvPrefab(m, "Graveyard", "rocks", "RubbleRocks", Fit.Length, 2.3f, 1.3f, V, true),
            EnvPrefab(m, "Survival", "rock-b", "RubbleRock", Fit.Length, 1.7f, 1f, V, true),
        };
        k.lowWalls = new[] { EnvPrefab(m, "Graveyard", "stone-wall-damaged", "BrokenWall", Fit.Length, 3.2f, 0.62f, V, true) };
        k.debris = new[]
        {
            EnvPrefab(m, "Graveyard", "debris", "Debris_Stone", Fit.Length, 1.4f, 1f, 0, false),
            EnvPrefab(m, "Graveyard", "debris-wood", "Debris_Wood", Fit.Length, 1.3f, 1f, 0, false),
            EnvPrefab(m, "Cars", "debris-plate-a", "Debris_Plate", Fit.Length, 1.2f, 1f, 0, false),
            EnvPrefab(m, "Cars", "debris-door", "Debris_Door", Fit.Length, 1.3f, 1f, 0, false),
            EnvPrefab(m, "Cars", "debris-bumper", "Debris_Bumper", Fit.Length, 1.4f, 1f, 0, false),
            EnvPrefab(m, "Survival", "resource-planks", "Debris_Planks", Fit.Length, 1.4f, 1f, 0, false),
            EnvPrefab(m, "Survival", "metal-panel", "Debris_Panel", Fit.Length, 1.2f, 1f, 0, false),
        };
        k.wrecks = new[] { "sedan", "suv", "van" }
            .Select(n => EnvPrefab(m, "Cars", n, "Wreck_" + n, Fit.Length, 4.0f, 0.62f, V, true, surface: Surface.Metal, matOverride: c.burntCar))
            .Append(EnvPrefab(m, "Cars", "truck", "Wreck_truck", Fit.Length, 5.2f, 0.8f, B, true, surface: Surface.Metal, matOverride: c.burntCar))
            .ToArray();
        k.ruins = new[] { "c", "d" }
            .Select(n => EnvPrefab(m, "Commercial", "building-" + n, "Ruin_" + n, Fit.Footprint, 8.2f, 0.42f, B, true, matOverride: c.burntShop))
            .ToArray();

        // Alley + landmarks
        k.alleyBuildings = new[] { "a", "b", "e", "h" }
            .Select(n => EnvPrefab(m, "Commercial", "building-" + n, "AlleyShop_" + n, Fit.Footprint, 6.8f, 1.25f, B, true, fadeable: true))
            .ToArray();
        k.trafficLights = new[] { EnvPrefab(m, "Roads", "traffic-light", "TrafficLight", Fit.Height, 4.2f, 1f, 0, false, radiusScale: 0.3f) };
        k.policeBuilding = EnvPrefab(m, "Commercial", "building-b", "PoliceStation", Fit.Footprint, 6.6f, 1f, B, true, fadeable: true, matOverride: c.policeShop,
            post: (root, rs) =>
            {
                // Blue light bar on the roof edge.
                float top = rs.Max(r => r.bounds.max.y);
                Box("LightBar", root.transform, new Vector3(0f, top + 0.15f, -2.2f), new Vector3(2.4f, 0.3f, 0.4f), c.blue);
            });
        k.hospitalBuilding = EnvPrefab(m, "Commercial", "building-e", "Hospital", Fit.Footprint, 6.6f, 1f, B, true, fadeable: true, matOverride: c.hospitalShop);
        k.ambulance = EnvPrefab(m, "Cars", "ambulance", "Ambulance", Fit.Length, 5.0f, 0.85f, B, true, surface: Surface.Metal);
        k.floodLight = EnvPrefab(m, "Roads", "construction-light", "FloodLight", Fit.Height, 2.3f, 1f, 0, false, radiusScale: 0.3f);

        k.busStop = Procedural("BusStop", 1.7f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            Box("Back", t, new Vector3(0f, 1.15f, 0.6f), new Vector3(3f, 2.1f, 0.08f), c.glass);
            Box("Roof", t, new Vector3(0f, 2.3f, 0.15f), new Vector3(3.3f, 0.12f, 1.5f), c.white);
            Box("PostL", t, new Vector3(-1.5f, 1.15f, 0.6f), new Vector3(0.1f, 2.3f, 0.1f), c.steel);
            Box("PostR", t, new Vector3(1.5f, 1.15f, 0.6f), new Vector3(0.1f, 2.3f, 0.1f), c.steel);
            Box("Bench", t, new Vector3(0f, 0.48f, 0.32f), new Vector3(2.3f, 0.1f, 0.42f), c.dark);
            Cyl("SignPole", t, new Vector3(1.95f, 1.3f, -0.2f), 0.05f, 2.6f, c.steel);
            Box("Sign", t, new Vector3(1.95f, 2.45f, -0.2f), new Vector3(0.55f, 0.55f, 0.06f), c.blue);
            Col(root, new Vector3(0f, 1.1f, 0.6f), new Vector3(3.1f, 2.2f, 0.25f));
        });
        k.gasCanopy = Procedural("GasCanopy", 3.6f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            var roof = Box("Roof", t, new Vector3(0f, 4.3f, 0f), new Vector3(7.4f, 0.35f, 5.2f), c.white);
            var band = Box("Band", t, new Vector3(0f, 4.05f, 0f), new Vector3(7.5f, 0.22f, 5.3f), c.red);
            foreach (var x in new[] { -3.2f, 3.2f })
                foreach (var z in new[] { -2.1f, 2.1f })
                {
                    Box("Post", t, new Vector3(x, 2.05f, z), new Vector3(0.35f, 4.1f, 0.35f), c.white);
                    Col(root, new Vector3(x, 1.2f, z), new Vector3(0.45f, 2.4f, 0.45f));
                }
            Box("Island", t, new Vector3(0f, 0.08f, 0f), new Vector3(5f, 0.16f, 1.3f), c.concrete);
            foreach (var x in new[] { -1.6f, 1.6f })
            {
                Box("Pump", t, new Vector3(x, 0.85f, 0f), new Vector3(0.9f, 1.4f, 0.6f), c.red);
                Box("PumpTop", t, new Vector3(x, 1.65f, 0f), new Vector3(0.95f, 0.25f, 0.65f), c.white);
                Box("Screen", t, new Vector3(x, 1.25f, -0.31f), new Vector3(0.5f, 0.3f, 0.02f), c.dark);
                Col(root, new Vector3(x, 0.9f, 0f), new Vector3(1f, 1.8f, 0.7f));
            }
            // Roof collider only for the camera's occluder fade (far above bullets, zombies and the player).
            Col(root, new Vector3(0f, 4.3f, 0f), new Vector3(7.4f, 0.4f, 5.2f));
            var f = root.AddComponent<Fadeable>();
            f.renderers = new Renderer[] { roof.GetComponent<Renderer>(), band.GetComponent<Renderer>() };
            f.fadeMaterial = c.canopyCut;
        });
        k.gasSign = Procedural("GasSign", 0.5f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            Cyl("Pole", t, new Vector3(0f, 1.6f, 0f), 0.12f, 3.2f, c.steel);
            Box("Panel", t, new Vector3(0f, 3.6f, 0f), new Vector3(1.4f, 1.1f, 0.22f), c.red);
            Box("Price", t, new Vector3(0f, 3.4f, -0.12f), new Vector3(1.1f, 0.35f, 0.02f), c.white);
            Box("Logo", t, new Vector3(0f, 3.85f, -0.12f), new Vector3(0.5f, 0.3f, 0.02f), c.yellow);
            Col(root, new Vector3(0f, 1.3f, 0f), new Vector3(0.3f, 2.6f, 0.3f));
        });
        k.hospitalSign = Procedural("HospitalSign", 0.4f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            Cyl("Pole", t, new Vector3(0f, 1.4f, 0f), 0.07f, 2.8f, c.steel);
            Box("Panel", t, new Vector3(0f, 3f, 0f), new Vector3(1.3f, 1.3f, 0.14f), c.white);
            foreach (float z in new[] { -0.075f, 0.075f })
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = "Cross";
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(t, false);
                q.transform.localPosition = new Vector3(0f, 3f, z * 1.02f);
                q.transform.localRotation = Quaternion.Euler(0f, z < 0f ? 0f : 180f, 0f);
                q.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
                q.GetComponent<MeshRenderer>().sharedMaterial = c.redCross;
            }
            Col(root, new Vector3(0f, 1.2f, 0f), new Vector3(0.25f, 2.4f, 0.25f));
        });
        k.fountain = Procedural("Fountain", 2.6f, B, Surface.Concrete, root =>
        {
            var t = root.transform;
            Cyl("Basin", t, new Vector3(0f, 0.3f, 0f), 2.3f, 0.6f, c.stone);
            Cyl("Water", t, new Vector3(0f, 0.52f, 0f), 2.05f, 0.04f, c.water);
            Cyl("Column", t, new Vector3(0f, 0.9f, 0f), 0.28f, 1.2f, c.stone);
            Cyl("Bowl", t, new Vector3(0f, 1.5f, 0f), 0.85f, 0.18f, c.stone);
            Cyl("BowlWater", t, new Vector3(0f, 1.6f, 0f), 0.72f, 0.03f, c.water);
            Cyl("Spout", t, new Vector3(0f, 1.85f, 0f), 0.12f, 0.5f, c.stone);
            var ps = new GameObject("Spray").AddComponent<ParticleSystem>();
            ps.transform.SetParent(t, false);
            ps.transform.localPosition = new Vector3(0f, 2.05f, 0f);
            ps.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var main = ps.main;
            main.loop = true; main.playOnAwake = true; main.maxParticles = 40;
            main.startLifetime = 0.9f; main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f); main.gravityModifier = 0.9f;
            main.startColor = new Color(0.75f, 0.9f, 1f, 0.7f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 30f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.05f;
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = c.spray; pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;
            var cap = root.AddComponent<CapsuleCollider>();
            cap.center = new Vector3(0f, 1.2f, 0f); cap.radius = 2.3f; cap.height = 4.6f;
        });

        var statueMesh = BakeSoldierStatue();
        var statues = new List<GameObject>
        {
            Procedural("Obelisk", 1.5f, B, Surface.Concrete, root =>
            {
                var t = root.transform;
                Box("Pedestal", t, new Vector3(0f, 0.5f, 0f), new Vector3(2.2f, 1f, 2.2f), c.stone);
                Box("Step", t, new Vector3(0f, 0.1f, 0f), new Vector3(2.8f, 0.2f, 2.8f), c.concrete);
                var shaft = Box("Shaft", t, new Vector3(0f, 3.2f, 0f), new Vector3(0.8f, 4.4f, 0.8f), c.stone);
                Box("Tip", t, new Vector3(0f, 5.6f, 0f), new Vector3(0.45f, 0.45f, 0.45f), c.stone, new Vector3(45f, 0f, 45f));
                Col(root, new Vector3(0f, 2.8f, 0f), new Vector3(2.2f, 5.6f, 2.2f));
                var f = root.AddComponent<Fadeable>();
                f.renderers = new Renderer[] { shaft.GetComponent<Renderer>() };
                f.fadeMaterial = c.stoneCut;
            }),
        };
        if (statueMesh)
            statues.Add(Procedural("StatueSoldier", 1.5f, B, Surface.Concrete, root =>
            {
                var t = root.transform;
                Box("Pedestal", t, new Vector3(0f, 0.7f, 0f), new Vector3(1.8f, 1.4f, 1.8f), c.stone);
                Box("Plinth", t, new Vector3(0f, 1.45f, 0f), new Vector3(2f, 0.12f, 2f), c.concrete);
                Box("Step", t, new Vector3(0f, 0.1f, 0f), new Vector3(2.6f, 0.2f, 2.6f), c.concrete);
                var fig = new GameObject("Figure");
                fig.transform.SetParent(t, false);
                fig.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                fig.transform.localScale = Vector3.one * 1.45f;
                fig.AddComponent<MeshFilter>().sharedMesh = statueMesh;
                var mr = fig.AddComponent<MeshRenderer>();
                mr.sharedMaterial = c.stone;
                Col(root, new Vector3(0f, 2.3f, 0f), new Vector3(1.8f, 4.6f, 1.8f));
                var f = root.AddComponent<Fadeable>();
                f.renderers = new Renderer[] { mr };
                f.fadeMaterial = c.stoneCut;
            }));
        k.statues = statues.ToArray();

        // Objective sites
        k.signalTower = Procedural("SignalTower", 1.4f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            const float h = 8f, baseHalf = 1.1f, topHalf = 0.35f;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var a = new Vector3(sx * baseHalf, 0f, sz * baseHalf);
                    var b = new Vector3(sx * topHalf, h, sz * topHalf);
                    var leg = Box("Leg", t, (a + b) * 0.5f, new Vector3(0.14f, (b - a).magnitude, 0.14f), c.red);
                    leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                }
            for (int i = 1; i <= 3; i++)
            {
                float y = i * h / 4f, half = Mathf.Lerp(baseHalf, topHalf, y / h);
                Box("BraceX", t, new Vector3(0f, y, half), new Vector3(half * 2f, 0.08f, 0.08f), i % 2 == 0 ? c.white : c.red);
                Box("BraceX", t, new Vector3(0f, y, -half), new Vector3(half * 2f, 0.08f, 0.08f), i % 2 == 0 ? c.white : c.red);
                Box("BraceZ", t, new Vector3(half, y, 0f), new Vector3(0.08f, 0.08f, half * 2f), i % 2 == 0 ? c.white : c.red);
                Box("BraceZ", t, new Vector3(-half, y, 0f), new Vector3(0.08f, 0.08f, half * 2f), i % 2 == 0 ? c.white : c.red);
            }
            Box("Platform", t, new Vector3(0f, h, 0f), new Vector3(1.1f, 0.12f, 1.1f), c.steel);
            Cyl("Mast", t, new Vector3(0f, h + 1.2f, 0f), 0.05f, 2.4f, c.steel);
            Cyl("Dish", t, new Vector3(0.5f, h + 0.7f, 0f), 0.6f, 0.08f, c.white, new Vector3(0f, 0f, 60f));
            var bulb = Quad("Bulb", t, c.bulb);
            bulb.transform.localPosition = new Vector3(0f, h + 2.5f, 0f);
            bulb.transform.localRotation = Quaternion.Euler(57f, 0f, 0f);
            bulb.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            Box("Cabinet", t, new Vector3(0f, 0.6f, -0.9f), new Vector3(0.9f, 1.2f, 0.5f), c.steel);
            Col(root, new Vector3(0f, 2f, 0f), new Vector3(2.0f, 4f, 2.0f));
        });
        k.generator = Procedural("Generator", 0.9f, V, Surface.Metal, root =>
        {
            var t = root.transform;
            Box("Body", t, new Vector3(0f, 0.55f, 0f), new Vector3(1.6f, 1.1f, 1f), c.yellow);
            Box("Top", t, new Vector3(0f, 1.13f, 0f), new Vector3(1.4f, 0.08f, 0.8f), c.dark);
            Box("Grill", t, new Vector3(0f, 0.6f, -0.51f), new Vector3(0.9f, 0.6f, 0.02f), c.dark);
            Col(root, new Vector3(0f, 0.55f, 0f), new Vector3(1.6f, 1.1f, 1f));
        });
        k.helipad = Procedural("Helipad", 5.5f, 0, Surface.Concrete, root =>
        {
            var q = Quad("Pad", root.transform, c.helipad);
            q.transform.localPosition = new Vector3(0f, 0.025f, 0f);
            q.transform.localScale = new Vector3(11f, 11f, 1f);
        }, shadows: false);
        return k;
    }

    // ---------------------------------------------------------------- objective props (not part of the city)
    static GameObject BuildFlarePrefab(CityMats c)
    {
        var root = new GameObject("ObjFlare");
        var t = root.transform;
        Box("Case", t, new Vector3(0f, 0.22f, 0f), new Vector3(0.75f, 0.44f, 0.45f), c.khaki);
        Box("Lid", t, new Vector3(0f, 0.46f, 0f), new Vector3(0.78f, 0.05f, 0.48f), c.dark);
        for (int i = 0; i < 3; i++)
        {
            var s = Cyl("Flare", t, new Vector3(-0.2f + i * 0.2f, 0.55f, 0f), 0.06f, 0.5f, c.red, new Vector3(90f, 0f, 0f));
            Cyl("Cap", t, new Vector3(-0.2f + i * 0.2f, 0.55f, 0.27f), 0.065f, 0.06f, c.white, new Vector3(90f, 0f, 0f));
            s.name = "Flare" + i;
        }
        var glow = Quad("Glow", t, c.bulb);
        glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        glow.transform.localRotation = Quaternion.Euler(57f, 0f, 0f);
        glow.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        return SavePrefab(root, $"{PrefabDir}/ObjFlare.prefab");
    }

    static GameObject BuildSupplyPrefab(CityMats c)
    {
        var root = new GameObject("ObjSupply");
        var t = root.transform;
        Box("Base", t, new Vector3(0f, 0.36f, 0f), new Vector3(1.3f, 0.72f, 0.85f), c.khaki);
        Box("StripeL", t, new Vector3(-0.45f, 0.36f, 0f), new Vector3(0.14f, 0.74f, 0.87f), c.orange);
        Box("StripeR", t, new Vector3(0.45f, 0.36f, 0f), new Vector3(0.14f, 0.74f, 0.87f), c.orange);
        Box("Cross", t, new Vector3(0f, 0.4f, -0.43f), new Vector3(0.36f, 0.12f, 0.02f), c.white);
        Box("Cross", t, new Vector3(0f, 0.4f, -0.43f), new Vector3(0.12f, 0.36f, 0.02f), c.white);
        var lid = new GameObject("Lid").transform;
        lid.SetParent(t, false);
        lid.localPosition = new Vector3(0f, 0.72f, 0.425f);
        Box("LidBox", lid, new Vector3(0f, 0.06f, -0.425f), new Vector3(1.34f, 0.12f, 0.89f), c.khaki);
        Box("LidStripe", lid, new Vector3(0f, 0.125f, -0.425f), new Vector3(1.36f, 0.02f, 0.2f), c.orange);
        return SavePrefab(root, $"{PrefabDir}/ObjSupply.prefab");
    }

    static GameObject BuildHelicopterPrefab(CityMats c)
    {
        var root = new GameObject("Helicopter");
        var fx = root.AddComponent<HelicopterFx>();
        var body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);
        Prim(PrimitiveType.Sphere, "Hull", body, new Vector3(0f, 1.55f, 0f), new Vector3(2.1f, 1.8f, 4f), c.khaki);
        Prim(PrimitiveType.Sphere, "Cockpit", body, new Vector3(0f, 1.75f, 1.45f), new Vector3(1.7f, 1.3f, 1.7f), c.glass);
        Cyl("Boom", body, new Vector3(0f, 1.9f, -3.3f), 0.22f, 3.4f, c.khaki, new Vector3(90f, 0f, 0f));
        Box("Fin", body, new Vector3(0f, 2.5f, -5f), new Vector3(0.1f, 1.2f, 0.8f), c.khaki, new Vector3(-15f, 0f, 0f));
        Box("Stab", body, new Vector3(0f, 1.95f, -4.6f), new Vector3(1.6f, 0.06f, 0.4f), c.khaki);
        foreach (var x in new[] { -0.95f, 0.95f })
        {
            Cyl("Skid", body, new Vector3(x, 0.08f, 0.2f), 0.06f, 3.2f, c.dark, new Vector3(90f, 0f, 0f));
            Box("Strut", body, new Vector3(x * 0.85f, 0.45f, 0.9f), new Vector3(0.08f, 0.8f, 0.08f), c.dark);
            Box("Strut", body, new Vector3(x * 0.85f, 0.45f, -0.6f), new Vector3(0.08f, 0.8f, 0.08f), c.dark);
        }
        Box("Star", body, new Vector3(1.03f, 1.6f, -0.2f), new Vector3(0.02f, 0.5f, 0.5f), c.white, new Vector3(0f, 0f, 0f));
        var rotor = new GameObject("MainRotor").transform;
        rotor.SetParent(body, false);
        rotor.localPosition = new Vector3(0f, 2.6f, 0f);
        Cyl("Hub", rotor, Vector3.zero, 0.18f, 0.3f, c.dark);
        Box("BladeA", rotor, new Vector3(0f, 0.12f, 0f), new Vector3(9.5f, 0.05f, 0.32f), c.dark);
        Box("BladeB", rotor, new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.05f, 9.5f), c.dark);
        var tail = new GameObject("TailRotor").transform;
        tail.SetParent(body, false);
        tail.localPosition = new Vector3(0.16f, 2.4f, -5.2f);
        Box("TBladeA", tail, Vector3.zero, new Vector3(0.04f, 1.5f, 0.16f), c.dark);
        Box("TBladeB", tail, Vector3.zero, new Vector3(0.04f, 0.16f, 1.5f), c.dark);
        fx.body = body; fx.mainRotor = rotor; fx.tailRotor = tail;
        return SavePrefab(root, $"{PrefabDir}/Helicopter.prefab");
    }

    static void BuildObjectiveScene(CityMats c)
    {
        new GameObject("ObjectiveDirector").AddComponent<ObjectiveDirector>();
        var vis = new GameObject("ObjectiveVisuals").AddComponent<ObjectiveVisuals>();
        var t = vis.transform;

        var beacon = new GameObject("Beacon").transform;
        beacon.SetParent(t, false);
        var beam = GameObject.CreatePrimitive(PrimitiveType.Quad);
        beam.name = "Beam";
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.transform.SetParent(beacon, false);
        beam.transform.localPosition = new Vector3(0f, 14f, 0f);
        beam.transform.localScale = new Vector3(2.4f, 28f, 1f);
        var br = beam.GetComponent<MeshRenderer>();
        br.sharedMaterial = c.beam; br.shadowCastingMode = ShadowCastingMode.Off; br.receiveShadows = false;
        vis.beacon = beacon; vis.beaconRenderer = br;

        var ring = Quad("ZoneRing", t, c.zoneRing);
        vis.zoneRing = ring.transform; vis.zoneRingRenderer = ring.GetComponent<Renderer>();
        var fill = Quad("ZoneFill", t, c.zoneFill);
        vis.zoneFill = fill.transform; vis.zoneFillRenderer = fill.GetComponent<Renderer>();

        vis.flare = (GameObject)PrefabUtility.InstantiatePrefab(LoadGo($"{PrefabDir}/ObjFlare.prefab"), t);
        vis.supplyCrate = (GameObject)PrefabUtility.InstantiatePrefab(LoadGo($"{PrefabDir}/ObjSupply.prefab"), t);
        vis.supplyLid = vis.supplyCrate.transform.Find("Lid");
        var heli = (GameObject)PrefabUtility.InstantiatePrefab(LoadGo($"{PrefabDir}/Helicopter.prefab"), t);
        vis.helicopter = heli.GetComponent<HelicopterFx>();
    }

    /// <summary>Ambient / sun / fog per district: Cadde warm, Çarşı orange, Sanayi grey-blue, Liman greenish fog.</summary>
    static DistrictMood[] DistrictMoods() => new[]
    {
        new DistrictMood { sky = new Color(0.82f, 0.76f, 0.66f), equator = new Color(0.6f, 0.55f, 0.48f), ground = new Color(0.36f, 0.32f, 0.28f),
            light = new Color(1f, 0.9f, 0.75f), lightIntensity = 1.25f, fog = new Color(0.6f, 0.54f, 0.46f), fogDensity = 0.005f },
        new DistrictMood { sky = new Color(0.9f, 0.7f, 0.5f), equator = new Color(0.68f, 0.5f, 0.36f), ground = new Color(0.38f, 0.28f, 0.2f),
            light = new Color(1f, 0.8f, 0.55f), lightIntensity = 1.2f, fog = new Color(0.68f, 0.5f, 0.32f), fogDensity = 0.008f },
        new DistrictMood { sky = new Color(0.6f, 0.66f, 0.78f), equator = new Color(0.46f, 0.5f, 0.56f), ground = new Color(0.28f, 0.3f, 0.33f),
            light = new Color(0.84f, 0.9f, 1f), lightIntensity = 1.12f, fog = new Color(0.42f, 0.47f, 0.55f), fogDensity = 0.01f },
        new DistrictMood { sky = new Color(0.6f, 0.72f, 0.66f), equator = new Color(0.45f, 0.55f, 0.5f), ground = new Color(0.27f, 0.32f, 0.3f),
            light = new Color(0.86f, 0.96f, 0.88f), lightIntensity = 1.05f, fog = new Color(0.42f, 0.55f, 0.48f), fogDensity = 0.014f },
    };

    // ---------------------------------------------------------------- HUD
    static Sprite[] ObjectiveIcons()
    {
        return new[]
        {
            Icon("Icon_ObjFlare", p => Min(
                RoundBox(Rot(p, -30f).x, Rot(p, -30f).y + 0.25f, 0.17f, 0.62f, 0.08f),
                Drop(Rot(p - new Vector2(0.36f, 0.44f), -30f) * new Vector2(1f, -1f) + new Vector2(0f, 0.05f), 0.22f))),
            Icon("Icon_ObjTower", p => Min(
                Seg(p, new Vector2(-0.6f, -0.9f), new Vector2(-0.05f, 0.45f), 0.1f),
                Seg(p, new Vector2(0.6f, -0.9f), new Vector2(0.05f, 0.45f), 0.1f),
                Seg(p, new Vector2(-0.42f, -0.45f), new Vector2(0.42f, -0.45f), 0.08f),
                Seg(p, new Vector2(-0.25f, 0f), new Vector2(0.25f, 0f), 0.08f),
                (p - new Vector2(0f, 0.58f)).magnitude - 0.15f,
                Mathf.Max(Mathf.Abs((p - new Vector2(0f, 0.58f)).magnitude - 0.42f) - 0.07f, -(p.y - 0.75f)),
                Mathf.Max(Mathf.Abs((p - new Vector2(0f, 0.58f)).magnitude - 0.7f) - 0.07f, -(p.y - 0.95f)))),
            Icon("Icon_ObjSupply", p => Mathf.Max(
                Min(RoundBox(p.x, p.y - 0.05f, 0.85f, 0.62f, 0.1f), RoundBox(p.x, p.y + 0.62f, 0.95f, 0.12f, 0.05f)),
                -Plus(p - new Vector2(0f, -0.05f), 0.36f, 0.11f))),
            Icon("Icon_ObjHeli", p => Min(
                RoundBox(p.x + 0.1f, p.y + 0.05f, 0.5f, 0.32f, 0.3f),
                Seg(p, new Vector2(0.3f, 0.05f), new Vector2(0.92f, 0.18f), 0.09f),
                Seg(p, new Vector2(0.88f, 0.05f), new Vector2(0.92f, 0.38f), 0.07f),
                Seg(p, new Vector2(-0.95f, 0.55f), new Vector2(0.75f, 0.55f), 0.06f),
                Seg(p, new Vector2(-0.1f, 0.35f), new Vector2(-0.1f, 0.55f), 0.07f),
                Seg(p, new Vector2(-0.65f, -0.55f), new Vector2(0.35f, -0.55f), 0.06f),
                Seg(p, new Vector2(-0.4f, -0.3f), new Vector2(-0.45f, -0.55f), 0.05f),
                Seg(p, new Vector2(0.1f, -0.3f), new Vector2(0.15f, -0.55f), 0.05f))),
            Icon("Icon_Radio", p => Mathf.Max(
                Min(RoundBox(p.x, p.y + 0.2f, 0.48f, 0.72f, 0.14f), Seg(p, new Vector2(0.28f, 0.45f), new Vector2(0.28f, 0.98f), 0.08f)),
                -Min(RoundBox(p.x, p.y - 0.05f, 0.28f, 0.04f, 0.02f), RoundBox(p.x, p.y + 0.12f, 0.28f, 0.04f, 0.02f), RoundBox(p.x, p.y - 0.22f, 0.28f, 0.04f, 0.02f),
                    RoundBox(p.x, p.y + 0.58f, 0.18f, 0.12f, 0.04f)))),
        };
    }

    static void BuildObjectiveHud(HUD hud, Tex t)
    {
        var root = hud.transform.Find("Legacy") ?? hud.transform;
        var icons = ObjectiveIcons();
        var obj = new GameObject("Objectives", typeof(RectTransform)).GetComponent<RectTransform>();
        obj.SetParent(root, false);
        Stretch(obj);
        var threats = root.Find("Threats");
        if (threats) obj.SetSiblingIndex(threats.GetSiblingIndex() + 1);
        var oh = obj.gameObject.AddComponent<ObjectiveHUD>();
        oh.kindIcons = icons.Take(4).ToArray();

        // Radio (top right, under the FPS counter).
        var radio = Rect("Radio", obj);
        Anchor(radio, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -74f), new Vector2(760f, 124f));
        var rg = radio.gameObject.AddComponent<CanvasGroup>();
        rg.blocksRaycasts = false; rg.interactable = false;
        var rp = Panel("Panel", radio, new Color(UiKhakiDark.r, UiKhakiDark.g, UiKhakiDark.b, 0.92f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 124f), 6f);
        var rIconBg = Img("IconBg", radio, ui.flat, UiOrange);
        Anchor(rIconBg.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64f, 0f), new Vector2(92f, 92f));
        var rIcon = Img("Icon", radio, icons[4], UiCream);
        Anchor(rIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(64f, 0f), new Vector2(76f, 76f));
        var rLabel = Txt("Label", radio, 22, TextAnchor.UpperLeft, UiOrange);
        Anchor(rLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(124f, -10f), new Vector2(300f, 28f));
        L(rLabel, "radio.label");
        var rText = Txt("Text", radio, 28, TextAnchor.UpperLeft, UiCream);
        Anchor(rText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(124f, -38f), new Vector2(620f, 84f));
        rText.horizontalOverflow = HorizontalWrapMode.Wrap;
        rText.verticalOverflow = VerticalWrapMode.Overflow;
        rText.text = "";
        oh.radioGroup = rg; oh.radioRoot = radio; oh.radioText = rText; oh.radioIcon = rIcon;

        // Tracker under the radio.
        var tr = Rect("Tracker", obj);
        Anchor(tr, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -210f), new Vector2(520f, 88f));
        var tg = tr.gameObject.AddComponent<CanvasGroup>();
        tg.blocksRaycasts = false; tg.interactable = false;
        Panel("Panel", tr, new Color(UiKhaki.r, UiKhaki.g, UiKhaki.b, 0.9f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 88f), 6f);
        var tIcon = Img("Icon", tr, icons[0], Color.white);
        Anchor(tIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(48f, 0f), new Vector2(64f, 64f));
        var tTitle = Txt("Title", tr, 26, TextAnchor.UpperLeft, UiCream);
        Anchor(tTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -10f), new Vector2(330f, 34f));
        tTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
        var tInfo = Txt("Info", tr, 30, TextAnchor.UpperRight, UiGold);
        Anchor(tInfo.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -8f), new Vector2(130f, 38f));
        var pRoot = Img("ProgressBg", tr, ui.flat, new Color(0f, 0f, 0f, 0.45f));
        Anchor(pRoot.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(92f, 14f), new Vector2(410f, 20f));
        var pFill = BarFill("Fill", pRoot.transform, UiOrange, 3f);
        oh.trackerGroup = tg; oh.trackerTitle = tTitle; oh.trackerInfo = tInfo; oh.trackerIcon = tIcon;
        oh.progressFill = pFill; oh.progressRoot = pRoot.gameObject;

        // Target marker / edge arrow.
        var mk = Rect("Marker", obj);
        Anchor(mk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110f, 110f));
        var mBg = Img("Bg", mk, ui.flat, new Color(UiInk.r, UiInk.g, UiInk.b, 0.75f));
        Anchor(mBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92f, 92f));
        var mIcon = Img("Icon", mk, icons[0], Color.white);
        Anchor(mIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
        var mArrow = Img("Arrow", mk, t.arrow, Color.white);
        Anchor(mArrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(62f, 0f), new Vector2(64f, 64f));
        var aSh = mArrow.gameObject.AddComponent<Outline>(); aSh.effectColor = new Color(UiInk.r, UiInk.g, UiInk.b, 0.9f); aSh.effectDistance = new Vector2(3f, -3f);
        var mDist = Txt("Distance", mk, 28, TextAnchor.UpperCenter, UiCream);
        Anchor(mDist.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(200f, 40f));
        oh.marker = mk; oh.markerArrow = mArrow; oh.markerIcon = mIcon; oh.markerDistance = mDist;
    }
}
