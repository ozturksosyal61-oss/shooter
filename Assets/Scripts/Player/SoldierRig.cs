using UnityEngine;

/// <summary>
/// Drives the soldier Animator and holds the rifle:
/// - base layer: 8-way locomotion blend tree in aim-local space (backpedal while shooting), vault, death
/// - upper-body layer (avatar mask): aim / reload, weight driven from code
/// - IK pass: rifle held in front of the chest along the aim; right hand IK onto the pistol grip,
///   left hand IK onto the handguard (released while reloading).
/// Lives on the animated model (same GameObject as the Animator).
/// </summary>
[RequireComponent(typeof(Animator))]
public class SoldierRig : MonoBehaviour
{
    public Transform weaponRoot;
    public Transform rightGrip;
    public Transform leftGrip;
    [Tooltip("Where the rifle's stock end sits relative to the character, in aim space (x right, y up, z forward).")]
    public Vector3 holdOffset = new Vector3(0.1f, 0.95f, 0.2f);
    [Tooltip("Raw humanoid root speed of the forward walk / run clips (0 = in-place clip, use the fallback).")]
    public float walkClipSpeed;
    public float runClipSpeed;
    public float walkSpeedFallback = 1.6f;
    public float runSpeedFallback = 4f;
    public float reloadClipLength = 2.2f;
    public float upperBlendSpeed = 8f;

    static readonly int MoveX = Animator.StringToHash("MoveX");
    static readonly int MoveY = Animator.StringToHash("MoveY");
    static readonly int LocoSpeed = Animator.StringToHash("LocoSpeed");
    static readonly int Vaulting = Animator.StringToHash("Vaulting");
    static readonly int Dead = Animator.StringToHash("Dead");
    static readonly int DeathIndex = Animator.StringToHash("DeathIndex");
    static readonly int ReloadSpeed = Animator.StringToHash("ReloadSpeed");
    static readonly int AimState = Animator.StringToHash("Aim");
    static readonly int ReloadState = Animator.StringToHash("Reload");

    Animator anim;
    Transform owner;
    float walkSpeed, runSpeed;
    float upperWeight, leftHandWeight, recoil, reloadT, reloadDuration = 1.5f;
    bool aiming, reloading, dead;
    Quaternion aimRot = Quaternion.identity;

    public Animator Animator => anim;
    public float RunSpeed => runSpeed;

    void Awake()
    {
        anim = GetComponent<Animator>();
        owner = transform.parent;
        // Clip root speeds are measured on the unscaled avatar.
        walkSpeed = walkClipSpeed > 0f ? walkClipSpeed * transform.lossyScale.y : walkSpeedFallback;
        runSpeed = runClipSpeed > 0f ? runClipSpeed * transform.lossyScale.y : runSpeedFallback;
    }

    /// <summary>Per-frame locomotion/aim state from the PlayerController.</summary>
    public void Tick(float dt, Vector3 move, float speed, bool isAiming, bool isVaulting)
    {
        if (dead) return;
        aiming = isAiming;
        // Local move direction relative to where the body faces → backpedal / strafe animations.
        Vector3 local = owner.InverseTransformDirection(move);
        anim.SetFloat(MoveX, local.x, 0.08f, dt);
        anim.SetFloat(MoveY, local.z, 0.08f, dt);
        float mag = move.magnitude;
        float expected = mag < 0.5f ? walkSpeed : Mathf.Lerp(walkSpeed, runSpeed, (mag - 0.5f) * 2f);
        float locoMult = mag < 0.5f ? 1f : Mathf.Clamp(speed / Mathf.Max(0.1f, expected), 0.7f, 1.8f);
        anim.SetFloat(LocoSpeed, locoMult);
        anim.SetBool(Vaulting, isVaulting);

        if (reloading)
        {
            reloadT += dt;
            if (reloadT >= reloadDuration) { reloading = false; anim.CrossFadeInFixedTime(AimState, 0.12f, 1); }
        }
        float targetUpper = reloading || aiming ? 1f : 0f;
        upperWeight = Mathf.MoveTowards(upperWeight, targetUpper, dt * upperBlendSpeed);
        anim.SetLayerWeight(1, upperWeight);
        leftHandWeight = Mathf.MoveTowards(leftHandWeight, reloading ? 0f : 1f, dt * 10f);
        recoil = Mathf.MoveTowards(recoil, 0f, dt * 14f);

        Vector3 f = owner.forward; f.y = 0f;
        if (f.sqrMagnitude > 0.001f) aimRot = Quaternion.LookRotation(f.normalized, Vector3.up);
    }

    public void Kick() => recoil = 1f;

    /// <summary>Animation event on the reload clip ("MagOut", "MagIn", "Bolt").</summary>
    public void OnReloadEvent(string part)
    {
        if (!reloading) return;
        var id = part == "MagOut" ? SfxId.MagOut : part == "MagIn" ? SfxId.MagIn : SfxId.Bolt;
        GameAudio.Play(id, owner.position + Vector3.up * 1.1f);
    }

    public void PlayReload(float duration)
    {
        reloading = true;
        reloadT = 0f;
        reloadDuration = duration;
        anim.SetFloat(ReloadSpeed, reloadClipLength / Mathf.Max(0.1f, duration));
        anim.CrossFadeInFixedTime(ReloadState, 0.1f, 1);
    }

    /// <summary>Perfect active reload: snap back to aiming right away.</summary>
    public void FinishReloadEarly()
    {
        if (!reloading) return;
        reloading = false;
        anim.CrossFadeInFixedTime(AimState, 0.08f, 1);
    }

    /// <summary>Jammed active reload: the reload pose lasts a bit longer.</summary>
    public void ExtendReload(float seconds)
    {
        if (!reloading) return;
        reloadDuration += seconds;
        anim.SetFloat(ReloadSpeed, reloadClipLength / Mathf.Max(0.1f, reloadDuration));
    }

    public void Die(Vector3 hitFromDir)
    {
        if (dead) return;
        dead = true;
        reloading = false;
        // Hit from the front (attacker in front) → fall backwards.
        Vector3 toAttacker = -hitFromDir; toAttacker.y = 0f;
        int index = Vector3.Dot(owner.forward, toAttacker) >= 0f ? 0 : 1;
        anim.SetInteger(DeathIndex, index);
        anim.SetBool(Dead, true);
        anim.SetLayerWeight(1, 0f);
        // Let the rifle follow the right hand from now on.
        var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand && weaponRoot) weaponRoot.SetParent(hand, true);
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || dead || !weaponRoot) return;

        // Rifle: grip at the right hand, barrel along the aim (with reload tilt + recoil).
        Quaternion rot = aimRot;
        if (reloading)
        {
            float t = Mathf.Clamp01(reloadT / reloadDuration);
            float tilt = Mathf.Sin(t * Mathf.PI);
            rot *= Quaternion.Euler(25f * tilt, -20f * tilt, 35f * tilt);
        }
        rot *= Quaternion.Euler(-7f * recoil, 0f, 0f);
        Vector3 anchor = owner.position + aimRot * holdOffset - aimRot * Vector3.forward * (0.07f * recoil);
        if (reloading) anchor += Vector3.down * 0.12f * Mathf.Sin(Mathf.Clamp01(reloadT / reloadDuration) * Mathf.PI);
        weaponRoot.SetPositionAndRotation(anchor, rot);

        // Right hand on the pistol grip (chibi arms are short, so the animation pose alone can't reach).
        anim.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
        anim.SetIKRotationWeight(AvatarIKGoal.RightHand, 0.5f);
        anim.SetIKPosition(AvatarIKGoal.RightHand, rightGrip.position);
        anim.SetIKRotation(AvatarIKGoal.RightHand, rightGrip.rotation);
        anim.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0.5f);
        anim.SetIKHintPosition(AvatarIKHint.RightElbow, rightGrip.position + rot * new Vector3(0.3f, -0.3f, -0.25f));

        anim.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandWeight);
        anim.SetIKRotationWeight(AvatarIKGoal.LeftHand, leftHandWeight * 0.6f);
        anim.SetIKPosition(AvatarIKGoal.LeftHand, leftGrip.position);
        anim.SetIKRotation(AvatarIKGoal.LeftHand, leftGrip.rotation);
        anim.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, leftHandWeight * 0.5f);
        anim.SetIKHintPosition(AvatarIKHint.LeftElbow, leftGrip.position + rot * new Vector3(-0.25f, -0.35f, -0.2f));
    }
}
