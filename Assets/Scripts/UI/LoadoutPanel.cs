using UnityEngine;
using UnityEngine.UI;

/// <summary>Before an endless run: pick an owned weapon (locked ones jump to the shop), then start.</summary>
public class LoadoutPanel : MonoBehaviour
{
    public GameObject root;
    public Button[] weaponButtons;   // indexed by WeaponId
    public Text[] weaponLabels, weaponStates;
    public Button start, back;
    public Text info;
    public ShopUI shop;
    public Color on = new Color(1f, 0.55f, 0.1f), off = new Color(0.34f, 0.4f, 0.22f), locked = new Color(0.17f, 0.2f, 0.11f);

    void Awake()
    {
        for (int i = 0; i < weaponButtons.Length; i++)
        {
            var w = (WeaponId)i;
            weaponButtons[i].onClick.AddListener(() =>
            {
                Click();
                if (Profile.Owns(w)) Profile.SelectWeapon(w);
                else { shop.Open(ShopUI.Tab.Weapons, (int)w); }
                Refresh();
            });
        }
        start.onClick.AddListener(() => { Click(); LoadingScreen.Load(LoadingScreen.GameScene); });
        back.onClick.AddListener(() => { Click(); root.SetActive(false); });
        root.SetActive(false);
        Profile.Changed += Refresh;
    }

    void OnDestroy() => Profile.Changed -= Refresh;
    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Open() { root.SetActive(true); Refresh(); }

    void Refresh()
    {
        if (!root.activeSelf) return;
        var sel = Profile.SelectedWeapon;
        for (int i = 0; i < weaponButtons.Length; i++)
        {
            var w = (WeaponId)i;
            bool owned = Profile.Owns(w);
            weaponLabels[i].text = Loc.T(Economy.Weapon(w).Key);
            weaponStates[i].text = owned ? (w == sel ? Loc.T("shop.equipped") : Loc.T("loadout.pick")) : Loc.F("shop.gold", Economy.Weapon(w).price);
            weaponButtons[i].image.color = w == sel ? on : owned ? off : locked;
        }
        int cards = Profile.BonusLevel(BonusId.StartCard);
        info.text = cards > 0 ? Loc.F("loadout.start_cards", cards) : "";
    }
}
