using System;
using UnityEngine;
using UnityEngine.Audio;

public enum SfxId
{
    Shoot, Hit, Crit, Kill, PlayerHurt, PlayerDeath, Vault, ZombieSpawn, ZombieAttack, GameOver,
    ReloadStart, ReloadEnd, ZombieScream,
    HitConcrete, HitMetal, ZombieMoan, Footstep, VaultLand, MagOut, MagIn, Bolt,
    DryFire, LowAmmo, ComboUp, KillStreak, UiClick, Explosion,
    ComboBreak, WaveStart, WaveClear, Pickup, ActiveReloadPerfect, ActiveReloadFail, NewRecord,
    // F7 — special zombies
    ShieldSpawn, ShieldBlock, BombSpawn, BombFuse, BombExplode, TongueSpawn, TongueWarn, TongueLash, TongueGrab, TongueSnap,
    // F6 — roguelite / barrels
    LevelUp, CardPick, Shotgun, TurretShot, GrenadeThrow, BarrelExplode,
}

[Serializable]
public class SfxEntry
{
    public SfxId id;
    public AudioClip[] clips;
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0f, 0.5f)] public float pitchVariation = 0.08f;
    [Range(0f, 0.5f)] public float volumeVariation = 0.1f;
    [Tooltip("Max simultaneous voices of this sound; the oldest is stolen beyond that.")]
    public int maxVoices = 4;
    public float minInterval = 0.03f;
    [Range(0f, 1f)] public float spatialBlend = 0.8f;
    public float minDistance = 8f;
    public float maxDistance = 40f;
    public bool ui;
    public bool haptic;
    public Haptics.Kind hapticKind;
    public bool duckMusic;
}

/// <summary>
/// Central sound player. Gameplay calls <see cref="Play"/>; <see cref="OnSfx"/> lets other systems listen.
/// Pooled AudioSources, per-sound voice limits, pitch/volume variation, top-down friendly 3D rolloff
/// (listener follows the player), haptics fired from the same call so they stay in sync, music ducking.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio I { get; private set; }
    public static event Action<SfxId, Vector3> OnSfx;

    public AudioMixer mixer;
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup uiGroup;
    public SfxEntry[] entries;
    public int voices = 24;
    public AudioClip heartbeatClip;

    struct Voice { public AudioSource src; public int id; public float started; }

    Voice[] pool;
    SfxEntry[] table;
    float[] lastPlayed;
    int[] lastClip;
    AudioSource heartbeat;
    bool lowHealth;

    void Awake()
    {
        I = this;
        int n = Enum.GetValues(typeof(SfxId)).Length;
        table = new SfxEntry[n];
        lastPlayed = new float[n];
        lastClip = new int[n];
        for (int i = 0; i < n; i++) { lastPlayed[i] = -10f; lastClip[i] = -1; }
        if (entries != null) foreach (var e in entries) table[(int)e.id] = e;

        pool = new Voice[voices];
        for (int i = 0; i < voices; i++)
        {
            var go = new GameObject("Voice" + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.dopplerLevel = 0f;
            pool[i] = new Voice { src = s, id = -1 };
        }

        heartbeat = gameObject.AddComponent<AudioSource>();
        heartbeat.clip = heartbeatClip;
        heartbeat.loop = true;
        heartbeat.playOnAwake = false;
        heartbeat.spatialBlend = 0f;
        heartbeat.outputAudioMixerGroup = sfxGroup;
    }

    void Start() => AudioPrefs.Apply(mixer);

    public static void Play(SfxId id, Vector3 pos, float pitchMul = 1f, float volumeMul = 1f)
    {
        OnSfx?.Invoke(id, pos);
        if (I) I.PlayInternal(id, pos, pitchMul, volumeMul);
    }

    void PlayInternal(SfxId id, Vector3 pos, float pitchMul, float volumeMul)
    {
        int i = (int)id;
        var e = table[i];
        if (e == null) return;
        if (e.haptic) Haptics.Play(e.hapticKind);
        if (e.duckMusic && MusicDirector.I) MusicDirector.I.Duck(0.35f, 0.35f);
        if (e.clips == null || e.clips.Length == 0) return;

        float now = Time.unscaledTime;
        if (now - lastPlayed[i] < e.minInterval) return;
        lastPlayed[i] = now;

        // Voice selection: respect the per-sound limit, otherwise a free voice, otherwise steal the oldest.
        int same = 0, oldestSame = -1, free = -1, oldest = 0;
        for (int v = 0; v < pool.Length; v++)
        {
            bool busy = pool[v].src.isPlaying;
            if (!busy) { if (free < 0) free = v; continue; }
            if (pool[v].id == i)
            {
                same++;
                if (oldestSame < 0 || pool[v].started < pool[oldestSame].started) oldestSame = v;
            }
            if (pool[v].started < pool[oldest].started) oldest = v;
        }
        int pick = same >= e.maxVoices ? oldestSame : free >= 0 ? free : oldest;

        int c = e.clips.Length == 1 ? 0 : UnityEngine.Random.Range(0, e.clips.Length - 1);
        if (e.clips.Length > 1 && c >= lastClip[i]) c++; // never the same clip twice in a row
        lastClip[i] = c;

        var s = pool[pick].src;
        s.Stop();
        s.clip = e.clips[c];
        s.outputAudioMixerGroup = e.ui ? uiGroup : sfxGroup;
        s.spatialBlend = e.ui ? 0f : e.spatialBlend;
        s.minDistance = e.minDistance;
        s.maxDistance = e.maxDistance;
        s.transform.position = pos;
        s.pitch = (1f + UnityEngine.Random.Range(-e.pitchVariation, e.pitchVariation)) * pitchMul;
        s.volume = e.volume * volumeMul * (1f - UnityEngine.Random.value * e.volumeVariation);
        s.Play();
        pool[pick].id = i;
        pool[pick].started = now;
    }

    /// <summary>Looping heartbeat while health is low; <paramref name="urgency"/> 0..1 speeds it up.</summary>
    public void SetLowHealth(bool on, float urgency)
    {
        if (!heartbeat || !heartbeatClip) return;
        if (on != lowHealth)
        {
            lowHealth = on;
            if (on) heartbeat.Play(); else heartbeat.Stop();
        }
        if (on)
        {
            heartbeat.pitch = Mathf.Lerp(0.95f, 1.35f, urgency);
            heartbeat.volume = Mathf.Lerp(0.5f, 0.9f, urgency);
        }
    }
}

/// <summary>Saved volume settings (0..1) applied to the AudioMixer's exposed parameters.</summary>
public static class AudioPrefs
{
    public const string Master = "MasterVolume", Music = "MusicVolume", Sfx = "SfxVolume", Ui = "UiVolume";

    public static float Get(string key, float def = 1f) => PlayerPrefs.GetFloat("vol_" + key, def);

    public static void Set(AudioMixer mixer, string key, float value)
    {
        PlayerPrefs.SetFloat("vol_" + key, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        if (mixer) mixer.SetFloat(key, ToDb(value));
    }

    public static void Apply(AudioMixer mixer)
    {
        if (!mixer) return;
        mixer.SetFloat(Master, ToDb(Get(Master)));
        mixer.SetFloat(Music, ToDb(Get(Music, 0.8f)));
        mixer.SetFloat(Sfx, ToDb(Get(Sfx)));
        mixer.SetFloat(Ui, ToDb(Get(Ui)));
    }

    public static float ToDb(float v) => v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
}
