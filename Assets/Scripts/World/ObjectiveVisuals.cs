using UnityEngine;

/// <summary>
/// World-side view of the current objective (reads <see cref="ObjectiveDirector"/>, never changes it): a light
/// beacon over the target, the hold-zone ring with a fill that grows with progress, the flare / supply crate props
/// and the helicopter that flies in, lands and leaves without you. Positions follow the director's global target,
/// so floating-origin shifts need no bookkeeping.
/// </summary>
public class ObjectiveVisuals : MonoBehaviour
{
    public Transform beacon;
    public Renderer beaconRenderer;
    public Transform zoneRing;
    public Renderer zoneRingRenderer;
    public Transform zoneFill;
    public Renderer zoneFillRenderer;
    public GameObject flare;
    public GameObject supplyCrate;
    public Transform supplyLid;
    public HelicopterFx helicopter;

    public Color[] kindColors =
    {
        new Color(1f, 0.25f, 0.15f, 1f), new Color(0.3f, 0.9f, 1f, 1f), new Color(0.4f, 1f, 0.35f, 1f), new Color(1f, 0.82f, 0.2f, 1f),
    };

    MaterialPropertyBlock mpb;
    static readonly int ColorId = Shader.PropertyToID("_BaseColor");
    float crateOpenT = -1f;
    Camera cam;

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        var d = ObjectiveDirector.I;
        if (d) d.Completed += OnCompleted;
        SetAll(false);
    }

    void OnDestroy()
    {
        var d = ObjectiveDirector.I;
        if (d) d.Completed -= OnCompleted;
    }

    void OnCompleted(ObjectiveStep s)
    {
        if (s.kind == ObjectiveKind.Supply) crateOpenT = 0f;
        if (s.kind == ObjectiveKind.Helicopter && helicopter) helicopter.Leave();
        if (s.kind == ObjectiveKind.Flare && Fx.I) { Fx.I.Spark(flare.transform.position + Vector3.up * 0.5f, Vector3.up); Fx.I.Dust(flare.transform.position); }
    }

    void SetAll(bool on)
    {
        if (beacon) beacon.gameObject.SetActive(on);
        if (zoneRing) zoneRing.gameObject.SetActive(on);
        if (zoneFill) zoneFill.gameObject.SetActive(on);
        if (flare) flare.SetActive(false);
        if (supplyCrate && crateOpenT < 0f) supplyCrate.SetActive(false);
    }

    void Tint(Renderer r, Color c)
    {
        if (!r) return;
        r.GetPropertyBlock(mpb);
        mpb.SetColor(ColorId, c);
        r.SetPropertyBlock(mpb);
    }

    void LateUpdate()
    {
        var d = ObjectiveDirector.I;
        if (!cam) cam = Camera.main;
        UpdateCrateOutro();
        if (!d || !d.HasActive)
        {
            if (beacon && beacon.gameObject.activeSelf) SetAll(false);
            return;
        }
        if (beacon && !beacon.gameObject.activeSelf) SetAll(true);
        var kind = d.Step.kind;
        Vector3 p = d.TargetLocal;
        Color c = kindColors[(int)kind];
        float t = Time.time;

        if (beacon)
        {
            beacon.position = p;
            // Billboard around Y toward the camera.
            if (cam) { Vector3 f = cam.transform.position - p; f.y = 0f; if (f.sqrMagnitude > 0.01f) beacon.rotation = Quaternion.LookRotation(-f.normalized, Vector3.up); }
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 3f);
            Tint(beaconRenderer, new Color(c.r, c.g, c.b, d.PlayerInZone ? 0.35f : 0.8f * pulse));
        }
        bool zone = d.Step.hold >= Objectives.TowerHold;
        if (zoneRing)
        {
            zoneRing.gameObject.SetActive(zone || kind == ObjectiveKind.Supply);
            zoneRing.position = p + Vector3.up * 0.05f;
            float r = d.Step.radius * 2f / 0.86f; // ring texture rim sits at 86% of the quad
            zoneRing.localScale = new Vector3(r, r, 1f);
            Tint(zoneRingRenderer, new Color(c.r, c.g, c.b, d.PlayerInZone ? 1f : 0.55f + 0.25f * Mathf.Sin(t * 4f)));
        }
        if (zoneFill)
        {
            zoneFill.gameObject.SetActive(d.Step.hold > 0f);
            zoneFill.position = p + Vector3.up * 0.04f;
            float f = d.Step.radius * 2f * Mathf.Max(0.02f, d.Progress01);
            zoneFill.localScale = new Vector3(f, f, 1f);
            Tint(zoneFillRenderer, new Color(c.r, c.g, c.b, 0.28f));
        }
        if (flare)
        {
            bool on = kind == ObjectiveKind.Flare;
            if (flare.activeSelf != on) flare.SetActive(on);
            if (on) { flare.transform.position = p; flare.transform.rotation = Quaternion.Euler(0f, t * 60f, 0f); }
        }
        if (supplyCrate && crateOpenT < 0f)
        {
            bool on = kind == ObjectiveKind.Supply;
            if (supplyCrate.activeSelf != on) supplyCrate.SetActive(on);
            if (on)
            {
                supplyCrate.transform.position = p;
                if (supplyLid) supplyLid.localRotation = Quaternion.Euler(-Mathf.Lerp(0f, 25f, d.Progress01) - (d.PlayerInZone ? Mathf.Abs(Mathf.Sin(t * 20f)) * 4f : 0f), 0f, 0f);
            }
        }
        if (helicopter && kind == ObjectiveKind.Helicopter) helicopter.Approach(p, d.HeliApproach01);
    }

    void UpdateCrateOutro()
    {
        if (crateOpenT < 0f || !supplyCrate) return;
        crateOpenT += Time.deltaTime;
        if (supplyLid) supplyLid.localRotation = Quaternion.Euler(-Mathf.Lerp(25f, 110f, Mathf.Clamp01(crateOpenT / 0.3f)), 0f, 0f);
        if (crateOpenT > 3f) { crateOpenT = -1f; supplyCrate.SetActive(false); }
    }
}
