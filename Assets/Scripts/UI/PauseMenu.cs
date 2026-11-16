using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pause button (top left) → Resume / Settings / Main menu. The game also pauses when the app goes to the
/// background (<see cref="GameManager"/>). The game-over screen gets a Main menu button too.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public Button pauseButton, resume, settings, mainMenu, gameOverMenu;
    public GameObject panel;
    public SettingsPanel settingsPanel;

    bool subscribed;

    void Start()
    {
        pauseButton.onClick.AddListener(() => { Click(); Pause(); });
        resume.onClick.AddListener(() => { Click(); Resume(); });
        settings.onClick.AddListener(() => { Click(); settingsPanel.Open(); });
        mainMenu.onClick.AddListener(() => { Click(); ToMenu(); });
        if (gameOverMenu) gameOverMenu.onClick.AddListener(() => { Click(); ToMenu(); });
        panel.SetActive(false);
        Subscribe();
    }

    void Subscribe()
    {
        if (subscribed || !GameManager.I) return;
        GameManager.I.PausedChanged += OnPaused;
        subscribed = true;
    }

    void OnDestroy()
    {
        if (subscribed && GameManager.I) GameManager.I.PausedChanged -= OnPaused;
    }

    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Pause() { if (GameManager.I) GameManager.I.SetPaused(true); }
    public void Resume() { if (GameManager.I) GameManager.I.SetPaused(false); }

    public void ToMenu()
    {
        if (GameManager.I) GameManager.I.SetPaused(false);
        LoadingScreen.Load(LoadingScreen.MenuScene);
    }

    void OnPaused(bool p)
    {
        panel.SetActive(p);
        if (!p && settingsPanel.IsOpen) settingsPanel.Close();
    }

    void Update()
    {
        Subscribe();
        var gm = GameManager.I;
        bool show = gm && !gm.IsGameOver && !gm.Paused && !(CardPicker.I && CardPicker.I.IsOpen);
        if (pauseButton.gameObject.activeSelf != show) pauseButton.gameObject.SetActive(show);
    }
}
