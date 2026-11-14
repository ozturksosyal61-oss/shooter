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
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        GameTime += Time.deltaTime;
        if (Combo > 0)
        {
            ComboTimeLeft -= Time.deltaTime;
            if (ComboTimeLeft <= 0f) { Combo = 0; ComboTimeLeft = 0f; }
        }
    }

    public void RegisterHit()
    {
        Combo++;
        ComboTimeLeft = ComboWindow;
    }

    public void RegisterKill() => Score += 100 * Multiplier;

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
        Haptics.Play(Haptics.Kind.Heavy);
        if (hud) hud.FlashDamage();
    }

    public void TriggerGameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        hitStopActive = false;
        Time.timeScale = 0.3f;
        GameAudio.Play(SfxId.GameOver, player ? player.transform.position : Vector3.zero);
        if (Score > BestScore) { BestScore = Score; PlayerPrefs.SetInt("best", BestScore); PlayerPrefs.Save(); }
        StartCoroutine(GameOverRoutine());
    }

    IEnumerator GameOverRoutine()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        Time.timeScale = 1f;
        if (hud) hud.ShowGameOver(Score, BestScore);
        yield return new WaitForSecondsRealtime(0.6f);
        canRestart = true;
    }
}
