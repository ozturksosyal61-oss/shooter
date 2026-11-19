using UnityEngine;

/// <summary>
/// Health packs dropped by zombies (low chance, higher when the player is hurt). Pooled; they bob,
/// are pulled in by a magnet when the player is close and needs health, and blink out after a while.
/// Hard mode adds ammo boxes (one reserve magazine each; mutations always drop one, more likely when the reserve
/// is low) — same pool, a kind per slot.
/// Co-op: the host decides the drops (for whoever is hurt) and tells everyone (<see cref="Dropped"/> →
/// <see cref="RemoteSpawn"/>); each game collects for its own soldier and says so (<see cref="Taken"/> → <see cref="RemoteTaken"/>).
/// </summary>
public class Pickups : MonoBehaviour
{
    public static Pickups I { get; private set; }

    public GameObject healthPackPrefab;
    public GameObject ammoBoxPrefab;
    public int poolSize = 8;
    public int ammoPoolSize = 8;
    [Range(0f, 1f)] public float dropChance = 0.05f;
    [Range(0f, 1f)] public float dropChanceWhenHurt = 0.12f;
    public float minTimeBetweenDrops = 5f;
    public float healAmount = 25f;
    public float magnetRadius = 5f;
    public float collectRadius = 0.9f;
    public float lifetime = 18f;

    Transform[] items;
    bool[] isAmmo;
    float lastAmmoDrop = -100f;
    System.Random ammoRng;
    Transform[] visuals;
    float[] born;
    Vector3[] vel;
    float lastDrop = -100f;
    ushort[] ids;
    ushort nextId = 1;

    /// <summary>Host (co-op): a pickup appeared (id, position, ammo box).</summary>
    public event System.Action<ushort, Vector3, bool> Dropped;
    /// <summary>This game's soldier picked one up (id) — teammates remove their copy.</summary>
    public event System.Action<ushort> Taken;

    public int ActiveCount
    {
        get { int n = 0; for (int i = 0; i < items.Length; i++) if (!isAmmo[i] && items[i].gameObject.activeSelf) n++; return n; }
    }

    public int ActiveAmmo
    {
        get { int n = 0; for (int i = 0; i < items.Length; i++) if (isAmmo[i] && items[i].gameObject.activeSelf) n++; return n; }
    }

    void Awake()
    {
        I = this;
        int ammoN = ammoBoxPrefab ? ammoPoolSize : 0;
        int n = poolSize + ammoN;
        items = new Transform[n];
        visuals = new Transform[n];
        born = new float[n];
        vel = new Vector3[n];
        isAmmo = new bool[n];
        ids = new ushort[n];
        ammoRng = new System.Random((ChunkManager.I ? ChunkManager.I.seed : 1) ^ 0x4a3307);
        for (int i = 0; i < n; i++)
        {
            isAmmo[i] = i >= poolSize;
            var go = Instantiate(isAmmo[i] ? ammoBoxPrefab : healthPackPrefab, transform);
            go.SetActive(false);
            items[i] = go.transform;
            visuals[i] = go.transform.childCount > 0 ? go.transform.GetChild(0) : go.transform;
        }
    }

    public void MaybeDrop(Vector3 pos, bool special = false)
    {
        var p = GameManager.I.player;
        var w = p ? p.weapon : null;
        if (w && w.limitedAmmo && Time.time - lastAmmoDrop >= (special ? 0f : ModeRules.AmmoDropMinInterval)
            && ammoRng.NextDouble() < ModeRules.AmmoDrop(special, w.Reserve, w.magazineSize))
        {
            DropAmmo(pos + new Vector3(0.6f, 0f, 0f));
        }
        if (Time.time - lastDrop < minTimeBetweenDrops) return;
        // Co-op: the most hurt soldier decides how likely a medkit is.
        float h01 = p ? p.Health / Mathf.Max(1f, p.maxHealth) : 1f;
        for (int i = 0; i < Squad.Members.Count; i++) if (!Squad.Members[i].IsDead) h01 = Mathf.Min(h01, Squad.Members[i].Health01);
        float chance = h01 < 0.5f ? dropChanceWhenHurt : dropChance;
        if (GameMode.IsHard) chance *= ModeRules.HardMedkitMul;
        if (Random.value > chance) return;
        Drop(pos);
    }

    public void Drop(Vector3 pos) => Spawn(pos, false);

    public void DropAmmo(Vector3 pos)
    {
        if (Spawn(pos, true)) lastAmmoDrop = Time.time;
    }

    bool Spawn(Vector3 pos, bool ammo) => Spawn(pos, ammo, 0) >= 0;

    int Spawn(Vector3 pos, bool ammo, ushort id)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (isAmmo[i] != ammo || items[i].gameObject.activeSelf) continue;
            if (!ammo) lastDrop = Time.time;
            items[i].position = new Vector3(pos.x, 0f, pos.z);
            items[i].gameObject.SetActive(true);
            born[i] = Time.time;
            vel[i] = Vector3.zero;
            bool local = id == 0;
            if (local) { if (nextId == 0) nextId = 1; id = nextId++; }
            ids[i] = id;
            if (local && GameMode.IsCoop) Dropped?.Invoke(id, items[i].position, ammo);
            return i;
        }
        return -1;
    }

    /// <summary>Co-op client: the host dropped pickup <paramref name="id"/>.</summary>
    public void RemoteSpawn(ushort id, Vector3 pos, bool ammo)
    {
        if (id == 0) return;
        Spawn(pos, ammo, id);
    }

    /// <summary>Co-op: a teammate picked pickup <paramref name="id"/> up — ours vanishes.</summary>
    public void RemoteTaken(ushort id)
    {
        for (int i = 0; i < items.Length; i++)
            if (ids[i] == id && items[i].gameObject.activeSelf) items[i].gameObject.SetActive(false);
    }

    void Update()
    {
        var gm = GameManager.I;
        if (!gm || !gm.player) return;
        var player = gm.player;
        Vector3 pp = player.transform.position;
        float dt = Time.deltaTime;
        for (int i = 0; i < items.Length; i++)
        {
            var t = items[i];
            if (!t.gameObject.activeSelf) continue;
            float age = Time.time - born[i];
            if (age > (isAmmo[i] ? lifetime * 1.4f : lifetime)) { t.gameObject.SetActive(false); continue; }

            Vector3 to = pp - t.position; to.y = 0f;
            float d = to.magnitude;
            bool wants = isAmmo[i] ? !player.IsDead && player.weapon && player.weapon.Reserve < ModeRules.ReserveCap(player.weapon.magazineSize) : player.NeedsHealth;
            if (wants && d < (isAmmo[i] ? Mathf.Max(magnetRadius, ModeRules.AmmoMagnetRadius) : magnetRadius))
            {
                // Magnet: accelerate toward the player.
                vel[i] = Vector3.MoveTowards(vel[i], to.normalized * 14f, 40f * dt);
                t.position += vel[i] * dt;
                if (d < collectRadius)
                {
                    if (isAmmo[i])
                    {
                        player.weapon.AddReserveMags(1);
                        if (DamageNumbers.I) DamageNumbers.I.ShowText(pp + Vector3.up * 2.3f, Loc.T("hud.ammo_pickup"), new Color(1f, 0.85f, 0.25f));
                        GameAudio.Play(SfxId.ReloadEnd, pp);
                    }
                    else player.Heal(healAmount);
                    GameAudio.Play(SfxId.Pickup, pp);
                    if (Fx.I) Fx.I.Dust(t.position);
                    t.gameObject.SetActive(false);
                    if (GameMode.IsCoop) Taken?.Invoke(ids[i]);
                    continue;
                }
            }
            else vel[i] = Vector3.zero;

            // Bob + spin, blink in the last 3 seconds.
            var v = visuals[i];
            v.localPosition = new Vector3(0f, 0.45f + Mathf.Sin(age * 4f) * 0.12f, 0f);
            v.localRotation = Quaternion.Euler(0f, age * 120f, 0f);
            bool visible = age < lifetime - 3f || Mathf.Repeat(age * 6f, 1f) < 0.6f;
            if (v.gameObject.activeSelf != visible) v.gameObject.SetActive(visible);
        }
    }
}
