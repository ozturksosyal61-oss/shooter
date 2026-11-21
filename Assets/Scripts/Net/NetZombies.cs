using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Co-op zombie + wave replication (Game scene). Host: every 1/15 s packs all active zombies (with their special
/// phase), the wave state and the boss's fight state (<see cref="ZombieSnapshot"/>) and sends it to the clients;
/// deaths go out at once on a reliable channel with the kill credit (who landed the blow gets the score / combo / XP
/// on their own game). Client: its own spawner / waves are off, objectives / boss run in remote mode; zombies are
/// puppets of the host's (<see cref="Zombie.Puppet"/>), created / moved / removed from the snapshots. A client's hits
/// are forwarded to the host (<see cref="NetPlayer.SendHit"/>) which owns every zombie's health.
/// Snapshots travel <b>unreliable</b>, split into parts of at most <see cref="PerPart"/> zombies (each under the MTU):
/// a lost packet is simply superseded by the next one instead of stalling every reliable message behind it (the old
/// fragmented-reliable 1.5 KB snapshots at wave 9+ froze teammates / zombies on lossy links and could overflow the
/// reliable queue, which closes the connection). Puppets missing from a complete snapshot vanish; with lost parts they
/// time out after <see cref="StaleSnapshots"/> newer snapshots.
/// </summary>
public class NetZombies : MonoBehaviour
{
    public static NetZombies I { get; private set; }

    public ZombieSpawner spawner;
    public float sendRate = 15f;

    const string SnapMsg = "coop.zs", KillMsg = "coop.zk";
    /// <summary>Zombies per snapshot part: 7 + 56 x 18 + 27 + 8 header = 1.05 KB, under Netcode's non-fragmented limit.</summary>
    public const int PerPart = 56;
    /// <summary>A puppet missing from this many newer snapshots (~1 s) is dropped (its parts were lost).</summary>
    public const int StaleSnapshots = 15;

    /// <summary>True on a co-op client (puppet zombies); tests can force it without a network.</summary>
    public bool IsClient { get; private set; }
    public bool IsHost { get; private set; }
    public int SnapshotsSent { get; private set; }
    public int SnapshotsReceived { get; private set; }
    public int Puppets => puppets.Count;

    /// <summary>This game's network client id (0 = host, also in single player / tests).</summary>
    public static ulong LocalClientId
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (I && I.IsClient && I.ForcedLocalId.HasValue) return I.ForcedLocalId.Value;
            return nm && nm.IsListening ? nm.LocalClientId : 0;
        }
    }

    /// <summary>Tests: the client id this in-process "client" pretends to have.</summary>
    public ulong? ForcedLocalId { get; set; }

    ushort nextId = 1;
    float sendT;
    byte[] buf = new byte[ZombieSnapshot.Size(128)];
    ZombieNetState[] states = new ZombieNetState[128];
    readonly Dictionary<ushort, Zombie> byId = new Dictionary<ushort, Zombie>(128);
    readonly Dictionary<ushort, Zombie> puppets = new Dictionary<ushort, Zombie>(128);
    readonly HashSet<ushort> seen = new HashSet<ushort>();
    readonly List<ushort> gone = new List<ushort>(32);
    readonly Dictionary<ushort, int> lastSeen = new Dictionary<ushort, int>(128);
    /// <summary>Recently killed ids: a late unreliable part must not bring a dead zombie back as a ghost.</summary>
    readonly Dictionary<ushort, float> killedAt = new Dictionary<ushort, float>(64);
    ushort sendSeq, curSeq;
    int partsMask, partsWanted;
    bool haveSeq;
    float staleT;
    bool registered;

    void Awake() => I = this;

    void Start()
    {
        if (!GameMode.IsCoop) { enabled = false; return; }
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsListening) { enabled = false; return; }
        IsHost = nm.IsServer;
        ModeRules.CoopPlayers = Mathf.Max(1, IsHost ? nm.ConnectedClientsIds.Count : NetPlayer.All.Count);
        if (!IsHost) BecomeClient();
        nm.CustomMessagingManager.RegisterNamedMessageHandler(SnapMsg, OnSnapshot);
        nm.CustomMessagingManager.RegisterNamedMessageHandler(KillMsg, OnKill);
        registered = true;
        if (IsHost && GameManager.I) GameManager.I.TeamKilled += OnHostKill;
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (registered && nm && nm.CustomMessagingManager != null)
        {
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(SnapMsg);
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(KillMsg);
        }
        if (GameManager.I) GameManager.I.TeamKilled -= OnHostKill;
        if (I == this) I = null;
    }

    /// <summary>Client mode: the host simulates; this game only shows puppets (tests call it without a network).</summary>
    public void BecomeClient()
    {
        IsClient = true;
        if (spawner) spawner.enabled = false;
        if (WaveDirector.I) WaveDirector.I.Remote = true;
        if (BossDirector.I) BossDirector.I.Remote = true;
        if (ObjectiveDirector.I) ObjectiveDirector.I.Remote = true;
        for (int i = Zombie.Active.Count - 1; i >= 0; i--) Zombie.Active[i].Despawn();
    }

    void Update()
    {
        if (IsClient) { DropStale(); if (spawner) spawner.TopUpPool(WaveDirector.I); return; }
        if (!IsHost) return;
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsListening || nm.ConnectedClientsIds.Count < 2) return;
        sendT -= Time.unscaledDeltaTime;
        if (sendT > 0f) return;
        sendT = 1f / sendRate;
        int n = CollectStates(out var wave, out var boss);
        int parts = Mathf.Max(1, (n + PerPart - 1) / PerPart);
        sendSeq++;
        for (int p = 0; p < parts; p++)
        {
            int start = p * PerPart, count = Mathf.Min(PerPart, n - start);
            int len = ZombieSnapshot.Write(buf, wave, states, start, count, p == 0 ? boss : null);
            using (var w = new FastBufferWriter(len + 12, Allocator.Temp))
            {
                w.WriteValueSafe(sendSeq);
                w.WriteValueSafe((byte)p);
                w.WriteValueSafe((byte)parts);
                w.WriteValueSafe(len);
                w.WriteBytesSafe(buf, len);
                nm.CustomMessagingManager.SendNamedMessageToAll(SnapMsg, w, NetworkDelivery.Unreliable);
            }
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

    /// <summary>Host: the whole snapshot in one buffer (tests; the network path sends it in parts).</summary>
    public int BuildSnapshot()
    {
        int n = CollectStates(out var w, out var boss);
        int size = ZombieSnapshot.Size(n);
        if (buf.Length < size) buf = new byte[size + 512];
        return ZombieSnapshot.Write(buf, w, states, n, boss);
    }

    int CollectStates(out WaveNetState w, out BossNetState? boss)
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
        int size = ZombieSnapshot.Size(Mathf.Min(n, PerPart));
        if (buf.Length < size) buf = new byte[size + 512];
        var wd = WaveDirector.I;
        w = new WaveNetState { wave = wd ? wd.Wave : 0, inBreak = wd && wd.InBreak, timeLeft = wd ? wd.PhaseTimeLeft : 0f };
        boss = null;
        var bd = BossDirector.I;
        if (bd && bd.BossAlive && bd.Boss is ButcherZombie b && b.NetId != 0) boss = b.ToBossNetState();
        return n;
    }

    public byte[] Buffer => buf;

    void OnHostKill(Zombie z)
    {
        if (z.NetId == 0) return;
        byId.Remove(z.NetId);
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsListening || nm.ConnectedClientsIds.Count < 2) return;
        using (var w = new FastBufferWriter(48, Allocator.Temp))
        {
            w.WriteValueSafe(z.NetId);
            w.WriteValueSafe(z.LastHitDir);
            w.WriteValueSafe(z.LastHitBy);
            nm.CustomMessagingManager.SendNamedMessageToAll(KillMsg, w, NetworkDelivery.ReliableSequenced);
        }
    }

    /// <summary>Host: a teammate's bullet / blast / tongue shot hit zombie <paramref name="id"/>; they get the credit.</summary>
    public void ApplyRemoteHit(ushort id, float amount, bool crit, Vector3 dir, Vector3 point, byte kind, ulong sender)
    {
        if (!byId.TryGetValue(id, out var z) || !z || z.NetId != id || !z.Damageable) return;
        if ((kind & NetPlayer.HitTongue) != 0)
        {
            if (z is TongueZombie t) t.HitTongue(amount, crit, dir, point);
            return;
        }
        z.PendingHitter = sender;
        z.TakeHit(amount, crit, dir, point, (kind & NetPlayer.HitExplosive) != 0);
        z.PendingHitter = null;
    }

    /// <summary>Host zombie by network id (null when gone).</summary>
    public Zombie HostById(ushort id) => byId.TryGetValue(id, out var z) && z && z.NetId == id ? z : null;

    // ---------------------------------------------------------------- client
    void OnSnapshot(ulong sender, FastBufferReader r)
    {
        if (IsHost) return;
        r.ReadValueSafe(out ushort seq);
        r.ReadValueSafe(out byte part);
        r.ReadValueSafe(out byte parts);
        r.ReadValueSafe(out int len);
        if (len <= 0 || len > 1 << 16 || parts == 0 || part >= parts || parts > 16) return;
        if (buf.Length < len) buf = new byte[len + 512];
        r.ReadBytesSafe(ref buf, len);
        ApplyPart(buf, len, seq, part, parts);
    }

    /// <summary>A complete snapshot in one buffer (tests; zombies missing from it vanish at once).</summary>
    public void ApplySnapshot(byte[] data, int len) => ApplyPart(data, len, (ushort)(curSeq + 1), 0, 1);

    /// <summary>Wrap-safe "a is newer than b" for 16-bit sequence numbers.</summary>
    public static bool SeqNewer(ushort a, ushort b) => (short)(a - b) > 0;

    void ApplyPart(byte[] data, int len, ushort seq, int part, int parts)
    {
        if (haveSeq && seq != curSeq && !SeqNewer(seq, curSeq)) return; // a late part of an older snapshot
        bool fresh = !haveSeq || seq != curSeq;
        int n = ZombieSnapshot.Read(data, len, out var w, ref states, out var boss);
        if (n < 0) return;
        if (fresh)
        {
            haveSeq = true;
            curSeq = seq;
            partsMask = 0;
            partsWanted = parts;
            seen.Clear();
            SnapshotsReceived++;
            if (WaveDirector.I) WaveDirector.I.ApplyRemote(w.wave, w.inBreak, w.timeLeft);
        }
        partsMask |= 1 << part;
        float now = Time.unscaledTime;
        for (int i = 0; i < n; i++)
        {
            var s = states[i];
            seen.Add(s.id);
            lastSeen[s.id] = SnapshotsReceived;
            if (!puppets.TryGetValue(s.id, out var z) || !z || z.NetId != s.id || !z.Puppet)
            {
                if (killedAt.TryGetValue(s.id, out float kt) && now - kt < 3f) continue;
                z = spawner ? spawner.SpawnPuppet(s) : null;
                if (!z) continue;
                puppets[s.id] = z;
                if (s.kind == ZombieKind.Boss && BossDirector.I) BossDirector.I.RemoteSpawned(z, (BossType)s.aux);
            }
            z.SetNetState(s);
        }
        if (boss.HasValue && puppets.TryGetValue(boss.Value.id, out var bz) && bz is ButcherZombie bb && bb.Puppet)
            bb.ApplyBossState(boss.Value);
        // Every part of this snapshot arrived: whoever is missing was recycled on the host.
        if (partsMask != (1 << partsWanted) - 1) return;
        gone.Clear();
        foreach (var kv in puppets) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
        RemovePuppets();
    }

    void RemovePuppets()
    {
        foreach (var id in gone)
        {
            var z = puppets[id];
            puppets.Remove(id);
            lastSeen.Remove(id);
            // Still standing = recycled on the host (no death message): just vanish.
            if (z && z.Puppet && z.NetId == id && z.Damageable) z.Despawn();
        }
        gone.Clear();
    }

    /// <summary>Client: drops puppets whose snapshot parts kept getting lost, and old kill records.</summary>
    void DropStale()
    {
        staleT -= Time.unscaledDeltaTime;
        if (staleT > 0f) return;
        staleT = 0.5f;
        float now = Time.unscaledTime;
        gone.Clear();
        foreach (var kv in puppets)
            if (lastSeen.TryGetValue(kv.Key, out int seenAt) && SnapshotsReceived - seenAt > StaleSnapshots) gone.Add(kv.Key);
        RemovePuppets();
        foreach (var kv in killedAt) if (now - kv.Value > 3f) gone.Add(kv.Key);
        foreach (var id in gone) killedAt.Remove(id);
        gone.Clear();
    }

    void OnKill(ulong sender, FastBufferReader r)
    {
        if (IsHost) return;
        r.ReadValueSafe(out ushort id);
        r.ReadValueSafe(out Vector3 dir);
        r.ReadValueSafe(out ulong killer);
        ApplyKill(id, dir, killer);
    }

    public void ApplyKill(ushort id, Vector3 dir) => ApplyKill(id, dir, ulong.MaxValue);

    /// <summary>Client: the host says zombie <paramref name="id"/> died; <paramref name="killer"/> gets the score / XP.</summary>
    public void ApplyKill(ushort id, Vector3 dir, ulong killer)
    {
        killedAt[id] = Time.unscaledTime;
        if (!puppets.TryGetValue(id, out var z)) return;
        puppets.Remove(id);
        lastSeen.Remove(id);
        if (!z || !z.Puppet || z.NetId != id) return;
        var gm = GameManager.I;
        if (gm) gm.RemoteKill(z, killer == LocalClientId);
        z.PuppetDie(dir);
    }

    /// <summary>Client puppet by network id (tongue grabs, tests).</summary>
    public Zombie PuppetById(ushort id) => puppets.TryGetValue(id, out var z) && z && z.NetId == id && z.Puppet ? z : null;

    /// <summary>Client: my bullet / blast hit a puppet → tell the host (it owns the damage).</summary>
    public static void ReportHit(Zombie z, float amount, bool crit, Vector3 dir, Vector3 point, byte kind = 0)
    {
        if (NetPlayer.Local) NetPlayer.Local.SendHit(z.NetId, amount, crit, dir, point, kind);
    }
}
