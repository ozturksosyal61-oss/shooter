using UnityEngine;

/// <summary>Tall obstacle that turns see-through while it hides the player from the camera.</summary>
public class Fadeable : MonoBehaviour
{
    public Renderer[] renderers;
    public Material fadeMaterial;

    Material[] originals;
    int requestFrame = -10;
    bool faded;

    void Awake()
    {
        originals = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originals[i] = renderers[i].sharedMaterial;
    }

    public void RequestFade() => requestFrame = Time.frameCount;

    void LateUpdate()
    {
        bool want = Time.frameCount - requestFrame <= 1;
        if (want == faded) return;
        faded = want;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = want ? fadeMaterial : originals[i];
            renderers[i].shadowCastingMode = want ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    void OnDisable()
    {
        if (!faded) return;
        faded = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].sharedMaterial = originals[i];
            renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
