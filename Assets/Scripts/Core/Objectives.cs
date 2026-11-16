using System.Collections.Generic;
using UnityEngine;

public enum ObjectiveKind { Flare, Tower, Supply, Helicopter }

/// <summary>One story objective: what, where (site chunk type) and how long the player has to hold the zone.</summary>
public struct ObjectiveStep
{
    public ObjectiveKind kind;
    public ChunkType site;
    public float radius;
    /// <summary>Seconds to stand in the zone (0 = touch to complete).</summary>
    public float hold;
    /// <summary>Loc key of the radio briefing ({0} = direction, {1} = hold seconds).</summary>
    public string briefKey;
}

/// <summary>
/// Pure objective rules (statically testable, no scene objects): schedule, step definitions, rewards and the radio
/// direction words. <see cref="ObjectiveDirector"/> owns the state; campaign levels can feed their own
/// <see cref="IObjectivePlan"/> instead of the endless one.
/// </summary>
public static class Objectives
{
    public const int FirstWave = 2;
    public const int EveryWaves = 3;
    public const int PerHelicopter = 3;

    public const float FlareRadius = 1.8f;
    public const float SupplyRadius = 2.4f, SupplyHold = 2f;
    public const float TowerRadius = 6f, TowerHold = 20f;
    public const float HeliRadius = 7.5f, HeliHold = 30f;
    /// <summary>The helicopter shows up this long before the hold completes.</summary>
    public const float HeliArriveBefore = 9f;
    /// <summary>Spawn interval multiplier while the player is holding a tower / landing zone.</summary>
    public const float HoldPressure = 0.75f;
    /// <summary>Supply crate: full heal below this health fraction, otherwise a card pick.</summary>
    public const float SupplyHealBelow = 0.6f;

    /// <summary>Endless mode: a new objective every <see cref="EveryWaves"/> waves (from <see cref="FirstWave"/>), never two at once.</summary>
    public static bool ShouldIssue(int wave, int lastIssuedWave, bool hasActive)
    {
        if (hasActive || wave < FirstWave) return false;
        return lastIssuedWave < 0 || wave - lastIssuedWave >= EveryWaves;
    }

    public static ObjectiveStep Step(ObjectiveKind k, System.Random rng)
    {
        switch (k)
        {
            case ObjectiveKind.Flare:
                return new ObjectiveStep { kind = k, site = ChunkType.PoliceStation, radius = FlareRadius, hold = 0f, briefKey = "radio.flare" };
            case ObjectiveKind.Tower:
                return new ObjectiveStep { kind = k, site = ChunkType.SignalTower, radius = TowerRadius, hold = TowerHold, briefKey = "radio.tower" };
            case ObjectiveKind.Supply:
                bool hospital = rng == null || rng.NextDouble() < 0.5;
                return new ObjectiveStep
                {
                    kind = k, site = hospital ? ChunkType.Hospital : ChunkType.GasStation, radius = SupplyRadius, hold = SupplyHold,
                    briefKey = hospital ? "radio.supply_hospital" : "radio.supply_gas",
                };
            default:
                return new ObjectiveStep { kind = k, site = ChunkType.Helipad, radius = HeliRadius, hold = HeliHold, briefKey = "radio.heli" };
        }
    }

    /// <summary>Bonus score and XP for completing an objective during wave <paramref name="wave"/>.</summary>
    public static void Reward(ObjectiveKind k, int wave, int helicopters, out int score, out int xp)
    {
        if (k == ObjectiveKind.Helicopter)
        {
            score = 1000 + 500 * helicopters + 50 * wave;
            xp = 60 + 8 * wave;
            return;
        }
        score = 250 + 25 * wave;
        xp = 20 + 3 * wave;
    }

    public static bool SupplyGivesHeal(float health01) => health01 < SupplyHealBelow;

    static readonly string[] DirKeys = { "dir.e", "dir.ne", "dir.n", "dir.nw", "dir.w", "dir.sw", "dir.s", "dir.se" };

    /// <summary>Compass word for a ground-plane offset (+z = north = screen up).</summary>
    public static string DirectionKey(Vector2 delta)
    {
        if (delta.sqrMagnitude < 0.01f) return "dir.here";
        float a = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        int i = Mathf.RoundToInt(Mathf.Repeat(a, 360f) / 45f) % 8;
        return DirKeys[i];
    }
}

/// <summary>Source of objectives: the endless generator now, scripted campaign chapters later.</summary>
public interface IObjectivePlan
{
    bool ShouldIssue(int wave, int lastIssuedWave, bool hasActive);
    ObjectiveStep Next(System.Random rng);
    void Completed(ObjectiveStep s);
    /// <summary>Plan finished (campaign chapter won). Endless never finishes.</summary>
    bool Finished { get; }
}

/// <summary>
/// Endless mode: flare / tower / supply in a shuffled bag, one every 3 waves; after 3 of them the helicopter landing
/// zone appears right away; after the helicopter the cycle starts over.
/// </summary>
public class EndlessPlan : IObjectivePlan
{
    readonly List<ObjectiveKind> bag = new List<ObjectiveKind>(3);
    public int DoneInCycle { get; private set; }
    public int Helicopters { get; private set; }
    public bool HelicopterDue => DoneInCycle >= Objectives.PerHelicopter;
    public bool Finished => false;

    public bool ShouldIssue(int wave, int lastIssuedWave, bool hasActive) =>
        !hasActive && (HelicopterDue || Objectives.ShouldIssue(wave, lastIssuedWave, false));

    public ObjectiveStep Next(System.Random rng)
    {
        if (HelicopterDue) return Objectives.Step(ObjectiveKind.Helicopter, rng);
        if (bag.Count == 0)
        {
            bag.Add(ObjectiveKind.Flare); bag.Add(ObjectiveKind.Tower); bag.Add(ObjectiveKind.Supply);
            for (int i = bag.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (bag[i], bag[j]) = (bag[j], bag[i]); }
        }
        var k = bag[bag.Count - 1];
        bag.RemoveAt(bag.Count - 1);
        return Objectives.Step(k, rng);
    }

    public void Completed(ObjectiveStep s)
    {
        if (s.kind == ObjectiveKind.Helicopter) { DoneInCycle = 0; Helicopters++; }
        else DoneInCycle++;
    }
}
