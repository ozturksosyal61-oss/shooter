using UnityEngine;

/// <summary>Endless spawning along the arena edges with density ramping over time.</summary>
public class ZombieSpawner : MonoBehaviour
{
    public Zombie prefab;
    public int prewarm = 40;
    public float minDistanceToPlayer = 9f;

    [Header("Difficulty ramp")]
    public float startInterval = 1.4f;
    public float minInterval = 0.22f;
    public float intervalRampSeconds = 150f;
    public int startMaxAlive = 8;
    public int endMaxAlive = 70;
    public float maxAliveRampSeconds = 200f;

    Pool<Zombie> pool;
    float timer = 1.2f;

    void Awake()
    {
        Zombie.Active.Clear();
        pool = new Pool<Zombie>(prefab, transform, prewarm);
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm.IsGameOver) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;

        float t = gm.GameTime;
        timer = Mathf.Lerp(startInterval, minInterval, Mathf.Clamp01(t / intervalRampSeconds)) * Random.Range(0.7f, 1.3f);
        int maxAlive = Mathf.RoundToInt(Mathf.Lerp(startMaxAlive, endMaxAlive, Mathf.Clamp01(t / maxAliveRampSeconds)));

        int burst = t > 45f && Random.value < 0.25f ? Random.Range(2, 4) : 1;
        for (int i = 0; i < burst && Zombie.Active.Count < maxAlive; i++)
        {
            if (!TryFindSpawnPoint(gm.player.transform.position, out var pos)) break;
            float hp = 3f + Mathf.Floor(t / 75f);
            float speed = Random.Range(1.7f, 2.5f) * (1f + Mathf.Min(t / 300f, 0.5f));
            pool.Get().Spawn(pos, hp, speed, pool);
        }
    }

    bool TryFindSpawnPoint(Vector3 playerPos, out Vector3 pos)
    {
        const float inset = 1.2f;
        float hx = Arena.HalfX - inset, hz = Arena.HalfZ - inset;
        float perimeter = 4f * (hx + hz);
        for (int attempt = 0; attempt < 10; attempt++)
        {
            float d = Random.value * perimeter;
            if (d < 2f * hx) pos = new Vector3(-hx + d, 0f, hz);
            else if ((d -= 2f * hx) < 2f * hz) pos = new Vector3(hx, 0f, hz - d);
            else if ((d -= 2f * hz) < 2f * hx) pos = new Vector3(hx - d, 0f, -hz);
            else { d -= 2f * hx; pos = new Vector3(-hx, 0f, -hz + d); }

            if ((pos - playerPos).sqrMagnitude < minDistanceToPlayer * minDistanceToPlayer) continue;
            if (Physics.CheckSphere(pos + Vector3.up * 0.6f, 0.5f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) continue;
            return true;
        }
        pos = default;
        return false;
    }
}
