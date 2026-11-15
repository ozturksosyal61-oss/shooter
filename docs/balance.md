# Denge — PlaytestBot sonuçları

Botlar oyunu gerçek girdi yolundan oynar (`Assets/Tests/PlayMode/PlaytestBot.cs`):
- **turret:** hiç kıpırdamaz, sağ yarıya basılı tutar (auto-aim).
- **kite (akıllı):** 16 yönü zombi yakınlığı + bina açıklığına göre puanlayıp kaçar, can paketlerini toplar, sakin anda reload yapar, aktif reload'u tatlı noktada basar.
- **explore:** sürekli kuzeye koşar, çok yaklaşan zombiden sapar, en yakına nişan alır.
- **human:** kite gibi kaçar ama insan gibi kusurlu: aktif reload'u ~%60 tutturur, tepki gecikmeli.
- Tüm botlar seviye atlayınca kart ekranında gerçek tıklamayla kart seçer (tarza göre öncelik).

Çalıştırma (4 bot tek oturumda, ~5 dk): `PLAYTEST_TIMESCALE=8 CAPTURE_DIR=<klasör> Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter PlaytestBot.Play ...` → `<klasör>/playtest_<tarz>.txt`. Opsiyonel `PLAYTEST_STRATEGIES`, `PLAYTEST_SEED`, `PLAYTEST_MAXTIME` (varsayılan 400).

## F6.5 — kamera 17.5 m sonrası (2026-11-15)

Kamera 14 → 17.5 m: görüş alanı büyüdü, zombiler görüş dışında doğduğu için daha uzakta doğuyor → oyun belirgin kolaylaştı.

| Ayar | human | İlk hasar |
|---|---|---|
| Kamera 17.5 m, spawn margin 0.08 + 3 m, AlivePerWave 5 | 406 / 403 sn (400 sn tavan) | 235 / 200 sn |
| **+ spawn margin 0.05 + 1 m, AlivePerWave 6 (final)** | **332 sn** (dalga 7) | 96 sn |

Aynı dönemde: taret artık zombilerin hedefi (yakınındaki zombiler ona saldırır, canı biter), ilk 2 kart seçiminde hasar kartı güvencesi. Turret/kite/explore bu turda ölçülmedi (bir sonraki denge turunda).

## Hızlı bot altyapısı — 8x / 3x doğrulaması (2026-11-15)

Her kare `Time.captureDeltaTime` ile tam 1/60 sn oyun süresi (60 FPS cihaz gibi), botun zamanlayıcıları oyun saatinde. Aynı seed (4242), `-nographics`, 400 sn tavan:

| Bot | 8x | 3x | Gerçek hız (8x / 3x) |
|---|---|---|---|
| turret | 116 sn | 106 sn | 3.8x / 2.2x |
| human | 251 sn | 290 sn | 4.3x / 2.2x |
| kite | 296 sn | 209 sn | 3.3x / 2.1x |
| explore | 400+ sn (sağ) | 267 sn | 3.7x / 2.0x |
| **4 bot toplam süre** | **306 sn** | **433 sn** | |

- Turret ayrıca tek başına: 105 / 107 sn. Fark, aynı ayarla iki koşu arasındaki oynamadan büyük değil (ör. human 192–398 sn), yani 8x sistematik bir sapma getirmiyor. Kart çekilişi ve spawn zamanlaması tam deterministik değil, tek koşu yine gürültülü.
- Gerçek hız işlemciye bağlı (~4x). 8 yalnızca üst sınır, kare adımı büyütülmez (aksi hâlde mermi/çarpışma doğruluğu bozulur).
- Önceki tablolar 3x + 60 FPS kare sınırıyla ölçüldü (kare başına ~0.05 sn), yenileri 1/60 sn adımla. Küçük fark olabilir.


## F6+F7 sonrası — final (2026-11-15)

Difficulty: BaseInterval 1.3, IntervalDecay 0.83, BaseAlive 12, **AlivePerWave 5**, HealthPerWave 0.18,
BaseDamage 9, **DamagePerWave 1.5**, özel zombi SpecialBase 0.05 + **SpecialPerWave 0.02** (maks 0.25), her 7. dalga özel dalga.
Timescale 3, her bot 2 koşu (rastgele seed). Koşu süresi üst sınırı 600 sn.

| Bot | Hayatta kalma | Dalga | Kill | İsabet* | Seviye | Özel kill | Takılma | Hedef |
|---|---|---|---|---|---|---|---|---|
| turret | **110 / 110 sn** | 3 | 106–117 | %45 | 5 | 0–1 | 0 | 90–120 ✔ |
| human | **321 / 192 sn** (ort. 256) | 5–7 | 121–243 | %59–76 | 6–9 | 4–18 | 0 | 240–360 ✔ ort., yüksek varyans |
| explore | **301 / 256 sn** (ort. 279) | 6–7 | 404–498 | %112–117 | 10–11 | 11–28 | 0 | human'ın biraz üstü ✔ |
| kite | **602 / 605 sn** (sınır) | 12 | 1203–1428 | %114–147 | 17–18 | 155–168 | 0 | human'ın biraz üstü ✘ çok üstünde |

\* %100 üstü isabet: delici / çift mermi kartları tek atışta birden çok isabet sayar.

### Ayar geçmişi
| Tur | Değişiklik | turret | human | kite | explore |
|---|---|---|---|---|---|
| 0 (F6 başlangıç) | AlivePerWave 6, SpecialPerWave 0.025, DamagePerWave 1.2 | 109 | 241 | 206 | 243 |
| 1 | AlivePerWave 6→5, SpecialPerWave 0.025→0.02 | 102 | 398 | 402 | 190 |
| 2 (final) | DamagePerWave 1.2→1.5 | 110 / 110 | 321 / 192 | 602 / 605 | 301 / 256 |

### Notlar
- Tur 0'da kite dalga 5'te 35 eşzamanlı zombide 22 sn içinde öldü (125→0 can) → önce geç dalga yoğunluğu düşürüldü; erken dalgalar (turret) neredeyse aynı kaldı.
- Tek koşular çok gürültülü (human aynı ayarla 192–398 sn): sonucu kart çekilişi belirliyor. Hasar kartı (delici/çift mermi/ateş hızı) erken gelen koşular 300+ sn, savunma kartlarıyla (can çalma, dalga arası can) gelenler ~190 sn.
- kite botu %100 aktif reload ve kusursuz kaçışla bir üst sınırdır; ölçekli kartlarla (3 taret, çift mermi) 600 sn'ye ulaşıyor. Kite'ı hedefe çekmek human/turret'i de düşürecek → bırakıldı. Sonraki adımlar için aday: geç dalgada (8+) yoğunluk artışı veya taret/çift mermi kartlarının yığılmasını sınırlamak.
- Eski "turret 27 sn" ölçümü kart ekranında ateşi bırakmayan bot hatasındandı; düzeltilmiş botla 110 sn.
- Smoke testleri kart seçmez: `GameSmokeTest.LoadGame` doğal seviye atlamayı kapatır (kart testleri `AddXp(XpToNext)` ile tetikler).

## F5 sonrası (Difficulty: BaseInterval 1.3, IntervalDecay 0.83, BaseAlive 12, AlivePerWave 6, HealthPerWave 0.18, runner %8/dalga)

| Bot | Hayatta kalma | Ulaşılan dalga | Kill | İsabet | İlk hasar | Can paketi | Aktif reload | Takılma |
|---|---|---|---|---|---|---|---|---|
| turret | 102–104 sn | 3 | ~100 | %45–49 | ~99. sn | 0–1 | — | 0 |
| kite (akıllı) | **507 sn** | 11 | 570 | %73 | 184. sn | 11 | 124/124 | 0 |
| explore | 267 sn | 6 | 277 | %74 | 106. sn | 6 | — | 0 |

## Karşılaştırma: F4 (dalga yok, zaman bazlı zorluk)

| Bot | Hayatta kalma |
|---|---|
| turret | 71 sn |
| kite (basit) | 85 sn |
| explore | 95 sn |

F4'te beceri fark etmiyordu (71–95 sn). F5'te sabit duran ~100 sn'de ölüyor, iyi oynayan 8+ dakika dayanıyor.

## Notlar
- Ara turda (BaseAlive 10, basit kite botu) kite 188 sn'de köşeye sıkışıp öldü → bot "açık alana kaç" mantığıyla güçlendirildi; oyun tarafında değişiklik gerekmedi.
- Takılma ölçümü: 100 ms üstü kareler profiler marker'larıyla loglanır. Ekran görüntüsü alınan turlarda görülen ~110–250 ms'lik kareler testin kendi PNG kodlamasıdır (45 sn aralıkla, ~1 MB GC).
