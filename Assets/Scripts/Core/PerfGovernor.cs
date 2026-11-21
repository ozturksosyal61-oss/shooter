using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Keeps long sessions cool on phones (device builds only; the editor / tests are untouched). Every few seconds it
/// reads the iOS thermal state and the measured frame rate and sets <see cref="Settings.GovernorFpsCap"/> /
/// <see cref="Settings.GovernorScale"/>:
/// · paused → 30 fps (nothing moves; the GPU was still drawing at full rate) ·
/// · thermal serious → at most 60 fps, render scale ×0.85 · critical → 30 fps, ×0.72 ·
/// · frames clearly below target for 8 s → one render-scale step down (×0.9, then ×0.8); back up after 20 s of
///   steady frames on a cool device.
/// Changes are rare (≥ 6 s apart) because a new render scale re-allocates the camera targets. Pure step rules in
/// <see cref="ThermalScale"/> / <see cref="ThermalFpsCap"/> / <see cref="LoadScale"/> (EditMode-tested).
/// </summary>
public class PerfGovernor : MonoBehaviour
{
    public static PerfGovernor I { get; private set; }
    /// <summary>0 nominal · 1 fair · 2 serious · 3 critical (always 0 off iOS).</summary>
    public static int Thermal { get; private set; }
    public static int LoadStep { get; private set; }

    const float Window = 4f, MinChangeGap = 6f;
    float windowT, frames, changeT, thermalT;
    int slowWindows, goodWindows;
    bool wasIdle;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int _HoldoutThermalState();
    static int ReadThermal() { try { return _HoldoutThermalState(); } catch { return 0; } }
#else
    static int ReadThermal() => 0;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Application.isEditor || !Application.isMobilePlatform || I) return;
        var go = new GameObject("PerfGovernor");
        DontDestroyOnLoad(go);
        I = go.AddComponent<PerfGovernor>();
    }

    public static float ThermalScale(int thermal) => thermal >= 3 ? 0.72f : thermal >= 2 ? 0.85f : 1f;
    public static int ThermalFpsCap(int thermal) => thermal >= 3 ? 30 : thermal >= 2 ? 60 : 0;
    public static float LoadScale(int step) => step >= 2 ? 0.8f : step == 1 ? 0.9f : 1f;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        changeT += dt;
        var gm = GameManager.I;
        bool idle = gm && gm.Paused;
        if (idle != wasIdle) { wasIdle = idle; Apply(); }

        thermalT -= dt;
        if (thermalT <= 0f)
        {
            thermalT = 3f;
            int t = ReadThermal();
            if (t != Thermal) { Thermal = t; Apply(); }
        }

        if (idle) { windowT = frames = 0f; return; }
        windowT += dt;
        frames++;
        if (windowT < Window) return;
        float fps = frames / windowT;
        windowT = frames = 0f;
        int target = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60;
        if (fps < target * 0.85f) { slowWindows++; goodWindows = 0; }
        else if (fps >= target * 0.95f) { goodWindows++; slowWindows = 0; }
        else slowWindows = goodWindows = 0;

        if (changeT < MinChangeGap) return;
        if (slowWindows >= 2 && LoadStep < 2) { LoadStep++; slowWindows = 0; Apply(); }
        else if (goodWindows >= 5 && LoadStep > 0 && Thermal < 2) { LoadStep--; goodWindows = 0; Apply(); }
    }

    void Apply()
    {
        changeT = 0f;
        int cap = ThermalFpsCap(Thermal);
        if (wasIdle) cap = 30;
        Settings.GovernorFpsCap = cap;
        Settings.GovernorScale = Mathf.Min(ThermalScale(Thermal), LoadScale(LoadStep));
        Settings.ApplyPerformance();
    }

    void OnDestroy() { if (I == this) I = null; }
}
