using System;
using System.Collections.Generic;
using UnityEngine;

public enum Lang { Tr = 0, En = 1 }

/// <summary>
/// The single localization table: every player-facing string lives here (columns: Türkçe, English).
/// Use <see cref="T"/> for plain strings and <see cref="F"/> for formatted ones; static UI labels use
/// <see cref="LocText"/>. First release is Turkish; English ("Last Patrol: Zombie Shooter") is filled in ahead.
/// Language is stored in PlayerPrefs <c>lang</c> ("tr"/"en").
/// </summary>
public static class Loc
{
    public const string PrefKey = "lang";
    public static event Action Changed;

    static Lang current = Lang.Tr;
    static bool loaded;

    public static Lang Current
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                try { current = PlayerPrefs.GetString(PrefKey, "tr") == "en" ? Lang.En : Lang.Tr; } catch { current = Lang.Tr; }
            }
            return current;
        }
        set
        {
            loaded = true;
            if (current == value) return;
            current = value;
            try { PlayerPrefs.SetString(PrefKey, value == Lang.En ? "en" : "tr"); } catch { }
            Changed?.Invoke();
        }
    }

    /// <summary>Looks a key up in the current language. Missing keys return the key itself (and fall back to Turkish).</summary>
    public static string T(string key) => T(key, Current);

    public static string T(string key, Lang lang)
    {
        if (Table.TryGetValue(key, out var row))
        {
            var s = row[(int)lang];
            return string.IsNullOrEmpty(s) ? row[0] : s;
        }
#if UNITY_EDITOR
        Debug.LogWarning("Loc: missing key " + key);
#endif
        return key;
    }

    public static string F(string key, object a) => string.Format(T(key), a);
    public static string F(string key, object a, object b) => string.Format(T(key), a, b);
    public static string F(string key, params object[] args) => string.Format(T(key), args);

    /// <summary>Upper-cases for the current language (Turkish i→İ, ı→I) without culture lookups.</summary>
    public static string Upper(string s)
    {
        if (Current == Lang.Tr) s = s.Replace('i', 'İ').Replace('ı', 'I');
        return s.ToUpperInvariant();
    }

    public static bool Has(string key) => Table.ContainsKey(key);
    public static IEnumerable<string> Keys => Table.Keys;
    public static int LanguageCount => 2;

    static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
    {
        // --- Game
        ["game.title"] = new[] { "Son Devriye", "Last Patrol" },
        ["game.store_title"] = new[] { "Son Devriye: Zombi Avı", "Last Patrol: Zombie Shooter" },

        // --- HUD
        ["hud.health"] = new[] { "CAN", "HP" },
        ["hud.level"] = new[] { "SV {0}", "LV {0}" },
        ["hud.wave"] = new[] { "DALGA {0}", "WAVE {0}" },
        ["hud.combo_kills"] = new[] { "{0} KİLL", "{0} KILLS" },
        ["hud.reload_button"] = new[] { "DOLDUR", "RELOAD" },
        ["hud.reload_hint"] = new[] { "ŞARJÖR · doğru anda tekrar bas", "RELOADING · tap again at the right time" },
        ["hud.jam"] = new[] { "TUTUKLUK!", "JAMMED!" },
        ["hud.perfect"] = new[] { "MÜKEMMEL!", "PERFECT!" },
        ["hud.breather"] = new[] { "DALGA {0} · {1}\n<size=26>Nefes al — can yenileniyor</size>", "WAVE {0} · {1}\n<size=26>Catch your breath — healing</size>" },
        ["wave.sub.first"] = new[] { "Devriye başladı", "The patrol begins" },
        ["wave.sub.special"] = new[] { "ÖZEL DALGA — mutantlar geliyor!", "SPECIAL WAVE — mutants incoming!" },
        ["wave.sub.first_special"] = new[] { "Dikkat: laboratuvar mutantları!", "Warning: lab mutants!" },
        ["wave.sub.more"] = new[] { "Daha fazlası geliyor!", "More are coming!" },

        // --- Tutorial
        ["tut.left"] = new[] { "SOL TARAF\n<size=34>parmağını koy ve sürükle → HAREKET</size>", "LEFT SIDE\n<size=34>touch and drag → MOVE</size>" },
        ["tut.right"] = new[] { "SAĞ TARAF\n<size=34>basılı tut → ATEŞ (en yakına nişan)\nsürükle → istediğin yöne</size>", "RIGHT SIDE\n<size=34>hold → FIRE (auto-aim nearest)\ndrag → aim anywhere</size>" },

        // --- Game over
        ["over.title"] = new[] { "ÖLDÜN", "YOU DIED" },
        ["over.record"] = new[] { "YENİ REKOR!", "NEW RECORD!" },
        ["over.again"] = new[] { "TEKRAR", "AGAIN" },
        ["over.stats"] = new[]
        {
            "SKOR  <b>{0}</b>   <size=30>(en iyi {1})</size>\n<size=40>Süre {2}   ·   Dalga {3}   ·   {4} kill   ·   En iyi kombo {5}</size>",
            "SCORE  <b>{0}</b>   <size=30>(best {1})</size>\n<size=40>Time {2}   ·   Wave {3}   ·   {4} kills   ·   Best combo {5}</size>",
        },

        // --- Card picker
        ["cards.header"] = new[] { "SEVİYE {0}  ·  bir kart seç", "LEVEL {0}  ·  pick a card" },
        ["cards.new"] = new[] { "YENİ", "NEW" },
        ["cards.level"] = new[] { "SEVİYE {0} / {1}", "LEVEL {0} / {1}" },
        ["rarity.common"] = new[] { "SIRADAN", "COMMON" },
        ["rarity.rare"] = new[] { "NADİR", "RARE" },
        ["rarity.epic"] = new[] { "EPİK", "EPIC" },

        // --- Cards: card.<CardId>.title / .1 .2 .3 (description per level)
        ["card.Pierce.title"] = new[] { "Delici Mermi", "Piercing Rounds" },
        ["card.Pierce.1"] = new[] { "Mermi 1 zombiden geçer", "Bullets pass through 1 zombie" },
        ["card.Pierce.2"] = new[] { "Mermi 2 zombiden geçer", "Bullets pass through 2 zombies" },
        ["card.Pierce.3"] = new[] { "Mermi 3 zombiden geçer", "Bullets pass through 3 zombies" },
        ["card.FireRate.title"] = new[] { "Hızlı Tetik", "Hair Trigger" },
        ["card.FireRate.1"] = new[] { "+%20 ateş hızı", "+20% fire rate" },
        ["card.FireRate.2"] = new[] { "+%40 ateş hızı", "+40% fire rate" },
        ["card.FireRate.3"] = new[] { "+%60 ateş hızı", "+60% fire rate" },
        ["card.BigMag.title"] = new[] { "Geniş Şarjör", "Extended Mag" },
        ["card.BigMag.1"] = new[] { "Şarjör +10", "Magazine +10" },
        ["card.BigMag.2"] = new[] { "Şarjör +20", "Magazine +20" },
        ["card.BigMag.3"] = new[] { "Şarjör +30", "Magazine +30" },
        ["card.FastReload.title"] = new[] { "Seri Doldurma", "Quick Reload" },
        ["card.FastReload.1"] = new[] { "Reload %20 hızlı", "Reload 20% faster" },
        ["card.FastReload.2"] = new[] { "Reload %36 hızlı", "Reload 36% faster" },
        ["card.FastReload.3"] = new[] { "Reload %49 hızlı", "Reload 49% faster" },
        ["card.DoubleShot.title"] = new[] { "Çift Mermi", "Double Shot" },
        ["card.DoubleShot.1"] = new[] { "+1 mermi (her biri %70 hasar)", "+1 bullet (70% damage each)" },
        ["card.DoubleShot.2"] = new[] { "+2 mermi", "+2 bullets" },
        ["card.DoubleShot.3"] = new[] { "+3 mermi", "+3 bullets" },
        ["card.Crit.title"] = new[] { "Keskin Nişan", "Sharpshooter" },
        ["card.Crit.1"] = new[] { "+%10 kritik şansı", "+10% crit chance" },
        ["card.Crit.2"] = new[] { "+%20 kritik şansı", "+20% crit chance" },
        ["card.Crit.3"] = new[] { "+%30 kritik şansı", "+30% crit chance" },
        ["card.KillBlast.title"] = new[] { "Patlayan Leş", "Volatile Corpses" },
        ["card.KillBlast.1"] = new[] { "Kill'de %15 patlama", "15% blast on kill" },
        ["card.KillBlast.2"] = new[] { "Kill'de %25 patlama", "25% blast on kill" },
        ["card.KillBlast.3"] = new[] { "Kill'de %35 patlama", "35% blast on kill" },
        ["card.MaxHealth.title"] = new[] { "Zırh", "Armor" },
        ["card.MaxHealth.1"] = new[] { "Maks can +25 (ve iyileşir)", "Max HP +25 (and heals)" },
        ["card.MaxHealth.2"] = new[] { "Maks can +50", "Max HP +50" },
        ["card.MaxHealth.3"] = new[] { "Maks can +75", "Max HP +75" },
        ["card.Lifesteal.title"] = new[] { "Can Çalma", "Lifesteal" },
        ["card.Lifesteal.1"] = new[] { "Her kill +1 can", "+1 HP per kill" },
        ["card.Lifesteal.2"] = new[] { "Her kill +2 can", "+2 HP per kill" },
        ["card.Lifesteal.3"] = new[] { "Her kill +3 can", "+3 HP per kill" },
        ["card.MoveSpeed.title"] = new[] { "Çeviklik", "Agility" },
        ["card.MoveSpeed.1"] = new[] { "+%10 hareket hızı", "+10% move speed" },
        ["card.MoveSpeed.2"] = new[] { "+%20 hareket hızı", "+20% move speed" },
        ["card.MoveSpeed.3"] = new[] { "+%30 hareket hızı", "+30% move speed" },
        ["card.MoveSpeed.icon"] = new[] { "HIZ", "SPD" },
        ["card.Magnet.title"] = new[] { "Mıknatıs", "Magnet" },
        ["card.Magnet.1"] = new[] { "Can paketi menzili +%60", "Health pack range +60%" },
        ["card.Magnet.2"] = new[] { "Menzil +%120", "Range +120%" },
        ["card.Magnet.3"] = new[] { "Menzil +%180", "Range +180%" },
        ["card.Shotgun.title"] = new[] { "Pompalı", "Shotgun" },
        ["card.Shotgun.1"] = new[] { "Yakındakilere otomatik pompalı", "Auto shotgun on nearby zombies" },
        ["card.Shotgun.2"] = new[] { "Pompalı daha sık", "Shotgun fires more often" },
        ["card.Shotgun.3"] = new[] { "Pompalı çok sık", "Shotgun fires very often" },
        ["card.Shotgun.icon"] = new[] { "PMP", "SHG" },
        ["card.Turret.title"] = new[] { "Taret", "Turret" },
        ["card.Turret.1"] = new[] { "30 sn taret (45 sn'de bir)", "30 s turret (every 45 s)" },
        ["card.Turret.2"] = new[] { "Taret daha hızlı", "Faster turret" },
        ["card.Turret.3"] = new[] { "Taret çok hızlı", "Much faster turret" },
        ["card.Turret.icon"] = new[] { "TRT", "TRT" },
        ["card.Grenade.title"] = new[] { "Otomatik Bomba", "Auto Grenade" },
        ["card.Grenade.1"] = new[] { "6 sn'de bir kalabalığa bomba", "Grenade into a crowd every 6 s" },
        ["card.Grenade.2"] = new[] { "5 sn'de bir, daha güçlü", "Every 5 s, stronger" },
        ["card.Grenade.3"] = new[] { "4 sn'de bir, en güçlü", "Every 4 s, strongest" },
        ["card.BreatherHeal.title"] = new[] { "Nefes", "Second Wind" },
        ["card.BreatherHeal.1"] = new[] { "Dalga arası tam can", "Full heal between waves" },
        ["card.BreatherHeal.2"] = new[] { "+ dalgada 0.5 can/sn", "+ 0.5 HP/s during waves" },
        ["card.BreatherHeal.3"] = new[] { "+ dalgada 1 can/sn", "+ 1 HP/s during waves" },
    };
}
