using UnityEngine;

/// <summary>
/// Rifle: aim (drag / screen point / nearest-zombie auto-aim), pooled bullets from the muzzle,
/// 40-round magazine: automatic, tactical and manual reloads (no firing while reloading).
/// Active reload: pressing reload again inside the sweet spot finishes instantly and buffs the next magazine;
/// missing it jams the gun for a moment.
/// </summary>
public class Weapon : MonoBehaviour
{
    public PlayerController owner;
    public SoldierRig rig;
    public Transform muzzle;
    public Transform muzzleFlash;
    public Bullet bulletPrefab;

    [Header("Firing")]
    public float fireRate = 10f;
    public float spreadDeg = 3f;
    public float bulletSpeed = 55f;
    public float damage = 25f;
    public float damageJitter = 0.15f;
    [Range(0f, 1f)] public float critChance = 0.12f;
    public float critMultiplier = 2f;
    public float autoAimRange = 18f;
    public float maxAimAngleToFire = 40f;
    public float cameraKick = 0.12f;
    [Header("Weapon type (set by WeaponLoadout)")]
    public float bulletLife = 0.6f;
    [Tooltip("Pellets per shot (shotgun); cards add extra bullets on top.")]
    public int basePellets = 1;
    public float pelletSpread = 6f;
    public int basePierce;
    public float shotShake = 0.02f;
    public SfxId shootSfx = SfxId.Shoot;
    [Tooltip("Zombies a bullet passes through (card: Delici Mermi).")]
    public int pierce;
    [Tooltip("Extra bullets per shot, fanned out, each at 70% damage (card: Çift Mermi).")]
    public int extraBullets;
    public float extraBulletDamage = 0.7f;
    public float extraBulletSpread = 7f;

    [Header("Magazine")]
    public int magazineSize = 40;
    public float reloadTime = 1.5f;
    [Tooltip("Idle time after releasing the trigger before a tactical reload starts.")]
    public float tacticalReloadDelay = 1f;
    [Range(0f, 1f)] public float tacticalReloadBelow = 0.5f;

    [Header("Active reload")]
    [Range(0f, 1f)] public float sweetSpotStart = 0.42f;
    [Range(0f, 1f)] public float sweetSpotEnd = 0.6f;
    public float jamPenalty = 0.6f;
    public float perfectDamageBonus = 0.15f;

    public bool IsFiring { get; private set; }
    public bool IsReloading { get; private set; }
    public float ReloadProgress => IsReloading ? Mathf.Clamp01(reloadT / reloadTime) : 0f;
    public int Ammo { get; private set; }
    /// <summary>-1 = not tried, 0 = jammed, 1 = perfect (for HUD feedback during the current reload).</summary>
    public int ActiveReloadResult { get; private set; } = -1;
    /// <summary>Remaining shots that carry the perfect-reload damage bonus.</summary>
    public int BonusShots { get; private set; }
    public Vector3 AimDir { get; private set; }
    public float Recoil { get; private set; }

    Pool<Bullet> pool;
    float cooldown, flashUntil, reloadT, idleT;
    bool lowAmmoWarned, dryFired;
    Camera cam;
    static readonly Plane AimPlane = new Plane(Vector3.up, new Vector3(0f, 1f, 0f));

    void Awake()
    {
        var container = new GameObject("Bullets").transform;
        pool = new Pool<Bullet>(bulletPrefab, container, 40);
        if (muzzleFlash) muzzleFlash.gameObject.SetActive(false);
        Ammo = magazineSize;
    }

    public void Tick(float dt, Vector3 moveDir)
    {
        var input = TouchControls.I;
        IsFiring = input && input.FireHeld && !owner.IsDead;
        Vector3 pos = owner.transform.position;

        Vector3 aim = Vector3.zero;
        if (IsFiring)
        {
            if (input.AimAtScreenPoint) aim = ScreenToGroundDir(input.AimScreenPoint, pos);
            else if (input.HasAimDir) aim = new Vector3(input.AimDir.x, 0f, input.AimDir.y);
            else
            {
                // Held by a tongue: auto-aim goes for the Dilci.
                Zombie z = owner.IsGrabbed && owner.Grabber.Targetable ? owner.Grabber : Zombie.FindNearest(pos, autoAimRange);
                if (z) { aim = z.transform.position - pos; aim.y = 0f; aim.Normalize(); }
                else aim = moveDir.sqrMagnitude > 0.01f ? moveDir : owner.transform.forward;
            }
        }
        AimDir = aim;

        if (input && input.ReloadPressed) OnReloadButton();
        UpdateMagazine(dt);

        // Dry trigger: pulling the trigger while the magazine is being swapped (once per pull).
        if (IsFiring && IsReloading && !dryFired)
        {
            dryFired = true;
            GameAudio.Play(SfxId.DryFire, muzzle.position);
        }
        if (!IsFiring) dryFired = false;

        cooldown -= dt;
        if (IsFiring && !IsReloading && aim.sqrMagnitude > 0.01f && cooldown <= 0f &&
            Vector3.Angle(owner.transform.forward, aim) < maxAimAngleToFire)
        {
            Fire(aim);
            cooldown += 1f / fireRate;
            if (cooldown < 0f) cooldown = 0f;
            if (Ammo == 0) StartReload();
        }
        else if (cooldown < 0f) cooldown = 0f;

        Recoil = Mathf.MoveTowards(Recoil, 0f, dt * 12f);
        if (muzzleFlash && muzzleFlash.gameObject.activeSelf && Time.unscaledTime >= flashUntil)
            muzzleFlash.gameObject.SetActive(false);
    }

    void UpdateMagazine(float dt)
    {
        if (IsReloading)
        {
            reloadT += dt;
            if (reloadT >= reloadTime)
            {
                IsReloading = false;
                Ammo = magazineSize;
                lowAmmoWarned = false;
                GameAudio.Play(SfxId.ReloadEnd, owner.transform.position);
            }
            return;
        }
        if (Ammo == 0) { StartReload(); return; }

        // Tactical reload: trigger released for a while with a less-than-half magazine.
        idleT = IsFiring ? 0f : idleT + dt;
        if (idleT >= tacticalReloadDelay && Ammo < magazineSize * tacticalReloadBelow) StartReload();
    }

    public void StartReload()
    {
        if (IsReloading || Ammo >= magazineSize || owner.IsDead) return;
        IsReloading = true;
        reloadT = 0f;
        idleT = 0f;
        ActiveReloadResult = -1;
        BonusShots = 0;
        if (rig) rig.PlayReload(reloadTime);
        GameAudio.Play(SfxId.ReloadStart, owner.transform.position);
    }

    /// <summary>Reload button: starts a reload, or during a reload attempts the active reload.</summary>
    public void OnReloadButton()
    {
        if (owner.IsDead) return;
        if (!IsReloading) { StartReload(); return; }
        if (ActiveReloadResult != -1) return;
        float p = ReloadProgress;
        if (p >= sweetSpotStart && p <= sweetSpotEnd)
        {
            ActiveReloadResult = 1;
            reloadT = reloadTime; // finishes this frame
            BonusShots = magazineSize;
            if (rig) rig.FinishReloadEarly();
            GameAudio.Play(SfxId.ActiveReloadPerfect, owner.transform.position, 1.5f);
            if (DamageNumbers.I) DamageNumbers.I.ShowText(owner.transform.position + Vector3.up * 2.3f, Loc.T("hud.perfect"), new Color(1f, 0.85f, 0.2f));
        }
        else
        {
            ActiveReloadResult = 0;
            reloadT -= jamPenalty;
            if (rig) rig.ExtendReload(jamPenalty);
            GameAudio.Play(SfxId.ActiveReloadFail, owner.transform.position);
        }
    }

    /// <summary>Damage multiplier of the next shot (perfect active reload bonus).</summary>
    public float DamageScale => BonusShots > 0 ? 1f + perfectDamageBonus : 1f;

    /// <summary>Pooled bullet for secondary weapons (shotgun pellets, turret).</summary>
    public void SpawnBullet(Vector3 start, Vector3 castFrom, Vector3 dir, float speed, float dmg, bool crit, float life, int pierceCount)
    {
        if (crit) dmg *= critMultiplier;
        pool.Get().Launch(start, castFrom, dir, speed, dmg, crit, pool, life, pierceCount);
    }

    Vector3 ScreenToGroundDir(Vector2 screen, Vector3 from)
    {
        if (!cam) cam = Camera.main;
        var ray = cam.ScreenPointToRay(screen);
        if (!AimPlane.Raycast(ray, out float enter)) return owner.transform.forward;
        Vector3 d = ray.GetPoint(enter) - from; d.y = 0f;
        return d.sqrMagnitude > 0.01f ? d.normalized : owner.transform.forward;
    }

    void Fire(Vector3 aim)
    {
        Ammo--;
        if (Ammo <= magazineSize / 6 && Ammo > 0 && !lowAmmoWarned)
        {
            lowAmmoWarned = true;
            GameAudio.Play(SfxId.LowAmmo, owner.transform.position);
        }
        Vector3 dir = Quaternion.Euler(0f, Random.Range(-spreadDeg, spreadDeg), 0f) * aim;
        Vector3 start = muzzle.position;
        Vector3 castFrom = owner.transform.position; castFrom.y = start.y;
        float scale = DamageScale;
        if (BonusShots > 0) BonusShots--;
        int pellets = Mathf.Max(1, basePellets);
        for (int p = 1; p < pellets; p++)
        {
            // Shotgun: a fan of pellets inside the cone.
            bool pc = Random.value < critChance;
            float pd = damage * scale * Random.Range(1f - damageJitter, 1f + damageJitter) * (pc ? critMultiplier : 1f);
            Vector3 pdir = Quaternion.Euler(0f, Random.Range(-pelletSpread, pelletSpread), 0f) * dir;
            pool.Get().Launch(start, castFrom, pdir, bulletSpeed * Random.Range(0.92f, 1.05f), pd, pc, pool, bulletLife * Random.Range(0.85f, 1.1f), pierce + basePierce);
        }
        for (int b = 0; b <= extraBullets; b++)
        {
            bool crit = Random.value < critChance;
            float dmg = damage * scale * Random.Range(1f - damageJitter, 1f + damageJitter) * (crit ? critMultiplier : 1f);
            Vector3 d = dir;
            if (b > 0)
            {
                // Fan the extra bullets out alternately left / right.
                float side = (b % 2 == 1 ? 1f : -1f) * ((b + 1) / 2) * extraBulletSpread;
                d = Quaternion.Euler(0f, side, 0f) * dir;
                dmg *= extraBulletDamage;
            }
            pool.Get().Launch(start, castFrom, d, bulletSpeed, dmg, crit, pool, bulletLife, pierce + basePierce);
        }

        if (muzzleFlash)
        {
            muzzleFlash.gameObject.SetActive(true);
            float s = Random.Range(0.8f, 1.25f);
            muzzleFlash.localScale = new Vector3(s * 0.55f, s * 0.9f, 1f);
            flashUntil = Time.unscaledTime + 0.04f;
        }
        if (Fx.I) Fx.I.Muzzle(start, dir);
        Recoil = 1f;
        if (rig) rig.Kick();
        var gm = GameManager.I;
        if (gm.cameraRig) gm.cameraRig.Kick(-dir, cameraKick);
        gm.Shake(shotShake);
        GameAudio.Play(shootSfx, start);
    }
}
