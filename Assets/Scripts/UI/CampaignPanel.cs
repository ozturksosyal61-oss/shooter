using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Son Devriye" chapter select: one card per chapter (number, title, description, objectives, stars), locked until
/// the previous chapter is cleared. Picking one sets <see cref="GameMode"/> and opens the weapon loadout.
/// </summary>
public class CampaignPanel : MonoBehaviour
{
    public GameObject root;
    public Button[] chapterButtons;
    public TMP_Text[] numbers, titles, descs, infos;
    /// <summary>Three per chapter: index = chapter * 3 + star.</summary>
    public Image[] stars;
    public GameObject[] locks;
    public Button back;
    public TMP_Text total;
    public LoadoutPanel loadout;
    public Color open = new Color(0.078f, 0.098f, 0.071f), locked = new Color(0.047f, 0.055f, 0.04f), starOn = new Color(1f, 0.788f, 0.235f);

    void Awake()
    {
        for (int i = 0; i < chapterButtons.Length; i++)
        {
            int c = i;
            chapterButtons[i].onClick.AddListener(() => Pick(c));
        }
        back.onClick.AddListener(() => { Click(); root.SetActive(false); });
        root.SetActive(false);
        Loc.Changed += Refresh;
    }

    void OnDestroy() => Loc.Changed -= Refresh;

    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Open() { root.SetActive(true); Refresh(); }

    void Pick(int c)
    {
        Click();
        if (!Campaign.Unlocked(SaveSystem.Data.campaign, c))
        {
            if (MainMenu.I) MainMenu.I.Toast(Loc.T("camp.locked"));
            return;
        }
        GameMode.Set(RunMode.Campaign, c);
        if (loadout) loadout.Open();
        else LoadingScreen.Load(LoadingScreen.GameScene);
    }

    void Refresh()
    {
        if (!root || !root.activeSelf) return;
        var data = SaveSystem.Data.campaign;
        for (int i = 0; i < chapterButtons.Length && i < Campaign.Count; i++)
        {
            var def = Campaign.Get(i);
            bool unlocked = Campaign.Unlocked(data, i);
            int s = Campaign.StarsOf(data, i);
            numbers[i].text = Loc.F("camp.chapter", i + 1);
            titles[i].text = Loc.T("chapter." + def.key + ".title");
            descs[i].text = unlocked ? Loc.T("chapter." + def.key + ".desc") : Loc.T("camp.locked");
            infos[i].text = Loc.F("camp.goals", def.steps.Length, def.startWave);
            chapterButtons[i].image.color = unlocked ? open : locked;
            if (locks != null && i < locks.Length && locks[i]) locks[i].SetActive(!unlocked);
            for (int k = 0; k < 3; k++)
            {
                var img = stars[i * 3 + k];
                if (img) img.color = k < s ? starOn : new Color(0.204f, 0.251f, 0.173f, 1f);
            }
        }
        if (total) total.text = Loc.F("menu.story_sub", Campaign.TotalStars(data), Campaign.Count * 3);
    }
}
