using UnityEngine;

/// <summary>
/// A soldier zombies can target: the local <see cref="PlayerController"/> or a co-op teammate's avatar
/// (<see cref="remote"/> set → damage, slows, burns and tongue grabs go to that player's own game over the network).
/// Everything that hurts soldiers on the host (zombies, bosses, blasts, acid) goes through here.
/// </summary>
public class SquadMember : MonoBehaviour
{
    public PlayerController local;
    public NetPlayer remote;

    public Vector3 Position => transform.position;
    public bool IsLocal => local != null;
    public bool IsDead => local ? local.IsDead : remote == null || remote.IsDead;
    public bool IsVaulting => local ? local.IsVaulting : remote != null && remote.State.Value.Vaulting;
    public bool IsGrabbed => local ? local.IsGrabbed : remote != null && remote.State.Value.Grabbed;
    public float Health01 => local ? local.Health / Mathf.Max(1f, local.maxHealth) : remote != null ? remote.State.Value.hp01 : 0f;

    float pendingBurn, burnFlushT, slowSentUntil;

    void OnEnable() { if (!Squad.Members.Contains(this)) Squad.Members.Add(this); }
    void OnDisable() => Squad.Members.Remove(this);

    public void Damage(float amount, Vector3 dir) => TakeDamage(amount, dir, 7f);

    public void TakeDamage(float amount, Vector3 dir, float knock)
    {
        if (local) local.TakeDamage(amount, dir, knock);
        else if (remote) remote.SendDamage(amount, dir, knock);
    }

    public void Slow(float factor, float seconds)
    {
        if (local) { local.Slow(factor, seconds); return; }
        if (!remote) return;
        // Continuous slows (shock ring, acid) arrive every frame: send only when it would run out soon.
        float until = Time.time + seconds;
        if (until <= slowSentUntil - 0.05f) return;
        float len = Mathf.Max(seconds, 0.35f);
        slowSentUntil = Time.time + len;
        remote.SendSlow(factor, len);
    }

    public void Burn(float amount)
    {
        if (local) { local.Burn(amount); return; }
        pendingBurn += amount; // batched: a few messages a second instead of one per frame
    }

    public void Grab(TongueZombie by)
    {
        if (local) local.Grab(by);
        else if (remote) remote.SendGrab(by, true);
    }

    public void ReleaseGrab(TongueZombie by)
    {
        if (local) local.ReleaseGrab(by);
        else if (remote) remote.SendGrab(by, false);
    }

    void Update()
    {
        if (!remote || pendingBurn <= 0f || Time.time < burnFlushT) return;
        burnFlushT = Time.time + 0.25f;
        remote.SendBurn(pendingBurn);
        pendingBurn = 0f;
    }
}
