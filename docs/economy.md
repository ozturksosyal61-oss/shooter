# Ekonomi — fiyat tablosu ve denge (F13)

Kaynak: `Assets/Scripts/Core/Economy.cs` (saf kurallar, EditMode testli `EconomyTests`). Değişiklikte bu tabloyu güncelle.

## Kazanç
| Kaynak | Miktar |
|---|---|
| Tur sonu altını (sonsuz) | `12 × dalga + 0.25 × kill + 30 × görev + 150 × boss + skor / 200` × çarpan |
| Çarpan | 1 + VIP 0.25 + Altın Takviyesi 0.10 (en fazla ×1.35) |
| Reklam (ödüllü) | tur sonu altını 2× (VIP: reklamsız) |
| Günlük giriş (7 gün döngü, gün kaçarsa baştan) | 100 · 150 · 200 · 250+2💎 · 300 · 400 · 500+10💎 (hafta: 1900 altın + 12 elmas) |
| Günlük görevler (3 adet, havuzdan seed'li) | 100–250 altın (200 zombi 150, 25 özel 150, 1 boss 250, dalga 8 200, 3 görev 150, 3 tur 100, 50 kombo 150) |
| Günlük ücretsiz elmas (reklam) | 5 elmas |

**Tipik tur** (human botu: dalga 6–9, 120–240 kill, 1–2 görev, ~8–15k skor): ≈ 200–300 altın; reklamla 400–600.
**Tipik gün** (4–5 tur + giriş + görevler): ≈ 1500–2000 altın.

## Harcama
| Öğe | Fiyat |
|---|---|
| M4 | başlangıç (ücretsiz) |
| SMG | 900 altın (~3–4 tur) |
| Pompalı | 1200 altın (~4–5 tur) |
| Ağır makineli | 1500 altın (~5–6 tur) |
| Keskin nişancı | 1800 altın (~6–7 tur) |
| Yükseltme (hasar +%8 / şarjör +%12 / reload −%7 seviye başına, 5 seviye) | taban hasar 160, şarjör/reload 120 × silah katsayısı (1.0–1.4) × 1.55^seviye |
| M4 yükseltmeleri (tümü) | 5790 · SMG 6350 · Pompalı 6930 · Ağır 7500 · Keskin 8090 → **toplam 34 660** |
| Kostümler (asker derileri, mağazada "Kostümler") | Orman 600, Çöl 800, Gece 1200, Kutup 1500 altın · Altın 200 elmas · Komando yalnız başlangıç paketinde |
| Karakterler (tam model, mağazada "Karakterler") | Özel Harekat 250 elmas · İtfaiyeci 250 elmas (`Economy.CharacterGems`; = 149,99 TL paketi) |
| Başlangıç canı (+10 can/seviye, 3 seviye) | 800 / 1600 / 3200 |
| Başlangıç kartı (1 tur, tüketilir) | 250 |
| Altın takviyesi +%10 (kalıcı) | 4000 |
| Elmasla devam (turda 1 kez) | 20 elmas (alternatif: reklam) |
| Elmas → altın | 40💎 → 1000, 100💎 → 3000 |

**Toplam altın hedefi:** silahlar 5400 + yükseltmeler 34 660 + bonuslar 9600 + kostümler 4100 ≈ 54 000 → günde ~1800 ile **~4 hafta** (yalnız silah + yükseltme ≈ 3 hafta). VIP / takviye ile ~%25–35 kısalır.

## Pay-to-win sınırı
- Gerçek parayla güç satılmaz: elmas yalnız kostüm, devam, altın takası; altın takası günlük kazancın ~1–1.5 günü kadar.
- Kalıcı güç küçük: tam yükseltilmiş silah +%40 hasar / +%60 şarjör / −%35 reload; tur içi kartlar (çift mermi, delici, kritik…) bunun çok üstünde kalır. Başlangıç canı en fazla +30.

## Mağaza ürünleri (IAP, `Economy.Products`)
Fiyatlar TL (`ProductDef.tl`; mağaza bağlıyken `localizedPriceString` gelir, App Store Connect / Play Console'da aynı TRY fiyatlarını seç). Kartta "250 ELMAS" + buton "149,99 TL". Büyük paket lira başına daha çok elmas verir (EditMode testli); bir karakter ≈ 150 TL.

| ID | Tür | Fiyat (TL) | İçerik | elmas/TL | TR ad | EN ad |
|---|---|---|---|---|---|---|
| `com.oztdigital.holdout.gems_small` | Consumable | 49,99 | 70 elmas | 1.40 | Avuç Dolusu Elmas | Handful of Gems |
| `com.oztdigital.holdout.gems_medium` | Consumable | 99,99 | 150 elmas | 1.50 | Elmas Kesesi | Pouch of Gems |
| `com.oztdigital.holdout.gems_large` | Consumable | 149,99 | 250 elmas (= 1 karakter, "1 KARAKTER" etiketi) | 1.67 | Elmas Sandığı | Chest of Gems |
| `com.oztdigital.holdout.gems_huge` | Consumable | 299,99 | 550 elmas | 1.83 | Elmas Hazinesi | Gem Hoard |
| `com.oztdigital.holdout.starter_pack` | Non-Consumable | 79,99 | 150 elmas + 2000 altın + Komando kostümü (tek sefer) | — | Başlangıç Paketi | Starter Pack |
| `com.oztdigital.holdout.vip` | Non-Consumable | 199,99 | Reklamsız ödüller + %25 altın | — | VIP | VIP |