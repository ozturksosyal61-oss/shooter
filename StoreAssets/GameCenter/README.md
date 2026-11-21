# Game Center — App Store Connect kurulumu (0.1.1)

App Store Connect → Holdout → **Özellikler / Features → Game Center**. Kimlikler (ID) koddakiyle (`GameIds`, `Assets/Scripts/Core/GameServices.cs`) **birebir aynı** olmalı. Sonra 0.1.1 sürüm sayfasında **Game Center** kutusunu işaretle ve aşağıdakilerin hepsini sürüme ekle.

## Sıralamalar (Leaderboards)

| Referans adı | Leaderboard ID | Tür | Sıralama | Skor biçimi | TR adı | EN adı |
|---|---|---|---|---|---|---|
| Endless score | `com.oztdigital.holdout.endless_score` | Classic | Yüksek → üstte | Tam sayı | Sonsuz — En İyi Skor | Endless — Best Score |
| Hard wave | `com.oztdigital.holdout.hard_wave` | Classic | Yüksek → üstte | Tam sayı (sonek " dalga" / " waves") | Zor — En Uzun Dalga | Hard — Highest Wave |
| Campaign stars | `com.oztdigital.holdout.campaign_stars` | Classic | Yüksek → üstte | Tam sayı (sonek " ★") | Son Devriye — Yıldız | Last Patrol — Stars |
| Weekly score | `com.oztdigital.holdout.weekly_score` | **Recurring** — başlangıç **Pazartesi 00:00 UTC**, süre **1 hafta** | Yüksek → üstte | Tam sayı | Haftalık Etkinlik | Weekly Event |

Skor gönderme: "En iyi skor" (Best score) seçeneği.

## Başarımlar (Achievements) — her biri 1 kez, "Gizli değil", tekrar edilemez

| ID | Puan | TR başlık / açıklama | EN title / description |
|---|---|---|---|
| `com.oztdigital.holdout.ach_wave5` | 10 | Isınma Turu — Bir turda 5. dalgaya ulaş. | Warm-Up — Reach wave 5 in one run. |
| `com.oztdigital.holdout.ach_wave10` | 20 | Sokakların Sahibi — Bir turda 10. dalgaya ulaş. | Street Owner — Reach wave 10 in one run. |
| `com.oztdigital.holdout.ach_wave20` | 50 | Son Kalan — Bir turda 20. dalgaya ulaş. | Last One Standing — Reach wave 20 in one run. |
| `com.oztdigital.holdout.ach_first_boss` | 20 | Kasap Avcısı — İlk boss'unu yen. | Butcher Hunter — Defeat your first boss. |
| `com.oztdigital.holdout.ach_helicopter` | 15 | Kalkış İzni — Bir helikopter iniş noktasını savun. | Clear for Take-Off — Defend a helicopter landing zone. |
| `com.oztdigital.holdout.ach_combo50` | 20 | Durdurulamaz — 50 öldürmelik kombo yap. | Unstoppable — Reach a 50-kill combo. |
| `com.oztdigital.holdout.ach_synergy` | 10 | Gizli Formül — Bir kart sinerjisi keşfet. | Secret Formula — Discover a card synergy. |
| `com.oztdigital.holdout.ach_hard_wave10` | 40 | Demir İrade — Zor modda 10. dalgaya ulaş. | Iron Will — Reach wave 10 in Hard mode. |
| `com.oztdigital.holdout.ach_campaign` | 50 | Son Devriye — Hikâyenin 6. bölümünü bitir. | Last Patrol — Finish chapter 6 of the story. |
| `com.oztdigital.holdout.ach_all_stars` | 65 | Kusursuz Asker — Hikâyede 18 yıldızın hepsini topla. | Perfect Soldier — Collect all 18 story stars. |

Toplam 300 puan (Apple sınırı 1000). Her başarım için 512×512 (veya 1024×1024) görsel istenir — şimdilik oyun ikonu kullanılabilir.

## Bu sürümde yenilikler (0.1.1)

**TR**
- Game Center: Sonsuz, Zor, Hikâye ve Haftalık sıralamaları + 10 başarım
- Yeni: Haftalık Etkinlik — herkese aynı şehir, her hafta yeni kural
- İlk tur için 3 adımlı rehber
- Dalga sonu altın ödülü, 5 gizli kart sinerjisi
- Daha dengeli zorluk, daha sık otomatik doldurma, çeşitli kart teklifleri
- Arayüz düzeltmeleri, binaların altında daha geniş görüş, reklam iyileştirmeleri

**EN**
- Game Center: Endless, Hard, Story and Weekly leaderboards + 10 achievements
- New: Weekly Event — the same city for everyone, a new rule every week
- 3-step guide for your first run
- Wave-clear gold reward, 5 hidden card synergies
- Smoother difficulty, smarter auto-reload, more varied card offers
- UI fixes, wider view under buildings, ad improvements
