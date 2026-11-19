using UnityEngine;

/// <summary>Button looks of the redesign (StyleGuide): primary yellow, reward green, secondary dark, disabled, danger outline, ghost tab.</summary>
public enum UiStyle { Yellow, Green, Dark, Disabled, Danger, Ghost, Gem }

/// <summary>
/// UI redesign palette + style table (docs/ui-ref/StyleGuide.png). Pure data; <see cref="PressButton"/> paints itself
/// from it and screens use the colours for texts / bars / tags.
/// </summary>
public static class UiKit
{
    public static readonly Color Ground = Hex(0x0B0D09);
    public static readonly Color Panel = Hex(0x11150F, 0.97f);
    public static readonly Color Card = Hex(0x141912);
    public static readonly Color CardHi = Hex(0x1A2016);
    public static readonly Color Line = Hex(0x34402C);
    public static readonly Color Line2 = Hex(0x3A4630);
    public static readonly Color Hazard = Hex(0xFFB21E);
    public static readonly Color HazardAlt = Hex(0xA86400);
    public static readonly Color Success = Hex(0xA6E22E);
    public static readonly Color SuccessAlt = Hex(0x4F7A0C);
    public static readonly Color Danger = Hex(0xE5483B);
    public static readonly Color DangerAlt = Hex(0xFF7A68);
    public static readonly Color Gem = Hex(0x5CCBF5);
    public static readonly Color Text = Hex(0xF2EDE1);
    public static readonly Color Secondary = Hex(0xB8BDA9);
    public static readonly Color Disabled = Hex(0x8E957F);
    public static readonly Color Gold = Hex(0xFFC93C);
    public static readonly Color Ink = Hex(0x1A1408);
    public static readonly Color Purple = Hex(0x9B5CF5);

    public struct Look
    {
        public Color face, lip, text, stroke;
        public bool hasLip, hasStroke, gloss;
    }

    public static Look For(UiStyle s)
    {
        switch (s)
        {
            case UiStyle.Yellow: return new Look { face = Hazard, lip = HazardAlt, text = Ink, hasLip = true, gloss = true };
            case UiStyle.Green: return new Look { face = Success, lip = SuccessAlt, text = Ink, hasLip = true, gloss = true };
            case UiStyle.Gem: return new Look { face = Gem, lip = Hex(0x2A7FA6), text = Ink, hasLip = true, gloss = true };
            case UiStyle.Dark: return new Look { face = Hex(0x1C2419), lip = Hex(0x070906), text = Text, stroke = Line, hasLip = true, hasStroke = true };
            case UiStyle.Disabled: return new Look { face = Card, text = Disabled, stroke = Line, hasStroke = true };
            case UiStyle.Danger: return new Look { face = new Color(Danger.r, Danger.g, Danger.b, 0.06f), text = DangerAlt, stroke = Danger, hasStroke = true };
            default: return new Look { face = new Color(0f, 0f, 0f, 0f), text = Secondary };
        }
    }

    public static Color Hex(int rgb, float a = 1f) =>
        new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

    public static string Time(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return s >= 3600 ? $"{s / 3600:00}:{s / 60 % 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
    }

    /// <summary>Seconds until the next UTC midnight (daily login / quests reset).</summary>
    public static float UntilUtcMidnight()
    {
        var now = System.DateTime.UtcNow;
        return (float)(now.Date.AddDays(1) - now).TotalSeconds;
    }
}
