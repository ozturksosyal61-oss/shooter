using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>Short haptic pulses. iOS: UIImpactFeedbackGenerator (Plugins/iOS/Haptics.mm). Android: VibrationEffect one-shot.</summary>
public static class Haptics
{
    public enum Kind { Light = 0, Medium = 1, Heavy = 2 }

    public static bool Enabled = true;
    static float lastTime = -1f;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void _OZT_Haptic(int style);
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject vibrator;
    static AndroidJavaClass effectClass;
    static bool initDone;
    // Never true; the reference makes Unity add the VIBRATE permission to the manifest.
    public static bool forcePermission;
#endif

    public static void Play(Kind kind)
    {
        if (!Enabled) return;
        float now = Time.unscaledTime;
        if (now - lastTime < 0.05f) return;
        lastTime = now;

#if UNITY_IOS && !UNITY_EDITOR
        _OZT_Haptic((int)kind);
#elif UNITY_ANDROID && !UNITY_EDITOR
        if (forcePermission) Handheld.Vibrate();
        long ms = kind == Kind.Light ? 12 : kind == Kind.Medium ? 25 : 45;
        int amp = kind == Kind.Light ? 90 : kind == Kind.Medium ? 170 : 255;
        AndroidVibrate(ms, amp);
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static void AndroidVibrate(long ms, int amplitude)
    {
        try
        {
            if (!initDone)
            {
                initDone = true;
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = up.GetStatic<AndroidJavaObject>("currentActivity"))
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            }
            if (vibrator == null) return;
            using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                vibrator.Call("vibrate", effect);
        }
        catch (System.Exception) { vibrator = null; }
    }
#endif
}
