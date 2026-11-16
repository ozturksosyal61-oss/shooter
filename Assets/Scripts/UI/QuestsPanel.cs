using UnityEngine;
using UnityEngine.UI;

/// <summary>Three daily quests with progress and claim buttons.</summary>
public class QuestsPanel : MonoBehaviour
{
    public GameObject root;
    public Text[] titles, progress;
    public Button[] claims;
    public Button close;

    void Awake()
    {
        for (int i = 0; i < claims.Length; i++) { int s = i; claims[i].onClick.AddListener(() => { if (Profile.ClaimQuest(s)) GameAudio.Play(SfxId.Purchase, Vector3.zero); Refresh(); }); }
        close.onClick.AddListener(() => root.SetActive(false));
        root.SetActive(false);
    }

    public void Open() { root.SetActive(true); Refresh(); }

    public static bool AnyClaimable()
    {
        foreach (var q in Profile.Quests) if (!q.claimed && q.progress >= Economy.QuestPool[q.index].target) return true;
        return false;
    }

    void Refresh()
    {
        var qs = Profile.Quests;
        for (int i = 0; i < titles.Length; i++)
        {
            bool has = i < qs.Count;
            titles[i].transform.parent.gameObject.SetActive(has);
            if (!has) continue;
            var q = qs[i]; var d = Economy.QuestPool[q.index];
            titles[i].text = Loc.F(d.Key, d.target);
            progress[i].text = Loc.F("quest.progress", q.progress, d.target, d.reward);
            bool ready = !q.claimed && q.progress >= d.target;
            claims[i].interactable = ready;
            claims[i].GetComponentInChildren<Text>().text = Loc.T(q.claimed ? "quest.claimed" : "quest.claim");
        }
    }
}
