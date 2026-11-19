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
    public byte flags;        // 1 firing · 2 reloading · 4 dead · 8 vaulting
    public byte weapon, costume;

    public bool Firing => (flags & 1) != 0;
    public bool Reloading => (flags & 2) != 0;
    public bool Dead => (flags & 4) != 0;
    public bool Vaulting => (flags & 8) != 0;

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
    }

    public bool Equals(PlayerNetState o) =>
        pos == o.pos && yaw == o.yaw && move == o.move && speed == o.speed && hp01 == o.hp01 && flags == o.flags && weapon == o.weapon && costume == o.costume;
}

/// <summary>
/// Network player (one per connected client, spawned by Netcode, lives across scenes). In the Game scene the owner's
/// copy mirrors the local <see cref="PlayerController"/> into <see cref="State"/>; everyone else gets an avatar
/// (the Player prefab with its controller / weapon / input stripped) driven by that state, registered as a
/// <see cref="SquadMember"/> so the host's zombies chase it. Zombie hits on a teammate travel to their owner
/// (<see cref="DamageRpc"/>); a client's bullet hits travel to the host (<see cref="HitRpc"/>), which owns all damage.
/// </summary>
public class NetPlayer : NetworkBehaviour
{
    public static NetPlayer Local { get; private set; }
    public static event Action<NetPlayer> AvatarCreated;

    [Tooltip("Player prefab: instantiated (stripped) as the teammate's avatar.")]
    public GameObject avatarPrefab;
    public Color mateRing = new Color(0.45f, 1f, 0.35f, 1f);

    public readonly NetworkVariable<PlayerNetState> State = new NetworkVariable<PlayerNetState>(
        default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    public bool IsDead => State.Value.Dead;
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
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this) Local = null;
        if (avatar) Destroy(avatar.gameObject);
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
        // Snap when far (spawn / teleport), otherwise smooth toward the replicated position.
        if ((s.pos - p).sqrMagnitude > 25f) avatar.position = s.pos;
        else avatar.position = Vector3.Lerp(p, s.pos, 1f - Mathf.Exp(-14f * dt));
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
            if (s.Firing && Time.time >= flashUntil)
            {
                // Cosmetic fire: muzzle flash + recoil (C2: tracers).
                flashUntil = Time.time + 0.1f;
                avatarRig.Kick();
                var slot = CurrentSlot();
                if (slot != null && Fx.I && slot.muzzle) Fx.I.Muzzle(slot.muzzle.position, avatar.forward);
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
    /// <summary>Host side: a zombie hit this teammate → their own game applies it.</summary>
    public void SendDamage(float amount, Vector3 dir)
    {
        if (avatarFlash) avatarFlash.Flash(0.07f);
        DamageRpc(amount, dir);
    }

    [Rpc(SendTo.Owner)]
    void DamageRpc(float amount, Vector3 dir)
    {
        var gm = GameManager.I;
        if (gm && gm.player) gm.player.TakeDamage(amount, dir);
    }

    /// <summary>Client side: my bullet hit a zombie (puppet) → the host applies the damage.</summary>
    public void SendHit(ushort zombieId, float amount, bool crit, Vector3 dir, Vector3 point) => HitRpc(zombieId, amount, crit, dir, point);

    [Rpc(SendTo.Server)]
    void HitRpc(ushort zombieId, float amount, bool crit, Vector3 dir, Vector3 point)
    {
        if (NetZombies.I) NetZombies.I.ApplyRemoteHit(zombieId, amount, crit, dir, point);
    }
}
