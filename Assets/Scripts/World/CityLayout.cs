using System.Collections.Generic;
using UnityEngine;

/// <summary>What a 20x20 m chunk's interior holds. Landmarks and objective sites are rare / placed on demand.</summary>
public enum ChunkType
{
    Block,          // building or props (the classic city block)
    Park,           // grass, trees, benches, low fences
    Plaza,          // tiled square with a statue or a fountain
    Alley,          // narrow lane between two rows of buildings
    Rubble,         // collapsed block: debris, rocks, wrecks, cracked asphalt
    GasStation,     // landmark: canopy + pumps
    PoliceStation,  // landmark: "karakol" with police cars
    Hospital,       // landmark: hospital entrance with an ambulance
    SignalTower,    // objective site (placed on demand)
    Helipad,        // objective site (placed on demand)
    Landmark,       // 1.1.0: the district's signature structure (crane, smokestacks, domed han, metro entrance…)
    Special,        // 1.1.0: rare set piece (crashed helicopter, military checkpoint, burning block)
}

public enum Edge { S = 0, N = 1, W = 2, E = 3 }

/// <summary>
/// Pure, seeded city layout (no Unity objects — EditMode-testable and identical on every peer for a given seed).
/// Roads run along chunk borders. Vertical lines (x = i·20) are endless avenues (every even line plus some odd
/// ones); horizontal streets only on some rows, and a row can skip a whole span between two avenues (T-junctions).
/// So many chunks have no intersection at all (long straight avenues), every road continues into its neighbour,
/// and there are no dead ends.
/// </summary>
public static class CityLayout
{
    public const float Size = 20f;
    public const float RoadHalf = 3.5f;
    public const float Sidewalk = 5.5f;

    public static float H(int seed, int a, int b, int salt)
    {
        unchecked
        {
            int h = seed * 73856093 ^ a * 19349663 ^ b * 83492791 ^ salt * 2654435;
            h ^= h >> 13; h *= 1274126177; h ^= h >> 16; h *= 668265263; h ^= h >> 15;
            return (h & 0xffffff) / 16777216f;
        }
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    /// <summary>Is there an avenue along the vertical line x = i·Size?</summary>
    public static bool AvenueLine(int seed, int i) => Mod(i, 2) == 0 || H(seed, i, 0, 0x51) < 0.3f;

    /// <summary>Is the horizontal line z = j·Size a street row?</summary>
    public static bool StreetRow(int seed, int j) => Mod(j, 3) == 0 || H(seed, 0, j, 0x77) < 0.12f;

    /// <summary>Road on the horizontal edge from x = i to i+1 at z = j (in chunk units)?</summary>
    public static bool RoadH(int seed, int i, int j)
    {
        if (!StreetRow(seed, j)) return false;
        if (j == 0) return true; // the spawn row always connects
        int a = i;
        while (!AvenueLine(seed, a)) a--; // span start (≤ 1 step: every even line is an avenue)
        return H(seed, a, j, 0x99) >= 0.22f;
    }

    public static bool RoadV(int seed, int i, int j) => AvenueLine(seed, i);

    public static bool HasRoad(int seed, Vector2Int c, Edge e)
    {
        switch (e)
        {
            case Edge.S: return RoadH(seed, c.x, c.y);
            case Edge.N: return RoadH(seed, c.x, c.y + 1);
            case Edge.W: return RoadV(seed, c.x, c.y);
            default: return RoadV(seed, c.x + 1, c.y);
        }
    }

    /// <summary>Road mask of a chunk: bit per <see cref="Edge"/>.</summary>
    public static int Mask(int seed, Vector2Int c)
    {
        int m = 0;
        for (int e = 0; e < 4; e++) if (HasRoad(seed, c, (Edge)e)) m |= 1 << e;
        return m;
    }

    /// <summary>Road segments meeting at grid node (i, j): up/down (vertical), left/right (horizontal).</summary>
    public static void Node(int seed, int i, int j, out bool up, out bool down, out bool left, out bool right)
    {
        up = RoadV(seed, i, j); down = RoadV(seed, i, j - 1);
        left = RoadH(seed, i - 1, j); right = RoadH(seed, i, j);
    }

    public static int NodeDegree(int seed, int i, int j)
    {
        Node(seed, i, j, out bool u, out bool d, out bool l, out bool r);
        return (u ? 1 : 0) + (d ? 1 : 0) + (l ? 1 : 0) + (r ? 1 : 0);
    }

    /// <summary>A node where the road turns or branches (so it gets crossings / traffic lights).</summary>
    public static bool IsJunction(int seed, int i, int j)
    {
        Node(seed, i, j, out bool u, out bool d, out bool l, out bool r);
        return (u || d) && (l || r);
    }

    // ------------------------------------------------------------------ chunk types
    /// <summary>Relative weights per district: Block, Park, Plaza, Alley, Rubble, GasStation, PoliceStation, Hospital.</summary>
    public static readonly float[][] TypeWeights =
    {
        new[] { 50f, 12f, 10f, 10f, 8f, 3f, 3f, 4f },   // Cadde (warm main street)
        new[] { 44f, 6f, 18f, 20f, 7f, 2f, 2f, 1f },    // Çarşı (bazaar)
        new[] { 52f, 4f, 4f, 8f, 22f, 6f, 2f, 2f },     // Sanayi (industrial)
        new[] { 60f, 3f, 4f, 4f, 22f, 4f, 2f, 1f },     // Liman (harbour)
    };

    public static bool IsLandmark(ChunkType t) => t == ChunkType.GasStation || t == ChunkType.PoliceStation || t == ChunkType.Hospital;
    public static bool IsObjectiveSite(ChunkType t) => t == ChunkType.SignalTower || t == ChunkType.Helipad;

    /// <summary>1.1.0: share of chunks that become the district's signature structure / a rare set piece.</summary>
    public const float LandmarkChance = 0.08f, SpecialChance = 0.045f;

    /// <summary>Alleys need open (road-less) south and north edges: the lane runs west–east between two building rows.</summary>
    public static bool AlleyFits(int mask) => (mask & ((1 << (int)Edge.S) | (1 << (int)Edge.N))) == 0;

    /// <summary>Natural type of a chunk (no overrides). The 2x2 chunks around the spawn are always plain blocks.</summary>
    public static ChunkType NaturalType(int seed, Vector2Int c, District d)
    {
        if (c.x >= -1 && c.x <= 0 && c.y >= -1 && c.y <= 0) return ChunkType.Block;
        int mask = Mask(seed, c);
        // Rare set pieces first, then the district's signature structures (both own rolls: the old type table and
        // so the old layout of every other chunk stays the same).
        if (H(seed, c.x, c.y, 0x5EC1) < SpecialChance) return ChunkType.Special;
        if (H(seed, c.x, c.y, 0x1A4D) < LandmarkChance) return ChunkType.Landmark;
        var w = TypeWeights[(int)d];
        float sum = 0f;
        for (int i = 0; i < w.Length; i++) sum += w[i];
        float r = H(seed, c.x, c.y, 0x1234) * sum;
        var t = ChunkType.Block;
        for (int i = 0; i < w.Length; i++)
        {
            if (r < w[i]) { t = (ChunkType)i; break; }
            r -= w[i];
        }
        if (t == ChunkType.Alley && !AlleyFits(mask)) t = ChunkType.Block;
        if (IsLandmark(t) && mask == 0) t = ChunkType.Block; // landmarks face a road
        return t;
    }

    /// <summary>Local point (in the chunk) where an objective item / zone sits for a site chunk type.</summary>
    public static Vector2 SiteLocal(ChunkType t)
    {
        switch (t)
        {
            case ChunkType.PoliceStation:
            case ChunkType.Hospital: return new Vector2(10f, 6.6f);
            case ChunkType.GasStation: return new Vector2(10f, 7.2f);
            default: return new Vector2(10f, 10f);
        }
    }

    public static Vector3 ChunkOrigin(Vector2Int c) => new Vector3(c.x * Size, 0f, c.y * Size);

    /// <summary>Global position of a site in chunk <paramref name="c"/>.</summary>
    public static Vector3 SiteGlobal(Vector2Int c, ChunkType t)
    {
        var l = SiteLocal(t);
        return ChunkOrigin(c) + new Vector3(l.x, 0f, l.y);
    }

    /// <summary>Chebyshev rings around <paramref name="from"/> from <paramref name="minR"/> to <paramref name="maxR"/>.</summary>
    public static IEnumerable<Vector2Int> Ring(Vector2Int from, int r)
    {
        if (r == 0) { yield return from; yield break; }
        for (int dx = -r; dx <= r; dx++) { yield return new Vector2Int(from.x + dx, from.y - r); yield return new Vector2Int(from.x + dx, from.y + r); }
        for (int dz = -r + 1; dz <= r - 1; dz++) { yield return new Vector2Int(from.x - r, from.y + dz); yield return new Vector2Int(from.x + r, from.y + dz); }
    }
}
