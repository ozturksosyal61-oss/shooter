using UnityEngine;

public enum DayPhase { Day, Dusk, Night }

/// <summary>The wave's sky: time of day + rain (cosmetic; same on every co-op machine — it only reads the wave).</summary>
public struct WeatherState
{
    public DayPhase phase;
    public bool rain;
    public bool Equals(WeatherState o) => phase == o.phase && rain == o.rain;
}

/// <summary>
/// 1.1.0 pure weather rules (EditMode-tested). Every 5 waves the sky turns: day (1–5) → dusk (6–10) → night (11–15)
/// → day again. About a quarter of the waves from wave 3 rain (seeded by wave + run seed, never two in a row).
/// Mood multipliers for <see cref="DistrictAtmosphere"/>: darker, colder nights; warm dusk; grey wet rain.
/// </summary>
public static class Weather
{
    public const int WavesPerPhase = 5;

    public static DayPhase PhaseFor(int wave) => wave <= 0 ? DayPhase.Day : (DayPhase)(((wave - 1) / WavesPerPhase) % 3);

    public static bool RainFor(int wave, int seed)
    {
        if (wave < 3) return false;
        if (Roll(wave - 1, seed)) return false; // no two rainy waves in a row
        return Roll(wave, seed);
    }

    static bool Roll(int wave, int seed)
    {
        unchecked
        {
            uint h = (uint)(wave * 73856093) ^ (uint)(seed * 19349663) ^ 0x9E3779B9u;
            h ^= h >> 13; h *= 0x85EBCA6Bu; h ^= h >> 16;
            return wave >= 3 && h % 100 < 28;
        }
    }

    public static WeatherState For(int wave, int seed) => new WeatherState { phase = PhaseFor(wave), rain = RainFor(wave, seed) };

    /// <summary>Applies the weather on top of a district mood.</summary>
    public static DistrictMood Tint(DistrictMood m, WeatherState w)
    {
        switch (w.phase)
        {
            case DayPhase.Dusk:
                m.sky *= new Color(0.95f, 0.72f, 0.62f); m.equator *= new Color(0.95f, 0.72f, 0.6f); m.ground *= new Color(0.85f, 0.7f, 0.6f);
                m.light *= new Color(1f, 0.62f, 0.42f); m.lightIntensity *= 0.8f;
                m.fog *= new Color(1f, 0.68f, 0.5f); m.fogDensity *= 1.1f;
                break;
            case DayPhase.Night:
                m.sky *= new Color(0.42f, 0.48f, 0.72f); m.equator *= new Color(0.4f, 0.45f, 0.68f); m.ground *= new Color(0.38f, 0.4f, 0.55f);
                m.light *= new Color(0.55f, 0.66f, 1f); m.lightIntensity *= 0.38f;
                m.fog *= new Color(0.3f, 0.34f, 0.48f); m.fogDensity *= 1.25f;
                break;
        }
        if (w.rain)
        {
            m.sky *= 0.85f; m.equator *= 0.85f; m.ground *= 0.85f;
            m.light *= new Color(0.85f, 0.9f, 1f); m.lightIntensity *= 0.8f;
            m.fog = Color.Lerp(m.fog, new Color(0.45f, 0.5f, 0.55f) * (w.phase == DayPhase.Night ? 0.4f : 1f), 0.5f);
            m.fogDensity *= 1.5f;
        }
        m.sky.a = m.equator.a = m.ground.a = m.light.a = m.fog.a = 1f;
        return m;
    }
}
