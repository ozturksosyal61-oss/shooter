using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponId { M4, Smg, Shotgun, Hmg, Sniper }
public enum WeaponStat { Damage, Magazine, Reload }
/// <summary>Standard..Commando: soldier textures. SpecOps / Firefighter (F15): whole other characters (own model, same animations).</summary>
public enum CostumeId { Standard, Woodland, Desert, Night, Arctic, Gold, Commando, SpecOps, Firefighter }
public enum BonusId { StartHealth, StartCard, GoldBoost }
public enum QuestKind { Kills, Specials, Bosses, ReachWave, Objectives, Runs, Combo }

/// <summary>Base stats of a weapon (before permanent upgrades and in-run cards).</summary>
[Serializable]
public struct WeaponDef
{
    public WeaponId id;
    public int price;              // gold; 0 = starter
    public float damage, fireRate, reloadTime, spread, bulletSpeed, range, crit;
    public int magazine, pellets, pierce;
    public float pelletSpread, kick, shake, moveMul;
    public SfxId sfx;
    public string Key => "weapon." + id;
}

[Serializable]
public struct CostumeDef
{
    public CostumeId id;
    public int gold, gems;
    /// <summary>Only from the starter pack (IAP).</summary>
    public bool packOnly;
    public string Key => "costume." + id;
}

[Serializable]
public struct BonusDef
{
    public BonusId id;
    public int maxLevel;
    public int[] costs;           // gold per level (StartCard: price per 1-run charge)
    public bool consumable;
    public string Key => "bonus." + id;
}

public struct QuestDef
{
    public QuestKind kind;
    public int target, reward;
    public string Key => "quest." + kind;
}

/// <summary>Store products (App Store / Google Play). IDs: com.oztdigital.holdout.*</summary>
public enum ProductKind { Consumable, NonConsumable }

public struct ProductDef
{
    public string id;
    public ProductKind kind;
    public int gems, gold;
    public CostumeId costume;
    public bool vip;
    /// <summary>Catalogue price in TRY (shown until the store returns its localized price; set the same tier in App Store Connect / Play Console).</summary>
    public string tl;
    public string Key => "iap." + id.Substring(id.LastIndexOf('.') + 1);
}

/// <summary>
/// Pure economy rules (EditMode-tested): prices, permanent upgrades, run rewards, daily login, daily quests,
/// store catalogue. No power is sold for real money: gems buy cosmetics, continues and gold only; permanent
/// weapon upgrades stay small (+8 % damage / +12 % magazine / −7 % reload per level, 5 levels).
/// Balanced in docs/economy.md.
/// </summary>
public static class Economy
{
    public const int MaxUpgrade = 5;
    public const int ContinueGems = 20;
    public const int FreeGemAd = 5;
    public const float VipGoldBonus = 0.25f;
    public const float GoldBoostPerLevel = 0.10f;
    public const int StartHealthPerLevel = 10;
    /// <summary>Whole new characters (F15): bought with gems; 250 gems = the 149,99 TL pack.</summary>
    public const int CharacterGems = 250;

    /// <summary>SpecOps / Firefighter: whole other characters (own model), not soldier skins.</summary>
    public static bool IsCharacter(CostumeId c) => c == CostumeId.SpecOps || c == CostumeId.Firefighter;

    /// <summary>Catalogue price label, e.g. "99,99 TL".</summary>
    public static string PriceLabel(ProductDef p) => p.tl + " TL";

    public static readonly WeaponDef[] Weapons =
    {
        new WeaponDef { id = WeaponId.M4, price = 0, damage = 25f, fireRate = 10f, magazine = 40, reloadTime = 1.5f, spread = 3f, bulletSpeed = 55f, range = 0.6f, crit = 0.12f, pellets = 1, kick = 0.12f, shake = 0.02f, moveMul = 1f, sfx = SfxId.Shoot },
        new WeaponDef { id = WeaponId.Smg, price = 900, damage = 15f, fireRate = 17f, magazine = 50, reloadTime = 1.2f, spread = 6f, bulletSpeed = 50f, range = 0.45f, crit = 0.1f, pellets = 1, kick = 0.07f, shake = 0.012f, moveMul = 1.06f, sfx = SfxId.ShootSmg },
        new WeaponDef { id = WeaponId.Shotgun, price = 1200, damage = 17f, fireRate = 1.6f, magazine = 8, reloadTime = 1.9f, spread = 2f, bulletSpeed = 48f, range = 0.22f, crit = 0.08f, pellets = 7, pelletSpread = 6f, kick = 0.45f, shake = 0.14f, moveMul = 1f, sfx = SfxId.Shotgun },
        new WeaponDef { id = WeaponId.Hmg, price = 1500, damage = 22f, fireRate = 12f, magazine = 120, reloadTime = 3.2f, spread = 5f, bulletSpeed = 55f, range = 0.65f, crit = 0.1f, pellets = 1, kick = 0.16f, shake = 0.035f, moveMul = 0.9f, sfx = SfxId.ShootHmg },
        new WeaponDef { id = WeaponId.Sniper, price = 1800, damage = 120f, fireRate = 1.4f, magazine = 6, reloadTime = 2.2f, spread = 0.5f, bulletSpeed = 90f, range = 0.9f, crit = 0.25f, pellets = 1, pierce = 4, kick = 0.6f, shake = 0.12f, moveMul = 0.97f, sfx = SfxId.ShootSniper },
    };

    public static readonly CostumeDef[] Costumes =
    {
        new CostumeDef { id = CostumeId.Standard },
        new CostumeDef { id = CostumeId.Woodland, gold = 600 },
        new CostumeDef { id = CostumeId.Desert, gold = 800 },
        new CostumeDef { id = CostumeId.Night, gold = 1200 },
        new CostumeDef { id = CostumeId.Arctic, gold = 1500 },
        new CostumeDef { id = CostumeId.Gold, gems = 200 },
        new CostumeDef { id = CostumeId.Commando, packOnly = true },
        new CostumeDef { id = CostumeId.SpecOps, gems = CharacterGems },
        new CostumeDef { id = CostumeId.Firefighter, gems = CharacterGems },
    };

    public static readonly BonusDef[] Bonuses =
    {
        new BonusDef { id = BonusId.StartHealth, maxLevel = 3, costs = new[] { 800, 1600, 3200 } },
        new BonusDef { id = BonusId.StartCard, maxLevel = 99, costs = new[] { 250 }, consumable = true },
        new BonusDef { id = BonusId.GoldBoost, maxLevel = 1, costs = new[] { 4000 } },
    };

    public const string ProductPrefix = "com.oztdigital.holdout.";
    public static readonly ProductDef[] Products =
    {
        new ProductDef { id = ProductPrefix + "gems_small", kind = ProductKind.Consumable, gems = 70, tl = "49,99" },
        new ProductDef { id = ProductPrefix + "gems_medium", kind = ProductKind.Consumable, gems = 150, tl = "99,99" },
        new ProductDef { id = ProductPrefix + "gems_large", kind = ProductKind.Consumable, gems = 250, tl = "149,99" },
        new ProductDef { id = ProductPrefix + "gems_huge", kind = ProductKind.Consumable, gems = 550, tl = "299,99" },
        new ProductDef { id = ProductPrefix + "starter_pack", kind = ProductKind.NonConsumable, gems = 150, gold = 2000, costume = CostumeId.Commando, tl = "79,99" },
        new ProductDef { id = ProductPrefix + "vip", kind = ProductKind.NonConsumable, vip = true, tl = "199,99" },
    };

    /// <summary>Gems → gold exchange offers in the gem tab.</summary>
    public static readonly (int gems, int gold)[] GoldOffers = { (40, 1000), (100, 3000) };

    /// <summary>Daily login, 7-day cycle (gold, gems).</summary>
    public static readonly (int gold, int gems)[] LoginRewards =
    {
        (100, 0), (150, 0), (200, 0), (250, 2), (300, 0), (400, 0), (500, 10),
    };

    public static readonly QuestDef[] QuestPool =
    {
        new QuestDef { kind = QuestKind.Kills, target = 200, reward = 150 },
        new QuestDef { kind = QuestKind.Specials, target = 25, reward = 150 },
        new QuestDef { kind = QuestKind.Bosses, target = 1, reward = 250 },
        new QuestDef { kind = QuestKind.ReachWave, target = 8, reward = 200 },
        new QuestDef { kind = QuestKind.Objectives, target = 3, reward = 150 },
        new QuestDef { kind = QuestKind.Runs, target = 3, reward = 100 },
        new QuestDef { kind = QuestKind.Combo, target = 50, reward = 150 },
    };
    public const int QuestsPerDay = 3;

    public static WeaponDef Weapon(WeaponId id) => Weapons[(int)id];
    public static CostumeDef Costume(CostumeId id) => Costumes[(int)id];
    public static BonusDef Bonus(BonusId id) => Bonuses[(int)id];
    public static ProductDef? Product(string id) { foreach (var p in Products) if (p.id == id) return p; return null; }

    // ---- permanent weapon upgrades
    static readonly float[] WeaponTier = { 1f, 1.1f, 1.2f, 1.3f, 1.4f };
    static readonly int[] StatBase = { 160, 120, 120 };

    /// <summary>Gold for the next level of <paramref name="stat"/> (current level <paramref name="level"/>), -1 at max.</summary>
    public static int UpgradeCost(WeaponId w, WeaponStat stat, int level)
    {
        if (level >= MaxUpgrade) return -1;
        return Mathf.RoundToInt(StatBase[(int)stat] * WeaponTier[(int)w] * Mathf.Pow(1.55f, level) / 10f) * 10;
    }

    public static float DamageMul(int level) => 1f + 0.08f * level;
    public static float MagazineMul(int level) => 1f + 0.12f * level;
    public static float ReloadMul(int level) => 1f - 0.07f * level;

    public static int Magazine(WeaponDef d, int level) => Mathf.Max(1, Mathf.RoundToInt(d.magazine * MagazineMul(level)));

    // ---- rewards
    public static float GoldMultiplier(bool vip, int goldBoostLevel) => 1f + (vip ? VipGoldBonus : 0f) + GoldBoostPerLevel * goldBoostLevel;

    /// <summary>End-of-run gold (endless): wave reached, kills, objectives, bosses, score.</summary>
    public static int RunGold(int score, int wave, int kills, int objectives, int bosses, float mul)
    {
        float g = wave * 12f + kills * 0.25f + objectives * 30f + bosses * 150f + score / 200f;
        return Mathf.Max(0, Mathf.RoundToInt(g * mul));
    }

    public static int StartHealthBonus(int level) => StartHealthPerLevel * Mathf.Clamp(level, 0, 3);

    // ---- days (UTC day numbers so the cycle is the same in every time zone)
    public static int DayNumber(DateTime utc) => (int)(utc.Date - new DateTime(2020, 1, 1)).TotalDays;

    /// <summary>
    /// Daily login: claimable once per day; the streak advances on consecutive days and restarts after a missed day
    /// (or after day 7). Returns the reward index to grant, or -1 when already claimed today.
    /// </summary>
    public static int LoginIndex(int lastDay, int lastIndex, int today)
    {
        if (lastDay == today) return -1;
        if (lastDay == today - 1 && lastIndex >= 0) return (lastIndex + 1) % LoginRewards.Length;
        return 0;
    }

    /// <summary>The day's quests: <see cref="QuestsPerDay"/> distinct entries of the pool, seeded by the day.</summary>
    public static List<int> QuestsFor(int day)
    {
        var rng = new System.Random(day * 7919 + 17);
        var idx = new List<int>();
        for (int i = 0; i < QuestPool.Length; i++) idx.Add(i);
        for (int i = idx.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (idx[i], idx[j]) = (idx[j], idx[i]); }
        idx.RemoveRange(QuestsPerDay, idx.Count - QuestsPerDay);
        return idx;
    }
}
