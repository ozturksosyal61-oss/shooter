using TMPro;
using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings screen (menu and pause): music / effects volume, vibration, frame rate (only rates the display supports),
/// graphics quality, joystick size + opacity, language (switches at once), support mail, privacy link,
/// reset progress (with confirmation). Writes <see cref="SettingsData"/>; <see cref="Settings"/> applies it.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    public const string SupportMail = "oztdigitaldestek@gmail.com";
    public const string SupportUrl = "https://www.oztdigital.com.tr/holdout/support";
    public const string PrivacyUrl = "https://www.oztdigital.com.tr/holdout/privacy";
    public const string TermsUrl = "https://www.oztdigital.com.tr/holdout/terms";

    public GameObject root;
    public Slider music, sfx, joySize, joyAlpha;
    public Button vibration;
    public TMP_Text vibrationLabel;
    public Button[] fpsButtons;
    public Button[] qualityButtons;
    public Button[] langButtons;
    public Button support, privacy, terms, reset, close, restore;
    [Tooltip("1.1.0 cloud save: a quiet status line; the button only shows when the save is NOT backed up (connect / retry).")]
    public Button cloud;
    public TMP_Text cloudText;
    public Image cloudIcon;
    public Sprite cloudOkIcon, cloudWarnIcon;
    public GameObject cloudRow;
    public GameObject confirm;
    public Button confirmYes, confirmNo;
    public TMP_Text resetDone;
    [Header("Redesign")]
    public TMP_Text musicValue, sfxValue, joySizeValue, joyAlphaValue, version;
    public RectTransform vibrationKnob;
    public Image vibrationTrack;

    public bool IsOpen => root && root.activeSelf;
    public event Action Closed;

    bool dirty;
    float resetDoneT;

    void Awake()
    {
        music.onValueChanged.AddListener(v => Change(s => s.music = v));
        sfx.onValueChanged.AddListener(v => Change(s => s.sfx = v));
        joySize.minValue = Settings.MinJoystickSize; joySize.maxValue = Settings.MaxJoystickSize;
        joyAlpha.minValue = Settings.MinJoystickAlpha; joyAlpha.maxValue = Settings.MaxJoystickAlpha;
        joySize.onValueChanged.AddListener(v => Change(s => s.joystickSize = v));
        joyAlpha.onValueChanged.AddListener(v => Change(s => s.joystickAlpha = v));
        vibration.onClick.AddListener(() => { Click(); Change(s => s.vibration = !s.vibration); if (Settings.S.vibration) Haptics.Play(Haptics.Kind.Medium); });
        for (int i = 0; i < fpsButtons.Length; i++) { int f = Settings.FpsOptions[i]; fpsButtons[i].onClick.AddListener(() => { Click(); Change(s => s.fps = f); }); }
        for (int i = 0; i < qualityButtons.Length; i++) { int q = i; qualityButtons[i].onClick.AddListener(() => { Click(); Change(s => s.quality = q); }); }
        for (int i = 0; i < langButtons.Length; i++) { string l = i == 0 ? "tr" : "en"; langButtons[i].onClick.AddListener(() => { Click(); Change(s => s.lang = l); }); }
        support.onClick.AddListener(() => { Click(); Application.OpenURL(SupportUrl); });
        terms.onClick.AddListener(() => { Click(); Application.OpenURL(TermsUrl); });
        privacy.onClick.AddListener(() => { Click(); Application.OpenURL(PrivacyUrl); });
        reset.onClick.AddListener(() => { Click(); confirm.SetActive(true); });
        if (restore) restore.onClick.AddListener(() =>
        {
            Click();
            Store.Restore(ok => Notify(Loc.T(ok ? "set.restored" : "set.restore_failed")));
        });
        if (cloud) cloud.onClick.AddListener(() =>
        {
            Click();
            if (!GameServices.Signed) { GameServices.Retry(); Notify(Loc.T("set.cloud.signin")); }
            else CloudSync.Begin();
        });
        CloudSync.Changed += RefreshCloud;
        confirmNo.onClick.AddListener(() => { Click(); confirm.SetActive(false); });
        confirmYes.onClick.AddListener(() => { Click(); ResetProgress(); });
        close.onClick.AddListener(() => { Click(); Close(); });
        root.SetActive(false);
        confirm.SetActive(false);
        if (resetDone) resetDone.gameObject.SetActive(false);
        Loc.Changed += Refresh;
    }

    void OnDestroy()
    {
        Loc.Changed -= Refresh;
        CloudSync.Changed -= RefreshCloud;
        if (dirty) SaveSystem.Write();
    }

    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Open()
    {
        confirm.SetActive(false);
        root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        if (dirty) { SaveSystem.Write(); dirty = false; }
        confirm.SetActive(false);
        root.SetActive(false);
        Closed?.Invoke();
    }

    /// <summary>Changes one setting and applies it right away (saved on close).</summary>
    public void Change(Action<SettingsData> c)
    {
        c(Settings.S);
        Settings.Apply();
        dirty = true;
        Refresh();
    }

    void Notify(string text)
    {
        if (!resetDone) return;
        resetDone.text = text;
        resetDone.gameObject.SetActive(true);
        resetDoneT = 2.5f;
    }

    public void ResetProgress()
    {
        SaveSystem.ResetProgress();
        confirm.SetActive(false);
        Notify(Loc.T("set.reset_done"));
    }

    void Update()
    {
        if (resetDoneT > 0f)
        {
            resetDoneT -= Time.unscaledDeltaTime;
            if (resetDoneT <= 0f && resetDone) resetDone.gameObject.SetActive(false);
        }
    }

    void RefreshCloud()
    {
        if (!root || !root.activeSelf) return;
        // Game Center is iOS only: Android builds don't show the row at all (until Play Games sign-in exists).
        if (cloudRow) cloudRow.SetActive(Application.platform != RuntimePlatform.Android);
        var st = CloudSync.State;
        bool ok = st == CloudSync.Status.Synced;
        if (cloudText)
        {
            cloudText.text = Loc.T(ok ? "set.cloud.on" : st == CloudSync.Status.Connecting ? "set.cloud.wait" : st == CloudSync.Status.Error ? "set.cloud.error" : "set.cloud.off");
            cloudText.color = ok ? UiKit.Success : UiKit.Secondary;
        }
        if (cloudIcon) { cloudIcon.color = ok ? UiKit.Success : UiKit.Hazard; if (cloudOkIcon && cloudWarnIcon) cloudIcon.sprite = ok ? cloudOkIcon : cloudWarnIcon; }
        if (cloud)
        {
            cloud.gameObject.SetActive(!ok && st != CloudSync.Status.Connecting);
            var pb = cloud.GetComponent<PressButton>();
            if (pb) pb.SetLabel(Loc.T(st == CloudSync.Status.Error ? "set.cloud.retry" : "set.cloud.connect"));
        }
    }

    void Refresh()
    {
        if (!root || !root.activeSelf) return;
        RefreshCloud();
        var s = Settings.S;
        music.SetValueWithoutNotify(s.music);
        sfx.SetValueWithoutNotify(s.sfx);
        joySize.SetValueWithoutNotify(s.joystickSize);
        joyAlpha.SetValueWithoutNotify(s.joystickAlpha);
        vibrationLabel.text = Loc.T(s.vibration ? "set.on" : "set.off");
        if (vibrationTrack) vibrationTrack.color = s.vibration ? UiKit.Hazard : UiKit.Hex(0x2E2740);
        if (vibrationKnob) vibrationKnob.anchoredPosition = new Vector2(s.vibration ? 16f : -16f, 0f);
        if (musicValue) musicValue.text = Mathf.RoundToInt(s.music * 100f).ToString();
        if (sfxValue) sfxValue.text = Mathf.RoundToInt(s.sfx * 100f).ToString();
        if (joySizeValue) joySizeValue.text = Mathf.RoundToInt(Mathf.InverseLerp(Settings.MinJoystickSize, Settings.MaxJoystickSize, s.joystickSize) * 100f).ToString();
        if (joyAlphaValue) joyAlphaValue.text = Mathf.RoundToInt(Mathf.InverseLerp(Settings.MinJoystickAlpha, Settings.MaxJoystickAlpha, s.joystickAlpha) * 100f).ToString();
        if (version) version.text = Loc.F("set.version", Application.version);
        var fps = Settings.SupportedFps(Settings.DisplayHz());
        int curFps = Settings.ClampFps(s.fps, Settings.DisplayHz());
        for (int i = 0; i < fpsButtons.Length; i++)
        {
            int f = Settings.FpsOptions[i];
            fpsButtons[i].gameObject.SetActive(fps.Contains(f));
            Seg(fpsButtons[i], f == curFps);
        }
        for (int i = 0; i < qualityButtons.Length; i++) Seg(qualityButtons[i], i == Settings.Quality);
        for (int i = 0; i < langButtons.Length; i++) Seg(langButtons[i], (i == 0 ? "tr" : "en") == s.lang);
    }

    /// <summary>Segmented control cell: selected = primary yellow, others ghost.</summary>
    static void Seg(Button b, bool on)
    {
        var pb = b.GetComponent<PressButton>();
        if (pb) pb.SetStyle(on ? UiStyle.Yellow : UiStyle.Ghost);
    }
}
