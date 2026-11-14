using System;
using System.Collections.Generic;
using UnityEngine;

public enum District { Cadde, Carsi, Sanayi, Liman }

[Serializable]
public class DistrictSettings
{
    public District district;
    public Material ground;
    public GameObject[] buildings;
    [Range(0f, 1f)] public float buildingChance;
    public GameObject[] blockProps;
    public int blockPropMin = 1, blockPropMax = 3;
    public GameObject[] sidewalkProps;
}

/// <summary>
/// Infinite seeded city made of 20x20 m chunks. Every chunk is one block: half of a 7 m road
/// on each edge (so roads meet between chunks), a sidewalk ring and a 9x9 m block interior.
/// The 5x5 chunks around the player are kept alive; everything is pooled.
/// </summary>
public class ChunkManager : MonoBehaviour
{
    public const float Size = 20f;
    const float RoadHalf = 3.5f;
    const float SidewalkOuter = 5.5f;

    public static ChunkManager I { get; private set; }

    public Transform player;
    public int radius = 2;
    public bool randomSeed = true;
    public int seed = 12345;

    public DistrictSettings[] districts;
    public GameObject[] roadCars;
    public GameObject[] roadBarricades;
    public GameObject[] burningBarrels;
    public GameObject[] roadDecor;

    class Chunk
    {
        public Vector2Int coord;
        public Transform root;
        public MeshRenderer ground;
        public readonly List<GameObject> items = new List<GameObject>();
    }

    struct Placement { public Vector2 pos; public float r; }

    readonly Dictionary<Vector2Int, Chunk> active = new Dictionary<Vector2Int, Chunk>();
    readonly Stack<Chunk> freeChunks = new Stack<Chunk>();
    readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
    readonly Dictionary<GameObject, GameObject> prefabOf = new Dictionary<GameObject, GameObject>();
    readonly List<Placement> placements = new List<Placement>(32);
    readonly List<Vector2Int> toRemove = new List<Vector2Int>(16);
    Transform poolRoot;
    Mesh groundMesh;
    Vector2Int center = new Vector2Int(int.MinValue, int.MinValue);
    Vector2 noiseOffset;

    public int ActiveChunkCount => active.Count;

    void Awake()
    {
        I = this;
        if (randomSeed) seed = UnityEngine.Random.Range(1, 1_000_000);
        var r = new System.Random(seed);
        noiseOffset = new Vector2((float)r.NextDouble() * 1000f, (float)r.NextDouble() * 1000f);
        groundMesh = BuildGroundMesh();
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

    void UpdateChunks()
    {
        if (!player) return;
        var c = ChunkOf(player.position);
        if (c == center) return;
        center = c;

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
        ch.root.name = $"Chunk {coord.x},{coord.y}";
        ch.root.localPosition = new Vector3(coord.x * Size, 0f, coord.y * Size);
        ch.root.gameObject.SetActive(true);

        var rng = new System.Random(Hash(seed, coord.x, coord.y));
        var ds = districts[(int)DistrictAt(coord)];
        ch.ground.sharedMaterial = ds.ground;
        placements.Clear();

        // Block interior: building or props.
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

        // Road obstacles: cars along the lane, barricades across it, burning barrels.
        int roadCount = rng.Next(1, 4);
        for (int i = 0; i < roadCount; i++)
        {
            int band = rng.Next(4); // 0=W 1=E 2=S 3=N
            bool alongZ = band < 2;
            float across = band == 0 || band == 2 ? 1.75f : Size - 1.75f;
            double roll = rng.NextDouble();
            GameObject prefab;
            float yaw;
            if (roll < 0.55) { prefab = Pick(roadCars, rng); yaw = (alongZ ? 0f : 90f) + (rng.Next(2) * 180f) + Range(rng, -20f, 20f); }
            else if (roll < 0.8) { prefab = Pick(roadBarricades, rng); yaw = (alongZ ? 90f : 0f) + Range(rng, -10f, 10f); }
            else { prefab = Pick(burningBarrels, rng); yaw = Range(rng, 0f, 360f); across = band == 0 || band == 2 ? Range(rng, 0.6f, 3f) : Size - Range(rng, 0.6f, 3f); }
            float a = across;
            TryPlace(ch, prefab, rng, () =>
            {
                float along = Range(rng, 6f, 14f);
                return alongZ ? new Vector2(a, along) : new Vector2(along, a);
            }, () => yaw);
        }

        // Sidewalk props.
        if (ds.sidewalkProps != null && ds.sidewalkProps.Length > 0 && rng.NextDouble() < 0.6)
        {
            int n = rng.Next(1, 3);
            for (int i = 0; i < n; i++)
            {
                var p = Pick(ds.sidewalkProps, rng);
                int side = rng.Next(4);
                TryPlace(ch, p, rng, () =>
                {
                    float off = 4.6f, along = Range(rng, 6f, 14f);
                    switch (side)
                    {
                        case 0: return new Vector2(off, along);
                        case 1: return new Vector2(Size - off, along);
                        case 2: return new Vector2(along, off);
                        default: return new Vector2(along, Size - off);
                    }
                }, () => Range(rng, 0f, 360f));
            }
        }

        // Decor without colliders (cones, tires).
        if (roadDecor != null && roadDecor.Length > 0)
        {
            int n = rng.Next(0, 4);
            for (int i = 0; i < n; i++)
            {
                var p = Pick(roadDecor, rng);
                var pos = new Vector2(Range(rng, 0.3f, Size - 0.3f), Range(rng, 0.3f, 3.2f));
                if (rng.Next(2) == 0) pos = new Vector2(pos.y, pos.x);
                Spawn(ch, p, pos, Range(rng, 0f, 360f));
            }
        }
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
        g.AddComponent<MeshFilter>().sharedMesh = groundMesh;
        ch.ground = g.AddComponent<MeshRenderer>();
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

    // ------------------------------------------------------------------ placement helpers
    static float RadiusOf(GameObject prefab) => prefab.TryGetComponent(out EnvProp e) ? e.radius : 1f;

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
        foreach (var o in placements)
            if ((o.pos - p).sqrMagnitude < (o.r + r) * (o.r + r)) return false;
        return true;
    }

    void Place(Chunk ch, GameObject prefab, Vector2 p, float yaw, float r)
    {
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

    static Mesh BuildGroundMesh()
    {
        var m = new Mesh { name = "ChunkGround" };
        m.vertices = new[] { new Vector3(0, 0, 0), new Vector3(0, 0, Size), new Vector3(Size, 0, Size), new Vector3(Size, 0, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
        m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        m.RecalculateBounds();
        return m;
    }
}
