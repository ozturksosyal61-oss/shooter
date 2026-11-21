#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// iOS (every build, local and Unity Cloud Build — runs as a PostProcessBuild after the LevelPlay one):
/// ProMotion (120 Hz needs CADisableMinimumFrameDurationOnPhone), home-screen name, ITSAppUsesNonExemptEncryption = false,
/// App Tracking Transparency text (NSUserTrackingUsageDescription, localized TR / EN through InfoPlist.strings) and the
/// SKAdNetworkItems LevelPlay recommends (ironSource + Unity Ads lists, baked in so the build never depends on a
/// download; merged with whatever LevelPlay already added). The ATT prompt itself is shown before LevelPlay.Init
/// (<c>LevelPlayAdBackend.Init</c>, Unity iOS Support).
/// </summary>
public static class IosPostBuild
{
    public const string TrackingEn = "Used to show you more relevant ads.";
    public const string TrackingTr = "Sana daha uygun reklamlar göstermek için kullanılır.";

    /// <summary>LevelPlay's SKAdNetwork IDs (skadnetworks.ironsource + skadnetworks.unityads plist XML, 2026-11-19).</summary>
    static readonly string[] SkAdNetworks =
    {
        "22mmun2rn5.skadnetwork", "238da6jt44.skadnetwork", "252b5q8x7y.skadnetwork", "294l99pt4k.skadnetwork", 
        "2fnua5tdw4.skadnetwork", "2u9pt9hc89.skadnetwork", "32z4fx6l9h.skadnetwork", "3qy4746246.skadnetwork", 
        "3rd42ekr43.skadnetwork", "3sh42y64q3.skadnetwork", "424m5254lk.skadnetwork", "4468km3ulz.skadnetwork", 
        "44jx6755aq.skadnetwork", "488r3q3dtq.skadnetwork", "4dzt52r2t5.skadnetwork", "4fzdc2evr5.skadnetwork", 
        "4pfyvq9l8r.skadnetwork", "4w7y6s5ca2.skadnetwork", "578prtvx9j.skadnetwork", "5a6flpkh64.skadnetwork", 
        "5f5u5tfb26.skadnetwork", "5l3tpt7t6e.skadnetwork", "5lm9lj6jb7.skadnetwork", "5tjdwbrq8w.skadnetwork", 
        "6yxyv74ff7.skadnetwork", "77y3x8wds4.skadnetwork", "7fmhfwg9en.skadnetwork", "7ug5zh24hu.skadnetwork", 
        "8s468mfl3y.skadnetwork", "97r2b46745.skadnetwork", "9nlqeag3gk.skadnetwork", "9rd848q2bz.skadnetwork", 
        "9t245vhmpl.skadnetwork", "a2p9lx4jpn.skadnetwork", "a8cz6cu7e5.skadnetwork", "av6w8kgt66.skadnetwork", 
        "c6k4g5qg8m.skadnetwork", "cstr6suwn9.skadnetwork", "cwn433xbcr.skadnetwork", "e5fvkxwrpn.skadnetwork", 
        "f38h382jlk.skadnetwork", "f73kdq92p3.skadnetwork", "f7s53z58qe.skadnetwork", "feyaarzu9v.skadnetwork", 
        "g6gcrrvk4p.skadnetwork", "glqzh8vgby.skadnetwork", "hs6bdukanm.skadnetwork", "k674qkevps.skadnetwork", 
        "k6y4y55b64.skadnetwork", "kbd757ywx3.skadnetwork", "kbmxgpxpgc.skadnetwork", "klf5c3l5u5.skadnetwork", 
        "lr83yxwka7.skadnetwork", "m8dbw4sv7c.skadnetwork", "mj797d8u6f.skadnetwork", "mlmmfzh3r3.skadnetwork", 
        "mp6xlyr22a.skadnetwork", "mqn7fxpca7.skadnetwork", "n9x2a789qt.skadnetwork", "p78axxw29g.skadnetwork", 
        "ppxm28t8ap.skadnetwork", "prcb7njmu6.skadnetwork", "pwa73g5rt2.skadnetwork", "qqp299437r.skadnetwork", 
        "s39g8k73mm.skadnetwork", "su67r6k2v3.skadnetwork", "t38b2kh725.skadnetwork", "tl55sbb4fm.skadnetwork", 
        "uw77j35x4d.skadnetwork", "v72qych5uu.skadnetwork", "v79kvwwj4g.skadnetwork", "v9wttpbfk9.skadnetwork", 
        "vhf287vqwu.skadnetwork", "w9q455wk68.skadnetwork", "wg4vff78zm.skadnetwork", "wzmmz9fp6w.skadnetwork", 
        "x44k69ngh6.skadnetwork", "xga6mpmplv.skadnetwork", "yclnxrl5pm.skadnetwork", "ydx93a7ass.skadnetwork", 
        "zmvfpc5aq8.skadnetwork", "zq492l623r.skadnetwork", 
    };

    /// <summary>Last uploaded TestFlight build was 2611210240 (CI yyMMddHHmm): never at or below it.</summary>
    public const long MinBuildNumber = 2611210241;
    const string Entitlements = "Unity-iPhone/Holdout.entitlements";

    [PostProcessBuild(1000)]
    public static void OnPostProcess(BuildTarget target, string path)
    {
        if (target != BuildTarget.iOS) return;
        string plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;
        root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
        root.SetString("CFBundleDisplayName", "Holdout");
        // GameKit as a *required* device capability is deprecated and blocks devices; Game Center is optional.
        if (root.values.TryGetValue("UIRequiredDeviceCapabilities", out var req) && req is PlistElementArray reqArr)
            reqArr.values.RemoveAll(e => e is PlistElementString es && es.value == "gamekit");
        // CI (Cloud Build / GitHub Actions) may inject its own build number: never below the last uploaded one (8).
        long.TryParse(root["CFBundleVersion"]?.AsString(), out long bn); // long: CI uses yyMMddHHmm (> int.MaxValue)
        if (bn < MinBuildNumber) root.SetString("CFBundleVersion", MinBuildNumber.ToString());
        // Only standard HTTPS / OS crypto: no export-compliance questions on every upload.
        root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
        // ATT: base text English; Turkish comes from tr.lproj/InfoPlist.strings.
        root.SetString("NSUserTrackingUsageDescription", TrackingEn);
        root.SetString("CFBundleDevelopmentRegion", "en");
        var locs = root.CreateArray("CFBundleLocalizations");
        locs.AddString("en");
        locs.AddString("tr");
        AddSkAdNetworks(root);
        plist.WriteToFile(plistPath);
        AddInfoPlistStrings(path);
        AddGameCenter(path);
    }

    /// <summary>Game Center capability (leaderboards + achievements, <c>GameServices</c>): entitlement + GameKit.</summary>
    static void AddGameCenter(string path)
    {
        string projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);
        string target = proj.GetUnityMainTargetGuid();
        var cap = new ProjectCapabilityManager(projPath, Entitlements, null, target);
        cap.AddGameCenter();
        cap.WriteToFile();
        // Make sure the key really is in the file (older Unity versions only add the pbx capability) and that every
        // configuration signs with it — CI re-signs the archive with this same file (ios-testflight.yml).
        string entPath = Path.Combine(path, Entitlements);
        var ent = new PlistDocument();
        if (File.Exists(entPath)) ent.ReadFromFile(entPath);
        ent.root.SetBoolean("com.apple.developer.game-center", true);
        ent.WriteToFile(entPath);
        // PlistDocument writes "<true />"; codesign's AMFI XML parser only accepts "<true/>" ("syntax error near line 6").
        File.WriteAllText(entPath, File.ReadAllText(entPath).Replace("<true />", "<true/>").Replace("<false />", "<false/>"),
            new System.Text.UTF8Encoding(false));
        proj = new PBXProject();
        proj.ReadFromFile(projPath);
        // Entitlements belong to the app target (Unity-iPhone) only; a framework target must not carry them.
        proj.SetBuildProperty(target, "CODE_SIGN_ENTITLEMENTS", Entitlements);
        string fw = proj.GetUnityFrameworkTargetGuid();
        if (!string.IsNullOrEmpty(fw) && fw != target && !string.IsNullOrEmpty(proj.GetBuildPropertyForAnyConfig(fw, "CODE_SIGN_ENTITLEMENTS")))
            proj.SetBuildProperty(fw, "CODE_SIGN_ENTITLEMENTS", "");
        proj.AddFrameworkToProject(target, "GameKit.framework", true);
        proj.WriteToFile(projPath);
        UnityEngine.Debug.Log("[IosPostBuild] Game Center: " + Entitlements + " → Unity-iPhone (main target)" +
            ", CODE_SIGN_ENTITLEMENTS=" + proj.GetBuildPropertyForAnyConfig(target, "CODE_SIGN_ENTITLEMENTS"));
    }

    static void AddSkAdNetworks(PlistElementDict root)
    {
        var have = new HashSet<string>();
        PlistElementArray items = root.values.TryGetValue("SKAdNetworkItems", out var v) ? v.AsArray() : root.CreateArray("SKAdNetworkItems");
        foreach (var e in items.values)
        {
            var d = e.AsDict();
            if (d != null && d.values.TryGetValue("SKAdNetworkIdentifier", out var id)) have.Add(id.AsString().ToLowerInvariant());
        }
        foreach (var id in SkAdNetworks)
        {
            if (!have.Add(id)) continue;
            items.AddDict().SetString("SKAdNetworkIdentifier", id);
        }
    }

    /// <summary>en.lproj / tr.lproj InfoPlist.strings with the ATT text, added to the app target's resources.</summary>
    static void AddInfoPlistStrings(string path)
    {
        string projPath = PBXProject.GetPBXProjectPath(path);
        var proj = new PBXProject();
        proj.ReadFromFile(projPath);
        string target = proj.GetUnityMainTargetGuid();
        foreach (var (lang, text) in new[] { ("en", TrackingEn), ("tr", TrackingTr) })
        {
            string abs = Path.Combine(path, lang + ".lproj", "InfoPlist.strings");
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            File.WriteAllText(abs, "/* App Tracking Transparency */\n\"NSUserTrackingUsageDescription\" = \"" + text.Replace("\"", "\\\"") + "\";\n", new System.Text.UTF8Encoding(false));
            // Folder reference: the .lproj folder is copied into the bundle as is (a plain file would be flattened).
            string folder = lang + ".lproj";
            // Path relative to the Xcode project (SOURCE_ROOT): CI generates on Linux (/github/workspace/...) and
            // archives on macOS, so an absolute path breaks CpResource. Replace any older (absolute) reference.
            if (!File.Exists(abs)) throw new IOException("[IosPostBuild] " + abs + " was not written");
            string old = proj.FindFileGuidByProjectPath(folder);
            if (!string.IsNullOrEmpty(old)) proj.RemoveFile(old);
            proj.AddFileToBuild(target, proj.AddFolderReference(folder, folder, PBXSourceTree.Source));
        }
        proj.WriteToFile(projPath);
    }
}
#endif
