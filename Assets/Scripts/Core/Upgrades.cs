using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardId
{
    Pierce, FireRate, BigMag, FastReload, DoubleShot, Crit, KillBlast, MaxHealth,
    Lifesteal, MoveSpeed, Magnet, Shotgun, Turret, Grenade, BreatherHeal,
    // 1.1.0
    Ricochet, Shock, Frost, Mines, Drone, Adrenaline, Armor,
}

public enum Rarity { Common, Rare, Epic }

public enum Synergy { FlameShot, TurretMaster, Demolition, IronBlood, Deadeye, Storm, Minefield, SkyGuard }

public struct CardDef
{
    public CardId id;
    public string icon;
    public Rarity rarity;
    public int maxLevel;

    // Text comes from the Loc table: card.<id>.title, card.<id>.<level>, optional card.<id>.icon.
    public string Title => Loc.T("card." + id + ".title");
    /// <summary>Description of level <paramref name="level"/> (1-based, clamped).</summary>
    public string Desc(int level) => Loc.T("card." + id + "." + Mathf.Clamp(level, 1, maxLevel));
    public string Icon => Loc.Has("card." + id + ".icon") ? Loc.T("card." + id + ".icon") : icon;
}

/// <summary>
/// Roguelite upgrades for the current run: 15 cards with up to 3 levels each. Stat cards are recomputed from the
/// base values; active cards (shotgun, turret, grenades, kill blasts) run here.
/// </summary>
public class Upgrades : MonoBehaviour
{
    public static Upgrades I { get; private set; }

    public static readonly CardDef[] Cards =
    {
        Def(CardId.Pierce, ">>", Rarity.Rare, 3),
        Def(CardId.FireRate, "»", Rarity.Common, 3),
        Def(CardId.BigMag, "+10", Rarity.Common, 3),
        Def(CardId.FastReload, "R", Rarity.Common, 3),
        Def(CardId.DoubleShot, "II", Rarity.Epic, 3),
        Def(CardId.Crit, "!", Rarity.Common, 3),
        Def(CardId.KillBlast, "*", Rarity.Rare, 3),
        Def(CardId.MaxHealth, "+25", Rarity.Common, 3),
        Def(CardId.Lifesteal, "♥", Rarity.Rare, 3),
        Def(CardId.MoveSpeed, "HIZ", Rarity.Common, 3),
        Def(CardId.Magnet, "U", Rarity.Common, 3),
        Def(CardId.Shotgun, "PMP", Rarity.Epic, 3),
        Def(CardId.Turret, "TRT", Rarity.Epic, 3),
        Def(CardId.Grenade, "o", Rarity.Rare, 3),
        Def(CardId.BreatherHeal, "+", Rarity.Rare, 3),
        Def(CardId.Ricochet, "<>", Rarity.Rare, 3),
        Def(CardId.Shock, "~", Rarity.Epic, 3),
        Def(CardId.Frost, "*", Rarity.Rare, 3),
        Def(CardId.Mines, "M", Rarity.Rare, 3),
        Def(CardId.Drone, "D", Rarity.Epic, 3),
        Def(CardId.Adrenaline, "A", Rarity.Common, 3),
        Def(CardId.Armor, "Z", Rarity.Rare, 3),
    };

    static CardDef Def(CardId id, string icon, Rarity r, int max) =>
        new CardDef { id = id, icon = icon, rarity = r, maxLevel = max };

    public static CardDef Get(CardId id) => Cards[(int)id];

    public static readonly Color[] RarityColors =
    {
        new Color(0.78f, 0.8f, 0.82f),   // common
        new Color(0.25f, 0.6f, 1f),      // rare
        new Color(0.75f, 0.35f, 1f),     // epic
    };
    public static readonly string[] RarityKeys = { "rarity.common", "rarity.rare", "rarity.epic" };
    public static string RarityName(Rarity r) => Loc.T(RarityKeys[(int)r]);
    static readonly float[] RarityWeight = { 60f, 30f, 12f };

    [Tooltip("Card icon per CardId (generated vector art, see GameSceneBuilder.Icons).")]
    public Sprite[] cardSprites;

    public Sprite IconOf(CardId id) => cardSprites != null && (int)id < cardSprites.Length ? cardSprites[(int)id] : null;

    /// <summary>Cards that raise damage output; the first <see cref="GuaranteedDamagePicks"/> offers always contain one.</summary>
    public static readonly CardId[] DamageCards = { CardId.Pierce, CardId.FireRate, CardId.DoubleShot, CardId.Crit, CardId.KillBlast, CardId.Shotgun, CardId.Ricochet, CardId.Shock };
    /// <summary>3 since 1.1.0: the 22-card pool (7 new) thinned the damage cards in the first offers.</summary>
    public const int GuaranteedDamagePicks = 3;
    public static bool IsDamageCard(CardId id) => Array.IndexOf(DamageCards, id) >= 0;

    // ---------------------------------------------------------------- hidden synergies (pure, unit-tested)
    /// <summary>Two owned cards unlock a bonus (announced when it happens; not shown in the offers).</summary>
    public static readonly (Synergy s, CardId a, CardId b)[] SynergyPairs =
    {
        (Synergy.FlameShot, CardId.Shotgun, CardId.Pierce),       // shotgun pellets pierce + hit harder
        (Synergy.TurretMaster, CardId.Turret, CardId.FireRate),   // turrets fire faster
        (Synergy.Demolition, CardId.Grenade, CardId.KillBlast),   // grenades more often, bigger kill blasts
        (Synergy.IronBlood, CardId.Lifesteal, CardId.MaxHealth),  // double lifesteal
        (Synergy.Deadeye, CardId.Crit, CardId.DoubleShot),        // +10 % crit chance
        (Synergy.Storm, CardId.Shock, CardId.Ricochet),           // every bounce shocks, +1 shock target
        (Synergy.Minefield, CardId.Mines, CardId.KillBlast),      // +2 mines, bigger blasts
        (Synergy.SkyGuard, CardId.Drone, CardId.FireRate),        // drones fire faster + pierce
    };
    public static bool HasSynergy(int[] levels, Synergy s)
    {
        foreach (var p in SynergyPairs) if (p.s == s) return levels[(int)p.a] > 0 && levels[(int)p.b] > 0;
        return false;
    }
    public const float FlameShotDamageMul = 1.4f, TurretMasterRateMul = 1.4f, DemolitionGrenadeMul = 0.7f,
        DemolitionBlastMul = 1.3f, IronBloodLifestealMul = 2f, DeadeyeCrit = 0.1f,
        MinefieldRadiusMul = 1.4f, SkyGuardRateMul = 1.5f;
    public const int MinefieldExtraMines = 2, StormExtraTargets = 1;
    public bool Has(Synergy s) => HasSynergy(levels, s);
    public event Action<Synergy> SynergyUnlocked;

    [Header("Active cards")]
    public Turret turretPrefab;
    public GameObject grenadePrefab;
    public float shotgunRange = 7f;
    public int shotgunPellets = 7;
    public float shotgunCone = 36f;
    public float shotgunDamage = 16f;
    public float turretLife = 30f;
    public float turretEvery = 45f;
    [Tooltip("Level 3: deploy interval (shorter than the lifetime, so two turrets can stand at once).")]
    public float turretEveryMax = 24f;
    public float turretHealth = 160f;
    public float grenadeRadius = 3.2f;
    public float grenadeRange = 12f;
    public float killBlastRadius = 2.6f;
    [Header("1.1.0 cards")]
    public GameObject minePrefab;
    public GameObject dronePrefab;
    public float ricochetRange = 7f;
    public float shockRange = 4.5f;
    public float mineTrigger = 1.3f, mineRadius = 2.6f;
    public float droneRange = 9f, droneOrbit = 1.6f, droneHeight = 2.3f;

    public event Action<CardId, int> Changed;
    public int Picks { get; private set; }
    /// <summary>Cards in the order they were first taken (for the HUD icon row).</summary>
    public readonly List<CardId> Owned = new List<CardId>(16);

    readonly int[] levels = new int[Enum.GetValues(typeof(CardId)).Length];
    PlayerController player;
    Weapon weapon;
    float baseFireRate, baseReload, baseCrit, baseMaxHealth, baseMove, baseMagnet;
    int baseMag;
    float shotgunT, turretT, grenadeT;
    readonly Turret[] turrets = new Turret[2];
    Transform[] grenades;
    Vector3[] grenadeFrom, grenadeTo;
    float[] grenadeT0;
    readonly List<int> pickBuffer = new List<int>(16);
    readonly List<CardId> offerBuffer = new List<CardId>(3);

    void Awake() => I = this;

    void Start()
    {
        var gm = GameManager.I;
        player = gm.player;
        weapon = player.weapon;
        baseFireRate = weapon.fireRate;
        baseReload = weapon.reloadTime;
        baseCrit = weapon.critChance;
        baseMag = weapon.magazineSize;
        baseMaxHealth = player.maxHealth;
        baseMove = player.moveSpeed;
        baseMagnet = Pickups.I ? Pickups.I.magnetRadius : 5f;
        gm.Killed += OnKilled;
        if (WaveDirector.I) WaveDirector.I.WaveCleared += OnWaveCleared;
        if (turretPrefab)
        {
            for (int i = 0; i < turrets.Length; i++)
            {
                turrets[i] = Instantiate(turretPrefab);
                turrets[i].gameObject.SetActive(false);
            }
        }
        if (grenadePrefab)
        {
            grenades = new Transform[4];
            grenadeFrom = new Vector3[4]; grenadeTo = new Vector3[4]; grenadeT0 = new float[4];
            var root = new GameObject("Grenades").transform;
            for (int i = 0; i < grenades.Length; i++)
            {
                grenades[i] = Instantiate(grenadePrefab, root).transform;
                grenades[i].gameObject.SetActive(false);
            }
        }
    }

    void OnEnable() => FloatingOrigin.OnShift += OnShift;

    void OnDisable()
    {
        FloatingOrigin.OnShift -= OnShift;
    }

    void OnDestroy()
    {
        if (GameManager.I) GameManager.I.Killed -= OnKilled;
        if (WaveDirector.I) WaveDirector.I.WaveCleared -= OnWaveCleared;
    }

    void OnShift(Vector3 d)
    {
        if (grenades == null) return;
        for (int i = 0; i < grenades.Length; i++) { grenadeFrom[i] += d; grenadeTo[i] += d; }
    }

    public int Level(CardId id) => levels[(int)id];
    public bool CanTake(CardId id) => levels[(int)id] < Get(id).maxLevel;

    /// <summary>Up to <paramref name="count"/> distinct cards, rarity-weighted, among those not maxed.</summary>
    public List<CardId> Offer(int count, Rarity guaranteeAtLeast = Rarity.Common) =>
        Offer(levels, count, () => UnityEngine.Random.value, offerBuffer, pickBuffer, Picks < GuaranteedDamagePicks, guaranteeAtLeast);

    public static float RarityWeightOf(Rarity r) => RarityWeight[(int)r];

    /// <summary>Pure offer logic (unit-tested): <paramref name="rand01"/> supplies [0,1) values.</summary>
    public static List<CardId> Offer(int[] levels, int count, Func<float> rand01, List<CardId> into, List<int> scratch, bool guaranteeDamage = false,
        Rarity guaranteeAtLeast = Rarity.Common)
    {
        Draw(levels, count, rand01, into, scratch);
        if (guaranteeAtLeast > Rarity.Common && into.Count > 0)
        {
            // Boss reward: at least one card of the given rarity (falls back to the next lower one if all are maxed).
            for (var r = guaranteeAtLeast; r > Rarity.Common; r--)
            {
                int at = into.FindIndex(id => Get(id).rarity >= r);
                if (at >= 0)
                {
                    // Already offered: show it first.
                    var c = into[at]; into.RemoveAt(at); into.Insert(0, c);
                    break;
                }
                var keep = into[0];
                into.RemoveAt(0);
                int before = into.Count;
                DrawWhere(levels, rand01, into, scratch, i => Cards[i].rarity >= r);
                if (into.Count > before) { into.Insert(0, into[into.Count - 1]); into.RemoveAt(into.Count - 1); break; }
                into.Insert(0, keep);
            }
            return into;
        }
        if (guaranteeDamage && into.Count > 0)
        {
            bool has = false;
            foreach (var id in into) if (IsDamageCard(id)) { has = true; break; }
            if (!has)
            {
                // Swap the last card for a weighted damage card that isn't offered yet.
                var keep = into[into.Count - 1];
                into.RemoveAt(into.Count - 1);
                int before = into.Count;
                DrawDamage(levels, rand01, into, scratch);
                if (into.Count == before) into.Add(keep); // every damage card maxed: nothing to guarantee
            }
        }
        return into;
    }

    static void DrawDamage(int[] levels, Func<float> rand01, List<CardId> into, List<int> scratch) =>
        DrawWhere(levels, rand01, into, scratch, i => IsDamageCard(Cards[i].id));

    static void DrawWhere(int[] levels, Func<float> rand01, List<CardId> into, List<int> scratch, Func<int, bool> filter)
    {
        scratch.Clear();
        for (int i = 0; i < Cards.Length; i++)
            if (filter(i) && levels[i] < Cards[i].maxLevel && !into.Contains(Cards[i].id)) scratch.Add(i);
        if (scratch.Count == 0) return;
        float total = 0f;
        foreach (int i in scratch) total += RarityWeight[(int)Cards[i].rarity];
        float r = rand01() * total;
        int chosen = scratch[scratch.Count - 1];
        foreach (int i in scratch) { r -= RarityWeight[(int)Cards[i].rarity]; if (r <= 0f) { chosen = i; break; } }
        into.Add(Cards[chosen].id);
    }

    /// <summary>Card families: an offer mixes them so the three choices feel different (playtest-02 #6).</summary>
    public enum Group { Damage, Build, Utility }

    public static Group GroupOf(CardId id)
    {
        switch (id)
        {
            case CardId.Pierce: case CardId.FireRate: case CardId.DoubleShot: case CardId.Crit:
            case CardId.Ricochet: case CardId.Shock: return Group.Damage;
            case CardId.KillBlast: case CardId.Shotgun: case CardId.Turret: case CardId.Grenade:
            case CardId.Mines: case CardId.Drone: return Group.Build;
            default: return Group.Utility;
        }
    }

    /// <summary>Owned cards come back more often (directed builds); a family already in the offer is less likely.</summary>
    public const float OwnedBoost = 1.7f, SameGroupMul = 0.12f;

    /// <summary>Offer weight of card <paramref name="i"/> given what is already offered (pure).</summary>
    public static float OfferWeight(int[] levels, int i, List<CardId> offered)
    {
        float w = RarityWeight[(int)Cards[i].rarity];
        if (levels[i] > 0) w *= OwnedBoost;
        var g = GroupOf(Cards[i].id);
        foreach (var id in offered) if (GroupOf(id) == g) { w *= SameGroupMul; break; }
        return w;
    }

    static void Draw(int[] levels, int count, Func<float> rand01, List<CardId> into, List<int> scratch)
    {
        into.Clear();
        scratch.Clear();
        for (int i = 0; i < Cards.Length; i++) if (levels[(int)Cards[i].id] < Cards[i].maxLevel) scratch.Add(i);
        while (into.Count < count && scratch.Count > 0)
        {
            float total = 0f;
            foreach (int i in scratch) total += OfferWeight(levels, i, into);
            float r = rand01() * total;
            int chosen = scratch[scratch.Count - 1];
            foreach (int i in scratch)
            {
                r -= OfferWeight(levels, i, into);
                if (r <= 0f) { chosen = i; break; }
            }
            scratch.Remove(chosen);
            into.Add(Cards[chosen].id);
        }
    }

    // ---------------------------------------------------------------- stat formulas (pure, unit-tested)
    public static float FireRateMul(int l) => 1f + 0.2f * l;
    public static float ReloadMul(int l) => Mathf.Pow(0.8f, l);
    public static float CritChance(float baseCrit, int l) => Mathf.Min(0.9f, baseCrit + 0.1f * l);
    public static int MagazineSize(int baseMag, int l) => baseMag + 10 * l;
    public static float MaxHealth(float baseHp, int l) => baseHp + 25f * l;
    public static float MoveMul(int l) => 1f + 0.1f * l;
    public static float MagnetMul(int l) => 1f + 0.6f * l;
    public static float KillBlastChance(int l) => l > 0 ? 0.05f + 0.1f * l : 0f;
    public static float ShotgunInterval(int l) => l == 1 ? 1.6f : l == 2 ? 1.25f : 0.95f;
    /// <summary>At most one turret at a time; two with the card at level 3.</summary>
    public static int MaxTurrets(int l) => l >= 3 ? 2 : l > 0 ? 1 : 0;
    // 1.1.0
    /// <summary>Ricochet: bounces per kill chain = level; each bounce keeps this share of the damage.</summary>
    public static float RicochetDamageMul(int l) => 0.5f + 0.1f * l;
    /// <summary>Shock: crits arc to this many nearby zombies for <see cref="ShockDamageMul"/> of the hit.</summary>
    public static int ShockTargets(int l) => l > 0 ? 1 + l : 0;
    public const float ShockDamageMul = 0.45f;
    /// <summary>Shock also fires on this share of ordinary (non-crit) hits.</summary>
    public static float ShockChance(int l) => 0.06f * l;
    /// <summary>Frost: speed multiplier while chilled (bosses get half the slow).</summary>
    public static float FrostSlow(int l) => l <= 0 ? 1f : 0.85f - 0.1f * l;
    public const float FrostSeconds = 1.6f;
    public static int MaxMines(int l) => l > 0 ? 1 + l : 0;
    public static float MineDamage(int l) => 100f + 40f * (l - 1);
    public static int DroneCount(int l) => l >= 3 ? 2 : l > 0 ? 1 : 0;
    public static float DroneRate(int l) => 2.5f + 1f * l;
    public static float DroneDamage(int l) => 15f + 4f * (l - 1);
    /// <summary>Adrenaline: below <see cref="AdrenalineHp"/> of max health, fire rate × this and move ×1.15.</summary>
    public static float AdrenalineRate(int l) => 1.1f + 0.15f * l;
    public const float AdrenalineHp = 0.4f, AdrenalineMove = 1.15f;
    public static int ArmorCharges(int l) => l;

    int DeployedTurrets()
    {
        int n = 0;
        foreach (var t in turrets) if (t && t.IsDeployed) n++;
        return n;
    }

    public void Take(CardId id)
    {
        if (!CanTake(id)) return;
        int synBefore = 0;
        foreach (var sp in SynergyPairs) if (Has(sp.s)) synBefore |= 1 << (int)sp.s;
        int l = ++levels[(int)id];
        Picks++;
        if (l == 1) Owned.Add(id);
        Recompute();
        switch (id)
        {
            case CardId.MaxHealth: player.Heal(25f); break;
            case CardId.Turret: if (DeployedTurrets() < MaxTurrets(l)) DeployTurret(); break; // level 2 must not add a second turret
            case CardId.Shotgun: shotgunT = 0.3f; break;
            case CardId.Grenade: grenadeT = 1f; break;
            case CardId.Armor: armorCharges = ArmorCharges(l); break;
        }
        Changed?.Invoke(id, l);
        foreach (var sp in SynergyPairs)
            if (Has(sp.s) && (synBefore & (1 << (int)sp.s)) == 0) { SynergyUnlocked?.Invoke(sp.s); GameServices.Unlock(GameIds.AchSynergy); }
    }

    void Recompute()
    {
        int ad = Level(CardId.Adrenaline);
        float adrRate = adrenaline ? AdrenalineRate(ad) : 1f;
        weapon.fireRate = baseFireRate * FireRateMul(Level(CardId.FireRate)) * adrRate;
        weapon.ricochet = Level(CardId.Ricochet);
        weapon.frost = Level(CardId.Frost);
        weapon.shock = Level(CardId.Shock);
        weapon.reloadTime = baseReload * ReloadMul(Level(CardId.FastReload));
        weapon.critChance = Mathf.Min(0.9f, CritChance(baseCrit, Level(CardId.Crit)) + (Has(Synergy.Deadeye) ? DeadeyeCrit : 0f));
        weapon.magazineSize = MagazineSize(baseMag, Level(CardId.BigMag));
        weapon.pierce = Level(CardId.Pierce);
        weapon.extraBullets = Level(CardId.DoubleShot);
        player.maxHealth = MaxHealth(baseMaxHealth, Level(CardId.MaxHealth));
        player.moveSpeed = baseMove * MoveMul(Level(CardId.MoveSpeed)) * (adrenaline ? AdrenalineMove : 1f);
        if (Pickups.I) Pickups.I.magnetRadius = baseMagnet * MagnetMul(Level(CardId.Magnet));
    }

    // ---------------------------------------------------------------- passive hooks
    void OnKilled(Zombie z)
    {
        int ls = Level(CardId.Lifesteal);
        if (ls > 0) player.Regen(ls * (Has(Synergy.IronBlood) ? IronBloodLifestealMul : 1f));
        int kb = Level(CardId.KillBlast);
        if (kb > 0 && !z.KilledByExplosion && UnityEngine.Random.value < KillBlastChance(kb))
            Explosions.Explode(z.transform.position, killBlastRadius * (Has(Synergy.Demolition) ? DemolitionBlastMul : 1f), 45f + 10f * kb, 0f, SfxId.Explosion, 0.25f, z, false);
    }

    void OnWaveCleared(int wave)
    {
        if (Level(CardId.BreatherHeal) > 0 && !player.IsDead) player.Heal(player.maxHealth);
        armorCharges = ArmorCharges(Level(CardId.Armor));
    }

    // ---------------------------------------------------------------- active cards
    void Update()
    {
        var gm = GameManager.I;
        if (!gm || gm.IsGameOver || player.IsDead) return;
        float dt = Time.deltaTime;

        int bh = Level(CardId.BreatherHeal);
        if (bh >= 2 && WaveDirector.I && !WaveDirector.I.InBreak) player.Regen((bh == 2 ? 0.5f : 1f) * dt);

        int sg = Level(CardId.Shotgun);
        if (sg > 0 && (shotgunT -= dt) <= 0f)
        {
            var z = Zombie.FindNearest(player.transform.position, shotgunRange);
            if (z) { FireShotgun(z); shotgunT = ShotgunInterval(sg); }
            else shotgunT = 0.1f;
        }

        int tr = Level(CardId.Turret);
        if (tr > 0 && (turretT -= dt) <= 0f && DeployedTurrets() < MaxTurrets(tr)) DeployTurret();

        int gr = Level(CardId.Grenade);
        if (gr > 0 && (grenadeT -= dt) <= 0f)
        {
            if (TryThrowGrenade()) grenadeT = (7f - gr) * (Has(Synergy.Demolition) ? DemolitionGrenadeMul : 1f);
            else grenadeT = 0.3f;
        }
        UpdateGrenades();

        // 1.1.0
        int ad = Level(CardId.Adrenaline);
        bool adr = ad > 0 && player.Health < player.maxHealth * AdrenalineHp;
        if (adr != adrenaline) { adrenaline = adr; Recompute(); }
        UpdateMines(dt);
        UpdateDrones(dt);
        UpdateZaps(dt);
    }

    // ---------------------------------------------------------------- 1.1.0 cards
    bool adrenaline;
    int armorCharges;
    public int ArmorLeft => armorCharges;

    /// <summary>Armour plate: soaks one whole hit per charge (charges refill every wave). True = absorbed.</summary>
    public bool AbsorbHit(Vector3 at, Vector3 fromDir)
    {
        if (armorCharges <= 0) return false;
        armorCharges--;
        if (Fx.I) Fx.I.Spark(at, -fromDir.normalized);
        GameAudio.Play(SfxId.HitMetal, at, 0.8f, 1.2f);
        if (player && player.flash) player.flash.Flash(0.06f);
        GameManager.I.Shake(0.12f);
        return true;
    }

    /// <summary>Ricochet: a kill sends a bounce bullet to the nearest other zombie in range.</summary>
    public void Ricochet(Zombie from, Vector3 point, float damage, int bouncesLeft, int frostLevel, int shockLevel)
    {
        var next = NearestOther(point, ricochetRange, from);
        if (!next) return;
        Vector3 start = new Vector3(point.x, 1.15f, point.z);
        Vector3 d = next.AimPoint - start; d.y = 0f;
        if (d.sqrMagnitude < 0.01f) return;
        weapon.SpawnBounce(start, d.normalized, damage * RicochetDamageMul(Level(CardId.Ricochet)), bouncesLeft, frostLevel, shockLevel);
    }

    static Zombie NearestOther(Vector3 p, float range, Zombie except)
    {
        Zombie best = null; float bd = range * range;
        for (int i = 0; i < Zombie.Active.Count; i++)
        {
            var z = Zombie.Active[i];
            if (z == except || !z.Targetable) continue;
            Vector3 d = z.transform.position - p; d.y = 0f;
            float m = d.sqrMagnitude;
            if (m < bd) { bd = m; best = z; }
        }
        return best;
    }

    readonly Zombie[] shockBuf = new Zombie[8];

    /// <summary>Shock: arcs from <paramref name="from"/> to the nearest zombies around it (no further chains).</summary>
    public void Shock(Zombie from, float damage, Vector3 point, int level)
    {
        int n = ShockTargets(level) + (Has(Synergy.Storm) ? StormExtraTargets : 0);
        n = Mathf.Min(n, shockBuf.Length);
        int found = 0;
        float r2 = shockRange * shockRange;
        for (int k = 0; k < n; k++)
        {
            Zombie best = null; float bd = r2;
            for (int i = 0; i < Zombie.Active.Count; i++)
            {
                var z = Zombie.Active[i];
                if (z == from || !z.Targetable || System.Array.IndexOf(shockBuf, z, 0, found) >= 0) continue;
                Vector3 d = z.transform.position - point; d.y = 0f;
                float m = d.sqrMagnitude;
                if (m < bd) { bd = m; best = z; }
            }
            if (!best) break;
            shockBuf[found++] = best;
        }
        Vector3 a = new Vector3(point.x, 1.1f, point.z);
        for (int k = 0; k < found; k++)
        {
            var z = shockBuf[k];
            Vector3 b = z.AimPoint;
            Vector3 dir = b - a; dir.y = 0f;
            Zap(a, b);
            z.TakeHit(damage * ShockDamageMul, false, dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward, b);
            a = b;
        }
        for (int k = 0; k < found; k++) shockBuf[k] = null;
        if (found > 0) GameAudio.Play(SfxId.HitMetal, point, 1.6f, 0.5f);
    }

    // Shock visuals: a few pooled jagged lines that fade out.
    LineRenderer[] zaps;
    float[] zapT;
    int nextZap;
    const float ZapLife = 0.14f;

    void Zap(Vector3 a, Vector3 b)
    {
        if (zaps == null)
        {
            zaps = new LineRenderer[6]; zapT = new float[6];
            var lib = CosmeticLibrary.I;
            var mat = lib ? lib.TracerMat(TracerId.Plasma, 0) : null;
            var root = new GameObject("ShockZaps").transform;
            for (int i = 0; i < zaps.Length; i++)
            {
                var lr = new GameObject("Zap" + i).AddComponent<LineRenderer>();
                lr.transform.SetParent(root, false);
                lr.positionCount = 5; lr.useWorldSpace = true; lr.sharedMaterial = mat;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
                lr.numCapVertices = 0; lr.textureMode = LineTextureMode.Stretch;
                lr.enabled = false;
                zaps[i] = lr;
            }
        }
        var l = zaps[nextZap]; zapT[nextZap] = ZapLife; nextZap = (nextZap + 1) % zaps.Length;
        Vector3 side = Vector3.Cross((b - a).normalized, Vector3.up);
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            Vector3 p = Vector3.Lerp(a, b, t);
            if (i > 0 && i < 4) p += side * UnityEngine.Random.Range(-0.35f, 0.35f) + Vector3.up * UnityEngine.Random.Range(-0.2f, 0.2f);
            l.SetPosition(i, p);
        }
        l.enabled = true;
    }

    void UpdateZaps(float dt)
    {
        if (zaps == null) return;
        for (int i = 0; i < zaps.Length; i++)
        {
            if (zapT[i] <= 0f) continue;
            zapT[i] -= dt;
            float w = Mathf.Max(0f, zapT[i] / ZapLife) * 0.16f;
            zaps[i].startWidth = zaps[i].endWidth = w;
            if (zapT[i] <= 0f) zaps[i].enabled = false;
        }
    }

    // Mines: one drops at your feet when a reload starts; armed after a moment, a zombie stepping close sets it off.
    Transform[] mines;
    float[] mineArm;
    int mineNext;
    bool wasReloading;
    float mineCheckT;

    int MineCap => MaxMines(Level(CardId.Mines)) + (Has(Synergy.Minefield) ? MinefieldExtraMines : 0);

    void UpdateMines(float dt)
    {
        int l = Level(CardId.Mines);
        bool rl = weapon.IsReloading;
        if (l > 0 && rl && !wasReloading) DropMine();
        wasReloading = rl;
        if (mines == null) return;
        mineCheckT -= dt;
        bool check = mineCheckT <= 0f;
        if (check) mineCheckT = 0.08f;
        float trig2 = mineTrigger * mineTrigger;
        for (int i = 0; i < mines.Length; i++)
        {
            var m = mines[i];
            if (!m.gameObject.activeSelf) continue;
            mineArm[i] -= dt;
            // Blink: the red light flashes faster once armed.
            var lt = m.childCount > 1 ? m.GetChild(1) : null;
            if (lt) lt.gameObject.SetActive(Mathf.Repeat(Time.time * (mineArm[i] > 0f ? 2f : 4f), 1f) < 0.5f);
            if (!check || mineArm[i] > 0f) continue;
            Vector3 mp = m.position;
            for (int k = 0; k < Zombie.Active.Count; k++)
            {
                var z = Zombie.Active[k];
                if (!z.Targetable) continue;
                Vector3 d = z.transform.position - mp; d.y = 0f;
                if (d.sqrMagnitude > trig2) continue;
                m.gameObject.SetActive(false);
                float r = mineRadius * (Has(Synergy.Minefield) ? MinefieldRadiusMul : 1f);
                Explosions.Explode(mp, r, MineDamage(l) * weapon.DamageScale, 0f, SfxId.Explosion, 0.3f, null, false);
                break;
            }
        }
    }

    void DropMine()
    {
        if (!minePrefab) return;
        if (mines == null)
        {
            mines = new Transform[MaxMines(3) + MinefieldExtraMines]; mineArm = new float[mines.Length];
            var root = new GameObject("Mines").transform;
            for (int i = 0; i < mines.Length; i++) { mines[i] = Instantiate(minePrefab, root).transform; mines[i].gameObject.SetActive(false); }
        }
        int cap = Mathf.Min(MineCap, mines.Length);
        int slot = -1;
        for (int i = 0; i < cap; i++) if (!mines[i].gameObject.activeSelf) { slot = i; break; }
        if (slot < 0) { slot = mineNext % cap; mineNext++; } // all out: the oldest moves
        Vector3 p = player.transform.position; p.y = 0f;
        mines[slot].position = p;
        mines[slot].gameObject.SetActive(true);
        mineArm[slot] = 0.7f;
        GameAudio.Play(SfxId.GrenadeThrow, p, 0.7f, 0.6f);
    }

    // Drones: orbit the soldier and shoot the nearest zombie.
    Transform[] drones;
    float[] droneT;
    float droneAngle;

    void UpdateDrones(float dt)
    {
        int l = Level(CardId.Drone);
        if (l <= 0 || !dronePrefab) return;
        if (drones == null)
        {
            drones = new Transform[2]; droneT = new float[2];
            for (int i = 0; i < 2; i++) { drones[i] = Instantiate(dronePrefab).transform; drones[i].gameObject.SetActive(false); }
        }
        int count = DroneCount(l);
        droneAngle += 100f * dt;
        Vector3 c = player.transform.position;
        bool sky = Has(Synergy.SkyGuard);
        float rate = DroneRate(l) * (sky ? SkyGuardRateMul : 1f);
        for (int i = 0; i < drones.Length; i++)
        {
            var d = drones[i];
            bool on = i < count;
            if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
            if (!on) continue;
            float a = (droneAngle + i * 180f) * Mathf.Deg2Rad;
            Vector3 pos = c + new Vector3(Mathf.Cos(a) * droneOrbit, droneHeight + Mathf.Sin(Time.time * 3f + i) * 0.12f, Mathf.Sin(a) * droneOrbit);
            d.position = pos;
            if (d.childCount > 0) d.GetChild(0).Rotate(0f, 1440f * dt, 0f, Space.Self);
            droneT[i] -= dt;
            if (droneT[i] > 0f) continue;
            var z = Zombie.FindNearest(pos, droneRange);
            if (!z) { droneT[i] = 0.15f; continue; }
            droneT[i] = 1f / rate;
            Vector3 aim = z.AimPoint - pos;
            d.rotation = Quaternion.LookRotation(new Vector3(aim.x, 0f, aim.z));
            weapon.SpawnBullet(pos, pos, aim.normalized, 52f, DroneDamage(l) * weapon.DamageScale, UnityEngine.Random.value < weapon.critChance * 0.5f, 0.3f, sky ? 1 : 0);
            GameAudio.Play(SfxId.ShootSmg, pos, 1.35f, 0.3f);
        }
    }

    void FireShotgun(Zombie target)
    {
        Vector3 p = player.transform.position;
        Vector3 aim = target.transform.position - p; aim.y = 0f; aim.Normalize();
        Vector3 start = p + Vector3.up * 1.1f + aim * 0.5f;
        bool flame = Has(Synergy.FlameShot);
        float dmg = shotgunDamage * weapon.DamageScale * (flame ? FlameShotDamageMul : 1f);
        for (int i = 0; i < shotgunPellets; i++)
        {
            float a = Mathf.Lerp(-shotgunCone * 0.5f, shotgunCone * 0.5f, i / (shotgunPellets - 1f)) + UnityEngine.Random.Range(-3f, 3f);
            Vector3 d = Quaternion.Euler(0f, a, 0f) * aim;
            weapon.SpawnBullet(start, p + Vector3.up * 1.1f, d, 48f, dmg, UnityEngine.Random.value < weapon.critChance, 0.2f, flame ? 1 + Level(CardId.Pierce) : 0);
        }
        if (Fx.I) Fx.I.Muzzle(start, aim);
        GameManager.I.Shake(0.12f);
        if (GameManager.I.cameraRig) GameManager.I.cameraRig.Kick(-aim, 0.2f);
        GameAudio.Play(SfxId.Shotgun, start);
    }

    void DeployTurret()
    {
        Turret turret = null;
        foreach (var t in turrets) if (t && !t.IsDeployed) { turret = t; break; }
        if (!turret) return;
        Vector3 p = player.transform.position;
        Vector3 spot = p - player.transform.forward * 1.4f;
        if (Physics.CheckSphere(spot + Vector3.up * 0.6f, 0.5f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) spot = p;
        spot.y = 0f;
        int l = Level(CardId.Turret);
        turret.Deploy(spot, turretLife, (6f + 2.5f * (l - 1)) * (Has(Synergy.TurretMaster) ? TurretMasterRateMul : 1f), 14f + 3f * (l - 1), turretHealth + 40f * (l - 1), weapon);
        turretT = l >= 3 ? turretEveryMax : turretEvery;
    }

    bool TryThrowGrenade()
    {
        if (grenades == null) return false;
        int slot = -1;
        for (int i = 0; i < grenades.Length; i++) if (!grenades[i].gameObject.activeSelf) { slot = i; break; }
        if (slot < 0) return false;
        // Aim at the zombie with the most neighbours (within range).
        Vector3 p = player.transform.position;
        Zombie best = null; int bestN = 0;
        float r2 = grenadeRange * grenadeRange;
        for (int i = 0; i < Zombie.Active.Count; i++)
        {
            var z = Zombie.Active[i];
            if (!z.Targetable) continue;
            Vector3 d = z.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude > r2 || d.sqrMagnitude < 9f) continue;
            int n = 0;
            for (int j = 0; j < Zombie.Active.Count; j++)
                if ((Zombie.Active[j].transform.position - z.transform.position).sqrMagnitude < grenadeRadius * grenadeRadius) n++;
            if (n > bestN) { bestN = n; best = z; }
        }
        if (!best) return false;
        grenadeFrom[slot] = p + Vector3.up * 1.4f;
        grenadeTo[slot] = best.transform.position;
        grenadeT0[slot] = Time.time;
        grenades[slot].position = grenadeFrom[slot];
        grenades[slot].gameObject.SetActive(true);
        GameAudio.Play(SfxId.GrenadeThrow, p);
        return true;
    }

    void UpdateGrenades()
    {
        if (grenades == null) return;
        const float flight = 0.6f;
        for (int i = 0; i < grenades.Length; i++)
        {
            var g = grenades[i];
            if (!g.gameObject.activeSelf) continue;
            float t = (Time.time - grenadeT0[i]) / flight;
            Vector3 pos = Vector3.Lerp(grenadeFrom[i], grenadeTo[i], t);
            pos.y = Mathf.Lerp(grenadeFrom[i].y, 0.2f, t) + Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 2.5f;
            g.position = pos;
            g.Rotate(720f * Time.deltaTime, 0f, 0f);
            if (t >= 1f)
            {
                g.gameObject.SetActive(false);
                int l = Level(CardId.Grenade);
                Explosions.Explode(grenadeTo[i], grenadeRadius, 70f + 25f * (l - 1), 0f, SfxId.Explosion, 0.35f, null, false);
            }
        }
    }
}
