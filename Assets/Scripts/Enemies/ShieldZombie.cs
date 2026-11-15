using UnityEngine;

/// <summary>
/// Kalkanlı — a hulking mutant carrying a chained car door. Slow, very tough; bullets from the front
/// glance off the door (metal ricochet + sparks), the back and sides take normal damage.
/// It turns slowly, so flanking it is the answer.
/// </summary>
public class ShieldZombie : Zombie
{
    [Tooltip("Half-angle of the protected front arc (degrees).")]
    public float shieldArc = 65f;

    float blockCos;

    protected override void Awake()
    {
        base.Awake();
        blockCos = Mathf.Cos(shieldArc * Mathf.Deg2Rad);
    }

    protected override SfxId SpawnSfx => SfxId.ShieldSpawn;
    protected override float KnockScale => 0.25f;

    public override Vector3 AimPoint => transform.position + Vector3.up * 1.2f;

    /// <summary>True when a shot travelling along <paramref name="dir"/> would hit the shield.</summary>
    public bool FacesShot(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return false;
        return Vector3.Dot(-dir.normalized, transform.forward) > blockCos;
    }

    protected override bool Deflects(Vector3 dir, Vector3 point)
    {
        if (!FacesShot(dir)) return false;
        Vector3 n = transform.forward;
        Vector3 p = point + n * 0.1f;
        if (Fx.I) Fx.I.Spark(p, Vector3.Reflect(dir.normalized, n) + Vector3.up * 0.3f);
        GameAudio.Play(SfxId.ShieldBlock, p);
        punch = Mathf.Max(punch, 0.3f);
        return true;
    }
}
