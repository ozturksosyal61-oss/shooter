using UnityEngine;

/// <summary>
/// Endless spawning just outside the camera view, biased toward where the player is heading.
/// Zombies left far behind are recycled.
/// </summary>
public class ZombieSpawner : MonoBehaviour
{
    public Zombie prefab;
    public int prewarm = 50;

    [Header("Special zombies (lab mutations)")]
    public Zombie shieldPrefab;
    public Zombie bombPrefab;
    public Zombie tonguePrefab;
    public int specialPrewarm = 3;
    public float shieldHealthMul = 8f, bombHealthMul = 1.3f, tongueHealthMul = 2.4f;
    public float shieldSpeed = 1.25f, bombSpeed = 2f, tongueSpeed = 2.1f;
    public float shieldDamageMul = 1.8f;
    [Tooltip("Relative pick weights when a special spawns: shield, bomb, tongue.")]
    public Vector3 specialWeights = new Vector3(0.3f, 0.45f, 0.25f);
    public float viewMargin = 0.05f;      // viewport units outside the screen
    public float extraDistance = 1f;      // metres beyond the view edge (camera at 17.5 m since F6.5: keep travel time similar)
    public float despawnDistance = 34f;
    public float baseHealth = 70f;
    public Vector2 walkerSpeed = new Vector2(1.7f, 2.5f);
    public Vector2 runnerSpeed = new Vector2(3.6f, 4.4f);

    Pool<Zombie> pool, shieldPool, bombPool, tonguePool;
    float timer = 1f;
    Camera cam;
    Vector3 lastPlayerPos, heading;
    static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

    public bool LastSpawnWasOutsideView { get; private set; }

    void Awake()
    {
        Zombie.Active.Clear();
        pool = new Pool<Zombie>(prefab, transform, prewarm);
        if (shieldPrefab) shieldPool = new Pool<Zombie>(shieldPrefab, transform, specialPrewarm);
        if (bombPrefab) bombPool = new Pool<Zombie>(bombPrefab, transform, specialPrewarm);
        if (tonguePrefab) tonguePool = new Pool<Zombie>(tonguePrefab, transform, specialPrewarm);
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm.IsGameOver) return;
        if (!cam) cam = Camera.main;
        var player = gm.player.transform;

        // Smoothed movement heading.
        Vector3 v = (player.position - lastPlayerPos) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPlayerPos = player.position;
        v.y = 0f;
        if (v.sqrMagnitude < 400f) heading = Vector3.Lerp(heading, v, 1f - Mathf.Exp(-3f * Time.deltaTime));

        RecycleStragglers(player.position);

        var waves = WaveDirector.I;
        if (waves && waves.InBreak) return; // breather: no spawning

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        var wp = waves ? waves.Params : Difficulty.ForWave(1);
        float pressure = ObjectiveDirector.I ? ObjectiveDirector.I.SpawnPressure : 1f;
        timer = wp.spawnInterval * pressure * Random.Range(0.7f, 1.3f);
        int burst = wp.burst + (Random.value < 0.3f ? 1 : 0);

        for (int i = 0; i < burst && Zombie.Active.Count < wp.maxAlive; i++)
        {
            if (!TryFindSpawnPoint(player.position, out var pos)) break;
            if (Random.value < wp.specialChance && TrySpawnSpecial(PickSpecial(wp), pos, wp) != null) continue;
            bool runner = Random.value < wp.runnerChance;
            float speed = (runner ? Random.Range(runnerSpeed.x, runnerSpeed.y) : Random.Range(walkerSpeed.x, walkerSpeed.y)) * wp.speedMultiplier;
            float hp = baseHealth * wp.healthMultiplier * (runner ? 0.6f : 1f);
            pool.Get().Spawn(pos, hp, speed, wp.contactDamage, runner, pool);
        }
    }

    /// <summary>Weighted pick among the special kinds still under their per-wave limit (Walker = none).</summary>
    ZombieKind PickSpecial(WaveParams wp)
    {
        float ws = Zombie.CountKind(ZombieKind.Shield) < wp.maxShield && shieldPool != null ? specialWeights.x : 0f;
        float wb = Zombie.CountKind(ZombieKind.Bomb) < wp.maxBomb && bombPool != null ? specialWeights.y : 0f;
        float wt = Zombie.CountKind(ZombieKind.Tongue) < wp.maxTongue && tonguePool != null ? specialWeights.z : 0f;
        float r = Random.value * (ws + wb + wt);
        if (ws + wb + wt <= 0f) return ZombieKind.Walker;
        if (r < ws) return ZombieKind.Shield;
        if (r < ws + wb) return ZombieKind.Bomb;
        return ZombieKind.Tongue;
    }

    /// <summary>Spawns a special zombie at <paramref name="pos"/> tuned for the wave (tests and bots call it directly).</summary>
    public Zombie TrySpawnSpecial(ZombieKind kind, Vector3 pos, WaveParams wp)
    {
        Pool<Zombie> p; float hpMul, spd, dmgMul = 1f;
        switch (kind)
        {
            case ZombieKind.Shield: p = shieldPool; hpMul = shieldHealthMul; spd = shieldSpeed; dmgMul = shieldDamageMul; break;
            case ZombieKind.Bomb: p = bombPool; hpMul = bombHealthMul; spd = bombSpeed; break;
            case ZombieKind.Tongue: p = tonguePool; hpMul = tongueHealthMul; spd = tongueSpeed; break;
            default: return null;
        }
        if (p == null) return null;
        var z = p.Get();
        z.Spawn(pos, baseHealth * wp.healthMultiplier * hpMul, spd * Mathf.Min(wp.speedMultiplier, 1.25f), wp.contactDamage * dmgMul, false, p);
        return z;
    }

    public Zombie SpawnSpecial(ZombieKind kind, Vector3 pos) =>
        TrySpawnSpecial(kind, pos, WaveDirector.I ? WaveDirector.I.Params : Difficulty.ForWave(1));

    void RecycleStragglers(Vector3 playerPos)
    {
        float max = despawnDistance * despawnDistance;
        for (int i = Zombie.Active.Count - 1; i >= 0; i--)
        {
            var z = Zombie.Active[i];
            Vector3 d = z.transform.position - playerPos; d.y = 0f;
            if (d.sqrMagnitude > max) z.Despawn();
        }
    }

    /// <summary>Samples points on a rectangle slightly larger than the viewport and projects them to the ground.</summary>
    public bool TryFindSpawnPoint(Vector3 playerPos, out Vector3 best)
    {
        best = default;
        if (!cam) cam = Camera.main;
        float bestScore = float.NegativeInfinity;
        Vector3 head = heading.sqrMagnitude > 0.25f ? heading.normalized : Vector3.zero;
        float lo = -viewMargin, hi = 1f + viewMargin;

        for (int i = 0; i < 12; i++)
        {
            float s = Random.value;
            Vector2 vp;
            switch (Random.Range(0, 4))
            {
                case 0: vp = new Vector2(lo, Mathf.Lerp(lo, hi, s)); break;
                case 1: vp = new Vector2(hi, Mathf.Lerp(lo, hi, s)); break;
                case 2: vp = new Vector2(Mathf.Lerp(lo, hi, s), lo); break;
                default: vp = new Vector2(Mathf.Lerp(lo, hi, s), hi); break;
            }
            var ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
            if (!Ground.Raycast(ray, out float enter)) continue;
            Vector3 p = ray.GetPoint(enter);
            Vector3 away = p - playerPos; away.y = 0f;
            if (away.sqrMagnitude < 1f) continue;
            p += away.normalized * extraDistance;
            p.y = 0f;
            if (Physics.CheckSphere(p + Vector3.up * 0.8f, 0.6f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) continue;

            float score = Vector3.Dot(away.normalized, head) * 2f + Random.value;
            if (score > bestScore) { bestScore = score; best = p; }
        }
        if (bestScore == float.NegativeInfinity) return false;
        Vector3 vpBest = cam.WorldToViewportPoint(best);
        LastSpawnWasOutsideView = vpBest.x < 0f || vpBest.x > 1f || vpBest.y < 0f || vpBest.y > 1f || vpBest.z < 0f;
        return true;
    }
}
