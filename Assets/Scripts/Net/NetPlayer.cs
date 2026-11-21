using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>A soldier's replicated state (written by its owner, ~tick rate).</summary>
public struct PlayerNetState : INetworkSerializable, IEquatable<PlayerNetState>
{
    public Vector3 pos;
    public float yaw;
    public Vector3 move;      // world move input (0..1 magnitude)
    public float speed;
    public float hp01;
    public byte flags;        // 1 firing · 2 reloading · 4 dead (down or out) · 8 vaulting · 16 downed (revivable) · 32 grabbed
    public byte weapon, costume;
    public int score;
    public ushort kills;
    public byte revive;       // 0..255 revive progress while downed
    public byte bleed;        // 0..255 bleed-out time left while downed

    public bool Firing => (flags & 1) != 0;
    public bool Reloading => (flags & 2) != 0;
    public bool Dead => (flags & 4) != 0;
    public bool Vaulting => (flags & 8) != 0;
    public bool Downed => (flags & 16) != 0;
    public bool Grabbed => (flags & 32) != 0;

    public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
    {
        s.SerializeValue(ref pos);
        s.SerializeValue(ref yaw);
        s.SerializeValue(ref move);
        s.SerializeValue(ref speed);
        s.SerializeValue(ref hp01);
        s.SerializeValue(ref flags);
        s.SerializeValue(ref weapon);
        s.SerializeValue(ref costume);
        s.SerializeValue(ref score);
        s.SerializeValue(ref kills);
        s.SerializeValue(ref revive);
        s.SerializeValue(ref bleed);
    }

    public bool Equals(PlayerNetState o) =>
        pos == o.pos && yaw == o.yaw && move == o.move && speed == o.speed && hp01 == o.hp01 && flags == o.flags && weapon == o.weapon
        && costume == o.costume && score == o.score && kills == o.kills && revive == o.revive && bleed == o.bleed;
}

/// <summary>
/// Network player (one per connected client, spawned by Netcode, lives across scenes). In the Game scene the owner's
/// copy mirrors the local <see cref="PlayerController"/> into <see cref="State"/>; everyone else gets an avatar
/// (the Player prefab with its controller / weapon / input stripped) driven by that state, registered as a
/// <see cref="SquadMember"/> so the host's zombies chase it. Zombie hits on a teammate travel to their owner
/// (<see cref="DamageRpc"/>, also slows / burns / tongue grabs); a client's bullet hits travel to the host
/// (<see cref="HitRpc"/>), which owns all damage. Teammates' shots show as cosmetic tracers. Score, kills and the
/// downed / revive state ride on <see cref="State"/> (team score, team screen, revive markers).
/// </summary>
public class NetPlayer : NetworkBehaviour
{
    public static NetPlayer Local { get; private set; }
    public static event Action<NetPlayer> AvatarCreated;
    /// <summary>A teammate left (their NetPlayer despawned during the run).</summary>
    public static event Action<NetPlayer> Left;
    /// <summary>Every spawned network player, ordered by client id (slot 0 = host).</summary>
    public static readonly System.Collections.Generic.List<NetPlayer> All = new System.Collections.Generic.List<NetPlayer>(4);

    /// <summary>Stable slot (0 = host): name "OYUNCU n", tongue target byte.</summary>
    public int Slot => All.IndexOf(this);

    public static NetPlayer BySlot(int slot) => slot >= 0 && slot < All.Count ? All[slot] : null;

    [Tooltip("Player prefab: instantiated (stripped) as the teammate's avatar.")]
    public GameObject avatarPrefab;
    public Color mateRing = new Color(0.45f, 1f, 0.35f, 1f);

    public readonly NetworkVariable<PlayerNetState> State = new NetworkVariable<PlayerNetState>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public bool IsDead => State.Value.Dead;
    /// <summary>Seconds of velocity lead on a teammate's avatar.</summary>
    public const float AvatarLead = 0.07f;
    public Transform Avatar => avatar;

    PlayerController localPlayer;
    Transform avatar;
    SoldierRig avatarRig;
    WeaponLoadout.Slot[] avatarSlots;
    Material[] avatarCostumes;
    GameObject[] avatarModels;
    GameObject avatarDefaultModel, avatarActiveModel;
    SkinnedMeshRenderer avatarBody;
    HitFlash avatarFlash;
    Transform avatarFlashT;
    byte shownWeapon = 255, shownCostume = 255;
    bool shownDead;
    float flashUntil;

    public override void OnNetworkSpawn()
    {
        DontDestroyOnLoad(gameObject);
        if (IsOwner) Local = this;
        if (!All.Contains(this)) All.Add(this);
        All.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner && GameMode.IsCoop && GameManager.I) Left?.Invoke(this); // before the slot goes away (name)
        if (Local == this) Local = null;
        All.Remove(this);
        if (avatar) Destroy(avatar.gameObject);
    }

    public override void OnDestroy()
    {
        All.Remove(this);
        if (Local == this) Local = null;
        base.OnDestroy();
    }

    void Update()
    {
        if (!IsSpawned || !GameMode.IsCoop) return;
        var gm = GameManager.I;
        if (!gm || !gm.player) return;
        if (IsOwner) WriteLocal(gm.player);
        else DriveAvatar(Time.deltaTime);
    }

    // ---------------------------------------------------------------- owner
    void WriteLocal(PlayerController p)
    {
        if (localPlayer != p)
        {
            localPlayer = p;
            // Spread the squad a little so nobody spawns inside a teammate.
            if (OwnerClientId != 0) p.Warp(p.transform.position + new Vector3(1.6f * (OwnerClientId % 2 == 0 ? -1 : 1), 0f, -1.2f * OwnerClientId));
        }
        var w = p.weapon;
        var loadout = p.GetComponent<WeaponLoadout>();
        byte f = 0;
        if (w && w.IsFiring) f |= 1;
        if (w && w.IsReloading) f |= 2;
        if (p.IsDead) f |= 4;
        if (p.IsVaulting) f |= 8;
        if (p.IsGrabbed) f |= 32;
        var team = CoopTeam.I;
        if (team && team.Downed) f |= 16;
        var gm = GameManager.I;
        var s = new PlayerNetState
        {
            pos = p.transform.position,
            yaw = p.transform.eulerAngles.y,
            move = p.MoveInput,
            speed = p.MoveInput.magnitude * p.moveSpeed,
            hp01 = p.Health / Mathf.Max(1f, p.maxHealth),
            flags = f,
            weapon = (byte)(loadout ? loadout.Current : 0),
            costume = (byte)(loadout ? loadout.Costume : 0),
            score = gm ? gm.Score : 0,
            kills = (ushort)Mathf.Min(gm ? gm.Kills : 0, ushort.MaxValue),
            revive = (byte)(team ? Mathf.RoundToInt(team.Revive01 * 255f) : 0),
            bleed = (byte)(team ? Mathf.RoundToInt(team.Bleed01 * 255f) : 0),
        };
        if (!s.Equals(State.Value)) State.Value = s;
    }

    // ---------------------------------------------------------------- teammates
    void DriveAvatar(float dt)
    {
        if (!avatar) CreateAvatar();
        if (!avatar) return;
        var s = State.Value;
        Vector3 p = avatar.position;
        // Aim slightly ahead along the replicated move (≈ one tick + smoothing lag) so the teammate doesn't trail.
        Vector3 goal = s.pos;
        if (!s.Dead && !s.Vaulting && s.move.sqrMagnitude > 0.01f) goal += s.move.normalized * (s.speed * AvatarLead);
        // Snap when far (spawn / teleport), otherwise smooth toward the replicated position.
        if ((s.pos - p).sqrMagnitude > 25f) avatar.position = s.pos;
        else avatar.position = Vector3.Lerp(p, goal, 1f - Mathf.Exp(-14f * dt));
        avatar.rotation = Quaternion.Slerp(avatar.rotation, Quaternion.Euler(0f, s.yaw, 0f), 1f - Mathf.Exp(-16f * dt));

        if (s.weapon != shownWeapon) ShowWeapon((WeaponId)s.weapon);
        if (s.costume != shownCostume) ShowCostume((CostumeId)s.costume);
        if (s.Dead != shownDead)
        {
            shownDead = s.Dead;
            if (avatarRig) { if (s.Dead) avatarRig.Die(-avatar.forward); else avatarRig.Revive(); }
        }
        if (avatarRig && !s.Dead)
        {
            avatarRig.Tick(dt, s.Vaulting ? Vector3.zero : s.move, s.Vaulting ? 0f : s.speed, s.Firing || s.Reloading, s.Vaulting);
            if (s.Firing && !s.Reloading && Time.time >= flashUntil)
            {
                // Cosmetic fire at the weapon's rate: muzzle flash, recoil, sound, tracers (damage comes as HitRpc).
                var def = Economy.Weapon((WeaponId)Mathf.Clamp(s.weapon, 0, Economy.Weapons.Length - 1));
                flashUntil = Time.time + 1f / Mathf.Max(1f, def.fireRate);
                avatarRig.Kick();
                var slot = CurrentSlot();
                Vector3 from = slot != null && slot.muzzle ? slot.muzzle.position : avatar.position + Vector3.up * 1.2f;
                if (Fx.I) Fx.I.Muzzle(from, avatar.forward);
                GameAudio.Play(def.sfx, from, 1f, 0.55f);
                var w = GameManager.I.player ? GameManager.I.player.weapon : null;
                if (w)
                    for (int i = 0; i < Mathf.Max(1, def.pellets); i++)
                    {
                        float spread = def.pellets > 1 ? def.pelletSpread : def.spread;
                        Vector3 dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-spread, spread), 0f) * avatar.forward;
                        w.SpawnCosmetic(from, dir, def.bulletSpeed, def.range);
                    }
            }
        }
    }

    WeaponLoadout.Slot CurrentSlot()
    {
        if (avatarSlots == null) return null;
        foreach (var sl in avatarSlots) if (sl.id == (WeaponId)shownWeapon) return sl;
        return null;
    }

    void CreateAvatar()
    {
        if (!avatarPrefab) return;
        // Instantiate under an inactive holder so nothing Awakes before the gameplay parts are stripped.
        var holder = new GameObject("MateHolder");
        holder.SetActive(false);
        var go = Instantiate(avatarPrefab, State.Value.pos, Quaternion.Euler(0f, State.Value.yaw, 0f), holder.transform);
        go.name = "Teammate" + OwnerClientId;
        var loadout = go.GetComponent<WeaponLoadout>();
        if (loadout)
        {
            avatarSlots = loadout.slots; avatarCostumes = loadout.costumeMaterials; avatarBody = loadout.body;
            avatarModels = loadout.costumeModels; avatarDefaultModel = avatarActiveModel = loadout.defaultModel;
            DestroyImmediate(loadout);
        }
        foreach (var c in go.GetComponents<PlayerController>()) DestroyImmediate(c);
        foreach (var c in go.GetComponents<Weapon>()) DestroyImmediate(c);
        foreach (var c in go.GetComponents<CharacterController>()) DestroyImmediate(c);
        avatarRig = avatarDefaultModel ? avatarDefaultModel.GetComponent<SoldierRig>() : go.GetComponentInChildren<SoldierRig>(true);
        avatarFlash = go.GetComponent<HitFlash>();
        var member = go.GetComponent<SquadMember>() ?? go.AddComponent<SquadMember>();
        member.local = null;
        member.remote = this;
        var ring = go.transform.Find("Ring");
        if (ring && ring.TryGetComponent<Renderer>(out var rr))
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", mateRing);
            rr.SetPropertyBlock(mpb);
        }
        go.transform.SetParent(null, true);
        Destroy(holder);
        go.SetActive(true);
        avatar = go.transform;
        shownWeapon = shownCostume = 255;
        shownDead = false;
        AvatarCreated?.Invoke(this);
    }

    void ShowWeapon(WeaponId id)
    {
        shownWeapon = (byte)id;
        if (avatarSlots == null) return;
        foreach (var sl in avatarSlots)
        {
            bool on = sl.id == id;
            if (sl.root) sl.root.gameObject.SetActive(on);
            if (on && avatarRig) { avatarRig.weaponRoot = sl.root; avatarRig.rightGrip = sl.rightGrip; avatarRig.leftGrip = sl.leftGrip; }
        }
    }

    void ShowCostume(CostumeId c)
    {
        shownCostume = (byte)c;
        // Whole-character costume: swap the model, move the rig + weapon grips over.
        var target = avatarModels != null && (int)c < avatarModels.Length && avatarModels[(int)c] ? avatarModels[(int)c] : avatarDefaultModel;
        if (target && target != avatarActiveModel)
        {
            if (avatarModels != null) foreach (var mo in avatarModels) if (mo) mo.SetActive(mo == target);
            if (avatarDefaultModel) avatarDefaultModel.SetActive(avatarDefaultModel == target);
            avatarActiveModel = target;
            avatarRig = target.GetComponent<SoldierRig>();
            shownWeapon = 255; // re-bind the grips on the new rig
            if (avatarFlash) avatarFlash.SetRenderers(target.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        }
        if (target != avatarDefaultModel) return;
        if (avatarCostumes == null || (int)c >= avatarCostumes.Length || !avatarCostumes[(int)c]) return;
        if (avatarBody) avatarBody.sharedMaterial = avatarCostumes[(int)c];
        if (avatarFlash) avatarFlash.SetBaseMaterial(avatarCostumes[(int)c]);
    }

    // ---------------------------------------------------------------- damage over the network
    /// <summary>Host side: a zombie / boss / blast hit this teammate → their own game applies it.</summary>
    public void SendDamage(float amount, Vector3 dir, float knock = 7f)
    {
        if (avatarFlash) avatarFlash.Flash(0.07f);
        DamageRpc(amount, dir, knock);
    }

    [Rpc(SendTo.Owner)]
    void DamageRpc(float amount, Vector3 dir, float knock)
    {
        var gm = GameManager.I;
        if (gm && gm.player) gm.player.TakeDamage(amount, dir, knock);
    }

    public void SendSlow(float factor, float seconds) => SlowRpc(factor, seconds);

    [Rpc(SendTo.Owner)]
    void SlowRpc(float factor, float seconds)
    {
        var gm = GameManager.I;
        if (gm && gm.player) gm.player.Slow(factor, seconds);
    }

    public void SendBurn(float amount) => BurnRpc(amount);

    [Rpc(SendTo.Owner)]
    void BurnRpc(float amount)
    {
        var gm = GameManager.I;
        if (gm && gm.player) gm.player.Burn(amount);
    }

    /// <summary>Host: a Dilci's tongue caught (or let go of) this teammate.</summary>
    public void SendGrab(TongueZombie by, bool on) { if (by) GrabRpc(by.NetId, on); }

    [Rpc(SendTo.Owner)]
    void GrabRpc(ushort zombieId, bool on)
    {
        var gm = GameManager.I;
        var p = gm ? gm.player : null;
        if (!p) return;
        var t = NetZombies.I ? NetZombies.I.PuppetById(zombieId) as TongueZombie : null;
        if (on) { if (t) p.Grab(t); }
        else if (t) p.ReleaseGrab(t);
        else if (p.Grabber) p.ReleaseGrab(p.Grabber);
    }

    public const byte HitExplosive = 1, HitTongue = 2;

    /// <summary>Client side: my bullet / blast hit a zombie (puppet) → the host applies the damage, credit to me.</summary>
    public void SendHit(ushort zombieId, float amount, bool crit, Vector3 dir, Vector3 point, byte kind = 0) => HitRpc(zombieId, amount, crit, dir, point, kind);

    [Rpc(SendTo.Server)]
    void HitRpc(ushort zombieId, float amount, bool crit, Vector3 dir, Vector3 point, byte kind)
    {
        if (NetZombies.I) NetZombies.I.ApplyRemoteHit(zombieId, amount, crit, dir, point, kind, OwnerClientId);
    }
}
