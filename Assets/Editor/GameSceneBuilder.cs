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
        ui = GenerateUiSprites();
        UiFonts.Ensure();
        kit = MakeKit();
        ConfigureUiFont();
        ConfigurePlayer();
        MenuIcons();
        BuildLoadingPrefab();
        ConfigureCharacterTexture(SoldierTex);
        ConfigureCharacterTexture(ZombieTex);
        foreach (var dir in new[] { ShieldDir, BombDir, TongueDir }) ConfigureCharacterTexture(BaseColor(dir));
        foreach (var c in CostumeNames()) ConfigureCharacterTexture(CostumeTex(c));
        var m = CreateMaterials(t);
        ConfigureAudioImports();
        var anims = ImportCharacterAnimations();
        BuildBulletPrefab(m);
        BuildCosmetics(m);
        BuildSplatPrefab(m);
        BuildHealthPackPrefab(m, t);
        BuildAmmoBoxPrefab(t);
        BuildTurretPrefab(m);
        BuildGrenadePrefab(m);
        BuildMinePrefab(m);
        BuildDronePrefab(m);
        BuildZombiePrefab(m, anims);
        BuildSpecialZombiePrefabs(m, anims);
        var env = BuildEnvironmentPrefabs(m);
        var cm = CreateCityMaterials(m, t);
        var cityKit = BuildCityPrefabs(m, cm, env);
        var boss = ImportBoss(m, t);
        BuildBossPrefab(m, boss); // after the env prefabs: its thrown debris reuses their meshes
        BuildMutantBosses(m, t, anims, boss);
        BuildFlarePrefab(cm);
        BuildSupplyPrefab(cm);
        BuildHelicopterPrefab(cm);
        AssetDatabase.SaveAssets();
        BuildPlayerPrefab(m, Load<Bullet>("Bullet"), anims);
        AssetDatabase.SaveAssets();
        BuildNetPrefabs();
        BuildScene(scene, m, t, env, cm, cityKit, boss);
        AssetDatabase.SaveAssets();
        BuildMenuScene(env, cm, cityKit, m);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MenuScenePath, true), new EditorBuildSettingsScene(ScenePath, true) };
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
        layers.GetArrayElementAtIndex(GameLayers.Preview).stringValue = "Preview";
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
        public Texture2D groundAtlas, beam, helipad, redCross;
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
        GenerateCityTextures(t);
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
        public readonly Dictionary<string, Material> kit = new Dictionary<string, Material>();
        public readonly Dictionary<string, Material> kitFade = new Dictionary<string, Material>();
        public Material barrelRed, turretBody, turretDark, grenade;
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
        foreach (var kit in new[] { "Cars", "Survival", "Roads", "Commercial", "Industrial", "Suburban", "Graveyard" })
        {
            var cmap = ConfigureColormap(kit);
            m.kit[kit] = Mat("M_Env_" + kit, simpleLit, x => { x.SetTexture("_BaseMap", cmap); x.SetColor("_BaseColor", Color.white); });
            AssetDatabase.DeleteAsset($"{MatDir}/M_EnvFade_{kit}.mat");
            // Occluding buildings get a see-through circle around the soldier instead of fading entirely.
            m.kitFade[kit] = Mat("M_EnvCut_" + kit, "Game/EnvCutout", x => { x.SetTexture("_BaseMap", cmap); x.SetColor("_BaseColor", Color.white); x.SetColor("_RimColor", new Color(1f, 0.9f, 0.55f)); });
        }
        m.barrelRed = Mat("M_Env_BarrelRed", simpleLit, x => { x.SetTexture("_BaseMap", m.kit["Survival"].GetTexture("_BaseMap")); x.SetColor("_BaseColor", new Color(1f, 0.28f, 0.2f)); });
        m.turretBody = Mat("M_Turret", simpleLit, x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.36f, 0.42f, 0.3f)); });
        m.turretDark = Mat("M_TurretDark", simpleLit, x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.13f, 0.14f, 0.15f)); });
        m.grenade = Mat("M_Grenade", simpleLit, x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.25f, 0.32f, 0.18f)); });
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

    /// <summary>Hard-mode ammo box: olive crate, yellow band, brass rounds on top + an amber ground glow.</summary>
    static void BuildAmmoBoxPrefab(Tex t)
    {
        const string lit = "Universal Render Pipeline/Simple Lit";
        var olive = Mat("M_Ammo_Olive", lit, x => { x.SetColor("_BaseColor", new Color(0.33f, 0.38f, 0.2f)); x.SetTexture("_BaseMap", null); });
        var band = Mat("M_Ammo_Band", lit, x => { x.SetColor("_BaseColor", new Color(1f, 0.78f, 0.15f)); x.SetTexture("_BaseMap", null); });
        var brass = Mat("M_Ammo_Brass", lit, x => { x.SetColor("_BaseColor", new Color(0.85f, 0.62f, 0.25f)); x.SetTexture("_BaseMap", null); });
        var glow = Mat("M_AmmoGlow", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", new Color(1f, 0.7f, 0.15f, 0.8f)); x.SetFloat("_VertexColor", 0f); });

        var root = new GameObject("AmmoBox");
        var visual = new GameObject("Visual").transform;
        visual.SetParent(root.transform, false);
        visual.localPosition = new Vector3(0f, 0.45f, 0f);
        void Box(string n, Vector3 pos, Vector3 size, Material mat)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = n;
            Object.DestroyImmediate(b.GetComponent<Collider>());
            b.transform.SetParent(visual, false);
            b.transform.localPosition = pos;
            b.transform.localScale = size;
            b.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        Box("Case", Vector3.zero, new Vector3(0.7f, 0.36f, 0.42f), olive);
        Box("Band", Vector3.zero, new Vector3(0.72f, 0.1f, 0.44f), band);
        for (int i = 0; i < 4; i++) Box("Round" + i, new Vector3(-0.21f + i * 0.14f, 0.27f, 0f), new Vector3(0.07f, 0.18f, 0.07f), brass);
        var g = Quad("Glow", root.transform, glow);
        g.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        g.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
        SavePrefab(root, $"{PrefabDir}/AmmoBox.prefab");
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

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localEulerAngles = euler;
        g.transform.localScale = scale;
        g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    /// <summary>Sentry: tripod base + rotating head with a twin barrel (no collider; it is not an obstacle).</summary>
    static void BuildTurretPrefab(Mats m)
    {
        var root = new GameObject("Turret");
        var rs = new List<Renderer>();
        void R(GameObject g) => rs.Add(g.GetComponent<Renderer>());
        R(Prim(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.12f, 0.9f), m.turretDark));
        R(Prim(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.22f, 0.35f, 0.22f), m.turretDark));
        var head = new GameObject("Head").transform;
        head.SetParent(root.transform, false);
        head.localPosition = new Vector3(0f, 0.95f, 0f);
        R(Prim(PrimitiveType.Cube, "Body", head, Vector3.zero, new Vector3(0.55f, 0.35f, 0.7f), m.turretBody));
        R(Prim(PrimitiveType.Cylinder, "BarrelL", head, new Vector3(-0.1f, 0.02f, 0.6f), new Vector3(0.08f, 0.3f, 0.08f), m.turretDark, new Vector3(90f, 0f, 0f)));
        R(Prim(PrimitiveType.Cylinder, "BarrelR", head, new Vector3(0.1f, 0.02f, 0.6f), new Vector3(0.08f, 0.3f, 0.08f), m.turretDark, new Vector3(90f, 0f, 0f)));
        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(head, false);
        muzzle.localPosition = new Vector3(0f, 0.02f, 0.95f);
        var ring = Quad("Ring", root.transform, m.ringSoldier);
        ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
        ring.transform.localScale = new Vector3(2.2f, 2.2f, 1f);
        rs.Add(ring.GetComponent<Renderer>());
        var t = root.AddComponent<Turret>();
        t.head = head;
        t.muzzle = muzzle;
        t.blinkRenderers = rs.ToArray();

        // Status above the turret (world-space, billboarded at runtime): radial timer ring + health bar.
        var cgo = new GameObject("Status", typeof(RectTransform));
        cgo.transform.SetParent(root.transform, false);
        cgo.transform.localPosition = new Vector3(0f, 2.15f, 0f);
        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 0;
        var crt = (RectTransform)cgo.transform;
        crt.sizeDelta = new Vector2(200f, 140f);
        crt.localScale = Vector3.one * 0.006f;
        var timerBg = Img("TimerBg", crt, ui.ringThick, new Color(0f, 0f, 0f, 0.55f));
        Anchor(timerBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(84f, 84f));
        var timer = Img("Timer", crt, ui.ringThick, UiOrange);
        Anchor(timer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(84f, 84f));
        timer.type = UnityEngine.UI.Image.Type.Filled; timer.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        timer.fillOrigin = 2; timer.fillClockwise = false; timer.fillAmount = 1f;
        var tIcon = Img("Icon", timer.transform, ui.cards[(int)CardId.Turret], UiCream);
        Anchor(tIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
        var hpFrame = Sliced("Health", crt, ui.panel, UiKhakiDark);
        Anchor(hpFrame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(170f, 34f));
        var hp = Img("Fill", hpFrame.transform, ui.flat, new Color(0.4f, 0.95f, 0.35f));
        hp.type = UnityEngine.UI.Image.Type.Filled; hp.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; hp.fillAmount = 1f;
        hp.rectTransform.anchorMin = Vector2.zero; hp.rectTransform.anchorMax = Vector2.one;
        hp.rectTransform.offsetMin = new Vector2(7f, 7f); hp.rectTransform.offsetMax = new Vector2(-7f, -7f);
        t.status = crt; t.healthFill = hp; t.timerFill = timer;
        SavePrefab(root, $"{PrefabDir}/Turret.prefab");
    }

    static void BuildGrenadePrefab(Mats m)
    {
        var root = new GameObject("Grenade");
        Prim(PrimitiveType.Sphere, "Body", root.transform, Vector3.zero, new Vector3(0.26f, 0.3f, 0.26f), m.grenade);
        Prim(PrimitiveType.Cube, "Cap", root.transform, new Vector3(0f, 0.17f, 0f), new Vector3(0.1f, 0.08f, 0.1f), m.turretDark);
        SavePrefab(root, $"{PrefabDir}/Grenade.prefab");
    }

    /// <summary>1.1.0 Mines card: flat olive mine, red blinking light (child 1, toggled by Upgrades), orange ground glow.</summary>
    static void BuildMinePrefab(Mats m)
    {
        var root = new GameObject("Mine");
        var olive = Mat("M_Mine", "Universal Render Pipeline/Simple Lit", x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.25f, 0.28f, 0.17f)); });
        var body = Prim(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.55f, 0.06f, 0.55f), olive);
        Prim(PrimitiveType.Cylinder, "Cap", body.transform, new Vector3(0f, 1.2f, 0f), new Vector3(0.4f, 0.8f, 0.4f), m.turretDark);
        var red = Mat("M_MineLight", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", m.muzzle.GetTexture("_BaseMap")); x.SetColor("_BaseColor", new Color(1f, 0.12f, 0.08f, 1f)); x.SetFloat("_VertexColor", 0f); });
        var light = Quad("Light", root.transform, red);
        light.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        light.transform.localScale = Vector3.one * 0.45f;
        var glow = Quad("Glow", root.transform, m.muzzle);
        glow.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        glow.transform.localScale = Vector3.one * 1.1f;
        SavePrefab(root, $"{PrefabDir}/Mine.prefab");
    }

    /// <summary>1.1.0 Drone card: small quad-copter (child 0 = rotors, spun by Upgrades), cyan belly light.</summary>
    static void BuildDronePrefab(Mats m)
    {
        var root = new GameObject("Drone");
        var rotors = new GameObject("Rotors").transform; rotors.SetParent(root.transform, false); rotors.localPosition = new Vector3(0f, 0.08f, 0f);
        var blade = Mat("M_DroneBlade", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", m.muzzle.GetTexture("_BaseMap")); x.SetColor("_BaseColor", new Color(0.8f, 0.85f, 0.9f, 0.55f)); });
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + i * 90f) * Mathf.Deg2Rad;
            var arm = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.28f;
            Prim(PrimitiveType.Cube, "Arm" + i, root.transform, arm * 0.5f, new Vector3(0.05f, 0.04f, 0.3f), m.turretDark, new Vector3(0f, 90f - (45f + i * 90f), 0f));
            var q = Quad("Blade" + i, rotors, blade);
            q.transform.localPosition = arm;
            q.transform.localScale = Vector3.one * 0.3f;
        }
        Prim(PrimitiveType.Cube, "Body", root.transform, Vector3.zero, new Vector3(0.22f, 0.1f, 0.3f), m.turretBody);
        Prim(PrimitiveType.Cube, "Gun", root.transform, new Vector3(0f, -0.06f, 0.18f), new Vector3(0.05f, 0.05f, 0.16f), m.turretDark);
        var cyan = Mat("M_DroneLight", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", m.muzzle.GetTexture("_BaseMap")); x.SetColor("_BaseColor", new Color(0.3f, 0.9f, 1f, 1f)); x.SetFloat("_VertexColor", 0f); });
        var l = Quad("Light", root.transform, cyan);
        l.transform.localPosition = new Vector3(0f, -0.07f, 0f);
        l.transform.localScale = Vector3.one * 0.35f;
        SavePrefab(root, $"{PrefabDir}/Drone.prefab");
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
        public GameObject[] cars, barricades, burningBarrels, decor, explosiveBarrels;
        public GameObject[] commercial, industrial;
        public GameObject crate, crateLarge, dumpster, streetLight, parasolA, parasolB, tank, tankLarge;
        public GameObject[] containers;
    }

    /// <summary>
    /// Flattens a Kenney GLB into a prefab: meshes re-parented under "Model", scaled to real-world size,
    /// pivot at bottom centre, kit material, optional box collider / fade / fire.
    /// </summary>
    static GameObject EnvPrefab(Mats m, string kit, string glb, string name, Fit fit, float size, float yScale,
        int layer, bool collide, bool fadeable = false, bool fire = false, float radiusScale = 1f, Surface surface = Surface.Concrete,
        Material matOverride = null, System.Action<GameObject, List<Renderer>> post = null)
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
            mr.sharedMaterials = Enumerable.Repeat(matOverride ? matOverride : m.kit[kit], Mathf.Max(1, mf.sharedMesh.subMeshCount)).ToArray();
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
        post?.Invoke(root, renderers);

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
        e.explosiveBarrels = new[] { EnvPrefab(m, "Survival", "barrel", "ExplosiveBarrel", Fit.Height, 1.05f, 1f, B, true, radiusScale: 1.3f,
            surface: Surface.Metal, matOverride: m.barrelRed, post: (root, rs) =>
            {
                var b = root.AddComponent<ExplosiveBarrel>();
                b.model = root.transform.Find("Model").gameObject;
                // Bullets fly at muzzle height (~1.15 m) — the 1.05 m barrel box alone let them pass over the lid.
                if (root.TryGetComponent<BoxCollider>(out var box)) { box.size = new Vector3(box.size.x, 1.7f, box.size.z); box.center = new Vector3(box.center.x, 0.85f, box.center.z); }
                b.colliders = root.GetComponents<Collider>();
                var f = root.AddComponent<HitFlash>();
                f.renderers = rs.ToArray();
                f.flashMaterial = m.flash;
                b.flash = f;
                // Hazard band: a glowing orange ring on the ground so it reads as "explosive" from the top.
                var ring = Quad("Warn", root.transform, m.ringBomb);
                ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                ring.transform.localScale = new Vector3(1.8f, 1.8f, 1f);
                ring.transform.SetParent(b.model.transform, true);
            }) };
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
    /// <summary>Lighting, fog, the infinite city around <paramref name="focus"/> and the district mood (game and menu).</summary>
    static ChunkManager BuildCity(Transform focus, Env env, CityMats cm, CityKit kit, out Light light, out DistrictAtmosphere atmo)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.005f;
        RenderSettings.fogColor = new Color(0.6f, 0.54f, 0.46f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.75f, 0.78f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.3f);
        RenderSettings.skybox = null;

        var lightGo = new GameObject("Directional Light");
        light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.94f, 0.85f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.6f;
        lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        lightGo.AddComponent<UniversalAdditionalLightData>();

        // Infinite city
        var chunks = new GameObject("City").AddComponent<ChunkManager>();
        chunks.player = focus;
        chunks.roadCars = env.cars;
        chunks.roadBarricades = env.barricades;
        chunks.burningBarrels = env.burningBarrels;
        chunks.explosiveBarrels = env.explosiveBarrels;
        chunks.roadDecor = env.decor;
        chunks.groundMaterial = cm.ground;
        chunks.kit = kit;
        chunks.districts = new[]
        {
            new DistrictSettings
            {
                district = District.Cadde, buildings = env.commercial, buildingChance = 0.7f,
                blockProps = new[] { env.crate, env.crateLarge, env.dumpster }, blockPropMin = 1, blockPropMax = 2,
                sidewalkProps = new[] { env.streetLight, env.dumpster, env.crate },
            },
            new DistrictSettings
            {
                district = District.Carsi, buildings = env.commercial, buildingChance = 0.25f,
                blockProps = new[] { env.parasolA, env.parasolB, env.crate, env.crateLarge, env.parasolA }, blockPropMin = 3, blockPropMax = 5,
                sidewalkProps = new[] { env.streetLight, env.crate },
            },
            new DistrictSettings
            {
                district = District.Sanayi, buildings = env.industrial, buildingChance = 0.65f,
                blockProps = new[] { env.tank, env.tankLarge, env.burningBarrels[0], env.crateLarge, env.containers[0], env.explosiveBarrels[0], env.explosiveBarrels[0] }, blockPropMin = 1, blockPropMax = 3,
                sidewalkProps = new[] { env.burningBarrels[0], env.dumpster, env.streetLight },
            },
            new DistrictSettings
            {
                district = District.Liman, buildings = new[] { env.industrial[4] }, buildingChance = 0.15f,
                blockProps = env.containers.Concat(new[] { env.crateLarge, env.tank }).ToArray(), blockPropMin = 2, blockPropMax = 3,
                sidewalkProps = new[] { env.crate, env.crateLarge, env.explosiveBarrels[0] },
            },
        };

        atmo = new GameObject("DistrictAtmosphere").AddComponent<DistrictAtmosphere>();
        atmo.sun = light;
        atmo.moods = DistrictMoods();
        return chunks;
    }

    static void BuildScene(UnityEngine.SceneManagement.Scene scene, Mats m, Tex t, Env env, CityMats cm, CityKit kit, BossAssets boss)
    {
        // Player
        var playerPrefab = Load<PlayerController>("Player");
        var player = ((GameObject)PrefabUtility.InstantiatePrefab(playerPrefab.gameObject)).GetComponent<PlayerController>();
        player.transform.position = Vector3.zero;

        var chunks = BuildCity(player.transform, env, cm, kit, out var light, out var atmo);

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
        atmo.cam = cam;
        var rig = camGo.AddComponent<CameraRig>();
        rig.target = player.transform;
        // Pulled back from 14 m (F6.5) so more of the street is visible; same angle. Rings/arrows/cutout scaled to match.
        rig.distance = 15.5f; // 1.1.0: closer (was 17.5) — characters and effects read bigger on phones
        rig.cutRadius = 0.17f; // playtest-02 #4: roofs hid half the screen with 0.105
        camGo.transform.SetPositionAndRotation(Quaternion.Euler(57f, 0f, 0f) * Vector3.back * 17.5f, Quaternion.Euler(57f, 0f, 0f));

        // Systems
        BuildFx(m);
        new GameObject("WaveDirector").AddComponent<WaveDirector>();
        var pickups = new GameObject("Pickups").AddComponent<Pickups>();
        pickups.healthPackPrefab = LoadGo($"{PrefabDir}/HealthPack.prefab");
        pickups.ammoBoxPrefab = LoadGo($"{PrefabDir}/AmmoBox.prefab");
        var spawner = new GameObject("ZombieSpawner").AddComponent<ZombieSpawner>();
        spawner.prefab = Load<Zombie>("Zombie");
        spawner.shieldPrefab = Load<Zombie>("ShieldZombie");
        spawner.bombPrefab = Load<Zombie>("BombZombie");
        spawner.tonguePrefab = Load<Zombie>("TongueZombie");
        spawner.despawnDistance = 40f; // the wider 17.5 m view reaches ~25 m to the corners
        BuildBossSystems(spawner);
        BuildNetZombies(spawner);
        BuildAudio(player.transform);
        new GameObject("Progression").AddComponent<Progression>();
        var upgrades = new GameObject("Upgrades").AddComponent<Upgrades>();
        upgrades.turretPrefab = Load<Turret>("Turret");
        upgrades.grenadePrefab = LoadGo($"{PrefabDir}/Grenade.prefab");
        upgrades.minePrefab = LoadGo($"{PrefabDir}/Mine.prefab");
        upgrades.dronePrefab = LoadGo($"{PrefabDir}/Drone.prefab");
        upgrades.cardSprites = ui.cards;

        BuildObjectiveScene(cm);
        var hud = BuildUI(t, out var controls);
        BuildObjectiveHud(hud, t);
        BuildBossHud(hud);
        BuildPauseUi(hud, controls);

        BuildWeather(t);

        var warm = new GameObject("Warmup").AddComponent<Warmup>();
        warm.materials = AssetDatabase.FindAssets("t:Material", new[] { MatDir })
            .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
        warm.skinnedSample = LoadGo(ZombieRigged);
        warm.skinnedMaterials = new[] { m.zombie, m.zombieRunner, m.flash, m.shieldZombie, m.bombZombie, m.tongueZombie, boss.body }.Concat(ZombieDistrictMaterials().Where(x => x)).ToArray();
        warm.fonts = new[] { UiFonts.Display, UiFonts.Bold, UiFonts.Semi };

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
    /// <summary>Rubik ExtraBold (OFL, static instance of the variable font; full Turkish set). See CREDITS.md.</summary>
    const string UiFontPath = "Assets/Art/UI/Fonts/Rubik-ExtraBold.ttf";
    static Font UIFont => AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);
    static UiSprites ui;

    static void ConfigureUiFont()
    {
        var imp = (TrueTypeFontImporter)AssetImporter.GetAtPath(UiFontPath);
        imp.fontTextureCase = FontTextureCase.Dynamic;
        imp.includeFontData = true;
        imp.fontRenderingMode = FontRenderingMode.Smooth;
        imp.SaveAndReimport();
    }

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

    static UiText Txt(string name, Transform parent, int size, TextAnchor align, Color color)
    {
        var r = Rect(name, parent);
        var t = r.gameObject.AddComponent<UiText>();
        // Redesign fonts: Bungee for big titles, Barlow Condensed 800 for everything else.
        t.font = size >= 80 ? UiFonts.Display : UiFonts.Bold;
        if (size >= 80 && UiFonts.DisplayShadow) t.fontSharedMaterial = UiFonts.DisplayShadow;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.overflowMode = TMPro.TextOverflowModes.Overflow;
        t.richText = true;
        return t;
    }
}
