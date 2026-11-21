using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardId
{
    Pierce, FireRate, BigMag, FastReload, DoubleShot, Crit, KillBlast, MaxHealth,
    Lifesteal, MoveSpeed, Magnet, Shotgun, Turret, Grenade, BreatherHeal,
}

public enum Rarity { Common, Rare, Epic }

public enum Synergy { FlameShot, TurretMaster, Demolition, IronBlood, Deadeye }

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
    public static readonly CardId[] DamageCards = { CardId.Pierce, CardId.FireRate, CardId.DoubleShot, CardId.Crit, CardId.KillBlast, CardId.Shotgun };
    public const int GuaranteedDamagePicks = 2;
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
    };
    public static bool HasSynergy(int[] levels, Synergy s)
    {
        foreach (var p in SynergyPairs) if (p.s == s) return levels[(int)p.a] > 0 && levels[(int)p.b] > 0;
        return false;
    }
    public const float FlameShotDamageMul = 1.4f, TurretMasterRateMul = 1.4f, DemolitionGrenadeMul = 0.7f,
        DemolitionBlastMul = 1.3f, IronBloodLifestealMul = 2f, DeadeyeCrit = 0.1f;
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
            case CardId.Pierce: case CardId.FireRate: case CardId.DoubleShot: case CardId.Crit: return Group.Damage;
            case CardId.KillBlast: case CardId.Shotgun: case CardId.Turret: case CardId.Grenade: return Group.Build;
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
            case CardId.Turret: DeployTurret(); break;
            case CardId.Shotgun: shotgunT = 0.3f; break;
            case CardId.Grenade: grenadeT = 1f; break;
        }
        Changed?.Invoke(id, l);
        foreach (var sp in SynergyPairs)
            if (Has(sp.s) && (synBefore & (1 << (int)sp.s)) == 0) { SynergyUnlocked?.Invoke(sp.s); GameServices.Unlock(GameIds.AchSynergy); }
    }

    void Recompute()
    {
        weapon.fireRate = baseFireRate * FireRateMul(Level(CardId.FireRate));
        weapon.reloadTime = baseReload * ReloadMul(Level(CardId.FastReload));
        weapon.critChance = Mathf.Min(0.9f, CritChance(baseCrit, Level(CardId.Crit)) + (Has(Synergy.Deadeye) ? DeadeyeCrit : 0f));
        weapon.magazineSize = MagazineSize(baseMag, Level(CardId.BigMag));
        weapon.pierce = Level(CardId.Pierce);
        weapon.extraBullets = Level(CardId.DoubleShot);
        player.maxHealth = MaxHealth(baseMaxHealth, Level(CardId.MaxHealth));
        player.moveSpeed = baseMove * MoveMul(Level(CardId.MoveSpeed));
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
