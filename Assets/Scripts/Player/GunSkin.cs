using UnityEngine;

/// <summary>
/// On every gun model (player, teammate avatar, shop / menu preview): swaps its material slots to a finish from
/// <see cref="CosmeticLibrary"/>. Each slot has a role (dark / mid / light / accent; -1 = keep, e.g. glass) set by
/// the builder from the source material name; <see cref="GunSkinId.Standard"/> restores the originals.
/// </summary>
public class GunSkin : MonoBehaviour
{
    public const int Roles = 4;

    public Renderer[] renderers;
    [Tooltip("Flattened over every renderer's material slots, in order.")]
    public int[] roles;
    public Material[] originals;

    public GunSkinId Current { get; private set; }

    public void Apply(GunSkinId skin)
    {
        Current = skin;
        if (renderers == null || roles == null || originals == null) return;
        var lib = CosmeticLibrary.I;
        int k = 0;
        foreach (var r in renderers)
        {
            if (!r) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length && k < roles.Length; i++, k++)
            {
                Material m = null;
                if (skin != GunSkinId.Standard && lib && roles[k] >= 0) m = lib.SkinMat(skin, roles[k]);
                mats[i] = m ? m : originals[k];
            }
            r.sharedMaterials = mats;
        }
    }

    /// <summary>Applies <paramref name="skin"/> to the gun under <paramref name="root"/> (no-op without a GunSkin).</summary>
    public static void ApplyTo(Transform root, GunSkinId skin)
    {
        if (root && root.TryGetComponent(out GunSkin g)) g.Apply(skin);
    }
}
