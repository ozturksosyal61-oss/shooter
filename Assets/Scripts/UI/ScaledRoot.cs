using UnityEngine;

/// <summary>
/// Hosts layouts authored for a 1080-high canvas inside the 590-high redesign canvas: scales them down and keeps
/// the rect covering the parent at any aspect (screen-space scripts keep using their own root's rect size).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class ScaledRoot : MonoBehaviour
{
    public float designHeight = 1080f;

    RectTransform rt, parent;
    Vector2 lastSize = new Vector2(-1f, -1f);

    void OnEnable() { rt = (RectTransform)transform; parent = transform.parent as RectTransform; Fit(); }
    void Update() => Fit();
    void OnRectTransformDimensionsChange() => Fit();

    void Fit()
    {
        if (!rt || !parent) return;
        Vector2 ps = parent.rect.size;
        if (ps == lastSize || ps.y <= 0f) return;
        lastSize = ps;
        float s = ps.y / designHeight;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = new Vector3(s, s, 1f);
        rt.sizeDelta = ps / s;
    }
}
