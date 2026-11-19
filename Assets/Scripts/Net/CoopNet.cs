using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Co-op event hub (Game scene, C2/C3). One named message ("coop.ev") carries small events (<see cref="CoopEv"/>):
/// host → clients: blasts to show, pickups, boss projectiles, objectives (issue / progress / complete), radio, team
/// over, wave-break respawn; client → host: barrel hits, picked-up pickups (relayed to everyone).
/// The host stays the authority: clients only show what it decided and report what their own soldier did.
/// Tests drive <see cref="Handle"/> directly (no network).
/// </summary>
public class CoopNet : MonoBehaviour
{
    public static CoopNet I { get; private set; }
    const string Msg = "coop.ev";

    /// <summary>This game is a co-op client (puppets, remote directors).</summary>
    public static bool IsClient => NetZombies.I && NetZombies.I.IsClient;
    public static bool IsHost => GameMode.IsCoop && NetZombies.I && NetZombies.I.IsHost;

    public float progressRate = 5f;
    public int Sent { get; private set; }
    public int Received { get; private set; }

    byte[] buf = new byte[256];
    byte[] rbuf = new byte[256];
    bool registered, hooked;
    float progressT;

    void Awake() => I = this;

    void Start()
    {
        if (!GameMode.IsCoop) { enabled = false; return; }
        var nm = NetworkManager.Singleton;
        if (nm && nm.IsListening && nm.CustomMessagingManager != null)
        {
            nm.CustomMessagingManager.RegisterNamedMessageHandler(Msg, OnMessage);
            registered = true;
        }
        Hook();
    }

    void OnDestroy()
    {
        var nm = NetworkManager.Singleton;
        if (registered && nm && nm.CustomMessagingManager != null) nm.CustomMessagingManager.UnregisterNamedMessageHandler(Msg);
        Unhook();
        if (I == this) I = null;
    }

    /// <summary>Subscribes to the game's events (host: what to send; client: what to report).</summary>
    public void Hook()
    {
        if (hooked) return;
        hooked = true;
        Explosions.Broadcast += OnExplosion;
        ButcherZombie.Thrown += OnThrown;
        if (Pickups.I) { Pickups.I.Dropped += OnDropped; Pickups.I.Taken += OnTaken; }
        var od = ObjectiveDirector.I;
        if (od) { od.Issued += OnIssued; od.Completed += OnCompleted; od.Radio += OnRadio; }
        if (WaveDirector.I) WaveDirector.I.WaveCleared += OnWaveCleared;
    }

    void Unhook()
    {
        if (!hooked) return;
        hooked = false;
        Explosions.Broadcast -= OnExplosion;
        ButcherZombie.Thrown -= OnThrown;
        if (Pickups.I) { Pickups.I.Dropped -= OnDropped; Pickups.I.Taken -= OnTaken; }
        var od = ObjectiveDirector.I;
        if (od) { od.Issued -= OnIssued; od.Completed -= OnCompleted; od.Radio -= OnRadio; }
        if (WaveDirector.I) WaveDirector.I.WaveCleared -= OnWaveCleared;
    }

    void Update()
    {
        if (!IsHost) return;
        var od = ObjectiveDirector.I;
        if (!od || !od.HasActive) return;
        progressT -= Time.unscaledDeltaTime;
        if (progressT > 0f) return;
        progressT = 1f / progressRate;
        var w = Begin(CoopEv.ObjProgress);
        w.Bool(od.HasActive); w.F32(od.Held); w.Bool(od.PlayerInZone);
        SendToClients(w);
    }

    // ---------------------------------------------------------------- sending
    NetWriter Begin(CoopEv ev)
    {
        var w = new NetWriter(buf);
        w.U8((byte)ev);
        return w;
    }

    /// <summary>Last encoded message (tests replay it into a "client").</summary>
    public byte[] LastBuffer => buf;
    public int LastLength { get; private set; }

    void SendToClients(NetWriter w)
    {
        buf = w.buf;
        LastLength = w.len;
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsListening || !nm.IsServer || nm.ConnectedClientsIds.Count < 2) return;
        using (var fw = new FastBufferWriter(w.len + 8, Allocator.Temp))
        {
            fw.WriteValueSafe(w.len);
            fw.WriteBytesSafe(w.buf, w.len);
            nm.CustomMessagingManager.SendNamedMessageToAll(Msg, fw, NetworkDelivery.ReliableSequenced);
        }
        Sent++;
    }

    void SendToHost(NetWriter w)
    {
        buf = w.buf;
        LastLength = w.len;
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsListening || nm.IsServer) return;
        using (var fw = new FastBufferWriter(w.len + 8, Allocator.Temp))
        {
            fw.WriteValueSafe(w.len);
            fw.WriteBytesSafe(w.buf, w.len);
            nm.CustomMessagingManager.SendNamedMessage(Msg, NetworkManager.ServerClientId, fw, NetworkDelivery.ReliableSequenced);
        }
        Sent++;
    }

    void OnExplosion(Vector3 pos, float radius, SfxId sfx, float shake, bool big)
    {
        if (!IsHost) return;
        var w = Begin(CoopEv.Explosion);
        w.XZ(pos); w.F32(radius); w.U16((ushort)sfx); w.F32(shake); w.Bool(big);
        SendToClients(w);
    }

    void OnThrown(ButcherZombie b, int index, Vector3 from, Vector3 to, float flight)
    {
        if (!IsHost || index < 0) return;
        var w = Begin(CoopEv.BossThrow);
        w.U16(b.NetId); w.U8((byte)index); w.V3(from); w.XZ(to); w.F32(flight);
        SendToClients(w);
    }

    void OnDropped(ushort id, Vector3 pos, bool ammo)
    {
        if (!IsHost) return;
        var w = Begin(CoopEv.PickupSpawn);
        w.U16(id); w.XZ(pos); w.Bool(ammo);
        SendToClients(w);
    }

    void OnTaken(ushort id)
    {
        var w = Begin(CoopEv.PickupTaken);
        w.U16(id);
        if (IsHost) SendToClients(w);
        else if (IsClient) SendToHost(w);
    }

    /// <summary>Client: my bullet hit a barrel — the host's barrel takes it.</summary>
    public void ReportBarrelHit(Vector3 pos, float damage)
    {
        var w = Begin(CoopEv.BarrelHit);
        w.XZ(pos); w.F32(damage);
        SendToHost(w);
    }

    void OnIssued(ObjectiveStep step)
    {
        if (!IsHost) return;
        var od = ObjectiveDirector.I;
        var w = Begin(CoopEv.ObjIssue);
        w.U8((byte)step.kind); w.U8((byte)step.site); w.F32(step.radius); w.F32(step.hold); w.Str(step.briefKey);
        w.V3(od.TargetGlobal); w.I32(od.SiteChunk.x); w.I32(od.SiteChunk.y); w.Bool(od.SiteForced);
        SendToClients(w);
    }

    void OnCompleted(ObjectiveStep step)
    {
        if (!IsHost) return;
        var od = ObjectiveDirector.I;
        var w = Begin(CoopEv.ObjComplete);
        w.U8((byte)step.kind); w.U16((ushort)Mathf.Clamp(od.LastXp, 0, ushort.MaxValue)); w.I32(od.LastBonus);
        w.U8((byte)Mathf.Clamp(od.Helicopters, 0, 255)); w.Bool(od.Plan.Finished);
        SendToClients(w);
    }

    void OnRadio(RadioMsg m)
    {
        if (!IsHost) return;
        var w = Begin(CoopEv.Radio);
        w.Str(m.key); w.Str(m.argKey); w.I32(m.num);
        SendToClients(w);
    }

    void OnWaveCleared(int wave)
    {
        if (!IsHost) return;
        SendToClients(Begin(CoopEv.Respawn));
        if (CoopTeam.I) CoopTeam.I.Respawn();
    }

    /// <summary>Host: nobody is standing — the run ends for everyone.</summary>
    public void SendTeamOver() => SendToClients(Begin(CoopEv.TeamOver));

    // ---------------------------------------------------------------- receiving
    void OnMessage(ulong sender, FastBufferReader r)
    {
        r.ReadValueSafe(out int len);
        if (len <= 0 || len > 1 << 16) return;
        if (rbuf.Length < len) rbuf = new byte[len + 64];
        r.ReadBytesSafe(ref rbuf, len);
        Handle(rbuf, len, sender);
    }

    /// <summary>Applies one event (from the network, or from a test).</summary>
    public void Handle(byte[] data, int len, ulong sender)
    {
        var r = new NetReader(data, len);
        var ev = (CoopEv)r.U8();
        bool host = !IsClient;
        Received++;
        switch (ev)
        {
            case CoopEv.Explosion:
            {
                Vector3 p = r.XZ(); float rad = r.F32(); var sfx = (SfxId)r.U16(); float shake = r.F32(); bool big = r.Bool();
                if (!r.Ok || host) return;
                if (sfx == SfxId.BarrelExplode) { var b = ExplosiveBarrel.Near(p, 0.8f); if (b) b.RemoteExploded(); }
                Explosions.Visual(p, rad, sfx, shake, big);
                return;
            }
            case CoopEv.BossThrow:
            {
                ushort id = r.U16(); int idx = r.U8(); Vector3 from = r.V3(); Vector3 to = r.XZ(); float flight = r.F32();
                if (!r.Ok || host) return;
                var bz = NetZombies.I ? NetZombies.I.PuppetById(id) as ButcherZombie : null;
                if (bz) bz.PuppetThrow(idx, from, to, flight);
                return;
            }
            case CoopEv.PickupSpawn:
            {
                ushort id = r.U16(); Vector3 p = r.XZ(); bool ammo = r.Bool();
                if (!r.Ok || host) return;
                if (Pickups.I) Pickups.I.RemoteSpawn(id, p, ammo);
                return;
            }
            case CoopEv.PickupTaken:
            {
                ushort id = r.U16();
                if (!r.Ok) return;
                if (Pickups.I) Pickups.I.RemoteTaken(id);
                if (host && IsHost)
                {
                    // Relay a client's pick-up to everyone else.
                    var w = Begin(CoopEv.PickupTaken);
                    w.U16(id);
                    SendToClients(w);
                }
                return;
            }
            case CoopEv.BarrelHit:
            {
                Vector3 p = r.XZ(); float dmg = r.F32();
                if (!r.Ok || !host) return;
                var b = ExplosiveBarrel.Near(p, 0.8f);
                if (b) b.Hit(dmg);
                return;
            }
            case CoopEv.ObjIssue:
            {
                var step = new ObjectiveStep { kind = (ObjectiveKind)r.U8(), site = (ChunkType)r.U8(), radius = r.F32(), hold = r.F32(), briefKey = r.Str() };
                Vector3 target = r.V3(); var chunk = new Vector2Int(r.I32(), r.I32()); bool forced = r.Bool();
                if (!r.Ok || host || !ObjectiveDirector.I) return;
                ObjectiveDirector.I.RemoteIssue(step, target, chunk, forced);
                return;
            }
            case CoopEv.ObjProgress:
            {
                bool active = r.Bool(); float held = r.F32(); bool inZone = r.Bool();
                if (!r.Ok || host || !ObjectiveDirector.I) return;
                ObjectiveDirector.I.RemoteProgress(active, held, inZone);
                return;
            }
            case CoopEv.ObjComplete:
            {
                var kind = (ObjectiveKind)r.U8(); int xp = r.U16(); int bonus = r.I32(); int helis = r.U8(); bool finished = r.Bool();
                if (!r.Ok || host || !ObjectiveDirector.I) return;
                ObjectiveDirector.I.RemoteComplete(kind, xp, bonus, helis, finished);
                return;
            }
            case CoopEv.Radio:
            {
                var m = new RadioMsg { key = r.Str(), argKey = r.Str(), num = r.I32() };
                if (!r.Ok || host || !ObjectiveDirector.I) return;
                if (string.IsNullOrEmpty(m.argKey)) m.argKey = null;
                ObjectiveDirector.I.RemoteRadio(m);
                return;
            }
            case CoopEv.TeamOver:
                if (host) return;
                if (CoopTeam.I) CoopTeam.I.TeamOver();
                return;
            case CoopEv.Respawn:
                if (host) return;
                if (CoopTeam.I) CoopTeam.I.Respawn();
                return;
        }
    }
}
