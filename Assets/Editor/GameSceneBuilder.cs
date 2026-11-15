using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the game: generated textures, materials, character + environment prefabs and the Game scene.
/// Menu: Tools/Zombie Shooter/Rebuild Game Scene. Batchmode: -executeMethod GameSceneBuilder.Build
/// Everything in Assets/Prefabs, Assets/Materials, Assets/Art/Textures and Game.unity is overwritten.
/// </summary>
public static partial class GameSceneBuilder
{
    const string ArtTex = "Assets/Art/Textures";
    const string MatDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string EnvPrefabDir = "Assets/Prefabs/Env";
    const string EnvArt = "Assets/Art/Environment";
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string SoldierTex = "Assets/Art/Characters/Soldier/Textures/Soldier_BaseColor.png";
    const string ZombieTex = "Assets/Art/Characters/Zombie/Textures/Zombie_BaseColor.png";


    [MenuItem("Tools/Zombie Shooter/Rebuild Game Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupLayers();
        var t = GenerateTextures();
        ConfigureCharacterTexture(SoldierTex);
        ConfigureCharacterTexture(ZombieTex);
        foreach (var dir in new[] { ShieldDir, BombDir, TongueDir }) ConfigureCharacterTexture(BaseColor(dir));
        var m = CreateMaterials(t);
        ConfigureAudioImports();
        var anims = ImportCharacterAnimations();
        BuildBulletPrefab(m);
        BuildSplatPrefab(m);
        BuildHealthPackPrefab(m, t);
        BuildZombiePrefab(m, anims);
        BuildSpecialZombiePrefabs(m, anims);
        var env = BuildEnvironmentPrefabs(m);
        AssetDatabase.SaveAssets();
        BuildPlayerPrefab(m, Load<Bullet>("Bullet"), anims);
        AssetDatabase.SaveAssets();
        BuildScene(scene, m, t, env);
        AssetDatabase.SaveAssets();
        Debug.Log("BUILD: done");
    }

    static T Load<T>(string name) where T : Component =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab").GetComponent<T>();

    static GameObject LoadGo(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

    // ---------------------------------------------------------------- layers
    static void SetupLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        layers.GetArrayElementAtIndex(GameLayers.Blocker).stringValue = "Blocker";
        layers.GetArrayElementAtIndex(GameLayers.Vaultable).stringValue = "Vaultable";
        layers.GetArrayElementAtIndex(GameLayers.Zombie).stringValue = "Zombie";
        layers.GetArrayElementAtIndex(GameLayers.Player).stringValue = "Player";
        tm.ApplyModifiedPropertiesWithoutUndo();

        Physics.IgnoreLayerCollision(GameLayers.Player, GameLayers.Zombie, true);
        Physics.IgnoreLayerCollision(GameLayers.Zombie, GameLayers.Zombie, true);
        Physics.IgnoreLayerCollision(GameLayers.Zombie, GameLayers.Blocker, true);
        Physics.IgnoreLayerCollision(GameLayers.Zombie, GameLayers.Vaultable, true);
        Physics.IgnoreLayerCollision(GameLayers.Zombie, 0, true);
    }

    // ---------------------------------------------------------------- textures
    class Tex
    {
        public Texture2D soft, tracer, splat, ringTex, band;
        public Texture2D[] ground = new Texture2D[4];
        public Sprite ring, disc, vignette, white, arrow;
    }

    static Tex GenerateTextures()
    {
        Directory.CreateDirectory(ArtTex);
        foreach (var old in new[] { "Crate", "Ground", "Wall" })
            AssetDatabase.DeleteAsset($"{ArtTex}/{old}.png");

        var t = new Tex();
        t.soft = WriteTex("FX_SoftCircle", 64, (u, v) =>
        {
            float r = Dist(u, v) * 2f; float a = Mathf.Clamp01(1f - r); return new Color(1, 1, 1, a * a);
        }, false, TextureWrapMode.Clamp, 64);
        t.tracer = WriteTex("FX_Tracer", 64, (u, v) =>
        {
            float across = 1f - Mathf.Abs(u - 0.5f) * 2f;
            float core = Mathf.Pow(Mathf.Clamp01(across), 2.5f);
            float along = Mathf.Pow(v, 1.6f);
            float head = Mathf.Clamp01((v - 0.85f) / 0.15f);
            return new Color(1f, 1f, 1f - 0.3f * (1f - head), core * along);
        }, false, TextureWrapMode.Clamp, 64);
        t.splat = WriteTex("FX_Splat", 128, SplatPixel, true, TextureWrapMode.Clamp, 128);
        t.ring = WriteSprite("UI_Ring", 256, (u, v) =>
        {
            float r = Dist(u, v) * 2f;
            float ring = Mathf.Clamp01((0.98f - r) * 80f) * Mathf.Clamp01((r - 0.86f) * 80f);
            float fill = Mathf.Clamp01((0.86f - r) * 80f) * 0.18f;
            return new Color(1, 1, 1, Mathf.Max(ring, fill));
        });
        t.disc = WriteSprite("UI_Disc", 128, (u, v) =>
        {
            float r = Dist(u, v) * 2f; return new Color(1, 1, 1, Mathf.Clamp01((0.97f - r) * 40f));
        });
        t.vignette = WriteSprite("UI_Vignette", 256, (u, v) =>
        {
            float r = Dist(u, v) * 2f; float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.35f, r)); return new Color(1, 1, 1, a);
        });
        t.white = WriteSprite("UI_White", 8, (u, v) => Color.white);
        // Ground ring under special zombies / the soldier: bright rim with a soft inner glow.
        t.ringTex = WriteTex("FX_Ring", 128, (u, v) =>
        {
            float r = Dist(u, v) * 2f;
            float rim = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.06f, 2f));
            float glow = Mathf.Clamp01(1f - r) * 0.18f * Mathf.Clamp01((0.95f - r) * 20f);
            return new Color(1, 1, 1, Mathf.Clamp01(rim + glow));
        }, true, TextureWrapMode.Clamp, 128);
        // Line band (tongue / telegraph): soft across the width, constant along the length.
        t.band = WriteTex("FX_Band", 32, (u, v) =>
        {
            float a = 1f - Mathf.Abs(v - 0.5f) * 2f;
            return new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, a * 1.6f));
        }, false, TextureWrapMode.Clamp, 32);
        // Off-screen threat arrow (points right).
        t.arrow = WriteSprite("UI_Arrow", 64, (u, v) =>
        {
            float x = u, y = Mathf.Abs(v - 0.5f) * 2f;
            float tri = Mathf.Clamp01((0.92f - x - y * 0.75f) * 30f) * Mathf.Clamp01((x - 0.12f) * 30f);
            float notch = Mathf.Clamp01((x - 0.12f - (1f - y) * 0.22f) * 30f);
            return new Color(1, 1, 1, tri * notch);
        });
        for (int d = 0; d < 4; d++)
        {
            var district = (District)d;
            t.ground[d] = WriteTex("Ground_" + district, 512, (u, v) => GroundPixel(district, u, v), true, TextureWrapMode.Clamp, 512);
        }
        return t;
    }

    static float Dist(float u, float v) => Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));

    static float Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return (h & 0xffff) / 65535f; } }

    static float ValueNoise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static Color SplatPixel(float u, float v)
    {
        // Blobby cartoon splat: a core plus a ring of droplets.
        float r = Dist(u, v) * 2f;
        float ang = Mathf.Atan2(v - 0.5f, u - 0.5f);
        float edge = 0.55f + 0.12f * Mathf.Sin(ang * 5f) + 0.07f * Mathf.Sin(ang * 11f + 1.3f);
        float a = Mathf.Clamp01((edge - r) * 25f);
        for (int i = 0; i < 7; i++)
        {
            float da = i * 0.9f + 0.4f, dr = 0.72f + 0.1f * Mathf.Sin(i * 2.1f);
            Vector2 c = new Vector2(0.5f + Mathf.Cos(da) * dr * 0.5f, 0.5f + Mathf.Sin(da) * dr * 0.5f);
            float dd = Vector2.Distance(new Vector2(u, v), c);
            a = Mathf.Max(a, Mathf.Clamp01((0.05f + 0.02f * (i % 3) - dd) * 60f));
        }
        float shade = 0.85f + 0.15f * Mathf.Clamp01(1f - r);
        return new Color(shade, shade, shade, a);
    }

    /// <summary>One 20x20 m city block: half road on each edge, curb, sidewalk, district interior.</summary>
    static Color GroundPixel(District d, float u, float v)
    {
        const float S = ChunkManager.Size;
        float mx = u * S, mz = v * S;
        float ex = Mathf.Min(mx, S - mx), ez = Mathf.Min(mz, S - mz);
        float e = Mathf.Min(ex, ez);
        float grain = 0.93f + 0.07f * Hash((int)(u * 512), (int)(v * 512));
        float blot = ValueNoise(mx * 0.35f, mz * 0.35f);

        bool roadX = ex < 3.5f, roadZ = ez < 3.5f;
        Color c;
        if (roadX || roadZ)
        {
            c = new Color(0.23f, 0.24f, 0.27f) * (0.9f + 0.15f * blot) * grain;
            bool inter = roadX && roadZ;
            if (!inter)
            {
                float across = roadX ? ex : ez;
                float along = roadX ? mz : mx;
                // Dashed yellow centre line on the chunk border.
                if (across < 0.1f && Mathf.Repeat(along, 3f) < 1.7f) c = new Color(0.95f, 0.78f, 0.2f);
                // Solid white edge line.
                if (across > 3.12f && across < 3.27f) c = new Color(0.85f, 0.85f, 0.82f);
                // Zebra crossing next to the intersection.
                float distToInter = roadX ? ez : ex;
                if (distToInter > 3.7f && distToInter < 5.2f && across < 3.0f)
                {
                    float stripeCoord = roadX ? mx : mz;
                    if (Mathf.Repeat(stripeCoord, 0.9f) < 0.45f) c = new Color(0.88f, 0.88f, 0.85f);
                }
            }
            // Cracks / patches.
            if (blot > 0.78f) c *= 0.85f;
        }
        else if (e < 3.75f)
        {
            c = new Color(0.72f, 0.72f, 0.7f) * grain; // curb
        }
        else if (e < 5.5f)
        {
            c = new Color(0.62f, 0.6f, 0.57f) * grain; // sidewalk tiles
            if (Mathf.Repeat(mx, 1f) < 0.05f || Mathf.Repeat(mz, 1f) < 0.05f) c *= 0.82f;
        }
        else
        {
            switch (d)
            {
                case District.Cadde:
                    c = new Color(0.56f, 0.56f, 0.54f) * grain;
                    if (Mathf.Repeat(mx, 2f) < 0.06f || Mathf.Repeat(mz, 2f) < 0.06f) c *= 0.85f;
                    break;
                case District.Carsi:
                {
                    float row = Mathf.Floor(mz / 0.3f);
                    float bx = mx + (row % 2f) * 0.3f;
                    c = Color.Lerp(new Color(0.66f, 0.38f, 0.28f), new Color(0.58f, 0.32f, 0.24f), Hash((int)(bx / 0.6f), (int)row)) * grain;
                    if (Mathf.Repeat(mz, 0.3f) < 0.03f || Mathf.Repeat(bx, 0.6f) < 0.03f) c = new Color(0.5f, 0.45f, 0.4f);
                    break;
                }
                case District.Sanayi:
                    c = new Color(0.43f, 0.43f, 0.41f) * (0.85f + 0.25f * blot) * grain;
                    if (ValueNoise(mx * 0.8f + 10f, mz * 0.8f) > 0.72f) c *= 0.65f; // oil stains
                    if (e < 5.85f && Mathf.Repeat(mx + mz, 1f) < 0.5f) c = new Color(0.9f, 0.72f, 0.1f); // hazard border
                    break;
                default: // Liman
                    c = new Color(0.52f, 0.52f, 0.5f) * (0.9f + 0.15f * blot) * grain;
                    if (Mathf.Repeat(mx, 3.2f) < 0.1f) c = new Color(0.9f, 0.75f, 0.2f);
                    if (ValueNoise(mx * 0.6f, mz * 0.6f + 30f) > 0.75f) c = Color.Lerp(c, new Color(0.55f, 0.35f, 0.2f), 0.5f); // rust
                    break;
            }
        }
        c.a = 1f;
        return c;
    }

    delegate Color PixelFn(float u, float v);

    static string WritePng(string name, int size, PixelFn fn)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = fn((x + 0.5f) / size, (y + 0.5f) / size);
        tex.SetPixels(px);
        string path = $"{ArtTex}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return path;
    }

    static Texture2D WriteTex(string name, int size, PixelFn fn, bool mips, TextureWrapMode wrap, int maxSize)
    {
        string path = WritePng(name, size, fn);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = mips;
        imp.wrapMode = wrap;
        imp.anisoLevel = mips ? 4 : 1;
        imp.maxTextureSize = maxSize;
        SetMobileCompression(imp, maxSize);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Sprite WriteSprite(string name, int size, PixelFn fn)
    {
        string path = WritePng(name, size, fn);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        SetMobileCompression(imp, 256);
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void SetMobileCompression(TextureImporter imp, int maxSize)
    {
        foreach (var platform in new[] { "Android", "iPhone" })
        {
            var s = imp.GetPlatformTextureSettings(platform);
            s.overridden = true;
            s.maxTextureSize = maxSize;
            s.format = TextureImporterFormat.ASTC_6x6;
            s.compressionQuality = 50;
            imp.SetPlatformTextureSettings(s);
        }
    }

    static void ConfigureCharacterTexture(string path)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.sRGBTexture = true;
        imp.alphaSource = TextureImporterAlphaSource.None;
        imp.mipmapEnabled = true;
        imp.maxTextureSize = 1024;
        imp.textureCompression = TextureImporterCompression.Compressed;
        SetMobileCompression(imp, 1024);
        imp.SaveAndReimport();
    }

    /// <summary>Kenney palette textures: small, no mips bleeding between swatches, kept uncompressed-ish.</summary>
    static Texture2D ConfigureColormap(string kit)
    {
        string path = $"{EnvArt}/{kit}/Textures/colormap.png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.maxTextureSize = 512;
        foreach (var platform in new[] { "Android", "iPhone" })
        {
            var s = imp.GetPlatformTextureSettings(platform);
            s.overridden = true;
            s.maxTextureSize = 256;
            s.format = TextureImporterFormat.ASTC_4x4;
            imp.SetPlatformTextureSettings(s);
        }
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- materials
    class Mats
    {
        public Material soldier, zombie, zombieRunner, flash, bullet, muzzle, goo, chunks, sparks, pop, dust, fire, splat;
        public Material shieldZombie, bombZombie, tongueZombie, ringShield, ringBomb, ringTongue, ringSoldier, tongue, telegraph, fxFire;
        public Material[] ground = new Material[4];
        public readonly Dictionary<string, Material> kit = new Dictionary<string, Material>();
        public readonly Dictionary<string, Material> kitFade = new Dictionary<string, Material>();
    }

    static Material Mat(string name, string shader, System.Action<Material> setup)
    {
        string path = $"{MatDir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find(shader);
        if (sh == null) throw new System.Exception("Shader not found: " + shader);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        else m.shader = sh;
        setup(m);
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mats CreateMaterials(Tex t)
    {
        foreach (var old in new[] { "M_Crate", "M_Ground", "M_Wall" }) AssetDatabase.DeleteAsset($"{MatDir}/{old}.mat");
        const string simpleLit = "Universal Render Pipeline/Simple Lit";
        var m = new Mats();
        var zTex = AssetDatabase.LoadAssetAtPath<Texture2D>(ZombieTex);
        m.soldier = Mat("M_Soldier", simpleLit, x => { x.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SoldierTex)); x.SetColor("_BaseColor", Color.white); });
        m.zombie = Mat("M_Zombie", simpleLit, x => { x.SetTexture("_BaseMap", zTex); x.SetColor("_BaseColor", Color.white); });
        m.zombieRunner = Mat("M_ZombieRunner", simpleLit, x => { x.SetTexture("_BaseMap", zTex); x.SetColor("_BaseColor", new Color(1f, 0.7f, 0.66f)); });
        m.flash = Mat("M_HitFlash", "Game/Flash", x => { x.SetColor("_BaseColor", Color.white); x.SetFloat("_VertexColor", 0f); });
        m.bullet = Mat("M_Bullet", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.tracer); x.SetColor("_BaseColor", new Color(1f, 0.85f, 0.45f, 1f)); x.SetFloat("_VertexColor", 0f); });
        m.muzzle = Mat("M_MuzzleFlash", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", new Color(1f, 0.75f, 0.3f, 1f)); x.SetFloat("_VertexColor", 0f); });
        m.goo = Mat("M_FxGoo", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.chunks = Mat("M_FxChunks", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.sparks = Mat("M_FxSparks", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.pop = Mat("M_FxPop", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.dust = Mat("M_FxDust", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.fire = Mat("M_FxFire", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        Material CharMat(string n, string dir) =>
            Mat(n, simpleLit, x => { x.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColor(dir))); x.SetColor("_BaseColor", Color.white); });
        m.shieldZombie = CharMat("M_ShieldZombie", ShieldDir);
        m.bombZombie = CharMat("M_BombZombie", BombDir);
        m.tongueZombie = CharMat("M_TongueZombie", TongueDir);
        Material Ring(string n, Color c) =>
            Mat(n, "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.ringTex); x.SetColor("_BaseColor", c); x.SetFloat("_VertexColor", 0f); });
        m.ringShield = Ring("M_Ring_Shield", new Color(1f, 0.15f, 0.12f, 1f));
        m.ringBomb = Ring("M_Ring_Bomb", new Color(1f, 0.55f, 0.08f, 1f));
        m.ringTongue = Ring("M_Ring_Tongue", new Color(0.75f, 0.25f, 1f, 1f));
        m.ringSoldier = Ring("M_Ring_Soldier", new Color(0.35f, 0.95f, 1f, 0.9f));
        m.tongue = Mat("M_Tongue", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", t.band); x.SetColor("_BaseColor", new Color(0.85f, 0.25f, 0.4f, 1f)); x.SetFloat("_VertexColor", 0f); });
        m.telegraph = Mat("M_Telegraph", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.band); x.SetColor("_BaseColor", new Color(1f, 0.1f, 0.08f, 1f)); x.SetFloat("_VertexColor", 0f); });
        m.fxFire = Mat("M_FxExplosion", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.splat = Mat("M_GooSplat", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", t.splat); x.SetColor("_BaseColor", new Color(0.35f, 0.75f, 0.12f, 0.9f)); x.SetFloat("_VertexColor", 0f); });
        for (int d = 0; d < 4; d++)
        {
            var tex = t.ground[d];
            m.ground[d] = Mat("M_Ground_" + (District)d, simpleLit, x => { x.SetTexture("_BaseMap", tex); x.SetColor("_BaseColor", Color.white); });
        }
        foreach (var kit in new[] { "Cars", "Survival", "Roads", "Commercial", "Industrial" })
        {
            var cmap = ConfigureColormap(kit);
            m.kit[kit] = Mat("M_Env_" + kit, simpleLit, x => { x.SetTexture("_BaseMap", cmap); x.SetColor("_BaseColor", Color.white); });
            m.kitFade[kit] = Mat("M_EnvFade_" + kit, "Game/FxAlpha", x => { x.SetTexture("_BaseMap", cmap); x.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.28f)); x.SetFloat("_VertexColor", 0f); });
        }
        return m;
    }

    // ---------------------------------------------------------------- character / fx prefabs
    static void SetLayer(GameObject go, int layer)
    {
        foreach (var tr in go.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = layer;
    }

    static GameObject Quad(string name, Transform parent, Material mat)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return q;
    }

    static GameObject SavePrefab(GameObject go, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static void BuildBulletPrefab(Mats m)
    {
        var go = new GameObject("Bullet");
        var q = Quad("Tracer", go.transform, m.bullet);
        q.transform.localScale = new Vector3(0.15f, 2.4f, 1f);
        q.transform.localPosition = new Vector3(0f, 0f, -1.2f);
        var b = go.AddComponent<Bullet>();
        b.tracer = q.transform;
        SavePrefab(go, $"{PrefabDir}/Bullet.prefab");
    }

    /// <summary>Medkit: white box with a red cross on top (bobs via Pickups) + a green ground glow.</summary>
    static void BuildHealthPackPrefab(Mats m, Tex t)
    {
        const string lit = "Universal Render Pipeline/Simple Lit";
        var white = Mat("M_Pickup_White", lit, x => { x.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.92f)); x.SetTexture("_BaseMap", null); });
        var red = Mat("M_Pickup_Red", lit, x => { x.SetColor("_BaseColor", new Color(0.9f, 0.12f, 0.1f)); x.SetTexture("_BaseMap", null); });
        var glow = Mat("M_PickupGlow", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", new Color(0.3f, 1f, 0.4f, 0.8f)); x.SetFloat("_VertexColor", 0f); });

        var root = new GameObject("HealthPack");
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        visual.localPosition = new Vector3(0f, 0.45f, 0f);
        GameObject Box(string n, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = n;
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            b.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return b;
        }
        Box("Case", visual, Vector3.zero, new Vector3(0.6f, 0.42f, 0.6f), white);
        Box("CrossA", visual, new Vector3(0f, 0.215f, 0f), new Vector3(0.42f, 0.02f, 0.13f), red);
        Box("CrossB", visual, new Vector3(0f, 0.215f, 0f), new Vector3(0.13f, 0.02f, 0.42f), red);
        var g = Quad("Glow", root.transform, glow);
        g.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        g.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        SavePrefab(root, $"{PrefabDir}/HealthPack.prefab");
    }

    static void BuildSplatPrefab(Mats m)
    {
        var go = new GameObject("GooSplat");
        var q = Quad("Quad", go.transform, m.splat);
        q.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        SavePrefab(go, $"{PrefabDir}/GooSplat.prefab");
    }

    // ---------------------------------------------------------------- environment prefabs
    enum Fit { Height, Length, Footprint }

    class Env
    {
        public GameObject[] cars, barricades, burningBarrels, decor;
        public GameObject[] commercial, industrial;
        public GameObject crate, crateLarge, dumpster, streetLight, parasolA, parasolB, tank, tankLarge;
        public GameObject[] containers;
    }

    /// <summary>
    /// Flattens a Kenney GLB into a prefab: meshes re-parented under "Model", scaled to real-world size,
    /// pivot at bottom centre, kit material, optional box collider / fade / fire.
    /// </summary>
    static GameObject EnvPrefab(Mats m, string kit, string glb, string name, Fit fit, float size, float yScale,
        int layer, bool collide, bool fadeable = false, bool fire = false, float radiusScale = 1f, Surface surface = Surface.Concrete)
    {
        var src = LoadGo($"{EnvArt}/{kit}/{glb}.glb");
        if (src == null) throw new System.Exception($"Missing {kit}/{glb}.glb");
        var inst = Object.Instantiate(src);
        inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        inst.transform.localScale = Vector3.one;

        var root = new GameObject(name);
        var model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);
        var renderers = new List<Renderer>();
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
        {
            var go = new GameObject(mf.name);
            go.transform.SetParent(model, false);
            go.transform.SetPositionAndRotation(mf.transform.position, mf.transform.rotation);
            go.transform.localScale = mf.transform.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = Enumerable.Repeat(m.kit[kit], Mathf.Max(1, mf.sharedMesh.subMeshCount)).ToArray();
            mr.shadowCastingMode = ShadowCastingMode.On;
            renderers.Add(mr);
        }
        Object.DestroyImmediate(inst);

        Bounds b = Encapsulate(renderers);
        float s = fit == Fit.Height ? size / b.size.y
                : fit == Fit.Length ? size / Mathf.Max(b.size.x, b.size.z)
                : size / Mathf.Max(b.size.x, b.size.z);
        model.localScale = new Vector3(s, s * yScale, s);
        b = Encapsulate(renderers);
        model.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
        b = Encapsulate(renderers);

        if (collide)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size;
        }
        var prop = root.AddComponent<EnvProp>();
        prop.radius = Mathf.Max(b.extents.x, b.extents.z) * radiusScale;
        prop.surface = surface;

        if (fadeable)
        {
            var f = root.AddComponent<Fadeable>();
            f.renderers = renderers.ToArray();
            f.fadeMaterial = m.kitFade[kit];
        }
        if (fire) AddFire(root.transform, m, new Vector3(0f, b.max.y - 0.05f, 0f), b.extents.x * 0.8f);
        if (!collide) foreach (var r in renderers) r.shadowCastingMode = ShadowCastingMode.Off;

        SetLayer(root, collide ? layer : 0);
        return SavePrefab(root, $"{EnvPrefabDir}/{name}.prefab");
    }

    static Bounds Encapsulate(List<Renderer> rs)
    {
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void AddFire(Transform parent, Mats m, Vector3 pos, float radius)
    {
        var go = new GameObject("Fire");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true; main.playOnAwake = true; main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
        main.startColor = new Color(1f, 0.6f, 0.15f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 30;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var em = ps.emission; em.rateOverTime = 20f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = Mathf.Max(0.1f, radius);
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f), new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = m.fire;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    static Env BuildEnvironmentPrefabs(Mats m)
    {
        const int V = GameLayers.Vaultable, B = GameLayers.Blocker;
        var e = new Env();
        e.cars = new[] { "sedan", "taxi", "police", "van", "suv", "hatchback-sports" }
            .Select(c => EnvPrefab(m, "Cars", c, "Car_" + c, Fit.Length, 4.0f, 0.72f, V, true, surface: Surface.Metal)).ToArray();
        e.barricades = new[] { EnvPrefab(m, "Roads", "construction-barrier", "Barricade", Fit.Length, 2.4f, 0.7f, V, true) };
        e.burningBarrels = new[] { EnvPrefab(m, "Survival", "barrel", "BurningBarrel", Fit.Height, 1.0f, 1f, B, true, fire: true, radiusScale: 1.4f, surface: Surface.Metal) };
        e.crate = EnvPrefab(m, "Survival", "box", "Crate", Fit.Height, 1.0f, 1f, V, true);
        e.crateLarge = EnvPrefab(m, "Survival", "box-large", "CrateLarge", Fit.Height, 1.0f, 1f, V, true);
        e.dumpster = EnvPrefab(m, "Roads", "dumpster", "Dumpster", Fit.Length, 2.2f, 0.85f, V, true, surface: Surface.Metal);
        e.streetLight = EnvPrefab(m, "Roads", "light-square", "StreetLight", Fit.Height, 4.5f, 1f, 0, false, radiusScale: 0.3f);
        e.parasolA = EnvPrefab(m, "Commercial", "detail-parasol-a", "ParasolA", Fit.Height, 2.6f, 1f, 0, false, radiusScale: 0.8f);
        e.parasolB = EnvPrefab(m, "Commercial", "detail-parasol-b", "ParasolB", Fit.Height, 2.6f, 1f, 0, false, radiusScale: 0.8f);
        e.decor = new[]
        {
            EnvPrefab(m, "Cars", "cone", "Cone", Fit.Height, 0.6f, 1f, 0, false),
            EnvPrefab(m, "Cars", "debris-tire", "Tire", Fit.Height, 0.7f, 1f, 0, false),
            EnvPrefab(m, "Roads", "construction-cone", "ConstructionCone", Fit.Height, 0.7f, 1f, 0, false),
        };
        e.commercial = new[] { "a", "b", "c", "d", "e", "h" }
            .Select(c => EnvPrefab(m, "Commercial", "building-" + c, "Shop_" + c, Fit.Footprint, 8.5f, 1f, B, true, fadeable: true)).ToArray();
        e.industrial = new[] { "a", "c", "d", "g", "h", "i", "k" }
            .Select(c => EnvPrefab(m, "Industrial", "building-" + c, "Factory_" + c, Fit.Footprint, 8.5f, 1f, B, true, fadeable: true)).ToArray();
        e.containers = new[] { "a", "b", "c" }
            .Select(c => EnvPrefab(m, "Industrial", "shipping-container-" + c, "Container_" + c, Fit.Length, 6.0f, 1f, B, true, fadeable: true, surface: Surface.Metal)).ToArray();
        e.tank = EnvPrefab(m, "Industrial", "detail-tank", "Tank", Fit.Length, 3.0f, 1f, B, true, fadeable: true, surface: Surface.Metal);
        e.tankLarge = EnvPrefab(m, "Industrial", "detail-tank-large", "TankLarge", Fit.Footprint, 3.6f, 1f, B, true, fadeable: true, surface: Surface.Metal);
        return e;
    }

    // ---------------------------------------------------------------- scene
    static void BuildScene(UnityEngine.SceneManagement.Scene scene, Mats m, Tex t, Env env)
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.75f, 0.78f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.3f);
        RenderSettings.skybox = null;

        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.94f, 0.85f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.6f;
        lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        lightGo.AddComponent<UniversalAdditionalLightData>();

        // Player
        var playerPrefab = Load<PlayerController>("Player");
        var player = ((GameObject)PrefabUtility.InstantiatePrefab(playerPrefab.gameObject)).GetComponent<PlayerController>();
        player.transform.position = Vector3.zero;

        // Infinite city
        var chunks = new GameObject("City").AddComponent<ChunkManager>();
        chunks.player = player.transform;
        chunks.roadCars = env.cars;
        chunks.roadBarricades = env.barricades;
        chunks.burningBarrels = env.burningBarrels;
        chunks.roadDecor = env.decor;
        chunks.districts = new[]
        {
            new DistrictSettings
            {
                district = District.Cadde, ground = m.ground[0], buildings = env.commercial, buildingChance = 0.7f,
                blockProps = new[] { env.crate, env.crateLarge, env.dumpster }, blockPropMin = 1, blockPropMax = 2,
                sidewalkProps = new[] { env.streetLight, env.dumpster, env.crate },
            },
            new DistrictSettings
            {
                district = District.Carsi, ground = m.ground[1], buildings = env.commercial, buildingChance = 0.25f,
                blockProps = new[] { env.parasolA, env.parasolB, env.crate, env.crateLarge, env.parasolA }, blockPropMin = 3, blockPropMax = 5,
                sidewalkProps = new[] { env.streetLight, env.crate },
            },
            new DistrictSettings
            {
                district = District.Sanayi, ground = m.ground[2], buildings = env.industrial, buildingChance = 0.65f,
                blockProps = new[] { env.tank, env.tankLarge, env.burningBarrels[0], env.crateLarge, env.containers[0] }, blockPropMin = 1, blockPropMax = 3,
                sidewalkProps = new[] { env.burningBarrels[0], env.dumpster, env.streetLight },
            },
            new DistrictSettings
            {
                district = District.Liman, ground = m.ground[3], buildings = new[] { env.industrial[4] }, buildingChance = 0.15f,
                blockProps = env.containers.Concat(new[] { env.crateLarge, env.tank }).ToArray(), blockPropMin = 2, blockPropMax = 3,
                sidewalkProps = new[] { env.crate, env.crateLarge },
            },
        };

        var origin = new GameObject("FloatingOrigin").AddComponent<FloatingOrigin>();
        origin.target = player.transform;

        // Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 70f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.2f, 0.21f, 0.23f);
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = false;
        var rig = camGo.AddComponent<CameraRig>();
        rig.target = player.transform;
        camGo.transform.SetPositionAndRotation(Quaternion.Euler(57f, 0f, 0f) * Vector3.back * 16f, Quaternion.Euler(57f, 0f, 0f));

        // Systems
        BuildFx(m);
        new GameObject("WaveDirector").AddComponent<WaveDirector>();
        var pickups = new GameObject("Pickups").AddComponent<Pickups>();
        pickups.healthPackPrefab = LoadGo($"{PrefabDir}/HealthPack.prefab");
        var spawner = new GameObject("ZombieSpawner").AddComponent<ZombieSpawner>();
        spawner.prefab = Load<Zombie>("Zombie");
        spawner.shieldPrefab = Load<Zombie>("ShieldZombie");
        spawner.bombPrefab = Load<Zombie>("BombZombie");
        spawner.tonguePrefab = Load<Zombie>("TongueZombie");
        BuildAudio(player.transform);

        var hud = BuildUI(t, out _);

        var warm = new GameObject("Warmup").AddComponent<Warmup>();
        warm.materials = AssetDatabase.FindAssets("t:Material", new[] { MatDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        warm.skinnedSample = LoadGo(ZombieRigged);
        warm.skinnedMaterials = new[] { m.zombie, m.zombieRunner, m.flash, m.shieldZombie, m.bombZombie, m.tongueZombie };
        warm.font = UIFont;

        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = player;
        gm.cameraRig = rig;
        gm.hud = hud;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    // ---------------------------------------------------------------- FX
    static Fx BuildFx(Mats m)
    {
        var fx = new GameObject("Fx").AddComponent<Fx>();
        fx.goo = Ps("Goo", fx.transform, m.goo, new Color(0.5f, 0.88f, 0.15f), 1.2f, 500, true);
        fx.chunks = Ps("Chunks", fx.transform, m.chunks, new Color(0.3f, 0.6f, 0.1f), 2.5f, 250, false);
        fx.sparks = Ps("Sparks", fx.transform, m.sparks, new Color(1f, 0.8f, 0.35f), 0f, 400, true);
        fx.pop = Ps("Pop", fx.transform, m.pop, new Color(1f, 1f, 0.9f, 0.9f), 0f, 60, false);
        fx.dust = Ps("Dust", fx.transform, m.dust, new Color(0.55f, 0.5f, 0.45f, 0.6f), -0.05f, 250, false);
        fx.fire = Ps("Fire", fx.transform, m.fxFire, new Color(1f, 0.55f, 0.15f, 1f), -0.15f, 200, true);
        fx.splatPrefab = LoadGo($"{PrefabDir}/GooSplat.prefab");
        return fx;
    }

    static ParticleSystem Ps(string name, Transform parent, Material mat, Color color, float gravity, int max, bool shrink)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 1f;
        main.startLifetime = 0.5f;
        main.startSpeed = 0f;
        main.startSize = 0.2f;
        main.startColor = color;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = max;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        var em = ps.emission; em.enabled = false;
        var sh = ps.shape; sh.enabled = false;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, shrink ? AnimationCurve.Linear(0f, 1f, 1f, 0.2f) : AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));

        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.dampen = 0f;
        lim.limit = 1000f;
        lim.drag = 2f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    // ---------------------------------------------------------------- UI
    static Font UIFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Anchor(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor;
        r.pivot = pivot;
        r.anchoredPosition = pos;
        r.sizeDelta = size;
    }

    static void Stretch(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    static Image Img(string name, Transform parent, Sprite sprite, Color color)
    {
        var r = Rect(name, parent);
        var img = r.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static Text Txt(string name, Transform parent, int size, TextAnchor align, Color color)
    {
        var r = Rect(name, parent);
        var t = r.gameObject.AddComponent<Text>();
        t.font = UIFont;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.supportRichText = true;
        var o = r.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.7f);
        o.effectDistance = new Vector2(3f, -3f);
        return t;
    }
}
