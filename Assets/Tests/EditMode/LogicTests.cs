using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// Fast unit tests for pure game logic: difficulty formula, XP curve, combo steps, cards, localization.

public class DifficultyTests
{
    [Test]
    public void WavesGetHarder()
    {
        var prev = Difficulty.ForWave(1);
        for (int n = 2; n <= 30; n++)
        {
            var w = Difficulty.ForWave(n);
            Assert.LessOrEqual(w.spawnInterval, prev.spawnInterval, $"wave {n} interval");
            Assert.GreaterOrEqual(w.maxAlive, prev.maxAlive, $"wave {n} max alive");
            Assert.Greater(w.healthMultiplier, prev.healthMultiplier, $"wave {n} health");
            Assert.GreaterOrEqual(w.speedMultiplier, prev.speedMultiplier, $"wave {n} speed");
            Assert.Greater(w.contactDamage, prev.contactDamage, $"wave {n} damage");
            Assert.GreaterOrEqual(w.duration, prev.duration, $"wave {n} duration");
            prev = w;
        }
    }

    [Test]
    public void FormulaValues()
    {
        var w1 = Difficulty.ForWave(1);
        Assert.AreEqual(1, w1.wave);
        Assert.AreEqual(30f, w1.duration, 1e-4f);
        Assert.AreEqual(Difficulty.BaseInterval * Mathf.Pow(Difficulty.IntervalDecay, Difficulty.DensityStep(1)), w1.spawnInterval, 1e-4f);
        Assert.AreEqual(Difficulty.BaseAlive + Mathf.RoundToInt(Difficulty.AlivePerWave * Difficulty.DensityStep(1)), w1.maxAlive);
        Assert.AreEqual(1f, w1.healthMultiplier, 1e-4f);
        Assert.AreEqual(Difficulty.BaseDamage, w1.contactDamage, 1e-4f);
        Assert.AreEqual(0f, w1.runnerChance, 1e-4f);

        var w5 = Difficulty.ForWave(5);
        Assert.AreEqual(Difficulty.BaseAlive + Mathf.RoundToInt(Difficulty.AlivePerWave * Difficulty.DensityStep(5)), w5.maxAlive);
        Assert.Less(Difficulty.DensityStep(5), 4f, "waves 4-6 ramp slower");
        Assert.AreEqual(6f, Difficulty.DensityStep(7), 1e-4f, "back on the curve from wave 7");
        Assert.Greater(Difficulty.DensityStep(1), 0.5f, "wave 1 not empty");
        for (int n = 1; n < 12; n++) Assert.Less(Difficulty.DensityStep(n), Difficulty.DensityStep(n + 1), "density rises every wave");
        Assert.AreEqual(Difficulty.BaseDamage + 4 * Difficulty.DamagePerWave, w5.contactDamage, 1e-4f);
        Assert.AreEqual(Difficulty.BaseInterval * Mathf.Pow(Difficulty.IntervalDecay, Difficulty.DensityStep(5)), w5.spawnInterval, 1e-4f);
    }

    [Test]
    public void Caps()
    {
        var w = Difficulty.ForWave(200);
        Assert.AreEqual(45f, w.duration, 1e-4f);
        Assert.AreEqual(Difficulty.MinInterval, w.spawnInterval, 1e-4f);
        Assert.AreEqual(Difficulty.MaxAliveCap, w.maxAlive);
        Assert.AreEqual(1.45f, w.speedMultiplier, 1e-4f);
        Assert.AreEqual(0.45f, w.runnerChance, 1e-4f);
        Assert.LessOrEqual(w.specialChance, 0.55f);
    }

    [Test]
    public void SpecialZombies()
    {
        for (int n = 1; n < Difficulty.FirstSpecialWave; n++)
        {
            var w = Difficulty.ForWave(n);
            Assert.AreEqual(0f, w.specialChance, $"no specials on wave {n}");
            Assert.AreEqual(0, w.maxShield + w.maxBomb + w.maxTongue);
        }
        var first = Difficulty.ForWave(Difficulty.FirstSpecialWave);
        Assert.Greater(first.specialChance, 0f);
        Assert.Greater(first.maxShield + first.maxBomb + first.maxTongue, 0);

        int every = Difficulty.SpecialWaveEvery;
        for (int n = 1; n <= 40; n++)
            Assert.AreEqual(n % every == 0, Difficulty.ForWave(n).specialWave, $"special wave flag {n}");

        // A special wave doubles the type limits and boosts the chance over its neighbours.
        var sw = Difficulty.ForWave(every);
        var before = Difficulty.ForWave(every - 1);
        Assert.Greater(sw.specialChance, before.specialChance);
        Assert.GreaterOrEqual(sw.maxBomb, 2 * before.maxBomb);
        for (int n = 1; n <= 60; n++)
            if (!Difficulty.IsSpecialWave(n)) Assert.LessOrEqual(Difficulty.ForWave(n).specialChance, Difficulty.SpecialMax + 1e-5f);
    }
}

public class ProgressionTests
{
    GameObject go;
    Progression prog;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject("prog");
        prog = go.AddComponent<Progression>();
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(go);

    [Test]
    public void XpCurve()
    {
        Assert.AreEqual(prog.baseXp, prog.Need(1));
        for (int l = 1; l < 30; l++)
        {
            int expected = prog.baseXp + prog.linear * (l - 1) + Mathf.RoundToInt(prog.quadratic * (l - 1) * (l - 1));
            Assert.AreEqual(expected, prog.Need(l), $"level {l}");
            Assert.Greater(prog.Need(l + 1), prog.Need(l), "each level costs more");
        }
    }

    [Test]
    public void LevelUpsQueuePicks()
    {
        int ups = 0;
        prog.LeveledUp += _ => ups++;
        prog.AddXp(prog.Need(1) - 1);
        Assert.AreEqual(1, prog.Level);
        Assert.AreEqual(0, prog.PendingPicks);
        prog.AddXp(1);
        Assert.AreEqual(2, prog.Level);
        Assert.AreEqual(0, prog.Xp);
        Assert.AreEqual(1, prog.PendingPicks);

        // One big chunk crosses several levels and keeps the remainder.
        prog.AddXp(prog.Need(2) + prog.Need(3) + 5);
        Assert.AreEqual(4, prog.Level);
        Assert.AreEqual(5, prog.Xp);
        Assert.AreEqual(3, prog.PendingPicks);
        Assert.AreEqual(3, ups);
        Assert.AreEqual(5f / prog.Need(4), prog.Progress01, 1e-5f);

        for (int i = 0; i < 4; i++) prog.ConsumePick();
        Assert.AreEqual(0, prog.PendingPicks, "never negative");
    }
}

public class ComboTests
{
    [Test]
    public void MultiplierSteps()
    {
        Assert.AreEqual(1, GameManager.MultiplierFor(0));
        Assert.AreEqual(1, GameManager.MultiplierFor(4));
        Assert.AreEqual(2, GameManager.MultiplierFor(5));
        Assert.AreEqual(2, GameManager.MultiplierFor(11));
        Assert.AreEqual(3, GameManager.MultiplierFor(12));
        Assert.AreEqual(10, GameManager.MultiplierFor(160));
        Assert.AreEqual(10, GameManager.MultiplierFor(100000), "capped at x10");
        for (int c = 1; c < 200; c++)
            Assert.GreaterOrEqual(GameManager.MultiplierFor(c), GameManager.MultiplierFor(c - 1));
    }
}

public class CardTests
{
    static int[] Levels() => new int[Enum.GetValues(typeof(CardId)).Length];

    [Test]
    public void TableMatchesEnum()
    {
        var ids = (CardId[])Enum.GetValues(typeof(CardId));
        Assert.AreEqual(22, ids.Length, "15 + 7 new in 1.1.0");
        Assert.AreEqual(ids.Length, Upgrades.Cards.Length);
        for (int i = 0; i < ids.Length; i++)
        {
            Assert.AreEqual(ids[i], Upgrades.Cards[i].id, "Cards[] is indexed by CardId");
            Assert.AreEqual(3, Upgrades.Cards[i].maxLevel);
            Assert.IsFalse(string.IsNullOrEmpty(Upgrades.Cards[i].Icon));
        }
    }

    [Test]
    public void OfferIsDistinctAndSkipsMaxed()
    {
        var into = new List<CardId>(); var scratch = new List<int>();
        var rng = new System.Random(7);
        Func<float> r = () => (float)rng.NextDouble();
        var levels = Levels();
        for (int k = 0; k < 200; k++)
        {
            Upgrades.Offer(levels, 3, r, into, scratch);
            Assert.AreEqual(3, into.Count);
            Assert.AreEqual(3, new HashSet<CardId>(into).Count, "no duplicates");
        }

        // Max everything but two cards: only those two can be offered.
        for (int i = 0; i < levels.Length; i++) levels[i] = 3;
        levels[(int)CardId.Pierce] = 2;
        levels[(int)CardId.Magnet] = 0;
        Upgrades.Offer(levels, 3, r, into, scratch);
        CollectionAssert.AreEquivalent(new[] { CardId.Pierce, CardId.Magnet }, into);

        levels[(int)CardId.Pierce] = 3; levels[(int)CardId.Magnet] = 3;
        Upgrades.Offer(levels, 3, r, into, scratch);
        Assert.AreEqual(0, into.Count, "everything maxed: empty offer");
    }

    [Test]
    public void RarityWeighting()
    {
        var into = new List<CardId>(); var scratch = new List<int>();
        var rng = new System.Random(11);
        Func<float> r = () => (float)rng.NextDouble();
        var levels = Levels();
        var counts = new int[3]; var cardsPer = new int[3];
        foreach (var c in Upgrades.Cards) cardsPer[(int)c.rarity]++;
        const int N = 20000;
        for (int k = 0; k < N; k++)
        {
            Upgrades.Offer(levels, 1, r, into, scratch);
            counts[(int)Upgrades.Get(into[0]).rarity]++;
        }
        float total = 0f;
        foreach (var c in Upgrades.Cards) total += Upgrades.RarityWeightOf(c.rarity);
        for (int i = 0; i < 3; i++)
        {
            float expected = cardsPer[i] * Upgrades.RarityWeightOf((Rarity)i) / total;
            Assert.AreEqual(expected, counts[i] / (float)N, 0.02f, $"rarity {(Rarity)i}");
        }
        Assert.Greater(Upgrades.RarityWeightOf(Rarity.Common), Upgrades.RarityWeightOf(Rarity.Rare));
        Assert.Greater(Upgrades.RarityWeightOf(Rarity.Rare), Upgrades.RarityWeightOf(Rarity.Epic));
    }

    [Test]
    public void FirstPicksGuaranteeADamageCard()
    {
        var into = new List<CardId>(); var scratch = new List<int>();
        var rng = new System.Random(3);
        Func<float> r = () => (float)rng.NextDouble();
        var levels = Levels();
        for (int k = 0; k < 2000; k++)
        {
            Upgrades.Offer(levels, 3, r, into, scratch, true);
            Assert.AreEqual(3, into.Count);
            Assert.AreEqual(3, new HashSet<CardId>(into).Count, "still distinct");
            bool dmg = false; foreach (var id in into) dmg |= Upgrades.IsDamageCard(id);
            Assert.IsTrue(dmg, "guaranteed damage card");
        }
        // Without the guarantee some offers have none (so the guarantee actually does something).
        int without = 0;
        for (int k = 0; k < 2000; k++)
        {
            Upgrades.Offer(levels, 3, r, into, scratch, false);
            bool dmg = false; foreach (var id in into) dmg |= Upgrades.IsDamageCard(id);
            if (!dmg) without++;
        }
        Assert.Greater(without, 0);
        // All damage cards maxed: the guarantee can't apply and the offer is still full.
        foreach (var id in Upgrades.DamageCards) levels[(int)id] = 3;
        Upgrades.Offer(levels, 3, r, into, scratch, true);
        Assert.AreEqual(3, into.Count);
        Assert.AreEqual(3, Upgrades.GuaranteedDamagePicks);
    }

    [Test]
    public void OffersMixCardFamiliesAndFavourOwnedCards()
    {
        var into = new List<CardId>(); var scratch = new List<int>();
        var rng = new System.Random(7);
        Func<float> r = () => (float)rng.NextDouble();
        var levels = Levels();
        int mixed = 0;
        for (int k = 0; k < 2000; k++)
        {
            Upgrades.Offer(levels, 3, r, into, scratch);
            var g = new HashSet<Upgrades.Group>(); foreach (var id in into) g.Add(Upgrades.GroupOf(id));
            if (g.Count == 3) mixed++;
        }
        Assert.Greater(mixed, 1100, "most offers show one damage, one build and one utility card");
        // Owning a card makes its next level show up more often.
        int Seen(int[] lv) { int n = 0; for (int k = 0; k < 3000; k++) { Upgrades.Offer(lv, 3, r, into, scratch); if (into.Contains(CardId.Grenade)) n++; } return n; }
        int fresh = Seen(Levels());
        var owned = Levels(); owned[(int)CardId.Grenade] = 1;
        Assert.Greater(Seen(owned), fresh * 1.2f);
    }

    [Test]
    public void SynergiesNeedBothCards()
    {
        var lv = Levels();
        foreach (var p in Upgrades.SynergyPairs)
        {
            Assert.IsFalse(Upgrades.HasSynergy(lv, p.s));
            lv[(int)p.a] = 1; Assert.IsFalse(Upgrades.HasSynergy(lv, p.s), "one card is not enough");
            lv[(int)p.b] = 2; Assert.IsTrue(Upgrades.HasSynergy(lv, p.s));
            lv = Levels();
            Assert.IsTrue(Loc.Has("syn." + p.s) && Loc.Has("syn." + p.s + ".desc"));
        }
        Assert.AreEqual(System.Enum.GetValues(typeof(Synergy)).Length, Upgrades.SynergyPairs.Length);
    }

    [Test]
    public void WeeklyEventIsStablePerWeek()
    {
        var mon = new System.DateTime(2026, 11, 23, 0, 0, 0, System.DateTimeKind.Utc); // a Monday
        int w = WeeklyEvent.Week(mon);
        Assert.AreEqual(w, WeeklyEvent.Week(mon.AddDays(6.99)), "same week until Sunday night");
        Assert.AreEqual(w + 1, WeeklyEvent.Week(mon.AddDays(7)));
        Assert.AreEqual(w - 1, WeeklyEvent.Week(mon.AddSeconds(-1)));
        Assert.AreEqual(7, WeeklyEvent.DaysLeft(mon));
        Assert.AreEqual(1, WeeklyEvent.DaysLeft(mon.AddDays(6.5)));
        Assert.AreNotEqual(WeeklyEvent.WeaponFor(w), WeeklyEvent.WeaponFor(w + 1), "rule rotates");
        Assert.AreNotEqual(WeeklyEvent.SeedFor(w), WeeklyEvent.SeedFor(w + 1));
        for (int k = -5; k < 200; k++)
        {
            Assert.AreNotEqual(0, WeeklyEvent.SeedFor(k));
            Assert.IsTrue(Loc.Has(WeeklyEvent.RuleKey(k)));
        }
    }

    [Test]
    public void AchievementRules()
    {
        var into = new List<string>();
        GameServices.AchievementsFor(new RunSummary { mode = RunMode.Endless, wave = 4 }, 0, 18, into);
        Assert.AreEqual(0, into.Count);
        GameServices.AchievementsFor(new RunSummary { mode = RunMode.Hard, wave = 10, bosses = 1, bestCombo = 50, helicopters = 1 }, 0, 18, into);
        CollectionAssert.IsSubsetOf(new[] { GameIds.AchWave5, GameIds.AchWave10, GameIds.AchHard10, GameIds.AchFirstBoss, GameIds.AchCombo50, GameIds.AchHelicopter }, into);
        CollectionAssert.DoesNotContain(into, GameIds.AchWave20);
        GameServices.AchievementsFor(new RunSummary { mode = RunMode.Campaign, wave = 30, won = true, chapter = Campaign.Count - 1 }, 18, 18, into);
        CollectionAssert.Contains(into, GameIds.AchCampaign);
        CollectionAssert.Contains(into, GameIds.AchAllStars);
        CollectionAssert.DoesNotContain(into, GameIds.AchWave20, "campaign waves don't count for wave achievements");
        foreach (var id in into) CollectionAssert.Contains(GameIds.Achievements, id);
    }

    [Test]
    public void TurretLimit()
    {
        Assert.AreEqual(0, Upgrades.MaxTurrets(0));
        Assert.AreEqual(1, Upgrades.MaxTurrets(1));
        Assert.AreEqual(1, Upgrades.MaxTurrets(2));
        Assert.AreEqual(2, Upgrades.MaxTurrets(3));
    }

    [Test]
    public void StatFormulasMatchDescriptions()
    {
        // +20/40/60% fire rate
        Assert.AreEqual(1.2f, Upgrades.FireRateMul(1), 1e-5f);
        Assert.AreEqual(1.6f, Upgrades.FireRateMul(3), 1e-5f);
        // reload 20/36/49% faster
        Assert.AreEqual(0.80f, Upgrades.ReloadMul(1), 1e-4f);
        Assert.AreEqual(0.64f, Upgrades.ReloadMul(2), 1e-4f);
        Assert.AreEqual(0.512f, Upgrades.ReloadMul(3), 1e-4f);
        // +10/20/30% crit, capped at 90%
        Assert.AreEqual(0.35f, Upgrades.CritChance(0.05f, 3), 1e-5f);
        Assert.AreEqual(0.9f, Upgrades.CritChance(0.85f, 3), 1e-5f);
        // magazine +10/20/30, max HP +25/50/75
        Assert.AreEqual(70, Upgrades.MagazineSize(40, 3));
        Assert.AreEqual(150f, Upgrades.MaxHealth(100f, 2), 1e-5f);
        // +10/20/30% move, magnet range +60/120/180%
        Assert.AreEqual(1.3f, Upgrades.MoveMul(3), 1e-5f);
        Assert.AreEqual(2.8f, Upgrades.MagnetMul(3), 1e-5f);
        // kill blast 15/25/35%
        Assert.AreEqual(0f, Upgrades.KillBlastChance(0));
        Assert.AreEqual(0.15f, Upgrades.KillBlastChance(1), 1e-5f);
        Assert.AreEqual(0.35f, Upgrades.KillBlastChance(3), 1e-5f);
        // shotgun fires more often per level
        Assert.Greater(Upgrades.ShotgunInterval(1), Upgrades.ShotgunInterval(2));
        Assert.Greater(Upgrades.ShotgunInterval(2), Upgrades.ShotgunInterval(3));
    }
}

public class LocTests
{
    [TearDown]
    public void TearDown() => Loc.Current = Lang.Tr;

    [Test]
    public void EveryKeyHasBothLanguages()
    {
        int n = 0;
        foreach (var key in Loc.Keys)
        {
            n++;
            for (int l = 0; l < Loc.LanguageCount; l++)
                Assert.IsFalse(string.IsNullOrEmpty(Loc.T(key, (Lang)l)), $"{key} [{(Lang)l}]");
            // Formatted entries must have the same placeholders in every language.
            string tr = Loc.T(key, Lang.Tr), en = Loc.T(key, Lang.En);
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(tr.Contains("{" + i + "}"), en.Contains("{" + i + "}"), $"{key} placeholder {i}");
        }
        Assert.Greater(n, 50);
    }

    [Test]
    public void EveryCardHasText()
    {
        Loc.Current = Lang.Tr;
        foreach (var c in Upgrades.Cards)
        {
            Assert.IsTrue(Loc.Has("card." + c.id + ".title"), c.id + " title");
            for (int l = 1; l <= c.maxLevel; l++) Assert.IsTrue(Loc.Has("card." + c.id + "." + l), c.id + " level " + l);
        }
        Assert.AreEqual("Delici Mermi", Upgrades.Get(CardId.Pierce).Title);
        Assert.AreEqual("Mermi 3 zombiden geçer", Upgrades.Get(CardId.Pierce).Desc(9), "level clamps to max");
        Assert.AreEqual("NADİR", Upgrades.RarityName(Rarity.Rare));
    }

    [Test]
    public void SwitchFormatUpper()
    {
        Loc.Current = Lang.Tr;
        Assert.AreEqual("DALGA 3", Loc.F("hud.wave", 3));
        Assert.AreEqual("Holdout", Loc.T("game.title"));
        Assert.AreEqual("Holdout: Last City", Loc.T("game.store_title"));
        Assert.AreEqual("Son Devriye", Loc.T("game.story"));
        for (int i = 1; i <= LoadingScreen.TipCount; i++) Assert.IsTrue(Loc.Has("tip." + i), "loading tip " + i);
        Assert.AreEqual("ÇİFT MERMİ", Loc.Upper("Çift Mermi"));
        Assert.AreEqual("KIRIK", Loc.Upper("kırık"));

        int changed = 0;
        Action h = () => changed++;
        Loc.Changed += h;
        Loc.Current = Lang.En;
        Loc.Changed -= h;
        Assert.AreEqual(1, changed);
        Assert.AreEqual("WAVE 3", Loc.F("hud.wave", 3));
        Assert.AreEqual("Holdout: Last City", Loc.T("game.store_title"));
        Assert.AreEqual("DOUBLE SHOT", Loc.Upper("Double Shot"));
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, "Loc: missing key missing.key");
        Assert.AreEqual("missing.key", Loc.T("missing.key"));
    }
}

public class CityLayoutTests
{
    const int Seed = 4242;

    [Test]
    public void RoadsConnectAcrossNeighbours()
    {
        for (int x = -20; x < 20; x++)
            for (int z = -20; z < 20; z++)
            {
                var c = new Vector2Int(x, z);
                Assert.AreEqual(CityLayout.HasRoad(Seed, c, Edge.E), CityLayout.HasRoad(Seed, c + Vector2Int.right, Edge.W), $"E/W {c}");
                Assert.AreEqual(CityLayout.HasRoad(Seed, c, Edge.N), CityLayout.HasRoad(Seed, c + Vector2Int.up, Edge.S), $"N/S {c}");
            }
    }

    [Test]
    public void NoDeadEndsAndManyStraightStretches()
    {
        int straight = 0, junction = 0, nodes = 0;
        for (int i = -30; i < 30; i++)
            for (int j = -30; j < 30; j++)
            {
                int d = CityLayout.NodeDegree(Seed, i, j);
                Assert.AreNotEqual(1, d, $"dead end at node {i},{j}");
                if (d == 0) continue;
                nodes++;
                if (CityLayout.IsJunction(Seed, i, j)) junction++; else straight++;
            }
        Assert.Greater(straight, nodes / 3, "most road nodes should be plain straight avenue (no crossing)");
        Assert.Greater(junction, nodes / 10, "still enough junctions");
    }

    [Test]
    public void EveryChunkTypeAppearsAndRulesHold()
    {
        var counts = new int[System.Enum.GetValues(typeof(ChunkType)).Length];
        for (int x = -40; x < 40; x++)
            for (int z = -40; z < 40; z++)
            {
                var c = new Vector2Int(x, z);
                var d = (District)(((x / 7) + (z / 5)) & 3);
                var t = CityLayout.NaturalType(Seed, c, d);
                counts[(int)t]++;
                int mask = CityLayout.Mask(Seed, c);
                if (t == ChunkType.Alley) Assert.IsTrue(CityLayout.AlleyFits(mask), $"alley {c} needs open S/N edges");
                if (CityLayout.IsLandmark(t)) Assert.AreNotEqual(0, mask, $"landmark {c} must face a road");
                Assert.IsFalse(CityLayout.IsObjectiveSite(t), "sites are only placed on demand");
            }
        for (int i = 0; i <= (int)ChunkType.Hospital; i++) Assert.Greater(counts[i], 0, ((ChunkType)i).ToString());
        Assert.Greater(counts[(int)ChunkType.Block], counts[(int)ChunkType.Park], "blocks stay the most common");
        Assert.AreEqual(ChunkType.Block, CityLayout.NaturalType(Seed, Vector2Int.zero, District.Cadde), "spawn chunk");
    }

    [Test]
    public void LayoutIsDeterministicPerSeed()
    {
        int diff = 0;
        for (int x = 0; x < 30; x++)
        {
            var c = new Vector2Int(x, 3);
            Assert.AreEqual(CityLayout.Mask(Seed, c), CityLayout.Mask(Seed, c));
            if (CityLayout.Mask(Seed, c) != CityLayout.Mask(Seed + 1, c) || CityLayout.NaturalType(Seed, c, District.Cadde) != CityLayout.NaturalType(Seed + 1, c, District.Cadde)) diff++;
        }
        Assert.Greater(diff, 0, "another seed gives another city");
    }

    [Test]
    public void AtlasUvsStayInsideTheirCell()
    {
        for (int cell = 0; cell < 16; cell++)
        {
            var a = GroundAtlas.UV(cell, 0f, 0f);
            var b = GroundAtlas.UV(cell, 1f, 1f);
            int cx = cell % GroundAtlas.Grid, cy = cell / GroundAtlas.Grid;
            float lo = 1f / GroundAtlas.Grid;
            Assert.Greater(a.x, cx * lo); Assert.Less(b.x, (cx + 1) * lo);
            Assert.Greater(a.y, cy * lo); Assert.Less(b.y, (cy + 1) * lo);
        }
    }
}

public class ObjectiveTests
{
    [Test]
    public void ScheduleEveryThreeWaves()
    {
        Assert.IsFalse(Objectives.ShouldIssue(1, -1, false), "not in wave 1");
        Assert.IsTrue(Objectives.ShouldIssue(2, -1, false));
        Assert.IsFalse(Objectives.ShouldIssue(3, 2, false));
        Assert.IsFalse(Objectives.ShouldIssue(4, 2, false));
        Assert.IsTrue(Objectives.ShouldIssue(5, 2, false));
        Assert.IsFalse(Objectives.ShouldIssue(9, 2, true), "never two at once");
    }

    [Test]
    public void EndlessPlanFirstHelicopterAfterTwoThenThree()
    {
        var plan = new EndlessPlan();
        var rng = new System.Random(3);
        var firstBag = new HashSet<ObjectiveKind>();
        Assert.AreEqual(2, Objectives.TasksForHelicopter(0));
        Assert.AreEqual(3, Objectives.TasksForHelicopter(1));
        for (int cycle = 0; cycle < 3; cycle++)
        {
            int need = cycle == 0 ? 2 : 3;
            for (int i = 0; i < need; i++)
            {
                Assert.IsFalse(plan.HelicopterDue);
                var s = plan.Next(rng);
                Assert.AreNotEqual(ObjectiveKind.Helicopter, s.kind);
                if (firstBag.Count < 3) Assert.IsTrue(firstBag.Add(s.kind), "the bag deals flare / tower / supply once each");
                plan.Completed(s);
            }
            Assert.IsTrue(plan.HelicopterDue, $"helicopter due after {need} tasks (cycle {cycle})");
            Assert.IsTrue(plan.ShouldIssue(1, 99, false), "helicopter comes right away");
            var h = plan.Next(rng);
            Assert.AreEqual(ObjectiveKind.Helicopter, h.kind);
            Assert.AreEqual(ChunkType.Helipad, h.site);
            Assert.AreEqual(Objectives.HeliHold, h.hold, 1e-4f);
            plan.Completed(h);
            Assert.AreEqual(cycle + 1, plan.Helicopters);
            Assert.AreEqual(0, plan.DoneInCycle);
        }
    }

    [Test]
    public void BossWavesHealthAndReward()
    {
        for (int n = 1; n < 6; n++) Assert.IsFalse(BossRules.IsBossWave(n), $"no boss before wave 6 ({n})");
        Assert.IsTrue(BossRules.IsBossWave(6));
        Assert.IsFalse(BossRules.IsBossWave(7));
        Assert.IsTrue(BossRules.IsBossWave(11));
        Assert.IsTrue(BossRules.IsBossWave(16));
        Assert.IsFalse(BossRules.IsBossWave(15));
        Assert.AreEqual(0, BossRules.BossIndex(6));
        Assert.AreEqual(2, BossRules.BossIndex(16));

        float h10 = BossRules.Health(70f, Difficulty.ForWave(6), 0);
        float h15 = BossRules.Health(70f, Difficulty.ForWave(11), 1);
        Assert.Greater(h10, 70f * 20f, "a boss is a long fight");
        Assert.Greater(h15, h10 * 1.3f, "later bosses are tougher");

        Assert.IsFalse(BossRules.Enraged(0.51f));
        Assert.IsTrue(BossRules.Enraged(0.49f));
        Assert.AreEqual(40f, BossRules.DamageTaken(20f, true), 1e-4f);
        Assert.AreEqual(20f, BossRules.DamageTaken(20f, false), 1e-4f);

        BossRules.Reward(6, 0, out int s0, out int x0);
        BossRules.Reward(11, 1, out int s1, out int x1);
        Assert.Greater(s0, 1000);
        Assert.Greater(s1, s0);
        Assert.Greater(x1, x0);
        Assert.Less(BossRules.AliveMul, 1f);
        Assert.Greater(BossRules.SpawnIntervalMul, 1f);
    }

    [Test]
    public void BossRewardGuaranteesEpicCard()
    {
        var into = new List<CardId>(); var scratch = new List<int>();
        var rng = new System.Random(11);
        Func<float> r = () => (float)rng.NextDouble();
        var levels = new int[System.Enum.GetValues(typeof(CardId)).Length];
        for (int k = 0; k < 300; k++)
        {
            Upgrades.Offer(levels, 3, r, into, scratch, false, Rarity.Epic);
            Assert.AreEqual(3, into.Count);
            Assert.AreEqual(3, new HashSet<CardId>(into).Count, "no duplicates");
            Assert.AreEqual(Rarity.Epic, Upgrades.Get(into[0]).rarity, "epic card first");
        }
        // Every epic maxed: falls back to a rare.
        foreach (var c in Upgrades.Cards) if (c.rarity == Rarity.Epic) levels[(int)c.id] = c.maxLevel;
        for (int k = 0; k < 100; k++)
        {
            Upgrades.Offer(levels, 3, r, into, scratch, false, Rarity.Epic);
            Assert.IsTrue(into.Exists(id => Upgrades.Get(id).rarity == Rarity.Rare), "rare fallback");
        }
    }

    [Test]
    public void StepsAndRewards()
    {
        var f = Objectives.Step(ObjectiveKind.Flare, null);
        Assert.AreEqual(ChunkType.PoliceStation, f.site);
        Assert.AreEqual(0f, f.hold);
        var t = Objectives.Step(ObjectiveKind.Tower, null);
        Assert.AreEqual(ChunkType.SignalTower, t.site);
        Assert.Greater(t.hold, 0f);
        var s = Objectives.Step(ObjectiveKind.Supply, new System.Random(1));
        Assert.IsTrue(s.site == ChunkType.Hospital || s.site == ChunkType.GasStation);
        foreach (ObjectiveKind k in System.Enum.GetValues(typeof(ObjectiveKind)))
            Assert.IsTrue(Loc.Has(Objectives.Step(k, new System.Random(2)).briefKey), k + " brief");

        Objectives.Reward(ObjectiveKind.Flare, 5, 0, out int sc, out int xp);
        Objectives.Reward(ObjectiveKind.Flare, 8, 0, out int sc2, out int xp2);
        Assert.Greater(sc2, sc); Assert.Greater(xp2, xp);
        Objectives.Reward(ObjectiveKind.Helicopter, 5, 0, out int hs, out int hx);
        Assert.Greater(hs, sc * 3, "helicopter is the big reward");
        Assert.Greater(hx, xp);
        Assert.IsTrue(Objectives.SupplyGivesHeal(0.3f));
        Assert.IsFalse(Objectives.SupplyGivesHeal(0.9f));
    }

    [Test]
    public void DirectionWords()
    {
        Assert.AreEqual("dir.n", Objectives.DirectionKey(new Vector2(0f, 50f)));
        Assert.AreEqual("dir.s", Objectives.DirectionKey(new Vector2(0f, -50f)));
        Assert.AreEqual("dir.e", Objectives.DirectionKey(new Vector2(50f, 2f)));
        Assert.AreEqual("dir.w", Objectives.DirectionKey(new Vector2(-50f, 0f)));
        Assert.AreEqual("dir.ne", Objectives.DirectionKey(new Vector2(40f, 40f)));
        Assert.AreEqual("dir.sw", Objectives.DirectionKey(new Vector2(-40f, -38f)));
        Loc.Current = Lang.Tr;
        Assert.AreEqual("Merkez: İşaret fişeği kuzeydeki karakolda!", Loc.F("radio.flare", Loc.T("dir.n")));
    }
}

public class SaveAndSettingsTests
{
    [TearDown]
    public void Restore() => SaveSystem.Store = null;

    [Test]
    public void SaveRoundTripMigrationAndCorruption()
    {
        var d = new SaveData { version = SaveSystem.CurrentVersion, tutorialDone = true };
        d.records.bestScore = 1234; d.wallet.gold = 50; d.settings.lang = "en"; d.settings.fps = 120;
        var back = SaveSystem.Deserialize(SaveSystem.Serialize(d));
        Assert.NotNull(back);
        Assert.AreEqual(1234, back.records.bestScore);
        Assert.AreEqual(50, back.wallet.gold);
        Assert.AreEqual("en", back.settings.lang);
        Assert.IsTrue(back.tutorialDone);

        Assert.IsNull(SaveSystem.Deserialize("{ this is not json"), "corrupt text");
        Assert.IsNull(SaveSystem.Deserialize(""), "empty file");
        Assert.IsNull(SaveSystem.Deserialize("{\"version\":0}"), "no version = not a save");

        // An old file without the newer sections still loads, with defaults filled in.
        var old = SaveSystem.Deserialize("{\"version\":1,\"records\":{\"bestScore\":7}}");
        Assert.NotNull(old);
        Assert.AreEqual(7, old.records.bestScore);
        Assert.NotNull(old.wallet); Assert.NotNull(old.settings); Assert.NotNull(old.campaign); Assert.NotNull(old.inventory);
        Assert.AreEqual(SaveSystem.CurrentVersion, old.version);
    }

    [Test]
    public void CorruptMainFallsBackToBackup()
    {
        var good = new SaveData { version = 1 };
        good.records.bestWave = 9;
        var mem = new MemorySaveStore { json = "garbage", backupJson = SaveSystem.Serialize(good) };
        SaveSystem.Store = mem;
        Assert.AreEqual(9, SaveSystem.Data.records.bestWave, "backup used when the main file is corrupt");

        SaveSystem.Data.wallet.gems = 3;
        SaveSystem.Data.settings.music = 0.3f;
        SaveSystem.Write();
        Assert.IsNotNull(SaveSystem.Deserialize(mem.json));
        SaveSystem.ResetProgress();
        Assert.AreEqual(0, SaveSystem.Data.wallet.gems, "progress wiped");
        Assert.AreEqual(0, SaveSystem.Data.records.bestWave);
        Assert.AreEqual(0.3f, SaveSystem.Data.settings.music, 1e-4f, "settings kept");
    }

    [Test]
    public void FrameRateOptions()
    {
        CollectionAssert.AreEqual(new[] { 30, 60 }, Settings.SupportedFps(60));
        CollectionAssert.AreEqual(new[] { 30, 60, 90 }, Settings.SupportedFps(90));
        CollectionAssert.AreEqual(new[] { 30, 60, 90, 120 }, Settings.SupportedFps(120));
        Assert.AreEqual(60, Settings.ClampFps(120, 60), "unsupported rate falls back");
        Assert.AreEqual(90, Settings.ClampFps(90, 120));
        Assert.AreEqual(30, Settings.ClampFps(30, 120));
    }
}

public class EconomyTests
{
    [SetUp]
    public void Mem()
    {
        SaveSystem.Store = new MemorySaveStore();
        Profile.UtcNow = () => new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
    }

    [TearDown]
    public void Restore()
    {
        SaveSystem.Store = null;
        Profile.UtcNow = () => DateTime.UtcNow;
    }

    [Test]
    public void Cosmetics110PricesAndOwnership()
    {
        // Cheapest gem item fits the smallest pack (70); every gold look costs at least a day of play.
        int minGem = int.MaxValue;
        foreach (var d in Economy.GunSkins) { if (d.gems > 0) minGem = Mathf.Min(minGem, d.gems); if (d.gold > 0) Assert.GreaterOrEqual(d.gold, 1000); }
        foreach (var d in Economy.Tracers) { if (d.gems > 0) minGem = Mathf.Min(minGem, d.gems); if (d.gold > 0) Assert.GreaterOrEqual(d.gold, 1000); }
        Assert.LessOrEqual(minGem, Economy.Products[0].gems);
        Assert.IsTrue(Economy.GunSkin(GunSkinId.Standard).Free);
        Assert.IsTrue(Economy.Tracer(TracerId.Standard).Free);
        for (int i = 0; i < Economy.GunSkins.Length; i++) { Assert.AreEqual(i, Economy.GunSkins[i].id); Assert.IsTrue(Loc.Has(Economy.GunSkins[i].key + ".desc")); }
        for (int i = 0; i < Economy.Tracers.Length; i++) { Assert.AreEqual(i, Economy.Tracers[i].id); Assert.IsTrue(Loc.Has(Economy.Tracers[i].key + ".desc")); }

        Assert.AreEqual(GunSkinId.Standard, Profile.SkinOf(WeaponId.M4));
        Assert.IsFalse(Profile.BuyGunSkin(GunSkinId.Carbon, WeaponId.M4), "no gems");
        Profile.Grant(5000, 200, "test");
        Assert.IsTrue(Profile.BuyGunSkin(GunSkinId.Carbon, WeaponId.M4));
        Assert.AreEqual(140, Profile.Gems);
        Assert.AreEqual(GunSkinId.Carbon, Profile.SkinOf(WeaponId.M4), "bought = worn by that gun");
        Assert.AreEqual(GunSkinId.Standard, Profile.SkinOf(WeaponId.Smg), "per weapon");
        Profile.SelectGunSkin(WeaponId.Smg, GunSkinId.Carbon);
        Assert.AreEqual(GunSkinId.Carbon, Profile.SkinOf(WeaponId.Smg), "owned once, usable on every gun");
        Profile.SelectGunSkin(WeaponId.Smg, GunSkinId.Gold);
        Assert.AreEqual(GunSkinId.Carbon, Profile.SkinOf(WeaponId.Smg), "unowned finish can't be chosen");
        Assert.IsTrue(Profile.BuyTracer(TracerId.Red));
        Assert.AreEqual(TracerId.Red, Profile.SelectedTracer);
        Assert.IsFalse(Profile.BuyTracer(TracerId.Red), "already owned");
        Profile.SelectTracer(TracerId.Standard);
        Assert.AreEqual(TracerId.Standard, Profile.SelectedTracer);
    }

    [Test]
    public void SniperBecomesUziOnLoad()
    {
        // A signed v2 save (1.0.x) that owns the sniper with upgrades and has it selected.
        var v2 = new SaveData { version = 2 };
        v2.economy.weapons.Add("Sniper");
        v2.economy.weapon = "Sniper";
        v2.economy.levels.Add(new WeaponLevels { id = "Sniper", damage = 2 });
        v2.economy.gunSkins = null; v2.economy.tracers = null; v2.economy.tracer = null;
        var old = SaveSystem.Deserialize(SaveSystem.Serialize(v2));
        Assert.NotNull(old);
        Assert.AreEqual(SaveSystem.CurrentVersion, old.version);
        CollectionAssert.Contains(old.economy.weapons, nameof(WeaponId.Uzi));
        CollectionAssert.DoesNotContain(old.economy.weapons, "Sniper");
        Assert.AreEqual(nameof(WeaponId.Uzi), old.economy.weapon);
        Assert.AreEqual(nameof(WeaponId.Uzi), old.economy.levels[0].id);
        Assert.AreEqual(2, old.economy.levels[0].damage, "upgrades kept");
        CollectionAssert.Contains(old.economy.gunSkins, nameof(GunSkinId.Standard));
        Assert.AreEqual(nameof(TracerId.Standard), old.economy.tracer);
        Assert.AreEqual(4, (int)WeaponId.Uzi, "same slot as the sniper");
    }

    [Test]
    public void PricesAndUpgradeCurve()
    {
        Assert.AreEqual(0, Economy.Weapon(WeaponId.M4).price, "M4 is the starter");
        for (int i = 1; i < Economy.Weapons.Length; i++) Assert.Greater(Economy.Weapons[i].price, Economy.Weapons[i - 1].price);
        foreach (WeaponId w in Enum.GetValues(typeof(WeaponId)))
            foreach (WeaponStat st in Enum.GetValues(typeof(WeaponStat)))
            {
                for (int l = 1; l < Economy.MaxUpgrade; l++) Assert.Greater(Economy.UpgradeCost(w, st, l), Economy.UpgradeCost(w, st, l - 1), "costs rise");
                Assert.AreEqual(-1, Economy.UpgradeCost(w, st, Economy.MaxUpgrade), "max level");
            }
        // No pay-to-win: maxed permanent upgrades stay small.
        Assert.LessOrEqual(Economy.DamageMul(Economy.MaxUpgrade), 1.4f);
        Assert.GreaterOrEqual(Economy.ReloadMul(Economy.MaxUpgrade), 0.6f);
        Assert.LessOrEqual(Economy.GoldMultiplier(true, 1), 1.36f);
        Assert.AreEqual(30, Economy.StartHealthBonus(9), "start health capped at +30");
        foreach (var p in Economy.Products) StringAssert.StartsWith("com.oztdigital.holdout.", p.id);
        // TRY packs: more gems per lira for bigger packs; one pack buys exactly one character.
        float prev = 0f; bool character = false;
        foreach (var p in Economy.Products)
        {
            if (p.kind != ProductKind.Consumable) continue;
            float tl = float.Parse(p.tl.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Greater(p.gems / tl, prev, p.id + " better value");
            prev = p.gems / tl;
            if (p.gems == Economy.CharacterGems) { character = true; Assert.That(tl, Is.InRange(140f, 160f), "character ≈ 150 TL"); }
        }
        Assert.IsTrue(character, "a pack matches the character price");
        Assert.AreEqual("99,99 TL", Economy.PriceLabel(Economy.Product(Economy.ProductPrefix + "gems_medium").Value));
    }

    [Test]
    public void RunGoldBalance()
    {
        // A typical run (wave 7, ~300 kills, 2 objectives, 1 boss) pays a few hundred gold; a weapon costs 3-4 such runs.
        int typical = Economy.RunGold(6000, 7, 300, 2, 1, 1f);
        Assert.That(typical, Is.InRange(300, 500));
        int noBoss = Economy.RunGold(3000, 5, 180, 1, 0, 1f);
        float runs = Economy.Weapon(WeaponId.Smg).price / (float)((typical + noBoss) / 2);
        Assert.That(runs, Is.InRange(2.5f, 4.5f), "first weapon in ~3-4 runs");
        Assert.Greater(Economy.RunGold(6000, 7, 300, 2, 1, 1.25f), typical);
        Assert.AreEqual(0, Economy.RunGold(0, 0, 0, 0, 0, 1f));
    }

    [Test]
    public void LoginStreakCycle()
    {
        Assert.AreEqual(0, Economy.LoginIndex(-1, -1, 100), "first ever");
        Assert.AreEqual(-1, Economy.LoginIndex(100, 0, 100), "once a day");
        Assert.AreEqual(1, Economy.LoginIndex(100, 0, 101), "consecutive");
        Assert.AreEqual(0, Economy.LoginIndex(100, 6, 101), "cycle restarts after day 7");
        Assert.AreEqual(0, Economy.LoginIndex(100, 3, 103), "missed a day");

        Assert.IsTrue(Profile.ClaimLogin());
        Assert.IsFalse(Profile.ClaimLogin(), "second claim today");
        Assert.AreEqual(Economy.LoginRewards[0].gold, Profile.Gold);
        Profile.UtcNow = () => new DateTime(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);
        Assert.IsTrue(Profile.ClaimLogin());
        Assert.AreEqual(Economy.LoginRewards[0].gold + Economy.LoginRewards[1].gold, Profile.Gold);
    }

    [Test]
    public void QuestsDailyAndClaim()
    {
        var a = Economy.QuestsFor(500); var b = Economy.QuestsFor(500);
        CollectionAssert.AreEqual(a, b, "same day, same quests");
        Assert.AreEqual(Economy.QuestsPerDay, new HashSet<int>(a).Count, "distinct");
        var qs = Profile.Quests;
        Assert.AreEqual(Economy.QuestsPerDay, qs.Count);
        Profile.ReportRun(1000, 100, 3, 20, 10, 200);
        int before = Profile.Gold;
        for (int i = 0; i < qs.Count; i++) Assert.IsTrue(Profile.ClaimQuest(i), "everything done by a huge run");
        Assert.Greater(Profile.Gold, before);
        Assert.IsFalse(Profile.ClaimQuest(0), "claimed once");
        // A continue after death must not count as another run.
        Profile.UtcNow = () => new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);
        Profile.ReportRun(1, 0, 0, 1, 0, 1, true);
        Profile.ReportRun(1, 0, 0, 1, 0, 1, false);
        foreach (var q in Profile.Quests)
            if (Economy.QuestPool[q.index].kind == QuestKind.Runs) Assert.AreEqual(1, q.progress);
    }

    [Test]
    public void BuyUpgradeSelectAndCosmetics()
    {
        Assert.IsTrue(Profile.Owns(WeaponId.M4));
        Assert.IsFalse(Profile.BuyWeapon(WeaponId.Smg), "no gold");
        Profile.Grant(5000, 400, "test");
        Assert.IsTrue(Profile.BuyWeapon(WeaponId.Smg));
        Assert.AreEqual(5000 - Economy.Weapon(WeaponId.Smg).price, Profile.Gold);
        Assert.IsFalse(Profile.BuyWeapon(WeaponId.Smg), "already owned");
        Profile.SelectWeapon(WeaponId.Smg);
        Assert.AreEqual(WeaponId.Smg, Profile.SelectedWeapon);
        Profile.SelectWeapon(WeaponId.Uzi);
        Assert.AreEqual(WeaponId.Smg, Profile.SelectedWeapon, "cannot select a locked weapon");
        int g = Profile.Gold;
        Assert.IsTrue(Profile.BuyUpgrade(WeaponId.Smg, WeaponStat.Damage));
        Assert.AreEqual(1, Profile.Level(WeaponId.Smg, WeaponStat.Damage));
        Assert.AreEqual(g - Economy.UpgradeCost(WeaponId.Smg, WeaponStat.Damage, 0), Profile.Gold);
        Assert.IsTrue(Profile.BuyCostume(CostumeId.Gold), "gems costume");
        Assert.AreEqual(400 - Economy.Costume(CostumeId.Gold).gems, Profile.Gems);
        // Characters cost gems; picking the soldier again restores its last skin.
        Assert.AreEqual(0, Economy.Costume(CostumeId.SpecOps).gold);
        Assert.AreEqual(Economy.CharacterGems, Economy.Costume(CostumeId.SpecOps).gems);
        Assert.AreEqual(CostumeId.Gold, Profile.SoldierSkin);
        Profile.Grant(0, Economy.CharacterGems, "test");
        Assert.IsTrue(Profile.BuyCostume(CostumeId.SpecOps));
        Assert.AreEqual(CostumeId.SpecOps, Profile.SelectedCostume);
        Assert.AreEqual(CostumeId.Gold, Profile.SoldierSkin, "skin kept");
        Profile.SelectCharacter(CostumeId.Standard);
        Assert.AreEqual(CostumeId.Gold, Profile.SelectedCostume, "soldier with its skin");
        Assert.IsFalse(Profile.BuyCostume(CostumeId.Commando), "starter-pack only");
        Assert.IsTrue(Profile.BuyBonus(BonusId.StartCard));
        Assert.IsTrue(Profile.ConsumeStartCard());
        Assert.IsFalse(Profile.ConsumeStartCard());
    }

    [Test]
    public void PurchasesDeliverOnceAndRestore()
    {
        string small = Economy.ProductPrefix + "gems_small", starter = Economy.ProductPrefix + "starter_pack", vip = Economy.ProductPrefix + "vip";
        Assert.IsTrue(Profile.Deliver(small, "t1", false));
        Assert.IsFalse(Profile.Deliver(small, "t1", false), "same transaction twice");
        Assert.AreEqual(70, Profile.Gems);
        Assert.IsTrue(Profile.Deliver(starter, "t2", false));
        Assert.AreEqual(220, Profile.Gems);
        Assert.AreEqual(2000, Profile.Gold);
        Assert.IsTrue(Profile.Owns(CostumeId.Commando));
        Assert.IsTrue(Profile.Deliver(starter, null, true), "restore");
        Assert.AreEqual(220, Profile.Gems, "restore never pays currency again");
        Assert.IsTrue(Profile.Deliver(vip, "t3", false));
        Assert.IsTrue(Profile.Vip);
        Assert.AreEqual(1.25f, Profile.GoldMultiplier, 1e-4f);
        Assert.IsFalse(Profile.Deliver("com.other.app.gems", "t4", false), "unknown product");
    }

    [Test]
    public void SaveIsSignedAndMigrated()
    {
        Profile.Grant(1234, 5, "test");
        var mem = (MemorySaveStore)SaveSystem.Store;
        StringAssert.DoesNotContain("1234", mem.json, "values are not stored in clear text");
        Assert.AreEqual(1234, SaveSystem.Deserialize(mem.json).wallet.gold);
        // Tamper: change one character of the payload → rejected.
        int i = mem.json.IndexOf("\"p\":\"") + 10;
        string bad = mem.json.Substring(0, i) + (mem.json[i] == 'A' ? 'B' : 'A') + mem.json.Substring(i + 1);
        Assert.IsNull(SaveSystem.Deserialize(bad), "tampered save rejected");
        // v1 (plain JSON, before F13) migrates to v2 with the starter items.
        var v1 = SaveSystem.Deserialize("{\"version\":1,\"wallet\":{\"gold\":50}}");
        Assert.AreEqual(SaveSystem.CurrentVersion, v1.version);
        Assert.AreEqual(50, v1.wallet.gold);
        Assert.IsTrue(v1.economy.weapons.Contains("M4"));
        Assert.IsTrue(v1.economy.costumes.Contains("Standard"));
        Assert.IsNull(SaveSystem.Deserialize("{\"version\":2,\"economy\":{\"vip\":true}}"), "unsigned v2 data rejected");
    }
}

public class ScriptFileTests
{
    // Unity binds a serialized MonoBehaviour to a script asset only when the class name matches its file name;
    // otherwise scenes embed a broken MonoScript and the component never initialises (menu panels stuck open).
    [Test]
    public void SceneComponentsLiveInMatchingFiles()
    {
        var files = new HashSet<string>();
        foreach (var f in System.IO.Directory.GetFiles("Assets/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            files.Add(System.IO.Path.GetFileNameWithoutExtension(f));
        var runtimeOnly = new HashSet<string> { "ServiceRunner" };
        foreach (var t in typeof(GameManager).Assembly.GetTypes())
        {
            if (!typeof(MonoBehaviour).IsAssignableFrom(t) || t.IsAbstract || t.IsNested || runtimeOnly.Contains(t.Name)) continue;
            Assert.IsTrue(files.Contains(t.Name), $"{t.Name} must be in {t.Name}.cs");
        }
    }
}

public class ModeTests
{
    [Test]
    public void HardIsHarderEveryWave()
    {
        for (int n = 1; n <= 25; n++)
        {
            var e = Difficulty.ForWave(n, false);
            var h = Difficulty.ForWave(n, true);
            Assert.Greater(h.healthMultiplier, e.healthMultiplier, "health w" + n);
            Assert.Greater(h.contactDamage, e.contactDamage, "damage w" + n);
            Assert.Less(h.spawnInterval, e.spawnInterval, "interval w" + n);
            Assert.GreaterOrEqual(h.maxAlive, e.maxAlive, "alive w" + n);
            Assert.GreaterOrEqual(h.speedMultiplier, e.speedMultiplier, "speed w" + n);
            Assert.GreaterOrEqual(h.specialChance, e.specialChance, "special w" + n);
        }
        Assert.AreEqual(0f, Difficulty.ForWave(1, true).specialChance);
        Assert.Greater(Difficulty.ForWave(2, true).specialChance, 0f, "hard: mutations from wave 2");
    }

    [Test]
    public void BossWavesPerMode()
    {
        Assert.IsTrue(ModeRules.IsBossWave(RunMode.Endless, 6));
        Assert.IsFalse(ModeRules.IsBossWave(RunMode.Endless, 5));
        Assert.IsTrue(ModeRules.IsBossWave(RunMode.Hard, 5));
        Assert.IsTrue(ModeRules.IsBossWave(RunMode.Hard, 9));
        Assert.IsFalse(ModeRules.IsBossWave(RunMode.Hard, 6));
        for (int w = 1; w < 30; w++) Assert.IsFalse(ModeRules.IsBossWave(RunMode.Campaign, w));
        Assert.AreEqual(1, ModeRules.BossIndex(RunMode.Hard, 9));
    }

    [Test]
    public void HardAmmoRules()
    {
        Assert.IsTrue(ModeRules.LimitedAmmo(RunMode.Hard));
        Assert.IsFalse(ModeRules.LimitedAmmo(RunMode.Endless));
        Assert.IsFalse(ModeRules.LimitedAmmo(RunMode.Campaign));
        Assert.IsFalse(ModeRules.BreatherRegen(RunMode.Hard));
        Assert.AreEqual(1f, ModeRules.AmmoDrop(true, 999, 40), "mutations always drop ammo");
        Assert.Greater(ModeRules.AmmoDrop(false, 10, 40), ModeRules.AmmoDrop(false, 100, 40), "pity when low");
        Assert.AreEqual(40 * ModeRules.MaxReserveMags, ModeRules.ReserveCap(40));
        Assert.Greater(ModeRules.GoldMul(RunMode.Hard), 1f);
    }

    [Test]
    public void ChapterPlanRunsInOrderAndFinishes()
    {
        var rng = new System.Random(1);
        for (int c = 0; c < Campaign.Count; c++)
        {
            var def = Campaign.Get(c);
            var plan = new CampaignPlan(def);
            for (int i = 0; i < def.steps.Length; i++)
            {
                Assert.IsFalse(plan.Finished);
                Assert.IsTrue(plan.ShouldIssue(1, -1, false));
                Assert.IsFalse(plan.ShouldIssue(1, -1, true), "one at a time");
                var s = plan.Next(rng);
                Assert.AreEqual(def.steps[i], s.kind);
                if (s.kind == ObjectiveKind.Survive) { Assert.AreEqual(def.surviveSeconds, s.hold); Assert.IsFalse(s.HasSite); }
                if (s.kind == ObjectiveKind.Boss) Assert.IsFalse(s.HasSite);
                if (s.kind == ObjectiveKind.Reach) Assert.IsTrue(CityLayout.IsLandmark(s.site));
                plan.Completed(s);
            }
            Assert.IsTrue(plan.Finished, "chapter " + c);
            Assert.IsFalse(plan.ShouldIssue(1, -1, false));
            Assert.IsTrue(Loc.Has("chapter." + def.key + ".title") && Loc.Has("chapter." + def.key + ".desc"), def.key);
        }
        Assert.IsTrue(Loc.Has("obj.Reach") && Loc.Has("obj.Survive") && Loc.Has("obj.Boss"));
    }

    [Test]
    public void StarsUnlockAndRewards()
    {
        Assert.AreEqual(1, Campaign.Stars(999f, 300f, 0.1f));
        Assert.AreEqual(2, Campaign.Stars(200f, 300f, 0.1f));
        Assert.AreEqual(2, Campaign.Stars(999f, 300f, 0.8f));
        Assert.AreEqual(3, Campaign.Stars(200f, 300f, 0.5f));

        var d = new CampaignData();
        Assert.IsTrue(Campaign.Unlocked(d, 0));
        Assert.IsFalse(Campaign.Unlocked(d, 1));
        Assert.IsTrue(Campaign.Record(d, 0, 2), "first clear");
        Assert.IsTrue(Campaign.Unlocked(d, 1));
        Assert.IsFalse(Campaign.Record(d, 0, 1), "replay");
        Assert.AreEqual(2, Campaign.StarsOf(d, 0), "best stars kept");
        Campaign.Record(d, 0, 3);
        Assert.AreEqual(3, Campaign.TotalStars(d));

        Campaign.Reward(0, true, out int g1, out int gem1);
        Campaign.Reward(0, false, out int g2, out int gem2);
        Assert.Greater(g1, g2); Assert.Greater(gem1, 0); Assert.AreEqual(0, gem2);
        Assert.AreEqual(ObjectiveKind.Helicopter, Campaign.Get(Campaign.Count - 1).steps[Campaign.Get(Campaign.Count - 1).steps.Length - 1], "finale: the chopper");
    }
}


public class CoopTests
{
    [Test]
    public void SnapshotRoundTrip()
    {
        var zs = new ZombieNetState[3];
        for (int i = 0; i < 3; i++)
            zs[i] = new ZombieNetState { id = (ushort)(100 + i), kind = (ZombieKind)i, runner = i == 1, rising = i == 2, pos = new Vector3(1234.5f + i, 0f, -987.25f), yaw = 90f * i, gait = i * 0.9f, hp01 = 0.5f, maxHp = 140f, act = (byte)(i + 1), aux = (byte)(250 + i) };
        var buf = new byte[ZombieSnapshot.Size(3)];
        int len = ZombieSnapshot.Write(buf, new WaveNetState { wave = 7, inBreak = true, timeLeft = 4.3f }, zs, 3);
        Assert.AreEqual(ZombieSnapshot.Size(3) - ZombieSnapshot.BossBytes + 1, len, "no boss: just the flag");
        ZombieNetState[] read = null;
        int n = ZombieSnapshot.Read(buf, len, out var w, ref read);
        Assert.AreEqual(3, n);
        Assert.AreEqual(7, w.wave); Assert.IsTrue(w.inBreak); Assert.AreEqual(4.3f, w.timeLeft, 0.05f);
        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(zs[i].id, read[i].id);
            Assert.AreEqual(zs[i].kind, read[i].kind);
            Assert.AreEqual(zs[i].runner, read[i].runner);
            Assert.AreEqual(zs[i].rising, read[i].rising);
            Assert.AreEqual(zs[i].pos.x, read[i].pos.x, 1e-3f);
            Assert.AreEqual(zs[i].pos.z, read[i].pos.z, 1e-3f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(zs[i].yaw, read[i].yaw), 1.5f);
            Assert.AreEqual(zs[i].gait, read[i].gait, 0.01f);
            Assert.AreEqual(0.5f, read[i].hp01, 0.01f);
            Assert.AreEqual(140f, read[i].maxHp);
            Assert.AreEqual(zs[i].act, read[i].act, "special phase");
            Assert.AreEqual(zs[i].aux, read[i].aux, "target slot / boss type");
        }
        Assert.AreEqual(-1, ZombieSnapshot.Read(buf, 5, out _, ref read), "truncated buffer rejected");
        Assert.Less(ZombieSnapshot.Size(70), 1300, "70 zombies stay small");
    }

    [Test]
    public void LeashKeepsTeammatesTogether()
    {
        var mate = Vector3.zero;
        var at = new Vector3(ModeRules.CoopLeash - 0.1f, 0f, 0f);
        Vector3 outward = Squad.Leash(at, new Vector3(1f, 0f, 0f), mate, ModeRules.CoopLeash);
        Assert.AreEqual(0f, outward.x, 1e-4f, "can't walk further away");
        Vector3 side = Squad.Leash(at, new Vector3(1f, 0f, 1f), mate, ModeRules.CoopLeash);
        Assert.Greater(side.z, 0.9f, "sliding along the leash is fine");
        Vector3 back = Squad.Leash(at, new Vector3(-1f, 0f, 0f), mate, ModeRules.CoopLeash);
        Assert.AreEqual(-1f, back.x, 1e-4f, "walking back always allowed");
        Vector3 free = Squad.Leash(new Vector3(5f, 0f, 0f), new Vector3(1f, 0f, 0f), mate, ModeRules.CoopLeash);
        Assert.AreEqual(1f, free.x, 1e-4f);
    }

    [Test]
    public void BossStateRoundTrip()
    {
        var b = new BossNetState { id = 77, phase = (byte)ButcherZombie.Phase.LeapWind, phaseT = 0.63f, chargeYaw = 135f, chargeLen = 21.4f, windTime = 0.85f,
            leapTo = new Vector3(512.25f, 0f, -33.5f), shockPos = new Vector3(-8f, 0f, 9.75f), shockT = 1.4f, enraged = true };
        var zs = new[] { new ZombieNetState { id = 77, kind = ZombieKind.Boss, pos = new Vector3(500f, 0f, -30f), maxHp = 9000f, hp01 = 0.4f, aux = (byte)BossType.Leaper } };
        var buf = new byte[ZombieSnapshot.Size(1)];
        int len = ZombieSnapshot.Write(buf, new WaveNetState { wave = 11 }, zs, 1, b);
        Assert.AreEqual(ZombieSnapshot.Size(1), len);
        ZombieNetState[] read = null;
        int n = ZombieSnapshot.Read(buf, len, out var w, ref read, out var rb);
        Assert.AreEqual(1, n);
        Assert.IsTrue(rb.HasValue, "boss block read");
        var r = rb.Value;
        Assert.AreEqual(77, r.id); Assert.AreEqual(b.phase, r.phase);
        Assert.AreEqual(0.63f, r.phaseT, 0.011f); Assert.AreEqual(0f, Mathf.DeltaAngle(135f, r.chargeYaw), 1.5f);
        Assert.AreEqual(21.4f, r.chargeLen, 0.21f); Assert.AreEqual(0.85f, r.windTime, 0.021f);
        Assert.AreEqual(512.25f, r.leapTo.x, 1e-3f); Assert.AreEqual(9.75f, r.shockPos.z, 1e-3f);
        Assert.AreEqual(1.4f, r.shockT, 0.021f); Assert.IsTrue(r.enraged);
        Assert.AreEqual((byte)BossType.Leaper, read[0].aux);
        Assert.AreEqual(-1, ZombieSnapshot.Read(buf, len - 3, out _, ref read, out _), "cut boss block rejected");
    }

    [Test]
    public void EventCodecRoundTrip()
    {
        var w = new NetWriter(new byte[4]); // grows
        w.U8((byte)CoopEv.ObjIssue); w.Bool(true); w.U16(65000); w.I32(-123456); w.U64(0xF00DCAFEBEEF1234UL);
        w.F32(3.25f); w.XZ(new Vector3(1f, 99f, -2f)); w.V3(new Vector3(4f, 5f, 6f)); w.Str("radio.flare_brief"); w.Str("");
        var r = new NetReader(w.buf, w.len);
        Assert.AreEqual((byte)CoopEv.ObjIssue, r.U8()); Assert.IsTrue(r.Bool()); Assert.AreEqual(65000, r.U16()); Assert.AreEqual(-123456, r.I32());
        Assert.AreEqual(0xF00DCAFEBEEF1234UL, r.U64()); Assert.AreEqual(3.25f, r.F32());
        var xz = r.XZ(); Assert.AreEqual(1f, xz.x); Assert.AreEqual(0f, xz.y); Assert.AreEqual(-2f, xz.z);
        Assert.AreEqual(new Vector3(4f, 5f, 6f), r.V3()); Assert.AreEqual("radio.flare_brief", r.Str()); Assert.AreEqual("", r.Str());
        Assert.IsTrue(r.Ok);
        r.U8();
        Assert.IsFalse(r.Ok, "reading past the end is flagged");
    }

    [Test]
    public void CoopWavesScaleWithTheTeam()
    {
        for (int n = 1; n <= 20; n++)
        {
            var solo = Difficulty.ForWave(n, false);
            var duo = ModeRules.ForCoop(solo, 2);
            var four = ModeRules.ForCoop(solo, 4);
            Assert.AreEqual(solo.specialChance, duo.specialChance, "C2: mutations in co-op too");
            Assert.GreaterOrEqual(duo.maxAlive, solo.maxAlive); Assert.GreaterOrEqual(four.maxAlive, duo.maxAlive);
            Assert.LessOrEqual(four.maxAlive, ModeRules.CoopAliveCap);
            Assert.Less(four.spawnInterval, solo.spawnInterval);
            Assert.Greater(four.healthMultiplier, solo.healthMultiplier);
            Assert.AreEqual(solo.maxAlive, ModeRules.ForCoop(solo, 1).maxAlive, "alone = endless");
            Assert.AreEqual(BossRules.IsBossWave(n), ModeRules.IsBossWave(RunMode.Coop, n), "co-op bosses like endless");
        }
        Assert.Greater(ModeRules.GoldMul(RunMode.Coop), 1f, "playing together pays a bit more");
    }

    [Test]
    public void StoreReviewOnlyAfterGoodMoments()
    {
        Assert.IsFalse(StoreReview.ShouldAsk(10, false, 100, -1), "not after a bad run");
        Assert.IsFalse(StoreReview.ShouldAsk(StoreReview.MinRuns - 1, true, 100, -1), "not for a brand new player");
        Assert.IsTrue(StoreReview.ShouldAsk(StoreReview.MinRuns, true, 100, -1));
        Assert.IsFalse(StoreReview.ShouldAsk(20, true, 100 + StoreReview.MinDaysBetween - 1, 100), "not again too soon");
        Assert.IsTrue(StoreReview.ShouldAsk(20, true, 100 + StoreReview.MinDaysBetween, 100));
    }

    [Test]
    public void AimAssistIsSoftAndConeLimited()
    {
        Vector3 f = Vector3.forward;
        Vector3 at4 = Quaternion.Euler(0f, 4f, 0f) * f, at7 = Quaternion.Euler(0f, 7f, 0f) * f, at20 = Quaternion.Euler(0f, 20f, 0f) * f;
        float drag = Vector3.SignedAngle(f, AimAssist.Bend(f, at4, AimAssistConfig.DragCone, AimAssistConfig.DragPull), Vector3.up);
        Assert.AreEqual(4f * AimAssistConfig.DragPull, drag, 0.01f, "drag: a small nudge, not a snap");
        Assert.AreEqual(0f, Vector3.Angle(f, AimAssist.Bend(f, at7, AimAssistConfig.DragCone, AimAssistConfig.DragPull)), 0.01f, "drag: outside ±5° nothing");
        float hold = Vector3.SignedAngle(f, AimAssist.Bend(f, at7, AimAssistConfig.HoldCone, AimAssistConfig.HoldPull), Vector3.up);
        Assert.Less(hold, 7f, "hold: no full lock"); Assert.Greater(hold, 0f, "hold: soft magnet inside ±8°");
        Assert.AreEqual(0f, Vector3.Angle(f, AimAssist.Bend(f, at20, AimAssistConfig.HoldCone, AimAssistConfig.HoldPull)), 0.01f, "hold: outside ±8° nothing");
        Assert.LessOrEqual(AimAssistConfig.DragCone, AimAssistConfig.HoldCone);
    }

    [Test]
    public void ReviveAndTeamOverRules()
    {
        float p = 0f;
        for (int i = 0; i < 30; i++) p = CoopTeam.Rules.StepRevive(p, true, 0.1f, 3f);
        Assert.AreEqual(1f, p, 1e-4f, "3 s next to a teammate");
        p = CoopTeam.Rules.StepRevive(0.5f, false, 1f, 3f);
        Assert.Less(p, 0.5f, "progress fades when the helper leaves"); Assert.Greater(p, 0f);
        Assert.AreEqual(10f, CoopTeam.Rules.StepBleed(10f, true, 1f), "no bleeding while being revived");
        Assert.AreEqual(9f, CoopTeam.Rules.StepBleed(10f, false, 1f));
        Assert.IsFalse(CoopTeam.Rules.TeamDown(false, new[] { true, true }, 2), "I'm standing");
        Assert.IsFalse(CoopTeam.Rules.TeamDown(true, new[] { true, false }, 2), "a teammate stands");
        Assert.IsTrue(CoopTeam.Rules.TeamDown(true, new[] { true, true }, 2), "nobody stands");
        Assert.IsTrue(CoopTeam.Rules.TeamDown(true, new bool[0], 0), "alone and down");
        Assert.AreEqual(600, CoopTeam.Rules.TeamScore(100, new[] { 200, 300 }, 2));
    }

    [Test]
    public void SnapshotPartsAndSequence()
    {
        // A split snapshot: part 2 carries zombies 56.. with no boss block, each part under the MTU.
        var zs = new ZombieNetState[70];
        for (int i = 0; i < zs.Length; i++) zs[i] = new ZombieNetState { id = (ushort)(100 + i), kind = ZombieKind.Walker, pos = new Vector3(i, 0f, -i), maxHp = 60f, hp01 = 1f };
        var buf = new byte[ZombieSnapshot.Size(NetZombies.PerPart)];
        int len = ZombieSnapshot.Write(buf, new WaveNetState { wave = 9 }, zs, NetZombies.PerPart, zs.Length - NetZombies.PerPart, null);
        Assert.Less(ZombieSnapshot.Size(NetZombies.PerPart) + 12, 1296 - 64, "a full part stays under Netcode's non-fragmented limit");
        var outS = new ZombieNetState[4];
        int n = ZombieSnapshot.Read(buf, len, out var w, ref outS, out var boss);
        Assert.AreEqual(14, n);
        Assert.AreEqual(9, w.wave);
        Assert.IsFalse(boss.HasValue);
        Assert.AreEqual(100 + NetZombies.PerPart, outS[0].id);
        Assert.AreEqual(NetZombies.PerPart, outS[0].pos.x, 0.01f);

        Assert.IsTrue(NetZombies.SeqNewer(5, 4));
        Assert.IsFalse(NetZombies.SeqNewer(4, 5));
        Assert.IsTrue(NetZombies.SeqNewer(2, 65530), "wraps");
        Assert.IsFalse(NetZombies.SeqNewer(65530, 2));
    }

}


public class MutantBossTests
{
    [Test]
    public void BossesComeInOrderEachTougher()
    {
        Assert.AreEqual(BossType.Butcher, BossRules.TypeFor(0));
        Assert.AreEqual(BossType.Leaper, BossRules.TypeFor(1));
        Assert.AreEqual(BossType.Hive, BossRules.TypeFor(2));
        Assert.AreEqual(BossType.Crusher, BossRules.TypeFor(3));
        Assert.AreEqual(BossType.Butcher, BossRules.TypeFor(4), "cycle restarts");
        var wp = Difficulty.ForWave(10, false);
        for (int i = 1; i < BossRules.TypeCount; i++)
        {
            Assert.Greater(BossRules.TypeScale[i], BossRules.TypeScale[i - 1], "each mutation is bigger");
            Assert.Greater(BossRules.Health(70f, wp, 0, (BossType)i), BossRules.Health(70f, wp, 0, (BossType)(i - 1)), "each mutation is tougher");
        }
        Assert.Greater(BossRules.Health(70f, wp, 4), BossRules.Health(70f, wp, 0), "second Kasap is stronger than the first");
        for (int i = 0; i < BossRules.TypeCount; i++)
        {
            string k = BossRules.NameKey((BossType)i);
            Assert.IsTrue(Loc.Has(k) && Loc.Has(k + ".sub") && Loc.Has("radio." + k), k);
        }
    }

    [Test]
    public void CampaignMeetsEveryMutationInOrder()
    {
        var seen = new List<BossType>();
        for (int c = 0; c < Campaign.Count; c++)
        {
            var d = Campaign.Get(c);
            foreach (var s in d.steps) if (s == ObjectiveKind.Boss) seen.Add(d.boss);
        }
        CollectionAssert.AreEqual(new[] { BossType.Butcher, BossType.Leaper, BossType.Hive, BossType.Crusher }, seen);
    }
}

public class PerformanceTests
{
    [Test]
    public void PoolHeadroomCoversAliveCapAndCorpses()
    {
        Assert.GreaterOrEqual(ZombieSpawner.PoolTarget(70), 90);
        Assert.GreaterOrEqual(ZombieSpawner.PoolTarget(Difficulty.ForWave(10, false).maxAlive), Difficulty.ForWave(10, false).maxAlive + 15);
        Assert.LessOrEqual(ZombieSpawner.PoolTarget(500), 130);
    }

    [Test]
    public void HitStopIsRateLimited()
    {
        Assert.IsTrue(GameManager.HitStopAllowed(1f, 0.012f, 0.9f, 0.9f));
        Assert.IsFalse(GameManager.HitStopAllowed(1f, 0.012f, 1.1f, 0f), "small stop waits for the gap");
        Assert.IsTrue(GameManager.HitStopAllowed(1f, 0.06f, 1.1f, 0.9f), "a kill isn't blocked by a hit's gap");
        Assert.IsFalse(GameManager.HitStopAllowed(1f, 0.06f, 0f, 1.2f));
    }

    [Test]
    public void ResolutionAndThermalSteps()
    {
        Assert.AreEqual(1f, Settings.ResolutionCapFor(828));
        Assert.AreEqual(1f, Settings.ResolutionCapFor(Settings.MaxRenderLines));
        Assert.AreEqual(Settings.MaxRenderLines / 2064f, Settings.ResolutionCapFor(2064), 1e-4f);
        Assert.AreEqual(0, PerfGovernor.ThermalFpsCap(0));
        Assert.AreEqual(0, PerfGovernor.ThermalFpsCap(1));
        Assert.AreEqual(60, PerfGovernor.ThermalFpsCap(2));
        Assert.AreEqual(30, PerfGovernor.ThermalFpsCap(3));
        Assert.Greater(PerfGovernor.ThermalScale(1), PerfGovernor.ThermalScale(2));
        Assert.Greater(PerfGovernor.ThermalScale(2), PerfGovernor.ThermalScale(3));
        Assert.Greater(PerfGovernor.LoadScale(0), PerfGovernor.LoadScale(1));
        Assert.Greater(PerfGovernor.LoadScale(1), PerfGovernor.LoadScale(2));
    }
}

public class Cards110Tests
{
    [Test]
    public void NewCardsFormulasGroupsAndText()
    {
        Assert.AreEqual(22, Upgrades.Cards.Length);
        for (int i = 0; i < Upgrades.Cards.Length; i++) Assert.AreEqual((CardId)i, Upgrades.Cards[i].id, "Cards[] in CardId order");
        foreach (var c in Upgrades.Cards)
        {
            Assert.IsTrue(Loc.Has("card." + c.id + ".title"), c.id + " title");
            for (int l = 1; l <= c.maxLevel; l++) Assert.IsTrue(Loc.Has("card." + c.id + "." + l), c.id + " level " + l);
        }
        foreach (Synergy sy in Enum.GetValues(typeof(Synergy)))
        {
            Assert.IsTrue(Loc.Has("syn." + sy) && Loc.Has("syn." + sy + ".desc"), sy.ToString());
            Assert.AreEqual(1, Array.FindAll(Upgrades.SynergyPairs, x => x.s == sy).Length, "one pair per synergy");
        }
        Assert.AreEqual(Upgrades.Group.Build, Upgrades.GroupOf(CardId.Mines));
        Assert.AreEqual(Upgrades.Group.Build, Upgrades.GroupOf(CardId.Drone));
        Assert.AreEqual(Upgrades.Group.Damage, Upgrades.GroupOf(CardId.Shock));
        Assert.AreEqual(Upgrades.Group.Utility, Upgrades.GroupOf(CardId.Frost));
        Assert.AreEqual(Upgrades.Group.Utility, Upgrades.GroupOf(CardId.Armor));
        Assert.IsTrue(Upgrades.IsDamageCard(CardId.Ricochet));

        for (int l = 1; l < 3; l++)
        {
            Assert.Greater(Upgrades.RicochetDamageMul(l + 1), Upgrades.RicochetDamageMul(l));
            Assert.Greater(Upgrades.ShockTargets(l + 1), Upgrades.ShockTargets(l));
            Assert.Less(Upgrades.FrostSlow(l + 1), Upgrades.FrostSlow(l), "more frost = slower");
            Assert.Greater(Upgrades.MaxMines(l + 1), Upgrades.MaxMines(l));
            Assert.Greater(Upgrades.DroneRate(l + 1), Upgrades.DroneRate(l));
            Assert.Greater(Upgrades.AdrenalineRate(l + 1), Upgrades.AdrenalineRate(l));
            Assert.AreEqual(l, Upgrades.ArmorCharges(l));
        }
        Assert.AreEqual(1f, Upgrades.FrostSlow(0));
        Assert.Greater(Upgrades.FrostSlow(3), 0.4f, "never a full freeze");
        Assert.AreEqual(2, Upgrades.DroneCount(3));

        // Offers still mix families: three distinct cards, never all from one family more than rarely.
        var levels = new int[Upgrades.Cards.Length];
        var into = new List<CardId>(); var scratch = new List<int>();
        var rng = new System.Random(3);
        int sameFamily = 0;
        for (int k = 0; k < 400; k++)
        {
            Upgrades.Offer(levels, 3, () => (float)rng.NextDouble(), into, scratch);
            Assert.AreEqual(3, into.Count);
            if (Upgrades.GroupOf(into[0]) == Upgrades.GroupOf(into[1]) && Upgrades.GroupOf(into[1]) == Upgrades.GroupOf(into[2])) sameFamily++;
        }
        Assert.Less(sameFamily, 20);
    }
}

public class CloudMergeTests
{
    static SaveData Save(long at, int gold, int gems, bool synced, params string[] weapons)
    {
        var d = new SaveData { version = SaveSystem.CurrentVersion, savedAt = at, cloudSynced = synced };
        d.wallet.gold = gold; d.wallet.gems = gems;
        foreach (var w in weapons) if (!d.economy.weapons.Contains(w)) d.economy.weapons.Add(w);
        return d;
    }

    [Test]
    public void FreshInstallNeverWipesTheCloud()
    {
        var cloud = Save(100, 5000, 300, true, "Smg", "Uzi");
        cloud.economy.costumes.Add("Hush"); cloud.economy.vip = true;
        cloud.economy.levels.Add(new WeaponLevels { id = "Smg", damage = 3 });
        cloud.campaign.stars.AddRange(new[] { 3, 2 });
        cloud.records.bestScore = 9000;
        var fresh = Save(500, 40, 0, false);           // newer, but a fresh install
        fresh.settings.lang = "en";
        var m = SaveMerge.Merge(fresh, cloud);
        Assert.AreEqual(5000, m.wallet.gold, "first sync: higher wallet");
        Assert.AreEqual(300, m.wallet.gems);
        CollectionAssert.IsSupersetOf(m.economy.weapons, new[] { "M4", "Smg", "Uzi" });
        CollectionAssert.Contains(m.economy.costumes, "Hush");
        Assert.IsTrue(m.economy.vip);
        Assert.AreEqual(3, m.economy.levels.Find(l => l.id == "Smg").damage);
        CollectionAssert.AreEqual(new[] { 3, 2 }, m.campaign.stars);
        Assert.AreEqual(9000, m.records.bestScore);
        Assert.AreEqual("en", m.settings.lang, "settings stay with the device");
        Assert.IsTrue(m.cloudSynced);
    }

    [Test]
    public void AfterSyncNewerWalletWinsButOwnershipUnites()
    {
        var local = Save(900, 100, 5, true, "Smg");     // spent gold after the last sync
        var cloud = Save(800, 2000, 50, true, "Uzi");
        cloud.economy.gunSkins.Add("Gold");
        local.campaign.stars.AddRange(new[] { 1, 3, 2 });
        cloud.campaign.stars.AddRange(new[] { 3, 1 });
        var m = SaveMerge.Merge(local, cloud);
        Assert.AreEqual(100, m.wallet.gold, "newer save's wallet (no duplicate currency)");
        Assert.AreEqual(5, m.wallet.gems);
        CollectionAssert.IsSupersetOf(m.economy.weapons, new[] { "Smg", "Uzi" });
        CollectionAssert.Contains(m.economy.gunSkins, "Gold");
        CollectionAssert.AreEqual(new[] { 3, 3, 2 }, m.campaign.stars, "stars: max per chapter");
        Assert.AreEqual(900, m.savedAt);
        Assert.AreSame(local, SaveMerge.Merge(local, null));
    }
}

public class World110Tests
{
    [Test]
    public void WeatherCycleAndRain()
    {
        Assert.AreEqual(DayPhase.Day, Weather.PhaseFor(1));
        Assert.AreEqual(DayPhase.Day, Weather.PhaseFor(5));
        Assert.AreEqual(DayPhase.Dusk, Weather.PhaseFor(6));
        Assert.AreEqual(DayPhase.Night, Weather.PhaseFor(11));
        Assert.AreEqual(DayPhase.Day, Weather.PhaseFor(16), "cycles back to day");
        int rain = 0;
        for (int w = 1; w <= 400; w++)
        {
            bool r = Weather.RainFor(w, 12345);
            if (w < 3) Assert.IsFalse(r, "no rain in the first waves");
            if (r) { rain++; Assert.IsFalse(Weather.RainFor(w - 1, 12345), "never two rainy waves in a row"); }
            Assert.AreEqual(r, Weather.RainFor(w, 12345), "deterministic (co-op peers agree)");
        }
        Assert.That(rain, Is.InRange(40, 120), "about a fifth of the waves");
        var day = new DistrictMood { sky = Color.white, equator = Color.white, ground = Color.white, light = Color.white, lightIntensity = 1f, fog = Color.white, fogDensity = 0.01f };
        var night = Weather.Tint(day, new WeatherState { phase = DayPhase.Night });
        Assert.Less(night.lightIntensity, 0.5f);
        Assert.Less(night.sky.r, day.sky.r);
        var wet = Weather.Tint(day, new WeatherState { phase = DayPhase.Day, rain = true });
        Assert.Greater(wet.fogDensity, day.fogDensity);
    }

    [Test]
    public void LandmarksAndSetPiecesAppearSometimes()
    {
        int lm = 0, sp = 0, n = 0;
        for (int x = -40; x < 40; x++)
            for (int y = -40; y < 40; y++)
            {
                var t = CityLayout.NaturalType(777, new Vector2Int(x, y), District.Liman);
                n++;
                if (t == ChunkType.Landmark) lm++;
                if (t == ChunkType.Special) sp++;
            }
        Assert.That(lm / (float)n, Is.InRange(0.05f, 0.11f));
        Assert.That(sp / (float)n, Is.InRange(0.025f, 0.07f));
        Assert.AreEqual(ChunkType.Block, CityLayout.NaturalType(777, Vector2Int.zero, District.Liman), "spawn stays plain");
    }
}

public class MetaEventsTests
{
    [Test]
    public void KeysAndRunParams()
    {
        Assert.AreEqual("1109686598492645", MetaEvents.AppId);
        Assert.AreEqual(32, MetaEvents.ClientToken.Length);
        Assert.AreEqual("{\"fb_level\":\"9\",\"mode\":\"Hard\"}", MetaEvents.RunParams(RunMode.Hard, 9));
        Assert.AreEqual("{\"fb_level\":\"0\",\"mode\":\"Endless\"}", MetaEvents.RunParams(RunMode.Endless, -3));
        // Not started (editor / no ATT answer yet): logging is a safe no-op.
        Assert.IsFalse(MetaEvents.Started);
        MetaEvents.TutorialCompleted();
        MetaEvents.RunFinished(RunMode.Endless, 4);
    }
}
