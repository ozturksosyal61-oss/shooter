using UnityEngine;

/// <summary>
/// Debris / car part hurled by the boss at a far-away player: lobbed on an arc to a fixed landing point that is
/// marked on the ground (shrinking red ring) for the whole flight; area damage + knockback on landing.
/// Owned by the boss (fixed set, reused); lives as a root object so the floating origin shifts it.
/// </summary>
public class BossProjectile : MonoBehaviour
{
    public Transform mesh;
    public Transform marker;
    public float radius = 2.8f;
    public float damage = 24f;
    public float arcHeight = 6f;

    Vector3 from, to, axis;
    float t, flight;
    bool flying;

    public bool Flying => flying;
    public Vector3 Target => to;
    public float Radius => radius;
    public static int Landed { get; private set; }

    void Awake()
    {
        // Runs on the first Launch if the prefab starts inactive: only detach here (the boss hides us at spawn).
        if (marker) marker.SetParent(null, true);
    }

    void OnEnable() => FloatingOrigin.OnShift += Shift;
    void OnDisable() => FloatingOrigin.OnShift -= Shift;

    void Shift(Vector3 d)
    {
        from += d;
        to += d;
        if (marker) marker.position += d;
    }

    public void Launch(Vector3 start, Vector3 target, float seconds)
    {
        from = start;
        to = target; to.y = 0f;
        flight = Mathf.Max(0.3f, seconds);
        t = 0f;
        flying = true;
        axis = Random.onUnitSphere;
        transform.position = from;
        gameObject.SetActive(true);
        if (marker)
        {
            marker.gameObject.SetActive(true);
            marker.position = to + Vector3.up * 0.05f;
        }
    }

    public void Hide()
    {
        flying = false;
        gameObject.SetActive(false);
        if (marker) marker.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!flying) return;
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / flight);
        transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (arcHeight * 4f * k * (1f - k));
        if (mesh) mesh.Rotate(axis, 540f * Time.deltaTime, Space.World);
        if (marker)
        {
            // Outline shrinks onto the impact size; blinks faster near the end.
            float s = radius * 2f * Mathf.Lerp(1.5f, 1f, k);
            float blink = 1f + 0.08f * Mathf.Sin(t * (10f + 30f * k));
            marker.localScale = new Vector3(s * blink, s * blink, 1f);
        }
        if (k >= 1f) Impact();
    }

    void Impact()
    {
        Vector3 p = to;
        Landed++;
        if (Fx.I) { Fx.I.Explosion(p, radius * 0.7f, false); Fx.I.Dust(p); }
        GameAudio.Play(SfxId.BossSlam, p, 1.35f, 0.8f);
        GameAudio.Play(SfxId.HitMetal, p);
        var gm = GameManager.I;
        if (gm)
        {
            gm.Shake(0.35f);
            var pl = gm.player;
            if (pl && !pl.IsDead)
            {
                Vector3 d = pl.transform.position - p; d.y = 0f;
                float m = d.magnitude;
                if (m < radius)
                    pl.TakeDamage(damage * (1f - 0.4f * m / radius), m > 0.05f ? d / m : Vector3.forward, 10f);
            }
        }
        foreach (var z in Zombie.Active)
        {
            Vector3 d = z.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude < radius * radius && z.Kind != ZombieKind.Boss) z.Knock(d.normalized * 7f);
        }
        Hide();
    }
}
