using UnityEngine;

/// <summary>Shop preview: a soldier (costume) or a weapon slowly turning in front of a camera that renders into a texture.</summary>
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
    public float degreesPerSecond = 35f;
    public Vector3 soldierCamPos, soldierLook, weaponCamPos, weaponLook;

    // -nographics (tests, bots): no GPU → a render-texture camera would only log errors.
    static bool CanRender => SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;

    void Awake() => ShowNone();

    public void ShowCostume(CostumeId c)
    {
        cam.enabled = CanRender;
        soldierPivot.gameObject.SetActive(true);
        weaponPivot.gameObject.SetActive(false);
        var model = costumeModels != null && (int)c < costumeModels.Length && costumeModels[(int)c] ? costumeModels[(int)c] : soldierModel;
        if (costumeModels != null) foreach (var m in costumeModels) if (m) m.SetActive(m == model);
        if (soldierModel) soldierModel.SetActive(model == soldierModel);
        if ((int)c < costumeMaterials.Length && costumeMaterials[(int)c]) soldierBody.sharedMaterial = costumeMaterials[(int)c];
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
        if (weaponPivot) weaponPivot.Rotate(0f, d * 1.3f, 0f, Space.World);
    }
}
