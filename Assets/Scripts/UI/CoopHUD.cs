using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Co-op HUD (C3): teammate list (name, health, down / out), "YARALANDIN" overlay with the bleed-out and revive bars,
/// "KALDIR!" markers over downed teammates (clamped to the screen edge when off-screen), the "you're reviving" bar,
/// and short toasts (teammate left, revived, connection lost). Reads <see cref="CoopTeam"/> / <see cref="NetPlayer"/> only.
/// </summary>
public class CoopHUD : MonoBehaviour
{
    [System.Serializable]
    public class Row
    {
        public GameObject root;
        public TMP_Text name, state;
        public RectTransform hpFill;
        public Image hpImage;
    }

    [System.Serializable]
    public class Marker
    {
        public RectTransform root;
        public TMP_Text label;
        public RectTransform fill;
    }

    public GameObject root;
    public Row[] rows;
    public GameObject downed;
    public TMP_Text downedTitle, downedSub, downedTimer;
    public RectTransform bleedFill, reviveFill;
    public GameObject reviveRow;
    public GameObject helping;
    public TMP_Text helpingText;
    public RectTransform helpingFill;
    public Marker[] markers;
    public CanvasGroup toast;
    public TMP_Text toastText;
    public float edgeMargin = 60f;

    Canvas canvas;
    Camera cam;
    float toastT;
    bool hooked;

    void Start()
    {
        canvas = GetComponentInParent<Canvas>();
        bool on = GameMode.IsCoop;
        if (root) root.SetActive(on);
        if (!on) { enabled = false; return; }
        if (toast) toast.alpha = 0f;
        Hook();
    }

    void Hook()
    {
        if (hooked || !CoopTeam.I) return;
        CoopTeam.I.Toast += ShowToast;
        hooked = true;
    }

    void OnDestroy() { if (hooked && CoopTeam.I) CoopTeam.I.Toast -= ShowToast; }

    public void ShowToast(string s)
    {
        if (!toastText) return;
        toastText.text = s;
        toastT = 2.6f;
    }

    void Update()
    {
        Hook();
        var gm = GameManager.I;
        var team = CoopTeam.I;
        if (!gm || !team) return;
        bool over = gm.IsGameOver;
        if (root && root.activeSelf == over) root.SetActive(!over);
        if (over) return;
        float dt = Time.unscaledDeltaTime;

        // Teammates (not me).
        int r = 0;
        foreach (var np in NetPlayer.All)
        {
            if (np == NetPlayer.Local || r >= rows.Length) continue;
            var row = rows[r++];
            var s = np.State.Value;
            row.root.SetActive(true);
            row.name.text = CoopTeam.NameOf(np);
            row.hpFill.anchorMax = new Vector2(s.Dead ? 0f : Mathf.Clamp01(s.hp01), 1f);
            row.hpImage.color = s.hp01 < 0.3f ? UiKit.Danger : UiKit.Success;
            row.state.text = s.Downed ? Loc.T("coop.state_down") : s.Dead ? Loc.T("coop.state_out") : "";
            row.state.color = s.Downed ? UiKit.DangerAlt : UiKit.Secondary;
        }
        for (; r < rows.Length; r++) rows[r].root.SetActive(false);

        // My own state.
        bool down = team.Downed, gone = team.Out;
        if (downed.activeSelf != (down || gone)) downed.SetActive(down || gone);
        if (down || gone)
        {
            downedTitle.text = Loc.T(down ? "coop.downed" : "coop.out");
            downedTitle.color = down ? UiKit.DangerAlt : UiKit.Secondary;
            downedSub.text = down ? Loc.T("coop.downed_sub")
                : team.Spectating ? Loc.F("coop.watching", WatchedName(team)) : Loc.T("coop.out_sub");
            downedTimer.text = down ? Mathf.CeilToInt(team.BleedLeft).ToString() : "";
            bleedFill.parent.gameObject.SetActive(down);
            bleedFill.anchorMax = new Vector2(team.Bleed01, 1f);
            reviveRow.SetActive(down && team.Revive01 > 0f);
            reviveFill.anchorMax = new Vector2(team.Revive01, 1f);
        }
        bool help = team.Helping;
        if (helping.activeSelf != help) helping.SetActive(help);
        if (help)
        {
            helpingText.text = Loc.F("coop.helping", CoopTeam.NameOf(team.Helping));
            helpingFill.anchorMax = new Vector2(team.Helping.State.Value.revive / 255f, 1f);
        }

        UpdateMarkers();

        if (toast)
        {
            if (toastT > 0f) toastT -= dt;
            toast.alpha = Mathf.MoveTowards(toast.alpha, toastT > 0f ? 1f : 0f, dt * 5f);
        }
    }

    static string WatchedName(CoopTeam team)
    {
        foreach (var np in NetPlayer.All) if (np.Avatar == team.Spectating) return CoopTeam.NameOf(np);
        return "";
    }

    void UpdateMarkers()
    {
        if (!cam) cam = Camera.main;
        int m = 0;
        if (cam && canvas)
        {
            float scale = canvas.scaleFactor;
            float w = Screen.width / scale, h = Screen.height / scale;
            foreach (var np in NetPlayer.All)
            {
                if (np == NetPlayer.Local || m >= markers.Length) continue;
                var s = np.State.Value;
                if (!s.Downed) continue;
                var mk = markers[m++];
                mk.root.gameObject.SetActive(true);
                Vector3 vp = cam.WorldToViewportPoint(s.pos + Vector3.up * 2.4f);
                bool behind = vp.z < 0f;
                if (behind) { vp.x = 1f - vp.x; vp.y = 1f - vp.y; }
                Vector2 p = new Vector2(vp.x * w, vp.y * h);
                bool off = behind || p.x < edgeMargin || p.x > w - edgeMargin || p.y < edgeMargin || p.y > h - edgeMargin;
                p.x = Mathf.Clamp(p.x, edgeMargin, w - edgeMargin);
                p.y = Mathf.Clamp(p.y, edgeMargin, h - edgeMargin);
                mk.root.anchoredPosition = p;
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 8f);
                mk.root.localScale = Vector3.one * (off ? 0.85f : pulse);
                mk.label.text = off ? CoopTeam.NameOf(np) : Loc.T("coop.revive_marker");
                mk.fill.anchorMax = new Vector2(s.revive / 255f, 1f);
            }
        }
        for (; m < markers.Length; m++) markers[m].root.gameObject.SetActive(false);
    }
}
