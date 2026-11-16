using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Story HUD (view only — listens to <see cref="ObjectiveDirector"/>): radio messages in the top-right corner with
/// a crackle and a typewriter effect (queued), the objective tracker (title, hold progress, distance) and a marker
/// that sits on the target when it is on screen or clings to the screen edge as a direction arrow with the distance.
/// </summary>
public class ObjectiveHUD : MonoBehaviour
{
    [Header("Radio")]
    public CanvasGroup radioGroup;
    public RectTransform radioRoot;
    public Text radioText;
    public Image radioIcon;
    public float charsPerSecond = 55f;
    public float holdSeconds = 4.5f;

    [Header("Tracker")]
    public CanvasGroup trackerGroup;
    public Text trackerTitle;
    public Text trackerInfo;
    public Image trackerIcon;
    public RectTransform progressFill;
    public GameObject progressRoot;

    [Header("Marker")]
    public RectTransform marker;
    public Image markerArrow;
    public Image markerIcon;
    public Text markerDistance;
    public float edgeMargin = 90f;
    public float topMargin = 230f;

    public Sprite[] kindIcons = new Sprite[4];
    public Color[] kindColors =
    {
        new Color(1f, 0.3f, 0.2f), new Color(0.35f, 0.9f, 1f), new Color(0.45f, 1f, 0.4f), new Color(1f, 0.85f, 0.2f),
    };

    readonly RadioMsg[] queue = new RadioMsg[6];
    int qHead, qCount;
    string current;
    float shownT = -1f, typed;
    int lastDist = -1, lastHold = -1;
    ObjectiveKind lastKind = (ObjectiveKind)(-1);
    Camera cam;
    RectTransform root;
    const int MaxNum = 400;
    readonly string[] distCache = new string[MaxNum], holdCache = new string[MaxNum];
    string[] prefixes = new string[0];
    Lang cacheLang;

    /// <summary>Message on screen right now (tests).</summary>
    public string CurrentRadio => current;
    public bool MarkerOnEdge { get; private set; }
    public bool MarkerVisible => marker && marker.gameObject.activeSelf;

    // Cached per value so the per-frame path does not allocate (filled lazily, reset on language change).
    string Cached(string[] cache, string key, int v)
    {
        if (cacheLang != Loc.Current) { System.Array.Clear(distCache, 0, MaxNum); System.Array.Clear(holdCache, 0, MaxNum); cacheLang = Loc.Current; }
        return cache[v] ??= Loc.F(key, v);
    }

    void Start()
    {
        root = (RectTransform)transform;
        if (radioGroup) radioGroup.alpha = 0f;
        if (trackerGroup) trackerGroup.alpha = 0f;
        if (marker) marker.gameObject.SetActive(false);
        var d = ObjectiveDirector.I;
        if (d) { d.Radio += OnRadio; d.Completed += OnCompleted; }
    }

    void OnDestroy()
    {
        var d = ObjectiveDirector.I;
        if (d) { d.Radio -= OnRadio; d.Completed -= OnCompleted; }
    }

    void OnRadio(RadioMsg m)
    {
        if (qCount == queue.Length) { qHead = (qHead + 1) % queue.Length; qCount--; }
        queue[(qHead + qCount) % queue.Length] = m;
        qCount++;
    }

    void OnCompleted(ObjectiveStep s)
    {
        var hud = GameManager.I ? GameManager.I.hud : null;
        int bonus = ObjectiveDirector.I.LastBonus;
        if (!hud) return;
        if (s.kind == ObjectiveKind.Helicopter) hud.ShowBanner(Loc.T("obj.heli_title"), Loc.F("obj.heli_sub", bonus), new Color(1f, 0.85f, 0.2f));
        else hud.ShowBanner(Loc.T("obj.done"), Loc.F("obj.done_sub", bonus), new Color(0.55f, 1f, 0.45f));
    }

    static string Format(RadioMsg m) =>
        Loc.F(m.key, m.argKey != null ? Loc.T(m.argKey) : "", m.num);

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        UpdateRadio(dt);
        UpdateTracker();
    }

    void UpdateRadio(float dt)
    {
        if (!radioGroup) return;
        var gm = GameManager.I;
        if (shownT < 0f && qCount > 0 && gm && !gm.IsGameOver)
        {
            current = Format(queue[qHead]);
            // Typewriter prefixes built once per message (no per-frame substring allocation).
            if (prefixes.Length < current.Length + 1) prefixes = new string[current.Length + 1];
            for (int i = 0; i <= current.Length; i++) prefixes[i] = current.Substring(0, i);
            qHead = (qHead + 1) % queue.Length;
            qCount--;
            shownT = 0f;
            typed = 0f;
            radioText.text = "";
            GameAudio.Play(SfxId.Radio, gm.player ? gm.player.transform.position : Vector3.zero);
        }
        if (shownT < 0f) { radioGroup.alpha = Mathf.MoveTowards(radioGroup.alpha, 0f, dt * 4f); return; }

        shownT += dt;
        int before = Mathf.FloorToInt(typed);
        typed = Mathf.Min(current.Length, typed + dt * charsPerSecond);
        int now = Mathf.FloorToInt(typed);
        if (now != before) radioText.text = prefixes[now];
        float life = current.Length / charsPerSecond + holdSeconds;
        radioGroup.alpha = shownT < 0.12f ? shownT / 0.12f : shownT > life ? Mathf.Clamp01(1f - (shownT - life) / 0.35f) : 1f;
        if (radioIcon) radioIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, now < current.Length ? Mathf.Sin(shownT * 40f) * 6f : 0f);
        if (radioRoot) radioRoot.anchoredPosition = new Vector2(shownT < 0.18f ? Mathf.Lerp(80f, 0f, shownT / 0.18f) : 0f, radioRoot.anchoredPosition.y);
        // A queued message cuts the hold short.
        if (shownT > life + 0.35f || (qCount > 0 && shownT > current.Length / charsPerSecond + 1.6f)) shownT = -1f;
    }

    void UpdateTracker()
    {
        var d = ObjectiveDirector.I;
        var gm = GameManager.I;
        if (!cam) cam = Camera.main;
        bool on = d && d.HasActive && gm && !gm.IsGameOver && cam;
        if (trackerGroup) trackerGroup.alpha = Mathf.MoveTowards(trackerGroup.alpha, on ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        if (!on)
        {
            if (marker && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
            lastKind = (ObjectiveKind)(-1);
            return;
        }
        var kind = d.Step.kind;
        Color col = kindColors[(int)kind];
        if (kind != lastKind)
        {
            lastKind = kind;
            trackerTitle.text = Loc.T("obj." + kind);
            if (trackerIcon) { trackerIcon.sprite = kindIcons[(int)kind]; trackerIcon.color = col; }
            if (markerIcon) { markerIcon.sprite = kindIcons[(int)kind]; markerIcon.color = col; }
            if (markerArrow) markerArrow.color = col;
            lastDist = lastHold = -1;
        }

        Vector3 target = d.TargetLocal;
        Vector3 pp = gm.player.transform.position;
        Vector3 delta = target - pp; delta.y = 0f;
        int dist = Mathf.Clamp(Mathf.RoundToInt(delta.magnitude), 0, MaxNum - 1);
        bool holding = d.Step.hold > 0f && (d.PlayerInZone || d.Held > 0f);
        if (progressRoot && progressRoot.activeSelf != (d.Step.hold > 0f)) progressRoot.SetActive(d.Step.hold > 0f);
        if (progressFill) progressFill.anchorMax = new Vector2(d.Progress01, 1f);
        if (holding)
        {
            int left = Mathf.Clamp(Mathf.CeilToInt(d.Hold - d.Held), 0, MaxNum - 1);
            if (left != lastHold) { lastHold = left; lastDist = -1; trackerInfo.text = Cached(holdCache, "obj.hold", left); }
        }
        else if (dist != lastDist)
        {
            lastDist = dist; lastHold = -1;
            trackerInfo.text = Cached(distCache, "obj.distance", dist);
            if (markerDistance) markerDistance.text = trackerInfo.text;
        }
        PlaceMarker(target, d.PlayerInZone);
    }

    void PlaceMarker(Vector3 target, bool inZone)
    {
        if (!marker) return;
        Vector3 vp = cam.WorldToViewportPoint(target + Vector3.up * 2.2f);
        bool behind = vp.z < 0f;
        bool onScreen = !behind && vp.x > 0.04f && vp.x < 0.96f && vp.y > 0.06f && vp.y < 0.9f;
        if (inZone) { if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false); return; }
        if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
        Vector2 size = root.rect.size;
        MarkerOnEdge = !onScreen;
        float t = Time.unscaledTime;
        if (onScreen)
        {
            Vector2 pos = new Vector2((vp.x - 0.5f) * size.x, (vp.y - 0.5f) * size.y + 30f + Mathf.Sin(t * 4f) * 10f);
            marker.anchoredPosition = pos;
            markerArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
            markerArrow.rectTransform.anchoredPosition = new Vector2(0f, -62f);
        }
        else
        {
            Vector2 c = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            if (behind) c = -c;
            if (c.sqrMagnitude < 0.0001f) c = Vector2.up;
            Vector2 half = size * 0.5f - new Vector2(edgeMargin, edgeMargin);
            Vector2 dirPx = new Vector2(c.x * size.x, c.y * size.y);
            float k = Mathf.Min(Mathf.Abs(half.x / Mathf.Max(0.001f, Mathf.Abs(dirPx.x))), Mathf.Abs(half.y / Mathf.Max(0.001f, Mathf.Abs(dirPx.y))));
            Vector2 pos = dirPx * k;
            pos.y = Mathf.Min(pos.y, size.y * 0.5f - topMargin); // keep clear of the score / combo
            marker.anchoredPosition = pos;
            float ang = Mathf.Atan2(dirPx.y, dirPx.x);
            markerArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ang * Mathf.Rad2Deg);
            markerArrow.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 62f;
        }
        float s = 1f + 0.08f * Mathf.Sin(t * 6f);
        marker.localScale = new Vector3(s, s, 1f);
    }
}
