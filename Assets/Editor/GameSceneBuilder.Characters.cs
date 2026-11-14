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
    }

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
    static AnimationClip ConfigureClip(string path, Avatar source, string name, bool loop, bool bakeXZ)
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
        a.reload = ConfigureClip(S("reload"), a.soldierAvatar, "Reload", false, true);
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

    static AnimatorController BuildZombieController(CharacterAnims a)
    {
        var c = NewController("Zombie");
        c.AddParameter("Gait", AnimatorControllerParameterType.Float);
        c.AddParameter(new AnimatorControllerParameter { name = "AnimSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        var sm = c.layers[0].stateMachine;

        var loco = c.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Gait";
        tree.useAutomaticThresholds = false;
        tree.AddChild(a.zIdle, 0f);
        tree.AddChild(a.zWalk, 1f);
        tree.AddChild(a.zRun, 2f);
        loco.speedParameterActive = true;
        loco.speedParameter = "AnimSpeed";
        sm.defaultState = loco;

        AnimatorState Add(string n, Motion m) { var s = sm.AddState(n); s.motion = m; return s; }
        var scream = Add("Scream", a.zScream);
        var attack = Add("Attack", a.zAttack);
        var punch = Add("Punch", a.zPunch);
        Add("Hit", a.zHit);
        Add("Dying", a.zDying);
        foreach (var s in new[] { attack, punch })
        {
            var t = s.AddTransition(loco);
            t.hasExitTime = true; t.exitTime = 0.85f; t.duration = 0.2f;
        }
        var ts = scream.AddTransition(loco);
        ts.hasExitTime = true; ts.exitTime = 0.95f; ts.duration = 0.25f;
        EditorUtility.SetDirty(c);
        return c;
    }

    // ---------------------------------------------------------------- models
    /// <summary>Instantiates a rigged FBX as "Model" under <paramref name="root"/>, scaled to <paramref name="height"/>, feet on y=0.</summary>
    static GameObject AddRiggedModel(Transform root, string fbx, Material mat, float height, AnimatorController controller, bool alwaysAnimate)
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
        model.transform.localScale = Vector3.one * s;
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

    static void BuildPlayerPrefab(Mats m, Bullet bullet, CharacterAnims a)
    {
        var controller = BuildSoldierController(a);
        var go = new GameObject("Player");
        var model = AddRiggedModel(go.transform, SoldierRigged, m.soldier, 1.8f, controller, true);
        var cc = go.AddComponent<CharacterController>();
        cc.radius = 0.35f; cc.height = 1.7f; cc.center = new Vector3(0f, 0.85f, 0f);
        cc.stepOffset = 0.25f; cc.skinWidth = 0.04f; cc.minMoveDistance = 0f;

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
        pc.model = model.transform;
        pc.rig = rig;
        pc.weapon = weapon;
        pc.flash = flash;
        weapon.owner = pc;
        SetLayer(go, GameLayers.Player);
        SavePrefab(go, $"{PrefabDir}/Player.prefab");
    }
}
