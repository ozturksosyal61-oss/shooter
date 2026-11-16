using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Animated characters: Mixamo Humanoid import (avatars, loop / root settings), animator controllers,
/// avatar mask, the rifle and the Player / Zombie prefabs.
/// </summary>
public static partial class GameSceneBuilder
{
    const string SoldierRigged = "Assets/Art/Characters/Soldier/Soldier_Rigged.fbx";
    const string ZombieRigged = "Assets/Art/Characters/Zombie/Zombie_Rigged.fbx";
    const string SoldierAnim = "Assets/Art/Characters/Soldier/Anim";
    const string ZombieAnim = "Assets/Art/Characters/Zombie/Anim";
    const string AnimOut = "Assets/Animation";
    const string RifleGlb = "Assets/Art/Weapons/Rifle/Rifle.glb";

    // Rifle grip points in the normalized rifle space (barrel along +Z, length 0.85 m).
    static readonly Vector3 RifleRightGrip = new Vector3(0f, 0.17f, 0.27f);
    static readonly Vector3 RifleLeftGrip = new Vector3(0f, 0.19f, 0.55f);
    static readonly Vector3 RifleMuzzle = new Vector3(0f, 0.223f, 0.86f);

    static readonly string[] Dirs = { "forward", "forward right", "right", "backward right", "backward", "backward left", "left", "forward left" };
    static readonly Vector2[] DirVecs =
    {
        new Vector2(0, 1), new Vector2(0.7071f, 0.7071f), new Vector2(1, 0), new Vector2(0.7071f, -0.7071f),
        new Vector2(0, -1), new Vector2(-0.7071f, -0.7071f), new Vector2(-1, 0), new Vector2(-0.7071f, 0.7071f),
    };

    class CharacterAnims
    {
        public Avatar soldierAvatar, zombieAvatar;
        public AnimationClip idle, idleAim, jump, reload, deathFront, deathBack;
        public AnimationClip[] walk = new AnimationClip[8], run = new AnimationClip[8];
        public AnimationClip zIdle, zWalk, zRun, zAttack, zPunch, zHit, zDying, zScream;
        public bool screamIsSubstitute;
        // Special zombies (lab mutations): own rigs, clips retargeted through the humanoid avatar.
        public Avatar shieldAvatar, bombAvatar, tongueAvatar;
        public AnimationClip sWalk, sPunch, sRoar, sDying;
        public AnimationClip bWalk, bRun, bSwell;
        public AnimationClip tSneak, tThrow, tPull;
    }

    const string ShieldDir = "Assets/Art/Characters/ShieldZombie";
    const string BombDir = "Assets/Art/Characters/BombZombie";
    const string TongueDir = "Assets/Art/Characters/TongueZombie";
    static string Rigged(string dir) => $"{dir}/{System.IO.Path.GetFileName(dir)}_Rigged.fbx";
    static string BaseColor(string dir) => $"{dir}/Textures/{System.IO.Path.GetFileName(dir)}_BaseColor.png";

    // ---------------------------------------------------------------- import
    static Avatar ConfigureRig(string path, bool optimize)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        imp.animationType = ModelImporterAnimationType.Human;
        imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        imp.importAnimation = false;
        imp.materialImportMode = ModelImporterMaterialImportMode.None;
        imp.optimizeGameObjects = optimize;
        imp.SaveAndReimport();
        var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new System.Exception("Invalid humanoid avatar: " + path);
        return avatar;
    }

    /// <summary>Humanoid clip import. Root motion is never applied at runtime; rotation/height are baked so the body stays upright.</summary>
    static AnimationClip ConfigureClip(string path, Avatar source, string name, bool loop, bool bakeXZ, AnimationEvent[] events = null)
    {
        var imp = (ModelImporter)AssetImporter.GetAtPath(path);
        if (imp == null) throw new System.Exception("Missing animation: " + path);
        if (imp.animationType != ModelImporterAnimationType.Human || imp.sourceAvatar != source)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            imp.sourceAvatar = source;
            imp.importAnimation = true;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();
        }
        var c = imp.defaultClipAnimations.First();
        c.name = name;
        c.loopTime = loop;
        c.loopPose = false;
        c.lockRootRotation = true;
        c.keepOriginalOrientation = true;
        c.lockRootHeightY = true;
        c.keepOriginalPositionY = true;
        c.lockRootPositionXZ = bakeXZ;
        c.keepOriginalPositionXZ = true;
        c.events = events ?? new AnimationEvent[0];
        imp.clipAnimations = new[] { c };
        imp.SaveAndReimport();
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(x => !x.name.StartsWith("__preview__"));
    }

    static CharacterAnims ImportCharacterAnimations()
    {
        var a = new CharacterAnims();
        a.soldierAvatar = ConfigureRig(SoldierRigged, false);
        a.zombieAvatar = ConfigureRig(ZombieRigged, true);

        string S(string f) => $"{SoldierAnim}/{f}.fbx";
        a.idle = ConfigureClip(S("idle"), a.soldierAvatar, "Idle", true, false);
        a.idleAim = ConfigureClip(S("idle aiming"), a.soldierAvatar, "IdleAiming", true, false);
        a.jump = ConfigureClip(S("jump loop"), a.soldierAvatar, "Vault", true, false);
        // Reload sound parts are fired by animation events (normalised times on the reload clip).
        AnimationEvent Ev(float t, string part) => new AnimationEvent { time = t, functionName = "OnReloadEvent", stringParameter = part };
        a.reload = ConfigureClip(S("reload"), a.soldierAvatar, "Reload", false, true,
            new[] { Ev(0.22f, "MagOut"), Ev(0.55f, "MagIn"), Ev(0.8f, "Bolt") });
        a.deathFront = ConfigureClip(S("death from the front"), a.soldierAvatar, "DeathFront", false, true);
        a.deathBack = ConfigureClip(S("death from the back"), a.soldierAvatar, "DeathBack", false, true);
        for (int i = 0; i < 8; i++)
        {
            a.walk[i] = ConfigureClip(S("walk " + Dirs[i]), a.soldierAvatar, "Walk " + Dirs[i], true, false);
            a.run[i] = ConfigureClip(S("run " + Dirs[i]), a.soldierAvatar, "Run " + Dirs[i], true, false);
        }

        string Z(string f) => $"{ZombieAnim}/{f}.fbx";
        a.zIdle = ConfigureClip(Z("zombie idle"), a.zombieAvatar, "ZIdle", true, false);
        a.zWalk = ConfigureClip(Z("walking"), a.zombieAvatar, "ZWalk", true, false);
        a.zRun = ConfigureClip(Z("zombie running"), a.zombieAvatar, "ZRun", true, false);
        a.zAttack = ConfigureClip(Z("zombie attack"), a.zombieAvatar, "ZAttack", false, true);
        a.zPunch = ConfigureClip(Z("zombie punching"), a.zombieAvatar, "ZPunch", false, true);
        a.zHit = ConfigureClip(Z("zombie reaction hit"), a.zombieAvatar, "ZHit", false, true);
        a.zDying = ConfigureClip(Z("dying"), a.zombieAvatar, "ZDying", false, true);
        // Mixamo "Zombie Scream" if present, otherwise "Zombie Agonizing" stands in.
        bool hasScream = AssetImporter.GetAtPath(Z("zombie scream")) != null;
        a.screamIsSubstitute = !hasScream;
        a.zScream = ConfigureClip(Z(hasScream ? "zombie scream" : "zombie agonizing"), a.zombieAvatar, "ZScream", false, true);

        // The special clips were downloaded on a standard 65-bone Mixamo skeleton -> retarget via the zombie avatar.
        a.shieldAvatar = ConfigureRig(Rigged(ShieldDir), true);
        a.bombAvatar = ConfigureRig(Rigged(BombDir), true);
        a.tongueAvatar = ConfigureRig(Rigged(TongueDir), true);
        string SA(string dir, string f) => $"{dir}/Anim/{f}.fbx";
        a.sWalk = ConfigureClip(SA(ShieldDir, "mutant walking"), a.zombieAvatar, "ShieldWalk", true, false);
        a.sPunch = ConfigureClip(SA(ShieldDir, "mutant punch"), a.zombieAvatar, "ShieldPunch", false, true);
        a.sRoar = ConfigureClip(SA(ShieldDir, "mutant roaring"), a.zombieAvatar, "ShieldRoar", false, true);
        a.sDying = ConfigureClip(SA(ShieldDir, "mutant dying"), a.zombieAvatar, "ShieldDying", false, true);
        a.bWalk = ConfigureClip(SA(BombDir, "drunk walk"), a.zombieAvatar, "BombWalk", true, false);
        a.bRun = ConfigureClip(SA(BombDir, "bomb running"), a.zombieAvatar, "BombRun", true, false);
        a.bSwell = ConfigureClip(SA(BombDir, "bomb agonizing"), a.zombieAvatar, "BombSwell", false, true);
        a.tSneak = ConfigureClip(SA(TongueDir, "sneaking forward"), a.zombieAvatar, "TongueSneak", true, false);
        a.tThrow = ConfigureClip(SA(TongueDir, "frisbee throw"), a.zombieAvatar, "TongueThrow", false, true);
        a.tPull = ConfigureClip(SA(TongueDir, "pulling a rope"), a.zombieAvatar, "TonguePull", true, true);
        return a;
    }

    /// <summary>Root speed of a clip on the unscaled avatar (multiply by the model scale at runtime); 0 for in-place clips.</summary>
    static float ClipSpeed(AnimationClip clip)
    {
        float v = new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude;
        return v > 0.05f ? v : 0f;
    }

    // ---------------------------------------------------------------- controllers
    static AvatarMask UpperBodyMask()
    {
        string path = $"{AnimOut}/UpperBody.mask";
        AssetDatabase.DeleteAsset(path);
        var mask = new AvatarMask();
        var upper = new HashSet<AvatarMaskBodyPart>
        {
            AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
            AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK,
        };
        for (var p = AvatarMaskBodyPart.Root; p < AvatarMaskBodyPart.LastBodyPart; p++)
            mask.SetHumanoidBodyPartActive(p, upper.Contains(p));
        AssetDatabase.CreateAsset(mask, path);
        return mask;
    }

    static AnimatorController NewController(string name)
    {
        System.IO.Directory.CreateDirectory(AnimOut);
        string path = $"{AnimOut}/{name}.controller";
        AssetDatabase.DeleteAsset(path);
        return AnimatorController.CreateAnimatorControllerAtPath(path);
    }

    static AnimatorController BuildSoldierController(CharacterAnims a)
    {
        var c = NewController("Soldier");
        c.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        c.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        c.AddParameter(new AnimatorControllerParameter { name = "LocoSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        c.AddParameter("Vaulting", AnimatorControllerParameterType.Bool);
        c.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        c.AddParameter("DeathIndex", AnimatorControllerParameterType.Int);
        c.AddParameter(new AnimatorControllerParameter { name = "ReloadSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        // Base layer: 8-way locomotion (walk at radius 0.5, run at 1), vault, death.
        var sm = c.layers[0].stateMachine;
        var loco = c.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveY";
        tree.AddChild(a.idle, Vector2.zero);
        for (int i = 0; i < 8; i++)
        {
            tree.AddChild(a.walk[i], DirVecs[i] * 0.5f);
            tree.AddChild(a.run[i], DirVecs[i]);
        }
        loco.speedParameterActive = true;
        loco.speedParameter = "LocoSpeed";
        sm.defaultState = loco;

        var vault = sm.AddState("Vault");
        vault.motion = a.jump;
        var toVault = sm.AddAnyStateTransition(vault);
        toVault.AddCondition(AnimatorConditionMode.If, 0, "Vaulting");
        toVault.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
        toVault.canTransitionToSelf = false; toVault.duration = 0.08f; toVault.hasExitTime = false;
        var fromVault = vault.AddTransition(loco);
        fromVault.AddCondition(AnimatorConditionMode.IfNot, 0, "Vaulting");
        fromVault.duration = 0.12f; fromVault.hasExitTime = false;

        var clips = new[] { a.deathFront, a.deathBack };
        for (int i = 0; i < 2; i++)
        {
            var d = sm.AddState(i == 0 ? "DeathFront" : "DeathBack");
            d.motion = clips[i];
            var t = sm.AddAnyStateTransition(d);
            t.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            t.AddCondition(AnimatorConditionMode.Equals, i, "DeathIndex");
            t.canTransitionToSelf = false; t.duration = 0.12f; t.hasExitTime = false;
        }

        var layers = c.layers;
        layers[0].iKPass = true;
        c.layers = layers;

        // Upper-body layer: aim / reload (weight set from code).
        var upperSm = new AnimatorStateMachine { name = "UpperBody", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(upperSm, c);
        var aim = upperSm.AddState("Aim");
        aim.motion = a.idleAim;
        var reload = upperSm.AddState("Reload");
        reload.motion = a.reload;
        reload.speedParameterActive = true;
        reload.speedParameter = "ReloadSpeed";
        var back = reload.AddTransition(aim);
        back.hasExitTime = true; back.exitTime = 0.95f; back.duration = 0.1f;
        upperSm.defaultState = aim;
        c.AddLayer(new AnimatorControllerLayer
        {
            name = "UpperBody", stateMachine = upperSm, avatarMask = UpperBodyMask(),
            defaultWeight = 0f, blendingMode = AnimatorLayerBlendingMode.Override,
        });
        EditorUtility.SetDirty(c);
        return c;
    }

    static AnimatorController BuildZombieController(CharacterAnims a) =>
        BuildMonsterController("Zombie", a.zIdle, a.zWalk, a.zRun, a.zScream, a.zAttack, a.zPunch, a.zHit, a.zDying);

    /// <summary>
    /// Zombie-style controller: Locomotion (Gait 0 idle / 1 walk / 2 run, speed AnimSpeed), Scream, Attack, Punch,
    /// Hit, Dying + optional extra states (name, clip, return to locomotion when done).
    /// </summary>
    static AnimatorController BuildMonsterController(string name, AnimationClip idle, AnimationClip walk, AnimationClip run,
        AnimationClip scream, AnimationClip attack, AnimationClip punch, AnimationClip hit, AnimationClip dying,
        params (string name, AnimationClip clip, bool returns)[] extras)
    {
        var c = NewController(name);
        c.AddParameter("Gait", AnimatorControllerParameterType.Float);
        c.AddParameter(new AnimatorControllerParameter { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        var sm = c.layers[0].stateMachine;

        var loco = c.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Gait";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 1f);
        tree.AddChild(run, 2f);
        loco.speedParameterActive = true;
        loco.speedParameter = "AnimSpeed";
        sm.defaultState = loco;

        AnimatorState Add(string n, Motion mo) { var st = sm.AddState(n); st.motion = mo; return st; }
        void Back(AnimatorState st, float exit, float dur)
        {
            var t = st.AddTransition(loco);
            t.hasExitTime = true; t.exitTime = exit; t.duration = dur;
        }
        Back(Add("Scream", scream), 0.95f, 0.25f);
        Back(Add("Attack", attack), 0.85f, 0.2f);
        Back(Add("Punch", punch), 0.85f, 0.2f);
        Add("Hit", hit);
        Add("Dying", dying);
        foreach (var e in extras)
        {
            var st = Add(e.name, e.clip);
            if (e.returns) Back(st, 0.95f, 0.2f);
        }
        EditorUtility.SetDirty(c);
        return c;
    }

    // ---------------------------------------------------------------- models
    /// <summary>Instantiates a rigged FBX as "Model" under <paramref name="root"/>, scaled to <paramref name="height"/>, feet on y=0.</summary>
    static GameObject AddRiggedModel(Transform root, string fbx, Material mat, float height, AnimatorController controller, bool alwaysAnimate, float widthMul = 1f)
    {
        var src = LoadGo(fbx);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(src);
        model.name = "Model";
        model.transform.SetParent(root, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var r in smrs)
        {
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            r.shadowCastingMode = ShadowCastingMode.On;
            r.updateWhenOffscreen = false;
        }
        Bounds b = smrs[0].bounds;
        foreach (var r in smrs) b.Encapsulate(r.bounds);
        float s = height / b.size.y;
        model.transform.localScale = new Vector3(s * widthMul, s, s * widthMul);
        b = smrs[0].bounds;
        foreach (var r in smrs) b.Encapsulate(r.bounds);
        model.transform.localPosition = new Vector3(0f, -b.min.y, 0f);

        var anim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = alwaysAnimate ? AnimatorCullingMode.AlwaysAnimate : AnimatorCullingMode.CullUpdateTransforms;
        return model;
    }

    static Material RifleMat(string name, Color c) =>
        Mat(name, "Universal Render Pipeline/Simple Lit", x => { x.SetColor("_BaseColor", c); x.SetTexture("_BaseMap", null); });

    /// <summary>Rifle hierarchy (meshes + RightGrip / LeftGrip / Muzzle → MuzzleFlash) under <paramref name="parent"/>.</summary>
    static Transform BuildRifle(Transform parent, Mats m, out Transform rightGrip, out Transform leftGrip, out Transform muzzle, out Transform flash)
    {
        var mats = new Dictionary<string, Material>
        {
            ["Main"] = RifleMat("M_Rifle_Main", new Color(0.2f, 0.21f, 0.23f)),
            ["MainDark"] = RifleMat("M_Rifle_Dark", new Color(0.11f, 0.11f, 0.13f)),
            ["MainLight"] = RifleMat("M_Rifle_Light", new Color(0.36f, 0.37f, 0.4f)),
        };
        var root = new GameObject("Rifle").transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * 1.1f; // a bit oversized so it reads well from the top-down camera
        var inst = Object.Instantiate(LoadGo(RifleGlb));
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
        {
            var go = new GameObject("Mesh");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            var src = mf.GetComponent<MeshRenderer>().sharedMaterials;
            mr.sharedMaterials = src.Select(x => x != null && mats.TryGetValue(x.name, out var mm) ? mm : mats["Main"]).ToArray();
            mr.shadowCastingMode = ShadowCastingMode.On;
        }
        Object.DestroyImmediate(inst);

        rightGrip = new GameObject("RightGrip").transform;
        rightGrip.SetParent(root, false);
        rightGrip.localPosition = RifleRightGrip;
        leftGrip = new GameObject("LeftGrip").transform;
        leftGrip.SetParent(root, false);
        leftGrip.localPosition = RifleLeftGrip;
        leftGrip.localRotation = Quaternion.Euler(0f, 0f, 90f);
        rightGrip.localRotation = Quaternion.Euler(0f, 0f, -90f);
        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(root, false);
        muzzle.localPosition = RifleMuzzle;
        var q = Quad("MuzzleFlash", muzzle, m.muzzle);
        q.transform.localPosition = new Vector3(0f, 0f, 0.22f);
        q.SetActive(false);
        flash = q.transform;
        return root;
    }

    // ---------------------------------------------------------------- prefabs
    static void BuildZombiePrefab(Mats m, CharacterAnims a)
    {
        var controller = BuildZombieController(a);
        var go = new GameObject("Zombie");
        var model = AddRiggedModel(go.transform, ZombieRigged, m.zombie, 1.8f, controller, false);
        var cap = go.AddComponent<CapsuleCollider>();
        cap.radius = 0.35f; cap.height = 1.7f; cap.center = new Vector3(0f, 0.85f, 0f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None;
        var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var flash = go.AddComponent<HitFlash>();
        flash.renderers = new Renderer[] { smr };
        flash.flashMaterial = m.flash;
        var z = go.AddComponent<Zombie>();
        z.model = model.transform;
        z.animator = model.GetComponent<Animator>();
        z.flash = flash;
        z.body = cap;
        z.bodyRenderer = smr;
        z.walkerMaterial = m.zombie;
        z.runnerMaterial = m.zombieRunner;
        z.walkClipSpeed = ClipSpeed(a.zWalk);
        z.runClipSpeed = ClipSpeed(a.zRun);
        z.dyingLength = a.zDying.length;
        Debug.Log($"BUILD: zombie walk={z.walkClipSpeed:0.00} run={z.runClipSpeed:0.00} dying={z.dyingLength:0.00}s scream={(a.screamIsSubstitute ? "agonizing (substitute)" : "scream")} {a.zScream.length:0.00}s");
        SetLayer(go, GameLayers.Zombie);
        SavePrefab(go, $"{PrefabDir}/Zombie.prefab");
    }

    /// <summary>Shared shell of a special zombie prefab: model, capsule, kinematic body, flash, ring, component.</summary>
    static T MonsterPrefab<T>(string name, string fbx, Mats m, Material mat, Material ringMat, float height, float widthMul,
        AnimatorController controller, float radius, float capHeight, out GameObject go) where T : Zombie
    {
        go = new GameObject(name);
        var model = AddRiggedModel(go.transform, fbx, mat, height, controller, false, widthMul);
        var cap = go.AddComponent<CapsuleCollider>();
        cap.radius = radius; cap.height = capHeight; cap.center = new Vector3(0f, capHeight * 0.5f, 0f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None;
        var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var flash = go.AddComponent<HitFlash>();
        flash.renderers = new Renderer[] { smr };
        flash.flashMaterial = m.flash;
        var ring = Quad("Ring", go.transform, ringMat);
        ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        // Scaled for the 17.5 m camera (was 3.6× at 14 m).
        ring.transform.localScale = new Vector3(radius * 4.4f, radius * 4.4f, 1f);
        var z = go.AddComponent<T>();
        z.model = model.transform;
        z.animator = model.GetComponent<Animator>();
        z.flash = flash;
        z.body = cap;
        z.bodyRenderer = smr;
        z.walkerMaterial = mat;
        z.runnerMaterial = null;
        z.ring = ring.transform;
        z.riseDepth = height + 0.1f;
        return z;
    }

    static void BuildSpecialZombiePrefabs(Mats m, CharacterAnims a)
    {
        // Kalkanli: ~1.4x wider than a walker, slow and heavy.
        var sc = BuildMonsterController("ShieldZombie", a.zIdle, a.sWalk, a.sWalk, a.sRoar, a.sPunch, a.sPunch, a.zHit, a.sDying);
        var shield = MonsterPrefab<ShieldZombie>("ShieldZombie", Rigged(ShieldDir), m, m.shieldZombie, m.ringShield, 2.1f, 1.22f, sc, 0.62f, 2f, out var sgo);
        shield.kind = ZombieKind.Shield;
        shield.contactRange = 1.4f; shield.contactCooldown = 1.3f; shield.separationRadius = 1.2f; shield.lookAhead = 1.8f;
        shield.walkClipSpeed = ClipSpeed(a.sWalk); shield.runClipSpeed = shield.walkClipSpeed;
        shield.walkSpeedFallback = shield.runSpeedFallback = 1.3f;
        shield.dyingLength = a.sDying.length; shield.deathThrow = 0.3f; shield.turnSpeed = 95f;
        shield.screamDuration = Mathf.Min(a.sRoar.length * 0.85f, 2.2f);
        shield.score = 60; shield.xp = 8;
        SetLayer(sgo, GameLayers.Zombie);
        SavePrefab(sgo, $"{PrefabDir}/ShieldZombie.prefab");

        // Bombali: short and ~1.3x bloated sideways.
        var bc = BuildMonsterController("BombZombie", a.zIdle, a.bWalk, a.bRun, a.zScream, a.zAttack, a.zPunch, a.zHit, a.zDying,
            ("Swell", a.bSwell, false));
        var bomb = MonsterPrefab<BombZombie>("BombZombie", Rigged(BombDir), m, m.bombZombie, m.ringBomb, 1.65f, 1.3f, bc, 0.55f, 1.6f, out var bgo);
        bomb.kind = ZombieKind.Bomb;
        bomb.contactRange = 1.1f; bomb.separationRadius = 1f;
        bomb.walkClipSpeed = ClipSpeed(a.bWalk); bomb.runClipSpeed = ClipSpeed(a.bRun);
        bomb.dyingLength = a.zDying.length; bomb.screamDuration = 0.9f;
        bomb.score = 30; bomb.xp = 5;
        SetLayer(bgo, GameLayers.Zombie);
        SavePrefab(bgo, $"{PrefabDir}/BombZombie.prefab");

        // Dilci: ~1.3x taller, lean; tongue lines + hitbox.
        var tc = BuildMonsterController("TongueZombie", a.zIdle, a.tSneak, a.tSneak, a.zScream, a.zAttack, a.zPunch, a.zHit, a.zDying,
            ("Throw", a.tThrow, true), ("Pull", a.tPull, false));
        var tongue = MonsterPrefab<TongueZombie>("TongueZombie", Rigged(TongueDir), m, m.tongueZombie, m.ringTongue, 2.35f, 1f, tc, 0.36f, 2.2f, out var tgo);
        tongue.kind = ZombieKind.Tongue;
        tongue.contactRange = 0.95f;
        tongue.walkClipSpeed = ClipSpeed(a.tSneak); tongue.runClipSpeed = tongue.walkClipSpeed;
        tongue.dyingLength = a.zDying.length; tongue.deathThrow = 0.8f;
        tongue.mouthHeight = 2.05f;
        tongue.score = 45; tongue.xp = 7;
        LineRenderer Line(string n, Material mat, float w)
        {
            var lgo = new GameObject(n);
            lgo.transform.SetParent(tgo.transform, false);
            var lr = lgo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; lr.positionCount = 2; lr.sharedMaterial = mat;
            lr.startWidth = lr.endWidth = w; lr.numCapVertices = 4; lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }
        tongue.tongueLine = Line("Tongue", m.tongue, 0.17f);
        tongue.telegraphLine = Line("Telegraph", m.telegraph, 0.08f);
        var hbGo = new GameObject("TongueHitbox");
        hbGo.transform.SetParent(tgo.transform, false);
        var hb = hbGo.AddComponent<CapsuleCollider>();
        hb.direction = 2; hb.radius = 0.32f; hb.height = 2f; hb.enabled = false;
        hbGo.AddComponent<TongueHitbox>().owner = tongue;
        tongue.tongueHitbox = hb;
        SetLayer(tgo, GameLayers.Zombie);
        SavePrefab(tgo, $"{PrefabDir}/TongueZombie.prefab");

        Debug.Log($"BUILD: specials shield walk={shield.walkClipSpeed:0.00} roar={a.sRoar.length:0.0}s | bomb walk={bomb.walkClipSpeed:0.00} run={bomb.runClipSpeed:0.00} swell={a.bSwell.length:0.0}s | tongue sneak={tongue.walkClipSpeed:0.00} throw={a.tThrow.length:0.0}s pull={a.tPull.length:0.0}s");
    }

    static void BuildPlayerPrefab(Mats m, Bullet bullet, CharacterAnims a)
    {
        var controller = BuildSoldierController(a);
        var go = new GameObject("Player");
        var model = AddRiggedModel(go.transform, SoldierRigged, m.soldier, 1.8f, controller, true);
        var cc = go.AddComponent<CharacterController>();
        cc.radius = 0.35f; cc.height = 1.7f; cc.center = new Vector3(0f, 0.85f, 0f);
        cc.stepOffset = 0.25f; cc.skinWidth = 0.04f; cc.minMoveDistance = 0f;

        // Readability: bright ring under the soldier.
        var ring = Quad("Ring", go.transform, m.ringSoldier);
        ring.transform.localPosition = new Vector3(0f, 0.035f, 0f);
        ring.transform.localScale = new Vector3(1.9f, 1.9f, 1f); // bright ring, sized for the 17.5 m camera

        var rifle = BuildRifle(go.transform, m, out var rGrip, out var lGrip, out var muzzle, out var mf);
        rifle.localPosition = new Vector3(0.1f, 0.95f, 0.2f);

        var rig = model.AddComponent<SoldierRig>();
        rig.weaponRoot = rifle;
        rig.rightGrip = rGrip;
        rig.leftGrip = lGrip;
        float scale = model.transform.localScale.x;
        rig.walkClipSpeed = ClipSpeed(a.walk[0]);
        rig.runClipSpeed = ClipSpeed(a.run[0]);
        rig.reloadClipLength = a.reload.length;
        Debug.Log($"BUILD: soldier walk={rig.walkClipSpeed:0.00} run={rig.runClipSpeed:0.00} reload={rig.reloadClipLength:0.00}s scale={scale:0.000}");

        var flash = go.AddComponent<HitFlash>();
        flash.renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>().Cast<Renderer>().ToArray();
        flash.flashMaterial = m.flash;

        var weapon = go.AddComponent<Weapon>();
        weapon.muzzle = muzzle;
        weapon.muzzleFlash = mf;
        weapon.bulletPrefab = bullet;
        weapon.rig = rig;

        var pc = go.AddComponent<PlayerController>();
        AddLoadout(go, pc, model, rifle, rGrip, lGrip, muzzle, mf);
        pc.model = model.transform;
        pc.rig = rig;
        pc.weapon = weapon;
        pc.flash = flash;
        weapon.owner = pc;
        SetLayer(go, GameLayers.Player);
        SavePrefab(go, $"{PrefabDir}/Player.prefab");
    }
}
