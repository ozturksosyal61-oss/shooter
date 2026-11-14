using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the player near the world origin: when it strays past <see cref="threshold"/>,
/// every root object is shifted back by a whole number of chunks.
/// </summary>
public class FloatingOrigin : MonoBehaviour
{
    public static event Action<Vector3> OnShift;

    public Transform target;
    public float threshold = 120f;
    public float snap = 20f; // keep shifts chunk-aligned

    /// <summary>Accumulated shift: global position = scene position + Offset.</summary>
    public static Vector3 Offset { get; private set; }

    readonly List<GameObject> roots = new List<GameObject>(64);

    void Awake() => Offset = Vector3.zero;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 p = target.position;
        if (Mathf.Abs(p.x) < threshold && Mathf.Abs(p.z) < threshold) return;
        var delta = new Vector3(-Mathf.Round(p.x / snap) * snap, 0f, -Mathf.Round(p.z / snap) * snap);
        Shift(delta);
    }

    public void Shift(Vector3 delta)
    {
        gameObject.scene.GetRootGameObjects(roots);
        foreach (var r in roots)
        {
            if (r.transform is RectTransform) continue;
            r.transform.position += delta;
        }
        Offset -= delta;
        Physics.SyncTransforms();
        OnShift?.Invoke(delta);
    }

    /// <summary>Converts a scene position to an origin-independent global position.</summary>
    public static Vector3 ToGlobal(Vector3 local) => local + Offset;
    public static Vector3 ToLocal(Vector3 global) => global - Offset;
}
