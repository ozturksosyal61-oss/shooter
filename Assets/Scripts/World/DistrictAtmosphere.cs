using System;
using UnityEngine;

/// <summary>Light / ambient / fog tone of a district.</summary>
[Serializable]
public struct DistrictMood
{
    public Color sky, equator, ground;
    public Color light;
    public float lightIntensity;
    public Color fog;
    public float fogDensity;
}

/// <summary>
/// Cosmetic only: blends ambient light, the sun colour and exponential fog toward the mood of the district the
/// player is in (Cadde warm, Çarşı orange, Sanayi grey-blue, Liman greenish fog). Reads state, never changes it.
/// </summary>
public class DistrictAtmosphere : MonoBehaviour
{
    public Light sun;
    public Camera cam;
    [Tooltip("Indexed by District.")]
    public DistrictMood[] moods = new DistrictMood[4];
    public float blendSpeed = 0.8f;

    DistrictMood cur;
    bool init;

    public District Current { get; private set; }

    void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        Apply(true);
    }

    void Update() => Apply(false);

    void Apply(bool snap)
    {
        var cm = ChunkManager.I;
        var gm = GameManager.I;
        if (!cm || !gm || !gm.player || moods == null || moods.Length < 4) return;
        Current = cm.DistrictAtPosition(gm.player.transform.position);
        var target = moods[(int)Current];
        float k = snap || !init ? 1f : 1f - Mathf.Exp(-blendSpeed * Time.unscaledDeltaTime);
        init = true;
        cur.sky = Color.Lerp(cur.sky, target.sky, k);
        cur.equator = Color.Lerp(cur.equator, target.equator, k);
        cur.ground = Color.Lerp(cur.ground, target.ground, k);
        cur.light = Color.Lerp(cur.light, target.light, k);
        cur.lightIntensity = Mathf.Lerp(cur.lightIntensity, target.lightIntensity, k);
        cur.fog = Color.Lerp(cur.fog, target.fog, k);
        cur.fogDensity = Mathf.Lerp(cur.fogDensity, target.fogDensity, k);

        RenderSettings.ambientSkyColor = cur.sky;
        RenderSettings.ambientEquatorColor = cur.equator;
        RenderSettings.ambientGroundColor = cur.ground;
        RenderSettings.fogColor = cur.fog;
        RenderSettings.fogDensity = cur.fogDensity;
        if (sun) { sun.color = cur.light; sun.intensity = cur.lightIntensity; }
        if (cam) cam.backgroundColor = cur.fog;
    }
}
