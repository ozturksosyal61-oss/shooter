using UnityEngine;
using UnityEngine.UI;

/// <summary>Before an endless run: pick an owned weapon (locked ones jump to the shop), then start.</summary>
public class LoadoutPanel : MonoBehaviour
{
    public GameObject root;
    public Button[] weaponButtons;   // indexed by WeaponId
    public Text[] weaponLabels, weaponStates;
    public Button start, back;
    public Text info;
    public ShopUI shop;
    public Color on = new Color(1f, 0.55f, 0.1f), off = new Color(0.34f, 0.4f, 0.22f), locked = new Color(0.17f, 0.2f, 0.11f);

    void Awake()
    {
        for (int i = 0; i < weaponButtons.Length; i++)
        {
            var w = (WeaponId)i;
            weaponButtons[i].onClick.AddListener(() =>
            {
                Click();
                if (Profile.Owns(w)) Profile.SelectWeapon(w);
                else { shop.Open(ShopUI.Tab.Weapons, (int)w); }
                Refresh();
            });
        }
        start.onClick.AddListener(() => { Click(); LoadingScreen.Load(LoadingScreen.GameScene); });
        back.onClick.AddListener(() => { Click(); root.SetActive(false); });
        root.SetActive(false);
        Profile.Changed += Refresh;
    }

    void OnDestroy() => Profile.Changed -= Refresh;
    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Open() { root.SetActive(true); Refresh(); }

    void Refresh()
    {
        if (!root.activeSelf) return;
        var sel = Profile.SelectedWeapon;
        for (int i = 0; i < weaponButtons.Length; i++)
        {
            var w = (WeaponId)i;
            bool owned = Profile.Owns(w);
            weaponLabels[i].text = Loc.T(Economy.Weapon(w).Key);
            weaponStates[i].text = owned ? (w == sel ? Loc.T("shop.equipped") : Loc.T("loadout.pick")) : Loc.F("shop.gold", Economy.Weapon(w).price);
            weaponButtons[i].image.color = w == sel ? on : owned ? off : locked;
        }
        int cards = Profile.BonusLevel(BonusId.StartCard);
        info.text = cards > 0 ? Loc.F("loadout.start_cards", cards) : "";
    }
}

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

/// <summary>Three daily quests with progress and claim buttons.</summary>
public class QuestsPanel : MonoBehaviour
{
    public GameObject root;
    public Text[] titles, progress;
    public Button[] claims;
    public Button close;

    void Awake()
    {
        for (int i = 0; i < claims.Length; i++) { int s = i; claims[i].onClick.AddListener(() => { if (Profile.ClaimQuest(s)) GameAudio.Play(SfxId.Purchase, Vector3.zero); Refresh(); }); }
        close.onClick.AddListener(() => root.SetActive(false));
        root.SetActive(false);
    }

    public void Open() { root.SetActive(true); Refresh(); }

    public static bool AnyClaimable()
    {
        foreach (var q in Profile.Quests) if (!q.claimed && q.progress >= Economy.QuestPool[q.index].target) return true;
        return false;
    }

    void Refresh()
    {
        var qs = Profile.Quests;
        for (int i = 0; i < titles.Length; i++)
        {
            bool has = i < qs.Count;
            titles[i].transform.parent.gameObject.SetActive(has);
            if (!has) continue;
            var q = qs[i]; var d = Economy.QuestPool[q.index];
            titles[i].text = Loc.F(d.Key, d.target);
            progress[i].text = Loc.F("quest.progress", q.progress, d.target, d.reward);
            bool ready = !q.claimed && q.progress >= d.target;
            claims[i].interactable = ready;
            claims[i].GetComponentInChildren<Text>().text = Loc.T(q.claimed ? "quest.claimed" : "quest.claim");
        }
    }
}
