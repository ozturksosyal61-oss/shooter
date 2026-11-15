using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level-up screen: the game slows to a crawl and three upgrade cards (rarity-coloured) pop in; tapping one
/// (or 1/2/3 on a keyboard) applies it. Input to the game is blocked while it is open; presses during the first
/// moment are ignored so a finger that was already shooting can't pick by accident.
/// </summary>
public class CardPicker : MonoBehaviour
{
    public static CardPicker I { get; private set; }

    public CanvasGroup group;
    public Text header;
    public RectTransform[] cards;
    public Image[] backgrounds;
    public Image[] rarityStrips;
    public Text[] titles;
    public Text[] descriptions;
    public Text[] levelTexts;
    public Text[] icons;
    public Text[] rarityTexts;
    [Range(0.01f, 1f)] public float slowFactor = 0.06f;
    public float inputDelay = 0.35f;

    readonly List<CardId> offer = new List<CardId>(3);
    float openT;

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
        if (!IsOpen)
        {
            if (prog.PendingPicks > 0 && !gm.IsGameOver && !gm.player.IsDead) Open();
            return;
        }
        if (gm.IsGameOver) { Close(); return; }

        openT += Time.unscaledDeltaTime;
        group.alpha = Mathf.Clamp01(openT / 0.15f);
        for (int i = 0; i < cards.Length; i++)
        {
            float t = Mathf.Clamp01((openT - i * 0.06f) / 0.22f);
            float pop = t < 1f ? Mathf.LerpUnclamped(0.4f, 1f, 1f - (1f - t) * (1f - t) * (1f - 2.2f * t)) : 1f;
            cards[i].localScale = Vector3.one * pop;
        }
        var input = TouchControls.I;
        int pressed = input ? input.CardPressed : -1;
        if (pressed >= 0 && pressed < offer.Count && openT >= inputDelay) Pick(pressed);
    }

    void Open()
    {
        offer.Clear();
        offer.AddRange(Upgrades.I.Offer(cards.Length));
        if (offer.Count == 0) { Progression.I.ConsumePick(); return; }
        IsOpen = true;
        openT = 0f;
        group.gameObject.SetActive(true);
        group.alpha = 0f;
        header.text = Loc.F("cards.header", Progression.I.Level);
        for (int i = 0; i < cards.Length; i++)
        {
            bool on = i < offer.Count;
            cards[i].gameObject.SetActive(on);
            if (!on) continue;
            var d = Upgrades.Get(offer[i]);
            int next = Upgrades.I.Level(offer[i]) + 1;
            Color rc = Upgrades.RarityColors[(int)d.rarity];
            backgrounds[i].color = Color.Lerp(new Color(0.08f, 0.09f, 0.11f, 0.96f), rc, 0.18f);
            rarityStrips[i].color = rc;
            titles[i].text = d.Title;
            descriptions[i].text = d.Desc(next);
            levelTexts[i].text = next == 1 ? Loc.T("cards.new") : Loc.F("cards.level", next, d.maxLevel);
            levelTexts[i].color = next == 1 ? new Color(0.5f, 1f, 0.5f) : Color.white;
            icons[i].text = d.Icon;
            icons[i].color = rc;
            rarityTexts[i].text = Upgrades.RarityName(d.rarity);
            rarityTexts[i].color = rc;
            cards[i].localScale = Vector3.one * 0.4f;
        }
        if (TouchControls.I) TouchControls.I.Blocked = true;
        GameManager.I.SetSlow(slowFactor);
        GameAudio.Play(SfxId.LevelUp, GameManager.I.player.transform.position);
    }

    void Pick(int i)
    {
        var id = offer[i];
        Upgrades.I.Take(id);
        Progression.I.ConsumePick();
        GameAudio.Play(SfxId.CardPick, GameManager.I.player.transform.position);
        var d = Upgrades.Get(id);
        if (DamageNumbers.I)
            DamageNumbers.I.ShowText(GameManager.I.player.transform.position + Vector3.up * 2.4f, Loc.Upper(d.Title), Upgrades.RarityColors[(int)d.rarity]);
        Close();
    }

    void Close()
    {
        IsOpen = false;
        group.alpha = 0f;
        group.gameObject.SetActive(false);
        if (TouchControls.I) TouchControls.I.Blocked = false;
        if (GameManager.I) GameManager.I.SetSlow(1f);
    }

    /// <summary>Screen position of card <paramref name="i"/> (bots click it).</summary>
    public Vector2 CardScreenPoint(int i) => RectTransformUtility.WorldToScreenPoint(null, cards[i].position);
}
