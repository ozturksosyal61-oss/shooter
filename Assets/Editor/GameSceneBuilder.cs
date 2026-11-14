using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the Phase 1 prototype: textures, materials, prefabs and the Game scene.
/// Menu: Tools/Zombie Shooter/Rebuild Game Scene. Batchmode: -executeMethod GameSceneBuilder.Build
/// </summary>
public static class GameSceneBuilder
{
    const string ArtTex = "Assets/Art/Textures";
    const string MatDir = "Assets/Materials";
    const string PrefabDir = "Assets/Prefabs";
    const string ScenePath = "Assets/Scenes/Game.unity";
    const string SoldierGlb = "Assets/Art/Characters/Soldier/Soldier.glb";
    const string ZombieGlb = "Assets/Art/Characters/Zombie/Zombie.glb";
    const string SoldierTex = "Assets/Art/Characters/Soldier/Textures/Soldier_BaseColor.png";
    const string ZombieTex = "Assets/Art/Characters/Zombie/Textures/Zombie_BaseColor.png";

    // Model facing correction (degrees around Y) so the mesh faces +Z.
    public const float SoldierYaw = 0f;
    public const float ZombieYaw = 0f;

    [MenuItem("Tools/Zombie Shooter/Rebuild Game Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SetupLayers();
        var t = GenerateTextures();
        ConfigureCharacterTexture(SoldierTex);
        ConfigureCharacterTexture(ZombieTex);
        var m = CreateMaterials(t);
        BuildBulletPrefab(m);
        BuildCratePrefab(m);
        BuildZombiePrefab(m);
        AssetDatabase.SaveAssets();
        BuildPlayerPrefab(m, Load<Bullet>("Bullet"));
        AssetDatabase.SaveAssets();
        BuildScene(scene, m, t, Load<PlayerController>("Player"), Load<Zombie>("Zombie"), Load<Transform>("Crate").gameObject);
        AssetDatabase.SaveAssets();
        Debug.Log("BUILD: done");
    }

    // ---------------------------------------------------------------- layers
    static void SetupLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        layers.GetArrayElementAtIndex(GameLayers.Wall).stringValue = "Wall";
        layers.GetArrayElementAtIndex(GameLayers.Crate).stringValue = "Crate";
        layers.GetArrayElementAtIndex(GameLayers.Zombie).stringValue = "Zombie";
        layers.GetArrayElementAtIndex(GameLayers.Player).stringValue = "Player";
        tm.ApplyModifiedPropertiesWithoutUndo();

        Physics.IgnoreLayerCollision(GameLayers.Player, GameLayers.Zombie, true);
        Physics.IgnoreLayerCollision(GameLayers.Zombie, GameLayers.Zombie, true);
        Physics.IgnoreLayerCollision(GameLayers.Zombie, GameLayers.Wall, true);
        Physics.IgnoreLayerCollision(GameLayers.Zombie, GameLayers.Crate, true);
        Physics.IgnoreLayerCollision(GameLayers.Zombie, 0, true);
    }

    // ---------------------------------------------------------------- textures
    class Tex { public Texture2D soft, crate, ground, wall; public Sprite ring, disc, vignette, white; }

    static Tex GenerateTextures()
    {
        Directory.CreateDirectory(ArtTex);
        var t = new Tex();
        t.soft = WriteTex("FX_SoftCircle", 64, (u, v) =>
        {
            float r = Dist(u, v) * 2f; float a = Mathf.Clamp01(1f - r); return new Color(1, 1, 1, a * a);
        }, false, TextureWrapMode.Clamp);
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
        t.crate = WriteTex("Crate", 128, CratePixel, true, TextureWrapMode.Clamp);
        t.ground = WriteTex("Ground", 256, GroundPixel, true, TextureWrapMode.Repeat);
        t.wall = WriteTex("Wall", 128, (u, v) =>
        {
            float stripe = Mathf.Repeat(u * 4f + v * 4f, 1f) < 0.5f ? 1f : 0f;
            float edge = (v < 0.12f || v > 0.88f) ? 0.75f : 1f;
            var c = Color.Lerp(new Color(0.22f, 0.22f, 0.25f), new Color(0.85f, 0.65f, 0.1f), stripe * (v > 0.3f && v < 0.7f ? 1f : 0f));
            return c * edge;
        }, true, TextureWrapMode.Repeat);
        return t;
    }

    static float Dist(float u, float v) => Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));

    static float Hash(int x, int y) { unchecked { int h = x * 374761393 + y * 668265263; h = (h ^ (h >> 13)) * 1274126177; return (h & 0xffff) / 65535f; } }

    static Color CratePixel(float u, float v)
    {
        var wood = new Color(0.78f, 0.53f, 0.27f);
        var dark = new Color(0.45f, 0.28f, 0.13f);
        int x = (int)(u * 128), y = (int)(v * 128);
        float plank = Mathf.Repeat(v * 4f, 1f);
        Color c = wood * (0.92f + 0.08f * Hash(x / 3, y)) * (plank < 0.06f ? 0.7f : 1f);
        bool border = u < 0.1f || u > 0.9f || v < 0.1f || v > 0.9f;
        bool diag = Mathf.Abs(u - v) < 0.07f;
        if (border || diag) c = dark * (0.9f + 0.1f * Hash(x, y / 3));
        if (border && (u < 0.03f || u > 0.97f || v < 0.03f || v > 0.97f)) c *= 0.7f;
        c.a = 1f;
        return c;
    }

    static Color GroundPixel(float u, float v)
    {
        int x = (int)(u * 256), y = (int)(v * 256);
        bool check = (u < 0.5f) ^ (v < 0.5f);
        Color a = new Color(0.47f, 0.55f, 0.36f), b = new Color(0.43f, 0.51f, 0.33f);
        Color c = (check ? a : b) * (0.94f + 0.06f * Hash(x / 2, y / 2));
        if (Hash(x, y) > 0.985f) c *= 0.85f;
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

    static Texture2D WriteTex(string name, int size, PixelFn fn, bool mips, TextureWrapMode wrap)
    {
        string path = WritePng(name, size, fn);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = mips;
        imp.wrapMode = wrap;
        imp.maxTextureSize = 256;
        SetMobileCompression(imp, 256);
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

    // ---------------------------------------------------------------- materials
    class Mats { public Material soldier, zombie, crate, ground, wall, flash, bullet, muzzle, goo, chunks, sparks, pop, dust; }

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
        const string simpleLit = "Universal Render Pipeline/Simple Lit";
        var m = new Mats();
        m.soldier = Mat("M_Soldier", simpleLit, x => { x.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(SoldierTex)); x.SetColor("_BaseColor", Color.white); });
        m.zombie = Mat("M_Zombie", simpleLit, x => { x.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ZombieTex)); x.SetColor("_BaseColor", Color.white); });
        m.crate = Mat("M_Crate", simpleLit, x => { x.SetTexture("_BaseMap", t.crate); x.SetColor("_BaseColor", Color.white); });
        m.ground = Mat("M_Ground", simpleLit, x => { x.SetTexture("_BaseMap", t.ground); x.SetTextureScale("_BaseMap", new Vector2(13f, 10f)); x.SetColor("_BaseColor", Color.white); });
        m.wall = Mat("M_Wall", simpleLit, x => { x.SetTexture("_BaseMap", t.wall); x.SetColor("_BaseColor", Color.white); });
        m.flash = Mat("M_HitFlash", "Game/Flash", x => { x.SetColor("_BaseColor", Color.white); x.SetFloat("_VertexColor", 0f); });
        m.bullet = Mat("M_Bullet", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", new Color(1f, 0.85f, 0.4f, 1f)); x.SetFloat("_VertexColor", 0f); });
        m.muzzle = Mat("M_MuzzleFlash", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", new Color(1f, 0.75f, 0.3f, 1f)); x.SetFloat("_VertexColor", 0f); });
        m.goo = Mat("M_FxGoo", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.chunks = Mat("M_FxChunks", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.sparks = Mat("M_FxSparks", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.pop = Mat("M_FxPop", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        m.dust = Mat("M_FxDust", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", Color.white); });
        return m;
    }

    // ---------------------------------------------------------------- prefabs
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

    /// <summary>Adds Model (animated pivot at feet) → Mesh (scaled so feet sit on y=0, centered).</summary>
    static MeshRenderer AddCharacterModel(Transform root, string glbPath, Material mat, float height, float yaw)
    {
        var mesh = AssetDatabase.LoadAllAssetsAtPath(glbPath).OfType<Mesh>().FirstOrDefault();
        if (mesh == null) throw new System.Exception("No mesh in " + glbPath);
        var model = new GameObject("Model").transform;
        model.SetParent(root, false);
        var meshGo = new GameObject("Mesh");
        meshGo.transform.SetParent(model, false);
        var b = mesh.bounds;
        float s = height / b.size.y;
        var rot = Quaternion.Euler(0f, yaw, 0f);
        meshGo.transform.localRotation = rot;
        meshGo.transform.localScale = Vector3.one * s;
        meshGo.transform.localPosition = rot * new Vector3(-b.center.x * s, 0f, -b.center.z * s) + Vector3.up * (-b.min.y * s);
        meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = meshGo.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.On;
        return mr;
    }

    static T Load<T>(string name) where T : Component =>
        AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab").GetComponent<T>();

    static GameObject SavePrefab(GameObject go, string name)
    {
        string path = $"{PrefabDir}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static Bullet BuildBulletPrefab(Mats m)
    {
        var go = new GameObject("Bullet");
        var q = Quad("Tracer", go.transform, m.bullet);
        q.transform.localScale = new Vector3(0.16f, 1.1f, 1f);
        q.transform.localPosition = new Vector3(0f, 0f, -0.45f);
        go.AddComponent<Bullet>();
        return SavePrefab(go, "Bullet").GetComponent<Bullet>();
    }

    static GameObject BuildCratePrefab(Mats m)
    {
        var go = new GameObject("Crate");
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Box";
        box.transform.SetParent(go.transform, false);
        box.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        box.transform.localScale = new Vector3(1f, 0.9f, 1f);
        box.GetComponent<MeshRenderer>().sharedMaterial = m.crate;
        SetLayer(go, GameLayers.Crate);
        go.isStatic = false;
        return SavePrefab(go, "Crate");
    }

    static Zombie BuildZombiePrefab(Mats m)
    {
        var go = new GameObject("Zombie");
        var mr = AddCharacterModel(go.transform, ZombieGlb, m.zombie, 1.8f, ZombieYaw);
        var cap = go.AddComponent<CapsuleCollider>();
        cap.radius = 0.35f; cap.height = 1.7f; cap.center = new Vector3(0f, 0.85f, 0f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None;
        var flash = go.AddComponent<HitFlash>();
        flash.renderers = new Renderer[] { mr };
        flash.flashMaterial = m.flash;
        var z = go.AddComponent<Zombie>();
        z.model = go.transform.Find("Model");
        z.flash = flash;
        z.body = cap;
        SetLayer(go, GameLayers.Zombie);
        return SavePrefab(go, "Zombie").GetComponent<Zombie>();
    }

    static PlayerController BuildPlayerPrefab(Mats m, Bullet bullet)
    {
        var go = new GameObject("Player");
        var mr = AddCharacterModel(go.transform, SoldierGlb, m.soldier, 1.75f, SoldierYaw);
        var cc = go.AddComponent<CharacterController>();
        cc.radius = 0.35f; cc.height = 1.7f; cc.center = new Vector3(0f, 0.85f, 0f);
        cc.stepOffset = 0.25f; cc.skinWidth = 0.04f; cc.minMoveDistance = 0f;

        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(go.transform, false);
        muzzle.localPosition = new Vector3(0.15f, 1.1f, 0.75f);
        var mf = Quad("MuzzleFlash", muzzle, m.muzzle);
        mf.transform.localPosition = new Vector3(0f, 0f, 0.25f);
        mf.SetActive(false);

        var flash = go.AddComponent<HitFlash>();
        flash.renderers = new Renderer[] { mr };
        flash.flashMaterial = m.flash;

        var weapon = go.AddComponent<Weapon>();
        weapon.muzzle = muzzle;
        weapon.muzzleFlash = mf.transform;
        weapon.bulletPrefab = bullet;

        var pc = go.AddComponent<PlayerController>();
        pc.model = go.transform.Find("Model");
        pc.weapon = weapon;
        pc.flash = flash;
        weapon.owner = pc;
        SetLayer(go, GameLayers.Player);
        return SavePrefab(go, "Player").GetComponent<PlayerController>();
    }

    // ---------------------------------------------------------------- scene
    static void BuildScene(UnityEngine.SceneManagement.Scene scene, Mats m, Tex t, PlayerController playerPrefab, Zombie zombiePrefab, GameObject cratePrefab)
    {
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.75f, 0.78f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.35f, 0.32f, 0.3f);
        RenderSettings.skybox = null;

        // Light
        var lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.6f;
        lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        lightGo.AddComponent<UniversalAdditionalLightData>();

        // Ground + walls
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3((Arena.HalfX * 2f + 12f) / 10f, 1f, (Arena.HalfZ * 2f + 12f) / 10f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = m.ground;
        ground.isStatic = true;

        var walls = new GameObject("Walls").transform;
        MakeWall(walls, m.wall, new Vector3(0f, 0.6f, Arena.HalfZ + 0.5f), new Vector3(Arena.HalfX * 2f + 2f, 1.2f, 1f));
        MakeWall(walls, m.wall, new Vector3(0f, 0.6f, -Arena.HalfZ - 0.5f), new Vector3(Arena.HalfX * 2f + 2f, 1.2f, 1f));
        MakeWall(walls, m.wall, new Vector3(Arena.HalfX + 0.5f, 0.6f, 0f), new Vector3(1f, 1.2f, Arena.HalfZ * 2f));
        MakeWall(walls, m.wall, new Vector3(-Arena.HalfX - 0.5f, 0.6f, 0f), new Vector3(1f, 1.2f, Arena.HalfZ * 2f));

        var arena = new GameObject("Arena").AddComponent<ArenaGenerator>();
        arena.cratePrefab = cratePrefab;

        // Player
        var player = ((GameObject)PrefabUtility.InstantiatePrefab(playerPrefab.gameObject)).GetComponent<PlayerController>();
        player.transform.position = Vector3.zero;

        // Camera
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 60f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.18f, 0.2f);
        var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = false;
        camGo.AddComponent<AudioListener>();
        var rig = camGo.AddComponent<CameraRig>();
        rig.target = player.transform;
        camGo.transform.SetPositionAndRotation(Quaternion.Euler(57f, 0f, 0f) * Vector3.back * 16f, Quaternion.Euler(57f, 0f, 0f));

        // Systems
        var fx = BuildFx(m);
        var spawner = new GameObject("ZombieSpawner").AddComponent<ZombieSpawner>();
        spawner.prefab = zombiePrefab;

        var hud = BuildUI(t, out var controls);

        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        gm.player = player;
        gm.cameraRig = rig;
        gm.hud = hud;

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static void MakeWall(Transform parent, Material mat, Vector3 pos, Vector3 size)
    {
        var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = "Wall";
        w.transform.SetParent(parent, false);
        w.transform.localPosition = pos;
        w.transform.localScale = size;
        w.GetComponent<MeshRenderer>().sharedMaterial = mat;
        w.layer = GameLayers.Wall;
        w.isStatic = true;
    }

    // ---------------------------------------------------------------- FX
    static Fx BuildFx(Mats m)
    {
        var fx = new GameObject("Fx").AddComponent<Fx>();
        fx.goo = Ps("Goo", fx.transform, m.goo, new Color(0.55f, 0.85f, 0.18f), 1.2f, 400, true);
        fx.chunks = Ps("Chunks", fx.transform, m.chunks, new Color(0.3f, 0.55f, 0.12f), 2.5f, 200, false);
        fx.sparks = Ps("Sparks", fx.transform, m.sparks, new Color(1f, 0.8f, 0.35f), 0f, 300, true);
        fx.pop = Ps("Pop", fx.transform, m.pop, new Color(1f, 1f, 0.9f, 0.9f), 0f, 50, false);
        fx.dust = Ps("Dust", fx.transform, m.dust, new Color(0.55f, 0.48f, 0.38f, 0.6f), -0.05f, 200, false);
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

    static HUD BuildUI(Tex t, out TouchControls controls)
    {
        var canvasGo = new GameObject("HUD", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        var root = canvasGo.transform;

        // Damage vignette
        var vig = Img("DamageVignette", root, t.vignette, new Color(0.85f, 0.05f, 0.05f, 0f));
        Stretch(vig.rectTransform);

        // Joystick (bottom-left anchored, positioned in screen/scale units)
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

        // Health bar
        var hpBg = Img("HealthBar", root, t.white, new Color(0f, 0f, 0f, 0.55f));
        Anchor(hpBg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(440f, 36f));
        var hpFill = Img("Fill", hpBg.transform, t.white, new Color(0.3f, 0.9f, 0.3f));
        var fr = hpFill.rectTransform;
        fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(4f, 4f); fr.offsetMax = new Vector2(-4f, -4f);
        fr.pivot = new Vector2(0f, 0.5f);
        var hpLabel = Txt("Label", hpBg.transform, 24, TextAnchor.MiddleLeft, Color.white);
        Stretch(hpLabel.rectTransform); hpLabel.rectTransform.offsetMin = new Vector2(14f, 0f);
        hpLabel.text = "CAN";

        // Score + combo
        var score = Txt("Score", root, 72, TextAnchor.UpperCenter, Color.white);
        Anchor(score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(600f, 90f));
        score.text = "0";

        var comboRoot = Rect("Combo", root);
        Anchor(comboRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(400f, 110f));
        var mult = Txt("Multiplier", comboRoot, 60, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.2f));
        Anchor(mult.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(400f, 64f));
        mult.text = "x1";
        var comboTxt = Txt("ComboCount", comboRoot, 28, TextAnchor.UpperCenter, Color.white);
        Anchor(comboTxt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(400f, 32f));
        var barBg = Img("ComboBarBg", comboRoot, t.white, new Color(0f, 0f, 0f, 0.5f));
        Anchor(barBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(220f, 10f));
        var bar = Img("Bar", barBg.transform, t.white, new Color(1f, 0.85f, 0.2f));
        bar.rectTransform.anchorMin = Vector2.zero; bar.rectTransform.anchorMax = Vector2.one;
        bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
        comboRoot.gameObject.SetActive(false);

        // FPS
        var fps = Txt("FPS", root, 28, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.8f));
        Anchor(fps.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(240f, 40f));
        fps.text = "-- FPS";

        // Game over
        var go = Img("GameOver", root, t.white, new Color(0.05f, 0f, 0f, 0.65f));
        Stretch(go.rectTransform);
        var title = Txt("Title", go.transform, 120, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.25f));
        Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1000f, 150f));
        title.text = "ÖLDÜN";
        var goScore = Txt("Score", go.transform, 56, TextAnchor.UpperCenter, Color.white);
        Anchor(goScore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 60f), new Vector2(1000f, 400f));
        go.gameObject.SetActive(false);

        controls = canvasGo.AddComponent<TouchControls>();
        controls.canvas = canvas;
        controls.joyBase = joyBase.rectTransform; controls.joyKnob = joyKnob.rectTransform; controls.joyGroup = joyGroup;
        controls.aimBase = aimBase.rectTransform; controls.aimKnob = aimKnob.rectTransform; controls.aimGroup = aimGroup;

        var hud = canvasGo.AddComponent<HUD>();
        hud.scoreText = score;
        hud.multiplierText = mult;
        hud.comboText = comboTxt;
        hud.comboRoot = comboRoot;
        hud.comboBar = bar.rectTransform;
        hud.healthFill = fr;
        hud.healthFillImage = hpFill;
        hud.damageVignette = vig;
        hud.fpsText = fps;
        hud.gameOverPanel = go.gameObject;
        hud.gameOverScoreText = goScore;
        return hud;
    }

    // ---------------------------------------------------------------- verification capture
    /// <summary>Renders a gameplay view and character close-ups to PNG (batchmode, needs graphics).</summary>
    public static void Capture()
    {
        string outDir = System.Environment.GetEnvironmentVariable("CAPTURE_DIR") ?? "Temp";
        EditorSceneManager.OpenScene(ScenePath);
        var cam = Camera.main;
        var player = Object.FindAnyObjectByType<PlayerController>();
        // Fake a few crates/zombies for the shot.
        var crate = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Crate.prefab");
        var zombie = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Zombie.prefab");
        Object.Instantiate(crate, new Vector3(3f, 0f, 2f), Quaternion.Euler(0f, 10f, 0f));
        Object.Instantiate(crate, new Vector3(-5f, 0f, -1f), Quaternion.identity);
        for (int i = 0; i < 5; i++)
        {
            var z = Object.Instantiate(zombie, new Vector3(-3f + i * 1.6f, 0f, 5f + (i % 2)), Quaternion.Euler(0f, 180f, 0f));
        }
        Shot(cam, $"{outDir}/capture_game.png", 1920, 1080);

        // Front close-ups: camera on +Z looking back toward -Z, so a model facing +Z shows its face.
        cam.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, 4f), Quaternion.LookRotation(new Vector3(0f, -0.1f, -1f)));
        cam.fieldOfView = 35f;
        Shot(cam, $"{outDir}/capture_soldier_front.png", 800, 800);
        player.gameObject.SetActive(false);
        var zz = Object.Instantiate(zombie, Vector3.zero, Quaternion.identity);
        Shot(cam, $"{outDir}/capture_zombie_front.png", 800, 800);
        Debug.Log("CAPTURE: done");
    }

    static void Shot(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24);
        cam.targetTexture = rt;
        cam.Render();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }
}
