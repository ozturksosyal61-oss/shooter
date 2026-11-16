#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>iOS: ProMotion (120 Hz on iPhone needs CADisableMinimumFrameDurationOnPhone) + home-screen name.</summary>
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
        plist.WriteToFile(plistPath);
    }
}
#endif
