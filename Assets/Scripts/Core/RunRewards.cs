using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// End of an endless run: gold from score / wave / kills / objectives / bosses (× VIP / gold boost) goes to the wallet,
/// daily quests get the run's numbers. The game-over panel counts the gold up and offers: 2× gold (rewarded ad),
/// one continue per run (rewarded ad or gems). After a continue only the difference is paid at the next game over.
/// </summary>
public class RunRewards : MonoBehaviour
{
    public static RunRewards I { get; private set; }

    public GameObject root;
    public Text goldText;
    public Button doubleButton, continueAdButton, continueGemButton;
    public Text doubleLabel, continueAdLabel, continueGemLabel, message;

    public int LastGold { get; private set; }
    public int TotalGold { get; private set; }
    public bool Doubled { get; private set; }

    int objectives, bosses, paidBase;
    int repKills, repSpecials, repBosses, repObjectives;
    bool reportedOnce, busy;
    float shown, tickT;

    void Awake() => I = this;

    void Start()
    {
        var gm = GameManager.I;
        gm.GameOverStarted += OnGameOver;
        gm.Revived += OnRevived;
        if (ObjectiveDirector.I) ObjectiveDirector.I.Completed += _ => objectives++;
        if (BossDirector.I) BossDirector.I.BossDefeated += _ => bosses++;
        if (doubleButton) doubleButton.onClick.AddListener(DoubleGold);
        if (continueAdButton) continueAdButton.onClick.AddListener(ContinueWithAd);
        if (continueGemButton) continueGemButton.onClick.AddListener(ContinueWithGems);
        if (root) root.SetActive(false);
    }

    void OnDestroy()
    {
        var gm = GameManager.I;
        if (gm) { gm.GameOverStarted -= OnGameOver; gm.Revived -= OnRevived; }
    }

    void OnGameOver()
    {
        var gm = GameManager.I;
        int wave = WaveDirector.I ? WaveDirector.I.Wave : 1;
        int earned = Economy.RunGold(gm.Score, wave, gm.Kills, objectives, bosses, Profile.GoldMultiplier * ModeRules.GoldMul(GameMode.Current));
        int delta = Mathf.Max(0, earned - paidBase);
        paidBase = earned;
        LastGold = delta;
        TotalGold += delta;
        Profile.Grant(delta, 0, "run");
        Profile.ReportRun(gm.Kills - repKills, gm.SpecialKills - repSpecials, bosses - repBosses, wave, objectives - repObjectives, gm.BestCombo, !reportedOnce);
        reportedOnce = true;
        repKills = gm.Kills; repSpecials = gm.SpecialKills; repBosses = bosses; repObjectives = objectives;
        shown = 0f;
        Refresh();
        if (root) root.SetActive(true);
    }

    void OnRevived()
    {
        if (root) root.SetActive(false);
    }

    void Refresh()
    {
        var gm = GameManager.I;
        bool vip = Profile.Vip;
        bool canContinue = gm.Continues == 0 && !gm.Won;
        if (doubleButton) doubleButton.gameObject.SetActive(!Doubled && LastGold > 0 && Ads.Available);
        if (continueAdButton) continueAdButton.gameObject.SetActive(canContinue && Ads.Available);
        if (continueGemButton) continueGemButton.gameObject.SetActive(canContinue);
        if (doubleLabel) doubleLabel.text = Loc.T(vip ? "rew.double_vip" : "rew.double");
        if (continueAdLabel) continueAdLabel.text = Loc.T(vip ? "rew.continue_vip" : "rew.continue_ad");
        if (continueGemLabel) continueGemLabel.text = Loc.F("rew.continue_gems", Economy.ContinueGems);
        if (message) message.text = "";
    }

    public void DoubleGold()
    {
        if (Doubled || busy || LastGold <= 0) return;
        busy = true;
        Ads.ShowRewarded(AdPlacement.DoubleGold, ok =>
        {
            busy = false;
            if (!ok || Doubled) return;
            Doubled = true;
            TotalGold += LastGold;
            Profile.Grant(LastGold, 0, "run-double");
            GameAudio.Play(SfxId.Purchase, Vector3.zero);
            Refresh();
        });
    }

    public void ContinueWithAd()
    {
        if (busy || GameManager.I.Continues > 0) return;
        busy = true;
        Ads.ShowRewarded(AdPlacement.Continue, ok => { busy = false; if (ok) GameManager.I.Revive(); });
    }

    public void ContinueWithGems()
    {
        if (GameManager.I.Continues > 0) return;
        if (Profile.TrySpend(0, Economy.ContinueGems)) GameManager.I.Revive();
        else if (message) message.text = Loc.T("shop.no_gems");
    }

    void Update()
    {
        if (!root || !root.activeSelf || !goldText) return;
        float target = TotalGold;
        if (shown < target)
        {
            float before = shown;
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * Mathf.Max(60f, target * 0.9f));
            tickT -= Time.unscaledDeltaTime;
            if (tickT <= 0f && (int)shown != (int)before) { tickT = 0.07f; GameAudio.Play(SfxId.Coin, Vector3.zero); }
        }
        goldText.text = Loc.F("rew.gold", Mathf.RoundToInt(shown));
    }
}
