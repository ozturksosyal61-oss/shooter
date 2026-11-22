using UnityEngine;

/// <summary>
/// Shop / menu preview: a soldier (costume) holding the selected weapon (same aim pose + hand IK as in the game,
/// <see cref="SoldierRig"/>), or a weapon alone, slowly turning in front of a camera that renders into a texture.
/// </summary>
public class PreviewStage : MonoBehaviour
{
    public Camera cam;
    public Transform soldierPivot;
    public SkinnedMeshRenderer soldierBody;
    public Transform weaponPivot;
    public Transform[] weapons;          // indexed by WeaponId
    public Material[] costumeMaterials;  // indexed by CostumeId
    [Tooltip("Whole-character costumes (indexed by CostumeId; null = the soldier with a costume texture).")]
    public GameObject[] costumeModels;
    public GameObject soldierModel;
    [System.Serializable]
    public class Held { public Transform root, rightGrip, leftGrip; }
    [Tooltip("Weapons the character holds (indexed by WeaponId), children of the soldier pivot.")]
    public Held[] heldWeapons;
    public float degreesPerSecond = 35f;
    [Header("1.1.0 hero poses (menu / shop)")]
    [Tooltip("By CostumeId: the weapon each character / skin shows off with.")]
    public WeaponId[] heroWeapon;
    [Tooltip("By CostumeId: index into poseStates.")]
    public int[] heroPose;
    public string[] poseStates;
    public bool[] poseTwoHanded, poseLoops;
    public float[] posePitch;
    /// <summary>Hero mode: the character stands in its pose at a fixed 3/4 angle instead of turning.</summary>
    bool heroMode;
    public const float HeroYaw = 24f;
    [Tooltip("1.1.0 tracer preview: quads streaming out of the shown gun's muzzle (children of the stage).")]
    public Renderer[] tracerQuads;
    TracerId shownTracer;
    bool tracerMode;
    float tracerT;
    Vector3 tracerFrom, pivotHome;
    bool pivotHomeSet;
    public Vector3 soldierCamPos, soldierLook, weaponCamPos, weaponLook;

    // -nographics (tests, bots): no GPU → a render-texture camera would only log errors.
    static bool CanRender => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

    void Awake() => ShowNone();

    GameObject activeModel;
    SoldierRig activeRig;

    public void ShowCostume(CostumeId c) => ShowCostume(c, Profile.SelectedWeapon);

    /// <summary>Menu / shop: the character with its own weapon, in its own pose (falls back to the aim pose).</summary>
    public void ShowHero(CostumeId c, float yaw = float.NaN)
    {
        int ci = (int)c;
        var w = heroWeapon != null && ci < heroWeapon.Length ? heroWeapon[ci] : Profile.SelectedWeapon;
        ShowCostume(c, w);
        heroMode = true;
        soldierPivot.rotation = Quaternion.Euler(0f, float.IsNaN(yaw) ? HeroYaw : yaw, 0f);
        int pi = heroPose != null && ci < heroPose.Length ? heroPose[ci] : -1;
        if (pi < 0 || poseStates == null || pi >= poseStates.Length || !activeRig || !activeModel) return;
        // One aim-pose IK pass first: the gun sits in the hands, then rides on the right hand into the pose.
        var anim = activeModel.GetComponent<Animator>();
        anim.Update(0f);
        for (int i = 0; i < 3; i++) { activeRig.Tick(0.5f, Vector3.zero, 0f, true, false); anim.Update(0.2f); }
        bool loops = poseLoops != null && pi < poseLoops.Length && poseLoops[pi];
        activeRig.StartPose(poseStates[pi], poseTwoHanded != null && pi < poseTwoHanded.Length && poseTwoHanded[pi], loops ? Random.value : 0.98f,
            posePitch != null && pi < posePitch.Length ? posePitch[pi] : 0f);
        anim.Update(0f);
    }

    /// <summary>Still of a character / skin in its hero pose (shop cards).</summary>
    public bool SnapshotHero(CostumeId c, RenderTexture rt, float yaw = 28f)
    {
        if (!CanRender || !cam || !rt) return false;
        var rot = soldierPivot.rotation;
        ShowHero(c, yaw);
        var anim = activeModel ? activeModel.GetComponent<Animator>() : null;
        if (anim) for (int i = 0; i < 3; i++) anim.Update(0.15f);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        soldierPivot.rotation = rot;
        return true;
    }

    void StopPoses()
    {
        if (soldierModel && soldierModel.TryGetComponent(out SoldierRig r0)) r0.StopPose();
        if (costumeModels != null) foreach (var m in costumeModels) if (m && m.TryGetComponent(out SoldierRig r)) r.StopPose();
    }

    public void ShowCostume(CostumeId c, WeaponId held)
    {
        StopPoses();
        heroMode = false;
        cam.enabled = CanRender;
        soldierPivot.gameObject.SetActive(true);
        weaponPivot.gameObject.SetActive(false);
        var model = costumeModels != null && (int)c < costumeModels.Length && costumeModels[(int)c] ? costumeModels[(int)c] : soldierModel;
        if (costumeModels != null) foreach (var m in costumeModels) if (m) m.SetActive(m == model);
        if (soldierModel) soldierModel.SetActive(model == soldierModel);
        if ((int)c < costumeMaterials.Length && costumeMaterials[(int)c]) soldierBody.sharedMaterial = costumeMaterials[(int)c];
        activeModel = model;
        activeRig = model ? model.GetComponent<SoldierRig>() : null;
        Held h = null;
        if (heldWeapons != null)
            for (int i = 0; i < heldWeapons.Length; i++)
            {
                var hw = heldWeapons[i];
                if (hw == null || !hw.root) continue;
                hw.root.gameObject.SetActive(i == (int)held);
                if (i == (int)held) h = hw;
            }
        if (activeRig && h != null) { activeRig.weaponRoot = h.root; activeRig.rightGrip = h.rightGrip; activeRig.leftGrip = h.leftGrip; }
        if (h != null) GunSkin.ApplyTo(h.root, Profile.SkinOf(held));
        SetTracers(false);
        cam.transform.localPosition = soldierCamPos;
        cam.transform.LookAt(transform.TransformPoint(soldierLook));
    }

    /// <summary>
    /// Renders one still of a character / costume (holding <paramref name="held"/>, 3/4 view) into <paramref name="rt"/>:
    /// shop thumbnails, so every card shows its character, not only the selected one. Leaves that model shown;
    /// the caller shows the selected one again afterwards. No-op without a GPU.
    /// </summary>
    public bool Snapshot(CostumeId c, WeaponId held, RenderTexture rt, float yaw = 28f)
    {
        if (!CanRender || !cam || !rt) return false;
        var rot = soldierPivot.rotation;
        soldierPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
        ShowCostume(c, held);
        var anim = activeModel ? activeModel.GetComponent<Animator>() : null;
        if (anim)
        {
            anim.Update(0f);
            // Settle the aim pose (upper-body layer + hand IK) before the still.
            for (int i = 0; i < 3; i++) { if (activeRig) activeRig.Tick(0.5f, Vector3.zero, 0f, true, false); anim.Update(0.2f); }
        }
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        soldierPivot.rotation = rot;
        return true;
    }

    public void ShowWeapon(WeaponId w) => ShowWeapon(w, Profile.SkinOf(w));

    /// <summary>The gun alone in finish <paramref name="skin"/> (shop: weapon detail + finish cards).</summary>
    public void ShowWeapon(WeaponId w, GunSkinId skin)
    {
        SetTracers(false);
        if (!pivotHomeSet) { pivotHome = weaponPivot.localPosition; pivotHomeSet = true; }
        weaponPivot.localPosition = pivotHome;
        if ((int)w < weapons.Length) GunSkin.ApplyTo(weapons[(int)w], skin);
        cam.enabled = CanRender;
        soldierPivot.gameObject.SetActive(false);
        weaponPivot.gameObject.SetActive(true);
        for (int i = 0; i < weapons.Length; i++) if (weapons[i]) weapons[i].gameObject.SetActive(i == (int)w);
        cam.transform.localPosition = weaponCamPos;
        cam.transform.LookAt(transform.TransformPoint(weaponLook));
    }

    /// <summary>Tracer card: the selected gun side-on, bullets of look <paramref name="t"/> streaming from its muzzle.</summary>
    public void ShowTracer(TracerId t, WeaponId w)
    {
        ShowWeapon(w, Profile.SkinOf(w));
        // Barrel to -X = screen right (the camera looks down -Z); bullets fly further right.
        weaponPivot.localRotation = Quaternion.Euler(0f, -90f, 0f);
        weaponPivot.localPosition = pivotHome + new Vector3(0.22f, 0f, 0f); // gun to the left of a narrow card, room for the bullets
        var mz = (int)w < weapons.Length && weapons[(int)w] ? weapons[(int)w].Find("Muzzle") : null;
        tracerFrom = mz ? transform.InverseTransformPoint(mz.position) : weaponLook;
        shownTracer = t;
        SetTracers(true);
        var lib = CosmeticLibrary.I;
        if (tracerQuads != null && lib)
            for (int i = 0; i < tracerQuads.Length; i++) if (tracerQuads[i]) tracerQuads[i].sharedMaterial = lib.TracerMat(t, i);
        tracerT = 0f;
    }

    void SetTracers(bool on)
    {
        tracerMode = on;
        if (tracerQuads != null) foreach (var q in tracerQuads) if (q) q.gameObject.SetActive(on);
    }

    /// <summary>One still of the gun in a finish (finish cards), like <see cref="Snapshot"/>.</summary>
    public bool SnapshotWeapon(WeaponId w, GunSkinId skin, RenderTexture rt)
    {
        if (!CanRender || !cam || !rt) return false;
        var rot = weaponPivot.localRotation;
        ShowWeapon(w, skin);
        weaponPivot.localRotation = Quaternion.Euler(0f, 62f, 0f);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        weaponPivot.localRotation = rot;
        return true;
    }

    /// <summary>One still of a tracer look (tracer cards).</summary>
    public bool SnapshotTracer(TracerId t, WeaponId w, RenderTexture rt)
    {
        if (!CanRender || !cam || !rt) return false;
        ShowTracer(t, w);
        tracerT = 0.25f;
        PlaceTracers();
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        return true;
    }

    void PlaceTracers()
    {
        if (tracerQuads == null) return;
        var lib = CosmeticLibrary.I;
        float wmul = lib ? lib.Width(shownTracer) : 1f;
        bool flick = lib && lib.Flicker(shownTracer);
        for (int i = 0; i < tracerQuads.Length; i++)
        {
            var q = tracerQuads[i];
            if (!q) continue;
            // Three bullets in flight, evenly spaced, looping 0 → 1.2 m past the muzzle (inside the card's frame).
            float d = Mathf.Repeat(tracerT * 2f + i * 0.25f, 0.75f);
            float len = Mathf.Min(d, 0.3f) + 0.05f;
            float w = 0.07f * wmul * (flick ? 0.75f + 0.5f * Mathf.PerlinNoise(i * 3.1f, tracerT * 30f) : 1f);
            q.transform.localPosition = tracerFrom + new Vector3(-(d + 0.03f - len * 0.5f), (i - 1) * 0.02f, 0f);
            q.transform.localScale = new Vector3(w, len, 1f);
        }
    }

    public void ShowNone()
    {
        SetTracers(false);
        if (cam) cam.enabled = false;
        if (soldierPivot) soldierPivot.gameObject.SetActive(false);
        if (weaponPivot) weaponPivot.gameObject.SetActive(false);
    }

    void Update()
    {
        float d = degreesPerSecond * Time.unscaledDeltaTime;
        if (soldierPivot && !heroMode) soldierPivot.Rotate(0f, d, 0f, Space.World);
        // Idle with the weapon up: aim layer on, hands on the grips (the rig reads the pivot's facing).
        if (activeRig && soldierPivot && soldierPivot.gameObject.activeInHierarchy) activeRig.Tick(Time.unscaledDeltaTime, Vector3.zero, 0f, true, false);
        if (tracerMode) { tracerT += Time.unscaledDeltaTime; PlaceTracers(); }
        else if (weaponPivot) weaponPivot.Rotate(0f, d * 1.3f, 0f, Space.World);
    }
}
