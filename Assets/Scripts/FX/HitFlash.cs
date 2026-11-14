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
