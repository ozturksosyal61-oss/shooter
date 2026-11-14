using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Left half: floating joystick (move). Right half: hold = fire, drag direction = aim (360°).
/// Editor/desktop: left mouse button simulates a finger on whichever half it is pressed,
/// WASD moves, right mouse button fires toward the cursor.
/// </summary>
public class TouchControls : MonoBehaviour
{
    public static TouchControls I { get; private set; }

    [Header("UI")]
    public Canvas canvas;
    public RectTransform joyBase, joyKnob, aimBase, aimKnob;
    public CanvasGroup joyGroup, aimGroup;

    [Header("Tuning (canvas units)")]
    public float joyRadius = 110f;
    public float aimRadius = 90f;
    public float aimDeadzone = 26f;
    public Vector2 joyIdlePos = new Vector2(260f, 240f);

    public Vector2 Move { get; private set; }
    public bool FireHeld { get; private set; }
    public bool HasAimDir { get; private set; }
    public Vector2 AimDir { get; private set; }
    public bool AimAtScreenPoint { get; private set; }
    public Vector2 AimScreenPoint { get; private set; }
    public bool AnyPressThisFrame { get; private set; }

    const int MouseId = -100;
    const int None = int.MinValue;

    int leftId = None, rightId = None;
    Vector2 leftOrigin, leftCur, rightOrigin, rightCur;
    bool mouseAim;
    Vector2 keyMove;

    void OnEnable()
    {
        I = this;
        EnhancedTouchSupport.Enable();
    }

    void OnDisable()
    {
        EnhancedTouchSupport.Disable();
        if (I == this) I = null;
    }

    void Update()
    {
        AnyPressThisFrame = false;
        ReadTouches();
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        ReadMouseAndKeyboard();
#endif
        float scale = canvas ? canvas.scaleFactor : 1f;

        // --- Move (left) ---
        Vector2 joyOffset = Vector2.zero;
        if (leftId != None)
        {
            float r = joyRadius * scale;
            Vector2 d = leftCur - leftOrigin;
            if (d.magnitude > r) { leftOrigin = leftCur - d.normalized * r; d = leftCur - leftOrigin; }
            Vector2 m = d / r;
            Move = m.magnitude < 0.12f ? Vector2.zero : Vector2.ClampMagnitude(m, 1f);
            joyOffset = d / scale;
        }
        else
        {
            Move = keyMove.sqrMagnitude > 1f ? keyMove.normalized : keyMove;
        }

        // --- Fire / aim (right) ---
        Vector2 aimOffset = Vector2.zero;
        if (rightId != None)
        {
            float r = aimRadius * scale;
            Vector2 d = rightCur - rightOrigin;
            if (d.magnitude > r) { rightOrigin = rightCur - d.normalized * r; d = rightCur - rightOrigin; }
            HasAimDir = d.magnitude > aimDeadzone * scale;
            AimDir = HasAimDir ? d.normalized : Vector2.zero;
            aimOffset = d / scale;
        }
        else HasAimDir = false;

        AimAtScreenPoint = rightId == None && mouseAim;
        FireHeld = rightId != None || mouseAim;

        UpdateVisuals(scale, joyOffset, aimOffset);
    }

    void ReadTouches()
    {
        bool leftSeen = false, rightSeen = false;
        foreach (var t in ETouch.activeTouches)
        {
            int id = t.touchId;
            Vector2 p = t.screenPosition;
            if (t.phase == ETouchPhase.Began) { AnyPressThisFrame = true; BeginPointer(id, p); }
            bool ended = t.phase == ETouchPhase.Ended || t.phase == ETouchPhase.Canceled;
            if (id == leftId) { if (ended) leftId = None; else { leftCur = p; leftSeen = true; } }
            else if (id == rightId) { if (ended) rightId = None; else { rightCur = p; rightSeen = true; } }
        }
        if (leftId >= 0 && !leftSeen) leftId = None;
        if (rightId >= 0 && !rightSeen) rightId = None;
    }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
    void ReadMouseAndKeyboard()
    {
        var m = Mouse.current;
        if (m != null)
        {
            Vector2 p = m.position.ReadValue();
            if (m.leftButton.wasPressedThisFrame) { AnyPressThisFrame = true; BeginPointer(MouseId, p); }
            if (leftId == MouseId) { if (m.leftButton.isPressed) leftCur = p; else leftId = None; }
            if (rightId == MouseId) { if (m.leftButton.isPressed) rightCur = p; else rightId = None; }
            if (m.rightButton.wasPressedThisFrame) AnyPressThisFrame = true;
            mouseAim = m.rightButton.isPressed;
            AimScreenPoint = p;
        }
        var k = Keyboard.current;
        keyMove = Vector2.zero;
        if (k != null)
        {
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) keyMove.x -= 1f;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) keyMove.x += 1f;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) keyMove.y -= 1f;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) keyMove.y += 1f;
            if (k.spaceKey.wasPressedThisFrame) AnyPressThisFrame = true;
        }
    }
#endif

    void BeginPointer(int id, Vector2 p)
    {
        if (p.x < Screen.width * 0.5f)
        {
            if (leftId == None) { leftId = id; leftOrigin = leftCur = p; }
        }
        else if (rightId == None) { rightId = id; rightOrigin = rightCur = p; }
    }

    void UpdateVisuals(float scale, Vector2 joyOffset, Vector2 aimOffset)
    {
        float dt = Time.unscaledDeltaTime;
        if (joyBase)
        {
            bool active = leftId != None;
            joyBase.anchoredPosition = active ? leftOrigin / scale : joyIdlePos;
            joyKnob.anchoredPosition = joyOffset;
            if (joyGroup) joyGroup.alpha = Mathf.MoveTowards(joyGroup.alpha, active ? 0.85f : 0.25f, dt * 6f);
        }
        if (aimBase)
        {
            bool active = rightId != None;
            if (active) aimBase.anchoredPosition = rightOrigin / scale;
            aimKnob.anchoredPosition = aimOffset;
            if (aimGroup) aimGroup.alpha = Mathf.MoveTowards(aimGroup.alpha, active ? 0.7f : 0f, dt * 8f);
        }
    }
}
