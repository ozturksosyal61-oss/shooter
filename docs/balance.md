# Denge — PlaytestBot sonuçları

Botlar oyunu gerçek girdi yolundan oynar (`Assets/Tests/PlayMode/PlaytestBot.cs`):
- **turret:** hiç kıpırdamaz, sağ yarıya basılı tutar (auto-aim).
- **kite (akıllı):** 16 yönü zombi yakınlığı + bina açıklığına göre puanlayıp kaçar, can paketlerini toplar, sakin anda reload yapar, aktif reload'u tatlı noktada basar.
- **explore:** sürekli kuzeye koşar, çok yaklaşan zombiden sapar, en yakına nişan alır.
- **human:** kite gibi kaçar ama insan gibi kusurlu: aktif reload'u ~%60 tutturur, tepki gecikmeli.
- Tüm botlar seviye atlayınca kart ekranında gerçek tıklamayla kart seçer (tarza göre öncelik).

Çalıştırma (4 bot tek oturumda, ~5 dk): `PLAYTEST_TIMESCALE=8 CAPTURE_DIR=<klasör> Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter PlaytestBot.Play ...` → `<klasör>/playtest_<tarz>.txt`. Opsiyonel `PLAYTEST_STRATEGIES`, `PLAYTEST_SEED`, `PLAYTEST_MAXTIME` (varsayılan 400).

## F9 — ilk boss Kasap (2026-11-16)

İlk boss dalga 6 (sonra 11, 16…); dalga 4–6 yoğunluk artışı yavaşlatıldı (`Difficulty.DensityDip` 1 / 1.6 / 2); kule alanı baskısı ×0.75 → ×0.85; boss dalgasında normal spawn aralığı ×4, eşzamanlı ×0.2. Human bot can < %35 iken tutma alanından çekilir. 600 sn tavan, human ×3 doğal koşu.

| Tur | Ayar | Boss'a ulaşan | Yenen | Dövüş | Hayatta kalma |
|---|---|---|---|---|---|
| 0 | boss dalga 10 | 0/3 | — | — | 205–218 sn (dalga 5, kule) |
| 1 | dalga 6, düşüş, geri çekilme; can ×42 | 2/3 | 2/2 | 30–36 sn | 203–292 sn |
| 2 | can ×65, hücum 15 m/s, hasar 28/26 | 3/3 | 3/3 | 27–61 sn | 319–485 sn |

**F9.1 — daha tehlikeli Kasap** (hücum takibi, öfkede çift hücum, uzak oyuncuya enkaz, şok halkası). `PLAYTEST_BOSS=1`, human ×3:

| Tur | Ayar | Yenen | Dövüş | Boss'tan isabet (hücum / yer vuruşu / fırlatma sayısı) |
|---|---|---|---|---|
| 0 | can ×65, takip 70°/sn, enkaz 18 / 2.4 m | 3/3 | 38–77 sn | 3 / 0 / 32 |
| 1 | can ×85, takip 110°/sn, enkaz 24 / 2.8 m, şok ×0.4 | 3/3 | 57–106 sn | 3 / 3 / 33 |

Hâlâ %50 galibiyetin altında değil; gerçek oyuncu testi bekleniyor. Önceki not: Kasap'ın saldırıları botlara neredeyse hiç isabet etmiyor (hücum 0 isabet / 15); zorluk boss dalgasındaki kalabalıktan geliyor. Hedef (~%50 galibiyet) için sonraki adım: hücumun takibi / ikinci hücum, boss dalgasında daha fazla özel zombi.

## F8 — şehir çeşitliliği + hikâye hedefleri (2026-11-16)

Değişiklik: yeni chunk tipleri (park, meydan, ara sokak, yıkıntı, simge yapılar), hedefler (dalga 2'den itibaren her 3 dalgada bir; fişek / kule 20 sn / ikmal), 3 hedef → helikopter (30 sn dayan). Hedef alanında dayanırken spawn aralığı ×0.75. Botlar (turret hariç) aktif hedefe gider: kite/human yolda zombilerden kaçarak (16 yön, hedefe hizalanma + zombi yakınlığı + bina), alanda kalıp kaçar; explore düz hedefe koşar. **turret** referans olarak yerinde kalır (hedef almaz). Difficulty değişmedi.
8x, `-nographics`, 400 sn tavan, rastgele seed, 2 tur:

| Bot | Hayatta kalma (tur 1 / tur 2) | Dalga | Kill | Hedef (verilen → biten) | Ort. hedef süresi | Takılma |
|---|---|---|---|---|---|---|
| turret | **101 / 105 sn** | 3 | 94–103 | 1 → 0 (gitmez) | — | 0 |
| human | **250 / 400+ sn** (tavan) | 6–9 | 285–646 | 2→2 / 4→3 (helikopter açıldı) | 13–26 sn | 0 |
| kite | **249 / 400+ sn** (tavan) | 6–9 | 383–926 | 2→2 / 4→3 (helikopter açıldı) | 29–35 sn | 0 |
| explore | **189 / 242 sn** | 5–6 | 222–357 | 2→1 / 2→2 | 29–30 sn | 0 |

- "400+": koşu 400 sn tavanında kesildi (son satırdaki `dead=True` tavan sonrası girdisiz geçen ~12 sn'den; helikopter alanında bekliyordu).
- Hedefe yürümek risk getiriyor: kite F6+F7'deki 600 sn sınırından düştü (artık kaçmak yerine görev için kalabalığın içinden geçiyor), explore ~280 → ~215 sn. Human hedef aralığında (240–360, yüksek varyans). Helikopter iyi oynayan için ~355–365. sn'de (dalga 8–9) açılıyor; 400 sn tavanında tamamlanan olmadı.
- Hedef ödülleri (tam can / kart) uzun koşularda belirgin; ilk hedef (dalga 2, ~41. sn) 10–37 sn'de bitiyor.

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

## F12 — Zor mod (2026-11-19)
`PLAYTEST_MODE=hard`, timescale 8, M4. Kurallar `ModeRules` (`Assets/Scripts/Core/GameMode.cs`).

| Tur | Ayar | human | kite | turret |
|---|---|---|---|---|
| 0 | can ×1.45, hasar ×1.5, yedek 3 şarjör, kutu %9 (az kalınca %30) | 93 sn / dalga 3 (12 sn mermisiz) | 62 sn / dalga 2 (27 sn mermisiz) | 28 sn |
| 1 | can ×1.3, hasar ×1.4, yedek 5 şarjör, kutu %16 (az kalınca %45, 1.5 sn arayla), mıknatıs 8 m, ikmal +3 şarjör | **143 sn / dalga 4** (10 kutu, 0 sn mermisiz) | **194 sn / dalga 5** (23 kutu, boss'a ulaştı) | — |

- Sonsuz modda human ~190–400 sn, kite 507–600 sn → zor modda süre ~1/3. Mermi artık "sürekli hareket et, kutuları topla" baskısı yaratıyor ama kesin ölüm sebebi değil.
- Turret botu yerinde durduğu için kutu toplayamıyor; zor mod bilerek hareketi ödüllendiriyor.

## F14 — Yeni boss'lar (2026-11-19)
Aynı koşul: `PLAYTEST_BOSS=1` (dalga 6, seviye 8), `PLAYTEST_BOSS_TYPE=<tip>`, seed 7, timescale 8. Normal oyunda Sıçrayan dalga 11, Kovan Ana 16, Ezici 21'de (zor modda 9 / 13 / 17) gelir — oralarda dalga zorluğu ve boss büyüme çarpanı da eklenir.

| Boss | boy | can (Kasap=1) | human | kite |
|---|---|---|---|---|
| Kasap | 4.9 m | 1.00 | yendi 73 sn (12 isabet) | yendi 37 sn |
| Sıçrayan | 5.4 m | 1.15 | yendi 76 sn (3 iniş isabeti) | yendi 57 sn |
| Kovan Ana | 5.8 m | 1.35 | öldü, boss %52 (3 yavru dalgası) | yendi 114 sn (9 yavru) |
| Ezici | 6.1 m | 1.65 | öldü, boss %41 (129 zırh isabeti) | öldü, boss %12 (23 engel ezdi) |

- Sıralama tuttu: her mutasyon bir öncekinden uzun sürüyor / daha ölümcül. Ezici'de botlar önden ateş ediyor (zırh) → oyuncu arkasına geçmeyi öğrenmeli; telsiz + "ZIRH!" yazısı bunu söylüyor.
- Yüksek dalgaya atlayan senaryo (`PLAYTEST_BOSS_WAVE=11/16`) temsilî değil: botlar boss'tan değil dalga 16'nın normal / bombalı zombilerinden ölüyor (4 isabette 100 can).

## Aim assist + variller (2026-11-19)
Auto-aim kaldırıldı → `AimAssistConfig` (herkese ve her moda aynı): sürüklerken ±5° içinde açının %30'u düzeltilir; sürüklemeden basılı tutunca yürüme / basış anındaki bakış yönüne ateş, ±8° içindeki en yakın zombiye açının %50'si, dönüş ≤ 300°/sn (sabit taban → kilitlenme yok). Dilci yakalayınca silah Dilci'de kalır (mekanik gereği). Variller: can 150 (~6 M4 mermisi), yarı canda duman + kıvılcım, yarıçap 4.5 → 3 m, zombi 220 / asker 40 → 30, zincir 0.2 sn (yarıçap + 0.8 m); sıklık: yol kenarı patlayan pay %60 → %70, kaldırım %30 → %42, ara sokak %15 → %24, yıkıntı (sanayi / liman) %35 → %45.

Seed 7, timescale 8, 600 sn tavan:

| Bot | süre | dalga | kill | isabet | not |
|---|---|---|---|---|---|
| turret (yerinde, sürüklemeden basılı) | 15 sn | 1 | 1 | %3 | eskiden auto-aim ile ~100 sn; artık nişan almayan oyuncu cezalı (bilerek) |
| human (nişan + gecikme) | 321 sn | 6 | 285 | %65 | Kasap'a ulaştı (%30 can kaldı); önceki bant 190–400 sn |
| kite | 355 sn | 8 | 681 | %93 | Kasap'ı 28 sn'de yendi, 7 varil; önceki 507–600 sn (tek seed, varilden ölümler dahil) |
| explore | 247 sn | 6 | 347 | %85 | |

- Nişan alan botlar önceki bantta → zombi canı değiştirilmedi. Botlar fareyle tam nişan alıyor; gerçek dokunmatik sürüklemede isabet daha düşük olacak — cihaz testinden sonra erken dalga canı (`Difficulty.HealthPerWave`) gerekirse düşürülür.

## 1.0.1 — playtest-02 düzeltmeleri (2026-11-21)

Değişiklikler: ilk 6 dalga `Difficulty.EarlyDensity` {0.7, 1.4, 1.8, 2.1, 2.5, 3} (dalga 1–2 daha kalabalık, 3–6 yumuşak), taktik reload 0.6 sn / < %70, aile karışık kart teklifleri (sahip olunana ×1.7), 5 gizli sinerji, boss dalgasında yeni hedef yok. Botlar tek oturum, timescale 8.

| Bot | süre | dalga | kill | isabet | not |
|---|---|---|---|---|---|
| turret | 16 sn | 1 | 3 | %8 | değişmedi (nişan almayan oyuncu cezalı) |
| human | 308 sn | 7 | 267 | %82 | Kasap'ı 51 sn'de yendi; önceki 321 sn / dalga 6 |
| kite | 469 sn | 10 | 1617 | — | helikopter 1, Kasap 23 sn; önceki 355 sn / dalga 8 |
| explore (tek başına, 150 sn tavan) | 159 sn | 4 | 140 | %55 | aynı oturumda kite'tan sonra koşunca spawn olmuyor (bot oturum sızıntısı, oyunla ilgisi yok) |
