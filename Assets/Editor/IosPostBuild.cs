#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>iOS (every build): ProMotion (120 Hz needs CADisableMinimumFrameDurationOnPhone), home-screen name,
/// ITSAppUsesNonExemptEncryption = false.</summary>
public static class IosPostBuild
{
    [PostProcessBuild]
    public static void OnPostProcess(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
        plist.root.SetString("CFBundleDisplayName", "Holdout");
        // Only standard HTTPS / OS crypto: no export-compliance questions on every upload.
        plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        // App Tracking Transparency prompt (rewarded ads; refused → non-personalised ads).
        plist.root.SetString("NSUserTrackingUsageDescription", "Reklamları ilgi alanlarına göre göstermek için kullanılır. Reddedersen reklamlar kişiselleştirilmez.");
        plist.WriteToFile(plistPath);
    }
}
#endif
