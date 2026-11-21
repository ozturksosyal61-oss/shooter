using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure merge of the local save with the cloud save (1.1.0 cloud sync, EditMode-tested). Nothing a player owns is
/// ever lost: owned weapons / characters / costumes / finishes / tracers, achievements, purchase receipts and campaign
/// stars are unions; upgrade levels, records and one-way flags take the maximum. Gold / gems and the day-to-day
/// state (selections, quests, login streak) come from the newer save — except on an install's first sync, where each
/// currency takes the higher of the two (a fresh install must not wipe the cloud wallet with its 0, and gold earned
/// offline before signing in isn't lost). Settings always stay local (they belong to the device).
/// </summary>
public static class SaveMerge
{
    public static SaveData Merge(SaveData local, SaveData cloud)
    {
        if (cloud == null) return local;
        if (local == null) return cloud;
        bool firstSync = !local.cloudSynced;
        bool cloudNewer = cloud.savedAt > local.savedAt;
        var newer = cloudNewer ? cloud : local;
        var older = cloudNewer ? local : cloud;
        // Start from a copy of the newer save (JSON round trip = deep copy).
        var m = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(newer));
        m.settings = local.settings;
        m.version = Mathf.Max(local.version, cloud.version);
        m.savedAt = System.Math.Max(local.savedAt, cloud.savedAt);
        m.tutorialDone = local.tutorialDone || cloud.tutorialDone;
        m.cloudSynced = true;

        if (firstSync)
        {
            m.wallet.gold = Mathf.Max(local.wallet.gold, cloud.wallet.gold);
            m.wallet.gems = Mathf.Max(local.wallet.gems, cloud.wallet.gems);
        }

        MergeRecords(m.records, older.records);
        Union(m.inventory, older.inventory);
        Union(m.achievements, older.achievements);
        var st = m.campaign.stars; var os = older.campaign.stars;
        for (int i = 0; i < os.Count; i++) { if (i < st.Count) st[i] = Mathf.Max(st[i], os[i]); else st.Add(os[i]); }

        var e = m.economy; var o = older.economy;
        Union(e.weapons, o.weapons);
        Union(e.costumes, o.costumes);
        Union(e.gunSkins, o.gunSkins);
        Union(e.tracers, o.tracers);
        Union(e.receipts, o.receipts);
        while (e.receipts.Count > 200) e.receipts.RemoveAt(0);
        foreach (var ol in o.levels)
        {
            if (ol == null) continue;
            var ml = e.levels.Find(x => x != null && x.id == ol.id);
            if (ml == null) { e.levels.Add(ol); continue; }
            ml.damage = Mathf.Max(ml.damage, ol.damage);
            ml.magazine = Mathf.Max(ml.magazine, ol.magazine);
            ml.reload = Mathf.Max(ml.reload, ol.reload);
            if (string.IsNullOrEmpty(ml.skin)) ml.skin = ol.skin;
        }
        e.startHealth = Mathf.Max(e.startHealth, o.startHealth);
        e.goldBoost = Mathf.Max(e.goldBoost, o.goldBoost);
        e.vip |= o.vip;
        e.starterPack |= o.starterPack;
        e.runsFinished = Mathf.Max(e.runsFinished, o.runsFinished);
        e.lifetimeGold = System.Math.Max(e.lifetimeGold, o.lifetimeGold);
        return m;
    }

    static void Union(List<string> into, List<string> from)
    {
        if (into == null || from == null) return;
        foreach (var x in from) if (!into.Contains(x)) into.Add(x);
    }

    static void MergeRecords(RecordsData r, RecordsData o)
    {
        r.bestScore = Mathf.Max(r.bestScore, o.bestScore);
        r.bestWave = Mathf.Max(r.bestWave, o.bestWave);
        r.bestKills = Mathf.Max(r.bestKills, o.bestKills);
        r.bestTime = Mathf.Max(r.bestTime, o.bestTime);
        r.hardScore = Mathf.Max(r.hardScore, o.hardScore);
        r.hardWave = Mathf.Max(r.hardWave, o.hardWave);
        r.hardKills = Mathf.Max(r.hardKills, o.hardKills);
        r.hardTime = Mathf.Max(r.hardTime, o.hardTime);
        if (o.weeklyWeek > r.weeklyWeek) { r.weeklyWeek = o.weeklyWeek; r.weeklyScore = o.weeklyScore; r.weeklyWave = o.weeklyWave; }
        else if (o.weeklyWeek == r.weeklyWeek) { r.weeklyScore = Mathf.Max(r.weeklyScore, o.weeklyScore); r.weeklyWave = Mathf.Max(r.weeklyWave, o.weeklyWave); }
    }
}
