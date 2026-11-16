using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public enum District { Cadde, Carsi, Sanayi, Liman }

[Serializable]
public class DistrictSettings
{
    public District district;
    public GameObject[] buildings;
    [Range(0f, 1f)] public float buildingChance;
    public GameObject[] blockProps;
    public int blockPropMin = 1, blockPropMax = 3;
    public GameObject[] sidewalkProps;
}

/// <summary>Prefabs for the non-block chunk types, landmarks and street furniture.</summary>
[Serializable]
public class CityKit
{
    public GameObject[] trees, benches, fences, planters, lamps, rocks, lowWalls, debris, wrecks, ruins, alleyBuildings, trafficLights, statues;
    public GameObject fountain, busStop, gasCanopy, gasSign, policeBuilding, policeCar, hospitalBuilding, hospitalSign, ambulance;
    public GameObject signalTower, helipad, floodLight, generator, dumpster, crate;
}

/// <summary>Ground texture atlas layout (shared with the builder that paints it).</summary>
public static class GroundAtlas
{
    public const int Grid = 4, CellPx = 256, Gutter = 8, AtlasPx = Grid * CellPx;
    public const float TileM = 5f;
    public const int Strip = 0, StripZebra = 1, StripCracked = 2, Corner = 3, CornerNode = 4,
        Cadde = 5, Carsi = 6, Sanayi = 7, Liman = 8, Park = 9, Plaza = 10, Rubble = 11, Alley = 12, Lot = 13,
        CornerCracked = 14, NodeCracked = 15;

    public static Vector2 UV(int cell, float u, float v)
    {
        int cx = cell % Grid, cy = cell / Grid;
        const float inner = CellPx - 2 * Gutter;
        return new Vector2((cx * CellPx + Gutter + u * inner) / AtlasPx, (cy * CellPx + Gutter + v * inner) / AtlasPx);
    }
}

/// <summary>
/// Infinite seeded city made of 20x20 m chunks. Roads run along chunk borders following <see cref="CityLayout"/>
/// (long avenues without crossings, T-junctions, every road continues into the neighbour). Each chunk has a type
/// (block, park, plaza, alley, rubble, landmark or objective site); its ground is one mesh into a shared atlas
/// (one draw call per chunk). The 5x5 chunks around the player are kept alive; everything is pooled.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    public const float Size = CityLayout.Size;
    const float SidewalkOuter = CityLayout.Sidewalk;

    public static ChunkManager I { get; private set; }
    static readonly ProfilerMarker BuildMarker = new ProfilerMarker(ProfilerCategory.Scripts, "Game.ChunkBuild");

    public Transform player;
    public int radius = 2;
    public bool randomSeed = true;
    public int seed = 12345;

    public Material groundMaterial;
    public DistrictSettings[] districts;
    public CityKit kit;
    public GameObject[] roadCars;
    public GameObject[] roadBarricades;
    public GameObject[] burningBarrels;
    [Tooltip("Red barrels that explode when shot.")]
    public GameObject[] explosiveBarrels;
    [Range(0f, 1f)] public float explosiveBarrelShare = 0.6f;
    [Tooltip("Chance per chunk of an extra explosive barrel on the sidewalk.")]
    [Range(0f, 1f)] public float sidewalkBarrelChance = 0.3f;
    [Range(0f, 1f)] public float busStopChance = 0.12f;
    public GameObject[] roadDecor;

    class Chunk
    {
        public Vector2Int coord;
        public ChunkType type;
        public Transform root;
        public MeshRenderer ground;
        public Mesh mesh;
        public readonly List<GameObject> items = new List<GameObject>();
    }

    struct Placement { public Vector2 pos; public float r; }

    readonly Dictionary<Vector2Int, Chunk> active = new Dictionary<Vector2Int, Chunk>();
    readonly Dictionary<Vector2Int, ChunkType> overrides = new Dictionary<Vector2Int, ChunkType>();
    readonly Stack<Chunk> freeChunks = new Stack<Chunk>();
    readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
    readonly Dictionary<GameObject, GameObject> prefabOf = new Dictionary<GameObject, GameObject>();
    readonly List<Placement> placements = new List<Placement>(48);
    readonly List<Vector2Int> toRemove = new List<Vector2Int>(16);
    readonly List<Vector3> gVerts = new List<Vector3>(256);
    readonly List<Vector2> gUvs = new List<Vector2>(256);
    readonly List<int> gTris = new List<int>(384);
    readonly List<Vector3> gNormals = new List<Vector3>(256);
    Transform poolRoot;
    Vector2Int center = new Vector2Int(int.MinValue, int.MinValue);
    Vector2 noiseOffset;

    public int ActiveChunkCount => active.Count;

    /// <summary>Raised after a chunk was (re)built: coord, type.</summary>
    public event Action<Vector2Int, ChunkType> ChunkBuilt;

    /// <summary>Non-zero: forces this seed on the next load (tests/bots compare runs on the same city).</summary>
    public static int SeedOverride;

    void Awake()
    {
        I = this;
        if (SeedOverride != 0) seed = SeedOverride;
        else if (randomSeed) seed = UnityEngine.Random.Range(1, 1_000_000);
        var r = new System.Random(seed);
        noiseOffset = new Vector2((float)r.NextDouble() * 1000f, (float)r.NextDouble() * 1000f);
        poolRoot = new GameObject("EnvPool").transform;
        poolRoot.SetParent(transform, false);
        poolRoot.gameObject.SetActive(false);
    }

    void Start() => UpdateChunks();

    void Update() => UpdateChunks();

    public static Vector2Int ChunkOf(Vector3 localPos)
    {
        Vector3 g = FloatingOrigin.ToGlobal(localPos);
        return new Vector2Int(Mathf.FloorToInt(g.x / Size), Mathf.FloorToInt(g.z / Size));
    }

    public District DistrictAt(Vector2Int c)
    {
        float n = Mathf.PerlinNoise(c.x * 0.11f + noiseOffset.x, c.y * 0.11f + noiseOffset.y);
        if (n < 0.4f) return District.Cadde;
        if (n < 0.5f) return District.Carsi;
        if (n < 0.6f) return District.Sanayi;
        return District.Liman;
    }

    public District DistrictAtPosition(Vector3 localPos) => DistrictAt(ChunkOf(localPos));

    public ChunkType TypeAt(Vector2Int c) => overrides.TryGetValue(c, out var t) ? t : CityLayout.NaturalType(seed, c, DistrictAt(c));

    public bool IsActive(Vector2Int c) => active.ContainsKey(c);

    /// <summary>Makes chunk <paramref name="c"/> a given type from now on (objective sites); rebuilds it if it is live.</summary>
    public void ForceType(Vector2Int c, ChunkType t)
    {
        overrides[c] = t;
        if (active.TryGetValue(c, out var ch) && ch.type != t)
        {
            ReleaseChunk(ch);
            active[c] = BuildChunk(c);
            Physics.SyncTransforms();
        }
    }

    /// <summary>Nearest chunk of type <paramref name="t"/> within the Chebyshev ring [minR, maxR] of <paramref name="from"/>.</summary>
    public bool FindType(ChunkType t, Vector2Int from, int minR, int maxR, out Vector2Int found)
    {
        for (int r = minR; r <= maxR; r++)
            foreach (var c in CityLayout.Ring(from, r))
                if (TypeAt(c) == t) { found = c; return true; }
        found = default;
        return false;
    }

    void UpdateChunks()
    {
        if (!player) return;
        var c = ChunkOf(player.position);
        if (c == center) return;
        center = c;
        using var _ = BuildMarker.Auto();

        toRemove.Clear();
        foreach (var kv in active)
            if (Mathf.Abs(kv.Key.x - c.x) > radius || Mathf.Abs(kv.Key.y - c.y) > radius) toRemove.Add(kv.Key);
        foreach (var k in toRemove) { ReleaseChunk(active[k]); active.Remove(k); }

        for (int dz = -radius; dz <= radius; dz++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                var k = new Vector2Int(c.x + dx, c.y + dz);
                if (!active.ContainsKey(k)) active.Add(k, BuildChunk(k));
            }
        Physics.SyncTransforms();
    }

    // ------------------------------------------------------------------ chunk lifecycle
    Chunk BuildChunk(Vector2Int coord)
    {
        var ch = freeChunks.Count > 0 ? freeChunks.Pop() : CreateChunkShell();
        ch.coord = coord;
        ch.root.localPosition = new Vector3(coord.x * Size, 0f, coord.y * Size);
        ch.root.gameObject.SetActive(true);

        var rng = new System.Random(Hash(seed, coord.x, coord.y));
        var district = DistrictAt(coord);
        var ds = districts[(int)district];
        int mask = CityLayout.Mask(seed, coord);
        ch.type = TypeAt(coord);
#if UNITY_EDITOR
        ch.root.name = $"Chunk {coord.x},{coord.y} {ch.type}";
#endif
        placements.Clear();
        BuildGround(ch, mask, district);

        switch (ch.type)
        {
            case ChunkType.Park: BuildPark(ch, rng, mask); break;
            case ChunkType.Plaza: BuildPlaza(ch, rng, district); break;
            case ChunkType.Alley: BuildAlley(ch, rng); break;
            case ChunkType.Rubble: BuildRubble(ch, rng, mask, district); break;
            case ChunkType.GasStation: BuildGasStation(ch, rng); break;
            case ChunkType.PoliceStation: BuildPoliceStation(ch, rng); break;
            case ChunkType.Hospital: BuildHospital(ch, rng); break;
            case ChunkType.SignalTower: BuildSignalTower(ch, rng); break;
            case ChunkType.Helipad: BuildHelipad(ch, rng); break;
            default: BuildBlock(ch, rng, ds); break;
        }

        bool site = CityLayout.IsObjectiveSite(ch.type);
        if (!site) RoadObstacles(ch, rng, mask);
        if (ch.type == ChunkType.Block || ch.type == ChunkType.Rubble) SidewalkProps(ch, rng, mask, ds);
        if ((ch.type == ChunkType.Block || ch.type == ChunkType.Park || ch.type == ChunkType.Plaza) && rng.NextDouble() < busStopChance) BusStop(ch, rng, mask);
        TrafficLights(ch, rng, mask);
        if (!site && ch.type != ChunkType.Alley) RoadDecor(ch, rng, mask);
        ChunkBuilt?.Invoke(coord, ch.type);
        return ch;
    }

    Chunk CreateChunkShell()
    {
        var ch = new Chunk();
        var go = new GameObject("Chunk");
        ch.root = go.transform;
        ch.root.SetParent(transform, false);
        var g = new GameObject("Ground");
        g.transform.SetParent(ch.root, false);
        ch.mesh = new Mesh { name = "ChunkGround" };
        ch.mesh.MarkDynamic();
        g.AddComponent<MeshFilter>().sharedMesh = ch.mesh;
        ch.ground = g.AddComponent<MeshRenderer>();
        ch.ground.sharedMaterial = groundMaterial;
        ch.ground.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var box = g.AddComponent<BoxCollider>();
        box.center = new Vector3(Size * 0.5f, -0.1f, Size * 0.5f);
        box.size = new Vector3(Size, 0.2f, Size);
        return ch;
    }

    void ReleaseChunk(Chunk ch)
    {
        foreach (var go in ch.items)
        {
            go.transform.SetParent(poolRoot, false);
            pools[prefabOf[go]].Push(go);
        }
        ch.items.Clear();
        ch.root.gameObject.SetActive(false);
        freeChunks.Push(ch);
    }

    // ------------------------------------------------------------------ ground mesh
    static int InteriorCell(ChunkType t, District d)
    {
        switch (t)
        {
            case ChunkType.Park: return GroundAtlas.Park;
            case ChunkType.Plaza: return GroundAtlas.Plaza;
            case ChunkType.Rubble: return GroundAtlas.Rubble;
            case ChunkType.Alley: return GroundAtlas.Alley;
            case ChunkType.Block: return GroundAtlas.Cadde + (int)d;
            default: return GroundAtlas.Lot;
        }
    }

    void BuildGround(Chunk ch, int mask, District d)
    {
        gVerts.Clear(); gUvs.Clear(); gTris.Clear();
        bool cracked = ch.type == ChunkType.Rubble;
        int interior = InteriorCell(ch.type, d);
        int stripCell = cracked ? GroundAtlas.StripCracked : GroundAtlas.Strip;
        const float A = SidewalkOuter, B = Size - SidewalkOuter;
        bool Has(Edge e) => (mask & (1 << (int)e)) != 0;

        Tiled(A, A, B, B, interior);
        // Edge middles.
        if (Has(Edge.S)) StripRun(Edge.S, A, B, stripCell, -1f); else Tiled(A, 0f, B, A, interior);
        if (Has(Edge.N)) StripRun(Edge.N, A, B, stripCell, -1f); else Tiled(A, B, B, Size, interior);
        if (Has(Edge.W)) StripRun(Edge.W, A, B, stripCell, -1f); else Tiled(0f, A, A, B, interior);
        if (Has(Edge.E)) StripRun(Edge.E, A, B, stripCell, -1f); else Tiled(B, A, Size, B, interior);
        // Corners.
        for (int cz = 0; cz < 2; cz++)
            for (int cx = 0; cx < 2; cx++)
            {
                Edge ea = cx == 0 ? Edge.W : Edge.E, eb = cz == 0 ? Edge.S : Edge.N;
                bool ha = Has(ea), hb = Has(eb);
                int ni = ch.coord.x + cx, nj = ch.coord.y + cz;
                float x0 = cx == 0 ? 0f : B, z0 = cz == 0 ? 0f : B;
                if (ha && hb) CornerQuad(cx, cz, cracked ? GroundAtlas.CornerCracked : GroundAtlas.Corner);
                else if (ha || hb)
                {
                    bool junction = CityLayout.IsJunction(seed, ni, nj);
                    int cell = junction && !cracked ? GroundAtlas.StripZebra : stripCell;
                    if (ha) StripRun(ea, z0, z0 + A, cell, cz == 0 ? 0f : Size);
                    else StripRun(eb, x0, x0 + A, cell, cx == 0 ? 0f : Size);
                }
                else if (CityLayout.NodeDegree(seed, ni, nj) > 0) CornerQuad(cx, cz, cracked ? GroundAtlas.NodeCracked : GroundAtlas.CornerNode);
                else Tiled(x0, z0, x0 + A, z0 + A, interior);
            }

        var m = ch.mesh;
        m.Clear();
        m.SetVertices(gVerts);
        if (gNormals.Count < gVerts.Count) while (gNormals.Count < gVerts.Count) gNormals.Add(Vector3.up);
        m.SetNormals(gNormals, 0, gVerts.Count);
        m.SetUVs(0, gUvs);
        m.SetTriangles(gTris, 0, false);
        m.bounds = new Bounds(new Vector3(Size * 0.5f, 0f, Size * 0.5f), new Vector3(Size, 0.2f, Size));
    }

    void Quad(float x0, float z0, float x1, float z1, Vector2 uv00, Vector2 uv01, Vector2 uv11, Vector2 uv10)
    {
        int b = gVerts.Count;
        gVerts.Add(new Vector3(x0, 0f, z0)); gUvs.Add(uv00);
        gVerts.Add(new Vector3(x0, 0f, z1)); gUvs.Add(uv01);
        gVerts.Add(new Vector3(x1, 0f, z1)); gUvs.Add(uv11);
        gVerts.Add(new Vector3(x1, 0f, z0)); gUvs.Add(uv10);
        gTris.Add(b); gTris.Add(b + 1); gTris.Add(b + 2);
        gTris.Add(b); gTris.Add(b + 2); gTris.Add(b + 3);
    }

    /// <summary>Next 5 m grid line after <paramref name="a"/> (capped at <paramref name="end"/>).</summary>
    static float NextLine(float a, float end) => Mathf.Min(end, (Mathf.Floor(a / GroundAtlas.TileM + 1e-4f) + 1f) * GroundAtlas.TileM);
    static float Frac(float a, float cellStart) => (a - cellStart) / GroundAtlas.TileM;
    static float CellStart(float a) => Mathf.Floor(a / GroundAtlas.TileM + 1e-4f) * GroundAtlas.TileM;

    void Tiled(float x0, float z0, float x1, float z1, int cell)
    {
        for (float xa = x0; xa < x1 - 1e-3f;)
        {
            float xb = NextLine(xa, x1), xs = CellStart(xa);
            float u0 = Frac(xa, xs), u1 = Frac(xb, xs);
            for (float za = z0; za < z1 - 1e-3f;)
            {
                float zb = NextLine(za, z1), zs = CellStart(za);
                float v0 = Frac(za, zs), v1 = Frac(zb, zs);
                Quad(xa, za, xb, zb, GroundAtlas.UV(cell, u0, v0), GroundAtlas.UV(cell, u0, v1), GroundAtlas.UV(cell, u1, v1), GroundAtlas.UV(cell, u1, v0));
                za = zb;
            }
            xa = xb;
        }
    }

    /// <summary>
    /// Road half + curb + sidewalk along an edge, from along <paramref name="a0"/> to <paramref name="a1"/>. Cell u runs
    /// along the road (5 m period), v across from the road centre (0) to the sidewalk's inner edge (1).
    /// <paramref name="nodeAt"/> = 0 or Size flips u so node-relative markings (crossings) face that node.
    /// </summary>
    void StripRun(Edge e, float a0, float a1, int cell, float nodeAt)
    {
        for (float aa = a0; aa < a1 - 1e-3f;)
        {
            float ab = NextLine(aa, a1), s = CellStart(aa);
            float u0 = Frac(aa, s), u1 = Frac(ab, s);
            if (nodeAt > 1f) { u0 = 1f - u0; u1 = 1f - u1; }
            const float W = SidewalkOuter;
            switch (e)
            {
                case Edge.S: Quad(aa, 0f, ab, W, GroundAtlas.UV(cell, u0, 0f), GroundAtlas.UV(cell, u0, 1f), GroundAtlas.UV(cell, u1, 1f), GroundAtlas.UV(cell, u1, 0f)); break;
                case Edge.N: Quad(aa, Size - W, ab, Size, GroundAtlas.UV(cell, u0, 1f), GroundAtlas.UV(cell, u0, 0f), GroundAtlas.UV(cell, u1, 0f), GroundAtlas.UV(cell, u1, 1f)); break;
                case Edge.W: Quad(0f, aa, W, ab, GroundAtlas.UV(cell, u0, 0f), GroundAtlas.UV(cell, u1, 0f), GroundAtlas.UV(cell, u1, 1f), GroundAtlas.UV(cell, u0, 1f)); break;
                default: Quad(Size - W, aa, Size, ab, GroundAtlas.UV(cell, u0, 1f), GroundAtlas.UV(cell, u1, 1f), GroundAtlas.UV(cell, u1, 0f), GroundAtlas.UV(cell, u0, 0f)); break;
            }
            aa = ab;
        }
    }

    /// <summary>Corner square: u = distance from the vertical chunk edge, v = from the horizontal one (both /5.5).</summary>
    void CornerQuad(int cx, int cz, int cell)
    {
        const float W = SidewalkOuter;
        float x0 = cx == 0 ? 0f : Size - W, z0 = cz == 0 ? 0f : Size - W;
        float ua = cx == 0 ? 0f : 1f, ub = 1f - ua, va = cz == 0 ? 0f : 1f, vb = 1f - va;
        Quad(x0, z0, x0 + W, z0 + W, GroundAtlas.UV(cell, ua, va), GroundAtlas.UV(cell, ua, vb), GroundAtlas.UV(cell, ub, vb), GroundAtlas.UV(cell, ub, va));
    }

    // ------------------------------------------------------------------ chunk types
    void BuildBlock(Chunk ch, System.Random rng, DistrictSettings ds)
    {
        if (ds.buildings != null && ds.buildings.Length > 0 && rng.NextDouble() < ds.buildingChance)
        {
            var b = Pick(ds.buildings, rng);
            Place(ch, b, new Vector2(Size * 0.5f, Size * 0.5f), rng.Next(4) * 90f, 4.5f);
        }
        else if (ds.blockProps != null && ds.blockProps.Length > 0)
        {
            int n = rng.Next(ds.blockPropMin, ds.blockPropMax + 1);
            for (int i = 0; i < n; i++)
            {
                var p = Pick(ds.blockProps, rng);
                float r = RadiusOf(p);
                float lo = SidewalkOuter + Mathf.Min(r, 3f), hi = Size - lo;
                TryPlace(ch, p, rng, () => new Vector2(Range(rng, lo, hi), Range(rng, lo, hi)), () => rng.Next(4) * 90f + Range(rng, -10f, 10f));
            }
        }
    }

    /// <summary>Interior bounds: up to the sidewalk where there is a road, nearly to the border where there is none.</summary>
    static void Inner(int mask, float margin, out float x0, out float z0, out float x1, out float z1)
    {
        x0 = (mask & (1 << (int)Edge.W)) != 0 ? SidewalkOuter + margin : 2f + margin;
        x1 = (mask & (1 << (int)Edge.E)) != 0 ? Size - SidewalkOuter - margin : Size - 2f - margin;
        z0 = (mask & (1 << (int)Edge.S)) != 0 ? SidewalkOuter + margin : 2f + margin;
        z1 = (mask & (1 << (int)Edge.N)) != 0 ? Size - SidewalkOuter - margin : Size - 2f - margin;
    }

    void BuildPark(Chunk ch, System.Random rng, int mask)
    {
        Inner(mask, 0.6f, out float x0, out float z0, out float x1, out float z1);
        int trees = rng.Next(4, 8);
        for (int i = 0; i < trees; i++)
            TryPlace(ch, Pick(kit.trees, rng), rng, () => new Vector2(Range(rng, x0, x1), Range(rng, z0, z1)), () => Range(rng, 0f, 360f));
        int benches = rng.Next(2, 4);
        for (int i = 0; i < benches; i++)
            TryPlace(ch, Pick(kit.benches, rng), rng, () => new Vector2(Range(rng, x0, x1), Range(rng, z0, z1)), () => rng.Next(4) * 90f);
        int planters = rng.Next(0, 3);
        for (int i = 0; i < planters; i++)
            TryPlace(ch, Pick(kit.planters, rng), rng, () => new Vector2(Range(rng, x0, x1), Range(rng, z0, z1)), () => rng.Next(4) * 90f);
        for (int i = 0; i < 2; i++)
            TryPlace(ch, Pick(kit.lamps, rng), rng, () => new Vector2(Range(rng, x0, x1), Range(rng, z0, z1)), () => 0f);
        // Low fence just inside the sidewalk on road sides, with a gap in the middle.
        if (kit.fences != null && kit.fences.Length > 0)
            for (int e = 0; e < 4; e++)
            {
                if ((mask & (1 << e)) == 0) continue;
                var f = Pick(kit.fences, rng);
                foreach (float along in FenceSpots)
                {
                    float across = SidewalkOuter + 0.35f;
                    Vector2 p = EdgePoint((Edge)e, along, across);
                    Spawn(ch, f, p, e < 2 ? 0f : 90f);
                }
            }
    }

    static readonly float[] FenceSpots = { 6.6f, 8.4f, 11.6f, 13.4f };

    void BuildPlaza(Chunk ch, System.Random rng, District d)
    {
        var c = new Vector2(Size * 0.5f, Size * 0.5f);
        bool statue = kit.statues != null && kit.statues.Length > 0 && (rng.NextDouble() < 0.5 || !kit.fountain);
        if (statue) Place(ch, Pick(kit.statues, rng), c, rng.Next(4) * 90f, 1.6f);
        else Place(ch, kit.fountain, c, 0f, 2.6f);
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f + 45f;
            var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
            Place(ch, Pick(kit.benches, rng), c + dir * 4.4f, -a + 90f, 0.7f);
            var axis = new Vector2(Mathf.Cos(i * Mathf.PI * 0.5f), Mathf.Sin(i * Mathf.PI * 0.5f));
            Spawn(ch, Pick(kit.lamps, rng), c + axis * 5.2f, i * 90f);
        }
        if (d == District.Carsi && districts[(int)d].blockProps != null)
            for (int i = 0; i < 2; i++)
            {
                var p = Pick(districts[(int)d].blockProps, rng);
                TryPlace(ch, p, rng, () => new Vector2(Range(rng, 6.2f, 13.8f), Range(rng, 6.2f, 13.8f)), () => rng.Next(4) * 90f);
            }
    }

    /// <summary>Narrow west–east lane between two rows of buildings (only where S and N have no road).</summary>
    void BuildAlley(Chunk ch, System.Random rng)
    {
        for (int row = 0; row < 2; row++)
        {
            float z = row == 0 ? 4f : 16f;
            for (int col = 0; col < 2; col++)
            {
                if (rng.NextDouble() < 0.12) continue; // a gap now and then
                Place(ch, Pick(kit.alleyBuildings, rng), new Vector2(col == 0 ? 5f : 15f, z), row == 0 ? 0f : 180f, 3.4f);
            }
        }
        // Clutter along the walls of the lane (keeps a ~3 m passage).
        int n = rng.Next(2, 5);
        for (int i = 0; i < n; i++)
        {
            double r = rng.NextDouble();
            if (r < 0.35) { Spawn(ch, Pick(kit.debris, rng), new Vector2(Range(rng, 1f, 19f), Range(rng, 8.4f, 11.6f)), Range(rng, 0f, 360f)); continue; }
            var p = r < 0.6 ? kit.crate : r < 0.85 ? Pick(burningBarrels, rng) : Pick(explosiveBarrels, rng);
            float zz = rng.Next(2) == 0 ? 11.3f : 8.7f;
            TryPlace(ch, p, rng, () => new Vector2(Range(rng, 2f, 18f), zz), () => Range(rng, -10f, 10f));
        }
        if (kit.dumpster) TryPlace(ch, kit.dumpster, rng, () => new Vector2(Range(rng, 3f, 17f), rng.Next(2) == 0 ? 8.9f : 11.1f), () => 0f);
    }

    void BuildRubble(Chunk ch, System.Random rng, int mask, District d)
    {
        Inner(mask, 0.4f, out float x0, out float z0, out float x1, out float z1);
        if (kit.ruins != null && kit.ruins.Length > 0 && rng.NextDouble() < 0.5)
            Place(ch, Pick(kit.ruins, rng), new Vector2(Size * 0.5f + Range(rng, -1f, 1f), Size * 0.5f + Range(rng, -1f, 1f)), rng.Next(4) * 90f + Range(rng, -8f, 8f), 4.2f);
        int rocks = rng.Next(1, 4);
        for (int i = 0; i < rocks; i++) TryPlace(ch, Pick(kit.rocks, rng), rng, () => new Vector2(Range(rng, x0, x1), Range(rng, z0, z1)), () => Range(rng, 0f, 360f));
        int walls = rng.Next(1, 3);
        for (int i = 0; i < walls; i++) TryPlace(ch, Pick(kit.lowWalls, rng), rng, () => new Vector2(Range(rng, x0, x1), Range(rng, z0, z1)), () => Range(rng, 0f, 180f));
        int wrecks = rng.Next(0, 3);
        for (int i = 0; i < wrecks; i++) TryPlace(ch, Pick(kit.wrecks, rng), rng, () => new Vector2(Range(rng, x0, x1), Range(rng, z0, z1)), () => Range(rng, 0f, 360f));
        if (rng.NextDouble() < 0.4) TryPlace(ch, Pick(burningBarrels, rng), rng, () => new Vector2(Range(rng, x0, x1), Range(rng, z0, z1)), () => 0f);
        if ((d == District.Sanayi || d == District.Liman) && rng.NextDouble() < 0.35)
            TryPlace(ch, Pick(explosiveBarrels, rng), rng, () => new Vector2(Range(rng, x0, x1), Range(rng, z0, z1)), () => 0f);
        int deb = rng.Next(4, 8);
        for (int i = 0; i < deb; i++) Spawn(ch, Pick(kit.debris, rng), new Vector2(Range(rng, 1f, 19f), Range(rng, 1f, 19f)), Range(rng, 0f, 360f));
    }

    void BuildGasStation(Chunk ch, System.Random rng)
    {
        Place(ch, kit.gasCanopy, new Vector2(10f, 11.4f), 0f, 3.6f);
        bool left = rng.Next(2) == 0;
        Place(ch, kit.gasSign, new Vector2(left ? 6.4f : 13.6f, 6.6f), 0f, 0.5f);
        Place(ch, Pick(explosiveBarrels, rng), new Vector2(left ? 13.8f : 6.2f, 6.4f), 0f, 0.7f);
        Place(ch, Pick(explosiveBarrels, rng), new Vector2(14.2f, 14.6f), 0f, 0.7f);
        if (rng.NextDouble() < 0.7) Place(ch, Pick(kit.wrecks, rng), new Vector2(left ? 13.2f : 6.8f, 9.2f), 90f + Range(rng, -15f, 15f), 2f);
    }

    void BuildPoliceStation(Chunk ch, System.Random rng)
    {
        Place(ch, kit.policeBuilding, new Vector2(10f, 12.4f), 0f, 3.4f);
        Place(ch, kit.policeCar, new Vector2(6.1f, 7.8f), 80f + Range(rng, -8f, 8f), 1.8f);
        Place(ch, kit.policeCar, new Vector2(13.9f, 7.4f), -70f + Range(rng, -8f, 8f), 1.8f);
        var bar = Pick(roadBarricades, rng);
        Place(ch, bar, new Vector2(7.2f, 4.6f), 0f, 0.6f);
        Place(ch, bar, new Vector2(12.8f, 4.6f), 0f, 0.6f);
        if (kit.floodLight) Spawn(ch, kit.floodLight, new Vector2(15.2f, 15.2f), -135f);
    }

    void BuildHospital(Chunk ch, System.Random rng)
    {
        Place(ch, kit.hospitalBuilding, new Vector2(10f, 12.4f), 0f, 3.4f);
        Place(ch, kit.hospitalSign, new Vector2(13.6f, 8.6f), 0f, 0.4f);
        Place(ch, kit.ambulance, new Vector2(6.2f, 8.2f), 90f + Range(rng, -10f, 10f), 2f);
        Place(ch, Pick(roadBarricades, rng), new Vector2(13.2f, 4.8f), Range(rng, -10f, 10f), 0.6f);
        if (kit.crate) Place(ch, kit.crate, new Vector2(14.6f, 6.2f), 15f, 0.6f);
    }

    void BuildSignalTower(Chunk ch, System.Random rng)
    {
        Place(ch, kit.signalTower, new Vector2(10f, 10f), 0f, 1.2f);
        if (kit.generator) Place(ch, kit.generator, new Vector2(12.4f, 9.2f), 0f, 0.8f);
        if (kit.floodLight) Spawn(ch, kit.floodLight, new Vector2(6.8f, 6.8f), 45f);
        // Some cover on the edge of the hold zone.
        for (int i = 0; i < 3; i++)
        {
            float a = (i * 120f + Range(rng, -20f, 20f)) * Mathf.Deg2Rad;
            var p = new Vector2(10f + Mathf.Cos(a) * 6.8f, 10f + Mathf.Sin(a) * 6.8f);
            Place(ch, Pick(roadBarricades, rng), p, -a * Mathf.Rad2Deg + 90f, 0.6f);
        }
    }

    void BuildHelipad(Chunk ch, System.Random rng)
    {
        if (kit.helipad) Spawn(ch, kit.helipad, new Vector2(10f, 10f), 0f);
        if (kit.floodLight)
        {
            Spawn(ch, kit.floodLight, new Vector2(4.6f, 4.6f), 45f);
            Spawn(ch, kit.floodLight, new Vector2(15.4f, 15.4f), -135f);
        }
    }

    // ------------------------------------------------------------------ streets
    static Vector2 EdgePoint(Edge e, float along, float across)
    {
        switch (e)
        {
            case Edge.S: return new Vector2(along, across);
            case Edge.N: return new Vector2(along, Size - across);
            case Edge.W: return new Vector2(across, along);
            default: return new Vector2(Size - across, along);
        }
    }

    int PickRoadEdge(System.Random rng, int mask)
    {
        int n = 0;
        for (int e = 0; e < 4; e++) if ((mask & (1 << e)) != 0) n++;
        if (n == 0) return -1;
        int k = rng.Next(n);
        for (int e = 0; e < 4; e++) if ((mask & (1 << e)) != 0 && k-- == 0) return e;
        return -1;
    }

    /// <summary>True when neither end node of edge <paramref name="e"/> is a junction (a straight stretch of avenue).</summary>
    bool StraightEdge(Vector2Int c, Edge e)
    {
        int i0, j0, i1, j1;
        if (e == Edge.S || e == Edge.N) { int j = c.y + (e == Edge.N ? 1 : 0); i0 = c.x; i1 = c.x + 1; j0 = j1 = j; }
        else { int i = c.x + (e == Edge.E ? 1 : 0); j0 = c.y; j1 = c.y + 1; i0 = i1 = i; }
        return !CityLayout.IsJunction(seed, i0, j0) && !CityLayout.IsJunction(seed, i1, j1);
    }

    /// <summary>Cars along the lane, barricades across it, burning / explosive barrels — only on edges that have a road.</summary>
    void RoadObstacles(Chunk ch, System.Random rng, int mask)
    {
        if (mask == 0) return;
        int roadCount = rng.Next(1, 4);
        for (int i = 0; i < roadCount; i++)
        {
            int band = PickRoadEdge(rng, mask);
            var e = (Edge)band;
            bool alongZ = e == Edge.W || e == Edge.E;
            float lo = StraightEdge(ch.coord, e) ? 2.5f : 6f, hi = Size - lo;
            float across = 1.75f;
            double roll = rng.NextDouble();
            GameObject prefab;
            float yaw;
            if (roll < 0.55) { prefab = Pick(roadCars, rng); yaw = (alongZ ? 0f : 90f) + (rng.Next(2) * 180f) + Range(rng, -20f, 20f); }
            else if (roll < 0.8) { prefab = Pick(roadBarricades, rng); yaw = (alongZ ? 90f : 0f) + Range(rng, -10f, 10f); }
            else
            {
                bool boom = explosiveBarrels != null && explosiveBarrels.Length > 0 && rng.NextDouble() < explosiveBarrelShare;
                prefab = boom ? Pick(explosiveBarrels, rng) : Pick(burningBarrels, rng);
                yaw = Range(rng, 0f, 360f);
                across = Range(rng, 0.6f, 3f);
            }
            float a = across;
            TryPlace(ch, prefab, rng, () => EdgePoint(e, Range(rng, lo, hi), a), () => yaw);
        }
    }

    void SidewalkProps(Chunk ch, System.Random rng, int mask, DistrictSettings ds)
    {
        if (mask == 0) return;
        if (ds.sidewalkProps != null && ds.sidewalkProps.Length > 0 && rng.NextDouble() < 0.6)
        {
            int n = rng.Next(1, 3);
            for (int i = 0; i < n; i++)
            {
                var p = Pick(ds.sidewalkProps, rng);
                var e = (Edge)PickRoadEdge(rng, mask);
                TryPlace(ch, p, rng, () => EdgePoint(e, Range(rng, 6f, 14f), 4.6f), () => Range(rng, 0f, 360f));
            }
        }
        if (explosiveBarrels != null && explosiveBarrels.Length > 0 && rng.NextDouble() < sidewalkBarrelChance)
        {
            var p = Pick(explosiveBarrels, rng);
            var e = (Edge)PickRoadEdge(rng, mask);
            TryPlace(ch, p, rng, () => EdgePoint(e, Range(rng, 5f, 15f), 4.6f), () => Range(rng, 0f, 360f));
        }
    }

    /// <summary>Bus shelter on the sidewalk, open side facing the road.</summary>
    void BusStop(Chunk ch, System.Random rng, int mask)
    {
        if (!kit.busStop) return;
        int band = PickRoadEdge(rng, mask);
        if (band < 0) return;
        var e = (Edge)band;
        float yaw = e == Edge.S ? 0f : e == Edge.N ? 180f : e == Edge.W ? 90f : -90f;
        float along = Range(rng, 8f, 12f);
        TryPlace(ch, kit.busStop, rng, () => EdgePoint(e, along, 4.65f), () => yaw);
    }

    void TrafficLights(Chunk ch, System.Random rng, int mask)
    {
        if (kit.trafficLights == null || kit.trafficLights.Length == 0) return;
        for (int cz = 0; cz < 2; cz++)
            for (int cx = 0; cx < 2; cx++)
            {
                Edge ea = cx == 0 ? Edge.W : Edge.E, eb = cz == 0 ? Edge.S : Edge.N;
                if ((mask & (1 << (int)ea)) == 0 || (mask & (1 << (int)eb)) == 0) continue;
                if (rng.NextDouble() > 0.55) continue;
                var p = new Vector2(cx == 0 ? 4.5f : Size - 4.5f, cz == 0 ? 4.5f : Size - 4.5f);
                float yaw = cx == 0 ? (cz == 0 ? 225f : 315f) : (cz == 0 ? 135f : 45f);
                Spawn(ch, Pick(kit.trafficLights, rng), p, yaw);
            }
    }

    void RoadDecor(Chunk ch, System.Random rng, int mask)
    {
        if (roadDecor == null || roadDecor.Length == 0 || mask == 0) return;
        int n = rng.Next(0, 4);
        for (int i = 0; i < n; i++)
        {
            var e = (Edge)PickRoadEdge(rng, mask);
            Spawn(ch, Pick(roadDecor, rng), EdgePoint(e, Range(rng, 0.3f, Size - 0.3f), Range(rng, 0.3f, 3.2f)), Range(rng, 0f, 360f));
        }
    }

    // ------------------------------------------------------------------ placement helpers
    static float RadiusOf(GameObject prefab) => prefab && prefab.TryGetComponent(out EnvProp e) ? e.radius : 1f;

    bool TryPlace(Chunk ch, GameObject prefab, System.Random rng, Func<Vector2> pos, Func<float> yaw)
    {
        if (!prefab) return false;
        float r = RadiusOf(prefab);
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var p = pos();
            if (!Free(ch, p, r)) continue;
            Place(ch, prefab, p, yaw(), r);
            return true;
        }
        return false;
    }

    bool Free(Chunk ch, Vector2 p, float r)
    {
        // Keep the spawn point (global origin) clear.
        Vector2 global = new Vector2(ch.coord.x * Size + p.x, ch.coord.y * Size + p.y);
        if (global.magnitude < 6f + r) return false;
        // Keep objective spots clear.
        if (CityLayout.IsLandmark(ch.type) && (p - CityLayout.SiteLocal(ch.type)).sqrMagnitude < (2.2f + r) * (2.2f + r)) return false;
        foreach (var o in placements)
            if ((o.pos - p).sqrMagnitude < (o.r + r) * (o.r + r)) return false;
        return true;
    }

    void Place(Chunk ch, GameObject prefab, Vector2 p, float yaw, float r)
    {
        if (!prefab) return;
        placements.Add(new Placement { pos = p, r = r });
        Spawn(ch, prefab, p, yaw);
    }

    void Spawn(Chunk ch, GameObject prefab, Vector2 p, float yaw)
    {
        if (!prefab) return;
        if (!pools.TryGetValue(prefab, out var stack)) { stack = new Stack<GameObject>(); pools[prefab] = stack; }
        GameObject go;
        if (stack.Count > 0) go = stack.Pop();
        else { go = Instantiate(prefab); prefabOf[go] = prefab; }
        go.transform.SetParent(ch.root, false);
        go.transform.SetLocalPositionAndRotation(new Vector3(p.x, 0f, p.y), Quaternion.Euler(0f, yaw, 0f));
        if (go.TryGetComponent(out ExplosiveBarrel barrel)) barrel.ResetBarrel();
        ch.items.Add(go);
    }

    static GameObject Pick(GameObject[] arr, System.Random rng) => arr == null || arr.Length == 0 ? null : arr[rng.Next(arr.Length)];
    static float Range(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

    static int Hash(int s, int x, int z)
    {
        unchecked
        {
            int h = s * 73856093 ^ x * 19349663 ^ z * 83492791;
            h ^= h >> 13; h *= 1274126177; h ^= h >> 16;
            return h;
        }
    }
}
