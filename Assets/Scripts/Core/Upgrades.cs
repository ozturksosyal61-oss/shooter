using System;
using System.Collections.Generic;
using UnityEngine;

public enum CardId
{
    Pierce, FireRate, BigMag, FastReload, DoubleShot, Crit, KillBlast, MaxHealth,
    Lifesteal, MoveSpeed, Magnet, Shotgun, Turret, Grenade, BreatherHeal,
}

public enum Rarity { Common, Rare, Epic }

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

    [Header("Active cards")]
    public Turret turretPrefab;
    public GameObject grenadePrefab;
    public float shotgunRange = 7f;
    public int shotgunPellets = 7;
    public float shotgunCone = 36f;
    public float shotgunDamage = 16f;
    public float turretLife = 30f;
    public float turretEvery = 45f;
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
    Turret turret;
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
            turret = Instantiate(turretPrefab);
            turret.gameObject.SetActive(false);
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
    public List<CardId> Offer(int count) => Offer(levels, count, () => UnityEngine.Random.value, offerBuffer, pickBuffer);

    public static float RarityWeightOf(Rarity r) => RarityWeight[(int)r];

    /// <summary>Pure offer logic (unit-tested): <paramref name="rand01"/> supplies [0,1) values.</summary>
    public static List<CardId> Offer(int[] levels, int count, Func<float> rand01, List<CardId> into, List<int> scratch)
    {
        into.Clear();
        scratch.Clear();
        for (int i = 0; i < Cards.Length; i++) if (levels[(int)Cards[i].id] < Cards[i].maxLevel) scratch.Add(i);
        while (into.Count < count && scratch.Count > 0)
        {
            float total = 0f;
            foreach (int i in scratch) total += RarityWeight[(int)Cards[i].rarity];
            float r = rand01() * total;
            int chosen = scratch[scratch.Count - 1];
            foreach (int i in scratch)
            {
                r -= RarityWeight[(int)Cards[i].rarity];
                if (r <= 0f) { chosen = i; break; }
            }
            scratch.Remove(chosen);
            into.Add(Cards[chosen].id);
        }
        return into;
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

    public void Take(CardId id)
    {
        if (!CanTake(id)) return;
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
    }

    void Recompute()
    {
        weapon.fireRate = baseFireRate * FireRateMul(Level(CardId.FireRate));
        weapon.reloadTime = baseReload * ReloadMul(Level(CardId.FastReload));
        weapon.critChance = CritChance(baseCrit, Level(CardId.Crit));
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
        if (ls > 0) player.Regen(ls);
        int kb = Level(CardId.KillBlast);
        if (kb > 0 && !z.KilledByExplosion && UnityEngine.Random.value < KillBlastChance(kb))
            Explosions.Explode(z.transform.position, killBlastRadius, 45f + 10f * kb, 0f, SfxId.Explosion, 0.25f, z, false);
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
        if (tr > 0 && turret && !turret.gameObject.activeSelf && (turretT -= dt) <= 0f) DeployTurret();

        int gr = Level(CardId.Grenade);
        if (gr > 0 && (grenadeT -= dt) <= 0f)
        {
            if (TryThrowGrenade()) grenadeT = 7f - gr;
            else grenadeT = 0.3f;
        }
        UpdateGrenades();
    }

    void FireShotgun(Zombie target)
    {
        Vector3 p = player.transform.position;
        Vector3 aim = target.transform.position - p; aim.y = 0f; aim.Normalize();
        Vector3 start = p + Vector3.up * 1.1f + aim * 0.5f;
        float dmg = shotgunDamage * weapon.DamageScale;
        for (int i = 0; i < shotgunPellets; i++)
        {
            float a = Mathf.Lerp(-shotgunCone * 0.5f, shotgunCone * 0.5f, i / (shotgunPellets - 1f)) + UnityEngine.Random.Range(-3f, 3f);
            Vector3 d = Quaternion.Euler(0f, a, 0f) * aim;
            weapon.SpawnBullet(start, p + Vector3.up * 1.1f, d, 48f, dmg, UnityEngine.Random.value < weapon.critChance, 0.2f, 0);
        }
        if (Fx.I) Fx.I.Muzzle(start, aim);
        GameManager.I.Shake(0.12f);
        if (GameManager.I.cameraRig) GameManager.I.cameraRig.Kick(-aim, 0.2f);
        GameAudio.Play(SfxId.Shotgun, start);
    }

    void DeployTurret()
    {
        if (!turret) return;
        Vector3 p = player.transform.position;
        Vector3 spot = p - player.transform.forward * 1.4f;
        if (Physics.CheckSphere(spot + Vector3.up * 0.6f, 0.5f, GameLayers.ObstacleMask, QueryTriggerInteraction.Ignore)) spot = p;
        spot.y = 0f;
        int l = Level(CardId.Turret);
        turret.Deploy(spot, turretLife, 6f + 2.5f * (l - 1), 14f + 3f * (l - 1), weapon);
        turretT = turretEvery;
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
