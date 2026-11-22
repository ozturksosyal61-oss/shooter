using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>F13 — weapons (Quaternius Ultimate Gun Pack, CC0), costumes, run rewards UI, shop / loadout / daily panels, preview stage.</summary>
public static partial class GameSceneBuilder
{
    struct GunSpec { public WeaponId id; public string glb; public Vector3 right, left, muzzle; }

    // Grip / muzzle points on the normalised models (barrel +Z, up +Y; see Tools/gunbounds in the F13 notes).
    static readonly GunSpec[] Guns =
    {
        new GunSpec { id = WeaponId.Smg, glb = "Assets/Art/Weapons/Smg/Smg.glb", right = new Vector3(0f, 0.166f, 0.19f), left = new Vector3(0f, 0.186f, 0.385f), muzzle = new Vector3(0f, 0.219f, 0.63f) },
        new GunSpec { id = WeaponId.Shotgun, glb = "Assets/Art/Weapons/Shotgun/Shotgun.glb", right = new Vector3(0f, 0.078f, 0.285f), left = new Vector3(0f, 0.1f, 0.59f), muzzle = new Vector3(0f, 0.128f, 0.96f) },
        new GunSpec { id = WeaponId.Hmg, glb = "Assets/Art/Weapons/Hmg/Hmg.glb", right = new Vector3(0f, 0.18f, 0.58f), left = new Vector3(0f, 0.205f, 0.8f), muzzle = new Vector3(0f, 0.235f, 1.01f) },
        new GunSpec { id = WeaponId.Uzi, glb = "Assets/Art/Weapons/Uzi/Uzi.glb", right = new Vector3(0f, 0.15f, 0.215f), left = new Vector3(0f, 0.17f, 0.375f), muzzle = new Vector3(0f, 0.222f, 0.51f) },
    };
    static readonly Vector3 RifleRootPos = new Vector3(0.1f, 0.95f, 0.2f);
    const float GunScale = 1.1f;

    /// <summary>Texture costumes on the soldier mesh (whole-character costumes have their own model).</summary>
    static IEnumerable<string> CostumeNames() => System.Enum.GetNames(typeof(CostumeId)).Where(n => n != nameof(CostumeId.Standard) && !Economy.IsCharacter((CostumeId)System.Enum.Parse(typeof(CostumeId), n)));
    static string CostumeTex(string name) => $"{SoldierDir}/Textures/Costumes/Soldier_{name}.png";
    static string SoldierDir => System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(SoldierTex)).Replace('\\', '/');

    static Material GunMat(string name)
    {
        Color c;
        switch (name)
        {
            case "Black": c = new Color(0.08f, 0.08f, 0.09f); break;
            case "DarkMetal": case "MainDark": c = new Color(0.13f, 0.13f, 0.15f); break;
            case "Metal": case "Main": c = new Color(0.22f, 0.23f, 0.25f); break;
            case "Grey": case "Grey2": case "DarkGrey": c = new Color(0.34f, 0.35f, 0.37f); break;
            case "LightMetal": case "MainLight": c = new Color(0.4f, 0.41f, 0.44f); break;
            case "Wood": c = new Color(0.5f, 0.33f, 0.18f); break;
            case "DarkWood": c = new Color(0.33f, 0.21f, 0.12f); break;
            case "Green": c = new Color(0.27f, 0.34f, 0.2f); break;
            case "Glass": c = new Color(0.25f, 0.4f, 0.5f); break;
            default: c = new Color(0.22f, 0.23f, 0.25f); break;
        }
        return RifleMat("M_Gun_" + name, c);
    }

    /// <summary>A gun model with RightGrip / LeftGrip / Muzzle → MuzzleFlash under <paramref name="parent"/> (no colliders).</summary>
    static Transform BuildGun(GunSpec g, Transform parent, Mats m, out Transform right, out Transform left, out Transform muzzle, out Transform flash)
    {
        var root = new GameObject(g.id.ToString()).transform;
        root.SetParent(parent, false);
        root.localScale = Vector3.one * GunScale;
        var inst = Object.Instantiate(LoadGo(g.glb));
        foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
        {
            var go = new GameObject("Mesh");
            go.transform.SetParent(root, false);
            go.transform.localPosition = inst.transform.InverseTransformPoint(mf.transform.position);
            go.transform.localRotation = Quaternion.Inverse(inst.transform.rotation) * mf.transform.rotation;
            go.transform.localScale = mf.transform.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials.Select(x => GunMat(x ? x.name : "Metal")).ToArray();
            mr.shadowCastingMode = ShadowCastingMode.On;
        }
        Object.DestroyImmediate(inst);
        right = new GameObject("RightGrip").transform; right.SetParent(root, false); right.localPosition = g.right; right.localRotation = Quaternion.Euler(0f, 0f, -90f);
        left = new GameObject("LeftGrip").transform; left.SetParent(root, false); left.localPosition = g.left; left.localRotation = Quaternion.Euler(0f, 0f, 90f);
        muzzle = new GameObject("Muzzle").transform; muzzle.SetParent(root, false); muzzle.localPosition = g.muzzle;
        flash = null;
        AttachGunSkin(root);
        return root;
    }

    static Material[] CostumeMaterials(Material standard)
    {
        var mats = new Material[System.Enum.GetValues(typeof(CostumeId)).Length];
        mats[(int)CostumeId.Standard] = standard;
        foreach (var n in CostumeNames())
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(CostumeTex(n));
            var id = (CostumeId)System.Enum.Parse(typeof(CostumeId), n);
            mats[(int)id] = Mat("M_Soldier_" + n, "Universal Render Pipeline/Simple Lit", x =>
            {
                x.SetTexture("_BaseMap", tex); x.SetColor("_BaseColor", Color.white);
                if (id == CostumeId.Gold) { x.SetFloat("_SpecularHighlights", 1f); x.SetColor("_SpecColor", new Color(1f, 0.85f, 0.5f)); x.SetFloat("_Smoothness", 0.7f); x.EnableKeyword("_SPECULAR_COLOR"); }
            });
        }
        return mats;
    }

    /// <summary>Player prefab: all five weapons (M4 = the existing rifle), loadout component, costume materials.</summary>
    static void AddLoadout(GameObject go, PlayerController pc, GameObject model, Transform rifle, Transform rGrip, Transform lGrip, Transform muzzle, Transform flash)
    {
        var lo = go.AddComponent<WeaponLoadout>();
        var slots = new List<WeaponLoadout.Slot> { new WeaponLoadout.Slot { id = WeaponId.M4, root = rifle, rightGrip = rGrip, leftGrip = lGrip, muzzle = muzzle, flash = flash } };
        Vector3 rifleGrip = RifleRootPos + RifleRightGrip * 1.1f;
        foreach (var g in Guns)
        {
            var root = BuildGun(g, go.transform, null, out var r, out var l, out var mz, out _);
            // Same right-hand spot as the M4: the root moves so the grips line up.
            root.localPosition = rifleGrip - g.right * GunScale;
            var fl = Object.Instantiate(flash.gameObject, mz).transform;
            fl.name = "MuzzleFlash";
            fl.localPosition = new Vector3(0f, 0f, 0.22f);
            fl.localRotation = flash.localRotation;
            fl.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
            slots.Add(new WeaponLoadout.Slot { id = g.id, root = root, rightGrip = r, leftGrip = l, muzzle = mz, flash = fl });
        }
        lo.slots = slots.OrderBy(s => (int)s.id).ToArray();
        lo.body = model.GetComponentInChildren<SkinnedMeshRenderer>();
        lo.costumeMaterials = CostumeMaterials(lo.body.sharedMaterial);
    }

    // ---------------------------------------------------------------- F15: whole-character costumes
    static readonly (CostumeId id, string name)[] PlayerCharacters = { (CostumeId.SpecOps, "OzelHarekat"), (CostumeId.Firefighter, "Itfaiyeci"), (CostumeId.Hush, "Hush"), (CostumeId.Lynx, "Lynx") };

    /// <summary>Material of a player character (own texture, colours untouched); null when its rig is missing.</summary>
    static Material PlayerCharacterMaterial(string name)
    {
        string dir = $"Assets/Art/Characters/{name}";
        if (!System.IO.File.Exists(Rigged(dir))) { Debug.LogWarning($"BUILD: player character {name} missing — run Tools/prepare_players.py"); return null; }
        ConfigureCharacterTexture(BaseColor(dir));
        ConfigureRig(Rigged(dir), false);
        return Mat("M_" + name, "Universal Render Pipeline/Simple Lit", x => { x.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColor(dir))); x.SetColor("_BaseColor", Color.white); });
    }

    /// <summary>Extra character models on the Player prefab (inactive); <see cref="WeaponLoadout.ApplyCostume"/> swaps them in.</summary>
    static void AddPlayerCharacters(GameObject go, GameObject soldierModel, AnimatorController controller, SoldierRig soldierRig)
    {
        var lo = go.GetComponent<WeaponLoadout>();
        lo.defaultModel = soldierModel;
        lo.costumeModels = new GameObject[System.Enum.GetValues(typeof(CostumeId)).Length];
        foreach (var (id, name) in PlayerCharacters)
        {
            var mat = PlayerCharacterMaterial(name);
            if (!mat) continue;
            var cm = AddRiggedModel(go.transform, Rigged($"Assets/Art/Characters/{name}"), mat, 1.8f, controller, true);
            cm.name = name;
            var r = cm.AddComponent<SoldierRig>();
            r.walkClipSpeed = soldierRig.walkClipSpeed; r.runClipSpeed = soldierRig.runClipSpeed; r.reloadClipLength = soldierRig.reloadClipLength;
            r.weaponRoot = soldierRig.weaponRoot; r.rightGrip = soldierRig.rightGrip; r.leftGrip = soldierRig.leftGrip;
            cm.SetActive(false);
            lo.costumeModels[(int)id] = cm;
        }
    }

    // ---------------------------------------------------------------- menu: preview stage (own camera → RenderTexture)
    static PreviewStage BuildPreviewStage(Mats m, out RenderTexture rt)
    {
        var stage = new GameObject("PreviewStage").AddComponent<PreviewStage>();
        // Built at the origin (models are placed from their world bounds), moved out of sight at the end.
        rt = new RenderTexture(768, 768, 24) { name = "PreviewRT" };
        AssetDatabase.DeleteAsset("Assets/Art/Textures/PreviewRT.renderTexture");
        AssetDatabase.CreateAsset(rt, "Assets/Art/Textures/PreviewRT.renderTexture");

        var camGo = new GameObject("PreviewCamera");
        camGo.transform.SetParent(stage.transform, false);
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.14f, 0.09f, 1f);
        cam.cullingMask = 1 << GameLayers.Preview;
        cam.fieldOfView = 30f; cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
        cam.targetTexture = rt;
        camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        stage.cam = cam;

        var sp = new GameObject("SoldierPivot").transform;
        sp.SetParent(stage.transform, false);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{AnimOut}/Soldier.controller");
        var model = AddRiggedModel(sp, SoldierRigged, m.soldier, 1.8f, ctrl, true);
        model.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
        stage.soldierPivot = sp;
        stage.soldierBody = model.GetComponentInChildren<SkinnedMeshRenderer>();
        stage.costumeMaterials = CostumeMaterials(m.soldier);
        stage.soldierModel = model;
        stage.costumeModels = new GameObject[System.Enum.GetValues(typeof(CostumeId)).Length];
        foreach (var (id, name) in PlayerCharacters)
        {
            var mat = PlayerCharacterMaterial(name);
            if (!mat) continue;
            var cm = AddRiggedModel(sp, Rigged($"Assets/Art/Characters/{name}"), mat, 1.8f, ctrl, true);
            cm.name = name;
            cm.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            cm.SetActive(false);
            stage.costumeModels[(int)id] = cm;
        }
        // The character holds the selected weapon (same aim pose + hand IK as in the game).
        model.AddComponent<SoldierRig>();
        foreach (var cm in stage.costumeModels) if (cm) cm.AddComponent<SoldierRig>();
        var held = new PreviewStage.Held[System.Enum.GetValues(typeof(WeaponId)).Length];
        var hr = BuildRifle(sp, m, out var hrg, out var hlg, out _, out var hfl);
        Object.DestroyImmediate(hfl.gameObject);
        held[(int)WeaponId.M4] = new PreviewStage.Held { root = hr, rightGrip = hrg, leftGrip = hlg };
        Vector3 rifleGrip = RifleRootPos + RifleRightGrip * 1.1f;
        foreach (var g in Guns)
        {
            var root = BuildGun(g, sp, m, out var r, out var l, out _, out _);
            root.localPosition = rifleGrip - g.right * GunScale;
            held[(int)g.id] = new PreviewStage.Held { root = root, rightGrip = r, leftGrip = l };
        }
        foreach (var h in held) if (h != null && h.root) h.root.gameObject.SetActive(false);
        stage.heldWeapons = held;
        stage.soldierCamPos = new Vector3(0f, 1.25f, 4.2f);
        // 1.1.0 hero poses: each character / skin with its own weapon and pose.
        int nc = System.Enum.GetValues(typeof(CostumeId)).Length;
        stage.heroWeapon = new WeaponId[nc]; stage.heroPose = new int[nc];
        for (int i = 0; i < nc; i++) { stage.heroWeapon[i] = WeaponId.M4; stage.heroPose[i] = -1; }
        foreach (var h in HeroLooks)
        {
            bool has = System.IO.File.Exists($"{SoldierAnim}/Poses/{PoseDefs[h.pose].file}.fbx");
            stage.heroWeapon[(int)h.c] = h.w; stage.heroPose[(int)h.c] = has ? h.pose : -1;
        }
        stage.poseStates = PoseDefs.Select(p => p.state).ToArray();
        stage.poseTwoHanded = PoseDefs.Select(p => p.twoHanded).ToArray();
        stage.poseLoops = PoseDefs.Select(p => p.loop).ToArray();
        stage.posePitch = PoseDefs.Select(p => p.pitch).ToArray();
        stage.soldierLook = new Vector3(0f, 0.95f, 0f);

        var wp = new GameObject("WeaponPivot").transform;
        wp.SetParent(stage.transform, false);
        wp.localPosition = new Vector3(0f, 1f, 0f);
        var weapons = new Transform[System.Enum.GetValues(typeof(WeaponId)).Length];
        var rifle = BuildRifle(wp, m, out _, out _, out _, out var rf);
        Object.DestroyImmediate(rf.gameObject);
        rifle.localPosition = new Vector3(0f, -0.15f, -0.47f);
        weapons[(int)WeaponId.M4] = rifle;
        foreach (var g in Guns)
        {
            var root = BuildGun(g, wp, m, out _, out _, out _, out _);
            root.localPosition = new Vector3(0f, -0.12f, -g.muzzle.z * GunScale * 0.5f);
            weapons[(int)g.id] = root;
        }
        stage.weapons = weapons;
        stage.weaponPivot = wp;
        stage.weaponCamPos = new Vector3(0.4f, 1.35f, 2.1f);
        stage.weaponLook = new Vector3(0f, 1f, 0f);
        stage.tracerQuads = PreviewTracers(stage.transform, m);
        stage.transform.position = new Vector3(0f, -400f, 0f);
        foreach (var smr in stage.GetComponentsInChildren<SkinnedMeshRenderer>(true)) smr.updateWhenOffscreen = true;
        SetLayer(stage.gameObject, GameLayers.Preview);
        return stage;
    }

}
