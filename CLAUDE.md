# Zombie Shooter — OZT Digital

Unity 6000.6.7f1 · URP 17.6 · glTFast · Input System (yalnızca yeni sistem) · Android + iOS
Bundle id: `com.oztdigital.zombieshooter` · Sahne: `Assets/Scenes/Game.unity`

## Oyun özeti
- **Tür:** Landscape (yalnızca sol/sağ yatay), top-down, sonsuz dalga zombi shooter.
- **Kontroller:**
  - Ekranın **sol yarısı**: yarı saydam **floating joystick** (dokunulan yerde belirir) → hareket.
  - Ekranın **sağ yarısı**: **basılı tut = ateş**. Sürükleme varsa nişan sürükleme yönünde (360°); sürükleme yoksa **en yakın zombiye otomatik nişan**.
- **Hareket:** Kasalara/engellere yaklaşınca **otomatik vault** (üstünden atlama).
- **Görsel:** Çizgi film tarzı asker + zombi; kamera ~55–60° eğik, oyuncuyu takip eder.
- **Skor + kombo** sistemi (ardışık öldürmeler çarpan verir).
- **Öncelik: vuruş hissi** — hit flash, knockback, kamera sarsıntısı, hit-stop, partikül, ses.
- **Performans:** Hedef 60 FPS mobil; mermi/zombi/efektler için **object pooling**, Update'te allocation yok.
- **Animasyon:** Şu an `.glb` modeller statik görsel (iskelet yok). İleride Mixamo'dan riglenip **Humanoid** avatar ile animasyonlar gelecek.

## Yapı
- `Assets/Scripts` · `Assets/Prefabs` · `Assets/Scenes` · `Assets/Art` · `Assets/Audio` · `Assets/Materials` · `Assets/Settings` (URP asset + renderer)
- Karakterler: `Assets/Art/Characters/{Soldier,Zombie}/*.glb` = Blender ile decimate edilmiş mesh (Soldier 9k, Zombie 6k tri), materyalsiz. Dokular `Textures/*_BaseColor.png` (max 1024, Android/iOS ASTC 6x6). Orijinal yüksek poligonlu dosyalar `Source~/` altında (EXT_texture_webp, Unity görmez). Modeller +Z'ye bakar.
- Script'ler `Game` asmdef'inde: Core (GameManager, Pool, GameDefs: layer/Arena/Vault, Haptics), Input (TouchControls), Player (PlayerController, Weapon, Bullet), Enemies (Zombie, ZombieSpawner), World (ArenaGenerator), FX (CameraRig, Fx, HitFlash), UI (HUD).
- **Sahne kod ile üretilir:** `Assets/Editor/GameSceneBuilder.cs` (menü: Tools/Zombie Shooter/Rebuild Game Scene veya `-executeMethod GameSceneBuilder.Build`). Dokular, materyaller, prefab'lar (Player, Zombie, Bullet, Crate) ve Game.unity'yi yeniden yazar — sahnede elle yapılan değişiklikler kaybolur, kalıcı değişiklikleri builder'a işle. `GameSceneBuilder.Capture` (graphics'li batchmode, `CAPTURE_DIR` env) doğrulama için PNG render alır.
- Layer'lar: 8 Wall, 9 Crate, 10 Zombie, 11 Player. Arena iç alanı 40x28 (`Arena.HalfX/HalfZ`).
- Shader'lar `Assets/Shaders`: Game/FxAdditive, Game/FxAlpha, Game/Flash (URP unlit). Karakter/çevre: URP Simple Lit.
- PlayMode testleri: `Assets/Tests/PlayMode/GameSmokeTest.cs` (spawn+auto-aim kill+joystick+ölüm+restart, vault). Çalıştır: `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>`.

## Kurallar
- Kullanıcıya onay sorma; adımları kendin yap.
- **Push yok** (yalnızca yerel commit).
- İş bitince **kısa rapor** ver.
- Unity batchmode: `"C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod X.Y -quit -logFile <log>` (Unity editörü açıkken çalışmaz).

## İlerleme
- [x] Proje kurulumu: URP, glTFast, Input System, Player Settings (landscape, bundle id), klasörler, Game sahnesi, git
- [x] **Faz 1 prototip** (2026-11-15)
  - [x] Oyuncu: floating joystick (sol yarı), kodla dönüş + yürüme bob/sallanma, CharacterController
  - [x] Ateş/nişan: sağ yarı basılı tut, sürükleme yönü 360°, sürüklemesiz en yakın zombiye auto-aim; pooled mermiler (spherecast)
  - [x] Zombi: oyuncuya yürür + kodla sallanma, yerden çıkma, ayrışma (separation), kasaları vault, temas hasarı; kenarlardan sonsuz spawn, zamanla artan yoğunluk/hız/can, pooling
  - [x] Arena: duvarlı 40x28 alan, rastgele kasalar, kasaya koşunca otomatik vault
  - [x] Vuruş hissi: muzzle flash, beyaz hit flash, knockback, kill'de hitstop, screenshake, haptic (iOS plugin / Android VibrationEffect), partiküller (goo, chunk, spark, pop, dust)
  - [x] UI: skor (sayaç animasyonu), kombo çarpanı (5 vuruşta +1, max x10; 2 sn vuruşsuz sıfırlanır), can barı, hasar vinyeti, FPS, game over + dokun/restart, en iyi skor (PlayerPrefs)
  - [x] Editor testi: mouse sol tık sol/sağ yarı simülasyonu, WASD hareket, sağ tık imlece ateş, Space restart
- [ ] Ses (silah, vuruş, ölüm, müzik)
- [ ] Mixamo rig + Humanoid animasyonlar (koşu, ateş, zombi yürüyüş/ölüm)
- [ ] Cihazda performans profili (60 FPS hedefi, 70 zombi), gerekirse GPU instancing/LOD
- [ ] Silah çeşitleri / power-up, dalga yapısı, ana menü
