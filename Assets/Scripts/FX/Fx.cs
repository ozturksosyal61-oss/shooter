using UnityEngine;

/// <summary>Particle effects emitted on demand from a few always-playing systems (no per-effect allocation).</summary>
public class Fx : MonoBehaviour
{
    public static Fx I { get; private set; }

    public ParticleSystem goo;     // alpha-blended zombie goo
    public ParticleSystem chunks;  // heavier goo chunks with gravity
    public ParticleSystem sparks;  // additive sparks
    public ParticleSystem pop;     // additive impact flash
    public ParticleSystem dust;    // alpha dust puffs

    static ParticleSystem.EmitParams ep;

    void Awake() => I = this;

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

    public void Hit(Vector3 p, Vector3 dir)
    {
        for (int i = 0; i < 7; i++) Emit(goo, p, Spray(dir, 2f, 6f, 2.5f, 0.5f, 3f), Random.Range(0.12f, 0.26f), Random.Range(0.25f, 0.45f));
        Emit(pop, p - dir * 0.2f, Vector3.zero, 0.9f, 0.07f);
    }

    public void Death(Vector3 p, Vector3 dir)
    {
        for (int i = 0; i < 22; i++) Emit(goo, p, Spray(dir, 1f, 8f, 4f, 0.5f, 4f), Random.Range(0.18f, 0.4f), Random.Range(0.3f, 0.6f));
        for (int i = 0; i < 8; i++) Emit(chunks, p, Spray(dir, 2f, 6f, 3f, 2f, 5f), Random.Range(0.15f, 0.3f), Random.Range(0.5f, 0.8f));
        Emit(pop, p, Vector3.zero, 2.2f, 0.1f);
    }

    public void Spark(Vector3 p, Vector3 normal)
    {
        for (int i = 0; i < 6; i++) Emit(sparks, p, (normal + Random.insideUnitSphere * 0.8f) * Random.Range(3f, 7f), Random.Range(0.06f, 0.12f), Random.Range(0.1f, 0.2f));
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
