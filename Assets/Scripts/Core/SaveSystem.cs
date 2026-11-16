using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Player settings (applied by <see cref="Settings"/>).</summary>
[Serializable]
public class SettingsData
{
    public float music = 0.8f;
    public float sfx = 1f;
    public bool vibration = true;
    public int fps = 60;
    /// <summary>0 low, 1 medium, 2 high.</summary>
    public int quality = 2;
    public float joystickSize = 1f;
    public float joystickAlpha = 1f;
    public string lang = "tr";
}

[Serializable]
public class RecordsData
{
    public int bestScore, bestWave, bestKills;
    public float bestTime;
}

[Serializable]
public class WalletData
{
    public int gold, gems;
}

/// <summary>Campaign progress (chapters later): stars per chapter.</summary>
[Serializable]
public class CampaignData
{
    public List<int> stars = new List<int>();
}

/// <summary>The single save file. Bump <see cref="SaveSystem.CurrentVersion"/> and add a step to
/// <see cref="SaveSystem.Migrate"/> whenever a field changes meaning.</summary>
[Serializable]
public class SaveData
{
    public int version;
    /// <summary>UTC ticks of the last write — cloud conflict resolution picks the newer one.</summary>
    public long savedAt;
    public bool tutorialDone;
    public SettingsData settings = new SettingsData();
    public RecordsData records = new RecordsData();
    public WalletData wallet = new WalletData();
    public CampaignData campaign = new CampaignData();
    public List<string> inventory = new List<string>();
}

/// <summary>Where the JSON lives. Local file now; a cloud store (Game Center / Play Games saved games) implements the same.</summary>
public interface ISaveStore
{
    bool TryRead(out string json, out string backupJson);
    void Write(string json);
    void Delete();
}

/// <summary>persistentDataPath/save.json, written atomically (temp file → replace) with the previous version kept as save.bak.</summary>
public class FileSaveStore : ISaveStore
{
    readonly string path, backup, temp;

    public FileSaveStore(string dir, string name = "save")
    {
        path = Path.Combine(dir, name + ".json");
        backup = Path.Combine(dir, name + ".bak");
        temp = Path.Combine(dir, name + ".tmp");
    }

    public bool TryRead(out string json, out string backupJson)
    {
        json = backupJson = null;
        try { if (File.Exists(path)) json = File.ReadAllText(path); } catch { }
        try { if (File.Exists(backup)) backupJson = File.ReadAllText(backup); } catch { }
        return json != null || backupJson != null;
    }

    public void Write(string json)
    {
        File.WriteAllText(temp, json);
        if (File.Exists(path)) File.Copy(path, backup, true);
        if (File.Exists(path)) File.Delete(path);
        File.Move(temp, path);
    }

    public void Delete()
    {
        foreach (var f in new[] { path, backup, temp }) try { if (File.Exists(f)) File.Delete(f); } catch { }
    }
}

/// <summary>In-memory store (tests).</summary>
public class MemorySaveStore : ISaveStore
{
    public string json, backupJson;
    public bool TryRead(out string j, out string b) { j = json; b = backupJson; return j != null || b != null; }
    public void Write(string j) { backupJson = json; json = j; }
    public void Delete() { json = backupJson = null; }
}

/// <summary>
/// One JSON save for settings, records, currency and (later) campaign / inventory. Corrupt main file → backup;
/// both bad → fresh data. The first run imports the old PlayerPrefs keys (version 0 → 1).
/// </summary>
public static class SaveSystem
{
    public const int CurrentVersion = 1;

    static SaveData data;
    static ISaveStore store;

    public static ISaveStore Store
    {
        get => store ??= new FileSaveStore(Application.persistentDataPath);
        set { store = value; data = null; }
    }

    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    /// <summary>Raised after a reset or load (UI refreshes).</summary>
    public static event Action Changed;

    public static void Load()
    {
        data = null;
        if (Store.TryRead(out var json, out var bak))
        {
            data = Deserialize(json) ?? Deserialize(bak);
            if (data == null) Debug.LogWarning("Save: main and backup unreadable, starting fresh");
        }
        if (data == null)
        {
            data = new SaveData();
            ImportLegacyPrefs(data);
            data.version = CurrentVersion;
            Write();
        }
        Changed?.Invoke();
    }

    public static void Write()
    {
        if (data == null) return;
        data.savedAt = DateTime.UtcNow.Ticks;
        try { Store.Write(Serialize(data)); }
        catch (Exception e) { Debug.LogWarning("Save: write failed: " + e.Message); }
    }

    /// <summary>Wipes progress (records, currency, campaign, inventory, tutorial) — settings are kept.</summary>
    public static void ResetProgress()
    {
        var keep = Data.settings;
        data = new SaveData { version = CurrentVersion, settings = keep };
        Write();
        Changed?.Invoke();
    }

    public static string Serialize(SaveData d) => JsonUtility.ToJson(d, true);

    /// <summary>Parses and migrates; null when the text is missing or corrupt.</summary>
    public static SaveData Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var d = JsonUtility.FromJson<SaveData>(json);
            if (d == null || d.version <= 0 || d.version > CurrentVersion + 100) return null;
            Migrate(d);
            return d;
        }
        catch { return null; }
    }

    /// <summary>Upgrades older data in place, one version step at a time.</summary>
    public static void Migrate(SaveData d)
    {
        d.settings ??= new SettingsData();
        d.records ??= new RecordsData();
        d.wallet ??= new WalletData();
        d.campaign ??= new CampaignData();
        d.inventory ??= new List<string>();
        // if (d.version < 2) { ... } — future steps go here.
        d.version = CurrentVersion;
    }

    static void ImportLegacyPrefs(SaveData d)
    {
        try
        {
            d.records.bestScore = PlayerPrefs.GetInt("best_v2", 0);
            d.records.bestWave = PlayerPrefs.GetInt("bestWave", 0);
            d.records.bestKills = PlayerPrefs.GetInt("bestKills", 0);
            d.records.bestTime = PlayerPrefs.GetFloat("bestTime", 0f);
            d.tutorialDone = PlayerPrefs.GetInt("tutorial_done", 0) == 1;
            d.settings.lang = PlayerPrefs.GetString("lang", "tr") == "en" ? "en" : "tr";
            d.settings.music = PlayerPrefs.GetFloat("vol_" + AudioPrefs.Music, d.settings.music);
            d.settings.sfx = PlayerPrefs.GetFloat("vol_" + AudioPrefs.Sfx, d.settings.sfx);
        }
        catch { }
    }
}
