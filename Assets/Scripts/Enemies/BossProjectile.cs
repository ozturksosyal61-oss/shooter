using UnityEngine;

/// <summary>
/// Debris / car part hurled by the boss at a far-away player: lobbed on an arc to a fixed landing point that is
/// marked on the ground (shrinking red ring) for the whole flight; area damage + knockback on landing.
/// Owned by the boss (fixed set, reused); lives as a root object so the floating origin shifts it.
/// Acid (Kovan Ana): a smaller splash, then a green pool stays on the ground for <see cref="poolTime"/> and burns
/// whoever stands in it (<see cref="poolDps"/>); the marker becomes the pool.
/// </summary>
public class BossProjectile : MonoBehaviour
{
    public Transform mesh;
    public Transform marker;
    public float radius = 2.8f;
    public float damage = 24f;
    public float arcHeight = 6f;
    [Header("Acid (Kovan Ana)")]
    public bool acid;
    public float poolTime = 4f;
    public float poolRadius = 2.6f;
    public float poolDps = 10f;
    public Color poolColor = new Color(0.45f, 1f, 0.2f, 1f);
    public Renderer markerRenderer;
    /// <summary>Co-op client copy: flies, marks and splashes, but the host deals the damage.</summary>
    [System.NonSerialized] public bool visualOnly;

    float poolT;
    MaterialPropertyBlock mpb;
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    /// <summary>Damage dealt by acid pools this run (bots / tests).</summary>
    public static float AcidDamage { get; private set; }
    public bool Pooling => poolT > 0f;

    Vector3 from, to, axis;
    float t, flight;
    bool flying;

    public bool Flying => flying || poolT > 0f;
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
        poolT = 0f;
        flying = true;
        if (mesh) mesh.gameObject.SetActive(true);
        Color(new Color(1f, 0.15f, 0.05f, 1f));
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
        poolT = 0f;
        gameObject.SetActive(false);
        if (marker) marker.gameObject.SetActive(false);
    }

    void Color(Color c)
    {
        if (!markerRenderer) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        markerRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColorId, c);
        markerRenderer.SetPropertyBlock(mpb);
    }

    void Update()
    {
        if (poolT > 0f) { UpdatePool(); return; }
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

    void UpdatePool()
    {
        poolT -= Time.deltaTime;
        float fade = Mathf.Clamp01(poolT / 0.8f);
        if (marker)
        {
            float s = poolRadius * 2f * (1f + 0.03f * Mathf.Sin(Time.time * 6f));
            marker.localScale = new Vector3(s, s, 1f);
        }
        Color(poolColor * (0.55f + 0.45f * fade));
        // Silent burn (no knockback spam) on everyone standing in it: health ticks down, the slow says "get out".
        if (!visualOnly) AcidDamage += Squad.BurnArea(to, poolRadius, poolDps * Time.deltaTime, 0.75f, 0.15f);
        if (poolT <= 0f) Hide();
    }

    void Impact()
    {
        Vector3 p = to;
        Landed++;
        if (acid)
        {
            // Splash + pool.
            flying = false;
            poolT = poolTime;
            if (mesh) mesh.gameObject.SetActive(false);
            if (Fx.I) { Fx.I.Hit(p + Vector3.up * 0.3f, Vector3.up); Fx.I.Dust(p); }
            GameAudio.Play(SfxId.BombExplode, p, 1.6f, 0.45f);
            if (!visualOnly) Squad.HitArea(p, radius, damage, 0.4f, 6f, false, false, Vector3.forward);
            return;
        }
        if (Fx.I) { Fx.I.Explosion(p, radius * 0.7f, false); Fx.I.Dust(p); }
        GameAudio.Play(SfxId.BossSlam, p, 1.35f, 0.8f);
        GameAudio.Play(SfxId.HitMetal, p);
        var gm = GameManager.I;
        if (gm) gm.Shake(0.35f);
        if (visualOnly) { Hide(); return; }
        Squad.HitArea(p, radius, damage, 0.4f, 10f, false, false, Vector3.forward);
        foreach (var z in Zombie.Active)
        {
            Vector3 d = z.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude < radius * radius && z.Kind != ZombieKind.Boss) z.Knock(d.normalized * 7f);
        }
        Hide();
    }
}
