using UnityEngine;

/// <summary>Metadata on environment prefabs used by the chunk generator.</summary>
public class EnvProp : MonoBehaviour
{
    [Tooltip("Footprint radius used to keep placements apart (m).")]
    public float radius = 1f;
}
