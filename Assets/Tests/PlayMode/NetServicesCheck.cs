using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// [Explicit] online check that Unity Gaming Services are set up for co-op: anonymous sign-in, a Relay allocation +
/// join code, a private Lobby created and deleted. `-testFilter NetServicesCheck` (needs internet).
/// </summary>
public class NetServicesCheck
{
    [UnityTest, Explicit, Category("Network"), Timeout(120000)]
    public IEnumerator RelayAndLobbyWork()
    {
        var t = Check();
        while (!t.IsCompleted) yield return null;
        if (t.Exception != null) throw t.Exception.InnerException ?? t.Exception;
        Assert.AreEqual("", t.Result, "service failures");
    }

    static async Task<string> Check()
    {
        string fails = "";
        await UnityServices.InitializeAsync(new InitializationOptions().SetEnvironmentName("production"));
        Debug.Log($"NETCHECK: services initialized (project {Application.cloudProjectId})");
        if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
        Debug.Log("NETCHECK: signed in as " + AuthenticationService.Instance.PlayerId);
        try
        {
            var alloc = await RelayService.Instance.CreateAllocationAsync(3);
            string join = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            Debug.Log($"NETCHECK: RELAY OK region={alloc.Region} joinCode={join}");
        }
        catch (Exception e) { fails += "relay "; Debug.LogWarning("NETCHECK: RELAY FAIL " + e.GetType().Name + ": " + e.Message); }
        try
        {
            var lobby = await LobbyService.Instance.CreateLobbyAsync("netcheck", 4, new CreateLobbyOptions { IsPrivate = true });
            Debug.Log($"NETCHECK: LOBBY OK code={lobby.LobbyCode}");
            await LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
        }
        catch (Exception e) { fails += "lobby "; Debug.LogWarning("NETCHECK: LOBBY FAIL " + e.GetType().Name + ": " + e.Message); }
        return fails.Trim();
    }
}
