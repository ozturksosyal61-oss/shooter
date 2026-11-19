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

    static InputField MakeInput(string name, Transform parent, string hintKey, Vector2 pos, Vector2 size)
    {
        var bg = Panel(name, parent, UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, 0f);
        bg.raycastTarget = true;
        var input = bg.gameObject.AddComponent<InputField>();
        var txt = Txt("Text", bg.transform, 64, TextAnchor.MiddleCenter, UiCream);
        Stretch(txt.rectTransform);
        txt.supportRichText = false;
        var ph = Label("Placeholder", bg.transform, 40, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.35f), hintKey);
        Stretch(ph.rectTransform);
        input.textComponent = txt;
        input.placeholder = ph;
        input.targetGraphic = bg;
        input.characterValidation = InputField.CharacterValidation.Alphanumeric;
        return input;
    }

    static CoopPanel BuildCoopPanel(Transform root)
    {
        var cp = new GameObject("Coop").AddComponent<CoopPanel>();
        cp.transform.SetParent(root, false);
        cp.root = Dim("CoopPanel", root, out var p, new Vector2(1300f, 780f));
        var t = Label("Title", p, 60, TextAnchor.MiddleCenter, UiOrange, "coop.title");
        Anchor(t.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1000f, 80f));

        var idle = Rect("Idle", p); Stretch(idle);
        cp.create = MakeButton("Create", idle, "coop.create", UiOrange, new Vector2(0.5f, 0.5f), new Vector2(0f, 150f), new Vector2(620f, 130f), 56, UiInk, 0f);
        cp.codeInput = MakeInput("Code", idle, "coop.code_hint", new Vector2(-150f, -20f), new Vector2(480f, 120f));
        cp.join = MakeButton("Join", idle, "coop.join", UiKhaki, new Vector2(0.5f, 0.5f), new Vector2(260f, -20f), new Vector2(300f, 120f), 50, null, 0f);

        var room = Rect("Room", p); Stretch(room);
        var rc = Label("CodeLabel", room, 34, TextAnchor.MiddleCenter, UiCream, "coop.room_code");
        Anchor(rc.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1000f, 50f));
        cp.codeText = Txt("Code", room, 130, TextAnchor.MiddleCenter, UiOrange);
        Anchor(cp.codeText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 95f), new Vector2(1000f, 160f));
        cp.playersText = Txt("Players", room, 44, TextAnchor.MiddleCenter, UiToxic);
        Anchor(cp.playersText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(800f, 60f));
        cp.start = MakeButton("Start", room, "coop.start", UiOrange, new Vector2(0.5f, 0.5f), new Vector2(190f, -140f), new Vector2(400f, 120f), 56, UiInk, 0f);
        cp.leave = MakeButton("Leave", room, "coop.leave", UiKhakiDark, new Vector2(0.5f, 0.5f), new Vector2(-240f, -140f), new Vector2(320f, 110f), 44, null, 0f);

        cp.status = Txt("Status", p, 32, TextAnchor.MiddleCenter, UiGold);
        Anchor(cp.status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 160f), new Vector2(1200f, 60f));
        cp.back = MakeButton("Back", p, "menu.back", UiKhakiDark, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(320f, 90f), 40, null, 0f);
        cp.idleGroup = idle.gameObject; cp.roomGroup = room.gameObject;
        return cp;
    }
}
