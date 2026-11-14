using UnityEngine;

/// <summary>
/// Tilted follow camera: trauma screenshake (unscaled time, so it shakes during hitstop),
/// firing kick-back, and see-through fading of tall obstacles between camera and player.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;
    public float pitch = 57f;
    public float distance = 16f;
    public float smoothTime = 0.12f;
    public float maxShakeOffset = 0.45f;
    public float maxShakeRoll = 2.5f;
    public float traumaDecay = 1.8f;
    public float kickRecover = 14f;
    public float maxKick = 0.5f;

    float trauma;
    Vector3 focus, vel, kick;
    static readonly RaycastHit[] Hits = new RaycastHit[8];

    void OnEnable() => FloatingOrigin.OnShift += OnShift;
    void OnDisable() => FloatingOrigin.OnShift -= OnShift;
    void OnShift(Vector3 delta) => focus += delta;

    void Start()
    {
        if (target) focus = target.position;
        Apply(0f);
    }

    public void AddTrauma(float amount) => trauma = Mathf.Min(1f, trauma + amount);

    /// <summary>Pushes the camera along <paramref name="dir"/> (e.g. opposite to the shot).</summary>
    public void Kick(Vector3 dir, float amount)
    {
        kick = Vector3.ClampMagnitude(kick + dir * amount, maxKick);
    }

    void LateUpdate()
    {
        if (target) focus = Vector3.SmoothDamp(focus, target.position, ref vel, smoothTime, Mathf.Infinity, Time.deltaTime);
        float udt = Time.unscaledDeltaTime;
        trauma = Mathf.Max(0f, trauma - udt * traumaDecay);
        kick = Vector3.Lerp(kick, Vector3.zero, 1f - Mathf.Exp(-kickRecover * udt));
        Apply(trauma * trauma);
        FadeOccluders();
    }

    void Apply(float shake)
    {
        Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 pos = focus + kick - rot * Vector3.forward * distance;
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
