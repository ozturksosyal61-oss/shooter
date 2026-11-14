using UnityEngine;

/// <summary>
/// Rifle: aim (drag / screen point / nearest-zombie auto-aim), pooled bullets from the muzzle,
/// 30-round magazine with automatic and tactical reloads (no firing while reloading).
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

    [Header("Magazine")]
    public int magazineSize = 30;
    public float reloadTime = 1.5f;
    [Tooltip("Idle time after releasing the trigger before a tactical reload starts.")]
    public float tacticalReloadDelay = 1f;
    [Range(0f, 1f)] public float tacticalReloadBelow = 0.5f;

    public bool IsFiring { get; private set; }
    public bool IsReloading { get; private set; }
    public float ReloadProgress => IsReloading ? Mathf.Clamp01(reloadT / reloadTime) : 0f;
    public int Ammo { get; private set; }
    public Vector3 AimDir { get; private set; }
    public float Recoil { get; private set; }

    Pool<Bullet> pool;
    float cooldown, flashUntil, reloadT, idleT;
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
                var z = Zombie.FindNearest(pos, autoAimRange);
                if (z) { aim = z.transform.position - pos; aim.y = 0f; aim.Normalize(); }
                else aim = moveDir.sqrMagnitude > 0.01f ? moveDir : owner.transform.forward;
            }
        }
        AimDir = aim;

        UpdateMagazine(dt);

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
                GameAudio.Play(SfxId.ReloadEnd, owner.transform.position);
                Haptics.Play(Haptics.Kind.Light);
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
        if (rig) rig.PlayReload(reloadTime);
        GameAudio.Play(SfxId.ReloadStart, owner.transform.position);
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
        Vector3 dir = Quaternion.Euler(0f, Random.Range(-spreadDeg, spreadDeg), 0f) * aim;
        Vector3 start = muzzle.position;
        Vector3 castFrom = owner.transform.position; castFrom.y = start.y;
        bool crit = Random.value < critChance;
        float dmg = damage * Random.Range(1f - damageJitter, 1f + damageJitter) * (crit ? critMultiplier : 1f);
        pool.Get().Launch(start, castFrom, dir, bulletSpeed, dmg, crit, pool);

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
        gm.Shake(0.02f);
        GameAudio.Play(SfxId.Shoot, start);
    }
}
