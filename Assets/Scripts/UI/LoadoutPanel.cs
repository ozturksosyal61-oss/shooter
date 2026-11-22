using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Weapon select before a run (redesign, docs/ui-ref 03): detail panel (3D preview, name, description, stat bars) and
/// the weapon list (equipped / equip / price → shop). GELİŞTİR opens the shop on that weapon, BAŞLA starts the run.
/// </summary>
public class LoadoutPanel : MonoBehaviour
{
    public GameObject root;
    public Button back, upgrade, start;
    public TMP_Text modeLabel, gold, gems;
    public Button[] rows;                 // by WeaponId
    public Image[] rowFrames, rowIcons;
    public GameObject[] rowSelected, rowEquipped;
    public TMP_Text[] rowNames, rowSubs;
    public Button[] rowActions;
    public GameObject equippedTag;
    public RawImage previewImage;
    public PreviewStage preview;
    public TMP_Text detailName, detailDesc;
    public RectTransform[] statBars;      // damage, shots/s, magazine, reload
    public TMP_Text[] statValues;
    public ShopUI shop;
    public Sprite coinIcon, lockIcon, crossIcon;

    int selected;

    void Awake()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            var w = (WeaponId)i;
            rows[i].onClick.AddListener(() => { Click(); selected = (int)w; Refresh(); });
            rowActions[i].onClick.AddListener(() =>
            {
                Click();
                selected = (int)w;
                if (Profile.Owns(w)) Profile.SelectWeapon(w);
                else shop.Open(ShopUI.Tab.Weapons, (int)w);
                Refresh();
            });
        }
        upgrade.onClick.AddListener(() => { Click(); shop.Open(ShopUI.Tab.Weapons, selected); });
        start.onClick.AddListener(() => { Click(); LoadingScreen.Load(LoadingScreen.GameScene); });
        back.onClick.AddListener(() => { Click(); Close(); });
        root.SetActive(false);
        Profile.Changed += Refresh;
        Loc.Changed += Refresh;
        if (shop) shop.Closed += Refresh;
    }

    void OnDestroy() { Profile.Changed -= Refresh; Loc.Changed -= Refresh; if (shop) shop.Closed -= Refresh; }
    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Open()
    {
        selected = (int)Profile.SelectedWeapon;
        root.SetActive(true);
        Refresh();
    }

    void Close()
    {
        root.SetActive(false);
        if (preview) preview.ShowCostume(Profile.SelectedCostume);
    }

    static float Max(System.Func<WeaponDef, float> f)
    {
        float m = 0.01f;
        foreach (var d in Economy.Weapons) m = Mathf.Max(m, f(d));
        return m;
    }

    void Refresh()
    {
        if (!root.activeSelf) return;
        gold.text = Profile.Gold.ToString();
        gems.text = Profile.Gems.ToString();
        string mode = GameMode.IsHard ? "menu.hard" : GameMode.IsCampaign ? "menu.story" : "menu.endless";
        modeLabel.text = Loc.F("loadout.mode", Loc.T(mode));
        var sel = Profile.SelectedWeapon;
        for (int i = 0; i < rows.Length; i++)
        {
            var w = (WeaponId)i; var d = Economy.Weapon(w);
            bool owned = Profile.Owns(w), eq = w == sel;
            rowNames[i].text = Loc.T(d.Key);
            rowSubs[i].text = eq ? Loc.T(d.Key + ".type") : owned ? Loc.T("shop.owned") : Loc.T(d.Key + ".short");
            rowSelected[i].SetActive(i == selected);
            rowFrames[i].color = i == selected ? UiKit.Hex(0x3A2C0E) : UiKit.Card;
            rowEquipped[i].SetActive(eq);
            rowIcons[i].sprite = owned ? crossIcon : lockIcon;
            rowIcons[i].color = eq ? UiKit.Hazard : UiKit.Disabled;
            rowActions[i].gameObject.SetActive(!eq);
            var pb = rowActions[i].GetComponent<PressButton>();
            if (pb)
            {
                pb.SetLabel(owned ? Loc.T("shop.equip") : d.price.ToString());
                if (pb.icon) { pb.icon.gameObject.SetActive(!owned); pb.icon.sprite = coinIcon; }
            }
        }
        var wid = (WeaponId)selected;
        var def = Economy.Weapon(wid);
        if (preview) preview.ShowWeapon(wid);
        equippedTag.SetActive(wid == sel);
        detailName.text = Loc.T(def.Key);
        detailDesc.text = Loc.T(def.Key + ".desc");
        int ld = Profile.Level(wid, WeaponStat.Damage), lm = Profile.Level(wid, WeaponStat.Magazine), lr = Profile.Level(wid, WeaponStat.Reload);
        float dmg = def.damage * Economy.DamageMul(ld) * Mathf.Max(1, def.pellets);
        int mag = Economy.Magazine(def, lm);
        float rel = def.reloadTime * Economy.ReloadMul(lr);
        float[] k =
        {
            dmg / Max(x => x.damage * Mathf.Max(1, x.pellets) * Economy.DamageMul(Economy.MaxUpgrade)),
            def.fireRate / Max(x => x.fireRate),
            mag / Max(x => Economy.Magazine(x, Economy.MaxUpgrade)),
            Mathf.Min(1f, 0.8f / Mathf.Max(0.1f, rel)),
        };
        statValues[0].text = Mathf.RoundToInt(def.damage * Economy.DamageMul(ld)) + (def.pellets > 1 ? "×" + def.pellets : "");
        statValues[1].text = def.fireRate.ToString("0.#");
        statValues[2].text = mag.ToString();
        statValues[3].text = Loc.F("unit.seconds", rel.ToString("0.0"));
        for (int i = 0; i < 4; i++) statBars[i].anchorMax = new Vector2(Mathf.Clamp(k[i], 0.06f, 1f), 1f);
    }
}
