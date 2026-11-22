using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game HUD: score, kill combo (with shatter on damage), health, waves (intro banner + breather
/// countdown), ammo + reload button with the active-reload bar, FPS, game-over stats and first-run tips.
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("Score / combo")]
    public TMP_Text scoreText;
    public TMP_Text multiplierText;
    public TMP_Text comboText;
    public RectTransform comboRoot;
    public RectTransform comboBar;
    public TMP_Text comboBrokenText;

    [Header("Health")]
    public RectTransform healthFill;
    public Image healthFillImage;
    [Tooltip("White bar that lags behind the fill after damage.")]
    public RectTransform healthLag;
    public TMP_Text healthText;
    public RectTransform healthHeart;
    public Image damageVignette;

    [Header("Waves")]
    public TMP_Text waveLabel;
    public TMP_Text waveBanner;
    public TMP_Text waveSubBanner;
    public CanvasGroup waveBannerGroup;
    public TMP_Text breakText;

    [Header("Weapon")]
    public TMP_Text ammoText;
    public GameObject reloadRoot;
    public RectTransform reloadBar;
    public Image reloadBarImage;
    public RectTransform reloadSweetSpot;
    public TMP_Text reloadLabel;
    public RectTransform reloadButton;
    public Image reloadButtonFill;
    public GameObject ammoPlate;

    [Header("Progression")]
    public RectTransform xpFill;
    public TMP_Text levelText;
    public TMP_Text levelNumber;
    public RectTransform levelBadge;
    public Image xpGlow;
    public RectTransform cardRow;
    [Tooltip("Badge template: children Icon (Image), LevelBg/Level (TMP_Text), Flash (Image).")]
    public Image cardIconTemplate;

    [Header("Misc")]
    public TMP_Text fpsText;
    public CanvasGroup tutorialGroup;
    [Tooltip("First-run guide: step text (steps 2–3 + done) and the left/right hints of step 1.")]
    public TMP_Text tutorialStep, tutorialCount;
    public GameObject tutorialStep1;

    [Header("Redesign HUD")]
    public TMP_Text killsText, runGoldText, waveNumber, waveLeft, weaponName;
    public RectTransform waveProgress;
    public ResultScreen result;

    [Header("Game over")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverTitle;
    public TMP_Text gameOverStats;
    public TMP_Text recordText;
    public RectTransform restartButton;
    [Tooltip("Victory screen: three star images (campaign chapter result).")]
    public Image[] victoryStars;

    const string TutorialKey = "tutorial_done";
    static readonly Color Gold = new Color(1f, 0.85f, 0.2f);

    int shownScore = -1, lastCombo = -1, lastMult = -1, lastFps = -1, lastAmmo = -1, lastWave = -1, lastBreak = -1;
    float ammoPunch, displayScore, healthShown = 1f, vignette, comboPunch, bannerT = 99f, brokenT = 99f, tutorialT, recordT;
    float fpsTimer;
    int fpsFrames;
    string[] ammoStrings;
    bool tutorialActive, recordActive;
    int lastReserve = -1;
    Vector2 brokenStart;
    int lastLevel = -1;
    float xpShown, levelPunch, healthLagShown = 1f, lagHold, heartPunch, lastHealth = -1f;
    int shownHp = -1;
    struct Badge { public Image frame, icon, flash; public TMP_Text level; public float flashT; }
    readonly System.Collections.Generic.List<Badge> cardIcons = new System.Collections.Generic.List<Badge>(16);

    int prevBestWave, shownKills = -1, shownGold = -1;
    float goldT;
    System.Action pendingResult;

    void Start()
    {
        prevBestWave = GameMode.IsHard ? SaveSystem.Data.records.hardWave : SaveSystem.Data.records.bestWave;
        if (weaponName && GameManager.I && GameManager.I.player)
        {
            var lo = GameManager.I.player.GetComponent<WeaponLoadout>();
            weaponName.text = lo ? Loc.T(Economy.Weapon(lo.Current).Key) : "";
        }
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (comboBrokenText) { comboBrokenText.gameObject.SetActive(false); brokenStart = comboBrokenText.rectTransform.anchoredPosition; }
        if (waveBannerGroup) waveBannerGroup.alpha = 0f;
        tutorialActive = tutorialGroup && !SaveSystem.Data.tutorialDone;
        if (tutorialGroup) tutorialGroup.alpha = tutorialActive ? 1f : 0f;
        if (WaveDirector.I) { WaveDirector.I.WaveStarted += OnWaveStarted; WaveDirector.I.WaveCleared += OnWaveClearedReward; }
        if (Upgrades.I) { Upgrades.I.Changed += OnCardChanged; Upgrades.I.SynergyUnlocked += OnSynergy; }
        if (cardIconTemplate) cardIconTemplate.gameObject.SetActive(false);
        if (reloadSweetSpot && GameManager.I && GameManager.I.player)
        {
            var w = GameManager.I.player.weapon;
            reloadSweetSpot.anchorMin = new Vector2(w.sweetSpotStart, 0f);
            reloadSweetSpot.anchorMax = new Vector2(w.sweetSpotEnd, 1f);
        }
    }

    void OnDestroy()
    {
        if (WaveDirector.I) { WaveDirector.I.WaveStarted -= OnWaveStarted; WaveDirector.I.WaveCleared -= OnWaveClearedReward; }
        if (Upgrades.I) { Upgrades.I.Changed -= OnCardChanged; Upgrades.I.SynergyUnlocked -= OnSynergy; }
        Explosions.OnExplosion -= OnTutorialExplosion;
    }

    static readonly string[] LevelDigits = { "0", "1", "2", "3", "4", "5" };

    /// <summary>Owned cards as framed rarity-tinted badges: icon, level number in the corner, flash on change.</summary>
    void OnCardChanged(CardId id, int level)
    {
        if (!cardIconTemplate) return;
        var owned = Upgrades.I.Owned;
        while (cardIcons.Count < owned.Count)
        {
            var img = Instantiate(cardIconTemplate, cardRow);
            img.gameObject.SetActive(true);
            var t = img.transform;
            cardIcons.Add(new Badge
            {
                frame = img,
                icon = t.Find("Icon").GetComponent<Image>(),
                flash = t.Find("Flash").GetComponent<Image>(),
                level = t.Find("LevelBg/Level").GetComponent<TMP_Text>(),
                flashT = 1f,
            });
        }
        for (int i = 0; i < owned.Count; i++)
        {
            var d = Upgrades.Get(owned[i]);
            var b = cardIcons[i];
            var rt = b.frame.rectTransform;
            rt.anchoredPosition = new Vector2(16f + (i % 15) * 36f, -16f - (i / 15) * 36f); // 22 cards (1.1.0): second row
            Color rc = Upgrades.RarityColors[(int)d.rarity];
            b.frame.color = Color.Lerp(new Color(0.17f, 0.2f, 0.11f), rc, 0.35f);
            b.icon.sprite = Upgrades.I.IconOf(owned[i]);
            b.icon.color = Color.Lerp(rc, Color.white, 0.4f);
            int l = Upgrades.I.Level(owned[i]);
            b.level.text = LevelDigits[Mathf.Clamp(l, 0, LevelDigits.Length - 1)];
            if (owned[i] == id) { rt.localScale = Vector3.one * 1.45f; b.flashT = 0f; }
            cardIcons[i] = b;
        }
    }

    void OnWaveStarted(int wave)
    {
        waveBanner.text = Loc.F("hud.wave", wave);
        bool special = WaveDirector.I && WaveDirector.I.Params.specialWave;
        waveSubBanner.text = Loc.T(special ? "wave.sub.special" : wave == 1 ? "wave.sub.first"
            : wave == Difficulty.FirstSpecialWave ? "wave.sub.first_special" : "wave.sub.more");
        waveSubBanner.color = special ? new Color(1f, 0.35f, 0.3f) : Gold;
        bannerT = 0f;
    }

    void OnSynergy(Synergy s)
    {
        ShowBanner(Loc.F("syn.title", Loc.T("syn." + s)), Loc.T("syn." + s + ".desc"), Upgrades.RarityColors[(int)Rarity.Epic]);
        GameAudio.Play(SfxId.ObjectiveDone, Vector3.zero);
    }

    // Wave-end reward moment (playtest-02): the gold this wave earned, coin sound, the run-gold pill pops.
    int goldAtWaveStart;
    float goldPop = 99f;

    int CurrentRunGold(GameManager gm)
    {
        int wave = WaveDirector.I ? WaveDirector.I.Wave : 0;
        int objectives = ObjectiveDirector.I ? ObjectiveDirector.I.CompletedCount : 0;
        int bosses = BossDirector.I ? BossDirector.I.BossesDefeated : 0;
        return Economy.RunGold(gm.Score, wave, gm.Kills, objectives, bosses, Profile.GoldMultiplier * ModeRules.GoldMul(GameMode.Current));
    }

    void OnWaveClearedReward(int wave)
    {
        var gm = GameManager.I;
        if (!gm || gm.IsGameOver) return;
        int g = CurrentRunGold(gm);
        int earned = Mathf.Max(0, g - goldAtWaveStart);
        goldAtWaveStart = g;
        goldT = 0f; goldPop = 0f;
        GameAudio.Play(SfxId.Coin, Vector3.zero);
        if (bannerT > 2.2f) ShowBanner(Loc.F("hud.wave_clear", wave), Loc.F("hud.wave_gold", earned), Gold);
    }

    /// <summary>Big centre banner (wave intro style) — objectives, story beats.</summary>
    // 1.1.0: "LİMAN BÖLGESİ" when the soldier settles in another district (not while another banner shows, ≥ 40 s apart).
    District shownDistrict, pendingDistrict;
    bool districtInit;
    float districtStableT, lastDistrictBanner = -99f;
    public const float DistrictSettle = 2.5f, DistrictGap = 40f;

    void UpdateDistrictBanner(float dt)
    {
        var a = DistrictAtmosphere.I;
        if (!a || Time.timeSinceLevelLoad < 1f) return;
        if (!districtInit) { shownDistrict = pendingDistrict = a.Current; districtInit = true; return; }
        if (a.Current == shownDistrict) { districtStableT = 0f; return; }
        if (a.Current != pendingDistrict) { pendingDistrict = a.Current; districtStableT = 0f; }
        districtStableT += dt;
        if (districtStableT < DistrictSettle || Time.unscaledTime - lastDistrictBanner < DistrictGap || bannerT < 3f) return;
        shownDistrict = a.Current;
        lastDistrictBanner = Time.unscaledTime;
        ShowBanner(Loc.T("district." + a.Current), Loc.T("district." + a.Current + ".sub"), UiKit.Hazard);
    }

    public void ShowBanner(string title, string sub, Color subColor)
    {
        if (!waveBanner) return;
        waveBanner.text = title;
        waveSubBanner.text = sub;
        waveSubBanner.color = subColor;
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
        UpdateProgression(dt);
        UpdateFps(dt);
        UpdateRun(gm, dt);
        if (recordActive)
        {
            recordT += dt;
            recordText.transform.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(recordT * 8f));
            recordText.transform.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(recordT * 5f));
            recordText.color = Color.Lerp(Gold, Color.white, 0.5f + 0.5f * Mathf.Sin(recordT * 12f));
        }
    }

    /// <summary>Kills + gold earned so far (same formula the run pays at the end).</summary>
    void UpdateRun(GameManager gm, float dt)
    {
        if (killsText && gm.Kills != shownKills) { shownKills = gm.Kills; killsText.text = gm.Kills.ToString(); }
        if (goldPop < 0.6f && runGoldText)
        {
            goldPop += dt;
            float k = goldPop / 0.6f;
            runGoldText.transform.parent.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(Mathf.Min(1f, k) * Mathf.PI));
            runGoldText.color = Color.Lerp(Gold, UiKit.Text, k);
        }
        goldT -= dt;
        if (!runGoldText || goldT > 0f) return;
        goldT = 0.5f;
        int g = CurrentRunGold(gm);
        if (g != shownGold) { shownGold = g; runGoldText.text = "+" + g; }
    }

    void UpdateScore(GameManager gm, float dt)
    {
        // Co-op: the team's score (everyone's own score added up).
        int target = GameMode.IsCoop && CoopTeam.I ? CoopTeam.I.TeamScore : gm.Score;
        displayScore = Mathf.MoveTowards(displayScore, target, Mathf.Max(60f, (target - displayScore) * 8f) * dt);
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
            if (combo > 0) comboText.text = Loc.F("hud.combo_kills", combo);
        }
        if (mult != lastMult)
        {
            if (mult > lastMult) comboPunch = 1.6f;
            lastMult = mult;
            multiplierText.text = "X" + mult;
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
        // Fill snaps quickly; the white "delayed" bar holds for a moment, then drains to the new value.
        if (p && lastHealth >= 0f && p.Health < lastHealth - 0.01f) { lagHold = 0.45f; heartPunch = 1f; }
        if (p) lastHealth = p.Health;
        healthShown = Mathf.MoveTowards(healthShown, h, dt * (h < healthShown ? 6f : 1.5f));
        if (lagHold > 0f) lagHold -= dt;
        else healthLagShown = Mathf.MoveTowards(healthLagShown, healthShown, dt * 0.8f);
        if (healthLagShown < healthShown) healthLagShown = healthShown;
        healthFill.anchorMax = new Vector2(healthShown, 1f);
        if (healthLag) healthLag.anchorMax = new Vector2(healthLagShown, 1f);
        if (healthText && p)
        {
            int hp = Mathf.CeilToInt(p.Health);
            if (hp != shownHp) { shownHp = hp; healthText.text = hp + " / " + Mathf.CeilToInt(p.maxHealth); }
        }
        heartPunch = Mathf.MoveTowards(heartPunch, 0f, dt * 4f);
        if (healthHeart)
        {
            float beat = h < 0.3f && !gm.IsGameOver ? 0.08f * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 9f)) : 0f;
            healthHeart.localScale = Vector3.one * (1f + 0.35f * heartPunch + beat);
        }
        var waves = WaveDirector.I;
        bool regen = waves && waves.InBreak && h < 1f && !gm.IsGameOver;
        Color baseCol = UiKit.Danger;
        healthFillImage.color = regen ? Color.Lerp(baseCol, Color.white, 0.35f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f)) : baseCol;
        vignette = Mathf.MoveTowards(vignette, 0f, dt * 1.6f);
        var vc = damageVignette.color; vc.a = vignette; damageVignette.color = vc;
    }

    void UpdateWaves(float dt)
    {
        var waves = WaveDirector.I;
        if (!waves) return;
        if (waves.Wave != lastWave && waves.Wave > 0)
        {
            lastWave = waves.Wave;
            if (waveLabel) waveLabel.text = Loc.F("hud.wave", waves.Wave);
            if (waveNumber) waveNumber.text = waves.Wave.ToString();
        }
        if (waveProgress)
        {
            float dur = waves.InBreak ? 9f : Mathf.Max(1f, waves.Params.duration);
            bool boss = BossDirector.I && BossDirector.I.HoldsWave;
            float k = waves.InBreak ? 1f - waves.PhaseTimeLeft / dur : boss ? 1f : 1f - waves.PhaseTimeLeft / dur;
            waveProgress.anchorMax = new Vector2(Mathf.Clamp01(k), 1f);
            int sec = Mathf.CeilToInt(Mathf.Max(0f, waves.PhaseTimeLeft));
            if (waveLeft && sec != lastLeft)
            {
                lastLeft = sec;
                waveLeft.text = boss && !waves.InBreak ? Loc.T("hud.boss_wave") : Loc.F(waves.InBreak ? "hud.breather_left" : "hud.wave_left", sec);
            }
        }

        // Intro banner: pop in, hold, fade.
        bannerT += dt;
        UpdateDistrictBanner(dt);
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
                breakText.text = Loc.F("hud.breather", waves.Wave + 1, sec);
            }
            breakText.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Max(0f, 1f - Mathf.Repeat(waves.PhaseTimeLeft, 1f) * 4f));
        }
    }

    int lastLeft = -1;

    void UpdateWeapon(GameManager gm, float dt)
    {
        var w = gm.player ? gm.player.weapon : null;
        if (!w) return;
        if (gm.IsGameOver)
        {
            // The weapon cluster would show through the game-over panel.
            if (reloadRoot.activeSelf) reloadRoot.SetActive(false);
            if (ammoText.gameObject.activeSelf)
            {
                ammoText.gameObject.SetActive(false);
                reloadButton.gameObject.SetActive(false);
                if (ammoPlate) ammoPlate.SetActive(false);
            }
            return;
        }
        if (w.limitedAmmo)
        {
            // Hard mode: magazine / reserve (string only rebuilt when a number changes).
            if (w.Ammo != lastAmmo || w.Reserve != lastReserve)
            {
                if (w.Ammo > lastAmmo || w.Reserve > lastReserve) ammoPunch = 1f;
                lastAmmo = w.Ammo; lastReserve = w.Reserve;
                ammoText.text = w.Ammo + "<size=55%><color=#B8BDA9>/" + w.Reserve + "</color></size>";
            }
        }
        else if (w.Ammo != lastAmmo)
        {
            if (w.Ammo > lastAmmo) ammoPunch = 1f;
            lastAmmo = w.Ammo;
            if (ammoStrings == null || ammoStrings.Length != w.magazineSize + 1)
            {
                ammoStrings = new string[w.magazineSize + 1];
                for (int i = 0; i <= w.magazineSize; i++) ammoStrings[i] = i + "<size=55%><color=#B8BDA9>/" + w.magazineSize + "</color></size>";
            }
            ammoText.text = ammoStrings[Mathf.Clamp(w.Ammo, 0, w.magazineSize)];
        }
        bool low = w.Ammo <= w.magazineSize / 5 || (w.ReserveLow && Mathf.Repeat(Time.unscaledTime * 2f, 1f) < 0.5f);
        ammoText.color = w.BonusShots > 0 ? Gold : low ? UiKit.DangerAlt : UiKit.Text;
        ammoPunch = Mathf.MoveTowards(ammoPunch, 0f, dt * 4f);
        ammoText.transform.localScale = Vector3.one * (1f + 0.25f * ammoPunch);

        reloadRoot.SetActive(w.IsReloading);
        if (w.IsReloading)
        {
            reloadBar.anchorMax = new Vector2(w.ReloadProgress, 1f);
            int r = w.ActiveReloadResult;
            reloadBarImage.color = r == 0 ? new Color(1f, 0.3f, 0.25f) : Color.white;
            reloadLabel.text = Loc.T(r == 0 ? "hud.jam" : "hud.reload_hint");
            reloadLabel.color = r == 0 ? new Color(1f, 0.35f, 0.3f) : Gold;
        }
        // Ammo ring (the panel is the reload button): magazine left, or reload progress while reloading.
        reloadButtonFill.fillAmount = w.IsReloading ? w.ReloadProgress : (float)w.Ammo / w.magazineSize;
        reloadButtonFill.color = w.IsReloading ? UiKit.Text : low ? UiKit.DangerAlt : UiKit.Hazard;
        float pulse = low && !w.IsReloading ? 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
        reloadButton.localScale = Vector3.one * pulse;
    }

    void UpdateProgression(float dt)
    {
        var prog = Progression.I;
        if (!prog || !xpFill) return;
        xpShown = Mathf.MoveTowards(xpShown, prog.Progress01, dt * 2.5f);
        if (prog.Level != lastLevel)
        {
            if (lastLevel > 0) { levelPunch = 1f; xpShown = 0f; }
            lastLevel = prog.Level;
            levelText.text = Loc.F("hud.level", prog.Level);
            if (levelNumber) levelNumber.text = prog.Level.ToString();
        }
        xpFill.anchorMax = new Vector2(xpShown, 1f);
        levelPunch = Mathf.MoveTowards(levelPunch, 0f, dt * 1.5f);
        levelText.transform.localScale = Vector3.one * (1f + 0.3f * levelPunch);
        if (levelBadge)
        {
            levelBadge.localScale = Vector3.one * (1f + 0.5f * levelPunch);
            levelBadge.localRotation = Quaternion.Euler(0f, 0f, 360f * levelPunch * levelPunch);
        }
        // XP bar glows on level-up (and softly when nearly full).
        if (xpGlow)
        {
            float near = Mathf.Clamp01((prog.Progress01 - 0.85f) / 0.15f) * (0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f));
            var gc = xpGlow.color; gc.a = Mathf.Max(levelPunch, near); xpGlow.color = gc;
        }
        for (int i = 0; i < cardIcons.Count; i++)
        {
            var b = cardIcons[i];
            var t = b.frame.rectTransform;
            t.localScale = Vector3.MoveTowards(t.localScale, Vector3.one, dt * 2.5f);
            if (b.flashT < 1f)
            {
                b.flashT += dt * 2f;
                b.flash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - b.flashT) * 0.9f);
                cardIcons[i] = b;
            }
        }
    }

    // First-run guide (playtest-02): 1 move + fire, 2 active reload, 3 shoot a barrel; each step also times out.
    int tutorialStepIx, tutorialReloads;
    float tutorialFire;
    bool tutorialWasReloading, tutorialBoom;
    static readonly string[] TutorialKeys = { null, "tut.step2", "tut.step3", "tut.done" };
    static readonly float[] TutorialTimeout = { 14f, 25f, 25f, 2f };

    void OnTutorialExplosion(Vector3 p, float r) => tutorialBoom = true;

    void ShowTutorialStep(int ix)
    {
        tutorialStepIx = ix; tutorialT = 0f;
        if (tutorialStep1) tutorialStep1.SetActive(ix == 0);
        if (tutorialStep) { tutorialStep.gameObject.SetActive(ix > 0); if (ix > 0) tutorialStep.text = Loc.T(TutorialKeys[ix]); }
        if (tutorialCount) { tutorialCount.gameObject.SetActive(ix < 3); tutorialCount.text = Loc.F("tut.count", ix + 1); }
        tutorialGroup.alpha = 1f;
        if (ix == 2) { tutorialBoom = false; Explosions.OnExplosion -= OnTutorialExplosion; Explosions.OnExplosion += OnTutorialExplosion; }
        if (ix > 0) GameAudio.Play(SfxId.UiClick, Vector3.zero);
    }

    void UpdateTutorial(float dt)
    {
        if (!tutorialActive) return;
        if (tutorialT == 0f && tutorialStepIx == 0 && tutorialFire == 0f) ShowTutorialStep(0);
        tutorialT += dt;
        var w = GameManager.I && GameManager.I.player ? GameManager.I.player.weapon : null;
        bool done = tutorialT > TutorialTimeout[tutorialStepIx];
        switch (tutorialStepIx)
        {
            case 0:
                if (w && w.IsFiring) tutorialFire += dt;
                done |= tutorialFire > 1.5f;
                break;
            case 1:
                if (w && w.IsReloading && !tutorialWasReloading) tutorialReloads++;
                if (w) tutorialWasReloading = w.IsReloading;
                done |= (w && w.ActiveReloadResult == 1) || (tutorialReloads >= 3 && w && !w.IsReloading);
                break;
            case 2:
                done |= tutorialBoom;
                break;
        }
        if (!done) return;
        if (tutorialStepIx < 3) { ShowTutorialStep(tutorialStepIx + 1); return; }
        tutorialGroup.alpha = Mathf.MoveTowards(tutorialGroup.alpha, 0f, dt * 2f);
        if (tutorialGroup.alpha > 0f) return;
        tutorialActive = false;
        Explosions.OnExplosion -= OnTutorialExplosion;
        SaveSystem.Data.tutorialDone = true;
        MetaEvents.TutorialCompleted();
        SaveSystem.Write();
    }

    void UpdateFps(float dt)
    {
        // Developer readout: hidden in store builds.
        if (!(Application.isEditor || Debug.isDebugBuild)) { if (fpsText && fpsText.gameObject.activeSelf) fpsText.gameObject.SetActive(false); return; }
        fpsFrames++;
        fpsTimer += dt;
        if (fpsTimer < 0.5f) return;
        int fps = Mathf.RoundToInt(fpsFrames / fpsTimer);
        fpsFrames = 0; fpsTimer = 0f;
        if (fps != lastFps) { lastFps = fps; fpsText.text = fps + " FPS"; }
    }

    public void FlashDamage() => vignette = 0.75f;

    static string Time2(float t) => $"{(int)(t / 60f)}:{(int)(t % 60f):00}";

    public void HideGameOver() { gameOverPanel.SetActive(false); pendingResult = null; }

    /// <summary>The revive offer is closed (declined / timed out): show the result screen that was waiting.</summary>
    public void RevealGameOver()
    {
        var a = pendingResult;
        pendingResult = null;
        a?.Invoke();
    }

    bool Defer(System.Action show)
    {
        if (RunRewards.I && RunRewards.I.ReviveOpen) { pendingResult = show; return true; }
        return false;
    }

    void SetRestartLabel(string key)
    {
        var lt = restartButton ? restartButton.GetComponentInChildren<LocText>(true) : null;
        if (lt) { lt.key = key; lt.Apply(); }
    }

    void ShowStars(int n)
    {
        if (victoryStars == null) return;
        for (int i = 0; i < victoryStars.Length; i++)
        {
            if (!victoryStars[i]) continue;
            victoryStars[i].gameObject.SetActive(n >= 0);
            victoryStars[i].color = i < n ? Gold : new Color(0f, 0f, 0f, 0.45f);
        }
    }

    /// <summary>Campaign chapter cleared: title, stars, time vs par, reward; the big button goes to the next chapter.</summary>
    public void ShowVictory(int chapter, int stars, int score, float time, int kills, int wave, int gold, int gems)
    {
        gameOverPanel.SetActive(true);
        if (result)
        {
            int total = ObjectiveDirector.I ? ObjectiveDirector.I.ChapterTotal : 0;
            result.ShowStory(restartButton, chapter, true, stars, time, total, total, gold);
        }
        breakText.gameObject.SetActive(false);
        var def = Campaign.Get(chapter);
        gameOverTitle.text = Loc.T("victory.title");
        gameOverStats.text = Loc.F("victory.stats", Loc.T("chapter." + def.key + ".title"), score, Time2(time), Time2(def.parSeconds), kills, gold, gems)
            + "\n<size=28>" + Loc.F("victory.stars_hint", Time2(def.parSeconds), Mathf.RoundToInt(Campaign.ThreeStarHealth * 100f)) + "</size>";
        recordActive = false;
        recordText.gameObject.SetActive(false);
        ShowStars(stars);
        SetRestartLabel(chapter + 1 < Campaign.Count ? "victory.next" : "over.again");
    }

    public void ShowGameOver(int score, float time, int kills, int bestCombo, int wave, GameManager.RunRecords rec)
    {
        if (Defer(() => ShowGameOver(score, time, kills, bestCombo, wave, rec))) return;
        gameOverPanel.SetActive(true);
        if (result)
        {
            if (GameMode.IsCampaign && ObjectiveDirector.I)
                result.ShowStory(restartButton, GameMode.Chapter, false, 0, time, ObjectiveDirector.I.ChapterIndex, ObjectiveDirector.I.ChapterTotal, RunRewards.I ? RunRewards.I.TotalGold : 0);
            else
                result.ShowDeath(restartButton, wave, kills, BossDirector.I ? BossDirector.I.BossesDefeated : 0, bestCombo, time, rec, prevBestWave);
        }
        ShowStars(-1);
        SetRestartLabel(GameMode.IsCampaign ? "over.retry" : GameMode.IsCoop ? "over.menu" : "over.again");
        breakText.gameObject.SetActive(false);
        gameOverTitle.text = Loc.T("over.title");
        gameOverStats.text = Loc.F("over.stats", score, rec.bestScore, Time2(time), wave, kills, bestCombo);
        recordActive = rec.newScore;
        recordText.gameObject.SetActive(rec.newScore);
        recordText.text = Loc.T("over.record");
    }
}
