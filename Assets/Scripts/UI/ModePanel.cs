using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Game-mode screen (redesign, docs/ui-ref 02), opened by OYNA: Story / Endless / Hard / Co-op cards with live data
/// (chapter progress + stars, best wave, hard-mode tags, 2–4 players); tapping a card selects it (yellow frame +
/// primary button), BAŞLA starts that mode (Co-op → room screen).
/// </summary>
public class ModePanel : MonoBehaviour
{
    public GameObject root;
    public Button back;
    public TMP_Text gold, gems;
    public const int Coop = 3;
    public Button[] cards;            // story, endless, hard, co-op
    public Button[] starts;
    public GameObject[] selectedFrames;
    public TMP_Text storyProgress, storyStars, endlessRecord, hardRecord, storyTag;
    public MainMenu menu;

    int selected = 1;

    void Awake()
    {
        back.onClick.AddListener(() => { Click(); root.SetActive(false); });
        for (int i = 0; i < cards.Length; i++)
        {
            int m = i;
            cards[i].onClick.AddListener(() => { Click(); Select(m); });
            starts[i].onClick.AddListener(() => { Click(); Select(m); Start(m); });
        }
        root.SetActive(false);
        Loc.Changed += Refresh;
    }

    void OnDestroy() => Loc.Changed -= Refresh;

    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Open()
    {
        selected = MainMenu.ModeIndex;
        root.SetActive(true);
        Refresh();
    }

    void Select(int m)
    {
        selected = m;
        if (m != Coop) MainMenu.SetMode(m);
        Refresh();
    }

    void Start(int m)
    {
        if (!menu) return;
        if (m == Coop) { if (menu.coopPanel) menu.coopPanel.Open(); }
        else if (m == 0) { root.SetActive(false); menu.campaignPanel.Open(); }
        else menu.StartMode(m == 2 ? RunMode.Hard : RunMode.Endless);
    }

    void Refresh()
    {
        if (!root.activeSelf) return;
        var d = SaveSystem.Data;
        gold.text = d.wallet.gold.ToString();
        gems.text = d.wallet.gems.ToString();
        int cleared = 0;
        for (int i = 0; i < Campaign.Count; i++) if (Campaign.StarsOf(d.campaign, i) > 0) cleared++;
        storyProgress.text = Loc.F("mode.chapter_progress", Mathf.Min(cleared + 1, Campaign.Count), Campaign.Count);
        storyStars.text = Loc.F("mode.stars", Campaign.TotalStars(d.campaign), Campaign.Count * 3);
        storyTag.text = Loc.F("mode.chapters_tag", Campaign.Count);
        endlessRecord.text = d.records.bestWave > 0 ? Loc.F("mode.wave", d.records.bestWave) : "—";
        if (hardRecord) hardRecord.text = d.records.hardWave > 0 ? Loc.F("mode.wave", d.records.hardWave) : "";
        for (int i = 0; i < cards.Length; i++)
        {
            selectedFrames[i].SetActive(i == selected);
            var pb = starts[i].GetComponent<PressButton>();
            if (pb) pb.SetStyle(i == selected ? UiStyle.Yellow : UiStyle.Dark);
        }
    }
}
