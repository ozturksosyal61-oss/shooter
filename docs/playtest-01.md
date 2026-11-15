# Playtest 01 — oyuncu gözüyle inceleme (Faz 4 sonrası, 2026-11-15)

Yöntem: `Assets/Tests/PlayMode/PlaytestBot.cs` ile oyun gerçek girdi yolundan (WASD + fare) üç tarzda oynatıldı; telemetri + HUD'lu ekran görüntüleri incelendi. Dokunmatik his, ses ve haptic bu yöntemle değerlendirilemez.

| Tarz | Hayatta kalma | Kill | İsabet | İlk temas | Aldığı darbe | Sonuç |
|---|---|---|---|---|---|---|
| Turret (sabit, auto-aim) | 71 sn | 81 | %66 | 66. sn | 8 | öldü |
| Kite (kaçarak) | 85 sn | 82 | %64 | 64. sn | 8 | öldü |
| Explore (keşif) | 95 sn | 89 | %70 | 38. sn | 7 | öldü |

## İlk izlenim
İlk dakika keyifli: yeşil sıçramalar, tüfek, sokak dokusu çizgi film tarzına oturuyor; zombilerin savrulup düşmesi tatmin edici. Ama 90 saniye sonra "neden bir daha oynayayım?" sorusunun cevabı yok.

## En büyük sorunlar
1. **Ne yaparsan yap 70–95 sn'de ölüyorsun — beceri fark etmiyor.** İlk 60 sn neredeyse sıfır tehlike (2–7 zombi, can hiç azalmıyor), sonra 60. saniyede duvar: zombi sayısı 10 sn'de 5 → 22, 8 darbede ölüm. Can yenilenmesi/paketi yok.
2. **Kombo anlamsız.** Çarpan 20–25. sn'de x10'a ulaşıyor ve hiç düşmüyor; kombo 200–386'ya çıkıyor. 90 sn'de 80.000 puan bir şey ifade etmiyor.
3. **Oyun içi ilerleme yok.** Yükseltme, toplanacak şey, seçim yok; 1. sn'deki asker ile 90. sn'deki aynı. Mahalle gezmenin ödülü yok.
4. **Şehir monoton.** Her karede ortada bir dört yol ağzı; her chunk köşesi kavşak → dama tahtası. Mahalleler yalnızca blok içinde farklı.
5. **Okunabilirlik.** Asker küçük, kalabalıkta kayboluyor; hasar sayıları üst üste biniyor; saydamlaşan binalar ekranın üçte birini kaplayabiliyor; ekran dışı tehdit uyarısı yok.
6. **Zamanın ~1/3'ü reload.** 30 mermi 3 sn, reload 1.5 sn.

## Küçük ama hissettirenler
- Ölüm ekranı: süre/kill/en iyi kombo yok, "TEKRAR" butonu yok, rekor kutlaması yok.
- Vault istemsiz tetiklenebiliyor (kaçan bot 85 sn'de 21 vault).
- Yanan variller sadece dekor — vurunca patlamalı.
- Ortalama 60 FPS ama her turda en az bir kez 2 FPS takılma.
- Kontroller için ilk açılış ipucu yok.

## İstenenler (öncelik sırası)
1. Dalga yapısı + düzgün zorluk eğrisi, dalga arası nefes + can yenilenmesi/paketi.
2. Oyun içi ilerleme (roguelite kartlar).
3. Kurguya bağlı hedefler (telsiz, fişek, helikopter iniş noktası).
4. Anlamlı kombo (kill bazlı, hasar alınca kırılan).
5. Özel zombiler (patlayan, tüküren, kalkanlı).
6. Okunabilirlik (kamera, oyuncu halkası, ekran dışı ok, toplu hasar sayısı, dairesel bina kesiti).
7. Şehir çeşitliliği (kavşaksız caddeler, park, meydan, simge yapılar, mahalle renk tonu).
8. Reload'a oyuncu etkisi (elle + aktif reload).
9. Zengin ölüm ekranı + hızlı tekrar.
10. Meta ilerleme (silah açma, görevler).
