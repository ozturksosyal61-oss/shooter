using UnityEngine;
using UnityEngine.UI;

/// <summary>Daily login: 7-day cycle, opens itself on the menu when today's reward is waiting.</summary>
public class DailyLoginPanel : MonoBehaviour
{
    public GameObject root;
    public Image[] days;
    public Text[] dayRewards, dayLabels;
    public Button claim, close;
    public Color today = new Color(1f, 0.55f, 0.1f), done = new Color(0.25f, 0.3f, 0.16f), future = new Color(0.34f, 0.4f, 0.22f);

    void Awake()
    {
        claim.onClick.AddListener(() => { if (Profile.ClaimLogin()) GameAudio.Play(SfxId.Purchase, Vector3.zero); Refresh(); });
        close.onClick.AddListener(() => root.SetActive(false));
        root.SetActive(false);
    }

    public void Open() { root.SetActive(true); Refresh(); }

    void Refresh()
    {
        int next = Profile.LoginClaimable;
        int cur = next >= 0 ? next : Profile.LoginStreakIndex;
        for (int i = 0; i < days.Length; i++)
        {
            var r = Economy.LoginRewards[i];
            if (dayLabels != null && i < dayLabels.Length) dayLabels[i].text = Loc.F("login.day", i + 1);
            dayRewards[i].text = (r.gold > 0 ? Loc.F("shop.gold", r.gold) : "") + (r.gems > 0 ? "\n" + Loc.F("shop.gems", r.gems) : "");
            days[i].color = i == cur && next >= 0 ? today : i < cur || (next < 0 && i <= cur) ? done : future;
        }
        claim.interactable = next >= 0;
        claim.GetComponentInChildren<Text>().text = Loc.T(next >= 0 ? "login.claim" : "login.claimed");
    }
}
