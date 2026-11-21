using System;
using UnityEngine;

/// <summary>
/// Applies the profile to the soldier before anything reads it (runs before Weapon / Upgrades):
/// selected weapon model + stats (base × permanent upgrades), costume material, start-health bonus.
/// A start-card charge (bonus) grants one card pick right at the start of the run.
/// </summary>
[DefaultExecutionOrder(-50)]
public class WeaponLoadout : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public WeaponId id;
        public Transform root, rightGrip, leftGrip, muzzle, flash;
    }

    public Slot[] slots;
    public Material[] costumeMaterials;   // indexed by CostumeId
    public SkinnedMeshRenderer body;
    [Tooltip("Whole-character costumes (indexed by CostumeId; null = the soldier model with a costume texture).")]
    public GameObject[] costumeModels;
    public GameObject defaultModel;

    GameObject activeModel;
    SkinnedMeshRenderer soldierBody;
    Slot currentSlot;

    public WeaponId Current { get; private set; }
    public CostumeId Costume { get; private set; }
    public static WeaponId? Override;      // tests / bots

    void Awake()
    {
        soldierBody = body;
        activeModel = defaultModel;
        var pc = GetComponent<PlayerController>();
        Apply(Override ?? (GameMode.Weekly ? WeeklyEvent.WeaponFor(WeeklyEvent.CurrentWeek) : Profile.SelectedWeapon), pc);
        ApplyCostume(Profile.SelectedCostume, pc);
        ApplyLooks(Profile.SkinOf(Current), Profile.SelectedTracer, pc);
        pc.maxHealth += Economy.StartHealthBonus(Profile.BonusLevel(BonusId.StartHealth));
        if (GameMode.IsHard) pc.maxHealth = Mathf.Round(pc.maxHealth * ModeRules.HardPlayerHealthMul);
    }

    void Start()
    {
        if (Override == null && Progression.I && Profile.ConsumeStartCard()) Progression.I.GrantPick();
    }

    public void Apply(WeaponId id, PlayerController pc)
    {
        Current = id;
        Slot s = null;
        foreach (var sl in slots)
        {
            bool on = sl.id == id;
            if (sl.root) sl.root.gameObject.SetActive(on);
            if (on) s = sl;
        }
        if (s == null) return;
        currentSlot = s;
        var w = pc.weapon;
        w.muzzle = s.muzzle;
        w.muzzleFlash = s.flash;
        var rig = pc.rig;
        rig.weaponRoot = s.root;
        rig.rightGrip = s.rightGrip;
        rig.leftGrip = s.leftGrip;

        var d = Economy.Weapon(id);
        w.damage = d.damage * Economy.DamageMul(Profile.Level(id, WeaponStat.Damage));
        w.magazineSize = Economy.Magazine(d, Profile.Level(id, WeaponStat.Magazine));
        w.reloadTime = d.reloadTime * Economy.ReloadMul(Profile.Level(id, WeaponStat.Reload));
        w.fireRate = d.fireRate;
        w.spreadDeg = d.spread;
        w.bulletSpeed = d.bulletSpeed;
        w.bulletLife = d.range;
        w.critChance = d.crit;
        w.basePellets = d.pellets;
        w.pelletSpread = d.pelletSpread;
        w.basePierce = d.pierce;
        w.cameraKick = d.kick;
        w.shotShake = d.shake;
        w.shootSfx = d.sfx;
        pc.moveSpeed *= d.moveMul;
    }

    public GunSkinId GunSkin { get; private set; }
    public TracerId Tracer { get; private set; }

    /// <summary>1.1.0 cosmetics: the current weapon's finish + this soldier's bullet tracers.</summary>
    public void ApplyLooks(GunSkinId skin, TracerId tracer, PlayerController pc)
    {
        GunSkin = skin;
        Tracer = tracer;
        if (currentSlot != null) global::GunSkin.ApplyTo(currentSlot.root, skin);
        if (pc && pc.weapon) pc.weapon.tracer = tracer;
    }

    public GameObject ModelFor(CostumeId c) =>
        costumeModels != null && (int)c < costumeModels.Length && costumeModels[(int)c] ? costumeModels[(int)c] : defaultModel;

    public GameObject ActiveModel => activeModel;

    public void ApplyCostume(CostumeId c, PlayerController pc)
    {
        Costume = c;
        var target = ModelFor(c);
        if (target && target != activeModel)
        {
            // Whole-character swap: same animator controller (humanoid clips), weapon IK moves to the new rig.
            if (costumeModels != null) foreach (var mo in costumeModels) if (mo) mo.SetActive(mo == target);
            if (defaultModel) defaultModel.SetActive(defaultModel == target);
            activeModel = target;
            var rig = target.GetComponent<SoldierRig>();
            pc.rig = rig;
            pc.model = target.transform;
            if (pc.weapon) pc.weapon.rig = rig;
            if (rig && currentSlot != null) { rig.weaponRoot = currentSlot.root; rig.rightGrip = currentSlot.rightGrip; rig.leftGrip = currentSlot.leftGrip; }
            body = target == defaultModel ? soldierBody : target.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (pc.flash) pc.flash.SetRenderers(target.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        }
        if (target != defaultModel)
        {
            if (pc.flash && body) pc.flash.SetBaseMaterial(body.sharedMaterial);
            return;
        }
        if (costumeMaterials == null || (int)c >= costumeMaterials.Length || !costumeMaterials[(int)c]) return;
        var m = costumeMaterials[(int)c];
        if (body) body.sharedMaterial = m;
        if (pc.flash) pc.flash.SetBaseMaterial(m);
    }
}
