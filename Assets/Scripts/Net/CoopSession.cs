using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Co-op session (state owner for the room): Unity Gaming Services sign-in, a private Multiplayer session with Relay
/// (6-character join code, 2–4 players), joining by code, starting the run (seed sent to everyone, then the host loads
/// the Game scene through Netcode's scene manager) and leaving. The NetworkManager comes from
/// Resources/NetworkManager (built by the scene builder) and lives across scenes.
/// Tests / LAN debugging can start a direct local host without Relay (<see cref="StartLocalHost"/>).
/// </summary>
public static class CoopSession
{
    public const int MaxPlayers = 4;
    const string StartMessage = "coop.start";

    public static ISession Session { get; private set; }
    public static bool Busy { get; private set; }
    public static string Error { get; private set; }
    public static bool InRoom => NetworkManager.Singleton && !NetworkManager.Singleton.ShutdownInProgress
        && (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsConnectedClient);
    public static bool IsHost => NetworkManager.Singleton && NetworkManager.Singleton.IsHost;
    public static string Code => Session != null ? Session.Code : localCode;
    public static int PlayerCount => NetworkManager.Singleton && NetworkManager.Singleton.IsServer
        ? NetworkManager.Singleton.ConnectedClientsIds.Count
        : Session != null ? Session.PlayerCount : InRoom ? 1 : 0;
    /// <summary>The run has started (Game scene) — set on everyone by the start message.</summary>
    public static bool Started { get; private set; }

    /// <summary>Room / connection changes (UI refresh) and "the host is gone" (back to the menu).</summary>
    public static event Action Changed;
    public static event Action Disconnected;

    static string localCode;
    static bool handlersRegistered;

    public static NetworkManager EnsureManager()
    {
        if (NetworkManager.Singleton) return NetworkManager.Singleton;
        var prefab = Resources.Load<GameObject>("NetworkManager");
        if (!prefab) { Debug.LogError("COOP: Resources/NetworkManager missing (run the scene builder)"); return null; }
        var go = UnityEngine.Object.Instantiate(prefab);
        go.name = "NetworkManager";
        UnityEngine.Object.DontDestroyOnLoad(go);
        var nm = go.GetComponent<NetworkManager>();
        nm.OnClientConnectedCallback += _ => Changed?.Invoke();
        nm.OnClientDisconnectCallback += OnDisconnect;
        return nm;
    }

    static async Task SignIn()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    /// <summary>Creates a private Relay room; the host starts automatically. Returns false (and sets Error) on failure.</summary>
    public static async Task<bool> Create()
    {
        if (Busy) return false;
        Busy = true; Error = null; Changed?.Invoke();
        try
        {
            if (!EnsureManager()) throw new Exception("no NetworkManager");
            await SignIn();
            var options = new SessionOptions { MaxPlayers = MaxPlayers, IsPrivate = true }.WithRelayNetwork();
            Session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Hook();
            Debug.Log($"COOP: room {Session.Code} created");
            return true;
        }
        catch (Exception e) { Fail("create", e); return false; }
        finally { Busy = false; Changed?.Invoke(); }
    }

    public static async Task<bool> Join(string code)
    {
        if (Busy) return false;
        code = (code ?? "").Trim().ToUpperInvariant();
        if (code.Length < 4) { Error = "code"; Changed?.Invoke(); return false; }
        Busy = true; Error = null; Changed?.Invoke();
        try
        {
            if (!EnsureManager()) throw new Exception("no NetworkManager");
            await SignIn();
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            Hook();
            Debug.Log($"COOP: joined {code}");
            return true;
        }
        catch (Exception e) { Fail("join", e); return false; }
        finally { Busy = false; Changed?.Invoke(); }
    }

    /// <summary>Direct local host (no Relay / services): PlayMode tests and LAN debugging.</summary>
    public static bool StartLocalHost(ushort port = 7777)
    {
        var nm = EnsureManager();
        if (!nm) return false;
        nm.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", port);
        if (!nm.StartHost()) return false;
        localCode = "LOCAL";
        Hook();
        Changed?.Invoke();
        return true;
    }

    static void Hook()
    {
        var nm = NetworkManager.Singleton;
        if (Session != null)
        {
            Session.PlayerJoined += _ => Changed?.Invoke();
            Session.PlayerHasLeft += _ => Changed?.Invoke();
        }
        if (handlersRegistered || nm.CustomMessagingManager == null) return;
        nm.CustomMessagingManager.RegisterNamedMessageHandler(StartMessage, OnStartMessage);
        handlersRegistered = true;
    }

    /// <summary>Host: seed to everyone, lock the room, load the Game scene for all.</summary>
    public static void StartGame()
    {
        var nm = NetworkManager.Singleton;
        if (!nm || !nm.IsHost || Started) return;
        int seed = UnityEngine.Random.Range(1, 1_000_000);
        using (var w = new FastBufferWriter(8, Allocator.Temp))
        {
            w.WriteValueSafe(seed);
            nm.CustomMessagingManager.SendNamedMessageToAll(StartMessage, w, NetworkDelivery.ReliableSequenced);
        }
        Begin(seed);
        if (Session != null && Session.IsHost)
        {
            var host = Session.AsHost();
            host.IsLocked = true;
            _ = host.SavePropertiesAsync();
        }
        nm.SceneManager.LoadScene(LoadingScreen.GameScene, LoadSceneMode.Single);
    }

    static void OnStartMessage(ulong sender, FastBufferReader r)
    {
        if (NetworkManager.Singleton.IsServer) return; // the host already began
        r.ReadValueSafe(out int seed);
        Begin(seed);
    }

    static void Begin(int seed)
    {
        Started = true;
        GameMode.Set(RunMode.Coop);
        ChunkManager.SeedOverride = seed;
        Changed?.Invoke();
    }

    /// <summary>Leaves the room (host: ends it for everyone) and returns to single player.</summary>
    public static async Task Leave()
    {
        var s = Session;
        Session = null;
        Reset();
        try { if (s != null) await s.LeaveAsync(); }
        catch (Exception e) { Debug.LogWarning("COOP: leave " + e.Message); }
        Changed?.Invoke();
    }

    static void Reset(bool keepMode = false)
    {
        var nm = NetworkManager.Singleton;
        if (nm)
        {
            if (handlersRegistered && nm.CustomMessagingManager != null) nm.CustomMessagingManager.UnregisterNamedMessageHandler(StartMessage);
            nm.Shutdown();
        }
        handlersRegistered = false;
        Started = false;
        localCode = null;
        // A run that just lost its host stays a co-op run until its result screen leaves (team table, no solo records).
        if (keepMode) return;
        if (GameMode.IsCoop) GameMode.Set(RunMode.Endless);
        ChunkManager.SeedOverride = 0;
        ModeRules.CoopPlayers = 1;
    }

    static void OnDisconnect(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        // A client lost the host (or got kicked): end the session locally.
        if (nm && !nm.IsServer && (clientId == nm.LocalClientId || !nm.IsConnectedClient))
        {
            Session = null;
            Reset(GameManager.I != null);
            Disconnected?.Invoke();
        }
        Changed?.Invoke();
    }

    static void Fail(string what, Exception e)
    {
        Error = what;
        Debug.LogWarning($"COOP: {what} failed — {e.GetType().Name}: {e.Message}");
        Session = null;
        if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
    }
}
