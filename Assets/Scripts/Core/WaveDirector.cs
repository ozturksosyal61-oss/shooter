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
        Params = Difficulty.ForWave(1);
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
            InBreak = true;
            PhaseTimeLeft = breakDuration;
            GameAudio.Play(SfxId.WaveClear, gm ? gm.player.transform.position : Vector3.zero);
            if (gm) gm.OnWaveCleared(Wave);
            WaveCleared?.Invoke(Wave);
        }
    }
}
