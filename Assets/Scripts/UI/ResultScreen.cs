using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// End-of-run screens (redesign, docs/ui-ref 13 / 14): death in Endless / Hard (wave, stats, earnings, quest progress,
/// MENÜ / SİLAH / TEKRAR) and Story chapter failed / cleared (chapter path, objective progress, stars, tip,
/// TEKRAR DENE / HARİTA / SİLAH DEĞİŞTİR). Fills itself from the run state; the big restart button is the
/// <see cref="HUD.restartButton"/> rect (TouchControls), moved into the active layout.
/// </summary>
public class ResultScreen : MonoBehaviour
{
    [Header("Death (endless / hard)")]
    public GameObject deathLayout;
    public TMP_Text modeTag, waveBig, prevRecord, statKills, statBosses, statCombo, statTime;
    public Image modeTagBg;
    public GameObject newRecord, hardBonus;
    public TMP_Text[] questTitles, questStates;
    public Image[] questChecks;
    public GameObject[] questRows;
    public RectTransform deathRestartSlot;
    public Button menuButton, weaponButton;

    [Header("Co-op team (replaces the quest list)")]
    public GameObject questBox, teamBox;
    public GameObject[] teamRows;
    public TMP_Text[] teamNames, teamKills, teamScores;
    public TMP_Text teamTotal;

    [Header("Story (failed / cleared)")]
    public GameObject storyLayout;
    public TMP_Text storyKicker, storyTitle, storyChapter, storyGoal, storyPercent, storyStarsHint, storyTip, storyGold;
    public RectTransform storyBar;
    public Image[] storyStars;
    public Image[] nodes;
    public TMP_Text[] nodeLabels;
    public Image[] nodeLines;
    public RectTransform storyRestartSlot;
    public Button mapButton, weaponButton2, storyMenuButton;
    public Sprite closeIcon, checkIcon;

    public bool StoryShown => storyLayout && storyLayout.activeSelf;

    void Awake()
    {
        if (menuButton) menuButton.onClick.AddListener(() => Leave(MainMenu.Open.None));
        if (storyMenuButton) storyMenuButton.onClick.AddListener(() => Leave(MainMenu.Open.None));
        if (weaponButton) weaponButton.onClick.AddListener(() => Leave(MainMenu.Open.Loadout));
        if (weaponButton2) weaponButton2.onClick.AddListener(() => Leave(MainMenu.Open.Loadout));
        if (mapButton) mapButton.onClick.AddListener(() => Leave(MainMenu.Open.Campaign));
    }

    static void Leave(MainMenu.Open open)
    {
        if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero);
        MainMenu.OpenOnStart = open;
        if (GameMode.IsCoop) _ = CoopSession.Leave();
        Time.timeScale = GameManager.TimeScale;
        LoadingScreen.Load(LoadingScreen.MenuScene);
    }

    static void Move(RectTransform button, RectTransform slot)
    {
        if (!button || !slot) return;
        button.SetParent(slot, false);
        button.anchorMin = Vector2.zero; button.anchorMax = Vector2.one;
        button.offsetMin = button.offsetMax = Vector2.zero;
        button.localScale = Vector3.one;
    }

    static string Time2(float t) => $"{(int)(t / 60f):00}:{(int)(t % 60f):00}";

    /// <summary>Endless / Hard death.</summary>
    public void ShowDeath(RectTransform restart, int wave, int kills, int bossKills, int bestCombo, float time, GameManager.RunRecords rec, int previousBestWave)
    {
        deathLayout.SetActive(true);
        storyLayout.SetActive(false);
        Move(restart, deathRestartSlot);
        bool hard = GameMode.IsHard;
        modeTag.text = Loc.T(hard ? "over.mode_hard" : GameMode.IsCoop ? "over.mode_coop" : "over.mode_endless");
        modeTag.color = hard ? UiKit.DangerAlt : UiKit.Success;
        if (modeTagBg) modeTagBg.color = hard ? UiKit.Hex(0x3A1512) : UiKit.Hex(0x1B2A12);
        waveBig.text = Loc.F("over.wave_big", wave);
        bool record = wave > previousBestWave && wave > 0;
        newRecord.SetActive(record || rec.newScore);
        prevRecord.text = previousBestWave > 0 ? Loc.F("over.prev_wave", previousBestWave) : "";
        statKills.text = kills.ToString();
        statBosses.text = bossKills.ToString();
        statCombo.text = "X" + GameManager.MultiplierFor(bestCombo);
        statTime.text = Time2(time);
        if (hardBonus) hardBonus.SetActive(hard);
        bool coop = GameMode.IsCoop;
        if (questBox) questBox.SetActive(!coop);
        if (teamBox) teamBox.SetActive(coop);
        if (coop) ShowTeam();
        if (weaponButton) weaponButton.gameObject.SetActive(!coop);
        var qs = Profile.Quests;
        for (int i = 0; i < questRows.Length; i++)
        {
            bool has = i < qs.Count;
            questRows[i].SetActive(has);
            if (!has) continue;
            var q = qs[i]; var d = Economy.QuestPool[q.index];
            bool done = q.progress >= d.target;
            questTitles[i].text = Loc.F(d.Key, d.target);
            questStates[i].text = done ? Loc.T("over.quest_done") : Mathf.Min(q.progress, d.target) + "/" + d.target;
            questStates[i].color = done ? UiKit.Success : UiKit.Text;
            questChecks[i].color = done ? UiKit.Success : new Color(0f, 0f, 0f, 0f);
            questChecks[i].transform.GetChild(0).gameObject.SetActive(done);
        }
    }

    /// <summary>Co-op: every soldier's kills and score, best first, and the team total.</summary>
    void ShowTeam()
    {
        if (teamRows == null) return;
        var list = new System.Collections.Generic.List<NetPlayer>(NetPlayer.All);
        var gm = GameManager.I;
        int Score(NetPlayer p) => p == NetPlayer.Local && gm ? gm.Score : p.State.Value.score;
        int Kills(NetPlayer p) => p == NetPlayer.Local && gm ? gm.Kills : p.State.Value.kills;
        list.Sort((a, b) => Score(b).CompareTo(Score(a)));
        int total = 0;
        for (int i = 0; i < teamRows.Length; i++)
        {
            bool has = i < list.Count;
            teamRows[i].SetActive(has);
            if (!has) continue;
            var p = list[i];
            bool me = p == NetPlayer.Local;
            teamNames[i].text = CoopTeam.NameOf(p);
            teamNames[i].color = me ? UiKit.Hazard : UiKit.Text;
            teamKills[i].text = Loc.F("coop.kills", Kills(p));
            teamScores[i].text = Score(p).ToString();
        }
        foreach (var p in list) total += Score(p);
        if (list.Count == 0 && gm) { total = gm.Score; teamRows[0].SetActive(true); teamNames[0].text = Loc.T("coop.you"); teamKills[0].text = Loc.F("coop.kills", gm.Kills); teamScores[0].text = gm.Score.ToString(); }
        if (teamTotal) teamTotal.text = Loc.F("coop.team_total", total);
    }

    /// <summary>Story chapter result: failed (died) or cleared (victory).</summary>
    public void ShowStory(RectTransform restart, int chapter, bool won, int stars, float time, int doneSteps, int totalSteps, int gold)
    {
        deathLayout.SetActive(false);
        storyLayout.SetActive(true);
        Move(restart, storyRestartSlot);
        var def = Campaign.Get(chapter);
        storyKicker.text = Loc.F("over.story_kicker", chapter + 1, Campaign.Count);
        storyTitle.text = Loc.T(won ? "victory.title" : "over.story_failed");
        storyTitle.color = won ? UiKit.Gold : UiKit.Text;
        storyChapter.text = "“" + Loc.T("chapter." + def.key + ".title") + "”";
        storyGoal.text = Loc.F("over.story_goal", Loc.T("chapter." + def.key + ".desc"));
        float k = totalSteps > 0 ? Mathf.Clamp01(doneSteps / (float)totalSteps) : 0f;
        if (won) k = 1f;
        storyPercent.text = "%" + Mathf.RoundToInt(k * 100f);
        storyBar.anchorMax = new Vector2(Mathf.Max(0.02f, k), 1f);
        for (int i = 0; i < storyStars.Length; i++) storyStars[i].color = won && i < stars ? UiKit.Gold : UiKit.Line2;
        storyStarsHint.text = won ? Loc.F("victory.stars_hint", Time2(def.parSeconds), Mathf.RoundToInt(Campaign.ThreeStarHealth * 100f)) : Loc.T("over.story_stars_hint");
        storyTip.text = Loc.T("tip." + Random.Range(1, 11));
        storyGold.text = Loc.F(won ? "over.story_gold_won" : "over.story_gold", gold);
        var data = SaveSystem.Data.campaign;
        for (int i = 0; i < nodes.Length; i++)
        {
            bool cleared = Campaign.StarsOf(data, i) > 0;
            bool current = i == chapter;
            nodes[i].color = current && !won ? UiKit.Danger : cleared || (current && won) ? UiKit.Success : UiKit.Card;
            nodeLabels[i].text = current && !won ? "×" : (i + 1).ToString();
            nodeLabels[i].color = cleared || current ? UiKit.Ink : UiKit.Secondary;
            if (current && !won) nodeLabels[i].color = UiKit.Text;
            if (i < nodeLines.Length) nodeLines[i].color = cleared && Campaign.StarsOf(data, i + 1) > 0 || (cleared && i + 1 == chapter) ? UiKit.Success : UiKit.Line;
        }
        if (mapButton) mapButton.gameObject.SetActive(true);
    }
}
