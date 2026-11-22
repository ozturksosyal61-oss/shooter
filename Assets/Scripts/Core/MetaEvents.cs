using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Meta (Facebook) app events for ad measurement (iOS only; no-op elsewhere). Started right after the App Tracking
/// Transparency answer (<see cref="LevelPlayAdBackend"/>): refused → no advertiser ID / tracking. Purchases are NOT
/// logged here — Meta's "Log in-app events automatically" logs them (no double counting). Events: app activation
/// (start + every return to the foreground), tutorial completed, run finished (as "level achieved" = wave),
/// rewarded ad watched. Keys: <see cref="AppId"/> / <see cref="ClientToken"/> (also in Info.plist via IosPostBuild).
/// </summary>
public static class MetaEvents
{
    public const string AppId = "1109686598492645";
    public const string ClientToken = "0342ef8738fe155e911c99af1d446239";

    public static bool Started { get; private set; }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void _HoldoutMetaInit(int trackingAllowed);
    [DllImport("__Internal")] static extern void _HoldoutMetaActivate();
    [DllImport("__Internal")] static extern void _HoldoutMetaLog(string name, string paramsJson);
#else
    static void _HoldoutMetaInit(int trackingAllowed) { }
    static void _HoldoutMetaActivate() { }
    static void _HoldoutMetaLog(string name, string paramsJson) { }
#endif

    /// <summary>After the ATT prompt (once per app session).</summary>
    public static void Init(bool trackingAllowed)
    {
        if (Started) return;
        Started = true;
        try { _HoldoutMetaInit(trackingAllowed ? 1 : 0); }
        catch (System.Exception e) { Debug.LogWarning("Meta: init " + e.Message); Started = false; return; }
        if (!hooked) { hooked = true; Application.focusChanged += f => { if (f && Started) Safe(() => _HoldoutMetaActivate()); }; }
    }

    static bool hooked;

    // Standard Meta event names (FBSDK AppEvents.Name) and parameter keys.
    public const string EvTutorial = "fb_mobile_tutorial_completion";
    public const string EvLevel = "fb_mobile_level_achieved";
    public const string EvAd = "rewarded_ad_watched";

    public static void TutorialCompleted() => Log(EvTutorial, "{\"fb_success\":1}");

    /// <summary>A finished run: the wave reached as the "level", plus the mode.</summary>
    public static void RunFinished(RunMode mode, int wave) => Log(EvLevel, RunParams(mode, wave));

    public static void RewardedAdWatched(string placement) => Log(EvAd, "{\"placement\":\"" + placement + "\"}");

    /// <summary>Pure (EditMode-tested): the run event's parameters.</summary>
    public static string RunParams(RunMode mode, int wave) => "{\"fb_level\":\"" + Mathf.Max(0, wave) + "\",\"mode\":\"" + mode + "\"}";

    static void Log(string name, string json)
    {
        if (!Started) return;
        Safe(() => _HoldoutMetaLog(name, json));
    }

    static void Safe(System.Action a)
    {
        try { a(); } catch (System.Exception e) { Debug.LogWarning("Meta: " + e.Message); }
    }
}
