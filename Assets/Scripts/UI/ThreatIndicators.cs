using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Edge-of-screen arrows pointing at threats that are off camera: every special zombie (coloured by kind,
/// pulsing) and — when <see cref="normalRange"/> &gt; 0 — ordinary zombies closer than that.
/// Pooled images, no per-frame allocation.
/// </summary>
public class ThreatIndicators : MonoBehaviour
{
    public Image template;
    public int poolSize = 12;
    public float edgeMargin = 64f;
    [Tooltip("Ordinary zombies closer than this (m) also get a (smaller, white) arrow. 0 = specials only.")]
    public float normalRange = 0f;
    public int maxNormal = 4;
    public Color shieldColor = new Color(1f, 0.2f, 0.15f);
    public Color bombColor = new Color(1f, 0.6f, 0.1f);
    public Color tongueColor = new Color(0.8f, 0.35f, 1f);
    public Color normalColor = new Color(1f, 1f, 1f, 0.75f);

    Image[] arrows;
    RectTransform root;
    Canvas canvas;
    Camera cam;

    /// <summary>Arrows shown last frame (tests / telemetry).</summary>
    public int Shown { get; private set; }
    public int ShownSpecial { get; private set; }

    void Awake()
    {
        root = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();
        arrows = new Image[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            arrows[i] = Instantiate(template, transform);
            arrows[i].gameObject.SetActive(false);
        }
        template.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        var gm = GameManager.I;
        if (!cam) cam = Camera.main;
        int used = 0;
        ShownSpecial = 0;
        if (gm && cam && !gm.IsGameOver && gm.player)
        {
            Vector3 pp = gm.player.transform.position;
            // Specials first, then the nearest ordinary zombies.
            for (int i = 0; i < Zombie.Active.Count && used < arrows.Length; i++)
            {
                var z = Zombie.Active[i];
                if (!z.IsSpecial || !z.Targetable) continue;
                if (Place(arrows[used], z, ColorOf(z.Kind), 1.15f, true)) { used++; ShownSpecial++; }
            }
            if (normalRange > 0f)
            {
                float r2 = normalRange * normalRange;
                int normals = 0;
                for (int i = 0; i < Zombie.Active.Count && used < arrows.Length && normals < maxNormal; i++)
                {
                    var z = Zombie.Active[i];
                    if (z.IsSpecial || !z.Targetable) continue;
                    Vector3 d = z.transform.position - pp; d.y = 0f;
                    if (d.sqrMagnitude > r2) continue;
                    if (Place(arrows[used], z, normalColor, 0.75f, false)) { used++; normals++; }
                }
            }
        }
        for (int i = used; i < arrows.Length; i++)
            if (arrows[i].gameObject.activeSelf) arrows[i].gameObject.SetActive(false);
        Shown = used;
    }

    Color ColorOf(ZombieKind k) => k == ZombieKind.Shield ? shieldColor : k == ZombieKind.Bomb ? bombColor : k == ZombieKind.Tongue ? tongueColor : normalColor;

    bool Place(Image img, Zombie z, Color color, float size, bool pulse)
    {
        Vector3 vp = cam.WorldToViewportPoint(z.AimPoint);
        bool behind = vp.z < 0f;
        if (!behind && vp.x > 0f && vp.x < 1f && vp.y > 0f && vp.y < 1f) return false;

        Vector2 c = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
        if (behind) c = -c;
        if (c.sqrMagnitude < 0.0001f) c = Vector2.down;
        Vector2 half = root.rect.size * 0.5f - new Vector2(edgeMargin, edgeMargin);
        // Scale the direction until it touches the inset screen rectangle.
        Vector2 dirPx = new Vector2(c.x * root.rect.width, c.y * root.rect.height);
        float k = Mathf.Min(Mathf.Abs(half.x / Mathf.Max(0.001f, Mathf.Abs(dirPx.x))), Mathf.Abs(half.y / Mathf.Max(0.001f, Mathf.Abs(dirPx.y))));
        Vector2 pos = dirPx * k;

        var rt = img.rectTransform;
        if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
        rt.anchoredPosition = pos;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dirPx.y, dirPx.x) * Mathf.Rad2Deg);
        float s = size * (pulse ? 1f + 0.15f * Mathf.Sin(Time.unscaledTime * 9f) : 1f);
        rt.localScale = new Vector3(s, s, 1f);
        img.color = color;
        return true;
    }
}
