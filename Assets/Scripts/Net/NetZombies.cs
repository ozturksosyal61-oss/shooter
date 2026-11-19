using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Co-op zombie + wave replication (Game scene). Host: every 1/15 s packs all active zombies and the wave state
/// (<see cref="ZombieSnapshot"/>) and sends it to the clients; deaths go out at once on a reliable channel so clients
/// play the death. Client: its own spawner / waves / objectives / boss are off; zombies are puppets of the host's
/// (<see cref="Zombie.Puppet"/>), created / moved / removed from the snapshots. A client's bullet hits are forwarded
/// to the host (<see cref="NetPlayer.SendHit"/>) which owns every zombie's health.
/// </summary>
public class NetZombies : MonoBehaviour
{
    public static NetZombies I { get; private set; }

    public ZombieSpawner spawner;
    public float sendRate = 15f;

    const string SnapMsg = "coop.zs", KillMsg = "coop.zk";

    /// <summary>True on a co-op client (puppet zombies); tests can force it without a network.</summary>
    public bool IsClient { get; private set; }
    public bool IsHost { get; private set; }
    public int SnapshotsSent { get; private set; }
    public int SnapshotsReceived { get; private set; }
    public int Puppets => puppets.Count;

    ushort nextId = 1;
    float sendT;
    byte[] buf = new byte[ZombieSnapshot.Size(128)];
    ZombieNetState[] states = new ZombieNetState[128];
    readonly Dictionary<ushort, Zombie> byId = new Dictionary<ushort, Zombie>(128);
    readonly Dictionary<ushort, Zombie> puppets = new Dictionary<ushort, Zombie>(128);
    readonly HashSet<ushort> seen = new HashSet<ushort>();
    readonly List<ushort> gone = new List<ushort>(32);
    bool registered;

    void Awake() => I = this;

    void Start()
    {
        if (!GameMode.IsCoop) { enabled = false; return; }
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsListening) { enabled = false; return; }
        IsHost = nm.IsServer;
        if (!IsHost) BecomeClient();
        nm.CustomMessagingManager.RegisterNamedMessageHandler(SnapMsg, OnSnapshot);
        nm.CustomMessagingManager.RegisterNamedMessageHandler(KillMsg, OnKill);
        registered = true;
        if (IsHost && GameManager.I) GameManager.I.Killed += OnHostKill;
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (registered && nm && nm.CustomMessagingManager != null)
        {
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(SnapMsg);
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(KillMsg);
        }
        if (GameManager.I) GameManager.I.Killed -= OnHostKill;
        if (I == this) I = null;
    }

    /// <summary>Client mode: the host simulates; this game only shows puppets (tests call it without a network).</summary>
    public void BecomeClient()
    {
        IsClient = true;
        if (spawner) spawner.enabled = false;
        if (WaveDirector.I) WaveDirector.I.Remote = true;
        if (BossDirector.I) BossDirector.I.enabled = false;
        if (ObjectiveDirector.I) ObjectiveDirector.I.enabled = false;
        for (int i = Zombie.Active.Count - 1; i >= 0; i--) Zombie.Active[i].Despawn();
    }

    void Update()
    {
        if (!IsHost) return;
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsListening || nm.ConnectedClientsIds.Count < 2) return;
        sendT -= Time.unscaledDeltaTime;
        if (sendT > 0f) return;
        sendT = 1f / sendRate;
        int len = BuildSnapshot();
        using (var w = new FastBufferWriter(len + 8, Allocator.Temp))
        {
            w.WriteValueSafe(len);
            w.WriteBytesSafe(buf, len);
            nm.CustomMessagingManager.SendNamedMessageToAll(SnapMsg, w, NetworkDelivery.ReliableFragmentedSequenced);
        }
        SnapshotsSent++;
    }

    // ---------------------------------------------------------------- host
    /// <summary>Host: gives every zombie a network id (called by <see cref="Zombie.Spawn"/>).</summary>
    public ushort Register(Zombie z)
    {
        if (nextId == 0) nextId = 1;
        ushort id = nextId++;
        byId[id] = z;
        return id;
    }

    public int BuildSnapshot()
    {
        var act = Zombie.Active;
        if (states.Length < act.Count) states = new ZombieNetState[act.Count + 32];
        int n = 0;
        for (int i = 0; i < act.Count; i++)
        {
            var z = act[i];
            if (z.NetId == 0) continue;
            states[n++] = z.ToNetState();
        }
        int size = ZombieSnapshot.Size(n);
        if (buf.Length < size) buf = new byte[size + 512];
        var wd = WaveDirector.I;
        var w = new WaveNetState { wave = wd ? wd.Wave : 0, inBreak = wd && wd.InBreak, timeLeft = wd ? wd.PhaseTimeLeft : 0f };
        return ZombieSnapshot.Write(buf, w, states, n);
    }

    public byte[] Buffer => buf;

    void OnHostKill(Zombie z)
    {
        if (z.NetId == 0) return;
        byId.Remove(z.NetId);
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsListening || nm.ConnectedClientsIds.Count < 2) return;
        using (var w = new FastBufferWriter(32, Allocator.Temp))
        {
            w.WriteValueSafe(z.NetId);
            w.WriteValueSafe(z.LastHitDir);
            nm.CustomMessagingManager.SendNamedMessageToAll(KillMsg, w, NetworkDelivery.ReliableSequenced);
        }
    }

    /// <summary>Host: a client's bullet hit zombie <paramref name="id"/>.</summary>
    public void ApplyRemoteHit(ushort id, float amount, bool crit, Vector3 dir, Vector3 point)
    {
        if (!byId.TryGetValue(id, out var z) || !z || z.NetId != id || !z.Damageable) return;
        z.TakeHit(amount, crit, dir, point);
    }

    // ---------------------------------------------------------------- client
    void OnSnapshot(ulong sender, FastBufferReader r)
    {
        if (IsHost) return;
        r.ReadValueSafe(out int len);
        if (len <= 0 || len > 1 << 20) return;
        if (buf.Length < len) buf = new byte[len + 512];
        r.ReadBytesSafe(ref buf, len);
        ApplySnapshot(buf, len);
    }

    public void ApplySnapshot(byte[] data, int len)
    {
        int n = ZombieSnapshot.Read(data, len, out var w, ref states);
        if (n < 0) return;
        SnapshotsReceived++;
        if (WaveDirector.I) WaveDirector.I.ApplyRemote(w.wave, w.inBreak, w.timeLeft);
        seen.Clear();
        for (int i = 0; i < n; i++)
        {
            var s = states[i];
            seen.Add(s.id);
            if (!puppets.TryGetValue(s.id, out var z) || !z || z.NetId != s.id || !z.Puppet)
            {
                z = spawner ? spawner.SpawnPuppet(s) : null;
                if (!z) continue;
                puppets[s.id] = z;
            }
            z.SetNetState(s);
        }
        gone.Clear();
        foreach (var kv in puppets) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var id in gone)
        {
            var z = puppets[id];
            puppets.Remove(id);
            // Still standing = recycled on the host (no death message): just vanish.
            if (z && z.Puppet && z.NetId == id && z.Damageable) z.Despawn();
        }
    }

    void OnKill(ulong sender, FastBufferReader r)
    {
        if (IsHost) return;
        r.ReadValueSafe(out ushort id);
        r.ReadValueSafe(out Vector3 dir);
        ApplyKill(id, dir);
    }

    public void ApplyKill(ushort id, Vector3 dir)
    {
        if (!puppets.TryGetValue(id, out var z)) return;
        puppets.Remove(id);
        if (z && z.Puppet && z.NetId == id) z.PuppetDie(dir);
    }

    /// <summary>Client: my bullet hit a puppet → tell the host (it owns the damage).</summary>
    public static void ReportHit(Zombie z, float amount, bool crit, Vector3 dir, Vector3 point)
    {
        if (NetPlayer.Local) NetPlayer.Local.SendHit(z.NetId, amount, crit, dir, point);
    }
}
