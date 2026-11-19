using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Daily quests (redesign, docs/ui-ref 09): three rows (icon, title, progress bar, reward, AL / GİT / claimed),
/// reset countdown, today's total. GİT opens the game-mode screen.
/// </summary>
public class QuestsPanel : MonoBehaviour
{
    public GameObject root;
    public GameObject[] rows;
    public Image[] rowBgs, iconTiles, icons;
    public TMP_Text[] titles, progress, rewards;
    public RectTransform[] bars;
    public Image[] barFills;
    public Button[] claims;
    public Button close;
    public TMP_Text timer, total;
    public Sprite[] kindIcons;      // by QuestKind
    public Sprite checkIcon;

    static readonly Color[] KindColors =
    {
        UiKit.DangerAlt, UiKit.Purple, UiKit.DangerAlt, UiKit.Success, UiKit.Hazard, UiKit.Gem, UiKit.Gold,
    };

    void Awake()
    {
        for (int i = 0; i < claims.Length; i++)
        {
            int s = i;
            claims[i].onClick.AddListener(() =>
            {
                var qs = Profile.Quests;
                if (s >= qs.Count) return;
                var q = qs[s]; var d = Economy.QuestPool[q.index];
                if (!q.claimed && q.progress >= d.target) { if (Profile.ClaimQuest(s)) GameAudio.Play(SfxId.Purchase, Vector3.zero); Refresh(); }
                else if (!q.claimed) { root.SetActive(false); if (MainMenu.I) MainMenu.I.ShowPlay(true); }
            });
        }
        close.onClick.AddListener(() => root.SetActive(false));
        root.SetActive(false);
        Loc.Changed += Refresh;
    }

    void OnDestroy() => Loc.Changed -= Refresh;

    public void Open() { root.SetActive(true); Refresh(); }

    public static bool AnyClaimable()
    {
        foreach (var q in Profile.Quests) if (!q.claimed && q.progress >= Economy.QuestPool[q.index].target) return true;
        return false;
    }

    void Refresh()
    {
        if (!root.activeSelf) return;
        var qs = Profile.Quests;
        int sum = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            bool has = i < qs.Count;
            rows[i].SetActive(has);
            if (!has) continue;
            var q = qs[i]; var d = Economy.QuestPool[q.index];
            sum += d.reward;
            bool ready = !q.claimed && q.progress >= d.target;
            int k = Mathf.Clamp((int)d.kind, 0, KindColors.Length - 1);
            titles[i].text = Loc.F(d.Key, d.target);
            progress[i].text = Mathf.Min(q.progress, d.target) + "/" + d.target;
            rewards[i].text = d.reward.ToString();
            bars[i].anchorMax = new Vector2(Mathf.Clamp01(q.progress / (float)d.target), 1f);
            barFills[i].color = ready || q.claimed ? UiKit.Success : UiKit.Hazard;
            icons[i].sprite = kindIcons[Mathf.Min((int)d.kind, kindIcons.Length - 1)];
            icons[i].color = KindColors[k];
            iconTiles[i].color = new Color(KindColors[k].r * 0.22f, KindColors[k].g * 0.22f, KindColors[k].b * 0.22f, 1f);
            rowBgs[i].color = ready ? UiKit.Hex(0x16200F) : UiKit.Card;
            var pb = claims[i].GetComponent<PressButton>();
            claims[i].interactable = !q.claimed;
            if (pb)
            {
                pb.SetStyle(q.claimed ? UiStyle.Disabled : ready ? UiStyle.Green : UiStyle.Dark);
                pb.SetLabel(Loc.T(q.claimed ? "quest.claimed" : ready ? "quest.claim" : "quest.go"));
                if (pb.icon) { pb.icon.gameObject.SetActive(q.claimed); pb.icon.sprite = checkIcon; }
            }
        }
        total.text = Loc.F("quest.total", sum);
    }

    void Update()
    {
        if (root.activeSelf && timer) timer.text = Loc.F("quest.reset_in", UiKit.Time(UiKit.UntilUtcMidnight()));
    }
}
