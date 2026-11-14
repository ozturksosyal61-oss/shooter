# Zombie Shooter — OZT Digital

Unity 6000.6.7f1 · URP 17.6 · glTFast · Input System (yalnızca yeni sistem) · Android + iOS
Bundle id: `com.oztdigital.zombieshooter` · Sahne: `Assets/Scenes/Game.unity`

## Kurgu — "Son Devriye"
- Düşmüş bir şehirde hayatta kalan **tek asker**. Telsizden söz verilen kurtarma helikopteri bir türlü gelmiyor; asker devriyeye devam ediyor.
- **Sonsuz şehir:** yürüdükçe mahalleler değişir — **cadde** (dükkânlar, terk edilmiş arabalar), **çarşı** (tezgâhlar, şemsiyeler, sandıklar), **sanayi** (fabrikalar, tanklar, varil), **liman** (konteyner yığınları).
- **Bosslar:** bir laboratuvar kazasından çıkan mutasyonlar. Tasarımlar **özgün** olmalı; Left 4 Dead (Tank, Witch, Boomer vb.) veya başka bir oyunun düşmanlarının kopyası/yeniden adlandırılmış hâli **olmaz**.

## Oyun özeti
- **Tür:** Landscape (yalnızca sol/sağ yatay), top-down, sonsuz dalga zombi shooter.
- **Kontroller:**
  - Ekranın **sol yarısı**: yarı saydam **floating joystick** (dokunulan yerde belirir) → hareket.
  - Ekranın **sağ yarısı**: **basılı tut = ateş**. Sürükleme varsa nişan sürükleme yönünde (360°); sürükleme yoksa **en yakın zombiye otomatik nişan**.
- **Hareket:** Alçak engellere (kasa, araba, barikat, çöp konteyneri) koşunca **otomatik vault** — vault **yalnızca oyuncuya** özgü.
- **Zombiler engel aşmaz:** etraflarından dolaşırlar (whisker steering). **İleride bazı boss'lar engel aşabilecek** (atlama/ezip geçme) — bu bir boss yeteneği olarak tasarlanır, sıradan zombilere verilmez.
- **Zorluk:** zamanla belirgin artar (`Difficulty` sınıfı: spawn aralığı, eşzamanlı zombi, can, hız, koşucu oranı, temas hasarı). **İleride özel zombi / boss dalgaları** eklenecek (zaman veya skor eşiğinde tetiklenen dalgalar).
- **Görsel:** Çizgi film tarzı asker + zombi; kamera ~55–60° eğik, oyuncuyu takip eder.
- **Skor + kombo** sistemi (ardışık öldürmeler çarpan verir).
- **Öncelik: vuruş hissi** — hit flash, knockback, kamera sarsıntısı, hit-stop, partikül, ses.
- **Performans:** Hedef 60 FPS mobil; mermi/zombi/efektler için **object pooling**, Update'te allocation yok.
- **Animasyon:** Şu an `.glb` modeller statik görsel (iskelet yok), hareket kodla. Mixamo için T-pose FBX'ler `_mixamo/` altında hazır; riglenip **Humanoid** avatar ile animasyonlar gelecek.

## Yapı
- `Assets/Scripts` · `Assets/Prefabs` (+`Env/`) · `Assets/Scenes` · `Assets/Art` · `Assets/Audio` · `Assets/Materials` · `Assets/Settings` (URP asset + renderer)
- Karakterler: `Assets/Art/Characters/{Soldier,Zombie}/*.glb` = Blender ile decimate edilmiş mesh (Soldier 9k, Zombie 6k tri), materyalsiz. Dokular `Textures/*_BaseColor.png` (max 1024, Android/iOS ASTC 6x6). Orijinal yüksek poligonlu dosyalar `Source~/` altında (EXT_texture_webp, Unity görmez). Modeller +Z'ye bakar.
- Ortam: `Assets/Art/Environment/{Cars,Survival,Roads,Commercial,Industrial}` — Kenney (CC0, lisanslar klasörlerde) GLB'leri + kit başına `Textures/colormap.png`. Builder bunları düzleştirip gerçek boyuta ölçekler → `Assets/Prefabs/Env/*.prefab` (pivot alt-orta, BoxCollider, `EnvProp.radius`, yüksekler `Fadeable`). Toplam ~3 MB.
- Mixamo: `_mixamo/{Soldier,Zombie}.fbx` (T-pose, 1.8 m, origin ayakta, doku gömülü). Yeniden üret: `blender -b --factory-startup -P Tools/blender_export_mixamo.py -- <glb> <basecolor.png> <out.fbx> 1.8`.
- Script'ler `Game` asmdef'inde:
  - Core: GameManager, Pool, GameDefs (layer'lar, `Vault`, `Difficulty`), Haptics, GameAudio (ses hook'ları: `GameAudio.Play(SfxId, pos)` + `OnSfx` event; klipler inspector'da SfxId sırasıyla)
  - Input: TouchControls · Player: PlayerController, Weapon, Bullet · Enemies: Zombie, ZombieSpawner
  - World: ChunkManager (sonsuz şehir), FloatingOrigin, EnvProp, Fadeable · FX: CameraRig, Fx, HitFlash · UI: HUD, DamageNumbers
- **Sonsuz dünya:** 20x20 m chunk = bir şehir bloğu (kenarlarda yarım 7 m yol + sarı orta şerit/yaya geçidi, kaldırım, 9x9 iç alan). Oyuncu etrafında 5x5 aktif, chunk ve objeler pool'lu, seed'li (`ChunkManager.seed`, `randomSeed`). Mahalle = seed'li Perlin (`DistrictAt`): Cadde / Çarşı / Sanayi / Liman, her birinin zemin dokusu ve obje seti `DistrictSettings`'te. Yollarda araba/barikat/yanan varil, iç alanda bina veya prop.
- **Floating origin:** oyuncu 120 m'yi geçince tüm root objeler chunk katı kadar kaydırılır (`FloatingOrigin.OnShift` event'i; dünya pozisyonu tutan sistemler buna abone). Global konum: `FloatingOrigin.ToGlobal`.
- Spawn: kamera viewport'unun hemen dışı (yere izdüşüm), oyuncunun hareket yönüne ağırlıklı; 34 m'den uzakta kalan zombiler geri dönüştürülür.
- **Sahne kod ile üretilir:** `Assets/Editor/GameSceneBuilder.cs` (menü: Tools/Zombie Shooter/Rebuild Game Scene veya `-executeMethod GameSceneBuilder.Build`). Üretilen dokular, materyaller, tüm prefab'lar ve Game.unity'yi yeniden yazar — elle yapılan değişiklikler kaybolur, kalıcı değişiklikleri builder'a işle.
- Layer'lar: 8 Blocker (bina, konteyner, yanan varil, tank — vault edilmez, mermi durdurur), 9 Vaultable (kasa, araba, barikat, çöp konteyneri), 10 Zombie, 11 Player.
- Shader'lar `Assets/Shaders`: Game/FxAdditive, Game/FxAlpha, Game/Flash (URP unlit). Karakter/çevre: URP Simple Lit. Kameranın önünü kapatan binalar `Fadeable` ile yarı saydam olur.
- PlayMode testleri `Assets/Tests/PlayMode/GameSmokeTest.cs`: SpawnShootDieRestart, VaultOverObstacle, FloatingOriginAndChunks; `CaptureScreens` [Explicit] gameplay PNG'si alır (`-testFilter GameSmokeTest.CaptureScreens`, env `CAPTURE_DIR`, `-nographics` olmadan). Çalıştır: `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>`.

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
- [x] **Faz 2 — Son Devriye dünyası** (2026-11-15)
  - [x] Sonsuz şehir: 20x20 chunk, 5x5 aktif, pooling, seed'li prosedürel, floating origin; asfalt/kaldırım/şerit zemin, 4 mahalle (cadde, çarşı, sanayi, liman)
  - [x] Kenney CC0 asset'leri: arabalar, barikat, yanan varil (partikül ateş), kasalar, çöp konteyneri, dükkân/fabrika binaları, konteyner, tank, şemsiye, sokak lambası, koni/lastik
  - [x] Spawn kamera görüşünün hemen dışından, hareket yönüne ağırlıklı; uzakta kalanlar geri dönüşüm
  - [x] Zombi vault kaldırıldı → engel etrafından dolaşma (sticky whisker steering); vault yalnız oyuncuda
  - [x] Belirgin zorluk eğrisi (`Difficulty`), koşucu zombi varyantı
  - [x] Vuruş hissi: tracer, isabet kıvılcımı + yeşil kan sıçraması ve yer lekesi, ragdoll benzeri savrulup devrilme, hitstop/shake (vuruşta hafif, kill'de güçlü), ateşte kamera geri tepmesi, hasar sayısı pop-up (kritik sarı), `GameAudio` ses hook'ları
  - [x] Mixamo hazırlık: `_mixamo/Soldier.fbx`, `_mixamo/Zombie.fbx`
- [ ] Mixamo'da rigle + animasyonları indir (koşu, ateş, zombi yürüyüş/ölüm) → Humanoid import
- [ ] Ses dosyaları (`GameAudio.clips`), müzik
- [ ] Özel zombi / boss dalgaları (laboratuvar mutasyonları, özgün tasarım; bazıları engel aşabilir)
- [ ] Cihazda performans profili (60 FPS hedefi, 70 zombi), gerekirse GPU instancing/LOD
- [ ] Silah çeşitleri / power-up, dalga yapısı, ana menü
