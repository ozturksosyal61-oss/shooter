using System;
using UnityEngine;

public enum SfxId
{
    Shoot, Hit, Crit, Kill, PlayerHurt, PlayerDeath, Vault, ZombieSpawn, ZombieAttack, GameOver
}

/// <summary>
/// Sound hook point. Gameplay calls <see cref="Play"/>; clips can be assigned later in the inspector
/// (index = SfxId). <see cref="OnSfx"/> lets other systems (music ducking, analytics) listen.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio I { get; private set; }
    public static event Action<SfxId, Vector3> OnSfx;

    [Tooltip("Indexed by SfxId. Empty slots are silent.")]
    public AudioClip[] clips = new AudioClip[Enum.GetValues(typeof(SfxId)).Length];
    [Range(0f, 1f)] public float volume = 1f;
    public int voices = 12;

    AudioSource[] sources;
    int next;
    readonly float[] lastPlayed = new float[Enum.GetValues(typeof(SfxId)).Length];

    void Awake()
    {
        I = this;
        sources = new AudioSource[voices];
        for (int i = 0; i < voices; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            sources[i] = s;
        }
    }

    public static void Play(SfxId id, Vector3 pos, float pitchJitter = 0.08f)
    {
        OnSfx?.Invoke(id, pos);
        if (I) I.PlayClip(id, pitchJitter);
    }

    void PlayClip(SfxId id, float pitchJitter)
    {
        int i = (int)id;
        if (clips == null || i >= clips.Length || clips[i] == null) return;
        if (Time.unscaledTime - lastPlayed[i] < 0.03f) return; // avoid stacking the same sound
        lastPlayed[i] = Time.unscaledTime;
        var s = sources[next];
        next = (next + 1) % sources.Length;
        s.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
        s.PlayOneShot(clips[i], volume);
    }
}
