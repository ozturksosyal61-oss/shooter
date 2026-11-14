using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    public PlayerController player;
    public CameraRig cameraRig;
    public HUD hud;

    public const float ComboWindow = 2f;

    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public int Combo { get; private set; }
    public float ComboTimeLeft { get; private set; }
    public int Multiplier => Mathf.Min(1 + Combo / 5, 10);
    public bool IsGameOver { get; private set; }
    public float GameTime { get; private set; }

    int comboKills;
    float hitStopUntil;
    bool hitStopActive;
    bool canRestart;

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Time.timeScale = 1f;
        BestScore = PlayerPrefs.GetInt("best", 0);
    }

    void Update()
    {
        if (hitStopActive && Time.unscaledTime >= hitStopUntil)
        {
            hitStopActive = false;
            if (!IsGameOver) Time.timeScale = 1f;
        }

        if (IsGameOver)
        {
            if (canRestart && TouchControls.I != null && TouchControls.I.AnyPressThisFrame)
            {
                GameAudio.Play(SfxId.UiClick, Vector3.zero);
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            return;
        }

        GameTime += Time.deltaTime;
        if (Combo > 0)
        {
            ComboTimeLeft -= Time.deltaTime;
            if (ComboTimeLeft <= 0f) { Combo = 0; ComboTimeLeft = 0f; comboKills = 0; }
        }
    }

    public void RegisterHit()
    {
        int before = Multiplier;
        Combo++;
        ComboTimeLeft = ComboWindow;
        // Rising "ding" each time the multiplier goes up.
        if (Multiplier > before) GameAudio.Play(SfxId.ComboUp, PlayerPos, 1f + 0.07f * (Multiplier - 2));
    }

    public void RegisterKill()
    {
        Score += 100 * Multiplier;
        comboKills++;
        if (comboKills % 5 == 0) GameAudio.Play(SfxId.KillStreak, PlayerPos, 1f + 0.05f * Mathf.Min(comboKills / 5 - 1, 6));
    }

    Vector3 PlayerPos => player ? player.transform.position : Vector3.zero;

    public void HitStop(float duration)
    {
        if (IsGameOver) return;
        hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + duration);
        hitStopActive = true;
        Time.timeScale = 0.02f;
    }

    public void Shake(float trauma)
    {
        if (cameraRig) cameraRig.AddTrauma(trauma);
    }

    public void OnPlayerHurt()
    {
        Shake(0.45f);
        if (hud) hud.FlashDamage();
    }

    public void TriggerGameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        hitStopActive = false;
        Time.timeScale = 0.3f;
        if (MusicDirector.I) MusicDirector.I.Duck(0.4f, 1f);
        if (Score > BestScore) { BestScore = Score; PlayerPrefs.SetInt("best", BestScore); PlayerPrefs.Save(); }
        StartCoroutine(GameOverRoutine());
    }

    IEnumerator GameOverRoutine()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        Time.timeScale = 1f;
        GameAudio.Play(SfxId.GameOver, PlayerPos);
        if (hud) hud.ShowGameOver(Score, BestScore);
        yield return new WaitForSecondsRealtime(0.6f);
        canRestart = true;
    }
}
