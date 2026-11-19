using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// F14 — the later lab mutations (Sıçrayan, Kovan Ana, Ezici): Mixamo rigs from Tools/prepare_bosses.py, clips
/// retargeted through the humanoid avatars (Kasap's + the special zombies'), each one bigger than the last
/// (<see cref="BossRules.TypeScale"/>), with its own ring colour, eye glow and body glows, and its abilities switched
/// on in <see cref="ButcherZombie"/>.
/// </summary>
public static partial class GameSceneBuilder
{
    class MutantSpec
    {
        public string name;
        public BossType type;
        public float radius, capHeight, ringScale;
        public Color ring, eye;
        public AnimationClip idle, walk, run, roar, slam, punch, dying, throwClip;
        public (HumanBodyBones bone, Vector3 offset, float size)[] glows;
        public System.Action<ButcherZombie> tune;
    }

    static void BuildMutantBosses(Mats m, Tex t, CharacterAnims a, BossAssets kasap)
    {
        var specs = new[]
        {
            new MutantSpec
            {
                name = "Sicrayan", type = BossType.Leaper, radius = 0.95f, capHeight = 4.8f, ringScale = 5.2f,
                ring = new Color(0.45f, 1f, 0.2f, 1f), eye = new Color(0.55f, 1f, 0.25f, 1f),
                idle = kasap.idle, walk = a.tSneak, run = a.bRun, roar = a.sRoar, slam = kasap.slam, punch = a.sPunch, dying = a.sDying,
                // Toxic veins down the long legs.
                glows = new[] { (HumanBodyBones.LeftLowerLeg, new Vector3(0f, 0f, 0.05f), 0.5f), (HumanBodyBones.RightLowerLeg, new Vector3(0f, 0f, 0.05f), 0.5f) },
                tune = k =>
                {
                    k.canCharge = false; k.canThrow = false; k.canLeap = true;
                    k.speedMul = 1.3f;
                    k.slamRadius = 4.5f; k.slamTelegraph = 0.65f; k.slamDamage = 22f; k.slamTrigger = 3.8f;
                    k.leapDamage = 30f; k.leapRadius = 4.5f; k.leapCooldown = 4.2f;
                },
            },
            new MutantSpec
            {
                name = "KovanAna", type = BossType.Hive, radius = 1.25f, capHeight = 5.2f, ringScale = 6f,
                ring = new Color(1f, 0.62f, 0.1f, 1f), eye = new Color(1f, 0.7f, 0.15f, 1f),
                idle = kasap.idle, walk = a.bWalk, run = a.sWalk, roar = kasap.roar, slam = kasap.slam, punch = a.sPunch, dying = kasap.dying,
                throwClip = a.tThrow,
                // The egg sacs on her back pulse.
                glows = new[]
                {
                    (HumanBodyBones.UpperChest, new Vector3(0f, 0.02f, -0.12f), 0.9f),
                    (HumanBodyBones.LeftShoulder, new Vector3(0f, 0.03f, -0.1f), 0.6f),
                    (HumanBodyBones.RightShoulder, new Vector3(0f, 0.03f, -0.1f), 0.6f),
                },
                tune = k =>
                {
                    k.canCharge = false; k.canThrow = true; k.canBrood = true;
                    k.speedMul = 0.85f;
                    k.throwMinDistance = 6f; k.throwCooldown = 2.4f; k.throwWind = 0.6f; k.throwCount = 2; k.throwCountRage = 3;
                    k.broodCooldown = 13f; k.broodCount = 4; k.broodExposed = 2.6f;
                    k.slamRadius = 5f; k.slamDamage = 24f;
                },
            },
            new MutantSpec
            {
                name = "Ezici", type = BossType.Crusher, radius = 1.6f, capHeight = 5.5f, ringScale = 7f,
                ring = new Color(0.7f, 0.25f, 1f, 1f), eye = new Color(0.8f, 0.35f, 1f, 1f),
                idle = kasap.idle, walk = a.sWalk, run = kasap.run, roar = kasap.roar, slam = kasap.slam, punch = a.sPunch, dying = a.sDying,
                // The chemical tank in its spine.
                glows = new[] { (HumanBodyBones.Chest, new Vector3(0f, 0.04f, -0.15f), 1f) },
                tune = k =>
                {
                    k.canCharge = true; k.canThrow = true; k.crushWalk = true;
                    k.frontArmor = 0.35f; k.frontArc = 70f; k.backDamageMul = 1.3f;
                    k.speedMul = 0.9f;
                    k.chargeSpeed = 13f; k.chargeDamage = 36f; k.stunTime = 1.3f; k.tiredTime = 2.6f;
                    k.slamRadius = 7f; k.slamDamage = 32f; k.slamTrigger = 5.5f; k.slamTelegraph = 0.9f;
                },
            },
        };
        foreach (var s in specs) BuildMutant(s, m, t, kasap);
    }

    static void BuildMutant(MutantSpec s, Mats m, Tex t, BossAssets kasap)
    {
        string dir = $"Assets/Art/Characters/{s.name}";
        if (!System.IO.File.Exists(Rigged(dir))) { Debug.LogWarning($"BUILD: {s.name} rig missing ({Rigged(dir)}) — run Tools/prepare_bosses.py"); return; }
        ConfigureCharacterTexture(BaseColor(dir));
        var avatar = ConfigureRig(Rigged(dir), false); // not optimised: glows hang off bones
        if (!avatar || !avatar.isHuman) Debug.LogWarning($"BUILD: {s.name} avatar is not humanoid");

        const string simpleLit = "Universal Render Pipeline/Simple Lit";
        var body = Mat("M_" + s.name, simpleLit, x => { x.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColor(dir))); x.SetColor("_BaseColor", Color.white); });
        var ring = Mat("M_Ring_" + s.name, "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.ringTex); x.SetColor("_BaseColor", s.ring); x.SetFloat("_VertexColor", 0f); });
        var glow = Mat("M_Glow_" + s.name, "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", s.eye); x.SetFloat("_VertexColor", 0f); });

        var extras = s.throwClip ? new[] { ("Throw", s.throwClip, true) } : new (string, AnimationClip, bool)[0];
        var ctrl = BuildMonsterController(s.name, s.idle, s.walk, s.run, s.roar, s.slam, s.punch ? s.punch : s.slam, s.idle, s.dying, extras);
        float height = 1.8f * BossRules.TypeScale[(int)s.type];
        var k = MonsterPrefab<ButcherZombie>(s.name, Rigged(dir), m, body, ring, height, 1f, ctrl, s.radius, s.capHeight, out var go);
        k.kind = ZombieKind.Boss;
        k.bossType = s.type;
        k.ringTint = s.ring;
        k.ring.localScale = new Vector3(s.ringScale, s.ringScale, 1f);
        k.ringRenderer = k.ring.GetComponent<Renderer>();
        k.walkClipSpeed = ClipSpeed(s.walk);
        k.runClipSpeed = ClipSpeed(s.run);
        k.walkSpeedFallback = 2.3f; k.runSpeedFallback = 6f;
        k.dyingLength = s.dying.length;
        k.riseDuration = 1.2f;
        k.screamDuration = Mathf.Min(s.roar.length * 0.85f, 2.6f);
        k.separationRadius = s.radius + 0.6f; k.lookAhead = 2.6f + s.radius; k.turnSpeed = 150f; k.contactRange = s.radius + 1f;
        k.deathThrow = 0f;
        k.score = 400 + 150 * (int)s.type; k.xp = 0;
        k.aimBias = 3f + s.radius;
        k.slamImpactTime = s.slam.length * 0.5f;
        k.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        s.tune(k);

        // Telegraphs (same red danger language as Kasap): charge strip, slam / landing ring + outline.
        var strip = Quad("ChargeStrip", go.transform, kasap.strip);
        strip.SetActive(false);
        k.chargeStrip = strip.transform;
        var zone = Quad("SlamZone", go.transform, kasap.telegraph);
        zone.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        zone.SetActive(false);
        k.slamZone = zone.transform;
        var sring = Quad("SlamRing", go.transform, kasap.telegraph);
        sring.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        sring.SetActive(false);
        k.slamRing = sring.transform;

        if (s.type == BossType.Hive) k.projectiles = AcidGlobs(go.transform, t, 3);
        else if (k.canThrow) k.projectiles = DebrisProjectiles(go.transform, kasap.telegraph, 2);

        // Glowing eyes + body glows (pulse with rage; billboarded by the boss).
        var head = k.animator.GetBoneTransform(HumanBodyBones.Head);
        var list = new List<Transform>();
        var smr = k.model.GetComponentInChildren<SkinnedMeshRenderer>();
        foreach (var e in FindEyes(smr, head, go.transform, height, null, out _))
        {
            var q = Quad("EyeGlow", head ? head : k.model, glow);
            q.transform.position = e;
            float sz = 0.36f * height / BossHeight / q.transform.parent.lossyScale.x;
            q.transform.localScale = new Vector3(sz, sz, sz);
            list.Add(q.transform);
        }
        foreach (var g in s.glows)
        {
            var bone = k.animator.GetBoneTransform(g.bone);
            if (!bone) continue;
            var q = Quad("BodyGlow", bone, glow);
            q.transform.position = bone.position + go.transform.TransformVector(g.offset * height);
            float sz = g.size * height / BossHeight / bone.lossyScale.x;
            q.transform.localScale = new Vector3(sz, sz, sz);
            list.Add(q.transform);
        }
        k.eyes = list.ToArray();
        Debug.Log($"BUILD: {s.name} h={height:0.0} m walk={k.walkClipSpeed:0.00} run={k.runClipSpeed:0.00} glows={list.Count} humanoid={(avatar && avatar.isHuman)}");
        SetLayer(go, GameLayers.Zombie);
        SavePrefab(go, $"{PrefabDir}/{s.name}.prefab");
    }

    /// <summary>Kovan Ana's acid: glowing globs that leave burning pools.</summary>
    static BossProjectile[] AcidGlobs(Transform parent, Tex t, int count)
    {
        var globMat = Mat("M_AcidGlob", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", new Color(0.6f, 1f, 0.2f, 1f)); x.SetFloat("_VertexColor", 0f); });
        var coreMat = Mat("M_AcidCore", "Universal Render Pipeline/Simple Lit", x => { x.SetColor("_BaseColor", new Color(0.55f, 0.9f, 0.15f)); x.SetTexture("_BaseMap", null); });
        var poolMat = Mat("M_AcidPool", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.ringTex); x.SetColor("_BaseColor", new Color(1f, 0.15f, 0.05f, 1f)); x.SetFloat("_VertexColor", 0f); });
        var list = new List<BossProjectile>();
        for (int i = 0; i < count; i++)
        {
            var pgo = new GameObject("Acid" + i);
            pgo.transform.SetParent(parent, false);
            var pr = pgo.AddComponent<BossProjectile>();
            var meshRoot = new GameObject("Mesh").transform;
            meshRoot.SetParent(pgo.transform, false);
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(core.GetComponent<Collider>());
            core.transform.SetParent(meshRoot, false);
            core.transform.localScale = Vector3.one * 0.7f;
            core.GetComponent<MeshRenderer>().sharedMaterial = coreMat;
            var halo = Quad("Halo", meshRoot, globMat);
            halo.transform.localScale = Vector3.one * 1.8f;
            pr.mesh = meshRoot;
            var mk = Quad("Marker", pgo.transform, poolMat);
            pr.marker = mk.transform;
            pr.markerRenderer = mk.GetComponent<Renderer>();
            pr.acid = true;
            pr.radius = 2.2f; pr.damage = 14f; pr.arcHeight = 5f;
            pr.poolRadius = 2.6f; pr.poolTime = 4.5f; pr.poolDps = 10f;
            pgo.SetActive(false);
            list.Add(pr);
        }
        return list.ToArray();
    }

    /// <summary>Hurled wreckage (Kasap's, reused by Ezici).</summary>
    static BossProjectile[] DebrisProjectiles(Transform parent, Material telegraph, int count)
    {
        var parts = new[] { "Debris_Door", "Debris_Bumper" };
        var projs = new List<BossProjectile>();
        for (int i = 0; i < count; i++)
        {
            var pgo = new GameObject("Debris" + i);
            pgo.transform.SetParent(parent, false);
            var pr = pgo.AddComponent<BossProjectile>();
            var meshRoot = new GameObject("Mesh").transform;
            meshRoot.SetParent(pgo.transform, false);
            var src = LoadGo($"{EnvPrefabDir}/{parts[i % parts.Length]}.prefab");
            if (src)
            {
                var inst = Object.Instantiate(src);
                foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
                {
                    var c = new GameObject("Part");
                    c.transform.SetParent(meshRoot, false);
                    c.transform.localPosition = inst.transform.InverseTransformPoint(mf.transform.position);
                    c.transform.localRotation = mf.transform.rotation;
                    c.transform.localScale = mf.transform.lossyScale;
                    c.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    c.AddComponent<MeshRenderer>().sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
                }
                Object.DestroyImmediate(inst);
            }
            meshRoot.localScale = Vector3.one * 1.8f;
            foreach (var col in meshRoot.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            pr.mesh = meshRoot;
            var mk = Quad("Marker", pgo.transform, telegraph);
            pr.marker = mk.transform;
            pr.damage = 30f; pr.radius = 3.2f;
            pgo.SetActive(false);
            projs.Add(pr);
        }
        return projs.ToArray();
    }
}
