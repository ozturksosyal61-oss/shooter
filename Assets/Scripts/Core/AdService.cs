using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;
#if UNITY_IOS && !UNITY_EDITOR
using Unity.Advertisement.IosSupport;
#endif

public enum AdPlacement { DoubleGold, Continue, FreeGems }

/// <summary>Unity LevelPlay keys (dashboard). Replace before release; test mode on until then.</summary>
public static class AdConfig
{
    public const string IosAppKey = "REPLACE_IOS_APP_KEY";
    public const string AndroidAppKey = "REPLACE_ANDROID_APP_KEY";
    public const string IosRewardedUnit = "REPLACE_IOS_REWARDED_AD_UNIT";
    public const string AndroidRewardedUnit = "REPLACE_ANDROID_REWARDED_AD_UNIT";
    public const bool TestMode = true;
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

    public static void ShowRewarded(AdPlacement p, Action<bool> done)
    {
        if (Profile.Vip) { done?.Invoke(true); return; }
        Backend.Show(p, done);
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
        LevelPlay.SetMetaData("is_child_directed", "false");          // COPPA off
        if (!tracking) LevelPlay.SetMetaData("do_not_sell", "true");   // ATT refused → non-personalised
        if (AdConfig.TestMode) LevelPlay.SetMetaData("is_test_suite", "enable");
        LevelPlay.OnInitSuccess += _ =>
        {
            initDone = true;
            rewarded = new LevelPlayRewardedAd(Unit);
            rewarded.OnAdRewarded += (info, reward) => rewardedFlag = true;
            rewarded.OnAdClosed += _ => Complete();
            rewarded.OnAdDisplayFailed += (info, err) => { rewardedFlag = false; Complete(); };
            rewarded.OnAdLoadFailed += err => Debug.LogWarning("Ads: load failed " + err);
            rewarded.LoadAd();
        };
        LevelPlay.OnInitFailed += e => Debug.LogWarning("Ads: init failed " + e);
        LevelPlay.Init(AppKey);
    }

    public void Show(AdPlacement placement, Action<bool> done)
    {
        if (!Ready) { done?.Invoke(false); return; }
        pending = done;
        rewardedFlag = false;
        rewarded.ShowAd(placement.ToString());
    }

    void Complete()
    {
        var cb = pending;
        pending = null;
        cb?.Invoke(rewardedFlag);
        rewarded?.LoadAd();
    }
}
