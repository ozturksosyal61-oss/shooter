using UnityEngine;

public class Weapon : MonoBehaviour
{
    public PlayerController owner;
    public Transform muzzle;
    public Transform muzzleFlash;
    public Bullet bulletPrefab;

    public float fireRate = 10f;
    public float spreadDeg = 3f;
    public float bulletSpeed = 42f;
    public float damage = 1f;
    public float autoAimRange = 16f;
    public float maxAimAngleToFire = 40f;

    public bool IsFiring { get; private set; }
    public Vector3 AimDir { get; private set; }
    public float Recoil { get; private set; }

    Pool<Bullet> pool;
    float cooldown;
    float flashUntil;
    Camera cam;
    static readonly Plane AimPlane = new Plane(Vector3.up, new Vector3(0f, 1f, 0f));

    void Awake()
    {
        var container = new GameObject("Bullets").transform;
        pool = new Pool<Bullet>(bulletPrefab, container, 40);
        if (muzzleFlash) muzzleFlash.gameObject.SetActive(false);
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

        cooldown -= dt;
        if (IsFiring && aim.sqrMagnitude > 0.01f && cooldown <= 0f &&
            Vector3.Angle(owner.transform.forward, aim) < maxAimAngleToFire)
        {
            Fire(aim);
            cooldown += 1f / fireRate;
            if (cooldown < 0f) cooldown = 0f;
        }
        else if (cooldown < 0f) cooldown = 0f;

        Recoil = Mathf.MoveTowards(Recoil, 0f, dt * 12f);
        if (muzzleFlash && muzzleFlash.gameObject.activeSelf && Time.unscaledTime >= flashUntil)
            muzzleFlash.gameObject.SetActive(false);
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
        Vector3 dir = Quaternion.Euler(0f, Random.Range(-spreadDeg, spreadDeg), 0f) * aim;
        Vector3 start = muzzle.position;
        Vector3 castFrom = owner.transform.position; castFrom.y = start.y;
        pool.Get().Launch(start, castFrom, dir, bulletSpeed, damage, pool);

        if (muzzleFlash)
        {
            muzzleFlash.gameObject.SetActive(true);
            float s = Random.Range(0.8f, 1.25f);
            muzzleFlash.localScale = new Vector3(s * 0.55f, s * 0.9f, 1f);
            flashUntil = Time.unscaledTime + 0.04f;
        }
        if (Fx.I) Fx.I.Muzzle(start, dir);
        Recoil = 1f;
        GameManager.I.Shake(0.035f);
    }
}
