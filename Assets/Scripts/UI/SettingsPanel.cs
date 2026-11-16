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
    public const string PrivacyUrl = "https://oztdigital.com.tr/holdout/privacy";

    public GameObject root;
    public Slider music, sfx, joySize, joyAlpha;
    public Button vibration;
    public Text vibrationLabel;
    public Button[] fpsButtons;
    public Button[] qualityButtons;
    public Button[] langButtons;
    public Button support, privacy, reset, close;
    public GameObject confirm;
    public Button confirmYes, confirmNo;
    public Text resetDone;
    public Color onColor = new Color(1f, 0.55f, 0.1f), offColor = new Color(0.17f, 0.2f, 0.11f);

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
        support.onClick.AddListener(() => { Click(); Application.OpenURL("mailto:" + SupportMail + "?subject=Holdout"); });
        privacy.onClick.AddListener(() => { Click(); Application.OpenURL(PrivacyUrl); });
        reset.onClick.AddListener(() => { Click(); confirm.SetActive(true); });
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

    public void ResetProgress()
    {
        SaveSystem.ResetProgress();
        confirm.SetActive(false);
        if (resetDone) { resetDone.gameObject.SetActive(true); resetDoneT = 2f; }
    }

    void Update()
    {
        if (resetDoneT > 0f)
        {
            resetDoneT -= Time.unscaledDeltaTime;
            if (resetDoneT <= 0f && resetDone) resetDone.gameObject.SetActive(false);
        }
    }

    void Refresh()
    {
        if (!root || !root.activeSelf) return;
        var s = Settings.S;
        music.SetValueWithoutNotify(s.music);
        sfx.SetValueWithoutNotify(s.sfx);
        joySize.SetValueWithoutNotify(s.joystickSize);
        joyAlpha.SetValueWithoutNotify(s.joystickAlpha);
        vibrationLabel.text = Loc.T(s.vibration ? "set.on" : "set.off");
        vibration.image.color = s.vibration ? onColor : offColor;
        var fps = Settings.SupportedFps(Settings.DisplayHz());
        int curFps = Settings.ClampFps(s.fps, Settings.DisplayHz());
        for (int i = 0; i < fpsButtons.Length; i++)
        {
            int f = Settings.FpsOptions[i];
            fpsButtons[i].gameObject.SetActive(fps.Contains(f));
            fpsButtons[i].image.color = f == curFps ? onColor : offColor;
        }
        for (int i = 0; i < qualityButtons.Length; i++) qualityButtons[i].image.color = i == Settings.Quality ? onColor : offColor;
        for (int i = 0; i < langButtons.Length; i++) langButtons[i].image.color = (i == 0 ? "tr" : "en") == s.lang ? onColor : offColor;
    }
}
