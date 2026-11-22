using UnityEngine;

/// <summary>Button looks of the redesign (StyleGuide): primary yellow, reward green, secondary dark, disabled, danger outline, ghost tab.</summary>
public enum UiStyle { Yellow, Green, Dark, Disabled, Danger, Ghost, Gem }

/// <summary>
/// UI palette + style table. 1.1.0 "B — comic apocalypse" look: deep purple-black ground, thick ink outlines, hard
/// offset shadows, bright sticker faces (yellow / green / gem / red), paper-white secondary buttons with ink text.
/// Pure data; <see cref="PressButton"/> paints itself from it and screens use the colours for texts / bars / tags.
/// </summary>
public static class UiKit
{
    public static readonly Color Ground = Hex(0x1D1A24);
    public static readonly Color Panel = Hex(0x262033, 0.97f);
    public static readonly Color Card = Hex(0x2C2540);
    public static readonly Color CardHi = Hex(0x372E50);
    public static readonly Color Line = Hex(0x0E0C12);
    public static readonly Color Line2 = Hex(0x0E0C12);
    public static readonly Color Hazard = Hex(0xFFC42E);
    public static readonly Color HazardAlt = Hex(0x0E0C12);
    public static readonly Color Success = Hex(0x8EDB2B);
    public static readonly Color SuccessAlt = Hex(0x0E0C12);
    public static readonly Color Danger = Hex(0xF0453A);
    public static readonly Color DangerAlt = Hex(0xFF8A7D);
    public static readonly Color Gem = Hex(0x4FC8FF);
    public static readonly Color Text = Hex(0xFFF7E6);
    public static readonly Color Secondary = Hex(0xC9C0D8);
    public static readonly Color Disabled = Hex(0x8F889C);
    public static readonly Color Gold = Hex(0xFFC93C);
    public static readonly Color Ink = Hex(0x141218);
    public static readonly Color Purple = Hex(0xB57CFF);
    /// <summary>B: paper-white sticker (secondary buttons, list cards, stat tiles) with ink text.</summary>
    public static readonly Color Paper = Hex(0xF6EEDC);
    public static readonly Color Orange = Hex(0xFF7A1A);
    /// <summary>B: hard offset shadow under stickers (x right, y down) and its colour.</summary>
    public static readonly Vector2 ShadowOffset = new Vector2(4f, -5f);
    public static readonly Color ShadowColor = Hex(0x0E0C12, 0.95f);

    public struct Look
    {
        public Color face, lip, text, stroke;
        public bool hasLip, hasStroke, gloss;
    }

    public static Look For(UiStyle s)
    {
        switch (s)
        {
            case UiStyle.Yellow: return new Look { face = Hazard, lip = ShadowColor, text = Ink, stroke = Line, hasLip = true, hasStroke = true, gloss = true };
            case UiStyle.Green: return new Look { face = Success, lip = ShadowColor, text = Ink, stroke = Line, hasLip = true, hasStroke = true, gloss = true };
            case UiStyle.Gem: return new Look { face = Gem, lip = ShadowColor, text = Ink, stroke = Line, hasLip = true, hasStroke = true, gloss = true };
            case UiStyle.Dark: return new Look { face = Paper, lip = ShadowColor, text = Ink, stroke = Line, hasLip = true, hasStroke = true };
            case UiStyle.Disabled: return new Look { face = Hex(0x4A4458), text = Hex(0xA49DB2), stroke = Line, hasStroke = true };
            case UiStyle.Danger: return new Look { face = Danger, lip = ShadowColor, text = Text, stroke = Line, hasLip = true, hasStroke = true };
            default: return new Look { face = Hex(0xFFFFFF, 0.92f), lip = ShadowColor, text = Ink, stroke = Line, hasLip = true, hasStroke = true };
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
