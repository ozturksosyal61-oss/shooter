using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shop (redesign, docs/ui-ref 04–07): sidebar tabs Weapons / Characters / Costumes / Bonuses / Gems + MENÜ.
/// Weapons: list + detail (3D preview, stats, permanent upgrades). Characters (soldier + whole other characters, gems)
/// and Costumes (soldier skins): cards (selected one shows the live 3D preview). Bonuses: rows with level pips.
/// Gems: free daily gems (rewarded ad), TRY store packs ("99,99 TL — 150 ELMAS"), gold offers.
/// Every change goes through <see cref="Profile"/> (gold / gems), <see cref="Store"/> (real money) or <see cref="Ads"/>.
/// </summary>
public class ShopUI : MonoBehaviour
{
    public enum Tab { Weapons, Characters, Costumes, Bonuses, Gems }
    enum Kind { Weapon, Costume, Bonus, Product, GoldOffer, FreeGems }

    struct Item { public Kind kind; public int index; public string product; public bool character; }

    [Serializable]
    public class Card
    {
        public Button button;
        public Image frame, selected;
        public TMP_Text title, sub, tag;
        public Image tagBg, icon;
        public RawImage preview;
        public Button action;
    }

    public GameObject root;
    public Button close;
    public TMP_Text gold, gems;
    public Button goldPlus, gemPlus;
    public Button[] tabButtons;
    public GameObject freeChip;
    public GameObject[] panes;          // by Tab (Characters + Costumes share the card pane)
    public PreviewStage preview;
    public RawImage previewImage;       // weapon detail preview (the character cards get the same texture)

    [Header("Weapons")]
    public Card[] weaponRows;
    public TMP_Text title, typeTag, description;
    public TMP_Text[] statValues;       // damage, shots/s, magazine, reload
    public Image[] upgradePips;         // 3 × 5
    public TMP_Text[] upgradeLevels;
    public Button[] upgradeButtons;
    public Button action;               // buy / equip (weapon detail)
    public GameObject equippedMark;

    [Header("Characters")]
    public RectTransform characterContent;
    public Card characterTemplate;

    [Header("Bonuses")]
    public Card[] bonusRows;
    public Image[] bonusPips;           // 3 × 3
    public TMP_Text[] bonusNext, bonusMissing;

    [Header("Gems")]
    public Button freeButton;
    public TMP_Text freeDesc, priceNote;
    public RectTransform productContent;
    public Card productTemplate;

    [Header("Confirm + message")]
    public GameObject confirm;
    public TMP_Text confirmText, message;
    public Button confirmYes, confirmNo;

    public bool IsOpen => root && root.activeSelf;
    public int ItemCount => items.Count;
    public Tab Current { get; private set; }
    public event Action Closed;

    readonly List<Item> items = new List<Item>();
    readonly List<Card> characterCards = new List<Card>();
    readonly List<Card> productCards = new List<Card>();
    readonly List<Item> productItems = new List<Item>();
    int selected;
    // Shop thumbnails: one still per costume (all cards show their character; the selected one turns live).
    readonly Dictionary<int, RenderTexture> thumbs = new Dictionary<int, RenderTexture>();
    WeaponId thumbWeapon = (WeaponId)(-1);
    Action pendingConfirm;
    float messageT;

    void Awake()
    {
        for (int i = 0; i < tabButtons.Length; i++) { var t = (Tab)i; tabButtons[i].onClick.AddListener(() => { Click(); Open(t); }); }
        close.onClick.AddListener(() => { Click(); Close(); });
        if (goldPlus) goldPlus.onClick.AddListener(() => { Click(); Open(Tab.Gems, 1 + Economy.Products.Length); });
        if (gemPlus) gemPlus.onClick.AddListener(() => { Click(); Open(Tab.Gems, 1); });
        action.onClick.AddListener(() => { Click(); DoAction(); });
        for (int i = 0; i < upgradeButtons.Length; i++) { var s = (WeaponStat)i; upgradeButtons[i].onClick.AddListener(() => { Click(); AskUpgrade(s); }); }
        for (int i = 0; i < weaponRows.Length; i++) { int w = i; weaponRows[i].button.onClick.AddListener(() => { Click(); Select(w); }); }
        for (int i = 0; i < bonusRows.Length; i++) { int b = i; bonusRows[i].action.onClick.AddListener(() => { Click(); Select(b); DoAction(); }); }
        freeButton.onClick.AddListener(() => { Click(); Select(0); DoAction(); });
        confirmYes.onClick.AddListener(() => { Click(); var a = pendingConfirm; pendingConfirm = null; confirm.SetActive(false); a?.Invoke(); });
        confirmNo.onClick.AddListener(() => { Click(); pendingConfirm = null; confirm.SetActive(false); });

        // Character / costume cards (pool; each tab shows its own subset).
        characterTemplate.button.gameObject.SetActive(false);
        for (int i = 0; i < Economy.Costumes.Length; i++)
        {
            var c = Clone(characterTemplate, characterContent);
            int idx = i;
            c.button.onClick.AddListener(() => { Click(); Select(idx); });
            c.action.onClick.AddListener(() => { Click(); Select(idx); DoAction(); });
            characterCards.Add(c);
        }
        // Store packs + gold offers.
        productTemplate.button.gameObject.SetActive(false);
        foreach (var p in Economy.Products) productItems.Add(new Item { kind = Kind.Product, product = p.id });
        for (int i = 0; i < Economy.GoldOffers.Length; i++) productItems.Add(new Item { kind = Kind.GoldOffer, index = i });
        for (int i = 0; i < productItems.Count; i++)
        {
            var c = Clone(productTemplate, productContent);
            int idx = i + 1; // item 0 = free gems
            c.action.onClick.AddListener(() => { Click(); Select(idx); DoAction(); });
            c.button.onClick.AddListener(() => { Click(); Select(idx); });
            productCards.Add(c);
        }
        root.SetActive(false);
        confirm.SetActive(false);
        Profile.Changed += Refresh;
        Loc.Changed += Refresh;
    }

    void OnDestroy()
    {
        Profile.Changed -= Refresh; Loc.Changed -= Refresh;
        foreach (var t in thumbs.Values) if (t) { t.Release(); Destroy(t); }
        thumbs.Clear();
    }

    /// <summary>Thumbnail key + what it shows (the soldier card follows its current skin).</summary>
    static int ThumbKey(Item it, out CostumeId shown)
    {
        var c = (CostumeId)it.index;
        if (it.character && !Economy.IsCharacter(c)) { shown = Profile.SoldierSkin; return 200 + (int)shown; }
        shown = c;
        return it.character ? 100 + it.index : it.index;
    }

    /// <summary>Stills for the current tab's cards (cached; re-rendered when the held weapon changed).</summary>
    void EnsureThumbs()
    {
        if (!preview) return;
        var w = Profile.SelectedWeapon;
        bool redo = w != thumbWeapon, any = false;
        foreach (var it in items)
        {
            if (it.kind != Kind.Costume) continue;
            int key = ThumbKey(it, out var c);
            if (thumbs.TryGetValue(key, out var rt) && rt && !redo) continue;
            if (!rt) { rt = new RenderTexture(252, 300, 24) { name = "Thumb" + key }; thumbs[key] = rt; }
            any |= preview.Snapshot(c, w, rt);
        }
        thumbWeapon = w;
        if (!any) return;
        if (selected >= 0 && selected < items.Count) { ThumbKey(items[selected], out var sc); preview.ShowCostume(sc); }
    }

    RenderTexture ThumbOf(Item it) => thumbs.TryGetValue(ThumbKey(it, out _), out var rt) ? rt : null;

    static Card Clone(Card t, Transform parent)
    {
        var go = Instantiate(t.button.gameObject, parent);
        go.SetActive(true);
        var c = new Card { button = go.GetComponent<Button>() };
        Transform F(Component x) => x ? go.transform.Find(Path(t.button.transform, x.transform)) : null;
        c.frame = F(t.frame)?.GetComponent<Image>();
        c.selected = F(t.selected)?.GetComponent<Image>();
        c.title = F(t.title)?.GetComponent<TMP_Text>();
        c.sub = F(t.sub)?.GetComponent<TMP_Text>();
        c.tag = F(t.tag)?.GetComponent<TMP_Text>();
        c.tagBg = F(t.tagBg)?.GetComponent<Image>();
        c.icon = F(t.icon)?.GetComponent<Image>();
        c.preview = F(t.preview)?.GetComponent<RawImage>();
        c.action = F(t.action)?.GetComponent<Button>();
        return c;
    }

    static string Path(Transform root, Transform t) => t == root || t.parent == null ? "" : (t.parent == root ? t.name : Path(root, t.parent) + "/" + t.name);

    static void Click() { if (GameAudio.I) GameAudio.Play(SfxId.UiClick, Vector3.zero); }

    public void Open(Tab tab, int select = 0)
    {
        root.SetActive(true);
        confirm.SetActive(false);
        Current = tab;
        for (int i = 0; i < tabButtons.Length; i++) Style(tabButtons[i], i == (int)tab ? UiStyle.Yellow : UiStyle.Ghost);
        var pane = (int)tab < panes.Length ? panes[(int)tab] : null;
        foreach (var p in panes) if (p && p != pane) p.SetActive(false);
        if (pane) pane.SetActive(true);
        items.Clear();
        switch (tab)
        {
            case Tab.Weapons: for (int i = 0; i < Economy.Weapons.Length; i++) items.Add(new Item { kind = Kind.Weapon, index = i }); break;
            case Tab.Characters:
                items.Add(new Item { kind = Kind.Costume, index = (int)CostumeId.Standard, character = true });
                for (int i = 0; i < Economy.Costumes.Length; i++)
                    if (Economy.IsCharacter((CostumeId)i)) items.Add(new Item { kind = Kind.Costume, index = i, character = true });
                break;
            case Tab.Costumes:
                for (int i = 0; i < Economy.Costumes.Length; i++)
                    if (!Economy.IsCharacter((CostumeId)i)) items.Add(new Item { kind = Kind.Costume, index = i });
                break;
            case Tab.Bonuses: for (int i = 0; i < Economy.Bonuses.Length; i++) items.Add(new Item { kind = Kind.Bonus, index = i }); break;
            case Tab.Gems:
                items.Add(new Item { kind = Kind.FreeGems });
                items.AddRange(productItems);
                break;
        }
        if (tab != Tab.Weapons && tab != Tab.Characters && tab != Tab.Costumes && preview) preview.ShowNone();
        Select(Mathf.Clamp(select, 0, items.Count - 1));
    }

    public void Close()
    {
        root.SetActive(false);
        if (preview) preview.ShowNone();
        Closed?.Invoke();
    }

    public void Select(int i)
    {
        selected = i;
        Refresh();
    }

    static void Style(Button b, UiStyle s) { var pb = b ? b.GetComponent<PressButton>() : null; if (pb) pb.SetStyle(s); }
    static void SetLabel(Button b, string text) { var pb = b ? b.GetComponent<PressButton>() : null; if (pb) pb.SetLabel(text); }

    static void SetButton(Button b, UiStyle s, string text, bool interactable = true, Sprite icon = null)
    {
        if (!b) return;
        b.interactable = interactable;
        var pb = b.GetComponent<PressButton>();
        if (!pb) return;
        pb.SetStyle(s);
        pb.SetLabel(text);
        if (pb.icon)
        {
            pb.icon.gameObject.SetActive(icon != null);
            if (icon) pb.icon.sprite = icon;
        }
    }

    public Sprite coinIcon, gemIcon, checkIcon, lockIcon, userIcon, heartIcon, cardIcon, goldIcon;

    // ------------------------------------------------------------------ texts
    string Name(Item it)
    {
        switch (it.kind)
        {
            case Kind.Weapon: return Loc.T(Economy.Weapons[it.index].Key);
            case Kind.Costume: return Loc.T(it.character && !Economy.IsCharacter((CostumeId)it.index) ? "menu.soldier_name" : Economy.Costumes[it.index].Key);
            case Kind.Bonus: return Loc.T(Economy.Bonuses[it.index].Key);
            case Kind.Product: return Loc.T(Economy.Product(it.product).Value.Key);
            case Kind.GoldOffer: return Loc.F("shop.gold_offer", Economy.GoldOffers[it.index].gold);
            default: return Loc.T("shop.free_gems");
        }
    }

    void Refresh()
    {
        if (!IsOpen) return;
        gold.text = Profile.Gold.ToString();
        gems.text = Profile.Gems.ToString();
        if (freeChip) freeChip.SetActive(Profile.FreeGemAvailable);
        if (selected < 0 || selected >= items.Count) return;
        switch (Current)
        {
            case Tab.Weapons: RefreshWeapons(); break;
            case Tab.Characters:
            case Tab.Costumes: RefreshCharacters(); break;
            case Tab.Bonuses: RefreshBonuses(); break;
            default: RefreshGems(); break;
        }
    }

    // ------------------------------------------------------------------ weapons
    void RefreshWeapons()
    {
        for (int i = 0; i < weaponRows.Length; i++)
        {
            var w = (WeaponId)i; var r = weaponRows[i];
            r.title.text = Loc.T(Economy.Weapon(w).Key);
            bool owned = Profile.Owns(w), eq = Profile.SelectedWeapon == w;
            r.sub.text = eq ? Loc.T("shop.equipped") : owned ? Loc.T("shop.owned") : Economy.Weapon(w).price.ToString();
            r.sub.color = eq ? UiKit.Hazard : owned ? UiKit.Success : UiKit.Text;
            r.sub.margin = new Vector4(owned ? 0f : 20f, 0f, 0f, 0f);
            if (r.icon) r.icon.gameObject.SetActive(!owned);
            if (r.selected) r.selected.gameObject.SetActive(i == selected);
            if (r.frame) r.frame.color = i == selected ? UiKit.Hex(0x2A2210) : UiKit.Card;
        }
        var wid = (WeaponId)selected;
        var d = Economy.Weapon(wid);
        preview.ShowWeapon(wid);
        previewImage.enabled = true;
        title.text = Loc.T(d.Key);
        typeTag.text = Loc.T(d.Key + ".type");
        description.text = Loc.T(d.Key + ".desc");
        int ld = Profile.Level(wid, WeaponStat.Damage), lm = Profile.Level(wid, WeaponStat.Magazine), lr = Profile.Level(wid, WeaponStat.Reload);
        statValues[0].text = Mathf.RoundToInt(d.damage * Economy.DamageMul(ld)) + (d.pellets > 1 ? "×" + d.pellets : "");
        statValues[1].text = d.fireRate.ToString("0.#");
        statValues[2].text = Economy.Magazine(d, lm).ToString();
        statValues[3].text = Loc.F("unit.seconds", (d.reloadTime * Economy.ReloadMul(lr)).ToString("0.0"));
        bool owned2 = Profile.Owns(wid);
        bool equipped = owned2 && Profile.SelectedWeapon == wid;
        if (equippedMark) equippedMark.SetActive(equipped);
        action.gameObject.SetActive(!equipped);
        if (!owned2) SetButton(action, UiStyle.Yellow, d.price.ToString(), true, coinIcon);
        else SetButton(action, UiStyle.Dark, Loc.T("shop.equip"));
        for (int i = 0; i < 3; i++)
        {
            var s = (WeaponStat)i;
            int l = Profile.Level(wid, s);
            int cost = Economy.UpgradeCost(wid, s, l);
            upgradeLevels[i].text = Loc.F("shop.lv", l, Economy.MaxUpgrade);
            for (int p = 0; p < Economy.MaxUpgrade; p++) upgradePips[i * Economy.MaxUpgrade + p].color = p < l ? UiKit.Hazard : UiKit.Hex(0x2A3324);
            if (!owned2) SetButton(upgradeButtons[i], UiStyle.Disabled, cost < 0 ? Loc.T("shop.max") : cost.ToString(), false, coinIcon);
            else if (cost < 0) SetButton(upgradeButtons[i], UiStyle.Disabled, Loc.T("shop.max"), false);
            else SetButton(upgradeButtons[i], Profile.CanAfford(cost, 0) ? UiStyle.Yellow : UiStyle.Disabled, cost.ToString(), true, coinIcon);
        }
    }

    // ------------------------------------------------------------------ characters + costumes
    /// <summary>Card i = items[i]: characters tab = soldier + SpecOps + Firefighter; costumes tab = soldier skins.</summary>
    void RefreshCharacters()
    {
        EnsureThumbs();
        var cur = Profile.SelectedCostume;
        CostumeId shown = CostumeId.Standard;
        for (int i = 0; i < characterCards.Count; i++)
        {
            var card = characterCards[i];
            bool on = i < items.Count;
            card.button.gameObject.SetActive(on);
            if (!on) continue;
            var it = items[i];
            var c = (CostumeId)it.index; var d = Economy.Costume(c);
            bool soldier = it.character && !Economy.IsCharacter(c);
            bool owned = Profile.Owns(c), sel = i == selected;
            bool eq = soldier ? !Economy.IsCharacter(cur) : cur == c;
            card.title.text = Name(it);
            card.sub.text = Loc.T(soldier ? "shop.soldier.desc" : d.Key + ".desc");
            string tagKey; Color tagBg, tagFg;
            if (eq) { tagKey = "shop.equipped"; tagBg = UiKit.Hazard; tagFg = UiKit.Ink; }
            else if (soldier) { tagKey = "rarity.standard"; tagBg = UiKit.Hex(0x2A3324); tagFg = UiKit.Secondary; }
            else if (d.packOnly) { tagKey = "rarity.legendary"; tagBg = UiKit.Hex(0x3A2A08); tagFg = UiKit.Gold; }
            else if (d.gems > 0) { tagKey = "rarity.epic"; tagBg = UiKit.Hex(0x2C1B45); tagFg = UiKit.Hex(0xC9A2FF); }
            else if (d.gold > 0) { tagKey = "rarity.rare"; tagBg = UiKit.Hex(0x14304A); tagFg = UiKit.Gem; }
            else { tagKey = "rarity.standard"; tagBg = UiKit.Hex(0x2A3324); tagFg = UiKit.Secondary; }
            card.tag.text = Loc.T(tagKey); card.tagBg.color = tagBg; card.tag.color = tagFg;
            // Every card shows its character: the selected one live (turning), the others as a still.
            var thumb = sel ? previewImage.texture : ThumbOf(it);
            card.preview.gameObject.SetActive(thumb);
            card.preview.texture = thumb;
            // Lock badge on locked cards; without a picture (no GPU) the icon stands in.
            card.icon.gameObject.SetActive(!owned || !thumb);
            card.icon.sprite = owned ? userIcon : lockIcon;
            if (card.selected) card.selected.gameObject.SetActive(sel || eq);
            if (card.selected) card.selected.color = eq ? UiKit.Hazard : UiKit.Line2;
            if (eq) SetButton(card.action, UiStyle.Disabled, Loc.T("shop.equipped"), false, checkIcon);
            else if (owned) SetButton(card.action, UiStyle.Dark, Loc.T("shop.equip"));
            else if (d.packOnly) SetButton(card.action, UiStyle.Yellow, Store.Price(Economy.ProductPrefix + "starter_pack"), !Profile.StarterPackOwned);
            else if (d.gems > 0) SetButton(card.action, UiStyle.Dark, d.gems.ToString(), true, gemIcon);
            else SetButton(card.action, UiStyle.Dark, d.gold.ToString(), true, coinIcon);
            if (sel) shown = soldier ? Profile.SoldierSkin : c;
        }
        preview.ShowCostume(shown);
        previewImage.enabled = true;
    }

    // ------------------------------------------------------------------ bonuses
    void RefreshBonuses()
    {
        for (int i = 0; i < bonusRows.Length && i < Economy.Bonuses.Length; i++)
        {
            var b = (BonusId)i; var def = Economy.Bonus(b); var r = bonusRows[i];
            int lvl = Profile.BonusLevel(b), cost = Profile.BonusCost(b);
            r.title.text = Loc.T(def.Key);
            r.sub.text = Loc.T(def.Key + ".desc");
            int maxL = Mathf.Max(1, def.maxLevel);
            for (int p = 0; p < 3; p++)
            {
                var pip = bonusPips[i * 3 + p];
                pip.gameObject.SetActive(p < maxL);
                pip.color = p < lvl ? UiKit.Hazard : UiKit.Hex(0x2A3324);
            }
            r.tag.text = b == BonusId.StartCard ? Loc.F("shop.charges", lvl) : Loc.F("shop.lv", lvl, def.maxLevel);
            bonusNext[i].text = cost < 0 ? "" : Loc.T(def.Key + ".next");
            bool afford = cost >= 0 && Profile.CanAfford(cost, 0);
            if (cost < 0) SetButton(r.action, UiStyle.Disabled, Loc.T("shop.max"), false);
            else SetButton(r.action, afford ? UiStyle.Yellow : UiStyle.Disabled, cost.ToString(), true, coinIcon);
            bonusMissing[i].text = cost >= 0 && !afford ? Loc.F("shop.missing_gold", cost - Profile.Gold) : "";
        }
    }

    // ------------------------------------------------------------------ gems
    void RefreshGems()
    {
        freeDesc.text = Loc.F("shop.free_gems.desc", Economy.FreeGemAd);
        if (priceNote) priceNote.text = Loc.F("shop.price_note", Economy.CharacterGems);
        bool free = Profile.FreeGemAvailable;
        SetButton(freeButton, free ? UiStyle.Green : UiStyle.Disabled, Loc.T(free ? "shop.watch_ad" : "shop.tomorrow"), free && Ads.Available);
        for (int i = 0; i < productCards.Count; i++)
        {
            var it = productItems[i]; var c = productCards[i];
            c.title.text = Name(it);
            if (c.selected) c.selected.gameObject.SetActive(i + 1 == selected);
            if (it.kind == Kind.Product)
            {
                var p = Economy.Product(it.product).Value;
                bool owned = p.kind == ProductKind.NonConsumable && (p.vip ? Profile.Vip : Profile.StarterPackOwned);
                c.sub.text = Amount(p);
                c.sub.color = p.vip ? UiKit.Gold : UiKit.Gem;
                c.icon.sprite = gemIcon;
                SetButton(c.action, owned ? UiStyle.Disabled : UiStyle.Yellow, owned ? Loc.T("shop.owned") : Store.Price(p.id), !owned && Store.Ready);
                if (c.tagBg) c.tagBg.gameObject.SetActive(p.kind == ProductKind.Consumable && p.gems == Economy.CharacterGems);
            }
            else
            {
                var o = Economy.GoldOffers[it.index];
                c.sub.text = Loc.T("shop.gold_trade");
                c.sub.color = UiKit.Gold;
                c.icon.sprite = goldIcon ? goldIcon : coinIcon;
                SetButton(c.action, Profile.CanAfford(0, o.gems) ? UiStyle.Gem : UiStyle.Disabled, o.gems.ToString(), true, gemIcon);
                if (c.tagBg) c.tagBg.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>What a pack gives, e.g. "150 ELMAS" / "150 ELMAS + 2000 ALTIN" / VIP perks.</summary>
    static string Amount(ProductDef p)
    {
        if (p.vip) return Loc.T("shop.vip_amount");
        string s = Loc.F("shop.gem_amount", p.gems);
        if (p.gold > 0) s += " + " + Loc.F("shop.gold_amount", p.gold);
        return s;
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
                if (Profile.Owns(c))
                {
                    if (it.character) Profile.SelectCharacter(c); else Profile.SelectCostume(c);
                    Refresh();
                    return;
                }
                if (d.packOnly)
                {
                    string id = Economy.ProductPrefix + "starter_pack";
                    Ask(Loc.F("shop.confirm_iap", Loc.T(Economy.Product(id).Value.Key), Store.Price(id)), () => { Message(Loc.T("shop.wait")); Store.Buy(id, Result); });
                    return;
                }
                if (!Profile.CanAfford(d.gold, d.gems))
                {
                    Message(Loc.T(d.gems > 0 ? "shop.no_gems" : "shop.no_gold"));
                    // Not enough gems → straight to the TRY packs.
                    if (d.gems > 0) Open(Tab.Gems, 1);
                    return;
                }
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
                Ask(Loc.F("shop.confirm_iap", Name(it) + " (" + Amount(Economy.Product(it.product).Value) + ")", Store.Price(it.product)), () => { Message(Loc.T("shop.wait")); Store.Buy(it.product, Result); });
                return;
        }
    }

    void AskUpgrade(WeaponStat s)
    {
        if (Current != Tab.Weapons) return;
        var w = (WeaponId)items[selected].index;
        if (!Profile.Owns(w)) return;
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
