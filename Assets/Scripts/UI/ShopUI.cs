using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop: tabs Weapons / Costumes / Bonuses / Gems; item grid (owned / equipped / locked / price), details with a turning
/// 3D preview, permanent weapon upgrades (damage / magazine / reload, 5 levels), purchase confirmation.
/// Every change goes through <see cref="Profile"/> (gold / gems) or <see cref="Store"/> (real money) / <see cref="Ads"/>.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public enum Tab { Weapons, Costumes, Bonuses, Gems }
    enum Kind { Weapon, Costume, Bonus, Product, GoldOffer, FreeGems }

    struct Item { public Kind kind; public int index; public string product; }

    public GameObject root;
    public Button[] tabButtons;
    public Button close;
    public Text gold, gems;
    public RectTransform grid;
    public Button cardTemplate;
    public PreviewStage preview;
    public RawImage previewImage;
    public Text title, description, stats, actionLabel, message;
    public Button action;
    public GameObject upgradeRoot;
    public Text[] upgradeLabels;
    public Button[] upgradeButtons;
    public Text[] upgradeCosts;
    public GameObject confirm;
    public Text confirmText;
    public Button confirmYes, confirmNo;
    public Color tabOn = new Color(1f, 0.55f, 0.1f), tabOff = new Color(0.17f, 0.2f, 0.11f);
    public Color cardBase = new Color(0.34f, 0.4f, 0.22f), cardSelected = new Color(1f, 0.55f, 0.1f);

    public bool IsOpen => root && root.activeSelf;
    public Tab Current { get; private set; }
    public event Action Closed;

    readonly List<Item> items = new List<Item>();
    readonly List<Button> cards = new List<Button>();
    int selected;
    Action pendingConfirm;
    float messageT;

    void Awake()
    {
        for (int i = 0; i < tabButtons.Length; i++) { var t = (Tab)i; tabButtons[i].onClick.AddListener(() => { Click(); Open(t); }); }
        close.onClick.AddListener(() => { Click(); Close(); });
        action.onClick.AddListener(() => { Click(); DoAction(); });
        for (int i = 0; i < upgradeButtons.Length; i++) { var s = (WeaponStat)i; upgradeButtons[i].onClick.AddListener(() => { Click(); AskUpgrade(s); }); }
        confirmYes.onClick.AddListener(() => { Click(); var a = pendingConfirm; pendingConfirm = null; confirm.SetActive(false); a?.Invoke(); });
        confirmNo.onClick.AddListener(() => { Click(); pendingConfirm = null; confirm.SetActive(false); });
        cardTemplate.gameObject.SetActive(false);
        root.SetActive(false);
        confirm.SetActive(false);
        Profile.Changed += Refresh;
    }

    void OnDestroy() => Profile.Changed -= Refresh;

    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Open(Tab tab, int select = 0)
    {
        root.SetActive(true);
        confirm.SetActive(false);
        Current = tab;
        for (int i = 0; i < tabButtons.Length; i++) tabButtons[i].image.color = i == (int)tab ? tabOn : tabOff;
        items.Clear();
        switch (tab)
        {
            case Tab.Weapons: for (int i = 0; i < Economy.Weapons.Length; i++) items.Add(new Item { kind = Kind.Weapon, index = i }); break;
            case Tab.Costumes: for (int i = 0; i < Economy.Costumes.Length; i++) items.Add(new Item { kind = Kind.Costume, index = i }); break;
            case Tab.Bonuses: for (int i = 0; i < Economy.Bonuses.Length; i++) items.Add(new Item { kind = Kind.Bonus, index = i }); break;
            case Tab.Gems:
                items.Add(new Item { kind = Kind.FreeGems });
                foreach (var p in Economy.Products) items.Add(new Item { kind = Kind.Product, product = p.id });
                for (int i = 0; i < Economy.GoldOffers.Length; i++) items.Add(new Item { kind = Kind.GoldOffer, index = i });
                break;
        }
        BuildCards();
        Select(Mathf.Clamp(select, 0, items.Count - 1));
    }

    public void Close()
    {
        root.SetActive(false);
        if (preview) preview.ShowNone();
        Closed?.Invoke();
    }

    void BuildCards()
    {
        while (cards.Count < items.Count)
        {
            var c = Instantiate(cardTemplate, grid);
            int idx = cards.Count;
            c.onClick.AddListener(() => { Click(); Select(idx); });
            cards.Add(c);
        }
        for (int i = 0; i < cards.Count; i++) cards[i].gameObject.SetActive(i < items.Count);
    }

    public void Select(int i)
    {
        selected = i;
        Refresh();
    }

    // ------------------------------------------------------------------ texts
    string Name(Item it)
    {
        switch (it.kind)
        {
            case Kind.Weapon: return Loc.T(Economy.Weapons[it.index].Key);
            case Kind.Costume: return Loc.T(Economy.Costumes[it.index].Key);
            case Kind.Bonus: return Loc.T(Economy.Bonuses[it.index].Key);
            case Kind.Product: return Loc.T(Economy.Product(it.product).Value.Key);
            case Kind.GoldOffer: return Loc.F("shop.gold_offer", Economy.GoldOffers[it.index].gold);
            default: return Loc.T("shop.free_gems");
        }
    }

    string Badge(Item it)
    {
        switch (it.kind)
        {
            case Kind.Weapon:
                var w = (WeaponId)it.index;
                if (!Profile.Owns(w)) return Loc.F("shop.gold", Economy.Weapon(w).price);
                return Profile.SelectedWeapon == w ? Loc.T("shop.equipped") : Loc.T("shop.owned");
            case Kind.Costume:
                var c = (CostumeId)it.index; var cd = Economy.Costume(c);
                if (!Profile.Owns(c)) return cd.packOnly ? Loc.T("shop.pack_only") : cd.gems > 0 ? Loc.F("shop.gems", cd.gems) : Loc.F("shop.gold", cd.gold);
                return Profile.SelectedCostume == c ? Loc.T("shop.equipped") : Loc.T("shop.owned");
            case Kind.Bonus:
                var b = (BonusId)it.index; int cost = Profile.BonusCost(b);
                return cost < 0 ? Loc.T("shop.max") : Loc.F("shop.gold", cost);
            case Kind.Product:
                var p = Economy.Product(it.product).Value;
                if (p.kind == ProductKind.NonConsumable && (p.vip ? Profile.Vip : Profile.StarterPackOwned)) return Loc.T("shop.owned");
                return Store.Price(p.id);
            case Kind.GoldOffer: return Loc.F("shop.gems", Economy.GoldOffers[it.index].gems);
            default: return Profile.FreeGemAvailable ? Loc.T("shop.watch_ad") : Loc.T("shop.tomorrow");
        }
    }

    void Refresh()
    {
        if (!IsOpen) return;
        gold.text = Profile.Gold.ToString();
        gems.text = Profile.Gems.ToString();
        for (int i = 0; i < items.Count; i++)
        {
            var t = cards[i].GetComponentsInChildren<Text>(true);
            t[0].text = Name(items[i]);
            t[1].text = Badge(items[i]);
            cards[i].image.color = i == selected ? cardSelected : cardBase;
        }
        if (selected < 0 || selected >= items.Count) return;
        var it = items[selected];
        title.text = Name(it);
        upgradeRoot.SetActive(false);
        stats.text = "";
        action.gameObject.SetActive(true);
        action.interactable = true;
        switch (it.kind)
        {
            case Kind.Weapon: DetailWeapon((WeaponId)it.index); break;
            case Kind.Costume: DetailCostume((CostumeId)it.index); break;
            case Kind.Bonus: DetailBonus((BonusId)it.index); break;
            default: DetailGems(it); break;
        }
        if (previewImage) previewImage.enabled = it.kind == Kind.Weapon || it.kind == Kind.Costume;
    }

    void DetailWeapon(WeaponId w)
    {
        var d = Economy.Weapon(w);
        preview.ShowWeapon(w);
        description.text = Loc.T(d.Key + ".desc");
        int ld = Profile.Level(w, WeaponStat.Damage), lm = Profile.Level(w, WeaponStat.Magazine), lr = Profile.Level(w, WeaponStat.Reload);
        stats.text = Loc.F("shop.stats", Mathf.RoundToInt(d.damage * Economy.DamageMul(ld)) + (d.pellets > 1 ? " x" + d.pellets : ""),
            d.fireRate.ToString("0.#"), Economy.Magazine(d, lm), (d.reloadTime * Economy.ReloadMul(lr)).ToString("0.0"));
        bool owned = Profile.Owns(w);
        if (!owned) actionLabel.text = Loc.F("shop.buy_gold", d.price);
        else if (Profile.SelectedWeapon == w) { actionLabel.text = Loc.T("shop.equipped"); action.interactable = false; }
        else actionLabel.text = Loc.T("shop.equip");
        upgradeRoot.SetActive(owned);
        if (!owned) return;
        for (int i = 0; i < 3; i++)
        {
            var s = (WeaponStat)i;
            int l = Profile.Level(w, s);
            int cost = Economy.UpgradeCost(w, s, l);
            upgradeLabels[i].text = Loc.F("shop.up." + s, l, Economy.MaxUpgrade);
            upgradeCosts[i].text = cost < 0 ? Loc.T("shop.max") : Loc.F("shop.gold", cost);
            upgradeButtons[i].interactable = cost >= 0;
        }
    }

    void DetailCostume(CostumeId c)
    {
        var d = Economy.Costume(c);
        preview.ShowCostume(c);
        description.text = Loc.T(d.Key + ".desc");
        if (!Profile.Owns(c))
        {
            if (d.packOnly) { actionLabel.text = Loc.T("shop.pack_only"); action.interactable = false; }
            else actionLabel.text = d.gems > 0 ? Loc.F("shop.buy_gems", d.gems) : Loc.F("shop.buy_gold", d.gold);
        }
        else if (Profile.SelectedCostume == c) { actionLabel.text = Loc.T("shop.equipped"); action.interactable = false; }
        else actionLabel.text = Loc.T("shop.equip");
    }

    void DetailBonus(BonusId b)
    {
        preview.ShowNone();
        description.text = Loc.T(Economy.Bonus(b).Key + ".desc");
        stats.text = b == BonusId.StartCard ? Loc.F("shop.charges", Profile.BonusLevel(b)) : Loc.F("shop.level", Profile.BonusLevel(b), Economy.Bonus(b).maxLevel);
        int cost = Profile.BonusCost(b);
        if (cost < 0) { actionLabel.text = Loc.T("shop.max"); action.interactable = false; }
        else actionLabel.text = Loc.F("shop.buy_gold", cost);
    }

    void DetailGems(Item it)
    {
        preview.ShowNone();
        if (it.kind == Kind.FreeGems)
        {
            description.text = Loc.F("shop.free_gems.desc", Economy.FreeGemAd);
            actionLabel.text = Badge(it);
            action.interactable = Profile.FreeGemAvailable && Ads.Available;
        }
        else if (it.kind == Kind.GoldOffer)
        {
            var o = Economy.GoldOffers[it.index];
            description.text = Loc.F("shop.gold_offer.desc", o.gems, o.gold);
            actionLabel.text = Loc.F("shop.buy_gems", o.gems);
        }
        else
        {
            var p = Economy.Product(it.product).Value;
            description.text = Loc.T(p.Key + ".desc");
            bool owned = p.kind == ProductKind.NonConsumable && (p.vip ? Profile.Vip : Profile.StarterPackOwned);
            actionLabel.text = owned ? Loc.T("shop.owned") : Store.Price(p.id);
            action.interactable = !owned && Store.Ready;
        }
    }

    // ------------------------------------------------------------------ actions
    void Ask(string text, Action yes)
    {
        confirmText.text = text;
        pendingConfirm = yes;
        confirm.SetActive(true);
    }

    void Result(bool ok)
    {
        Message(Loc.T(ok ? "shop.done" : "shop.failed"));
        if (ok) GameAudio.Play(SfxId.Purchase, Vector3.zero);
        Refresh();
    }

    void Message(string s)
    {
        message.text = s;
        messageT = 2f;
    }

    void DoAction()
    {
        if (selected < 0 || selected >= items.Count) return;
        var it = items[selected];
        switch (it.kind)
        {
            case Kind.Weapon:
            {
                var w = (WeaponId)it.index;
                if (Profile.Owns(w)) { Profile.SelectWeapon(w); Refresh(); return; }
                int price = Economy.Weapon(w).price;
                if (!Profile.CanAfford(price, 0)) { Message(Loc.T("shop.no_gold")); return; }
                Ask(Loc.F("shop.confirm_gold", Name(it), price), () => Result(Profile.BuyWeapon(w)));
                return;
            }
            case Kind.Costume:
            {
                var c = (CostumeId)it.index; var d = Economy.Costume(c);
                if (Profile.Owns(c)) { Profile.SelectCostume(c); Refresh(); return; }
                if (!Profile.CanAfford(d.gold, d.gems)) { Message(Loc.T(d.gems > 0 ? "shop.no_gems" : "shop.no_gold")); return; }
                Ask(d.gems > 0 ? Loc.F("shop.confirm_gems", Name(it), d.gems) : Loc.F("shop.confirm_gold", Name(it), d.gold), () => Result(Profile.BuyCostume(c)));
                return;
            }
            case Kind.Bonus:
            {
                var b = (BonusId)it.index; int cost = Profile.BonusCost(b);
                if (cost < 0) return;
                if (!Profile.CanAfford(cost, 0)) { Message(Loc.T("shop.no_gold")); return; }
                Ask(Loc.F("shop.confirm_gold", Name(it), cost), () => Result(Profile.BuyBonus(b)));
                return;
            }
            case Kind.GoldOffer:
            {
                var o = Economy.GoldOffers[it.index];
                if (!Profile.CanAfford(0, o.gems)) { Message(Loc.T("shop.no_gems")); return; }
                Ask(Loc.F("shop.confirm_gems", Name(it), o.gems), () => Result(Profile.BuyGoldWithGems(it.index)));
                return;
            }
            case Kind.FreeGems:
                Ads.ShowRewarded(AdPlacement.FreeGems, ok => Result(ok && Profile.ClaimFreeGems()));
                return;
            case Kind.Product:
                Ask(Loc.F("shop.confirm_iap", Name(it), Store.Price(it.product)), () => { Message(Loc.T("shop.wait")); Store.Buy(it.product, Result); });
                return;
        }
    }

    void AskUpgrade(WeaponStat s)
    {
        var w = (WeaponId)items[selected].index;
        int cost = Economy.UpgradeCost(w, s, Profile.Level(w, s));
        if (cost < 0) return;
        if (!Profile.CanAfford(cost, 0)) { Message(Loc.T("shop.no_gold")); return; }
        Ask(Loc.F("shop.confirm_gold", Loc.F("shop.up." + s, Profile.Level(w, s) + 1, Economy.MaxUpgrade), cost), () => Result(Profile.BuyUpgrade(w, s)));
    }

    /// <summary>Tests: performs the selected item's action without the confirmation dialog.</summary>
    public void ActNow()
    {
        DoAction();
        if (confirm.activeSelf) confirmYes.onClick.Invoke();
    }

    public int IndexOfWeapon(WeaponId w) => Current == Tab.Weapons ? (int)w : -1;

    void Update()
    {
        if (messageT > 0f) { messageT -= Time.unscaledDeltaTime; if (messageT <= 0f) message.text = ""; }
    }
}
