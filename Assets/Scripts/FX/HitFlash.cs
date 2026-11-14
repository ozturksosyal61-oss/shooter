using UnityEngine;

/// <summary>Swaps renderers to a solid flash material for a short (real-time) duration.</summary>
public class HitFlash : MonoBehaviour
{
    public Renderer[] renderers;
    public Material flashMaterial;

    Material[] originals;
    float until;
    bool on;

    void Awake()
    {
        originals = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) originals[i] = renderers[i].sharedMaterial;
    }

    /// <summary>Changes the material restored after a flash (e.g. zombie variants).</summary>
    public void SetBaseMaterial(Material m)
    {
        if (m == null) return;
        for (int i = 0; i < originals.Length; i++) originals[i] = m;
        if (!on) for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = m;
    }

    public void Flash(float duration)
    {
        until = Time.unscaledTime + duration;
        if (on) return;
        on = true;
        for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = flashMaterial;
    }

    public void ResetFlash()
    {
        if (!on) return;
        on = false;
        for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = originals[i];
    }

    void LateUpdate()
    {
        if (on && Time.unscaledTime >= until) ResetFlash();
    }

    void OnDisable() => ResetFlash();
}
