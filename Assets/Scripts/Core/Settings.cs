using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies <see cref="SettingsData"/> (save file) to the engine: language, vibration, frame rate (only what the
/// display supports — 90/120 on ProMotion / high-refresh Android), graphics quality (render scale, shadow distance,
/// particle density) and audio. Pure helpers are static and unit-tested.
/// </summary>
public static class Settings
{
    public static readonly int[] FpsOptions = { 30, 60, 90, 120 };
    public static readonly float[] RenderScales = { 0.7f, 0.85f, 1f };
    public static readonly float[] ShadowMul = { 0f, 0.6f, 1f };
    public static readonly float[] ParticleScales = { 0.45f, 0.75f, 1f };
    public const float MinJoystickSize = 0.75f, MaxJoystickSize = 1.35f;
    public const float MinJoystickAlpha = 0.3f, MaxJoystickAlpha = 1f;

    public static SettingsData S => SaveSystem.Data.settings;
    public static int Quality => Mathf.Clamp(S.quality, 0, 2);
    public static float ParticleScale => ParticleScales[Quality];

    public static event Action Changed;

    static float baseShadowDistance = -1f, baseRenderScale = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot() => Apply();

    /// <summary>Frame-rate choices for a display of <paramref name="maxHz"/> (30 always; 60 at least).</summary>
    public static List<int> SupportedFps(int maxHz)
    {
        var l = new List<int>(4);
        foreach (int f in FpsOptions) if (f <= 60 || f <= maxHz + 1) l.Add(f);
        return l;
    }

    /// <summary>The wanted rate if supported, otherwise the highest supported one below it.</summary>
    public static int ClampFps(int wanted, int maxHz)
    {
        int best = 30;
        foreach (int f in SupportedFps(maxHz)) if (f <= wanted) best = f;
        return best;
    }

    public static int DisplayHz()
    {
        double hz = Screen.currentResolution.refreshRateRatio.value;
        return hz < 1 ? 60 : (int)Math.Round(hz);
    }

    public static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = ClampFps(S.fps, DisplayHz());
    }

    public static void Apply()
    {
        var s = S;
        Loc.Current = s.lang == "en" ? Lang.En : Lang.Tr;
        Haptics.Enabled = s.vibration;
        ApplyFrameRate();
        ApplyQuality();
        if (GameAudio.I) AudioPrefs.Apply(GameAudio.I.mixer);
        Changed?.Invoke();
    }

    static void ApplyQuality()
    {
        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
        {
            // Relative to the asset's own values (in the editor the asset is not left changed at "high").
            if (baseShadowDistance < 0f) { baseShadowDistance = urp.shadowDistance; baseRenderScale = urp.renderScale; }
            urp.renderScale = baseRenderScale * RenderScales[Quality];
            urp.shadowDistance = baseShadowDistance * ShadowMul[Quality];
        }
    }

    /// <summary>Changes a setting, applies everything and saves.</summary>
    public static void Set(Action<SettingsData> change)
    {
        change(S);
        Apply();
        SaveSystem.Write();
    }
}
