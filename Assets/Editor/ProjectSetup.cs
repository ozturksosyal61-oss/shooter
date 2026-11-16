using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS;
using UnityEngine;

/// <summary>
/// Player Settings that are not scene content: identity, version, orientation, minimum OS, app icons
/// (default, every iOS size + App Store marketing, Android adaptive + round + legacy) and the iOS build.
/// Called by <see cref="GameSceneBuilder.Build"/>; <see cref="BuildIos"/> is the batch entry point.
/// </summary>
public static class ProjectSetup
{
    public const string ProductName = "Holdout";
    public const string BundleId = "com.oztdigital.holdout";
    public const string Version = "0.1.0";
    public const string IosBuildNumber = "2";
    public const string IconDir = "Assets/Art/Icon";

    public static void Apply()
    {
        PlayerSettings.productName = ProductName;
        PlayerSettings.companyName = "OZT Digital";
        foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
            PlayerSettings.SetApplicationIdentifier(t, BundleId);
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.iOS.buildNumber = IosBuildNumber;
        PlayerSettings.Android.bundleVersionCode = 2;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.backgroundColor = new Color(0.07f, 0.08f, 0.05f);
        // Landscape only (left + right).
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        ApplyIcons();
    }

    static Texture2D IconTex(string file, bool alpha)
    {
        string path = $"{IconDir}/{file}.png";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) { Debug.LogWarning("Icon missing: " + path); return null; }
        imp.textureType = TextureImporterType.Default;
        imp.mipmapEnabled = false;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.alphaSource = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
        imp.alphaIsTransparency = alpha;
        imp.maxTextureSize = 1024;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void ApplyIcons()
    {
        var icon = IconTex("AppIcon", false);
        var fg = IconTex("AppIcon_Foreground", true);
        var bg = IconTex("AppIcon_Background", false);
        if (!icon) return;
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        // iOS: every kind and size (application, spotlight, settings, notification, App Store marketing 1024).
        int ios = 0;
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
            foreach (var ic in icons) { ic.SetTexture(icon); ios++; }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
        }

        // Android: adaptive (background + foreground layers).
        int android = 0;
        // Unity 6.6 only has Adaptive (Round / Legacy are obsolete); older launchers use the default icon above.
        foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            foreach (var ic in icons)
            {
                if (ic.maxLayerCount >= 2 && bg && fg) ic.SetTextures(bg, fg);
                else ic.SetTexture(icon);
                android++;
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
        }
        Debug.Log($"SETUP: icons ios={ios} android={android} kinds ios=[{string.Join(",", PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS).Select(k => k.ToString()))}] android=[{string.Join(",", PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android).Select(k => k.ToString()))}]");
    }

    /// <summary>Batch: switch to iOS and export the Xcode project to Builds/iOS.</summary>
    public static void BuildIos()
    {
        Apply();
        AssetDatabase.SaveAssets();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes, locationPathName = "Builds/iOS", target = BuildTarget.iOS, options = BuildOptions.None,
        });
        var s = report.summary;
        Debug.Log($"BUILD iOS: {s.result} errors={s.totalErrors} warnings={s.totalWarnings} time={s.totalTime.TotalSeconds:0}s scenes={string.Join(",", scenes)}");
        if (s.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
