using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss UI: intro title ("KASAP" slams in under the camera swing), big health bar across the top with a lagging
/// white bar, enraged colour and a blinking "ZAYIF!" tag while the boss is winded. Read-only on boss state.
/// </summary>
public class BossHUD : MonoBehaviour
{
    public CanvasGroup barGroup;
    public RectTransform barFill;
    public RectTransform barLag;
    public Image barFillImage;
    public TMP_Text barName;
    public TMP_Text weakTag;
    public CanvasGroup introGroup;
    public TMP_Text introTitle;
    public TMP_Text introSub;
    public float introSeconds = 2.4f;

    static readonly Color FillColor = new Color(0.86f, 0.12f, 0.1f);
    static readonly Color RageColor = new Color(1f, 0.38f, 0.05f);
    static readonly Color WeakColor = new Color(1f, 0.85f, 0.2f);

    Zombie boss;
    ButcherZombie butcher;
    float shown, lag = 1f, introT = -1f, barAlpha;
    bool subscribed;

    public bool BarVisible => barGroup && barGroup.alpha > 0.5f;
    public bool WeakShown => weakTag && weakTag.gameObject.activeSelf;

    void Start()
    {
        barGroup.alpha = 0f;
        introGroup.alpha = 0f;
        weakTag.gameObject.SetActive(false);
        Subscribe();
    }

    void Subscribe()
    {
        if (subscribed || !BossDirector.I) return;
        BossDirector.I.BossSpawned += OnSpawned;
        subscribed = true;
    }

    void OnDestroy()
    {
        if (subscribed && BossDirector.I) BossDirector.I.BossSpawned -= OnSpawned;
    }

    void OnSpawned(Zombie z)
    {
        boss = z;
        butcher = z as ButcherZombie;
        shown = lag = 1f;
        introT = 0f;
        string key = BossRules.NameKey(BossDirector.I ? BossDirector.I.CurrentType : BossType.Butcher);
        barName.text = Loc.Upper(Loc.T(key));
        introTitle.text = Loc.Upper(Loc.T(key));
        introSub.text = Loc.T(key + ".sub");
    }

    void Update()
    {
        Subscribe();
        float dt = Time.unscaledDeltaTime;
        bool alive = boss && boss.Damageable && boss.gameObject.activeInHierarchy;

        // Intro title: punch in, hold, fade.
        if (introT >= 0f)
        {
            introT += dt;
            float k = introT / introSeconds;
            float s = introT < 0.25f ? Mathf.Lerp(2.2f, 1f, introT / 0.25f) : 1f + 0.02f * Mathf.Sin(introT * 8f);
            introTitle.rectTransform.localScale = Vector3.one * s;
            introGroup.alpha = k < 0.08f ? k / 0.08f : k > 0.8f ? Mathf.Clamp01((1f - k) / 0.2f) : 1f;
            if (k >= 1f) { introT = -1f; introGroup.alpha = 0f; }
        }

        barAlpha = Mathf.MoveTowards(barAlpha, alive ? 1f : 0f, dt * (alive ? 2f : 1f));
        barGroup.alpha = barAlpha;
        if (!boss) return;

        float h = alive ? Mathf.Clamp01(boss.Health / Mathf.Max(1f, boss.MaxHealth)) : 0f;
        shown = Mathf.MoveTowards(shown, h, dt * 2f);
        lag = h < lag ? Mathf.MoveTowards(lag, h, dt * 0.35f) : h;
        barFill.anchorMax = new Vector2(shown, 1f);
        barLag.anchorMax = new Vector2(Mathf.Max(lag, shown), 1f);

        bool weak = alive && butcher && butcher.IsWeak;
        bool rage = alive && butcher && butcher.Enraged;
        barFillImage.color = weak ? Color.Lerp(FillColor, WeakColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f)) : rage ? RageColor : FillColor;
        weakTag.gameObject.SetActive(weak);
        if (weak)
        {
            weakTag.rectTransform.localScale = Vector3.one * (1f + 0.12f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f)));
        }
    }
}
