using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;
#if UNITY_IOS && !UNITY_EDITOR
using Unity.Advertisement.IosSupport;
#endif

public enum AdPlacement { DoubleGold, Continue, FreeGems }

/// <summary>Unity LevelPlay keys (dashboard, app "Holdout"). Live: TestMode stays false in store builds (true only for a local test-suite build).</summary>
public static class AdConfig
{
    public const string IosAppKey = "29b3d33ed";
    public const string AndroidAppKey = "29b3e5395";
    public const string IosRewardedUnit = "zo4omkyj6tja4e71";
    public const string AndroidRewardedUnit = "08q0pyan2usynqfd";
    public const bool TestMode = false;
}

public interface IAdBackend
{
    bool Ready { get; }
    void Init();
    void Show(AdPlacement placement, Action<bool> done);
}

/// <summary>
/// Rewarded ads: 2x run gold, one continue per run, daily free gems. VIP gets the reward without the ad.
/// iOS asks for App Tracking Transparency first; if refused, ads are served non-personalised. Not child-directed (COPPA off).
/// </summary>
public static class Ads
{
    static IAdBackend backend;
    public static bool UseFake = Application.isEditor;

    public static IAdBackend Backend
    {
        get
        {
            if (backend == null)
            {
                backend = UseFake ? new FakeAdBackend() : new LevelPlayAdBackend();
                backend.Init();
            }
            return backend;
        }
        set => backend = value;
    }

    public static bool Available => Profile.Vip || Backend.Ready;

    /// <summary>Starts the SDK (iOS: ATT prompt first) and loads the first rewarded ad. Called from the main menu after the splash.</summary>
    public static void Warm() { var _ = Backend; }

    public static void ShowRewarded(AdPlacement p, Action<bool> done)
    {
        if (Profile.Vip) { done?.Invoke(true); return; }
        Backend.Show(p, ok => { if (ok) MetaEvents.RewardedAdWatched(p.ToString()); done?.Invoke(ok); });
    }
}

public class FakeAdBackend : IAdBackend
{
    public bool Ready => true;
    public bool skip;
    public void Init() { }

    public void Show(AdPlacement placement, Action<bool> done) => ServiceRunner.Run(Play(done));

    IEnumerator Play(Action<bool> done)
    {
        yield return new WaitForSecondsRealtime(0.5f);
        done?.Invoke(!skip);
    }
}

public class LevelPlayAdBackend : IAdBackend
{
    LevelPlayRewardedAd rewarded;
    Action<bool> pending;
    bool rewardedFlag, initDone;

    public bool Ready => initDone && rewarded != null && rewarded.IsAdReady();

    static string AppKey => Application.platform == RuntimePlatform.IPhonePlayer ? AdConfig.IosAppKey : AdConfig.AndroidAppKey;
    static string Unit => Application.platform == RuntimePlatform.IPhonePlayer ? AdConfig.IosRewardedUnit : AdConfig.AndroidRewardedUnit;

    public void Init()
    {
        if (AppKey.StartsWith("REPLACE")) { Debug.LogWarning("Ads: LevelPlay app key not set — rewarded ads disabled"); return; }
#if UNITY_IOS && !UNITY_EDITOR
        var status = ATTrackingStatusBinding.GetAuthorizationTrackingStatus();
        if (status == ATTrackingStatusBinding.AuthorizationTrackingStatus.NOT_DETERMINED)
        {
            ATTrackingStatusBinding.RequestAuthorizationTracking(s => StartSdk(s == (int)ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED));
            return;
        }
        StartSdk(status == ATTrackingStatusBinding.AuthorizationTrackingStatus.AUTHORIZED);
#else
        StartSdk(true);
#endif
    }

    void StartSdk(bool tracking)
    {
        MetaEvents.Init(tracking); // Meta app events right after the ATT answer (refused → no advertiser tracking)
        LevelPlay.SetMetaData("is_child_directed", "false");          // COPPA off
        if (!tracking) LevelPlay.SetMetaData("do_not_sell", "true");   // ATT refused → non-personalised
        if (AdConfig.TestMode) LevelPlay.SetMetaData("is_test_suite", "enable");
        LevelPlay.OnInitSuccess += _ =>
        {
            initDone = true;
            LevelPlay.SetPauseGame(true); // iOS: Unity (game audio, timers) pauses while a full-screen ad is up
            if (AdConfig.TestMode) LevelPlay.ValidateIntegration();
            rewarded = new LevelPlayRewardedAd(Unit);
            rewarded.OnAdRewarded += (info, reward) => { rewardedFlag = true; if (closed) Complete(); };
            rewarded.OnAdClosed += _ => { closed = true; ServiceRunner.Run(CompleteSoon()); };
            rewarded.OnAdDisplayFailed += (info, err) => { Debug.LogWarning("Ads: display failed " + err); rewardedFlag = false; closed = true; Complete(); };
            rewarded.OnAdLoaded += _ => loadRetry = 0;
            rewarded.OnAdLoadFailed += err => { Debug.LogWarning("Ads: load failed " + err); ServiceRunner.Run(RetryLoad()); };
            rewarded.LoadAd();
        };
        LevelPlay.OnInitFailed += e => Debug.LogWarning("Ads: init failed " + e);
        LevelPlay.Init(AppKey);
    }

    bool closed;
    int loadRetry;

    public void Show(AdPlacement placement, Action<bool> done)
    {
        if (!Ready) { done?.Invoke(false); rewarded?.LoadAd(); return; }
        pending = done;
        rewardedFlag = false;
        closed = false;
        // Default placement (no dashboard placements needed); our placement goes to the log for reporting.
        Debug.Log("Ads: rewarded for " + placement);
        rewarded.ShowAd();
    }

    // Some networks send the reward just after the close: give it a moment before deciding.
    IEnumerator CompleteSoon()
    {
        yield return new WaitForSecondsRealtime(0.6f);
        Complete();
    }

    IEnumerator RetryLoad()
    {
        loadRetry = Mathf.Min(loadRetry + 1, 6);
        yield return new WaitForSecondsRealtime(Mathf.Pow(2f, loadRetry)); // 2, 4, 8 … 64 s
        if (rewarded != null && !rewarded.IsAdReady()) rewarded.LoadAd();
    }

    void Complete()
    {
        if (pending == null) return;
        var cb = pending;
        pending = null;
        cb?.Invoke(rewardedFlag);
        rewarded?.LoadAd();
    }
}
