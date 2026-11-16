using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Audio: import settings, AudioMixer (Master / Music / SFX / UI), sound bank, music and listener.</summary>
public static partial class GameSceneBuilder
{
    const string AudioDir = "Assets/Audio";
    const string MixerPath = "Assets/Audio/GameMixer.mixer";

    class AudioRefs
    {
        public AudioMixer mixer;
        public AudioMixerGroup music, sfx, ui;
    }

    // ---------------------------------------------------------------- import settings
    static void ConfigureAudioImports()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = (AudioImporter)AssetImporter.GetAtPath(path);
            bool music = path.Contains("/Music/");
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var s = imp.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            if (music)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.quality = 0.4f;
                imp.forceToMono = false;
                imp.loadInBackground = true;
            }
            else
            {
                // Short one-shots decompress on load (no CPU decode at play time); longer voices stay compressed.
                s.loadType = clip.length <= 1.0f ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
                s.quality = 0.5f;
                imp.forceToMono = true;
                imp.loadInBackground = false;
            }
            s.preloadAudioData = true;
            imp.defaultSampleSettings = s;
            imp.SaveAndReimport();
        }
    }

    // ---------------------------------------------------------------- mixer
    static Type EditorType(string name) =>
        AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null);

    /// <summary>
    /// Creates the AudioMixer through UnityEditor.Audio's internal API (there is no public one):
    /// Master → Music / SFX / UI, each volume exposed as MasterVolume / MusicVolume / SfxVolume / UiVolume.
    /// </summary>
    static AudioRefs CreateMixer()
    {
        const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        AssetDatabase.DeleteAsset(MixerPath);
        var ctrlType = EditorType("UnityEditor.Audio.AudioMixerController");
        var pathType = EditorType("UnityEditor.Audio.AudioGroupParameterPath");
        var ctrl = ctrlType.GetMethod("CreateMixerControllerAtPath", F).Invoke(null, new object[] { MixerPath });
        var master = ctrlType.GetProperty("masterGroup", F).GetValue(ctrl);
        var createGroup = ctrlType.GetMethod("CreateNewGroup", F);
        var addChild = ctrlType.GetMethod("AddChildToParent", F);
        var addExposed = ctrlType.GetMethods(F).First(m => m.Name == "AddExposedParameter" && m.GetParameters().Length == 1);
        var exposedProp = ctrlType.GetProperty("exposedParameters", F);

        object Group(string name)
        {
            var g = createGroup.Invoke(ctrl, new object[] { name, false });
            addChild.Invoke(ctrl, new[] { g, master });
            return g;
        }

        void Expose(object group, string name)
        {
            var guid = group.GetType().GetMethod("GetGUIDForVolume", F).Invoke(group, null);
            var path = Activator.CreateInstance(pathType, group, guid);
            addExposed.Invoke(ctrl, new[] { path });
            var arr = (Array)exposedProp.GetValue(ctrl);
            for (int i = 0; i < arr.Length; i++)
            {
                var e = arr.GetValue(i);
                var gf = e.GetType().GetField("guid", F);
                if (!gf.GetValue(e).Equals(guid)) continue;
                e.GetType().GetField("name", F).SetValue(e, name);
                arr.SetValue(e, i);
            }
            exposedProp.SetValue(ctrl, arr);
        }

        var music = Group("Music");
        var sfx = Group("SFX");
        var ui = Group("UI");
        Expose(master, AudioPrefs.Master);
        Expose(music, AudioPrefs.Music);
        Expose(sfx, AudioPrefs.Sfx);
        Expose(ui, AudioPrefs.Ui);
        EditorUtility.SetDirty((UnityEngine.Object)ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath);

        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        var refs = new AudioRefs
        {
            mixer = mixer,
            music = mixer.FindMatchingGroups("Music").First(),
            sfx = mixer.FindMatchingGroups("SFX").First(),
            ui = mixer.FindMatchingGroups("UI").First(),
        };
        Debug.Log("BUILD: mixer exposed=" + string.Join(",", ((Array)exposedProp.GetValue(ctrl)).Cast<object>()
            .Select(e => (string)e.GetType().GetField("name", F).GetValue(e))));
        return refs;
    }

    // ---------------------------------------------------------------- bank
    static AudioClip[] Clips(string prefix) =>
        AssetDatabase.FindAssets(prefix, new[] { AudioDir + "/SFX" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => System.IO.Path.GetFileNameWithoutExtension(p).StartsWith(prefix))
            .OrderBy(p => p)
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>)
            .ToArray();

    static SfxEntry E(SfxId id, string prefix, float volume = 1f, int maxVoices = 4, float spatial = 0.8f,
        float minInterval = 0.03f, bool ui = false, bool haptic = false, Haptics.Kind hk = Haptics.Kind.Light,
        float pitchVar = 0.08f, float volVar = 0.1f, bool duck = false, float minDist = 8f, float maxDist = 40f) => new SfxEntry
    {
        id = id, clips = prefix == null ? new AudioClip[0] : Clips(prefix), volume = volume, maxVoices = maxVoices,
        spatialBlend = spatial, minInterval = minInterval, ui = ui, haptic = haptic, hapticKind = hk,
        pitchVariation = pitchVar, volumeVariation = volVar, duckMusic = duck, minDistance = minDist, maxDistance = maxDist,
    };

    static SfxEntry[] SoundBank() => new[]
    {
        // Weapon (player sounds: mostly 2D so they never pan away from the hero).
        E(SfxId.Shoot, "rifle_shot", 0.7f, maxVoices: 4, spatial: 0.2f, minInterval: 0.02f, haptic: true, hk: Haptics.Kind.Light),
        E(SfxId.DryFire, "dry_fire", 0.8f, 1, spatial: 0f),
        E(SfxId.LowAmmo, "low_ammo", 0.6f, 1, ui: true, pitchVar: 0f),
        E(SfxId.MagOut, "reload_mag_out", 0.9f, 1, spatial: 0.2f),
        E(SfxId.MagIn, "reload_mag_in", 0.9f, 1, spatial: 0.2f),
        E(SfxId.Bolt, "reload_bolt", 0.9f, 1, spatial: 0.2f),
        E(SfxId.ReloadStart, null),
        E(SfxId.ReloadEnd, null, haptic: true, hk: Haptics.Kind.Light),
        // Impacts.
        E(SfxId.Hit, "hit_flesh", 0.75f, maxVoices: 5, minInterval: 0.02f),
        E(SfxId.Crit, "hit_crit", 0.8f, maxVoices: 3),
        E(SfxId.HitConcrete, "hit_concrete", 0.5f, maxVoices: 3, minInterval: 0.05f),
        E(SfxId.HitMetal, "hit_metal", 0.5f, maxVoices: 3, minInterval: 0.05f),
        // Zombies.
        E(SfxId.ZombieMoan, "zombie_moan", 0.55f, maxVoices: 3, minInterval: 0.6f, pitchVar: 0.12f, maxDist: 30f),
        E(SfxId.ZombieScream, "zombie_scream", 0.6f, maxVoices: 2, minInterval: 0.4f, pitchVar: 0.12f, maxDist: 35f),
        E(SfxId.ZombieAttack, "zombie_attack", 0.8f, maxVoices: 3, minInterval: 0.1f, pitchVar: 0.1f),
        E(SfxId.Kill, "zombie_death", 0.85f, maxVoices: 4, minInterval: 0.05f, haptic: true, hk: Haptics.Kind.Medium, pitchVar: 0.1f),
        E(SfxId.ZombieSpawn, null),
        // Player.
        E(SfxId.PlayerHurt, "player_hurt", 0.9f, 1, spatial: 0f, minInterval: 0.2f, haptic: true, hk: Haptics.Kind.Heavy),
        E(SfxId.PlayerDeath, "player_death", 1f, 1, spatial: 0f, haptic: true, hk: Haptics.Kind.Heavy, duck: true),
        E(SfxId.Footstep, "footstep", 0.35f, 2, spatial: 0.3f, minInterval: 0.12f),
        E(SfxId.Vault, "vault_jump", 0.6f, 1, spatial: 0.3f),
        E(SfxId.VaultLand, "vault_land", 0.75f, 1, spatial: 0.3f, haptic: true, hk: Haptics.Kind.Light),
        // Feedback / UI.
        E(SfxId.ComboUp, "combo_up", 0.5f, 2, ui: true, pitchVar: 0f, volVar: 0f),
        E(SfxId.KillStreak, "kill_streak", 0.7f, 1, ui: true, pitchVar: 0f, haptic: true, hk: Haptics.Kind.Heavy),
        E(SfxId.UiClick, "ui_click", 0.7f, 2, ui: true, pitchVar: 0.03f),
        E(SfxId.GameOver, "gameover_jingle", 0.9f, 1, ui: true, pitchVar: 0f, volVar: 0f),
        E(SfxId.Explosion, "explosion", 1f, 2, spatial: 0.6f, haptic: true, hk: Haptics.Kind.Heavy, duck: true, maxDist: 50f),
        // Game loop.
        E(SfxId.ComboBreak, "combo_break", 0.9f, 1, ui: true, pitchVar: 0.03f),
        E(SfxId.WaveStart, "explosion", 0.5f, 1, ui: true, pitchVar: 0f, duck: true, haptic: true, hk: Haptics.Kind.Medium),
        E(SfxId.WaveClear, "kill_streak", 0.6f, 1, ui: true, pitchVar: 0f),
        E(SfxId.Pickup, "pickup", 0.8f, 2, ui: true, haptic: true, hk: Haptics.Kind.Light),
        E(SfxId.ActiveReloadPerfect, "combo_up", 0.8f, 1, ui: true, pitchVar: 0f, haptic: true, hk: Haptics.Kind.Medium),
        E(SfxId.ActiveReloadFail, "dry_fire", 1f, 1, spatial: 0f),
        E(SfxId.NewRecord, "new_record_jingle", 0.9f, 1, ui: true, pitchVar: 0f, volVar: 0f),
        // F7 special zombies (spawn cues are wide so they are heard off screen).
        E(SfxId.ShieldSpawn, "shield_roar", 0.95f, 1, spatial: 0.4f, minInterval: 0.5f, pitchVar: 0.05f, duck: true, haptic: true, hk: Haptics.Kind.Medium, maxDist: 60f),
        E(SfxId.ShieldBlock, "shield_ricochet", 0.65f, maxVoices: 3, minInterval: 0.04f, pitchVar: 0.1f, haptic: true, hk: Haptics.Kind.Light),
        E(SfxId.BombSpawn, "bomb_spawn", 0.9f, 1, spatial: 0.4f, minInterval: 0.5f, maxDist: 60f),
        E(SfxId.BombFuse, "bomb_fuse", 1f, 2, spatial: 0.5f, pitchVar: 0.03f, volVar: 0f, haptic: true, hk: Haptics.Kind.Medium),
        E(SfxId.BombExplode, "bomb_burst", 1f, 3, spatial: 0.5f, minInterval: 0.05f, duck: true, haptic: true, hk: Haptics.Kind.Heavy, maxDist: 60f),
        E(SfxId.TongueSpawn, "tongue_spawn", 0.9f, 1, spatial: 0.4f, minInterval: 0.5f, maxDist: 60f),
        E(SfxId.TongueWarn, "tongue_warn", 0.9f, 2, spatial: 0.3f, pitchVar: 0f, haptic: true, hk: Haptics.Kind.Light),
        E(SfxId.TongueLash, "tongue_lash", 0.9f, 2, spatial: 0.5f),
        E(SfxId.TongueGrab, "tongue_grab", 1f, 1, spatial: 0.2f, haptic: true, hk: Haptics.Kind.Heavy, duck: true),
        E(SfxId.TongueSnap, "tongue_snap", 1f, 2, spatial: 0.3f, haptic: true, hk: Haptics.Kind.Medium),
        // F6 roguelite / barrels.
        E(SfxId.LevelUp, "level_up", 0.85f, 1, ui: true, pitchVar: 0f, volVar: 0f, duck: true, haptic: true, hk: Haptics.Kind.Medium),
        E(SfxId.CardPick, "card_pick", 0.85f, 1, ui: true, pitchVar: 0f, haptic: true, hk: Haptics.Kind.Light),
        E(SfxId.Shotgun, "shotgun", 0.75f, 2, spatial: 0.2f, minInterval: 0.1f, haptic: true, hk: Haptics.Kind.Medium),
        E(SfxId.TurretShot, "turret_shot", 0.45f, 3, minInterval: 0.05f),
        E(SfxId.GrenadeThrow, "grenade_throw", 0.7f, 2, spatial: 0.3f),
        E(SfxId.BarrelExplode, "barrel_explode", 1f, 3, spatial: 0.5f, minInterval: 0.05f, duck: true, haptic: true, hk: Haptics.Kind.Heavy, maxDist: 60f),
        // F8 — story objectives
        E(SfxId.Radio, "radio_static", 0.75f, 1, ui: true, pitchVar: 0.04f, minInterval: 0.2f),
        E(SfxId.ObjectiveTick, "objective_tick", 0.55f, 1, ui: true, pitchVar: 0f, volVar: 0f),
        E(SfxId.ObjectiveDone, "objective_done", 0.9f, 1, ui: true, pitchVar: 0f, volVar: 0f, duck: true, haptic: true, hk: Haptics.Kind.Medium),
        E(SfxId.ObjectiveBig, "objective_big", 1f, 1, ui: true, pitchVar: 0f, volVar: 0f, duck: true, haptic: true, hk: Haptics.Kind.Heavy),
        E(SfxId.Helicopter, "heli_rotor", 0.85f, 2, spatial: 0.6f, minInterval: 0.5f, pitchVar: 0.02f, volVar: 0f, maxDist: 80f),
        // F9 boss (Kasap): heard from far away, rumble = heavy haptic + music duck.
        E(SfxId.BossRoar, "boss_roar", 1f, 2, spatial: 0.3f, minInterval: 0.3f, pitchVar: 0.04f, duck: true, haptic: true, hk: Haptics.Kind.Heavy, maxDist: 90f),
        E(SfxId.BossStep, "boss_step", 0.8f, 3, spatial: 0.6f, minInterval: 0.12f, pitchVar: 0.06f, haptic: true, hk: Haptics.Kind.Light, maxDist: 60f),
        E(SfxId.BossCharge, "boss_charge", 1f, 1, spatial: 0.4f, minInterval: 0.3f, pitchVar: 0.03f, haptic: true, hk: Haptics.Kind.Medium, maxDist: 80f),
        E(SfxId.BossSlam, "boss_slam", 1f, 3, spatial: 0.5f, minInterval: 0.06f, pitchVar: 0.05f, duck: true, haptic: true, hk: Haptics.Kind.Heavy, maxDist: 80f),
    };

    // ---------------------------------------------------------------- scene objects
    static void BuildAudio(Transform player)
    {
        AudioRefs refs = null;
        try { refs = CreateMixer(); }
        catch (Exception e) { Debug.LogError("BUILD: mixer creation failed, audio runs without groups: " + e); }

        var go = new GameObject("GameAudio");
        var ga = go.AddComponent<GameAudio>();
        ga.entries = SoundBank();
        ga.heartbeatClip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/SFX/heartbeat.ogg");
        if (refs != null) { ga.mixer = refs.mixer; ga.sfxGroup = refs.sfx; ga.uiGroup = refs.ui; }
        int missing = ga.entries.Count(e => e.clips.Length == 0);
        Debug.Log($"BUILD: sound bank {ga.entries.Length} ids, {ga.entries.Sum(e => e.clips.Length)} clips, {missing} silent ids");

        var music = new GameObject("Music");
        var md = music.AddComponent<MusicDirector>();
        AudioSource Layer(string name, string clip, out AudioLowPassFilter lp)
        {
            var l = new GameObject(name);
            l.transform.SetParent(music.transform, false);
            var s = l.AddComponent<AudioSource>();
            s.clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{AudioDir}/Music/{clip}.ogg");
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.priority = 0;
            if (refs != null) s.outputAudioMixerGroup = refs.music;
            lp = l.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = 22000f;
            return s;
        }
        md.calm = Layer("Calm", "music_calm", out md.calmFilter);
        md.intense = Layer("Intense", "music_intense", out md.intenseFilter);
        md.boss = Layer("Boss", "music_boss", out md.bossFilter);

        var listener = new GameObject("AudioListener");
        listener.AddComponent<AudioListener>();
        listener.AddComponent<ListenerFollow>().target = player;
    }
}
