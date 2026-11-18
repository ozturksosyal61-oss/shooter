using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu: HOLDOUT logo over a live city with a slowly orbiting camera; Play (Story chapters / Endless / Hard),
/// Shop, Settings; gold / gem counters from the save. The OZT Digital splash plays once
/// per app session (after the Unity splash).
/// </summary>
public class MainMenu : MonoBehaviour
{
    public static MainMenu I { get; private set; }

    public GameObject mainButtons, playButtons;
    public Button play, shop, settings, campaign, endless, hard, back;
    public Text storySub, hardSub;
    public CampaignPanel campaignPanel;
    public Text gold, gems;
    public RectTransform logo;
    public CanvasGroup splash;
    public RectTransform splashLogo;
    public float splashSeconds = 2.4f;
    public CanvasGroup toast;
    public Text toastText;
    public SettingsPanel settingsPanel;
    public ShopUI shopUI;
    public LoadoutPanel loadout;
    public DailyLoginPanel login;
    public QuestsPanel quests;
    public Button questsButton, loginButton;
    public GameObject questBadge, loginBadge;

    static bool splashShown;
    float splashT = -1f, toastT, t;

    public bool SplashActive => splashT >= 0f;

    void Awake() => I = this;

    void Start()
    {
        play.onClick.AddListener(() => { Click(); ShowPlay(true); });
        back.onClick.AddListener(() => { Click(); ShowPlay(false); });
        endless.onClick.AddListener(() => { Click(); StartMode(RunMode.Endless); });
        if (hard) hard.onClick.AddListener(() => { Click(); StartMode(RunMode.Hard); });
        campaign.onClick.AddListener(() => { Click(); if (campaignPanel) campaignPanel.Open(); else Toast(Loc.T("menu.soon")); });
        Loc.Changed += RefreshModes;
        shop.onClick.AddListener(() => { Click(); if (shopUI) shopUI.Open(ShopUI.Tab.Weapons); else Toast(Loc.T("menu.soon")); });
        if (questsButton) questsButton.onClick.AddListener(() => { Click(); quests.Open(); });
        if (loginButton) loginButton.onClick.AddListener(() => { Click(); login.Open(); });
        Profile.Changed += RefreshWallet;
        settings.onClick.AddListener(() => { Click(); settingsPanel.Open(); });
        ShowPlay(false);
        toast.alpha = 0f;
        SaveSystem.Changed += RefreshWallet;
        RefreshWallet();
        if (!splashShown) { splashShown = true; splashT = 0f; splash.gameObject.SetActive(true); splash.alpha = 1f; }
        else { splash.gameObject.SetActive(false); AutoLogin(); }
    }

    void OnDestroy() { SaveSystem.Changed -= RefreshWallet; Profile.Changed -= RefreshWallet; Loc.Changed -= RefreshModes; }

    void StartMode(RunMode m)
    {
        GameMode.Set(m);
        if (loadout) loadout.Open(); else PlayEndless();
    }

    void RefreshModes()
    {
        var d = SaveSystem.Data;
        if (storySub) storySub.text = Loc.F("menu.story_sub", Campaign.TotalStars(d.campaign), Campaign.Count * 3);
        if (hardSub) hardSub.text = d.records.hardWave > 0 ? Loc.F("menu.hard_best", d.records.hardWave) : Loc.T("menu.hard_sub");
    }

    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    void RefreshWallet()
    {
        var w = SaveSystem.Data.wallet;
        gold.text = w.gold.ToString();
        gems.text = w.gems.ToString();
        if (questBadge) questBadge.SetActive(QuestsPanel.AnyClaimable());
        if (loginBadge) loginBadge.SetActive(Profile.LoginClaimable >= 0);
    }

    static bool loginShown;

    /// <summary>Once per session: the daily reward pops up if it is waiting.</summary>
    void AutoLogin()
    {
        if (loginShown || !login || Profile.LoginClaimable < 0) return;
        loginShown = true;
        login.Open();
    }

    public void ShowPlay(bool on)
    {
        mainButtons.SetActive(!on);
        playButtons.SetActive(on);
        if (on) RefreshModes();
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
        t += dt;
        logo.localScale = Vector3.one * (1f + 0.015f * Mathf.Sin(t * 1.6f));
        logo.localRotation = Quaternion.Euler(0f, 0f, 1.2f * Mathf.Sin(t * 0.8f));

        if (splashT >= 0f)
        {
            splashT += dt;
            float k = splashT / splashSeconds;
            splashLogo.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.04f, k);
            var g = splashLogo.GetComponent<CanvasGroup>();
            if (g) g.alpha = k < 0.25f ? k / 0.25f : k > 0.75f ? Mathf.Clamp01((1f - k) / 0.25f) : 1f;
            if (k > 0.85f) splash.alpha = Mathf.Clamp01((1f - k) / 0.15f);
            if (k >= 1f) { splashT = -1f; splash.gameObject.SetActive(false); AutoLogin(); }
        }

        if (toastT > 0f) toastT -= dt;
        toast.alpha = Mathf.MoveTowards(toast.alpha, toastT > 0f ? 1f : 0f, dt * 6f);
    }
}
