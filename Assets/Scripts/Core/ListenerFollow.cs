using UnityEngine;

/// <summary>
/// Top-down audio: the AudioListener sits just above the player (not at the far-away camera),
/// facing the screen's "up" so left/right panning matches what the player sees.
/// </summary>
public class ListenerFollow : MonoBehaviour
{
    public Transform target;
    public float height = 2f;

    void LateUpdate()
    {
        if (!target) return;
        transform.SetPositionAndRotation(target.position + Vector3.up * height, Quaternion.identity);
    }
}
