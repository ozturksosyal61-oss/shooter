using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using Unity.Services.Core;
using UnityEngine;

/// <summary>
/// 1.1.0 cloud save. After Game Center sign-in (<see cref="GameServices"/>), the player signs in to Unity
/// Authentication with their Game Center identity (same account on every device), the cloud copy of the save is
/// pulled and merged (<see cref="SaveMerge"/>: nothing owned is ever lost), and from then on every save write is
/// uploaded a few seconds later (and right away when the app goes to the background). Never blocks the game:
/// offline / not signed in = local save only. Status for the settings panel: <see cref="State"/>.
/// </summary>
public static class CloudSync
{
    public enum Status { Off, Connecting, Synced, Error }
    public static Status State { get; private set; }
    public static DateTime LastSync { get; private set; }
    public static event Action Changed;

    const string Key = "save";
    const float PushDelay = 4f;
    static bool started, pushQueued, pushing, hooked;

    /// <summary>Called once Game Center is signed in (iOS). Safe to call again (manual "sync now").</summary>
    public static async void Begin()
    {
        if (started && State == Status.Connecting) return;
        started = true;
        Set(Status.Connecting);
        try
        {
            if (!await SignIn()) { Set(Status.Off); return; }
            await Pull();
            Hook();
            Set(Status.Synced);
        }
        catch (Exception e)
        {
            Debug.LogWarning("CloudSync: " + e.GetType().Name + " " + e.Message);
            Set(Status.Error);
        }
    }

    static void Set(Status s) { State = s; Changed?.Invoke(); }

    static void Hook()
    {
        if (hooked) return;
        hooked = true;
        SaveSystem.Written += QueuePush;
        Application.focusChanged += f => { if (!f) PushNow(); };
    }

    // ---------------------------------------------------------------- sign-in (Game Center identity)
    static async Task<bool> SignIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
        var auth = AuthenticationService.Instance;
        var id = await GameCenterIdentity();
        if (id == null) return false;
        // A co-op room may be using the anonymous sign-in right now: don't pull the rug, try again next launch.
        if (auth.IsSignedIn && CoopSession.InRoom) return false;
        if (auth.IsSignedIn) auth.SignOut(true);
        await auth.SignInWithAppleGameCenterAsync(id.signature, id.teamPlayerId, id.publicKeyUrl, id.salt, id.Timestamp);
        return auth.IsSignedIn;
    }

    [Serializable]
    class GcIdentity
    {
        public string publicKeyUrl, signature, salt, timestamp, teamPlayerId, error;
        public ulong Timestamp => ulong.TryParse(timestamp, out var t) ? t : 0;
    }

#if UNITY_IOS && !UNITY_EDITOR
    delegate void IdCallback(string json);
    [DllImport("__Internal")] static extern void _HoldoutGameCenterIdentity(IdCallback cb);
    static TaskCompletionSource<GcIdentity> pendingId;

    [AOT.MonoPInvokeCallback(typeof(IdCallback))]
    static void OnIdentity(string json)
    {
        var id = JsonUtility.FromJson<GcIdentity>(json);
        if (id != null && !string.IsNullOrEmpty(id.error)) { Debug.Log("CloudSync: Game Center identity — " + id.error); id = null; }
        pendingId?.TrySetResult(id);
    }

    static Task<GcIdentity> GameCenterIdentity()
    {
        pendingId = new TaskCompletionSource<GcIdentity>();
        _HoldoutGameCenterIdentity(OnIdentity);
        return pendingId.Task;
    }
#else
    static Task<GcIdentity> GameCenterIdentity() => Task.FromResult<GcIdentity>(null);
#endif

    // ---------------------------------------------------------------- pull / merge / push
    static async Task Pull()
    {
        var got = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { Key });
        SaveData cloud = null;
        if (got.TryGetValue(Key, out var item)) cloud = SaveSystem.Deserialize(item.Value.GetAs<string>());
        var local = SaveSystem.Data;
        var merged = SaveMerge.Merge(local, cloud);
        SaveSystem.Replace(merged);   // writes locally → queues the upload of the merged save
        LastSync = DateTime.UtcNow;
        await Push();
    }

    static void QueuePush()
    {
        if (State != Status.Synced || pushQueued) return;
        pushQueued = true;
        ServiceRunner.Run(After(PushDelay, () => { pushQueued = false; PushNow(); }));
    }

    static System.Collections.IEnumerator After(float s, Action a) { yield return new WaitForSecondsRealtime(s); a(); }

    static async void PushNow()
    {
        if (State != Status.Synced && State != Status.Connecting) return;
        try { await Push(); }
        catch (Exception e) { Debug.LogWarning("CloudSync: push " + e.Message); }
    }

    static async Task Push()
    {
        if (pushing || !AuthenticationService.Instance.IsSignedIn) return;
        pushing = true;
        try
        {
            await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { Key, SaveSystem.CurrentText() } });
            LastSync = DateTime.UtcNow;
            Changed?.Invoke();
        }
        finally { pushing = false; }
    }
}
