using TMPro;
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
        // C2 / C3: event hub (blasts, pickups, objectives, boss projectiles…) + team rules (revive, team over).
        nz.gameObject.AddComponent<CoopNet>();
        nz.gameObject.AddComponent<CoopTeam>();
    }

    /// <summary>Co-op HUD: teammate rows (left), down / bleed-out overlay (centre), revive markers, toast.</summary>
    static void BuildCoopHud(Transform root)
    {
        var box = Rect("CoopHUD", root); Fill(box);
        var ch = box.gameObject.AddComponent<CoopHUD>();
        var content = Rect("Content", box); Fill(content);
        ch.root = content.gameObject;

        // Teammates: under the card row (top-left).
        ch.rows = new CoopHUD.Row[3];
        for (int i = 0; i < 3; i++)
        {
            var row = KCard("Mate" + i, content, 12, UiKit.Hex(0x10140E, 0.88f), UiKit.Line); TL(row, Safe, 124f + i * 34f, 214f, 30f);
            var ic = KIcon("Icon", row.transform, kit.user, UiKit.Success, 14f); In(ic, 8f, 8f, 14f, 14f);
            var name = KTxt("Name", row.transform, F.Bold, 13f, UiKit.Text); In(name, 28f, 5f, 80f, 20f);
            var bar = KBar("Hp", row.transform, UiKit.Success, out var trough, 6f); In(trough, 108f, 12f, 96f, 6f);
            bar.anchorMax = new Vector2(1f, 1f);
            var state = KTxt("State", row.transform, F.Bold, 12f, UiKit.DangerAlt, TextAlignmentOptions.Right); In(state, 108f, 2f, 96f, 14f);
            ch.rows[i] = new CoopHUD.Row { root = row.gameObject, name = name, state = state, hpFill = bar, hpImage = bar.GetComponent<Image>() };
            row.gameObject.SetActive(false);
        }

        // Down / out overlay (centre).
        var dn = KCard("Downed", content, 20, UiKit.Hex(0x10140E, 0.9f), UiKit.Hex(0x5A1E18)); CC(dn, 400f, 330f, 480f, 150f);
        ch.downed = dn.gameObject;
        ch.downedTitle = KTxt("Title", dn.transform, F.Display, 30f, UiKit.DangerAlt, TextAlignmentOptions.Center); In(ch.downedTitle, 20f, 14f, 440f, 38f);
        ch.downedTimer = KTxt("Timer", dn.transform, F.Display, 26f, UiKit.Text, TextAlignmentOptions.Right); In(ch.downedTimer, 380f, 16f, 80f, 34f);
        ch.downedSub = KTxt("Sub", dn.transform, F.Semi, 15f, UiKit.Secondary, TextAlignmentOptions.Center); In(ch.downedSub, 20f, 54f, 440f, 22f);
        ch.bleedFill = KBar("Bleed", dn.transform, UiKit.Danger, out var bt, 8f); In(bt, 40f, 88f, 400f, 8f);
        var rr = Rect("Revive", dn.transform); In(rr, 40f, 104f, 400f, 34f);
        var rl = KCaps("Label", rr, 12f, UiKit.Success, "coop.revive_marker", TextAlignmentOptions.Center); In(rl, 0f, 0f, 400f, 16f);
        ch.reviveFill = KBar("Bar", rr, UiKit.Success, out var rt2, 8f); In(rt2, 0f, 20f, 400f, 8f);
        ch.reviveRow = rr.gameObject;
        dn.gameObject.SetActive(false);

        // "Reviving X" (bottom centre).
        var hp = KCard("Helping", content, 16, UiKit.Hex(0x10140E, 0.9f), UiKit.Hex(0x4F7A0C)); CC(hp, 470f, 470f, 340f, 56f);
        ch.helping = hp.gameObject;
        ch.helpingText = KTxt("Text", hp.transform, F.Bold, 16f, UiKit.Success, TextAlignmentOptions.Center); In(ch.helpingText, 10f, 8f, 320f, 20f);
        ch.helpingFill = KBar("Bar", hp.transform, UiKit.Success, out var ht, 8f); In(ht, 30f, 36f, 280f, 8f);
        hp.gameObject.SetActive(false);

        // Markers over downed teammates (positioned in canvas units from the bottom-left).
        var mroot = Rect("Markers", content); Fill(mroot);
        ch.markers = new CoopHUD.Marker[3];
        for (int i = 0; i < 3; i++)
        {
            var m = KImg("Marker" + i, mroot, kit.pill, UiKit.Hex(0x10140E, 0.92f), true, 2f);
            var mr = m.rectTransform; mr.anchorMin = mr.anchorMax = Vector2.zero; mr.pivot = new Vector2(0.5f, 0f); mr.sizeDelta = new Vector2(120f, 40f);
            var ms = KImg("Stroke", m.transform, kit.pillStroke, UiKit.Success, true, 2f); Fill(ms);
            var lab = KTxt("Label", m.transform, F.Display, 15f, UiKit.Success, TextAlignmentOptions.Center); In(lab, 6f, 4f, 108f, 20f);
            var fill = KBar("Bar", m.transform, UiKit.Success, out var mt, 5f); In(mt, 16f, 27f, 88f, 5f);
            ch.markers[i] = new CoopHUD.Marker { root = mr, label = lab, fill = fill };
            m.gameObject.SetActive(false);
        }

        // Toast (top centre, under the wave pill).
        var toast = KImg("Toast", content, kit.pill, UiKit.Hex(0x10140E, 0.94f), true, 2f); TC(toast, 440f, 72f, 400f, 40f);
        var ts = KImg("Stroke", toast.transform, kit.pillStroke, UiKit.Hazard, true, 2f); Fill(ts);
        ch.toast = toast.gameObject.AddComponent<CanvasGroup>();
        ch.toast.blocksRaycasts = false;
        ch.toastText = KTxt("Text", toast.transform, F.Bold, 16f, UiKit.Gold, TextAlignmentOptions.Center); Fill(ch.toastText);
    }

}
