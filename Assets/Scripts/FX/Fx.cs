using UnityEngine;

/// <summary>
/// Particle effects emitted on demand from a few always-playing systems, plus pooled
/// green goo splats on the ground. No per-effect allocation.
/// </summary>
public class Fx : MonoBehaviour
{
    public static Fx I { get; private set; }

    public ParticleSystem goo;     // alpha-blended green zombie goo
    public ParticleSystem chunks;  // heavier goo drops with gravity
    public ParticleSystem sparks;  // additive sparks
    public ParticleSystem pop;     // additive impact flash
    public ParticleSystem dust;    // alpha dust puffs

    public GameObject splatPrefab;
    public int splatCount = 40;
    public float splatLife = 8f;

    static ParticleSystem.EmitParams ep;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    Transform[] splats;
    Renderer[] splatRenderers;
    float[] splatBorn;
    Color[] splatColor;
    int nextSplat;
    MaterialPropertyBlock mpb;
    ParticleSystem.Particle[] shiftBuffer;

    void Awake()
    {
        I = this;
        mpb = new MaterialPropertyBlock();
        if (splatPrefab)
        {
            splats = new Transform[splatCount];
            splatRenderers = new Renderer[splatCount];
            splatBorn = new float[splatCount];
            splatColor = new Color[splatCount];
            for (int i = 0; i < splatCount; i++)
            {
                var s = Instantiate(splatPrefab, transform);
                s.SetActive(false);
                splats[i] = s.transform;
                splatRenderers[i] = s.GetComponentInChildren<Renderer>();
            }
        }
    }

    void OnEnable() => FloatingOrigin.OnShift += OnShift;
    void OnDisable() => FloatingOrigin.OnShift -= OnShift;

    void OnShift(Vector3 delta)
    {
        // Fx root itself is shifted with the other roots; world-space particles must be moved by hand.
        foreach (var ps in new[] { goo, chunks, sparks, pop, dust })
        {
            if (!ps) continue;
            int max = ps.main.maxParticles;
            if (shiftBuffer == null || shiftBuffer.Length < max) shiftBuffer = new ParticleSystem.Particle[max];
            int n = ps.GetParticles(shiftBuffer);
            for (int i = 0; i < n; i++) shiftBuffer[i].position += delta;
            ps.SetParticles(shiftBuffer, n);
        }
    }

    void Update()
    {
        if (splats == null) return;
        float now = Time.time;
        for (int i = 0; i < splats.Length; i++)
        {
            if (!splats[i].gameObject.activeSelf) continue;
            float age = now - splatBorn[i];
            if (age > splatLife) { splats[i].gameObject.SetActive(false); continue; }
            float fade = Mathf.Clamp01((splatLife - age) / 2f);
            var c = splatColor[i]; c.a *= fade;
            mpb.SetColor(BaseColor, c);
            splatRenderers[i].SetPropertyBlock(mpb);
        }
    }

    static void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life)
    {
        ep.position = pos;
        ep.velocity = vel;
        ep.startSize = size;
        ep.startLifetime = life;
        ep.rotation = Random.Range(0f, 360f);
        ps.Emit(ep, 1);
    }

    static Vector3 Spray(Vector3 dir, float min, float max, float spread, float upMin, float upMax)
    {
        Vector3 r = Random.insideUnitSphere * spread; r.y = 0f;
        return dir * Random.Range(min, max) + r + Vector3.up * Random.Range(upMin, upMax);
    }

    public void Splat(Vector3 p, float size)
    {
        if (splats == null) return;
        int i = nextSplat;
        nextSplat = (nextSplat + 1) % splats.Length;
        var t = splats[i];
        t.gameObject.SetActive(true);
        t.SetPositionAndRotation(new Vector3(p.x, 0.02f + i * 0.0004f, p.z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        t.localScale = Vector3.one * size * Random.Range(0.8f, 1.2f);
        splatBorn[i] = Time.time;
        splatColor[i] = Color.Lerp(new Color(0.35f, 0.75f, 0.12f, 0.9f), new Color(0.25f, 0.55f, 0.08f, 0.9f), Random.value);
        mpb.SetColor(BaseColor, splatColor[i]);
        splatRenderers[i].SetPropertyBlock(mpb);
    }

    public void Hit(Vector3 p, Vector3 dir)
    {
        for (int i = 0; i < 8; i++) Emit(goo, p, Spray(dir, 2f, 6f, 2.5f, 0.5f, 3f), Random.Range(0.12f, 0.26f), Random.Range(0.25f, 0.45f));
        for (int i = 0; i < 5; i++) Emit(sparks, p, (-dir + Random.insideUnitSphere * 0.9f) * Random.Range(3f, 7f), Random.Range(0.05f, 0.1f), Random.Range(0.08f, 0.16f));
        Emit(pop, p - dir * 0.2f, Vector3.zero, 0.9f, 0.07f);
        if (Random.value < 0.35f) Splat(p + dir * Random.Range(0.5f, 1.5f), 0.7f);
    }

    public void Death(Vector3 p, Vector3 dir)
    {
        for (int i = 0; i < 24; i++) Emit(goo, p, Spray(dir, 1f, 8f, 4f, 0.5f, 4f), Random.Range(0.18f, 0.4f), Random.Range(0.3f, 0.6f));
        for (int i = 0; i < 10; i++) Emit(chunks, p, Spray(dir, 2f, 6f, 3f, 2f, 5f), Random.Range(0.15f, 0.3f), Random.Range(0.5f, 0.8f));
        Emit(pop, p, Vector3.zero, 2.2f, 0.1f);
        Splat(p + dir * 1.2f, 1.6f);
    }

    public void Spark(Vector3 p, Vector3 normal)
    {
        for (int i = 0; i < 7; i++) Emit(sparks, p, (normal + Random.insideUnitSphere * 0.8f) * Random.Range(3f, 7f), Random.Range(0.06f, 0.12f), Random.Range(0.1f, 0.2f));
        Emit(pop, p, Vector3.zero, 0.5f, 0.05f);
    }

    public void Muzzle(Vector3 p, Vector3 dir)
    {
        for (int i = 0; i < 3; i++) Emit(sparks, p, (dir + Random.insideUnitSphere * 0.35f) * Random.Range(6f, 11f), Random.Range(0.05f, 0.1f), Random.Range(0.06f, 0.12f));
    }

    public void Dust(Vector3 p)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 r = Random.insideUnitSphere; r.y = 0f;
            Emit(dust, p + r * 0.4f + Vector3.up * 0.1f, r * Random.Range(0.8f, 2f) + Vector3.up * Random.Range(0.5f, 1.5f), Random.Range(0.4f, 0.8f), Random.Range(0.4f, 0.7f));
        }
    }
}
