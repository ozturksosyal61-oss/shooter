using UnityEngine;

/// <summary>
/// Cosmetic rescue helicopter: driven by <see cref="ObjectiveVisuals"/> — flies in along an arc while the landing
/// zone is held (synced to hold progress, so it hovers if you step out), touches down at the end, then lifts off and
/// leaves without you. Spinning rotors; rotor sound through GameAudio.
/// </summary>
public class HelicopterFx : MonoBehaviour
{
    public Transform body;
    public Transform mainRotor;
    public Transform tailRotor;
    public Vector3 approachFrom = new Vector3(-55f, 26f, 45f);
    public float leaveTime = 7f;

    enum State { Hidden, Approach, Leave }
    State state;
    Vector3 pad, leaveStart;
    float leaveT, soundT, rotorSpeed;

    public bool Visible => state != State.Hidden;
    public bool Landed { get; private set; }

    void Awake() => gameObject.SetActive(false);

    /// <summary>t = 0 far away … 1 landed on <paramref name="padPos"/>.</summary>
    public void Approach(Vector3 padPos, float t)
    {
        if (t <= 0f) { if (state == State.Approach) Hide(); return; }
        if (state == State.Leave) return;
        pad = padPos;
        if (state == State.Hidden) { state = State.Approach; gameObject.SetActive(true); }
        float e = 1f - (1f - t) * (1f - t);
        Vector3 from = pad + approachFrom;
        Vector3 hover = pad + new Vector3(0f, 9f, 0f);
        Vector3 pos = t < 0.75f ? Vector3.Lerp(from, hover, Mathf.SmoothStep(0f, 1f, t / 0.75f)) : Vector3.Lerp(hover, pad, Mathf.SmoothStep(0f, 1f, (t - 0.75f) / 0.25f));
        pos += Vector3.up * Mathf.Sin(Time.time * 2.3f) * 0.15f * (1f - t);
        transform.position = pos;
        Vector3 dir = hover - from; dir.y = 0f;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(Mathf.Lerp(12f, 0f, e), 0f, 0f);
        Landed = t >= 0.999f;
        rotorSpeed = 1f;
    }

    public void Leave()
    {
        if (state == State.Hidden) { gameObject.SetActive(true); transform.position = pad; }
        state = State.Leave;
        leaveT = 0f;
        leaveStart = transform.position;
        Landed = false;
    }

    void Hide()
    {
        state = State.Hidden;
        Landed = false;
        gameObject.SetActive(false);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (state == State.Leave)
        {
            leaveT += dt;
            float k = leaveT / leaveTime;
            Vector3 away = new Vector3(approachFrom.x, 0f, approachFrom.z) * -1.4f;
            // Short pause on the pad (the "it can't wait" beat), lift straight up, then tilt and race away.
            float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((leaveT - 0.8f) / 1.6f)) * 10f;
            float run = Mathf.Pow(Mathf.Clamp01((leaveT - 2f) / (leaveTime - 2f)), 1.6f);
            transform.position = leaveStart + Vector3.up * (lift + run * 18f) + away * run;
            transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up) * Quaternion.Euler(Mathf.Lerp(0f, 16f, Mathf.Clamp01(run * 3f)), 0f, 0f);
            if (k >= 1f) Hide();
        }
        if (mainRotor) mainRotor.Rotate(0f, 1400f * dt * rotorSpeed, 0f, Space.Self);
        if (tailRotor) tailRotor.Rotate(2000f * dt * rotorSpeed, 0f, 0f, Space.Self);
        soundT -= dt;
        if (soundT <= 0f && state != State.Hidden)
        {
            soundT = 1.4f;
            GameAudio.Play(SfxId.Helicopter, transform.position);
        }
    }
}
