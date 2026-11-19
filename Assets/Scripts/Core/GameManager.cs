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

    float hitStopUntil, hitStopScale = 0.02f, slow = 1f;
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
        Settings.ApplyFrameRate();
        Time.timeScale = TimeScale;
        var rec = SaveSystem.Data.records;
        bool hard = GameMode.IsHard;
        Records = new RunRecords
        {
            bestScore = hard ? rec.hardScore : rec.bestScore,
            bestWave = hard ? rec.hardWave : rec.bestWave,
            bestKills = hard ? rec.hardKills : rec.bestKills,
            bestTime = hard ? rec.hardTime : rec.bestTime,
        };
    }

    void Update()
    {
        if (hitStopActive && Time.unscaledTime >= hitStopUntil)
        {
            hitStopActive = false;
            if (!IsGameOver && !Paused) Time.timeScale = TimeScale * slow;
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
        if (GameMode.IsCoop)
        {
            // Co-op: the room ends with the run — back to the menu (a new room is a new run).
            Time.timeScale = TimeScale;
            _ = CoopSession.Leave();
            LoadingScreen.Load(LoadingScreen.MenuScene);
            return;
        }
        // Chapter cleared: the big button goes on to the next chapter.
        if (Won && GameMode.IsCampaign && GameMode.Chapter + 1 < Campaign.Count) GameMode.Chapter++;
        Time.timeScale = TimeScale;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public static int MultiplierFor(int combo)
    {
        int m = 1;
        for (int i = 1; i < MultiplierSteps.Length; i++) if (combo >= MultiplierSteps[i]) m = i + 1;
        return m;
    }

    public int SpecialKills { get; private set; }

    /// <summary>Raised for every kill (after scoring) — progression / cards listen.</summary>
    public event System.Action<Zombie> Killed;

    /// <summary>Raised for every zombie the team kills (single player: every kill) — boss reward, kill events, drops.</summary>
    public event System.Action<Zombie> TeamKilled;

    /// <summary>
    /// A zombie died on this (authoritative) game: the score / combo / XP go to this player only when they landed the
    /// killing blow (<paramref name="mine"/>); a teammate's kill is credited on their own game via the kill event.
    /// </summary>
    public void CreditKill(Zombie z, bool mine)
    {
        if (mine || !GameMode.IsCoop) RegisterKill(z);
        TeamKilled?.Invoke(z);
    }

    /// <summary>Co-op client: the host says the team killed <paramref name="z"/> (a puppet); mine = my killing blow.</summary>
    public void RemoteKill(Zombie z, bool mine)
    {
        if (mine) RegisterKill(z);
        TeamKilled?.Invoke(z);
    }

    public void RegisterKill(Zombie z)
    {
        if (z.IsSpecial) SpecialKills++;
        RegisterKill(z.IsRunner ? RunnerScore : z.score);
        Killed?.Invoke(z);
    }

    public void RegisterKill(bool runner) => RegisterKill(runner ? RunnerScore : WalkerScore);

    public void RegisterKill(int baseScore)
    {
        Kills++;
        Combo++;
        ComboTimeLeft = ComboWindow;
        BestCombo = Mathf.Max(BestCombo, Combo);
        int m = MultiplierFor(Combo);
        if (m > Multiplier) GameAudio.Play(SfxId.ComboUp, PlayerPos, 1f + 0.07f * (m - 2));
        Multiplier = m;
        Score += baseScore * Multiplier;
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

    /// <summary>Objective / story bonus (not multiplied by the combo).</summary>
    public void AddBonus(int amount)
    {
        if (IsGameOver || amount <= 0) return;
        Score += amount;
    }

    Vector3 PlayerPos => player ? player.transform.position : Vector3.zero;

    /// <summary>Global slow-motion factor (level-up card pick); 1 = normal.</summary>
    public void SetSlow(float factor)
    {
        if (GameMode.IsCoop) factor = 1f; // a shared world can't slow down for one player's card pick
        slow = Mathf.Clamp(factor, 0.01f, 1f);
        if (!IsGameOver && !hitStopActive && !Paused) Time.timeScale = TimeScale * slow;
    }

    /// <summary>Pause menu / app in background: time stops, audio pauses, gameplay input is cut.</summary>
    public bool Paused { get; private set; }
    public event System.Action<bool> PausedChanged;

    public void SetPaused(bool p)
    {
        if (p == Paused || (p && IsGameOver)) return;
        Paused = p;
        // Co-op: the menu opens but the shared world keeps running.
        if (!GameMode.IsCoop) Time.timeScale = p ? 0f : TimeScale * slow;
        if (!p) hitStopActive = false;
        if (!GameMode.IsCoop) AudioListener.pause = p;
        if (TouchControls.I) TouchControls.I.Paused = p;
        PausedChanged?.Invoke(p);
    }

    void OnApplicationPause(bool background) { if (background) SetPaused(true); }
    void OnApplicationFocus(bool focus) { if (!focus && !Application.isEditor) SetPaused(true); }
    void OnDestroy() { if (Paused) { AudioListener.pause = false; Time.timeScale = TimeScale; } }

    public bool IsSlowed => slow < 1f;

    public void HitStop(float duration) => HitStop(duration, 0.02f);

    /// <summary>Short slow motion (boss kill); <paramref name="realSeconds"/> of unscaled time at <paramref name="factor"/>.</summary>
    public void SlowMo(float factor, float realSeconds) => HitStop(realSeconds, factor);

    void HitStop(float duration, float scale)
    {
        if (IsGameOver || slow < 1f || Paused || GameMode.IsCoop) return;
        float until = Time.unscaledTime + duration;
        // The longer-lasting request sets the scale (a kill hit-stop inside the boss slow-mo keeps the slow-mo).
        if (!hitStopActive || until >= hitStopUntil) hitStopScale = scale;
        hitStopUntil = Mathf.Max(hitStopUntil, until);
        hitStopActive = true;
        Time.timeScale = hitStopScale * TimeScale;
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

    /// <summary>Raised when the run ends (rewards, quests) — again after a continue.</summary>
    public event System.Action GameOverStarted;
    public event System.Action Revived;
    public int Continues { get; private set; }
    Coroutine gameOverCo;

    /// <summary>Continue (rewarded ad / gems): back to life, zombies around pushed away.</summary>
    public void Revive()
    {
        if (!IsGameOver || !player) return;
        if (gameOverCo != null) StopCoroutine(gameOverCo);
        IsGameOver = false;
        canRestart = false;
        Continues++;
        Time.timeScale = TimeScale * slow;
        Vector3 p = player.transform.position;
        for (int i = Zombie.Active.Count - 1; i >= 0; i--)
        {
            var z = Zombie.Active[i];
            Vector3 d = z.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude > 12f * 12f) continue;
            if (z.Kind == ZombieKind.Boss) z.Knock(d.normalized * 12f);
            else z.Despawn();
        }
        player.Revive();
        if (hud) hud.HideGameOver();
        if (Fx.I) Fx.I.Explosion(p, 5f, false);
        GameAudio.Play(SfxId.LevelUp, p);
        Revived?.Invoke();
    }

    /// <summary>Co-op: the local soldier went down (not the end: teammates can revive; the host ends the run when nobody stands).</summary>
    public event System.Action LocalDowned;

    public void OnLocalDown()
    {
        if (IsGameOver) return;
        BreakCombo();
        Shake(0.6f);
        LocalDowned?.Invoke();
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
        var rec = SaveSystem.Data.records;
        if (GameMode.IsHard) { rec.hardScore = r.bestScore; rec.hardWave = r.bestWave; rec.hardKills = r.bestKills; rec.hardTime = r.bestTime; }
        else if (GameMode.IsCoop) r.newScore = false; // team runs don't touch the solo records
        else if (!GameMode.IsCampaign) { rec.bestScore = r.bestScore; rec.bestWave = r.bestWave; rec.bestKills = r.bestKills; rec.bestTime = r.bestTime; }
        else r.newScore = false; // chapters have stars, not score records
        Records = r;
        SaveSystem.Write();
        StoreReview.RunFinished(r.newScore || wave >= StoreReview.GoodWave);
        GameOverStarted?.Invoke();
        gameOverCo = StartCoroutine(GameOverRoutine(wave));
    }

    /// <summary>Campaign chapter cleared (true) — the run ends with the victory screen.</summary>
    public bool Won { get; private set; }
    public int ChapterStars { get; private set; }
    public int ChapterGold { get; private set; }
    public int ChapterGems { get; private set; }
    public bool ChapterFirstClear { get; private set; }

    /// <summary>The last objective of a chapter is done: stars, first-clear reward, victory screen.</summary>
    public void TriggerVictory()
    {
        if (IsGameOver) return;
        Won = true;
        IsGameOver = true;
        hitStopActive = false;
        Time.timeScale = 0.5f * TimeScale;
        int ch = GameMode.Chapter;
        var def = Campaign.Get(ch);
        float h01 = player ? player.Health / Mathf.Max(1f, player.maxHealth) : 0f;
        ChapterStars = Campaign.Stars(GameTime, def.parSeconds, h01);
        ChapterFirstClear = Campaign.Record(SaveSystem.Data.campaign, ch, ChapterStars);
        Campaign.Reward(ch, ChapterFirstClear, out int gold, out int gems);
        ChapterGold = gold; ChapterGems = gems;
        Profile.Grant(gold, gems, "chapter");
        SaveSystem.Write();
        StoreReview.RunFinished(true);
        if (MusicDirector.I) MusicDirector.I.Duck(0.4f, 1.5f);
        GameOverStarted?.Invoke();
        gameOverCo = StartCoroutine(VictoryRoutine());
    }

    IEnumerator VictoryRoutine()
    {
        yield return new WaitForSecondsRealtime(1.2f);
        Time.timeScale = TimeScale;
        GameAudio.Play(SfxId.NewRecord, PlayerPos);
        int wave = WaveDirector.I ? WaveDirector.I.Wave : 0;
        if (hud) hud.ShowVictory(GameMode.Chapter, ChapterStars, Score, GameTime, Kills, wave, ChapterGold, ChapterGems);
        yield return new WaitForSecondsRealtime(0.4f);
        canRestart = true;
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
