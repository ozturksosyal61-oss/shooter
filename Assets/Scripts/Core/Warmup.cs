using UnityEngine;

/// <summary>
/// Removes first-use hitches: on the first frame it renders every gameplay material once (on a quad and on a
/// skinned sample, so both vertex layouts get their shader variants / pipeline states) into a tiny offscreen
/// target, and pre-fills the dynamic UI font atlas with the glyphs and sizes the HUD uses.
/// </summary>
public class Warmup : MonoBehaviour
{
    public Material[] materials;
    [Tooltip("A skinned character (e.g. the rigged zombie model) used to warm skinned variants.")]
    public GameObject skinnedSample;
    public Material[] skinnedMaterials;
    public Font font;
    public int[] fontSizes = { 24, 26, 28, 30, 34, 36, 38, 40, 46, 48, 50, 54, 60, 64, 70, 72, 84, 110, 120 };

    const string Glyphs = "0123456789x/+-!:·.,()%•♥»<>*o ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZabcçdefgğhıijklmnoöprsştuüvyzFPS—→";

    void Start()
    {
        if (font)
            foreach (int size in fontSizes)
            {
                font.RequestCharactersInTexture(Glyphs, size, FontStyle.Bold);
                font.RequestCharactersInTexture(Glyphs, size, FontStyle.Normal);
            }

        var cam = Camera.main;
        if (!cam) { Destroy(gameObject); return; }
        var root = new GameObject("WarmupObjects").transform;
        root.SetPositionAndRotation(cam.transform.position + cam.transform.forward * 3f, cam.transform.rotation);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var quadMesh = quad.GetComponent<MeshFilter>().sharedMesh;
        Destroy(quad);
        if (materials != null)
            for (int i = 0; i < materials.Length; i++)
            {
                if (!materials[i]) continue;
                var go = new GameObject("w" + i, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3((i % 8) * 0.2f - 0.7f, (i / 8) * 0.2f - 0.3f, 0f);
                go.transform.localScale = Vector3.one * 0.15f;
                go.GetComponent<MeshFilter>().sharedMesh = quadMesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = materials[i];
            }
        if (skinnedSample && skinnedMaterials != null)
            for (int i = 0; i < skinnedMaterials.Length; i++)
            {
                var s = Instantiate(skinnedSample, root);
                s.transform.localPosition = new Vector3(i * 0.5f - 0.5f, -0.8f, 0.5f);
                s.transform.localScale = Vector3.one * 0.3f;
                foreach (var r in s.GetComponentsInChildren<Renderer>()) r.sharedMaterial = skinnedMaterials[i];
            }

        var rt = RenderTexture.GetTemporary(64, 64, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        RenderTexture.ReleaseTemporary(rt);
        Destroy(root.gameObject);
        Destroy(gameObject);
    }
}
