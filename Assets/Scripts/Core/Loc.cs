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

    public static Lang Current
    {
        get
        {
            return current;
        }
        set
        {
            // Persisted by Settings (save file); Loc itself stays a pure table.
            if (current == value) return;
            current = value;
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
        ["game.title"] = new[] { "Holdout", "Holdout" },
        ["game.store_title"] = new[] { "Holdout: Zombi Avı", "Holdout: Zombie Survival" },
        ["game.story"] = new[] { "Son Devriye", "Last Patrol" },
        ["menu.play"] = new[] { "OYNA", "PLAY" },
        ["menu.shop"] = new[] { "MAĞAZA", "SHOP" },
        ["menu.settings"] = new[] { "AYARLAR", "SETTINGS" },
        ["menu.campaign"] = new[] { "KAMPANYA", "CAMPAIGN" },
        ["menu.campaign_sub"] = new[] { "Son Devriye", "Last Patrol" },
        ["menu.endless"] = new[] { "SONSUZ", "ENDLESS" },
        ["menu.endless_sub"] = new[] { "Dayanabildiğin kadar", "Hold out as long as you can" },
        ["menu.back"] = new[] { "GERİ", "BACK" },
        ["menu.tagline"] = new[] { "ZOMBİ AVI", "ZOMBIE SURVIVAL" },
        ["menu.soon"] = new[] { "YAKINDA", "COMING SOON" },
        ["menu.locked"] = new[] { "YAKINDA", "SOON" },
        ["menu.splash"] = new[] { "OZT DIGITAL", "OZT DIGITAL" },
        ["menu.splash_sub"] = new[] { "sunar", "presents" },
        ["set.title"] = new[] { "AYARLAR", "SETTINGS" },
        ["set.music"] = new[] { "Müzik", "Music" },
        ["set.sfx"] = new[] { "Efektler", "Effects" },
        ["set.vibration"] = new[] { "Titreşim", "Vibration" },
        ["set.on"] = new[] { "AÇIK", "ON" },
        ["set.off"] = new[] { "KAPALI", "OFF" },
        ["set.fps"] = new[] { "Kare hızı", "Frame rate" },
        ["set.quality"] = new[] { "Grafik", "Graphics" },
        ["set.q0"] = new[] { "DÜŞÜK", "LOW" },
        ["set.q1"] = new[] { "ORTA", "MEDIUM" },
        ["set.q2"] = new[] { "YÜKSEK", "HIGH" },
        ["set.joy_size"] = new[] { "Joystick boyutu", "Joystick size" },
        ["set.joy_alpha"] = new[] { "Joystick saydamlığı", "Joystick opacity" },
        ["set.lang"] = new[] { "Dil", "Language" },
        ["set.support"] = new[] { "DESTEK", "SUPPORT" },
        ["set.privacy"] = new[] { "GİZLİLİK", "PRIVACY" },
        ["set.reset"] = new[] { "İLERLEMEYİ SIFIRLA", "RESET PROGRESS" },
        ["set.reset_q"] = new[] { "Rekorlar, para ve ilerleme silinsin mi? Bu geri alınamaz.", "Delete records, currency and progress? This cannot be undone." },
        ["set.reset_done"] = new[] { "İlerleme sıfırlandı", "Progress reset" },
        ["set.yes"] = new[] { "EVET, SİL", "YES, DELETE" },
        ["set.no"] = new[] { "VAZGEÇ", "CANCEL" },
        ["set.close"] = new[] { "TAMAM", "DONE" },
        ["pause.title"] = new[] { "DURAKLATILDI", "PAUSED" },
        ["pause.resume"] = new[] { "DEVAM", "RESUME" },
        ["pause.menu"] = new[] { "ANA MENÜ", "MAIN MENU" },
        ["load.title"] = new[] { "YÜKLENİYOR", "LOADING" },
        ["load.tip"] = new[] { "İPUCU", "TIP" },
        ["tip.1"] = new[] { "Kasap hücumdan sonra nefes nefese kalır: o an iki kat hasar alır.", "The Butcher is winded after a charge: it takes double damage then." },
        ["tip.2"] = new[] { "Kasap'ı bir binaya doğru koşturursan duvara çarpıp sersemler.", "Lure the Butcher's charge into a building and it stuns itself." },
        ["tip.3"] = new[] { "Kalkanlı zombiyi yandan ya da arkadan vur.", "Shoot the Shield zombie from the side or behind." },
        ["tip.4"] = new[] { "Bombalıyı kalabalığın ortasında patlat.", "Pop the Bomber in the middle of a crowd." },
        ["tip.5"] = new[] { "Dil seni yakalarsa dile ateş et — kopar.", "Grabbed by a tongue? Shoot the tongue to snap it." },
        ["tip.6"] = new[] { "Doldururken tatlı noktada tekrar bas: anında dolar, sonraki şarjör güçlü.", "Tap again in the sweet spot while reloading: instant reload and a stronger next mag." },
        ["tip.7"] = new[] { "Joystick'i sonuna kadar itip alçak engele koş: üstünden atlarsın.", "Push the stick all the way into low cover to vault over it." },
        ["tip.8"] = new[] { "Kırmızı variller zincirleme patlar — zombileri onlara çek.", "Red barrels chain-explode — lead the horde past them." },
        ["tip.9"] = new[] { "Nefes arasında can yenilenir; hedeflere o zaman koş.", "Health regenerates between waves; run for objectives then." },
        ["tip.10"] = new[] { "Hasar almak komboyu kırar. Mesafeni koru.", "Taking damage breaks your combo. Keep your distance." },

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
        ["tut.left"] = new[] { "SOL TARAF\n<size=34>parmağını koy ve sürükle » HAREKET</size>", "LEFT SIDE\n<size=34>touch and drag » MOVE</size>" },
        ["tut.right"] = new[] { "SAĞ TARAF\n<size=34>basılı tut » ATEŞ (en yakına nişan)\nsürükle » istediğin yöne</size>", "RIGHT SIDE\n<size=34>hold » FIRE (auto-aim nearest)\ndrag » aim anywhere</size>" },

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
        ["cards.levelup"] = new[] { "SEVİYE ATLADIN!", "LEVEL UP!" },
        ["cards.header"] = new[] { "Seviye {0}  ·  bir kart seç", "Level {0}  ·  pick a card" },
        ["cards.new"] = new[] { "YENİ", "NEW" },
        ["cards.level"] = new[] { "SEVİYE {0} / {1}", "LEVEL {0} / {1}" },
        ["rarity.common"] = new[] { "SIRADAN", "COMMON" },
        ["rarity.rare"] = new[] { "NADİR", "RARE" },
        ["rarity.epic"] = new[] { "EPİK", "EPIC" },

        // --- Story objectives (F8): radio lines ({0} = direction word, {1} = seconds)
        ["radio.label"] = new[] { "TELSİZ", "RADIO" },
        ["radio.flare"] = new[] { "Merkez: İşaret fişeği {0} karakolda!", "HQ: There's a signal flare at the police station {0}!" },
        ["radio.flare_done"] = new[] { "Fişek elimde. Merkez: Güzel iş asker, sakla onu.", "Got the flare. HQ: Good work, soldier — keep it safe." },
        ["radio.tower"] = new[] { "Merkez: {0} sinyal kulesini çalıştır, alanda {1} sn dayan!", "HQ: Power up the signal tower {0} and hold the area for {1} s!" },
        ["radio.tower_half"] = new[] { "Sinyal %50... biraz daha dayan!", "Signal at 50%... hold on a little longer!" },
        ["radio.tower_lost"] = new[] { "Sinyal zayıflıyor — kulenin yanına dön!", "Signal fading — get back to the tower!" },
        ["radio.tower_done"] = new[] { "Sinyal kuruldu! Merkez: Seni duyuyoruz, asker.", "Signal up! HQ: We hear you, soldier." },
        ["radio.supply_hospital"] = new[] { "Merkez: {0} hastane girişine ikmal sandığı bıraktık.", "HQ: We dropped a supply crate at the hospital entrance {0}." },
        ["radio.supply_gas"] = new[] { "Merkez: {0} benzin istasyonunda ikmal sandığı var.", "HQ: There's a supply crate at the gas station {0}." },
        ["radio.supply_heal"] = new[] { "İkmal: sağlık çantası — yaraların sarıldı!", "Supply: med kit — you're patched up!" },
        ["radio.supply_card"] = new[] { "İkmal: yeni teçhizat — birini seç!", "Supply: new gear — pick one!" },
        ["radio.heli_soon"] = new[] { "Merkez: Görevler tamam. Helikopter yolda, iniş noktası geliyor!", "HQ: Tasks done. The chopper is on its way — landing zone incoming!" },
        ["radio.heli"] = new[] { "Merkez: Helikopter iniyor! {0} iniş noktasına git, {1} sn dayan!", "HQ: Chopper inbound! Get to the landing zone {0} and hold for {1} s!" },
        ["radio.heli_close"] = new[] { "Pilot: Görüyorum seni! Alanı temiz tut, iniyorum!", "Pilot: I see you! Keep the zone clear, coming down!" },
        ["radio.heli_lost"] = new[] { "Pilot: Neredesin asker? İniş noktasına dön!", "Pilot: Where are you, soldier? Back to the landing zone!" },
        ["radio.heli_done"] = new[] { "Helikopter yine indi... ama seni bekleyemedi.", "The chopper landed again... but it couldn't wait for you." },
        ["radio.heli_after"] = new[] { "Pilot: Çok kalabalık, üzgünüm! Merkez: Devriyeye devam et, yine geleceğiz.", "Pilot: Too many of them, sorry! HQ: Keep patrolling, we'll be back." },
        ["dir.n"] = new[] { "kuzeydeki", "to the north" },
        ["dir.ne"] = new[] { "kuzeydoğudaki", "to the north-east" },
        ["dir.e"] = new[] { "doğudaki", "to the east" },
        ["dir.se"] = new[] { "güneydoğudaki", "to the south-east" },
        ["dir.s"] = new[] { "güneydeki", "to the south" },
        ["dir.sw"] = new[] { "güneybatıdaki", "to the south-west" },
        ["dir.w"] = new[] { "batıdaki", "to the west" },
        ["dir.nw"] = new[] { "kuzeybatıdaki", "to the north-west" },
        ["dir.here"] = new[] { "yakındaki", "nearby" },
        // HUD objective tracker / banners
        ["obj.Flare"] = new[] { "İŞARET FİŞEĞİNİ AL", "GRAB THE SIGNAL FLARE" },
        ["obj.Tower"] = new[] { "SİNYAL KULESİNİ ÇALIŞTIR", "POWER UP THE SIGNAL TOWER" },
        ["obj.Supply"] = new[] { "İKMAL SANDIĞINI AÇ", "OPEN THE SUPPLY CRATE" },
        ["obj.Helicopter"] = new[] { "İNİŞ NOKTASINDA DAYAN", "HOLD THE LANDING ZONE" },
        ["obj.distance"] = new[] { "{0} m", "{0} m" },
        ["obj.hold"] = new[] { "{0} sn", "{0} s" },
        ["obj.done"] = new[] { "GÖREV TAMAM!", "OBJECTIVE COMPLETE!" },
        ["obj.done_sub"] = new[] { "+{0} puan", "+{0} points" },
        ["obj.heli_title"] = new[] { "HELİKOPTER KALKTI", "THE CHOPPER IS GONE" },
        ["obj.heli_sub"] = new[] { "...ama seni bekleyemedi. +{0} puan", "...but it couldn't wait for you. +{0} points" },
        ["cards.reward"] = new[] { "İKMAL!", "SUPPLIES!" },
        ["cards.reward_sub"] = new[] { "Ödül  ·  bir kart seç", "Reward  ·  pick a card" },
        ["cards.boss"] = new[] { "KASAP DÜŞTÜ!", "BUTCHER DOWN!" },
        ["cards.boss_sub"] = new[] { "Boss ödülü  ·  epik kart garanti", "Boss reward  ·  epic card guaranteed" },
        ["boss.butcher"] = new[] { "KASAP", "BUTCHER" },
        ["boss.butcher.sub"] = new[] { "Laboratuvar mutasyonu", "Lab mutation" },
        ["boss.weak"] = new[] { "ZAYIF!", "WEAK!" },
        ["boss.stunned"] = new[] { "SERSEM!", "STUNNED!" },
        ["boss.rage"] = new[] { "ÖFKELENDİ!", "ENRAGED!" },
        ["wave.sub.boss"] = new[] { "BOSS DALGASI — Kasap geliyor!", "BOSS WAVE — the Butcher is coming!" },
        ["radio.boss"] = new[] { "Merkez: Laboratuvardan dev bir sinyal! Kasap peşinde, hücumdan kaç, yorulunca vur!", "HQ: Huge signal from the lab! The Butcher is after you — dodge the charge, hit it when it's winded!" },
        ["radio.boss_down"] = new[] { "Merkez: Kasap düştü! İyi iş asker, devriyeye devam.", "HQ: The Butcher is down! Good work, soldier — keep patrolling." },

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
        ["card.Turret.3"] = new[] { "Çok hızlı + aynı anda 2 taret", "Much faster + 2 turrets at once" },
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
