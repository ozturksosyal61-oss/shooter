using UnityEngine;

/// <summary>
/// Tilted follow camera: trauma screenshake (unscaled time, so it shakes during hitstop),
/// firing kick-back, and see-through fading of tall obstacles between camera and player.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;
    public float pitch = 57f;
    public float distance = 14f;
    [Tooltip("Radius of the see-through circle cut into buildings around the soldier (fraction of screen height).")]
    public float cutRadius = 0.13f;
    public float cutRim = 0.012f;
    public float smoothTime = 0.12f;
    public float maxShakeOffset = 0.45f;
    public float maxShakeRoll = 2.5f;
    public float traumaDecay = 1.8f;
    public float kickRecover = 14f;
    public float maxKick = 0.5f;

    float trauma;
    Vector3 focus, vel, kick;
    // Cinematic focus (boss intro): look at another target, a bit closer, for a few real seconds.
    Transform focusTarget;
    float focusUntil, focusDistMul = 1f, distMul = 1f;
    static readonly RaycastHit[] Hits = new RaycastHit[8];
    static readonly int CutCircle = Shader.PropertyToID("_CutCircle");
    Camera cam;

    void OnEnable() => FloatingOrigin.OnShift += OnShift;
    void OnDisable() => FloatingOrigin.OnShift -= OnShift;
    void OnShift(Vector3 delta) => focus += delta;

    void Start()
    {
        if (target) focus = target.position;
        Apply(0f);
    }

    public void AddTrauma(float amount) => trauma = Mathf.Min(1f, trauma + amount);

    /// <summary>Swings the camera over to <paramref name="t"/> for <paramref name="realSeconds"/>, pushed in to <paramref name="distanceMul"/>.</summary>
    public void Focus(Transform t, float realSeconds, float distanceMul = 0.72f)
    {
        focusTarget = t;
        focusUntil = Time.unscaledTime + realSeconds;
        focusDistMul = distanceMul;
    }

    public bool IsFocusing => focusTarget && Time.unscaledTime < focusUntil;

    /// <summary>Pushes the camera along <paramref name="dir"/> (e.g. opposite to the shot).</summary>
    public void Kick(Vector3 dir, float amount)
    {
        kick = Vector3.ClampMagnitude(kick + dir * amount, maxKick);
    }

    void LateUpdate()
    {
        float udt = Time.unscaledDeltaTime;
        bool cine = IsFocusing && focusTarget.gameObject.activeInHierarchy;
        if (cine)
        {
            Vector3 goal = Vector3.Lerp(target ? target.position : focusTarget.position, focusTarget.position, 0.8f);
            focus = Vector3.SmoothDamp(focus, goal, ref vel, 0.3f, Mathf.Infinity, udt);
        }
        else if (target) focus = Vector3.SmoothDamp(focus, target.position, ref vel, smoothTime, Mathf.Infinity, Time.deltaTime);
        distMul = Mathf.Lerp(distMul, cine ? focusDistMul : 1f, 1f - Mathf.Exp(-4f * udt));
        trauma = Mathf.Max(0f, trauma - udt * traumaDecay);
        kick = Vector3.Lerp(kick, Vector3.zero, 1f - Mathf.Exp(-kickRecover * udt));
        Apply(trauma * trauma);
        FadeOccluders();
        if (target)
        {
            if (!cam) cam = GetComponent<Camera>();
            Vector3 vp = cam ? cam.WorldToViewportPoint(target.position + Vector3.up * 1f) : new Vector3(0.5f, 0.5f, 1f);
            Shader.SetGlobalVector(CutCircle, new Vector4(vp.x, vp.y, cutRadius, cutRim));
        }
    }

    void Apply(float shake)
    {
        Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 pos = focus + kick - rot * Vector3.forward * (distance * distMul);
        if (shake > 0f)
        {
            float t = Time.unscaledTime * 28f;
            float x = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f;
            float r = (Mathf.PerlinNoise(t, t * 0.5f) - 0.5f) * 2f;
            pos += rot * new Vector3(x, y, 0f) * (maxShakeOffset * shake);
            rot *= Quaternion.Euler(0f, 0f, r * maxShakeRoll * shake);
        }
        transform.SetPositionAndRotation(pos, rot);
    }

    void FadeOccluders()
    {
        if (!target) return;
        Vector3 to = target.position + Vector3.up * 1f;
        Vector3 from = transform.position;
        Vector3 d = to - from;
        float len = d.magnitude;
        int n = Physics.SphereCastNonAlloc(from, 0.6f, d / len, Hits, len, GameLayers.BlockerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var f = Hits[i].collider.GetComponentInParent<Fadeable>();
            if (f) f.RequestFade();
        }
    }
}
