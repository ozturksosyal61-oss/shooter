using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// UI redesign fonts (OFL, Google Fonts): Bungee (logo, screen titles, big CTAs) and Barlow Condensed 800 / 600
/// (buttons, tabs, rows / descriptions). Imports TMP Essential Resources once, then builds dynamic SDF font assets
/// pre-filled with ASCII + the full Turkish set (no first-use atlas hitch). `-executeMethod UiFonts.Ensure`.
/// </summary>
public static class UiFonts
{
    const string Dir = "Assets/Art/UI/Fonts";
    const string TmpDir = Dir + "/TMP";
    public const string DisplayPath = TmpDir + "/Bungee SDF.asset";
    public const string BoldPath = TmpDir + "/Barlow800 SDF.asset";
    public const string SemiPath = TmpDir + "/Barlow600 SDF.asset";
    public const string DisplayShadowPath = TmpDir + "/Bungee Shadow.mat";

    public static TMP_FontAsset Display => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DisplayPath);
    public static TMP_FontAsset Bold => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPath);
    public static TMP_FontAsset Semi => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SemiPath);
    public static Material DisplayShadow => AssetDatabase.LoadAssetAtPath<Material>(DisplayShadowPath);
    /// <summary>1.1.0 B look: Bungee with a thick ink outline + hard ink drop (big titles, logo, CTA labels).</summary>
    public const string DisplayInkPath = TmpDir + "/Bungee Ink.mat";
    public static Material DisplayInk => AssetDatabase.LoadAssetAtPath<Material>(DisplayInkPath);

    static string Charset()
    {
        var sb = new System.Text.StringBuilder();
        for (int c = 32; c < 127; c++) sb.Append((char)c);
        sb.Append("ÇĞİÖŞÜçğıöşüÂâÎîÛû·•—–…×€₺«»’‘“”°");
        return sb.ToString();
    }

    [MenuItem("Tools/Zombie Shooter/UI Fonts")]
    public static void Ensure()
    {
        ImportEssentials();
        Directory.CreateDirectory(TmpDir);
        Make(Dir + "/Bungee-Regular.ttf", DisplayPath, 72);
        Make(Dir + "/BarlowCondensed-ExtraBold.ttf", BoldPath, 64);
        Make(Dir + "/BarlowCondensed-SemiBold.ttf", SemiPath, 56);
        // Logo / big titles: soft dark drop shadow (underlay).
        if (!File.Exists(DisplayShadowPath))
        {
            var m = new Material(Display.material);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.55f));
            m.SetFloat("_UnderlayOffsetX", 0.35f);
            m.SetFloat("_UnderlayOffsetY", -0.55f);
            m.SetFloat("_UnderlaySoftness", 0.25f);
            AssetDatabase.CreateAsset(m, DisplayShadowPath);
        }
        // B look (rebuilt every run so it can be tuned): ink outline + hard offset underlay.
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(DisplayInkPath);
            bool fresh = m == null;
            if (fresh) m = new Material(Display.material);
            m.EnableKeyword("OUTLINE_ON");
            m.SetFloat("_OutlineWidth", 0.22f);
            m.SetColor("_OutlineColor", new Color(0.055f, 0.047f, 0.07f, 1f));
            m.SetFloat("_FaceDilate", 0.18f);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0.055f, 0.047f, 0.07f, 1f));
            m.SetFloat("_UnderlayOffsetX", 0.75f);
            m.SetFloat("_UnderlayOffsetY", -0.85f);
            m.SetFloat("_UnderlayDilate", 0.4f);
            m.SetFloat("_UnderlaySoftness", 0f);
            if (fresh) AssetDatabase.CreateAsset(m, DisplayInkPath); else EditorUtility.SetDirty(m);
        }
        var settings = TMP_Settings.instance;
        if (settings != null)
        {
            var so = new SerializedObject(settings);
            var p = so.FindProperty("m_defaultFontAsset");
            if (p != null) { p.objectReferenceValue = Bold; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"UIFONTS: display={Display != null} bold={Bold != null} semi={Semi != null} settings={settings != null}");
    }

    static void ImportEssentials()
    {
        if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return;
        foreach (var guid in AssetDatabase.FindAssets("TMP Essential Resources"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".unitypackage")) continue;
            AssetDatabase.ImportPackage(path, false);
            AssetDatabase.Refresh();
            Debug.Log("UIFONTS: imported " + path);
            return;
        }
        string pkg = Path.GetFullPath("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage");
        if (File.Exists(pkg)) { AssetDatabase.ImportPackage(pkg, false); AssetDatabase.Refresh(); Debug.Log("UIFONTS: imported " + pkg); }
        else Debug.LogError("UIFONTS: TMP Essential Resources not found");
    }

    static void Make(string ttf, string path, int sampling)
    {
        if (File.Exists(path)) return;
        AssetDatabase.ImportAsset(ttf, ImportAssetOptions.ForceSynchronousImport);
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (!font) { Debug.LogError("UIFONTS: missing " + ttf); return; }
        var fa = TMP_FontAsset.CreateFontAsset(font, sampling, 7, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
        fa.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(fa, path);
        fa.atlasTextures[0].name = fa.name + " Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        fa.TryAddCharacters(Charset(), out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"UIFONTS: {fa.name} missing '{missing}'");
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
    }
}
