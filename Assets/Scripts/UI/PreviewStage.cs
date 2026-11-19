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
    public Vector3 soldierCamPos, soldierLook, weaponCamPos, weaponLook;

    // -nographics (tests, bots): no GPU → a render-texture camera would only log errors.
    static bool CanRender => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

    void Awake() => ShowNone();

    GameObject activeModel;
    SoldierRig activeRig;

    public void ShowCostume(CostumeId c) => ShowCostume(c, Profile.SelectedWeapon);

    public void ShowCostume(CostumeId c, WeaponId held)
    {
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
        cam.transform.localPosition = soldierCamPos;
        cam.transform.LookAt(transform.TransformPoint(soldierLook));
    }

    public void ShowWeapon(WeaponId w)
    {
        cam.enabled = CanRender;
        soldierPivot.gameObject.SetActive(false);
        weaponPivot.gameObject.SetActive(true);
        for (int i = 0; i < weapons.Length; i++) if (weapons[i]) weapons[i].gameObject.SetActive(i == (int)w);
        cam.transform.localPosition = weaponCamPos;
        cam.transform.LookAt(transform.TransformPoint(weaponLook));
    }

    public void ShowNone()
    {
        if (cam) cam.enabled = false;
        if (soldierPivot) soldierPivot.gameObject.SetActive(false);
        if (weaponPivot) weaponPivot.gameObject.SetActive(false);
    }

    void Update()
    {
        float d = degreesPerSecond * Time.unscaledDeltaTime;
        if (soldierPivot) soldierPivot.Rotate(0f, d, 0f, Space.World);
        // Idle with the weapon up: aim layer on, hands on the grips (the rig reads the pivot's facing).
        if (activeRig && soldierPivot && soldierPivot.gameObject.activeInHierarchy) activeRig.Tick(Time.unscaledDeltaTime, Vector3.zero, 0f, true, false);
        if (weaponPivot) weaponPivot.Rotate(0f, d * 1.3f, 0f, Space.World);
    }
}
