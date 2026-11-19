using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Co-op room screen: create a room (shows the 6-character code to share) or join one by code; the host starts the
/// run once a teammate is in. Talks only to <see cref="CoopSession"/>.
/// </summary>
public class CoopPanel : MonoBehaviour
{
    public GameObject root;
    public GameObject idleGroup, roomGroup;
    public Button create, join, start, leave, back;
    public InputField codeInput;
    public Text codeText, playersText, status;

    void Awake()
    {
        create.onClick.AddListener(() => { Click(); _ = CoopSession.Create(); });
        join.onClick.AddListener(() => { Click(); _ = CoopSession.Join(codeInput.text); });
        start.onClick.AddListener(() => { Click(); CoopSession.StartGame(); });
        leave.onClick.AddListener(() => { Click(); _ = CoopSession.Leave(); });
        back.onClick.AddListener(() => { Click(); if (!CoopSession.InRoom) root.SetActive(false); else _ = CoopSession.Leave(); });
        codeInput.characterLimit = 6;
        codeInput.onValueChanged.AddListener(v => { string u = v.ToUpperInvariant(); if (u != v) codeInput.text = u; });
        root.SetActive(false);
        CoopSession.Changed += Refresh;
        CoopSession.Disconnected += OnDisconnected;
        Loc.Changed += Refresh;
    }

    void OnDestroy()
    {
        CoopSession.Changed -= Refresh;
        CoopSession.Disconnected -= OnDisconnected;
        Loc.Changed -= Refresh;
    }

    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Open() { root.SetActive(true); Refresh(); }

    void OnDisconnected()
    {
        if (MainMenu.I) MainMenu.I.Toast(Loc.T("coop.lost"));
        Refresh();
    }

    void Update()
    {
        // Player count changes arrive from the network: poll cheaply while open.
        if (root.activeSelf && CoopSession.InRoom && Time.frameCount % 15 == 0) RefreshCount();
    }

    void RefreshCount()
    {
        int n = CoopSession.PlayerCount;
        playersText.text = Loc.F("coop.players", n, CoopSession.MaxPlayers);
        start.gameObject.SetActive(CoopSession.IsHost);
        start.interactable = n >= 2 && !CoopSession.Started;
    }

    void Refresh()
    {
        if (!root || !root.activeSelf) return;
        bool inRoom = CoopSession.InRoom;
        idleGroup.SetActive(!inRoom);
        roomGroup.SetActive(inRoom);
        create.interactable = join.interactable = !CoopSession.Busy;
        if (inRoom)
        {
            codeText.text = CoopSession.Code ?? "…";
            RefreshCount();
            status.text = CoopSession.IsHost ? Loc.T(CoopSession.PlayerCount >= 2 ? "coop.ready" : "coop.wait") : Loc.T("coop.wait_host");
        }
        else status.text = CoopSession.Busy ? Loc.T("coop.connecting") : CoopSession.Error != null ? Loc.T("coop.err." + CoopSession.Error) : Loc.T("coop.hint");
    }
}
