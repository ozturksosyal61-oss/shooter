using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu (redesign, docs/ui-ref 01): HOLDOUT logo over the live city, quests / daily reward / shop list,
/// the soldier (3D preview of the selected character, holding the selected weapon) with its costume + weapon chips and
/// the big OYNA button, which opens the game-mode screen (Story / Endless / Hard / Co-op); gold / gems pills, settings, a random tip. The OZT Digital splash
/// plays once per app session (after the Unity splash).
/// </summary>
public class MainMenu : MonoBehaviour
{
    public static MainMenu I { get; private set; }

    /// <summary>Set before returning to the menu (death screen "SİLAH" / "HARİTA"): opens that panel at once.</summary>
    public enum Open { None, Loadout, Campaign }
    public static Open OpenOnStart;

    public GameObject mainButtons;
    public Button play, shop, settings;
    public TMP_Text gold, gems;
    public Button goldPlus, gemPlus;
    public RectTransform logo;
    public CanvasGroup splash;
    public RectTransform splashLogo;
    public float splashSeconds = 2.4f;
    public CanvasGroup toast;
    public TMP_Text toastText;
    public SettingsPanel settingsPanel;
    public ShopUI shopUI;
    public LoadoutPanel loadout;
    public DailyLoginPanel login;
    public QuestsPanel quests;
    public CampaignPanel campaignPanel;
    public CoopPanel coopPanel;
    public ModePanel modePanel;
    public Button questsButton, loginButton;
    [Header("Weekly event + Game Center")]
    public Button weeklyButton, leaderboardButton;
    public TMP_Text weeklySub;
    public GameObject questBadge, loginBadge;
    public TMP_Text questBadgeText, questSub, loginSub, loginTimer;

    [Header("Soldier")]
    public RawImage soldierPreview;
    public PreviewStage preview;
    public TMP_Text characterName, costumeChip, weaponChip, tip;

    static bool splashShown;
    static int modeIndex = 1; // 0 story, 1 endless, 2 hard
    float splashT = -1f, toastT;

    public bool SplashActive => splashT >= 0f;

    void Awake() => I = this;

    void Start()
    {
        play.onClick.AddListener(() => { Click(); ShowPlay(true); });
        shop.onClick.AddListener(() => { Click(); shopUI.Open(ShopUI.Tab.Weapons, (int)Profile.SelectedWeapon); });
        if (goldPlus) goldPlus.onClick.AddListener(() => { Click(); shopUI.Open(ShopUI.Tab.Gems, 1 + Economy.Products.Length); });
        if (gemPlus) gemPlus.onClick.AddListener(() => { Click(); shopUI.Open(ShopUI.Tab.Gems, 1); });
        if (questsButton) questsButton.onClick.AddListener(() => { Click(); quests.Open(); });
        if (loginButton) loginButton.onClick.AddListener(() => { Click(); login.Open(); });
        if (weeklyButton) weeklyButton.onClick.AddListener(() => { Click(); GameMode.SetWeekly(); PlayEndless(); });
        if (leaderboardButton) leaderboardButton.onClick.AddListener(() => { Click(); GameServices.ShowLeaderboards(); });
        settings.onClick.AddListener(() => { Click(); settingsPanel.Open(); });
        if (shopUI) shopUI.Closed += RefreshSoldier;
        Profile.Changed += Refresh;
        SaveSystem.Changed += Refresh;
        Loc.Changed += Refresh;
        toast.alpha = 0f;
        if (tip) tip.text = Loc.T("tip." + Random.Range(1, 11));
        Refresh();
        if (!splashShown) { splashShown = true; splashT = 0f; splash.gameObject.SetActive(true); splash.alpha = 1f; }
        else { splash.gameObject.SetActive(false); AfterSplash(); }
    }

    void OnDestroy()
    {
        Profile.Changed -= Refresh; SaveSystem.Changed -= Refresh; Loc.Changed -= Refresh;
        if (shopUI) shopUI.Closed -= RefreshSoldier;
    }

    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    void AfterSplash()
    {
        Ads.Warm();
        GameServices.Init(); // ATT prompt + SDK init + first rewarded load now, so ads are ready by the first death / shop visit
        var o = OpenOnStart;
        OpenOnStart = Open.None;
        if (GameMode.Weekly) GameMode.Set(RunMode.Endless); // back from a weekly run: the normal weapon choice again
        if (o == Open.Loadout && loadout) { loadout.Open(); return; }
        if (o == Open.Campaign && campaignPanel) { campaignPanel.Open(); return; }
        AutoLogin();
    }

    void Refresh()
    {
        var w = SaveSystem.Data.wallet;
        gold.text = w.gold.ToString();
        gems.text = w.gems.ToString();
        if (weeklySub)
        {
            int wk = WeeklyEvent.CurrentWeek;
            var rec = SaveSystem.Data.records;
            int best = rec.weeklyWeek == wk ? rec.weeklyScore : 0;
            weeklySub.text = Loc.F(best > 0 ? "weekly.sub_best" : "weekly.sub", Loc.T(WeeklyEvent.RuleKey(wk)), WeeklyEvent.DaysLeft(System.DateTime.UtcNow), best);
            // 1.1.0: the next weekly prize ("Dalga 10 → 10 elmas").
            int claimed = rec.weeklyWeek == wk ? rec.weeklyClaimed : 0;
            int next = WeeklyEvent.NextMilestone(claimed);
            if (next >= 0)
            {
                var m = WeeklyEvent.Milestones[next];
                weeklySub.text += " · " + Loc.F("weekly.next", m.wave, RunRewards.RewardText(m.gold, m.gems));
            }
            else weeklySub.text += " · " + Loc.T("weekly.all_done");
        }
        if (leaderboardButton) leaderboardButton.gameObject.SetActive(Application.platform == RuntimePlatform.IPhonePlayer || Application.isEditor);
        int open = 0;
        foreach (var q in Profile.Quests) if (!q.claimed) open++;
        if (questBadge) questBadge.SetActive(open > 0);
        if (questBadgeText) questBadgeText.text = open.ToString();
        if (questSub) questSub.text = Loc.F("menu.quests_sub", open);
        if (loginBadge) loginBadge.SetActive(Profile.LoginClaimable >= 0);
        if (loginSub)
        {
            int next = Profile.LoginClaimable;
            int idx = next >= 0 ? next : (Profile.LoginStreakIndex + 1) % Economy.LoginRewards.Length;
            loginSub.text = Loc.F(next >= 0 ? "menu.login_today" : "menu.login_tomorrow", Economy.LoginRewards[idx].gold);
        }
        RefreshSoldier();
    }

    void RefreshSoldier()
    {
        var c = Profile.SelectedCostume;
        var wpn = Profile.SelectedWeapon;
        if (characterName) characterName.text = Loc.T(Economy.IsCharacter(c) ? Economy.Costume(c).Key : "menu.soldier_name");
        if (costumeChip) costumeChip.text = Loc.T(Economy.Costume(c).Key);
        int lv = Profile.Level(wpn, WeaponStat.Damage) + Profile.Level(wpn, WeaponStat.Magazine) + Profile.Level(wpn, WeaponStat.Reload);
        if (weaponChip) weaponChip.text = Loc.F("menu.weapon_chip", Loc.T(Economy.Weapon(wpn).Key), lv);
        if (preview && (!shopUI || !shopUI.IsOpen) && (!loadout || !loadout.root.activeSelf)) preview.ShowHero(c);
    }

    /// <summary>OYNA: story → chapter select, endless / hard → weapon select.</summary>
    public void PlaySelected()
    {
        if (modeIndex == 0) { if (campaignPanel) campaignPanel.Open(); return; }
        StartMode(modeIndex == 2 ? RunMode.Hard : RunMode.Endless);
    }

    public void StartMode(RunMode m)
    {
        GameMode.Set(m);
        if (loadout) loadout.Open(); else PlayEndless();
    }

    public static void SetMode(int index) => modeIndex = Mathf.Clamp(index, 0, 2);
    public static int ModeIndex => modeIndex;

    static bool loginShown;

    /// <summary>Once per session: the daily reward pops up if it is waiting.</summary>
    void AutoLogin()
    {
        if (loginShown || !login || Profile.LoginClaimable < 0) return;
        loginShown = true;
        login.Open();
    }

    /// <summary>Opens / closes the game-mode screen.</summary>
    public void ShowPlay(bool on)
    {
        if (modePanel) { if (on) modePanel.Open(); else modePanel.root.SetActive(false); }
    }

    public void PlayEndless() => LoadingScreen.Load(LoadingScreen.GameScene);

    public void Toast(string text)
    {
        toastText.text = text;
        toastT = 1.8f;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        // Full-screen pages (modes, weapons, shop, chapters) hide the main column behind them.
        bool page = (modePanel && modePanel.root.activeSelf) || (shopUI && shopUI.IsOpen) || (loadout && loadout.root.activeSelf)
            || (campaignPanel && campaignPanel.root.activeSelf);
        if (mainButtons && mainButtons.activeSelf == page) mainButtons.SetActive(!page);
        if (loginTimer) loginTimer.text = Profile.LoginClaimable >= 0 ? "" : UiKit.Time(UiKit.UntilUtcMidnight()).Substring(0, 5);
        if (splashT >= 0f)
        {
            splashT += dt;
            float k = splashT / splashSeconds;
            splashLogo.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.04f, k);
            var g = splashLogo.GetComponent<CanvasGroup>();
            if (g) g.alpha = k < 0.25f ? k / 0.25f : k > 0.75f ? Mathf.Clamp01((1f - k) / 0.25f) : 1f;
            if (k > 0.85f) splash.alpha = Mathf.Clamp01((1f - k) / 0.15f);
            if (k >= 1f) { splashT = -1f; splash.gameObject.SetActive(false); AfterSplash(); }
        }
        if (toastT > 0f) toastT -= dt;
        toast.alpha = Mathf.MoveTowards(toast.alpha, toastT > 0f ? 1f : 0f, dt * 6f);
    }
}
