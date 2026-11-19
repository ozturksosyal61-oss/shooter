using UnityEngine;

/// <summary>
/// A soldier zombies can target: the local <see cref="PlayerController"/> or a co-op teammate's avatar
/// (<see cref="remote"/> set → damage goes to that player's own game over the network).
/// </summary>
public class SquadMember : MonoBehaviour
{
    public PlayerController local;
    public NetPlayer remote;

    public Vector3 Position => transform.position;
    public bool IsLocal => local != null;
    public bool IsDead => local ? local.IsDead : remote == null || remote.IsDead;

    void OnEnable() { if (!Squad.Members.Contains(this)) Squad.Members.Add(this); }
    void OnDisable() => Squad.Members.Remove(this);

    public void Damage(float amount, Vector3 dir)
    {
        if (local) local.TakeDamage(amount, dir);
        else if (remote) remote.SendDamage(amount, dir);
    }
}
