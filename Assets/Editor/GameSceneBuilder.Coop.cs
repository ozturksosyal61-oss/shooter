using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Co-op (C1): NetPlayer prefab, Resources/NetworkManager, the room panel in the menu.</summary>
public static partial class GameSceneBuilder
{
    const string NetPlayerPath = PrefabDir + "/NetPlayer.prefab";
    const string NetManagerPath = "Assets/Resources/NetworkManager.prefab";
    const string NetPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";

    static void BuildNetPrefabs()
    {
        var np = new GameObject("NetPlayer");
        np.AddComponent<NetworkObject>();
        var p = np.AddComponent<NetPlayer>();
        p.avatarPrefab = LoadGo($"{PrefabDir}/Player.prefab");
        var saved = PrefabUtility.SaveAsPrefabAsset(np, NetPlayerPath);
        Object.DestroyImmediate(np);
        // Batchmode save skips Netcode's editor hook: generate the prefab's GlobalObjectIdHash now (it needs the GUID).
        var no = saved.GetComponent<NetworkObject>();
        typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.Invoke(no, null);
        EditorUtility.SetDirty(saved);
        AssetDatabase.SaveAssets();
        if (no.PrefabIdHash == 0) Debug.LogError("BUILD: NetPlayer GlobalObjectIdHash is 0");

        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetPrefabsListPath);
        if (!list)
        {
            list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, NetPrefabsListPath);
        }
        if (!list.Contains(saved)) list.Add(new NetworkPrefab { Prefab = saved });
        EditorUtility.SetDirty(list);

        System.IO.Directory.CreateDirectory("Assets/Resources");
        var go = new GameObject("NetworkManager");
        var transport = go.AddComponent<UnityTransport>();
        var nm = go.AddComponent<NetworkManager>();
        nm.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            PlayerPrefab = saved,
            TickRate = 30,
            EnableSceneManagement = true,
            ConnectionApproval = false,
        };
        nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
        PrefabUtility.SaveAsPrefabAsset(go, NetManagerPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
    }

    static void BuildNetZombies(ZombieSpawner spawner)
    {
        var nz = new GameObject("NetZombies").AddComponent<NetZombies>();
        nz.spawner = spawner;
    }

}
