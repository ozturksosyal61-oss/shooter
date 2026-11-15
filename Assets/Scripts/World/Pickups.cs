using UnityEngine;

/// <summary>
/// Health packs dropped by zombies (low chance, higher when the player is hurt). Pooled; they bob,
/// are pulled in by a magnet when the player is close and needs health, and blink out after a while.
/// </summary>
public class Pickups : MonoBehaviour
{
    public static Pickups I { get; private set; }

    public GameObject healthPackPrefab;
    public int poolSize = 8;
    [Range(0f, 1f)] public float dropChance = 0.05f;
    [Range(0f, 1f)] public float dropChanceWhenHurt = 0.12f;
    public float minTimeBetweenDrops = 5f;
    public float healAmount = 25f;
    public float magnetRadius = 5f;
    public float collectRadius = 0.9f;
    public float lifetime = 18f;

    Transform[] items;
    Transform[] visuals;
    float[] born;
    Vector3[] vel;
    float lastDrop = -100f;

    public int ActiveCount
    {
        get { int n = 0; foreach (var t in items) if (t.gameObject.activeSelf) n++; return n; }
    }

    void Awake()
    {
        I = this;
        items = new Transform[poolSize];
        visuals = new Transform[poolSize];
        born = new float[poolSize];
        vel = new Vector3[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            var go = Instantiate(healthPackPrefab, transform);
            go.SetActive(false);
            items[i] = go.transform;
            visuals[i] = go.transform.childCount > 0 ? go.transform.GetChild(0) : go.transform;
        }
    }

    public void MaybeDrop(Vector3 pos)
    {
        var p = GameManager.I.player;
        if (Time.time - lastDrop < minTimeBetweenDrops) return;
        float chance = p && p.Health < p.maxHealth * 0.5f ? dropChanceWhenHurt : dropChance;
        if (Random.value > chance) return;
        Drop(pos);
    }

    public void Drop(Vector3 pos)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].gameObject.activeSelf) continue;
            lastDrop = Time.time;
            items[i].position = new Vector3(pos.x, 0f, pos.z);
            items[i].gameObject.SetActive(true);
            born[i] = Time.time;
            vel[i] = Vector3.zero;
            return;
        }
    }

    void Update()
    {
        var gm = GameManager.I;
        if (!gm || !gm.player) return;
        var player = gm.player;
        Vector3 pp = player.transform.position;
        float dt = Time.deltaTime;
        for (int i = 0; i < items.Length; i++)
        {
            var t = items[i];
            if (!t.gameObject.activeSelf) continue;
            float age = Time.time - born[i];
            if (age > lifetime) { t.gameObject.SetActive(false); continue; }

            Vector3 to = pp - t.position; to.y = 0f;
            float d = to.magnitude;
            if (player.NeedsHealth && d < magnetRadius)
            {
                // Magnet: accelerate toward the player.
                vel[i] = Vector3.MoveTowards(vel[i], to.normalized * 14f, 40f * dt);
                t.position += vel[i] * dt;
                if (d < collectRadius)
                {
                    player.Heal(healAmount);
                    GameAudio.Play(SfxId.Pickup, pp);
                    if (Fx.I) Fx.I.Dust(t.position);
                    t.gameObject.SetActive(false);
                    continue;
                }
            }
            else vel[i] = Vector3.zero;

            // Bob + spin, blink in the last 3 seconds.
            var v = visuals[i];
            v.localPosition = new Vector3(0f, 0.45f + Mathf.Sin(age * 4f) * 0.12f, 0f);
            v.localRotation = Quaternion.Euler(0f, age * 120f, 0f);
            bool visible = age < lifetime - 3f || Mathf.Repeat(age * 6f, 1f) < 0.6f;
            if (v.gameObject.activeSelf != visible) v.gameObject.SetActive(visible);
        }
    }
}
