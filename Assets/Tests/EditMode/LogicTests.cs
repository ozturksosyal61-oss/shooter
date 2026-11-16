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
        Assert.AreEqual(Difficulty.BaseInterval, w1.spawnInterval, 1e-4f);
        Assert.AreEqual(Difficulty.BaseAlive, w1.maxAlive);
        Assert.AreEqual(1f, w1.healthMultiplier, 1e-4f);
        Assert.AreEqual(Difficulty.BaseDamage, w1.contactDamage, 1e-4f);
        Assert.AreEqual(0f, w1.runnerChance, 1e-4f);

        var w5 = Difficulty.ForWave(5);
        Assert.AreEqual(Difficulty.BaseAlive + 4 * Difficulty.AlivePerWave, w5.maxAlive);
        Assert.AreEqual(Difficulty.BaseDamage + 4 * Difficulty.DamagePerWave, w5.contactDamage, 1e-4f);
        Assert.AreEqual(Difficulty.BaseInterval * Mathf.Pow(Difficulty.IntervalDecay, 4), w5.spawnInterval, 1e-4f);
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
        Assert.AreEqual(15, ids.Length);
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
        Assert.AreEqual(2, Upgrades.GuaranteedDamagePicks);
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
        Assert.AreEqual("Son Devriye", Loc.T("game.title"));
        Assert.AreEqual("Son Devriye: Zombi Avı", Loc.T("game.store_title"));
        Assert.AreEqual("ÇİFT MERMİ", Loc.Upper("Çift Mermi"));
        Assert.AreEqual("KIRIK", Loc.Upper("kırık"));

        int changed = 0;
        Action h = () => changed++;
        Loc.Changed += h;
        Loc.Current = Lang.En;
        Loc.Changed -= h;
        Assert.AreEqual(1, changed);
        Assert.AreEqual("WAVE 3", Loc.F("hud.wave", 3));
        Assert.AreEqual("Last Patrol: Zombie Shooter", Loc.T("game.store_title"));
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
    public void EndlessPlanCyclesThreeThenHelicopter()
    {
        var plan = new EndlessPlan();
        var rng = new System.Random(3);
        for (int cycle = 0; cycle < 2; cycle++)
        {
            var seen = new HashSet<ObjectiveKind>();
            for (int i = 0; i < 3; i++)
            {
                Assert.IsFalse(plan.HelicopterDue);
                var s = plan.Next(rng);
                Assert.AreNotEqual(ObjectiveKind.Helicopter, s.kind);
                Assert.IsTrue(seen.Add(s.kind), "each of flare / tower / supply once per cycle");
                plan.Completed(s);
            }
            Assert.IsTrue(plan.HelicopterDue);
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
