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
        new GunSpec { id = WeaponId.Sniper, glb = "Assets/Art/Weapons/Sniper/Sniper.glb", right = new Vector3(0f, 0.075f, 0.345f), left = new Vector3(0f, 0.095f, 0.69f), muzzle = new Vector3(0f, 0.125f, 1.16f) },
    };
    static readonly Vector3 RifleRootPos = new Vector3(0.1f, 0.95f, 0.2f);
    const float GunScale = 1.1f;

    static IEnumerable<string> CostumeNames() => System.Enum.GetNames(typeof(CostumeId)).Where(n => n != nameof(CostumeId.Standard));
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

    // ---------------------------------------------------------------- game scene: run rewards on the game-over panel
    static void BuildRewardsUi(HUD hud, TouchControls controls)
    {
        var go = hud.transform.Find("GameOver");
        var host = new GameObject("RunRewards").AddComponent<RunRewards>();
        host.transform.SetParent(hud.transform, false);
        var root = Rect("Rewards", go);
        Anchor(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1900f, 1000f));
        host.root = root.gameObject;
        host.goldText = Txt("Gold", root, 66, TextAnchor.MiddleCenter, UiGold);
        Anchor(host.goldText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(900f, 90f));
        var coin = Img("Coin", root, coinIcon, UiGold);
        Anchor(coin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -110f), new Vector2(70f, 70f));
        host.doubleButton = MakeButton("Double", root, null, UiToxic, new Vector2(0.5f, 0.5f), new Vector2(-620f, -230f), new Vector2(460f, 110f), 34, UiInk, 0f);
        host.doubleLabel = host.doubleButton.GetComponentInChildren<Text>();
        host.continueAdButton = MakeButton("ContinueAd", root, null, UiOrange, new Vector2(0.5f, 0.5f), new Vector2(620f, -170f), new Vector2(460f, 100f), 32, UiInk, 0f);
        host.continueAdLabel = host.continueAdButton.GetComponentInChildren<Text>();
        host.continueGemButton = MakeButton("ContinueGems", root, null, new Color(0.25f, 0.55f, 0.75f), new Vector2(0.5f, 0.5f), new Vector2(620f, -285f), new Vector2(460f, 100f), 32, UiCream, 0f);
        host.continueGemLabel = host.continueGemButton.GetComponentInChildren<Text>();
        host.message = Txt("Message", root, 34, TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.3f));
        Anchor(host.message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(620f, -360f), new Vector2(600f, 50f));
        var list = controls.uiButtons.ToList();
        list.Add(host.doubleButton.GetComponent<RectTransform>());
        list.Add(host.continueAdButton.GetComponent<RectTransform>());
        list.Add(host.continueGemButton.GetComponent<RectTransform>());
        controls.uiButtons = list.ToArray();
    }

    // ---------------------------------------------------------------- menu: preview stage (own camera → RenderTexture)
    static PreviewStage BuildPreviewStage(Mats m, out RenderTexture rt)
    {
        var stage = new GameObject("PreviewStage").AddComponent<PreviewStage>();
        stage.transform.position = new Vector3(0f, -400f, 0f);
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
        stage.soldierCamPos = new Vector3(0f, 1.25f, 4.2f);
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
        SetLayer(stage.gameObject, GameLayers.Preview);
        return stage;
    }

    // ---------------------------------------------------------------- menu panels
    static Button TabButton(string name, Transform p, string key, float x, float y, float w)
        => MakeButton(name, p, key, UiKhakiDark, new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(w, 80f), 34, null, 0f);

    static GameObject Dim(string name, Transform root, out Transform panel, Vector2 size)
    {
        var dim = Img(name, root, ui.flat, new Color(0f, 0f, 0f, 0.75f));
        Stretch(dim.rectTransform); dim.raycastTarget = true; // modal: blocks clicks behind
        var p = Panel("Panel", dim.transform, UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, 12f);
        panel = p.transform;
        return dim.gameObject;
    }

    static ShopUI BuildShop(Transform root, PreviewStage stage, RenderTexture rt)
    {
        var shop = new GameObject("ShopUI").AddComponent<ShopUI>();
        shop.transform.SetParent(root, false);
        shop.root = Dim("Shop", root, out var p, new Vector2(1820f, 1020f));
        var title = Label("Title", p, 56, TextAnchor.MiddleLeft, UiOrange, "shop.title");
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -14f), new Vector2(400f, 80f));
        string[] tabs = { "shop.tab.weapons", "shop.tab.costumes", "shop.tab.bonuses", "shop.tab.gems" };
        shop.tabButtons = tabs.Select((k, i) => TabButton("Tab" + i, p, k, -330f + i * 250f, -55f, 236f)).ToArray();
        shop.close = MakeButton("Close", p, "set.close", UiOrange, new Vector2(1f, 1f), new Vector2(-120f, -55f), new Vector2(200f, 80f), 34, UiInk, 0f);
        Text Pill(string n, Sprite icon, Color c, float x)
        {
            var i = Img(n + "Icon", p, icon, c);
            Anchor(i.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(x, -55f), new Vector2(50f, 50f));
            var t = Txt(n, p, 34, TextAnchor.MiddleLeft, UiCream);
            Anchor(t.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(x + 34f, -55f), new Vector2(150f, 50f));
            return t;
        }
        shop.gold = Pill("Gold", coinIcon, UiGold, -560f);
        shop.gems = Pill("Gems", gemIcon, new Color(0.45f, 0.85f, 1f), -370f);

        // Item grid (left).
        var grid = Rect("Grid", p);
        Anchor(grid, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -120f), new Vector2(820f, 880f));
        var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
        gl.cellSize = new Vector2(400f, 112f); gl.spacing = new Vector2(16f, 12f);
        var card = MakeButton("Card", grid, null, UiKhaki, new Vector2(0f, 1f), Vector2.zero, new Vector2(400f, 112f), 32, UiCream, 0f);
        var name = card.GetComponentInChildren<Text>();
        name.alignment = TextAnchor.UpperLeft; name.rectTransform.offsetMin = new Vector2(18f, 10f); name.rectTransform.offsetMax = new Vector2(-12f, -10f);
        var badge = Txt("Badge", card.transform, 28, TextAnchor.LowerRight, UiGold);
        Stretch(badge.rectTransform); badge.rectTransform.offsetMin = new Vector2(12f, 10f); badge.rectTransform.offsetMax = new Vector2(-16f, -10f);
        shop.cardTemplate = card;

        // Details (right): preview, texts, action, upgrades.
        var pv = new GameObject("Preview", typeof(RectTransform)).AddComponent<RawImage>();
        pv.transform.SetParent(p, false);
        pv.texture = rt;
        Anchor(pv.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -120f), new Vector2(420f, 420f));
        shop.previewImage = pv;
        shop.preview = stage;
        float dx = -930f;
        shop.title = Txt("ItemTitle", p, 46, TextAnchor.UpperLeft, UiCream);
        Anchor(shop.title.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(dx, -120f), new Vector2(460f, 60f));
        shop.description = Txt("Desc", p, 28, TextAnchor.UpperLeft, UiCream);
        Anchor(shop.description.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(dx, -190f), new Vector2(460f, 200f));
        shop.description.horizontalOverflow = HorizontalWrapMode.Wrap;
        shop.stats = Txt("Stats", p, 28, TextAnchor.UpperLeft, UiGold);
        Anchor(shop.stats.rectTransform, new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(dx, -400f), new Vector2(880f, 90f));
        shop.action = MakeButton("Action", p, null, UiOrange, new Vector2(1f, 1f), new Vector2(-250f, -600f), new Vector2(420f, 100f), 36, UiInk, 0f);
        shop.actionLabel = shop.action.GetComponentInChildren<Text>();
        var up = Rect("Upgrades", p);
        Anchor(up, new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(dx, -680f), new Vector2(880f, 300f));
        shop.upgradeRoot = up.gameObject;
        shop.upgradeLabels = new Text[3]; shop.upgradeButtons = new Button[3]; shop.upgradeCosts = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            float y = -40f - i * 92f;
            var l = Txt("Up" + i, up, 32, TextAnchor.MiddleLeft, UiCream);
            Anchor(l.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, y), new Vector2(440f, 60f));
            shop.upgradeLabels[i] = l;
            var b = MakeButton("UpBtn" + i, up, null, UiKhakiDark, new Vector2(0f, 1f), new Vector2(660f, y), new Vector2(360f, 76f), 30, UiGold, 0f);
            shop.upgradeButtons[i] = b;
            shop.upgradeCosts[i] = b.GetComponentInChildren<Text>();
        }
        shop.message = Txt("Message", p, 32, TextAnchor.MiddleCenter, UiGold);
        Anchor(shop.message.rectTransform, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-450f, 30f), new Vector2(880f, 50f));

        // Confirmation.
        var conf = Img("Confirm", shop.root.transform, ui.flat, new Color(0f, 0f, 0f, 0.6f));
        Stretch(conf.rectTransform); conf.raycastTarget = true;
        var cp = Panel("Panel", conf.transform, UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 420f), 12f);
        shop.confirmText = Txt("Text", cp.transform, 40, TextAnchor.MiddleCenter, UiCream);
        Anchor(shop.confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 200f));
        shop.confirmText.horizontalOverflow = HorizontalWrapMode.Wrap;
        shop.confirmYes = MakeButton("Yes", cp.transform, "shop.yes", UiOrange, new Vector2(0.5f, 0f), new Vector2(-220f, 85f), new Vector2(380f, 100f), 40, UiInk, 0f);
        shop.confirmNo = MakeButton("No", cp.transform, "set.no", UiKhaki, new Vector2(0.5f, 0f), new Vector2(220f, 85f), new Vector2(380f, 100f), 40, null, 0f);
        shop.confirm = conf.gameObject;
        return shop;
    }

    static LoadoutPanel BuildLoadout(Transform root, ShopUI shop)
    {
        var lp = new GameObject("Loadout").AddComponent<LoadoutPanel>();
        lp.transform.SetParent(root, false);
        lp.root = Dim("LoadoutPanel", root, out var p, new Vector2(1600f, 760f));
        var t = Label("Title", p, 56, TextAnchor.MiddleCenter, UiOrange, "loadout.title");
        Anchor(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1000f, 80f));
        int n = System.Enum.GetValues(typeof(WeaponId)).Length;
        lp.weaponButtons = new Button[n]; lp.weaponLabels = new Text[n]; lp.weaponStates = new Text[n];
        for (int i = 0; i < n; i++)
        {
            var b = MakeButton("W" + i, p, null, UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(-600f + i * 300f, 60f), new Vector2(280f, 260f), 36, UiCream, 0f);
            var lbl = b.GetComponentInChildren<Text>();
            lbl.rectTransform.offsetMin = new Vector2(8f, 90f);
            var st = Txt("State", b.transform, 28, TextAnchor.LowerCenter, UiGold);
            Stretch(st.rectTransform); st.rectTransform.offsetMin = new Vector2(8f, 24f);
            lp.weaponButtons[i] = b; lp.weaponLabels[i] = lbl; lp.weaponStates[i] = st;
        }
        lp.info = Txt("Info", p, 30, TextAnchor.MiddleCenter, UiToxic);
        Anchor(lp.info.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1200f, 50f));
        lp.start = MakeButton("Start", p, "loadout.start", UiOrange, new Vector2(0.5f, 0f), new Vector2(200f, 90f), new Vector2(420f, 120f), 56, UiInk, 0f);
        lp.back = MakeButton("Back", p, "menu.back", UiKhakiDark, new Vector2(0.5f, 0f), new Vector2(-260f, 90f), new Vector2(300f, 100f), 40, null, 0f);
        lp.shop = shop;
        return lp;
    }

    static DailyLoginPanel BuildLogin(Transform root)
    {
        var dp = new GameObject("DailyLogin").AddComponent<DailyLoginPanel>();
        dp.transform.SetParent(root, false);
        dp.root = Dim("LoginPanel", root, out var p, new Vector2(1640f, 660f));
        var t = Label("Title", p, 56, TextAnchor.MiddleCenter, UiOrange, "login.title");
        Anchor(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1000f, 80f));
        int n = Economy.LoginRewards.Length;
        dp.days = new Image[n]; dp.dayRewards = new Text[n]; dp.dayLabels = new Text[n];
        for (int i = 0; i < n; i++)
        {
            var d = Panel("Day" + i, p, UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-690f + i * 230f, 30f), new Vector2(210f, 260f), 0f);
            var dl = Txt("Label", d.transform, 30, TextAnchor.UpperCenter, UiCream);
            Stretch(dl.rectTransform); dl.rectTransform.offsetMax = new Vector2(0f, -14f);
            dl.text = Loc.F("login.day", i + 1);
            dp.dayLabels[i] = dl;
            var ic = Img("Icon", d.transform, i == n - 1 ? gemIcon : coinIcon, i == n - 1 ? new Color(0.45f, 0.85f, 1f) : UiGold);
            Anchor(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(70f, 70f));
            var r = Txt("Reward", d.transform, 26, TextAnchor.LowerCenter, UiGold);
            Stretch(r.rectTransform); r.rectTransform.offsetMin = new Vector2(4f, 14f);
            dp.days[i] = d; dp.dayRewards[i] = r;
        }
        dp.claim = MakeButton("Claim", p, "login.claim", UiOrange, new Vector2(0.5f, 0f), new Vector2(150f, 80f), new Vector2(520f, 110f), 44, UiInk, 0f);
        dp.close = MakeButton("Close", p, "set.close", UiKhakiDark, new Vector2(0.5f, 0f), new Vector2(-300f, 80f), new Vector2(260f, 100f), 40, null, 0f);
        return dp;
    }

    static QuestsPanel BuildQuests(Transform root)
    {
        var qp = new GameObject("Quests").AddComponent<QuestsPanel>();
        qp.transform.SetParent(root, false);
        qp.root = Dim("QuestsPanel", root, out var p, new Vector2(1400f, 720f));
        var t = Label("Title", p, 56, TextAnchor.MiddleCenter, UiOrange, "quest.title");
        Anchor(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1000f, 80f));
        int n = Economy.QuestsPerDay;
        qp.titles = new Text[n]; qp.progress = new Text[n]; qp.claims = new Button[n];
        for (int i = 0; i < n; i++)
        {
            var row = Panel("Row" + i, p, UiKhakiDark, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f - i * 150f), new Vector2(1280f, 130f), 0f);
            var tt = Txt("Title", row.transform, 38, TextAnchor.UpperLeft, UiCream);
            Stretch(tt.rectTransform); tt.rectTransform.offsetMin = new Vector2(28f, 0f); tt.rectTransform.offsetMax = new Vector2(-320f, -14f);
            var pr = Txt("Progress", row.transform, 30, TextAnchor.LowerLeft, UiGold);
            Stretch(pr.rectTransform); pr.rectTransform.offsetMin = new Vector2(28f, 14f); pr.rectTransform.offsetMax = new Vector2(-320f, 0f);
            var b = MakeButton("Claim", row.transform, "quest.claim", UiOrange, new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(260f, 90f), 38, UiInk, 0f);
            qp.titles[i] = tt; qp.progress[i] = pr; qp.claims[i] = b;
        }
        qp.close = MakeButton("Close", p, "set.close", UiKhakiDark, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(300f, 100f), 40, null, 0f);
        return qp;
    }

    /// <summary>Menu scene additions: shop, loadout, daily login, quests, preview; extra menu buttons with badges.</summary>
    static void BuildEconomyMenu(Transform root, MainMenu mm, Mats m)
    {
        var stage = BuildPreviewStage(m, out var rt);
        // Menu buttons: Shop (now open) + Quests + Daily next to Settings.
        var lockIcon = mm.shop.transform.Find("Lock");
        if (lockIcon) Object.DestroyImmediate(lockIcon.gameObject);
        var main = mm.mainButtons.transform;
        mm.shop.GetComponent<RectTransform>().anchoredPosition = new Vector2(-470f, -235f);
        mm.settings.GetComponent<RectTransform>().anchoredPosition = new Vector2(470f, -235f);
        mm.questsButton = MakeButton("Quests", main, "menu.quests", UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(-157f, -235f), new Vector2(290f, 110f), 46);
        mm.loginButton = MakeButton("Daily", main, "menu.daily", UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(157f, -235f), new Vector2(290f, 110f), 46);
        GameObject Badge(Button b)
        {
            var d = Img("Badge", b.transform, ui.dot, new Color(1f, 0.25f, 0.2f));
            Anchor(d.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-12f, -12f), new Vector2(36f, 36f));
            return d.gameObject;
        }
        mm.questBadge = Badge(mm.questsButton);
        mm.loginBadge = Badge(mm.loginButton);
        mm.shopUI = BuildShop(root, stage, rt);
        mm.loadout = BuildLoadout(root, mm.shopUI);
        mm.login = BuildLogin(root);
        mm.quests = BuildQuests(root);
        // Shop / panels above the settings panel and under the splash.
        var splash = root.Find("Splash");
        if (splash) splash.SetAsLastSibling();
    }
}
