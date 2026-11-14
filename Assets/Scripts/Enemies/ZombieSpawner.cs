using UnityEngine;

/// <summary>
/// Endless spawning just outside the camera view, biased toward where the player is heading.
/// Zombies left far behind are recycled.
/// </summary>
public class ZombieSpawner : MonoBehaviour
{
    public Zombie prefab;
    public int prewarm = 50;
    public float viewMargin = 0.08f;      // viewport units outside the screen
    public float extraDistance = 1.5f;    // metres beyond the view edge
    public float despawnDistance = 34f;
    public float baseHealth = 70f;
    public Vector2 walkerSpeed = new Vector2(1.7f, 2.5f);
    public Vector2 runnerSpeed = new Vector2(3.6f, 4.4f);

    Pool<Zombie> pool;
    float timer = 1f;
    Camera cam;
    Vector3 lastPlayerPos, heading;
    static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

    public bool LastSpawnWasOutsideView { get; private set; }

    void Awake()
    {
        Zombie.Active.Clear();
        pool = new Pool<Zombie>(prefab, transform, prewarm);
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

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        float t = gm.GameTime;
        timer = Difficulty.SpawnInterval(t) * Random.Range(0.7f, 1.3f);
        int maxAlive = Difficulty.MaxAlive(t);
        int burst = Difficulty.BurstSize(t);

        for (int i = 0; i < burst && Zombie.Active.Count < maxAlive; i++)
        {
            if (!TryFindSpawnPoint(player.position, out var pos)) break;
            bool runner = Random.value < Difficulty.RunnerChance(t);
            float speed = (runner ? Random.Range(runnerSpeed.x, runnerSpeed.y) : Random.Range(walkerSpeed.x, walkerSpeed.y)) * Difficulty.SpeedMultiplier(t);
            float hp = baseHealth * Difficulty.HealthMultiplier(t) * (runner ? 0.6f : 1f);
            pool.Get().Spawn(pos, hp, speed, Difficulty.ContactDamage(t), runner, pool);
        }
    }

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
