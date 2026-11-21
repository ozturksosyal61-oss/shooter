using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>1.1.0 world variety: weather visuals (dusk / night flashlight, rain), district signature structures, rare set pieces.</summary>
public static partial class GameSceneBuilder
{
    static void BuildWeather(Tex t)
    {
        var cone = WriteTex("FX_Flashlight", 128, (u, v) =>
        {
            // Narrow at v = 0 (the soldier), wide and faded at v = 1.
            float half = Mathf.Lerp(0.08f, 0.5f, v);
            float across = 1f - Mathf.Clamp01(Mathf.Abs(u - 0.5f) / Mathf.Max(0.001f, half));
            float a = Mathf.SmoothStep(0f, 1f, across) * Mathf.Clamp01(v * 6f) * (1f - v * v);
            return new Color(1f, 1f, 1f, a);
        }, false, TextureWrapMode.Clamp, 128);
        var go = new GameObject("WeatherFx");
        var fx = go.AddComponent<WeatherFx>();
        fx.flashlightMat = Mat("M_Flashlight", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", cone); x.SetColor("_BaseColor", new Color(1f, 0.92f, 0.7f, 0.5f)); x.SetFloat("_VertexColor", 0f); });
        fx.glowMat = Mat("M_NightGlow", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", new Color(1f, 0.9f, 0.7f, 0.3f)); x.SetFloat("_VertexColor", 0f); });
        fx.rainMat = Mat("M_Rain", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", new Color(0.7f, 0.8f, 1f, 0.6f)); x.SetFloat("_VertexColor", 1f); });
        fx.rainLoop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/rain_loop.ogg");
    }

    // ---------------------------------------------------------------- 1.1.0 district signature structures + set pieces
    /// <summary>
    /// Procedural (primitive) prefabs in the city's style, footprint ≤ 9 m so they fit between the sidewalks.
    /// Index = District * 2 + variant: Cadde metro entrance / overturned bus · Çarşı domed han / covered market ·
    /// Sanayi smokestacks / rail yard · Liman harbour crane / container yard. Tall parts fade under the camera.
    /// </summary>
    static void BuildLandmarks(CityKit k, CityMats c, Env env, Mats m)
    {
        const int B = GameLayers.Blocker, V = GameLayers.Vaultable;
        var brick = Mat("M_LM_Brick", "Universal Render Pipeline/Simple Lit", x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.62f, 0.38f, 0.28f)); });
        var sand = Mat("M_LM_Sand", "Universal Render Pipeline/Simple Lit", x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.86f, 0.76f, 0.58f)); });
        var dome = Mat("M_LM_Dome", "Universal Render Pipeline/Simple Lit", x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.35f, 0.5f, 0.55f)); });
        var rust = Mat("M_LM_Rust", "Universal Render Pipeline/Simple Lit", x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.5f, 0.27f, 0.16f)); });
        var charred = Mat("M_LM_Charred", "Universal Render Pipeline/Simple Lit", x => { x.SetTexture("_BaseMap", null); x.SetColor("_BaseColor", new Color(0.16f, 0.14f, 0.13f)); });
        var smoke = Mat("M_LM_Smoke", "Game/FxAlpha", x => { x.SetTexture("_BaseMap", m.muzzle.GetTexture("_BaseMap")); x.SetColor("_BaseColor", new Color(0.35f, 0.35f, 0.36f, 0.5f)); });
        Fadeable Fade(GameObject root, params GameObject[] parts)
        {
            var f = root.AddComponent<Fadeable>();
            f.renderers = parts.SelectMany(p => p.GetComponentsInChildren<Renderer>()).ToArray();
            f.fadeMaterial = c.stoneCut;
            return f;
        }
        var l = new GameObject[8];

        // ---- Cadde 0: metro entrance (low pavilion, stairs going down, blue "M" sign)
        l[0] = Procedural("LM_Metro", 4.2f, B, Surface.Concrete, root =>
        {
            var t = root.transform;
            Box("Floor", t, new Vector3(0f, 0.05f, 0f), new Vector3(8f, 0.1f, 6f), c.concrete);
            Box("WallL", t, new Vector3(-1.9f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 5f), c.stone);
            Box("WallR", t, new Vector3(1.9f, 0.6f, 0f), new Vector3(0.3f, 1.2f, 5f), c.stone);
            Box("WallB", t, new Vector3(0f, 0.6f, 2.35f), new Vector3(4.1f, 1.2f, 0.3f), c.stone);
            for (int i = 0; i < 6; i++) Box("Step" + i, t, new Vector3(0f, 0.11f, -2f + i * 0.75f), new Vector3(3.4f, 0.08f, 0.7f), i % 2 == 0 ? c.dark : c.steel);
            var hole = Box("Hole", t, new Vector3(0f, 0.12f, 0.6f), new Vector3(3.4f, 0.02f, 3f), c.dark);
            var roof = Box("Roof", t, new Vector3(0f, 2.7f, 0.4f), new Vector3(4.6f, 0.2f, 5.4f), c.glass);
            foreach (var x in new[] { -2.1f, 2.1f }) Box("RoofPost", t, new Vector3(x, 1.95f, 2.6f), new Vector3(0.15f, 1.5f, 0.15f), c.steel);
            Cyl("SignPole", t, new Vector3(3.2f, 1.6f, -2.4f), 0.08f, 3.2f, c.steel);
            Box("Sign", t, new Vector3(3.2f, 3.4f, -2.4f), new Vector3(0.9f, 0.9f, 0.15f), c.blue);
            Box("SignM", t, new Vector3(3.09f, 3.5f, -2.49f), new Vector3(0.09f, 0.42f, 0.02f), c.white, new Vector3(0f, 0f, 32f));
            Box("SignM4", t, new Vector3(3.31f, 3.5f, -2.49f), new Vector3(0.09f, 0.42f, 0.02f), c.white, new Vector3(0f, 0f, -32f));
            Box("SignM2", t, new Vector3(2.98f, 3.4f, -2.49f), new Vector3(0.1f, 0.55f, 0.02f), c.white);
            Box("SignM3", t, new Vector3(3.42f, 3.4f, -2.49f), new Vector3(0.1f, 0.55f, 0.02f), c.white);
            Col(root, new Vector3(-1.9f, 0.6f, 0f), new Vector3(0.4f, 1.2f, 5f));
            Col(root, new Vector3(1.9f, 0.6f, 0f), new Vector3(0.4f, 1.2f, 5f));
            Col(root, new Vector3(0f, 0.6f, 2.35f), new Vector3(4.1f, 1.2f, 0.4f));
            Col(root, new Vector3(0f, 0.6f, 0.6f), new Vector3(3.4f, 1.2f, 3f)); // the stair well itself (can't walk into it)
            Fade(root, roof);
        });
        // ---- Cadde 1: overturned city bus across the block + a few shed cases
        l[1] = Procedural("LM_Bus", 4.6f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            var bus = new GameObject("Bus").transform; bus.SetParent(t, false);
            bus.localRotation = Quaternion.Euler(0f, 20f, 88f); bus.localPosition = new Vector3(0f, 1.25f, 0f);
            Box("Body", bus, Vector3.zero, new Vector3(2.5f, 2.6f, 9f), c.yellow);
            Box("Stripe", bus, new Vector3(0f, -0.2f, 0f), new Vector3(2.52f, 0.35f, 9.02f), c.red);
            for (int i = 0; i < 6; i++) Box("Win" + i, bus, new Vector3(1.26f, 0.55f, -3.6f + i * 1.4f), new Vector3(0.02f, 0.8f, 1.1f), c.glass);
            Box("Front", bus, new Vector3(0f, 0.4f, 4.51f), new Vector3(2.2f, 1.2f, 0.02f), c.glass);
            foreach (var z in new[] { -3f, 3f })
                foreach (var x in new[] { -1.25f, 1.25f }) Cyl("Wheel", bus, new Vector3(x, -1.3f, z), 0.5f, 0.35f, c.dark, new Vector3(0f, 0f, 90f));
            var rotated = new GameObject("BusCollider"); rotated.transform.SetParent(t, false);
            rotated.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
            var rc = rotated.AddComponent<BoxCollider>(); rc.center = new Vector3(0f, 1.25f, 0f); rc.size = new Vector3(2.6f, 2.5f, 9f);
            Box("Case", t, new Vector3(3f, 0.3f, -2.5f), new Vector3(0.9f, 0.6f, 0.6f), c.khaki, new Vector3(0f, 30f, 0f));
            Box("Case2", t, new Vector3(-3.2f, 0.25f, 2.8f), new Vector3(0.7f, 0.5f, 0.5f), c.orange, new Vector3(0f, -15f, 0f));
        });
        // ---- Çarşı 0: domed han (stone hall, big dome, arched doors, four small domes)
        l[2] = Procedural("LM_Han", 4.6f, B, Surface.Concrete, root =>
        {
            var t = root.transform;
            var hall = Box("Hall", t, new Vector3(0f, 1.8f, 0f), new Vector3(8f, 3.6f, 8f), sand);
            Box("Base", t, new Vector3(0f, 0.15f, 0f), new Vector3(8.4f, 0.3f, 8.4f), c.stone);
            Box("Cornice", t, new Vector3(0f, 3.65f, 0f), new Vector3(8.3f, 0.2f, 8.3f), c.stone);
            var drum = Cyl("Drum", t, new Vector3(0f, 4.1f, 0f), 2.6f, 0.9f, sand);
            var big = Prim(PrimitiveType.Sphere, "Dome", t, new Vector3(0f, 4.55f, 0f), new Vector3(5.2f, 3.6f, 5.2f), dome);
            var minis = new List<GameObject>();
            foreach (var x in new[] { -2.9f, 2.9f })
                foreach (var z in new[] { -2.9f, 2.9f })
                    minis.Add(Prim(PrimitiveType.Sphere, "MiniDome", t, new Vector3(x, 3.75f, z), new Vector3(1.4f, 1f, 1.4f), dome));
            for (int s = 0; s < 4; s++)
            {
                var side = Quaternion.Euler(0f, s * 90f, 0f);
                Box("Door" + s, t, side * new Vector3(0f, 1.2f, 4.01f), new Vector3(1.6f, 2.4f, 0.05f), c.dark, new Vector3(0f, s * 90f, 0f));
                Prim(PrimitiveType.Sphere, "Arch" + s, t, side * new Vector3(0f, 2.4f, 4.0f), new Vector3(1.6f, 1.1f, 0.1f), c.dark, new Vector3(0f, s * 90f, 0f));
            }
            Col(root, new Vector3(0f, 1.8f, 0f), new Vector3(8f, 3.6f, 8f));
            var parts = new List<GameObject> { hall, drum, big }; parts.AddRange(minis);
            Fade(root, parts.ToArray());
        });
        // ---- Çarşı 1: covered market street (two rows of striped awnings over stalls)
        l[3] = Procedural("LM_Market", 4.6f, V, Surface.Concrete, root =>
        {
            var t = root.transform;
            Material[] stripes = { c.red, c.orange, c.blue, c.yellow };
            var roofs = new List<GameObject>();
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 4; i++)
                {
                    float x = row == 0 ? -2.6f : 2.6f, z = -3.3f + i * 2.2f;
                    Box("Stall", t, new Vector3(x, 0.5f, z), new Vector3(1.8f, 1f, 1.8f), c.khaki);
                    Box("Goods", t, new Vector3(x, 1.05f, z), new Vector3(1.5f, 0.12f, 1.5f), i % 2 == 0 ? c.orange : c.red);
                    roofs.Add(Box("Awning", t, new Vector3(x + (row == 0 ? 0.4f : -0.4f), 2.4f, z), new Vector3(2.6f, 0.08f, 2f), stripes[(i + row) % 4], new Vector3(0f, 0f, row == 0 ? -12f : 12f)));
                    Box("Post", t, new Vector3(x + (row == 0 ? 1.3f : -1.3f), 1.2f, z), new Vector3(0.08f, 2.4f, 0.08f), c.steel);
                    Col(root, new Vector3(x, 0.5f, z), new Vector3(1.8f, 1f, 1.8f));
                }
            Fade(root, roofs.ToArray());
        });
        // ---- Sanayi 0: twin smokestacks + pipe rack + smoke
        l[4] = Procedural("LM_Stacks", 4.4f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            Box("Plant", t, new Vector3(0f, 1.6f, 1.5f), new Vector3(7.5f, 3.2f, 4f), c.concrete);
            var tall = new List<GameObject>();
            foreach (var x in new[] { -2.2f, 1.8f })
            {
                tall.Add(Cyl("Stack", t, new Vector3(x, 6f, -1.8f), 0.75f, 12f, brick));
                tall.Add(Cyl("Band", t, new Vector3(x, 10.5f, -1.8f), 0.78f, 0.8f, c.white));
                tall.Add(Cyl("Band2", t, new Vector3(x, 11.6f, -1.8f), 0.78f, 0.6f, c.red));
                Col(root, new Vector3(x, 3f, -1.8f), new Vector3(1.5f, 6f, 1.5f));
                var sm = new GameObject("Smoke"); sm.transform.SetParent(t, false); sm.transform.localPosition = new Vector3(x, 12.1f, -1.8f);
                var ps = sm.AddComponent<ParticleSystem>();
                var main = ps.main; main.startLifetime = 4f; main.startSpeed = 1.4f; main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
                main.maxParticles = 14; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startColor = new Color(0.4f, 0.4f, 0.42f, 0.45f);
                var em = ps.emission; em.rateOverTime = 3f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12f; sh.radius = 0.4f;
                sm.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
                var col = ps.colorOverLifetime; col.enabled = true;
                var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
                col.color = gr;
                var pr = sm.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = smoke; pr.shadowCastingMode = ShadowCastingMode.Off;
            }
            for (int i = 0; i < 3; i++) Cyl("Pipe" + i, t, new Vector3(0f, 3.6f + i * 0.45f, -3.9f), 0.18f, 8.5f, i == 1 ? rust : c.steel, new Vector3(0f, 0f, 90f));
            foreach (var x in new[] { -3.6f, 0f, 3.6f }) Box("Rack", t, new Vector3(x, 2.1f, -3.9f), new Vector3(0.2f, 4.2f, 0.2f), c.steel);
            Col(root, new Vector3(0f, 1.6f, 1.5f), new Vector3(7.5f, 3.2f, 4f));
            Fade(root, tall.ToArray());
        });
        // ---- Sanayi 1: rail yard (track + two rusty freight wagons)
        l[5] = Procedural("LM_Rail", 4.6f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            for (int i = 0; i < 12; i++) Box("Sleeper" + i, t, new Vector3(0f, 0.04f, -4.4f + i * 0.8f), new Vector3(2.4f, 0.08f, 0.3f), c.dark);
            foreach (var x in new[] { -0.75f, 0.75f }) Box("Rail", t, new Vector3(x, 0.12f, 0f), new Vector3(0.1f, 0.12f, 9.6f), c.steel);
            for (int w = 0; w < 2; w++)
            {
                float z = w == 0 ? -2.3f : 2.4f;
                Box("Wagon" + w, t, new Vector3(0f, 1.55f, z), new Vector3(2.6f, 2.3f, 4.2f), w == 0 ? rust : c.blue);
                Box("Door" + w, t, new Vector3(1.31f, 1.5f, z), new Vector3(0.04f, 1.8f, 1.6f), c.dark);
                foreach (var dz in new[] { -1.4f, 1.4f }) foreach (var x in new[] { -0.75f, 0.75f }) Cyl("Wheel", t, new Vector3(x, 0.4f, z + dz), 0.35f, 0.15f, c.dark, new Vector3(0f, 0f, 90f));
                Col(root, new Vector3(0f, 1.4f, z), new Vector3(2.6f, 2.8f, 4.2f));
            }
            Box("Signal", t, new Vector3(2.4f, 1.8f, -4.2f), new Vector3(0.15f, 3.6f, 0.15f), c.steel);
            Box("Lamp", t, new Vector3(2.4f, 3.5f, -4.12f), new Vector3(0.3f, 0.5f, 0.12f), c.red);
        });
        // ---- Liman 0: harbour gantry crane (legs straddle the lot, boom overhead)
        l[6] = Procedural("LM_Crane", 4.6f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            var top = new List<GameObject>();
            foreach (var x in new[] { -3.6f, 3.6f })
                foreach (var z in new[] { -2.4f, 2.4f })
                {
                    top.Add(Box("Leg", t, new Vector3(x, 5f, z), new Vector3(0.45f, 10f, 0.45f), c.yellow));
                    Col(root, new Vector3(x, 1.5f, z), new Vector3(0.7f, 3f, 0.7f));
                    Box("Bogie", t, new Vector3(x, 0.3f, z), new Vector3(0.9f, 0.6f, 1.4f), c.dark);
                }
            foreach (var z in new[] { -2.4f, 2.4f }) top.Add(Box("Beam", t, new Vector3(0f, 10f, z), new Vector3(7.7f, 0.6f, 0.6f), c.yellow));
            top.Add(Box("Boom", t, new Vector3(-2f, 10.7f, 0f), new Vector3(14f, 0.7f, 1.2f), c.yellow));
            top.Add(Box("Cab", t, new Vector3(-1f, 9.6f, 0f), new Vector3(1.6f, 1.3f, 1.6f), c.white));
            top.Add(Box("CabGlass", t, new Vector3(-1f, 9.6f, -0.81f), new Vector3(1.3f, 0.8f, 0.02f), c.glass));
            top.Add(Box("Cable", t, new Vector3(1.5f, 7.6f, 0f), new Vector3(0.05f, 5.6f, 0.05f), c.dark));
            var cont = Box("Container", t, new Vector3(1.5f, 4.3f, 0f), new Vector3(2.4f, 2.4f, 5.8f), c.red);
            top.Add(cont);
            Fade(root, top.ToArray());
        });
        // ---- Liman 1: container yard (stacked boxes, a forklift)
        l[7] = Procedural("LM_Containers", 4.6f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            Material[] cols = { c.red, c.blue, c.orange, c.khaki, rust };
            var high = new List<GameObject>();
            int n = 0;
            foreach (var x in new[] { -2.6f, 0.2f })
                for (int h = 0; h < (x < 0f ? 3 : 2); h++)
                {
                    var g = Box("Box" + n, t, new Vector3(x, 1.25f + h * 2.5f, -1.2f), new Vector3(2.4f, 2.5f, 6f), cols[n++ % cols.Length]);
                    if (h > 0) high.Add(g);
                }
            Col(root, new Vector3(-1.2f, 2.5f, -1.2f), new Vector3(5.2f, 5f, 6f));
            Box("Fork", t, new Vector3(3.2f, 0.8f, 2.6f), new Vector3(1.2f, 1.2f, 1.8f), c.yellow);
            Box("ForkMast", t, new Vector3(3.2f, 1.4f, 3.6f), new Vector3(0.9f, 2.4f, 0.15f), c.dark);
            Col(root, new Vector3(3.2f, 0.8f, 2.8f), new Vector3(1.3f, 1.6f, 2.2f));
            Fade(root, high.ToArray());
        });
        k.landmarks = l;

        // ---- Rare set pieces
        var s = new GameObject[3];
        s[0] = Procedural("SP_HeliCrash", 4.6f, B, Surface.Metal, root =>
        {
            var t = root.transform;
            var body = new GameObject("Wreck").transform; body.SetParent(t, false);
            body.localRotation = Quaternion.Euler(8f, 25f, 22f); body.localPosition = new Vector3(0f, 0.6f, 0f);
            Prim(PrimitiveType.Sphere, "Hull", body, new Vector3(0f, 0.9f, 0f), new Vector3(2.1f, 1.8f, 4f), charred);
            Prim(PrimitiveType.Sphere, "Cockpit", body, new Vector3(0f, 1.1f, 1.45f), new Vector3(1.7f, 1.3f, 1.7f), c.dark);
            Cyl("Boom", body, new Vector3(0f, 1.2f, -3.1f), 0.22f, 3f, charred, new Vector3(80f, 0f, 0f));
            Box("BladeA", body, new Vector3(0.8f, 2f, 0.6f), new Vector3(5.5f, 0.05f, 0.3f), c.dark, new Vector3(0f, 0f, -18f));
            Box("BladeB", t, new Vector3(-3.2f, 0.06f, 2.6f), new Vector3(0.3f, 0.05f, 4.2f), c.dark, new Vector3(0f, 40f, 0f));
            Box("Scorch", t, new Vector3(0f, 0.02f, 0f), new Vector3(7f, 0.02f, 7f), c.dark);
            Col(root, new Vector3(0f, 1f, 0f), new Vector3(2.4f, 2f, 5.5f));
        });
        s[1] = Procedural("SP_Checkpoint", 4.6f, V, Surface.Concrete, root =>
        {
            var t = root.transform;
            // Sandbag walls (vaultable), a tent, a crate stack, a flag.
            for (int i = 0; i < 4; i++)
            {
                var side = Quaternion.Euler(0f, i * 90f, 0f);
                for (int b = -2; b <= 2; b++)
                {
                    if (i == 0 && b == 0) continue; // the gate gap
                    Box("Bag", t, side * new Vector3(b * 1.1f, 0.35f, 4f), new Vector3(1.05f, 0.7f, 0.6f), c.khaki, new Vector3(0f, i * 90f, 0f));
                }
                Col(root, side * new Vector3(i == 0 ? -1.65f : 0f, 0.35f, 4f), i % 2 == 0 ? new Vector3(i == 0 ? 2.2f : 5.5f, 0.7f, 0.6f) : new Vector3(0.6f, 0.7f, 5.5f));
            }
            Col(root, new Vector3(1.65f, 0.35f, 4f), new Vector3(2.2f, 0.7f, 0.6f));
            var tent = Prim(PrimitiveType.Cube, "Tent", t, new Vector3(-1.2f, 0.9f, -0.8f), new Vector3(2.6f, 1.8f, 3f), c.khaki, new Vector3(0f, 0f, 45f));
            tent.transform.localScale = new Vector3(1.8f, 1.8f, 3f);
            Box("Crates", t, new Vector3(1.8f, 0.5f, 1.2f), new Vector3(1.2f, 1f, 1.2f), c.khaki);
            Cyl("FlagPole", t, new Vector3(2.6f, 2f, -2.4f), 0.06f, 4f, c.steel);
            Box("Flag", t, new Vector3(3.1f, 3.6f, -2.4f), new Vector3(1f, 0.6f, 0.03f), c.red);
            Col(root, new Vector3(-1.2f, 0.9f, -0.8f), new Vector3(2.6f, 1.8f, 3f));
        });
        s[2] = Procedural("SP_BurningBlock", 4.6f, B, Surface.Concrete, root =>
        {
            var t = root.transform;
            var shell = Box("Shell", t, new Vector3(0f, 2.4f, 0f), new Vector3(7.5f, 4.8f, 7f), charred);
            Box("Collapse", t, new Vector3(2.4f, 4.9f, 1.8f), new Vector3(2.8f, 0.4f, 3.2f), c.dark, new Vector3(18f, 0f, -12f));
            for (int i = 0; i < 5; i++) Box("Win" + i, t, new Vector3(-3f + i * 1.5f, 2.6f, -3.51f), new Vector3(0.9f, 1.2f, 0.02f), c.dark);
            var flames = Mat("M_LM_Flame", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", m.muzzle.GetTexture("_BaseMap")); x.SetColor("_BaseColor", new Color(1f, 0.45f, 0.12f, 1f)); x.SetFloat("_VertexColor", 1f); });
            foreach (var p in new[] { new Vector3(-2.4f, 2.8f, -3.6f), new Vector3(1.2f, 5.2f, 0f), new Vector3(2.8f, 1.2f, -3.6f) })
            {
                var f = new GameObject("Fire"); f.transform.SetParent(t, false); f.transform.localPosition = p; f.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                var ps = f.AddComponent<ParticleSystem>();
                var main = ps.main; main.startLifetime = 0.9f; main.startSpeed = 2.2f; main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.3f); main.maxParticles = 18;
                main.startColor = new Color(1f, 0.6f, 0.2f, 0.9f);
                var em = ps.emission; em.rateOverTime = 14f;
                var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 15f; sh.radius = 0.5f;
                var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
                var pr = f.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = flames; pr.shadowCastingMode = ShadowCastingMode.Off;
            }
            Col(root, new Vector3(0f, 2.4f, 0f), new Vector3(7.5f, 4.8f, 7f));
            Fade(root, shell);
        });
        k.specials = s;
    }
}
