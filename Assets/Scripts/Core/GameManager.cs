using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Run state: score, kill-based combo, stats, records, hitstop, game over and restart.</summary>
public class GameManager : MonoBehaviour
{
    public static GameManager I { get; private set; }

    /// <summary>Base time scale (1 in the game; play-test bots raise it to simulate faster).</summary>
    public static float TimeScale = 1f;

    public PlayerController player;
    public CameraRig cameraRig;
    public HUD hud;

    public const float ComboWindow = 3f;
    /// <summary>Kills in a chain needed for x1..x10 — each step is harder than the last.</summary>
    public static readonly int[] MultiplierSteps = { 0, 5, 12, 22, 35, 50, 70, 95, 125, 160 };
    public const int WalkerScore = 10, RunnerScore = 15, WaveBonus = 50;

    public int Score { get; private set; }
    public int Combo { get; private set; }
    public float ComboTimeLeft { get; private set; }
    public int Multiplier { get; private set; } = 1;
    public int Kills { get; private set; }
    public int BestCombo { get; private set; }
    public bool IsGameOver { get; private set; }
    public float GameTime { get; private set; }
    public RunRecords Records { get; private set; }

    float hitStopUntil;
    bool hitStopActive;
    bool canRestart;

    public struct RunRecords
    {
        public int bestScore, bestWave, bestKills;
        public float bestTime;
        public bool newScore;
    }

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Time.timeScale = TimeScale;
        Records = new RunRecords
        {
            bestScore = PlayerPrefs.GetInt("best_v2", 0),
            bestWave = PlayerPrefs.GetInt("bestWave", 0),
            bestKills = PlayerPrefs.GetInt("bestKills", 0),
            bestTime = PlayerPrefs.GetFloat("bestTime", 0f),
        };
    }

    void Update()
    {
        if (hitStopActive && Time.unscaledTime >= hitStopUntil)
        {
            hitStopActive = false;
            if (!IsGameOver) Time.timeScale = TimeScale;
        }

        var input = TouchControls.I;
        if (IsGameOver)
        {
            if (canRestart && input && input.RestartPressed) Restart();
            return;
        }

        GameTime += Time.deltaTime;
        if (Combo > 0)
        {
            ComboTimeLeft -= Time.deltaTime;
            if (ComboTimeLeft <= 0f) ResetCombo();
        }
    }

    void Restart()
    {
        GameAudio.Play(SfxId.UiClick, Vector3.zero);
        Time.timeScale = TimeScale;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static int MultiplierFor(int combo)
    {
        int m = 1;
        for (int i = 1; i < MultiplierSteps.Length; i++) if (combo >= MultiplierSteps[i]) m = i + 1;
        return m;
    }

    public void RegisterKill(bool runner)
    {
        Kills++;
        Combo++;
        ComboTimeLeft = ComboWindow;
        BestCombo = Mathf.Max(BestCombo, Combo);
        int m = MultiplierFor(Combo);
        if (m > Multiplier) GameAudio.Play(SfxId.ComboUp, PlayerPos, 1f + 0.07f * (m - 2));
        Multiplier = m;
        Score += (runner ? RunnerScore : WalkerScore) * Multiplier;
        if (Combo % 10 == 0) GameAudio.Play(SfxId.KillStreak, PlayerPos, 1f + 0.04f * Mathf.Min(Combo / 10 - 1, 8));
    }

    void ResetCombo()
    {
        Combo = 0;
        ComboTimeLeft = 0f;
        Multiplier = 1;
    }

    /// <summary>Taking damage shatters the combo.</summary>
    public void BreakCombo()
    {
        if (Combo == 0) return;
        int lost = Multiplier;
        if (Combo >= 3)
        {
            GameAudio.Play(SfxId.ComboBreak, PlayerPos);
            if (hud) hud.ComboBroken(lost);
        }
        ResetCombo();
    }

    public void OnWaveCleared(int wave) => Score += WaveBonus * wave;

    Vector3 PlayerPos => player ? player.transform.position : Vector3.zero;

    public void HitStop(float duration)
    {
        if (IsGameOver) return;
        hitStopUntil = Mathf.Max(hitStopUntil, Time.unscaledTime + duration);
        hitStopActive = true;
        Time.timeScale = 0.02f * TimeScale;
    }

    public void Shake(float trauma)
    {
        if (cameraRig) cameraRig.AddTrauma(trauma);
    }

    public void OnPlayerHurt()
    {
        Shake(0.45f);
        BreakCombo();
        if (hud) hud.FlashDamage();
    }

    public void TriggerGameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;
        hitStopActive = false;
        Time.timeScale = 0.3f * TimeScale;
        if (MusicDirector.I) MusicDirector.I.Duck(0.4f, 1f);

        var r = Records;
        int wave = WaveDirector.I ? WaveDirector.I.Wave : 0;
        r.newScore = Score > r.bestScore && Score > 0;
        r.bestScore = Mathf.Max(r.bestScore, Score);
        r.bestWave = Mathf.Max(r.bestWave, wave);
        r.bestKills = Mathf.Max(r.bestKills, Kills);
        r.bestTime = Mathf.Max(r.bestTime, GameTime);
        Records = r;
        PlayerPrefs.SetInt("best_v2", r.bestScore);
        PlayerPrefs.SetInt("bestWave", r.bestWave);
        PlayerPrefs.SetInt("bestKills", r.bestKills);
        PlayerPrefs.SetFloat("bestTime", r.bestTime);
        PlayerPrefs.Save();
        StartCoroutine(GameOverRoutine(wave));
    }

    IEnumerator GameOverRoutine(int wave)
    {
        yield return new WaitForSecondsRealtime(0.7f);
        Time.timeScale = TimeScale;
        GameAudio.Play(Records.newScore ? SfxId.NewRecord : SfxId.GameOver, PlayerPos);
        if (hud) hud.ShowGameOver(Score, GameTime, Kills, BestCombo, wave, Records);
        yield return new WaitForSecondsRealtime(0.2f);
        canRestart = true;
    }
}
