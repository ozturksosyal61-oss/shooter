using TMPro;
using UnityEngine;

/// <summary>
/// TextMeshPro text with a few legacy-Text-shaped setters (alignment as TextAnchor, overflow, best fit) so the scene
/// builder's older layout code reads the same; runtime scripts only see <see cref="TMP_Text"/>.
/// </summary>
public class UiText : TextMeshProUGUI
{
    public new TextAnchor alignment
    {
        get => TextAnchor.MiddleCenter;
        set => base.alignment = Map(value);
    }

    public HorizontalWrapMode horizontalOverflow
    {
        get => textWrappingMode == TextWrappingModes.NoWrap ? HorizontalWrapMode.Overflow : HorizontalWrapMode.Wrap;
        set => textWrappingMode = value == HorizontalWrapMode.Wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
    }

    public VerticalWrapMode verticalOverflow { get => VerticalWrapMode.Overflow; set => overflowMode = TextOverflowModes.Overflow; }
    public bool resizeTextForBestFit { get => enableAutoSizing; set => enableAutoSizing = value; }
    public int resizeTextMinSize { get => (int)fontSizeMin; set => fontSizeMin = value; }
    public int resizeTextMaxSize { get => (int)fontSizeMax; set => fontSizeMax = value; }
    public bool supportRichText { get => richText; set => richText = value; }

    public static TextAlignmentOptions Map(TextAnchor a)
    {
        switch (a)
        {
            case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
            case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
            case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
            case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
            case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
            case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
            case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
            case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
            default: return TextAlignmentOptions.Center;
        }
    }
}
