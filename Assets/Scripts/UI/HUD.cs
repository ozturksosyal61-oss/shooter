using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game HUD: score, kill combo (with shatter on damage), health, waves (intro banner + breather
/// countdown), ammo + reload button with the active-reload bar, FPS, game-over stats and first-run tips.
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("Score / combo")]
    public Text scoreText;
    public Text multiplierText;
    public Text comboText;
    public RectTransform comboRoot;
    public RectTransform comboBar;
    public Text comboBrokenText;

    [Header("Health")]
    public RectTransform healthFill;
    public Image healthFillImage;
    public Image damageVignette;

    [Header("Waves")]
    public Text waveLabel;
    public Text waveBanner;
    public Text waveSubBanner;
    public CanvasGroup waveBannerGroup;
    public Text breakText;

    [Header("Weapon")]
    public Text ammoText;
    public GameObject reloadRoot;
    public RectTransform reloadBar;
    public Image reloadBarImage;
    public RectTransform reloadSweetSpot;
    public Text reloadLabel;
    public RectTransform reloadButton;
    public Image reloadButtonFill;

    [Header("Misc")]
    public Text fpsText;
    public CanvasGroup tutorialGroup;

    [Header("Game over")]
    public GameObject gameOverPanel;
    public Text gameOverTitle;
    public Text gameOverStats;
    public Text recordText;
    public RectTransform restartButton;

    const string TutorialKey = "tutorial_done";
    static readonly Color Gold = new Color(1f, 0.85f, 0.2f);

    int shownScore = -1, lastCombo = -1, lastMult = -1, lastFps = -1, lastAmmo = -1, lastWave = -1, lastBreak = -1;
    float ammoPunch, displayScore, healthShown = 1f, vignette, comboPunch, bannerT = 99f, brokenT = 99f, tutorialT, recordT;
    float fpsTimer;
    int fpsFrames;
    string[] ammoStrings;
    bool tutorialActive, recordActive;
    Vector2 brokenStart;

    void Start()
    {
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (comboBrokenText) { comboBrokenText.gameObject.SetActive(false); brokenStart = comboBrokenText.rectTransform.anchoredPosition; }
        if (waveBannerGroup) waveBannerGroup.alpha = 0f;
        tutorialActive = tutorialGroup && PlayerPrefs.GetInt(TutorialKey, 0) == 0;
        if (tutorialGroup) tutorialGroup.alpha = tutorialActive ? 1f : 0f;
        if (WaveDirector.I) WaveDirector.I.WaveStarted += OnWaveStarted;
        if (reloadSweetSpot && GameManager.I && GameManager.I.player)
        {
            var w = GameManager.I.player.weapon;
            reloadSweetSpot.anchorMin = new Vector2(w.sweetSpotStart, 0f);
            reloadSweetSpot.anchorMax = new Vector2(w.sweetSpotEnd, 1f);
        }
    }

    void OnDestroy()
    {
        if (WaveDirector.I) WaveDirector.I.WaveStarted -= OnWaveStarted;
    }

    void OnWaveStarted(int wave)
    {
        waveBanner.text = "DALGA " + wave;
        waveSubBanner.text = wave == 1 ? "Devriye başladı" : "Daha fazlası geliyor!";
        bannerT = 0f;
    }

    public void ComboBroken(int lostMultiplier)
    {
        if (!comboBrokenText) return;
        comboBrokenText.text = "x" + lostMultiplier;
        comboBrokenText.gameObject.SetActive(true);
        brokenT = 0f;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (!gm) return;
        float dt = Time.unscaledDeltaTime;

        UpdateScore(gm, dt);
        UpdateCombo(gm, dt);
        UpdateHealth(gm, dt);
        UpdateWaves(dt);
        UpdateWeapon(gm, dt);
        UpdateTutorial(dt);
        UpdateFps(dt);
        if (recordActive)
        {
            recordT += dt;
            recordText.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(recordT * 8f));
            recordText.transform.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(recordT * 5f));
            recordText.color = Color.Lerp(Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(recordT * 12f));
        }
    }

    void UpdateScore(GameManager gm, float dt)
    {
        displayScore = Mathf.MoveTowards(displayScore, gm.Score, Mathf.Max(60f, (gm.Score - displayScore) * 8f) * dt);
        int s = Mathf.RoundToInt(displayScore);
        if (s != shownScore) { shownScore = s; scoreText.text = s.ToString(); }
    }

    void UpdateCombo(GameManager gm, float dt)
    {
        int combo = gm.Combo, mult = gm.Multiplier;
        if (combo != lastCombo)
        {
            if (combo > lastCombo) comboPunch = Mathf.Max(comboPunch, 0.6f);
            lastCombo = combo;
            comboRoot.gameObject.SetActive(combo > 0);
            if (combo > 0) comboText.text = combo + " KİLL";
        }
        if (mult != lastMult)
        {
            if (mult > lastMult) comboPunch = 1.6f;
            lastMult = mult;
            multiplierText.text = "x" + mult;
        }
        if (combo > 0)
        {
            comboPunch = Mathf.MoveTowards(comboPunch, 0f, dt * 6f);
            comboRoot.localScale = Vector3.one * (1f + 0.25f * comboPunch);
            comboBar.anchorMax = new Vector2(Mathf.Clamp01(gm.ComboTimeLeft / GameManager.ComboWindow), 1f);
        }

        // Shattered multiplier: shakes, then drops and fades.
        if (comboBrokenText && comboBrokenText.gameObject.activeSelf)
        {
            brokenT += dt;
            var rt = comboBrokenText.rectTransform;
            float shake = brokenT < 0.25f ? Mathf.Sin(brokenT * 90f) * 14f * (1f - brokenT / 0.25f) : 0f;
            float fall = brokenT > 0.25f ? (brokenT - 0.25f) * (brokenT - 0.25f) * 900f : 0f;
            rt.anchoredPosition = brokenStart + new Vector2(shake, -fall);
            rt.localRotation = Quaternion.Euler(0f, 0f, brokenT > 0.25f ? -(brokenT - 0.25f) * 70f : 0f);
            var c = comboBrokenText.color; c.a = Mathf.Clamp01(1.1f - brokenT); comboBrokenText.color = c;
            if (brokenT > 1.1f) comboBrokenText.gameObject.SetActive(false);
        }
    }

    void UpdateHealth(GameManager gm, float dt)
    {
        var p = gm.player;
        float h = p ? p.Health / p.maxHealth : 0f;
        healthShown = Mathf.MoveTowards(healthShown, h, dt * 2f);
        healthFill.anchorMax = new Vector2(healthShown, 1f);
        var waves = WaveDirector.I;
        bool regen = waves && waves.InBreak && h < 1f && !gm.IsGameOver;
        Color baseCol = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.9f, 0.3f), healthShown);
        healthFillImage.color = regen ? Color.Lerp(baseCol, Color.white, 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f)) : baseCol;
        vignette = Mathf.MoveTowards(vignette, 0f, dt * 1.6f);
        var vc = damageVignette.color; vc.a = vignette; damageVignette.color = vc;
    }

    void UpdateWaves(float dt)
    {
        var waves = WaveDirector.I;
        if (!waves) return;
        if (waves.Wave != lastWave && waves.Wave > 0) { lastWave = waves.Wave; waveLabel.text = "DALGA " + waves.Wave; }

        // Intro banner: pop in, hold, fade.
        bannerT += dt;
        if (waveBannerGroup)
        {
            float a = bannerT < 0.2f ? bannerT / 0.2f : bannerT < 1.8f ? 1f : Mathf.Clamp01(1f - (bannerT - 1.8f) / 0.5f);
            waveBannerGroup.alpha = a;
            float pop = bannerT < 0.25f ? Mathf.Lerp(1.8f, 1f, bannerT / 0.25f) : 1f;
            waveBannerGroup.transform.localScale = Vector3.one * pop;
        }

        // Breather countdown.
        bool show = waves.InBreak && waves.Wave > 0 && GameManager.I && !GameManager.I.IsGameOver;
        breakText.gameObject.SetActive(show);
        if (show)
        {
            int sec = Mathf.CeilToInt(waves.PhaseTimeLeft);
            if (sec != lastBreak)
            {
                lastBreak = sec;
                breakText.text = "DALGA " + (waves.Wave + 1) + " · " + sec + "\n<size=26>Nefes al — can yenileniyor</size>";
            }
            breakText.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Max(0f, 1f - Mathf.Repeat(waves.PhaseTimeLeft, 1f) * 4f));
        }
    }

    void UpdateWeapon(GameManager gm, float dt)
    {
        var w = gm.player ? gm.player.weapon : null;
        if (!w) return;
        if (gm.IsGameOver)
        {
            // The weapon cluster would show through the game-over panel.
            if (reloadRoot.activeSelf) reloadRoot.SetActive(false);
            if (ammoText.gameObject.activeSelf) { ammoText.gameObject.SetActive(false); reloadButton.gameObject.SetActive(false); }
            return;
        }
        if (w.Ammo != lastAmmo)
        {
            if (w.Ammo > lastAmmo) ammoPunch = 1f;
            lastAmmo = w.Ammo;
            if (ammoStrings == null || ammoStrings.Length != w.magazineSize + 1)
            {
                ammoStrings = new string[w.magazineSize + 1];
                for (int i = 0; i <= w.magazineSize; i++) ammoStrings[i] = i + "<size=28> / " + w.magazineSize + "</size>";
            }
            ammoText.text = ammoStrings[Mathf.Clamp(w.Ammo, 0, w.magazineSize)];
        }
        bool low = w.Ammo <= w.magazineSize / 5;
        ammoText.color = w.BonusShots > 0 ? Gold : low ? new Color(1f, 0.35f, 0.3f) : Color.white;
        ammoPunch = Mathf.MoveTowards(ammoPunch, 0f, dt * 4f);
        ammoText.transform.localScale = Vector3.one * (1f + 0.25f * ammoPunch);

        reloadRoot.SetActive(w.IsReloading);
        if (w.IsReloading)
        {
            reloadBar.anchorMax = new Vector2(w.ReloadProgress, 1f);
            int r = w.ActiveReloadResult;
            reloadBarImage.color = r == 0 ? new Color(1f, 0.3f, 0.25f) : Color.white;
            reloadLabel.text = r == 0 ? "TUTUKLUK!" : "ŞARJÖR · doğru anda tekrar bas";
            reloadLabel.color = r == 0 ? new Color(1f, 0.35f, 0.3f) : Gold;
        }
        // Reload button: fills as the magazine empties, pulses when low.
        reloadButtonFill.fillAmount = 1f - (float)w.Ammo / w.magazineSize;
        float pulse = low && !w.IsReloading ? 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
        reloadButton.localScale = Vector3.one * pulse;
    }

    void UpdateTutorial(float dt)
    {
        if (!tutorialActive) return;
        tutorialT += dt;
        if (tutorialT > 5f)
        {
            tutorialGroup.alpha = Mathf.MoveTowards(tutorialGroup.alpha, 0f, dt * 2f);
            if (tutorialGroup.alpha <= 0f)
            {
                tutorialActive = false;
                PlayerPrefs.SetInt(TutorialKey, 1);
                PlayerPrefs.Save();
            }
        }
    }

    void UpdateFps(float dt)
    {
        fpsFrames++;
        fpsTimer += dt;
        if (fpsTimer < 0.5f) return;
        int fps = Mathf.RoundToInt(fpsFrames / fpsTimer);
        fpsFrames = 0; fpsTimer = 0f;
        if (fps != lastFps) { lastFps = fps; fpsText.text = fps + " FPS"; }
    }

    public void FlashDamage() => vignette = 0.75f;

    static string Time2(float t) => $"{(int)(t / 60f)}:{(int)(t % 60f):00}";

    public void ShowGameOver(int score, float time, int kills, int bestCombo, int wave, GameManager.RunRecords rec)
    {
        gameOverPanel.SetActive(true);
        breakText.gameObject.SetActive(false);
        gameOverTitle.text = "ÖLDÜN";
        gameOverStats.text =
            $"SKOR  <b>{score}</b>   <size=30>(en iyi {rec.bestScore})</size>\n" +
            $"<size=40>Süre {Time2(time)}   ·   Dalga {wave}   ·   {kills} kill   ·   En iyi kombo {bestCombo}</size>";
        recordActive = rec.newScore;
        recordText.gameObject.SetActive(rec.newScore);
        recordText.text = "YENİ REKOR!";
    }
}
