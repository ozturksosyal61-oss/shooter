using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Daily reward (redesign, docs/ui-ref 08): 7 day cards (claimed ✓ green / today / tomorrow dashed / future, day 7 big),
/// streak bar, claim button or "come back tomorrow" countdown. Opens itself on the menu when today's reward waits.
/// </summary>
public class DailyLoginPanel : MonoBehaviour
{
    public GameObject root;
    public Image[] days, dayStrokes;
    public GameObject[] dayChecks, dayIcons, dayDash;
    public TMP_Text[] dayRewards, dayLabels, dayTags;
    public GameObject[] dayTagBgs;
    public RectTransform streakFill;
    public TMP_Text streakText;
    public Button claim, close;
    public TMP_Text claimLabel;

    void Awake()
    {
        claim.onClick.AddListener(() => { if (Profile.ClaimLogin()) GameAudio.Play(SfxId.Purchase, Vector3.zero); Refresh(); });
        close.onClick.AddListener(() => root.SetActive(false));
        root.SetActive(false);
        Loc.Changed += Refresh;
    }

    void OnDestroy() => Loc.Changed -= Refresh;

    public void Open() { root.SetActive(true); Refresh(); }

    void Refresh()
    {
        if (!root.activeSelf) return;
        int next = Profile.LoginClaimable;
        int done = next >= 0 ? next : Profile.LoginStreakIndex + 1; // days already claimed in this streak
        int tomorrow = next >= 0 ? -1 : done % days.Length;
        for (int i = 0; i < days.Length; i++)
        {
            var r = Economy.LoginRewards[i];
            bool claimed = i < done, today = i == next, big = i == days.Length - 1;
            dayLabels[i].text = big ? Loc.F("login.day_big", i + 1) : Loc.F("login.day", i + 1);
            dayRewards[i].text = (r.gold > 0 ? Loc.F("shop.gold", r.gold) : "") + (r.gems > 0 ? "\n<color=#5CCBF5>+" + Loc.F("shop.gems", r.gems) + "</color>" : "");
            dayChecks[i].SetActive(claimed);
            dayIcons[i].SetActive(!claimed);
            days[i].color = claimed ? UiKit.Hex(0x24331A) : big ? UiKit.Hex(0x12273A) : today ? UiKit.Hex(0x3A2C0E) : UiKit.Card;
            dayStrokes[i].color = claimed ? UiKit.Hex(0x4F7A0C) : big ? UiKit.Hex(0x2C6E92) : UiKit.Line;
            dayStrokes[i].enabled = !(today || i == tomorrow);
            dayDash[i].SetActive(today || i == tomorrow);
            dayLabels[i].color = claimed ? UiKit.Success : big ? UiKit.Gem : today || i == tomorrow ? UiKit.Hazard : UiKit.Secondary;
            dayTagBgs[i].SetActive(today || i == tomorrow);
            dayTags[i].text = Loc.T(today ? "login.today" : "login.tomorrow");
        }
        streakFill.anchorMax = new Vector2(Mathf.Clamp01(done / (float)days.Length), 1f);
        streakText.text = done + "/" + days.Length;
        claim.interactable = next >= 0;
        var pb = claim.GetComponent<PressButton>();
        if (pb) pb.SetStyle(next >= 0 ? UiStyle.Yellow : UiStyle.Disabled);
    }

    void Update()
    {
        if (!root.activeSelf || claimLabel == null) return;
        claimLabel.text = Profile.LoginClaimable >= 0 ? Loc.T("login.claim") : Loc.F("login.come_back", UiKit.Time(UiKit.UntilUtcMidnight()));
    }
}
