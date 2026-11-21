using UnityEngine;

/// <summary>
/// 1.1.0 cosmetics (builder-made, <c>Resources/Cosmetics.asset</c>): gun finish materials (<see cref="GunSkinId"/> ×
/// <see cref="GunSkin.Roles"/>) and bullet tracer looks (<see cref="TracerId"/>). Looks only — no gameplay reads it.
/// </summary>
[CreateAssetMenu(menuName = "Holdout/Cosmetic Library")]
public class CosmeticLibrary : ScriptableObject
{
    [Tooltip("skin * GunSkin.Roles + role (dark, mid, light, accent); skin 0 = the model's own materials (empty).")]
    public Material[] skinMats;
    [Tooltip("By TracerId.")]
    public Material[] tracerMats;
    [Tooltip("Rainbow tracer: one material per hue, cycled shot by shot.")]
    public Material[] rainbowMats;
    [Tooltip("Tracer width multiplier by TracerId.")]
    public float[] tracerWidth;
    [Tooltip("Tracer flickers (fire) by TracerId.")]
    public bool[] tracerFlicker;
    [Tooltip("Shop swatch colour by TracerId / GunSkinId.")]
    public Color[] tracerColors, skinColors;

    static CosmeticLibrary cached;
    static bool loaded;

    public static CosmeticLibrary I
    {
        get
        {
            if (!loaded) { cached = Resources.Load<CosmeticLibrary>("Cosmetics"); loaded = true; }
            return cached;
        }
    }

    public Material SkinMat(GunSkinId skin, int role)
    {
        int i = (int)skin * GunSkin.Roles + role;
        return skinMats != null && i >= 0 && i < skinMats.Length ? skinMats[i] : null;
    }

    /// <summary>The tracer material for a shot (<paramref name="shot"/> cycles the rainbow).</summary>
    public Material TracerMat(TracerId t, int shot)
    {
        if (t == TracerId.Rainbow && rainbowMats != null && rainbowMats.Length > 0)
            return rainbowMats[(shot & int.MaxValue) % rainbowMats.Length];
        return tracerMats != null && (int)t < tracerMats.Length ? tracerMats[(int)t] : null;
    }

    public float Width(TracerId t) => tracerWidth != null && (int)t < tracerWidth.Length ? tracerWidth[(int)t] : 1f;
    public bool Flicker(TracerId t) => tracerFlicker != null && (int)t < tracerFlicker.Length && tracerFlicker[(int)t];
}
