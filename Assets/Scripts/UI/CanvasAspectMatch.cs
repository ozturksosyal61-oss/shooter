using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keeps the 1280×590 reference layout inside the screen on every aspect: wide phones (19.5:9 and wider) match the
/// height (layout 1:1, extra width becomes side room), narrower screens (16:9 phones, 4:3 iPads) match the width so
/// nothing overlaps or runs off the edge — the UI gets a little smaller and gains vertical room instead.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class CanvasAspectMatch : MonoBehaviour
{
    CanvasScaler scaler;
    Canvas canvas;
    float lastAspect = -1f;

    /// <summary>Pure: 1 (match height) at or above the reference aspect, 0 (match width) below it.</summary>
    public static float MatchFor(float aspect, float refAspect) => aspect >= refAspect - 0.01f ? 1f : 0f;

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        canvas = GetComponent<Canvas>();
        Apply();
    }

    void Update() => Apply();

    void Apply()
    {
        if (!scaler) return;
        var r = canvas ? canvas.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
        if (r.height < 1f) return;
        float aspect = r.width / r.height;
        if (Mathf.Approximately(aspect, lastAspect)) return;
        lastAspect = aspect;
        var refRes = scaler.referenceResolution;
        scaler.matchWidthOrHeight = MatchFor(aspect, refRes.x / refRes.y);
    }
}
