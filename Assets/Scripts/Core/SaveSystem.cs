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
    // Hard mode keeps its own records.
    public int hardScore, hardWave, hardKills;
    public float hardTime;
}

[Serializable]
public class WalletData
{
    public int gold, gems;
}

[Serializable]
public class WeaponLevels
{
    public string id;
    public int damage, magazine, reload;
}

[Serializable]
public class QuestState
{
    public int index;     // into Economy.QuestPool
    public int progress;
    public bool claimed;
}

/// <summary>Owned items, permanent upgrades, selections and daily state (F13).</summary>
[Serializable]
public class EconomyData
{
    public List<string> weapons = new List<string> { nameof(WeaponId.M4) };
    public List<WeaponLevels> levels = new List<WeaponLevels>();
    public string weapon = nameof(WeaponId.M4);
    public List<string> costumes = new List<string> { nameof(CostumeId.Standard) };
    public string costume = nameof(CostumeId.Standard);
    public int startHealth, goldBoost, startCards;
    public bool vip, starterPack;
    public int loginDay = -1, loginIndex = -1;
    public int questDay = -1;
    public List<QuestState> quests = new List<QuestState>();
    public int freeGemDay = -1;
    public long lifetimeGold;
    public List<string> receipts = new List<string>();
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
    public EconomyData economy = new EconomyData();
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
    public const int CurrentVersion = 2;
    // Tamper protection: the JSON is obfuscated (xor + base64) and signed (SHA-256 of a salt + payload).
    // Not unbreakable — just enough that editing the file by hand makes it invalid (→ backup / fresh).
    const string Salt = "h0ld0ut-0zt-5a1t-2026";
    const string Key = "ZombieHoldout#OZT";

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

    [Serializable]
    class Envelope { public int v; public string p; public string h; }

    public static string Serialize(SaveData d)
    {
        string json = JsonUtility.ToJson(d);
        return JsonUtility.ToJson(new Envelope { v = d.version, p = Obfuscate(json), h = Hash(json) });
    }

    static string Obfuscate(string json)
    {
        var b = System.Text.Encoding.UTF8.GetBytes(json);
        for (int i = 0; i < b.Length; i++) b[i] ^= (byte)Key[i % Key.Length];
        return Convert.ToBase64String(b);
    }

    static string Deobfuscate(string p)
    {
        var b = Convert.FromBase64String(p);
        for (int i = 0; i < b.Length; i++) b[i] ^= (byte)Key[i % Key.Length];
        return System.Text.Encoding.UTF8.GetString(b);
    }

    public static string Hash(string json)
    {
        using (var sha = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Salt + json))).Replace("-", "");
    }

    /// <summary>Parses (signed envelope, or a plain v1 JSON from before F13) and migrates; null when missing, corrupt or tampered.</summary>
    public static SaveData Deserialize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            string json = text;
            var env = JsonUtility.FromJson<Envelope>(text);
            if (env != null && !string.IsNullOrEmpty(env.p))
            {
                json = Deobfuscate(env.p);
                if (env.h != Hash(json)) { Debug.LogWarning("Save: signature mismatch"); return null; }
            }
            else if (text.Contains("\"economy\"")) return null; // v2+ data must be signed
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
        // v1 → v2: economy section (owned M4 + standard uniform).
        d.economy ??= new EconomyData();
        var e = d.economy;
        e.weapons ??= new List<string>(); e.levels ??= new List<WeaponLevels>(); e.costumes ??= new List<string>();
        e.quests ??= new List<QuestState>(); e.receipts ??= new List<string>();
        if (!e.weapons.Contains(nameof(WeaponId.M4))) e.weapons.Add(nameof(WeaponId.M4));
        if (!e.costumes.Contains(nameof(CostumeId.Standard))) e.costumes.Add(nameof(CostumeId.Standard));
        if (string.IsNullOrEmpty(e.weapon)) e.weapon = nameof(WeaponId.M4);
        if (string.IsNullOrEmpty(e.costume)) e.costume = nameof(CostumeId.Standard);
        // if (d.version < 3) { ... } — future steps go here.
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
