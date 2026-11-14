using UnityEngine;

/// <summary>Tilted follow camera with trauma-based screenshake (runs on unscaled time so it shakes during hitstop).</summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;
    public float pitch = 57f;
    public float distance = 16f;
    public float smoothTime = 0.12f;
    public float maxShakeOffset = 0.4f;
    public float maxShakeRoll = 2.5f;
    public float traumaDecay = 1.8f;

    float trauma;
    Vector3 focus, vel;

    void Start()
    {
        if (target) focus = target.position;
        Apply(0f);
    }

    public void AddTrauma(float amount) => trauma = Mathf.Min(1f, trauma + amount);

    void LateUpdate()
    {
        if (target) focus = Vector3.SmoothDamp(focus, target.position, ref vel, smoothTime, Mathf.Infinity, Time.deltaTime);
        trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * traumaDecay);
        Apply(trauma * trauma);
    }

    void Apply(float shake)
    {
        Quaternion rot = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 pos = focus - rot * Vector3.forward * distance;
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
}
