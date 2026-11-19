using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level-up screen: the game slows to a crawl, the background darkens under a "SEVİYE ATLADIN!" title and three
/// big upgrade cards flip in one after another (rarity strip with a shine sweep and glow, big icon, name,
/// description, level dots ●●○). Hover/touch grows a card; tapping one (or 1/2/3) applies it at once — gameplay
/// resumes immediately while the picked card flashes and the others fall away (unscaled outro).
/// Presses during the first moment are ignored so a finger that was already shooting can't pick by accident.
/// </summary>
public class CardPicker : MonoBehaviour
{
    public static CardPicker I { get; private set; }

    public CanvasGroup group;
    public Image dim;
    public RectTransform titleRoot;
    public TMP_Text header;
    public TMP_Text subHeader;
    public RectTransform[] cards;
    public Image[] backgrounds;
    public Image[] rarityStrips;
    public Image[] glows;
    public Image[] flashes;
    public RectTransform[] shines;
    public GameObject[] newBadges;
    public TMP_Text[] titles;
    public TMP_Text[] descriptions;
    public TMP_Text[] rarityTexts;
    public Image[] icons;
    [Tooltip("3 per card, left to right.")]
    public Image[] dots;
    public Sprite dotFull, dotEmpty;
    [Range(0.01f, 1f)] public float slowFactor = 0.06f;
    public float inputDelay = 0.35f;
    public float cardDelay = 0.09f;
    public float flipTime = 0.38f;
    public float outroTime = 0.35f;

    static readonly Color BodyBase = new Color(0.17f, 0.2f, 0.11f, 1f);

    readonly List<CardId> offer = new List<CardId>(3);
    float openT, outroT = -1f;
    int picked = -1;
    readonly float[] hoverScale = new float[3];
    readonly int[] nextLevel = new int[3];

    public bool IsOpen { get; private set; }
    public int OfferCount => offer.Count;
    public CardId OfferAt(int i) => offer[i];

    void Awake()
    {
        I = this;
        group.alpha = 0f;
        group.gameObject.SetActive(false);
    }

    void Update()
    {
        var gm = GameManager.I;
        var prog = Progression.I;
        if (!gm || !prog || !Upgrades.I) return;
        float dt = Time.unscaledDeltaTime;
        if (!IsOpen)
        {
            if (outroT >= 0f) UpdateOutro(dt);
            if (prog.PendingPicks > 0 && !gm.IsGameOver && !gm.player.IsDead && outroT < 0f) Open();
            return;
        }
        if (gm.IsGameOver) { Close(); HideNow(); return; }

        openT += dt;
        group.alpha = Mathf.Clamp01(openT / 0.15f);
        float tp = Mathf.Clamp01(openT / 0.3f);
        titleRoot.localScale = Vector3.one * (tp < 1f ? Mathf.LerpUnclamped(1.8f, 1f, EaseOutBack(tp)) : 1f + 0.03f * Mathf.Sin(openT * 4f));

        var input = TouchControls.I;
        int hover = input ? input.CardHover : -1;
        for (int i = 0; i < offer.Count; i++)
        {
            // Flip in: rotate from edge-on, scale with a little overshoot.
            float t = Mathf.Clamp01((openT - 0.08f - i * cardDelay) / flipTime);
            float e = EaseOutBack(t);
            float target = hover == i && openT >= inputDelay ? 1.07f : 1f;
            hoverScale[i] = Mathf.MoveTowards(hoverScale[i], target, dt * 1.2f);
            var c = cards[i];
            c.localRotation = Quaternion.Euler(0f, Mathf.Lerp(90f, 0f, e), Mathf.Lerp(-8f, 0f, t));
            c.localScale = Vector3.one * Mathf.LerpUnclamped(0.55f, 1f, e) * hoverScale[i];
            var rc = RarityColor(i);
            // Glow pulses (stronger for rarer cards and under the pointer).
            float rarity = (int)Upgrades.Get(offer[i]).rarity;
            float pulse = 0.25f + 0.12f * rarity + 0.1f * Mathf.Sin(openT * 3f + i);
            glows[i].color = new Color(rc.r, rc.g, rc.b, t * (pulse + (hover == i ? 0.2f : 0f)));
            // Shine sweeps across the rarity strip every ~2 s.
            float sweep = Mathf.Repeat(openT * 0.55f + i * 0.17f, 1f);
            shines[i].anchoredPosition = new Vector2(Mathf.Lerp(-120f, 580f, sweep * 1.6f), 0f);
            // The dot for the level about to be gained blinks.
            var dot = dots[i * 3 + Mathf.Clamp(nextLevel[i] - 1, 0, 2)];
            dot.transform.localScale = Vector3.one * (1f + 0.18f * Mathf.Max(0f, Mathf.Sin(openT * 6f)));
            flashes[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - t * 3f) * 0.6f * (t > 0f ? 1f : 0f));
        }

        int pressed = input ? input.CardPressed : -1;
        if (pressed >= 0 && pressed < offer.Count && openT >= inputDelay) Pick(pressed);
    }

    void Open()
    {
        offer.Clear();
        bool epic = Progression.I.EpicPicks > 0;
        offer.AddRange(Upgrades.I.Offer(cards.Length, epic ? Rarity.Epic : Rarity.Common));
        if (offer.Count == 0) { Progression.I.ConsumePick(); return; }
        IsOpen = true;
        openT = 0f;
        picked = -1;
        group.gameObject.SetActive(true);
        group.alpha = 0f;
        bool bonus = Progression.I.BonusPicks > 0;
        header.text = epic ? Loc.F("cards.boss", Loc.Upper(Loc.T(BossRules.NameKey(BossDirector.I ? BossDirector.I.CurrentType : BossType.Butcher))))
            : Loc.T(bonus ? "cards.reward" : "cards.levelup");
        subHeader.text = epic ? Loc.T("cards.boss_sub") : bonus ? Loc.T("cards.reward_sub") : Loc.F("cards.header", Progression.I.Level);
        for (int i = 0; i < cards.Length; i++)
        {
            bool on = i < offer.Count;
            cards[i].gameObject.SetActive(on);
            if (!on) continue;
            var d = Upgrades.Get(offer[i]);
            int next = Upgrades.I.Level(offer[i]) + 1;
            nextLevel[i] = next;
            hoverScale[i] = 1f;
            Color rc = RarityColor(i);
            backgrounds[i].color = Color.Lerp(BodyBase, rc, 0.12f);
            rarityStrips[i].color = rc;
            rarityTexts[i].text = Upgrades.RarityName(d.rarity);
            titles[i].text = d.Title;
            descriptions[i].text = d.Desc(next);
            icons[i].sprite = Upgrades.I.IconOf(offer[i]);
            icons[i].color = Color.Lerp(rc, Color.white, 0.35f);
            newBadges[i].SetActive(next == 1);
            for (int k = 0; k < 3; k++)
            {
                var dot = dots[i * 3 + k];
                bool shown = k < d.maxLevel;
                dot.gameObject.SetActive(shown);
                dot.sprite = k < next ? dotFull : dotEmpty;
                dot.color = k < next - 1 ? new Color(1f, 0.56f, 0.12f) : k == next - 1 ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 1f, 1f, 0.8f);
                dot.transform.localScale = Vector3.one;
            }
            cards[i].localScale = Vector3.one * 0.55f;
            cards[i].localRotation = Quaternion.Euler(0f, 90f, 0f);
            glows[i].color = new Color(rc.r, rc.g, rc.b, 0f);
            flashes[i].color = new Color(1f, 1f, 1f, 0f);
        }
        if (TouchControls.I) TouchControls.I.Blocked = true;
        GameManager.I.SetSlow(slowFactor);
        GameAudio.Play(SfxId.LevelUp, GameManager.I.player.transform.position);
    }

    Color RarityColor(int i) => Upgrades.RarityColors[(int)Upgrades.Get(offer[i]).rarity];

    void Pick(int i)
    {
        var id = offer[i];
        Upgrades.I.Take(id);
        Progression.I.ConsumePick();
        GameAudio.Play(SfxId.CardPick, GameManager.I.player.transform.position);
        var d = Upgrades.Get(id);
        if (DamageNumbers.I)
            DamageNumbers.I.ShowText(GameManager.I.player.transform.position + Vector3.up * 2.4f, Loc.Upper(d.Title), Upgrades.RarityColors[(int)d.rarity]);
        picked = i;
        Close();
        outroT = 0f;
    }

    /// <summary>Gameplay already runs again; the picked card flashes and pops, the others shrink away.</summary>
    void UpdateOutro(float dt)
    {
        outroT += dt;
        float k = Mathf.Clamp01(outroT / outroTime);
        group.alpha = 1f - k * k;
        for (int i = 0; i < offer.Count; i++)
        {
            var c = cards[i];
            if (i == picked)
            {
                c.localScale = Vector3.one * (1.08f + 0.25f * Mathf.Sin(Mathf.Min(1f, k * 1.6f) * Mathf.PI));
                flashes[i].color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - k * 1.4f));
                var rc = RarityColor(i);
                glows[i].color = new Color(rc.r, rc.g, rc.b, 0.9f * (1f - k));
            }
            else
            {
                c.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
                c.localRotation = Quaternion.Euler(0f, 0f, (i < picked ? 1f : -1f) * 25f * k);
            }
        }
        if (k >= 1f) HideNow();
    }

    void Close()
    {
        IsOpen = false;
        if (TouchControls.I) TouchControls.I.Blocked = false;
        if (GameManager.I) GameManager.I.SetSlow(1f);
    }

    void HideNow()
    {
        outroT = -1f;
        group.alpha = 0f;
        group.gameObject.SetActive(false);
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    /// <summary>Screen position of card <paramref name="i"/> (bots click it).</summary>
    public Vector2 CardScreenPoint(int i) => RectTransformUtility.WorldToScreenPoint(null, cards[i].position);
}
