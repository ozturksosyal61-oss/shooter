using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The only place that changes currency, inventory, upgrades, selections and daily state (save-backed).
/// UI and gameplay call these; rules come from <see cref="Economy"/>. Campaign rewards (F12) go through
/// <see cref="Grant"/> like everything else.
/// </summary>
public static class Profile
{
    public static event Action Changed;

    static SaveData D => SaveSystem.Data;
    static EconomyData E => D.economy;

    public static int Gold => D.wallet.gold;
    public static int Gems => D.wallet.gems;
    public static bool Vip => E.vip;

    /// <summary>"Now" for daily systems (tests override).</summary>
    public static Func<DateTime> UtcNow = () => DateTime.UtcNow;
    public static int Today => Economy.DayNumber(UtcNow());

    static void Commit()
    {
        SaveSystem.Write();
        Changed?.Invoke();
    }

    // ------------------------------------------------------------------ currency
    /// <summary>Adds rewards from any source (run, quest, login, campaign, purchase).</summary>
    public static void Grant(int gold, int gems, string reason)
    {
        if (gold <= 0 && gems <= 0) return;
        D.wallet.gold += Mathf.Max(0, gold);
        D.wallet.gems += Mathf.Max(0, gems);
        E.lifetimeGold += Mathf.Max(0, gold);
        Commit();
    }

    public static bool CanAfford(int gold, int gems) => Gold >= gold && Gems >= gems;

    public static bool TrySpend(int gold, int gems)
    {
        if (gold < 0 || gems < 0 || !CanAfford(gold, gems)) return false;
        D.wallet.gold -= gold;
        D.wallet.gems -= gems;
        Commit();
        return true;
    }

    public static float GoldMultiplier => Economy.GoldMultiplier(E.vip, E.goldBoost);

    // ------------------------------------------------------------------ weapons
    public static bool Owns(WeaponId w) => E.weapons.Contains(w.ToString());
    public static WeaponId SelectedWeapon => Enum.TryParse(E.weapon, out WeaponId w) && Owns(w) ? w : WeaponId.M4;

    public static bool BuyWeapon(WeaponId w)
    {
        if (Owns(w) || !TrySpend(Economy.Weapon(w).price, 0)) return false;
        E.weapons.Add(w.ToString());
        Commit();
        return true;
    }

    public static void SelectWeapon(WeaponId w)
    {
        if (!Owns(w)) return;
        E.weapon = w.ToString();
        Commit();
    }

    static WeaponLevels Levels(WeaponId w, bool create)
    {
        foreach (var l in E.levels) if (l.id == w.ToString()) return l;
        if (!create) return null;
        var n = new WeaponLevels { id = w.ToString() };
        E.levels.Add(n);
        return n;
    }

    public static int Level(WeaponId w, WeaponStat s)
    {
        var l = Levels(w, false);
        if (l == null) return 0;
        return s == WeaponStat.Damage ? l.damage : s == WeaponStat.Magazine ? l.magazine : l.reload;
    }

    public static bool BuyUpgrade(WeaponId w, WeaponStat s)
    {
        if (!Owns(w)) return false;
        int cost = Economy.UpgradeCost(w, s, Level(w, s));
        if (cost < 0 || !TrySpend(cost, 0)) return false;
        var l = Levels(w, true);
        if (s == WeaponStat.Damage) l.damage++; else if (s == WeaponStat.Magazine) l.magazine++; else l.reload++;
        Commit();
        return true;
    }

    // ------------------------------------------------------------------ gun finishes + tracers (1.1.0)
    public static bool Owns(GunSkinId k) => E.gunSkins.Contains(k.ToString());
    public static bool Owns(TracerId k) => E.tracers.Contains(k.ToString());

    /// <summary>The finish shown on <paramref name="w"/> (standard when not owned any more / never chosen).</summary>
    public static GunSkinId SkinOf(WeaponId w)
    {
        var l = Levels(w, false);
        return l != null && Enum.TryParse(l.skin, out GunSkinId k) && Owns(k) ? k : GunSkinId.Standard;
    }

    public static TracerId SelectedTracer => Enum.TryParse(E.tracer, out TracerId t) && Owns(t) ? t : TracerId.Standard;

    /// <summary>Buys a finish and puts it on <paramref name="on"/> right away.</summary>
    public static bool BuyGunSkin(GunSkinId k, WeaponId on)
    {
        var d = Economy.GunSkin(k);
        if (Owns(k) || !TrySpend(d.gold, d.gems)) return false;
        E.gunSkins.Add(k.ToString());
        Levels(on, true).skin = k.ToString();
        Commit();
        return true;
    }

    public static void SelectGunSkin(WeaponId w, GunSkinId k)
    {
        if (!Owns(k)) return;
        Levels(w, true).skin = k.ToString();
        Commit();
    }

    public static bool BuyTracer(TracerId t)
    {
        var d = Economy.Tracer(t);
        if (Owns(t) || !TrySpend(d.gold, d.gems)) return false;
        E.tracers.Add(t.ToString());
        E.tracer = t.ToString();
        Commit();
        return true;
    }

    public static void SelectTracer(TracerId t)
    {
        if (!Owns(t)) return;
        E.tracer = t.ToString();
        Commit();
    }

    // ------------------------------------------------------------------ costumes
    public static bool Owns(CostumeId c) => E.costumes.Contains(c.ToString());
    public static CostumeId SelectedCostume => Enum.TryParse(E.costume, out CostumeId c) && Owns(c) ? c : CostumeId.Standard;

    /// <summary>The soldier's skin to show / restore (even while another character is selected).</summary>
    public static CostumeId SoldierSkin =>
        Enum.TryParse(E.soldierSkin, out CostumeId c) && Owns(c) && !Economy.IsCharacter(c) ? c : CostumeId.Standard;

    public static bool BuyCostume(CostumeId c)
    {
        var def = Economy.Costume(c);
        if (Owns(c) || def.packOnly || !TrySpend(def.gold, def.gems)) return false;
        E.costumes.Add(c.ToString());
        Select(c);
        Commit();
        return true;
    }

    public static void SelectCostume(CostumeId c)
    {
        if (!Owns(c)) return;
        Select(c);
        Commit();
    }

    /// <summary>Shop "Characters" tab: the soldier (with its last skin) or a whole other character.</summary>
    public static void SelectCharacter(CostumeId c) => SelectCostume(Economy.IsCharacter(c) ? c : SoldierSkin);

    static void Select(CostumeId c)
    {
        E.costume = c.ToString();
        if (!Economy.IsCharacter(c)) E.soldierSkin = c.ToString();
    }

    // ------------------------------------------------------------------ bonuses
    public static int BonusLevel(BonusId b) => b == BonusId.StartHealth ? E.startHealth : b == BonusId.GoldBoost ? E.goldBoost : E.startCards;

    public static int BonusCost(BonusId b)
    {
        var d = Economy.Bonus(b);
        if (d.consumable) return d.costs[0];
        int l = BonusLevel(b);
        return l >= d.maxLevel ? -1 : d.costs[l];
    }

    public static bool BuyBonus(BonusId b)
    {
        int cost = BonusCost(b);
        if (cost < 0 || !TrySpend(cost, 0)) return false;
        if (b == BonusId.StartHealth) E.startHealth++; else if (b == BonusId.GoldBoost) E.goldBoost++; else E.startCards++;
        Commit();
        return true;
    }

    /// <summary>Uses one start-card charge (at the start of a run).</summary>
    public static bool ConsumeStartCard()
    {
        if (E.startCards <= 0) return false;
        E.startCards--;
        Commit();
        return true;
    }

    // ------------------------------------------------------------------ daily login
    public static int LoginClaimable => Economy.LoginIndex(E.loginDay, E.loginIndex, Today);
    public static int LoginStreakIndex => E.loginIndex;

    public static bool ClaimLogin(int multiplier = 1)
    {
        int i = LoginClaimable;
        if (i < 0) return false;
        E.loginDay = Today;
        E.loginIndex = i;
        var r = Economy.LoginRewards[i];
        Grant(r.gold * multiplier, r.gems * multiplier, "login");
        return true;
    }

    // ------------------------------------------------------------------ daily quests
    public static List<QuestState> Quests
    {
        get
        {
            int today = Today;
            if (E.questDay != today)
            {
                E.questDay = today;
                E.quests.Clear();
                foreach (int i in Economy.QuestsFor(today)) E.quests.Add(new QuestState { index = i });
                SaveSystem.Write();
            }
            return E.quests;
        }
    }

    /// <summary>Feeds a finished run into the day's quests (counters add up; wave / combo keep the best).</summary>
    public static void ReportRun(int kills, int specials, int bosses, int wave, int objectives, int bestCombo, bool newRun = true)
    {
        foreach (var q in Quests)
        {
            var def = Economy.QuestPool[q.index];
            switch (def.kind)
            {
                case QuestKind.Kills: q.progress += kills; break;
                case QuestKind.Specials: q.progress += specials; break;
                case QuestKind.Bosses: q.progress += bosses; break;
                case QuestKind.Objectives: q.progress += objectives; break;
                case QuestKind.Runs: q.progress += newRun ? 1 : 0; break;
                case QuestKind.ReachWave: q.progress = Mathf.Max(q.progress, wave); break;
                case QuestKind.Combo: q.progress = Mathf.Max(q.progress, bestCombo); break;
            }
            q.progress = Mathf.Min(q.progress, def.target);
        }
        Commit();
    }

    public static bool ClaimQuest(int slot)
    {
        var qs = Quests;
        if (slot < 0 || slot >= qs.Count) return false;
        var q = qs[slot];
        var def = Economy.QuestPool[q.index];
        if (q.claimed || q.progress < def.target) return false;
        q.claimed = true;
        Grant(def.reward, 0, "quest");
        return true;
    }

    // ------------------------------------------------------------------ ads / purchases
    public static bool FreeGemAvailable => E.freeGemDay != Today;

    public static bool ClaimFreeGems()
    {
        if (!FreeGemAvailable) return false;
        E.freeGemDay = Today;
        Grant(0, Economy.FreeGemAd, "ad");
        return true;
    }

    public static bool BuyGoldWithGems(int offer)
    {
        if (offer < 0 || offer >= Economy.GoldOffers.Length) return false;
        var o = Economy.GoldOffers[offer];
        if (!TrySpend(0, o.gems)) return false;
        Grant(o.gold, 0, "exchange");
        return true;
    }

    /// <summary>Delivers a store product. <paramref name="restore"/>: re-grant entitlements only (no currency twice).</summary>
    public static bool Deliver(string productId, string transactionId, bool restore)
    {
        var p = Economy.Product(productId);
        if (p == null) return false;
        var def = p.Value;
        if (!string.IsNullOrEmpty(transactionId))
        {
            if (E.receipts.Contains(transactionId)) return false; // already delivered
            E.receipts.Add(transactionId);
            if (E.receipts.Count > 200) E.receipts.RemoveAt(0);
        }
        bool already = def.kind == ProductKind.NonConsumable && (def.vip ? E.vip : E.starterPack);
        if (def.vip) E.vip = true;
        if (def.costume != CostumeId.Standard && !Owns(def.costume)) E.costumes.Add(def.costume.ToString());
        if (def.kind == ProductKind.NonConsumable && !def.vip) E.starterPack = true;
        if (!restore && !already) Grant(def.gold, def.gems, "iap");
        else Commit();
        return true;
    }

    public static bool StarterPackOwned => E.starterPack;
}
