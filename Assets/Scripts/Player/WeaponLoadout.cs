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

    public WeaponId Current { get; private set; }
    public CostumeId Costume { get; private set; }
    public static WeaponId? Override;      // tests / bots

    void Awake()
    {
        var pc = GetComponent<PlayerController>();
        Apply(Override ?? Profile.SelectedWeapon, pc);
        ApplyCostume(Profile.SelectedCostume, pc);
        pc.maxHealth += Economy.StartHealthBonus(Profile.BonusLevel(BonusId.StartHealth));
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

    public void ApplyCostume(CostumeId c, PlayerController pc)
    {
        Costume = c;
        if (costumeMaterials == null || (int)c >= costumeMaterials.Length || !costumeMaterials[(int)c]) return;
        var m = costumeMaterials[(int)c];
        if (body) body.sharedMaterial = m;
        if (pc.flash) pc.flash.SetBaseMaterial(m);
    }
}
