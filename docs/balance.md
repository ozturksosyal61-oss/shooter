# Denge — PlaytestBot sonuçları

Botlar oyunu gerçek girdi yolundan oynar (`Assets/Tests/PlayMode/PlaytestBot.cs`):
- **turret:** hiç kıpırdamaz, sağ yarıya basılı tutar (auto-aim).
- **kite (akıllı):** 16 yönü zombi yakınlığı + bina açıklığına göre puanlayıp kaçar, can paketlerini toplar, sakin anda reload yapar, aktif reload'u tatlı noktada basar.
- **explore:** sürekli kuzeye koşar, çok yaklaşan zombiden sapar, en yakına nişan alır.

Çalıştırma: `PLAYTEST_STRATEGY=<tarz> PLAYTEST_TIMESCALE=3 PLAYTEST_SHOTS=0 CAPTURE_DIR=<klasör> Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter PlaytestBot.Play ...` → `<klasör>/playtest_<tarz>.txt`.

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
