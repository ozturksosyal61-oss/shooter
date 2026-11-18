using System;
using UnityEngine;

/// <summary>
/// Wave loop: wave (30–45 s of spawning) → breather (spawns stop, health regenerates) → bigger wave.
/// Difficulty comes from <see cref="Difficulty.ForWave"/>.
/// </summary>
public class WaveDirector : MonoBehaviour
{
    public static WaveDirector I { get; private set; }

    public float firstWaveDelay = 1.5f;
    public float breakDuration = 9f;

    public int Wave { get; private set; }
    public bool InBreak { get; private set; } = true;
    public float PhaseTimeLeft { get; private set; }
    public WaveParams Params { get; private set; }

    public event Action<int> WaveStarted;
    public event Action<int> WaveCleared;

    void Awake()
    {
        I = this;
        PhaseTimeLeft = firstWaveDelay;
        // Campaign chapters start further in (the first wave is the chapter's start wave).
        if (GameMode.IsCampaign) Wave = Mathf.Max(0, Campaign.Get(GameMode.Chapter).startWave - 1);
        Params = Difficulty.ForWave(Mathf.Max(1, Wave));
    }

    /// <summary>Skips ahead: the breather before <paramref name="wave"/> starts now (bots / scripted scenarios).</summary>
    public void JumpToBreakBefore(int wave)
    {
        Wave = Mathf.Max(0, wave - 1);
        Params = Difficulty.ForWave(Mathf.Max(1, Wave));
        InBreak = true;
        PhaseTimeLeft = breakDuration;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm && gm.IsGameOver) return;
        PhaseTimeLeft -= Time.deltaTime;
        if (PhaseTimeLeft > 0f) return;

        if (InBreak)
        {
            Wave++;
            Params = Difficulty.ForWave(Wave);
            InBreak = false;
            PhaseTimeLeft = Params.duration;
            GameAudio.Play(SfxId.WaveStart, gm ? gm.player.transform.position : Vector3.zero);
            WaveStarted?.Invoke(Wave);
        }
        else
        {
            // Boss wave: keeps going until the boss is down.
            if (BossDirector.I && BossDirector.I.HoldsWave) { PhaseTimeLeft = 0f; return; }
            InBreak = true;
            PhaseTimeLeft = breakDuration;
            GameAudio.Play(SfxId.WaveClear, gm ? gm.player.transform.position : Vector3.zero);
            if (gm) gm.OnWaveCleared(Wave);
            WaveCleared?.Invoke(Wave);
        }
    }
}
