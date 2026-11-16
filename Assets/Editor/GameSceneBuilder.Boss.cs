using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>F9 — first boss "Kasap": import, prefab, telegraph visuals, boss HUD, scene systems.</summary>
public static partial class GameSceneBuilder
{
    const string BossDir = "Assets/Art/Characters/Kasap";
    /// <summary>~2.7× an ordinary 1.8 m zombie.</summary>
    const float BossHeight = 1.8f * 2.7f;

    class BossAssets
    {
        public Avatar avatar;
        public AnimationClip idle, walk, run, roar, slam, dying;
        public Material body, ring, telegraph, strip, eyes;
    }

    static BossAssets ImportBoss(Mats m, Tex t)
    {
        var b = new BossAssets();
        ConfigureCharacterTexture(BaseColor(BossDir));
        // Not optimised: the eye glows hang off the head bone.
        b.avatar = ConfigureRig(Rigged(BossDir), false);
        string A(string f) => $"{BossDir}/Anim/kasap {f}.fbx";
        b.idle = ConfigureClip(A("breathing idle"), b.avatar, "KasapIdle", true, false);
        b.walk = ConfigureClip(A("walking"), b.avatar, "KasapWalk", true, false);
        b.run = ConfigureClip(A("run"), b.avatar, "KasapRun", true, false);
        b.roar = ConfigureClip(A("roaring"), b.avatar, "KasapRoar", false, true);
        b.slam = ConfigureClip(A("jump attack"), b.avatar, "KasapSlam", false, true);
        b.dying = ConfigureClip(A("dying"), b.avatar, "KasapDying", false, true);

        const string simpleLit = "Universal Render Pipeline/Simple Lit";
        b.body = Mat("M_Kasap", simpleLit, x => { x.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColor(BossDir))); x.SetColor("_BaseColor", Color.white); });
        b.ring = Mat("M_Ring_Boss", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.ringTex); x.SetColor("_BaseColor", new Color(1f, 0.12f, 0.08f, 1f)); x.SetFloat("_VertexColor", 0f); });
        b.telegraph = Mat("M_BossSlam", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.ringTex); x.SetColor("_BaseColor", new Color(1f, 0.15f, 0.05f, 1f)); x.SetFloat("_VertexColor", 0f); });
        var stripTex = WriteTex("FX_ChargeStrip", 64, (u, v) =>
        {
            float across = 1f - Mathf.Abs(u - 0.5f) * 2f;
            float edge = Mathf.SmoothStep(0f, 1f, across * 3f);
            float chevron = Mathf.Repeat(v * 7f - Mathf.Abs(u - 0.5f) * 1.6f, 1f) < 0.32f ? 1f : 0.45f;
            float rim = Mathf.Exp(-Mathf.Pow((across - 0.1f) / 0.07f, 2f));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(edge * chevron * 0.75f + rim * 0.6f));
        }, false, TextureWrapMode.Clamp, 64);
        b.strip = Mat("M_BossCharge", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", stripTex); x.SetColor("_BaseColor", new Color(1f, 0.1f, 0.05f, 1f)); x.SetFloat("_VertexColor", 0f); });
        b.eyes = Mat("M_BossEyes", "Game/FxAdditive", x => { x.SetTexture("_BaseMap", t.soft); x.SetColor("_BaseColor", new Color(1f, 0.55f, 0.1f, 1f)); x.SetFloat("_VertexColor", 0f); });
        return b;
    }

    static void BuildBossPrefab(Mats m, BossAssets b)
    {
        var ctrl = BuildMonsterController("Kasap", b.idle, b.walk, b.run, b.roar, b.slam, b.slam, b.idle, b.dying);
        const float radius = 1.15f;
        var k = MonsterPrefab<ButcherZombie>("Kasap", Rigged(BossDir), m, b.body, b.ring, BossHeight, 1f, ctrl, radius, 4.4f, out var go);
        k.kind = ZombieKind.Boss;
        k.ring.localScale = new Vector3(5.6f, 5.6f, 1f); // big red ring
        k.ringRenderer = k.ring.GetComponent<Renderer>();
        k.walkClipSpeed = ClipSpeed(b.walk);
        k.runClipSpeed = ClipSpeed(b.run);
        k.walkSpeedFallback = 2.3f; k.runSpeedFallback = 6f;
        k.dyingLength = b.dying.length;
        k.riseDuration = 1.1f;
        k.screamDuration = Mathf.Min(b.roar.length * 0.85f, 2.6f);
        k.separationRadius = 1.7f; k.lookAhead = 2.6f; k.turnSpeed = 150f; k.contactRange = 2.2f;
        k.deathThrow = 0f;
        k.score = 400; k.xp = 0; // the boss reward (BossRules) is paid by BossDirector
        k.aimBias = 3f;
        k.slamImpactTime = b.slam.length * 0.5f;
        k.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Telegraphs: charge strip (rescaled per frame), slam ring growing inside a fixed danger outline.
        var strip = Quad("ChargeStrip", go.transform, b.strip);
        strip.SetActive(false);
        k.chargeStrip = strip.transform;
        var zone = Quad("SlamZone", go.transform, b.telegraph);
        zone.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        zone.SetActive(false);
        k.slamZone = zone.transform;
        var ring = Quad("SlamRing", go.transform, b.telegraph);
        ring.transform.localPosition = new Vector3(0f, 0.07f, 0f);
        ring.SetActive(false);
        k.slamRing = ring.transform;

        // Eyes: orange glow sprites on the head bone, placed on the red eye texels of the mesh.
        var model = k.model;
        var head = k.animator.GetBoneTransform(HumanBodyBones.Head);
        var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
        var eyes = FindEyes(smr, head, go.transform, out int found);
        var list = new List<Transform>();
        foreach (var e in eyes)
        {
            var q = Quad("EyeGlow", head ? head : model, b.eyes);
            q.transform.position = e;
            float s = 0.32f / q.transform.parent.lossyScale.x;
            q.transform.localScale = new Vector3(s, s, s);
            list.Add(q.transform);
        }
        k.eyes = list.ToArray();
        Debug.Log($"BUILD: kasap walk={k.walkClipSpeed:0.00} run={k.runClipSpeed:0.00} roar={b.roar.length:0.0}s slam={b.slam.length:0.0}s dying={b.dying.length:0.0}s eyes={(found > 0 ? found + " red texels" : "estimated")} head={(head ? head.name : "-")}");

        SetLayer(go, GameLayers.Zombie);
        SavePrefab(go, $"{PrefabDir}/Kasap.prefab");
    }

    /// <summary>World positions of the two eyes: red texels in the head region of the bind-pose mesh (fallback: estimate).</summary>
    static Vector3[] FindEyes(SkinnedMeshRenderer smr, Transform head, Transform root, out int found)
    {
        found = 0;
        Vector3 hp = head ? head.position : root.position + Vector3.up * BossHeight * 0.9f;
        Vector3 fwd = root.forward, right = root.right;
        Vector3[] Estimate() => new[]
        {
            hp + Vector3.up * BossHeight * 0.045f + fwd * BossHeight * 0.06f - right * BossHeight * 0.022f,
            hp + Vector3.up * BossHeight * 0.045f + fwd * BossHeight * 0.06f + right * BossHeight * 0.022f,
        };
        if (!smr || !head) return Estimate();

        // The model is still in its import (bind) pose here: mesh space → world through the renderer's full matrix.
        var mesh = smr.sharedMesh;
        var verts = mesh.vertices;
        var uvs = mesh.uv;
        var m2w = smr.transform.localToWorldMatrix;
        var w = new Vector3[verts.Length];
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < verts.Length; i++) { w[i] = m2w.MultiplyPoint3x4(verts[i]); minY = Mathf.Min(minY, w[i].y); maxY = Mathf.Max(maxY, w[i].y); }
        float kScale = maxY - minY;

        var tex = new Texture2D(2, 2);
        tex.LoadImage(System.IO.File.ReadAllBytes(BaseColor(BossDir)));
        Vector3 l = Vector3.zero, r = Vector3.zero;
        int nl = 0, nr = 0, region = 0;
        float topY = float.MinValue;
        foreach (var v in w) topY = Mathf.Max(topY, v.y);
        for (int i = 0; i < w.Length && uvs.Length == w.Length; i++)
        {
            // Face: the top ~13 % of the body, front half.
            if (w[i].y < topY - BossHeight * 0.13f || Vector3.Dot(w[i] - hp, fwd) < -BossHeight * 0.01f) continue;
            region++;
            var c = tex.GetPixelBilinear(uvs[i].x, uvs[i].y);
            if (c.r < 0.3f || c.r < c.g * 1.8f || c.r < c.b * 1.8f) continue;
            if (Vector3.Dot(w[i] - hp, right) < 0f) { l += w[i]; nl++; } else { r += w[i]; nr++; }
        }
        Object.DestroyImmediate(tex);
        found = nl + nr;
        Debug.Log($"BUILD: kasap eye search region={region} verts, red left={nl} right={nr}, top={topY:0.00} head={hp.y:0.00} meshHeight={kScale:0.00} minY={minY:0.00}");
        if (nl < 2 || nr < 2) { found = 0; return Estimate(); }
        // Slightly in front of the surface so the glow is not swallowed by the head.
        return new[] { l / nl + fwd * 0.06f, r / nr + fwd * 0.06f };
    }

    static void BuildBossSystems(ZombieSpawner spawner)
    {
        spawner.bossPrefab = Load<Zombie>("Kasap");
        var bd = new GameObject("BossDirector").AddComponent<BossDirector>();
        bd.spawner = spawner;
        new GameObject("PropTosser").AddComponent<PropTosser>();
    }

    static void BuildBossHud(HUD hud)
    {
        var root = hud.transform;
        var host = Rect("Boss", root);
        Stretch(host);
        var threats = root.Find("Threats");
        if (threats) host.SetSiblingIndex(threats.GetSiblingIndex() + 1);
        var bh = host.gameObject.AddComponent<BossHUD>();

        // Health bar: top centre, under the wave label.
        var bar = Rect("Bar", host);
        Anchor(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -118f), new Vector2(940f, 96f));
        var bg = bar.gameObject.AddComponent<CanvasGroup>();
        bg.blocksRaycasts = false; bg.interactable = false;
        var frame = Panel("Frame", bar, UiKhakiDark, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(940f, 50f));
        var trough = BarFill("Trough", frame.transform, new Color(0f, 0f, 0f, 0.6f), 8f);
        trough.pivot = new Vector2(0.5f, 0.5f);
        var lagBar = BarFill("Lag", frame.transform, new Color(1f, 1f, 1f, 0.9f), 8f);
        var fill = BarFill("Fill", frame.transform, new Color(0.86f, 0.12f, 0.1f), 8f);
        var shine = Sliced("Shine", fill, ui.flat, new Color(1f, 1f, 1f, 0.2f));
        shine.rectTransform.anchorMin = new Vector2(0f, 0.55f); shine.rectTransform.anchorMax = Vector2.one;
        shine.rectTransform.offsetMin = new Vector2(4f, 0f); shine.rectTransform.offsetMax = new Vector2(-4f, -3f);
        var skull = Img("Skull", frame.transform, ui.skull, new Color(1f, 0.35f, 0.3f));
        Anchor(skull.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(84f, 84f));
        var name = Txt("Name", bar, 34, TextAnchor.LowerLeft, UiCream);
        Anchor(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, 0f), new Vector2(500f, 46f));
        name.text = "KASAP";
        var weak = Txt("Weak", bar, 40, TextAnchor.LowerRight, UiGold);
        Anchor(weak.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 0f), new Vector2(-8f, -44f), new Vector2(300f, 52f));
        L(weak, "boss.weak");
        bh.barGroup = bg; bh.barFill = fill; bh.barLag = lagBar; bh.barFillImage = fill.GetComponent<Image>();
        bh.barName = name; bh.weakTag = weak;

        // Intro title (centre, a little above the soldier).
        var intro = Rect("Intro", host);
        Anchor(intro, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1200f, 260f));
        var ig = intro.gameObject.AddComponent<CanvasGroup>();
        ig.blocksRaycasts = false; ig.interactable = false;
        var title = Txt("Title", intro, 150, TextAnchor.MiddleCenter, new Color(1f, 0.3f, 0.2f));
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1200f, 180f));
        title.text = "KASAP";
        var sub = Txt("Sub", intro, 40, TextAnchor.MiddleCenter, UiCream);
        Anchor(sub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(1200f, 60f));
        bh.introGroup = ig; bh.introTitle = title; bh.introSub = sub;
    }
}
