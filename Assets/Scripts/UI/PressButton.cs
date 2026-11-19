using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Redesign button: a coloured face on a darker 4 px lip. Pressing slides the face down onto the lip (shadow goes to 0)
/// in 90 ms; a non-interactable Button shows the disabled look. Colours come from <see cref="UiKit.For"/>.
/// </summary>
[RequireComponent(typeof(Button))]
public class PressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public RectTransform face;
    public Image faceImage, lip, stroke, gloss;
    public TMP_Text label;
    public Image icon;
    public UiStyle style;
    public float depth = 4f;
    [Tooltip("Keep the icon's own colour (coins, gems) instead of the label colour.")]
    public bool keepIconColor;

    Button button;
    Vector2 rest;
    bool down, lastInteractable = true, init;
    float k;
    UiStyle shown = (UiStyle)(-1);

    void Awake() => Init();

    void Init()
    {
        if (init) return;
        init = true;
        button = GetComponent<Button>();
        if (face) rest = face.anchoredPosition;
        Apply();
    }

    public void SetStyle(UiStyle s)
    {
        Init();
        style = s;
        Apply();
    }

    public void SetLabel(string text)
    {
        Init();
        if (label) label.text = text;
    }

    void Apply()
    {
        var s = button && !button.interactable && style != UiStyle.Ghost ? UiStyle.Disabled : style;
        shown = s;
        var l = UiKit.For(s);
        if (faceImage) faceImage.color = l.face;
        if (lip) { lip.enabled = l.hasLip; lip.color = l.lip; }
        if (stroke) { stroke.enabled = l.hasStroke; stroke.color = l.stroke; }
        if (gloss) gloss.enabled = l.gloss;
        if (label) label.color = l.text;
        if (icon && !keepIconColor) icon.color = l.text;
    }

    public void OnPointerDown(PointerEventData e) { if (button.interactable) down = true; }
    public void OnPointerUp(PointerEventData e) => down = false;
    public void OnPointerExit(PointerEventData e) => down = false;

    void OnDisable() { down = false; k = 0f; if (face) face.anchoredPosition = rest; }

    void Update()
    {
        bool inter = button.interactable;
        if (inter != lastInteractable) { lastInteractable = inter; Apply(); }
        float target = down ? 1f : 0f;
        if (Mathf.Approximately(k, target)) return;
        k = Mathf.MoveTowards(k, target, Time.unscaledDeltaTime / 0.09f);
        if (face) face.anchoredPosition = rest + Vector2.down * depth * k;
    }
}
