using UnityEngine;

public enum Surface { Concrete, Metal }

/// <summary>Metadata on environment prefabs used by the chunk generator and bullet impacts.</summary>
public class EnvProp : MonoBehaviour
{
    [Tooltip("Bullet impact sound family.")]
    public Surface surface = Surface.Concrete;

    [Tooltip("Footprint radius used to keep placements apart (m).")]
    public float radius = 1f;
}
