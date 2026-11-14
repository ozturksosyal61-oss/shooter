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
- **Ses:** CC0 kaynaklar (Kenney, OpenGameArt) + Python sentezi; her dosyanın kaynağı/lisansı `CREDITS.md`'de. Lisansı CC0 veya ticari kullanıma açık olmayan ses **eklenmez**. Toplam ses bütçesi ≤ 6 MB (şu an ~1.4 MB).
- **Performans:** Hedef 60 FPS mobil; mermi/zombi/efektler için **object pooling**, Update'te allocation yok.
- **Animasyon:** Mixamo Humanoid (Soldier / Zombie avatar), root motion kapalı. Asker: alt gövde 8 yönlü locomotion (hareket yönü nişan yönüne göre lokal → geri geri koşarken ateş), üst gövde avatar mask ile nişan/reload, vault = jump, 2 ölüm animasyonu. Zombi: idle/walk/run, temas anında saldırı, doğarken çığlık, ölümde önce savrulma sonra dying.
- **Silah:** Tüfek göğüs önünde nişan yönünde tutulur, iki el IK ile kabza / el kundağına; mermi ve muzzle flash namlu ucundan. 30'luk şarjör, boşalınca otomatik reload (1.5 sn, animasyon hızı senkron), reload sırasında ateş yok; tetik bırakılıp 1 sn geçince ve şarjör yarıdan azsa taktik reload.

## Yapı
- `Assets/Scripts` · `Assets/Prefabs` (+`Env/`) · `Assets/Scenes` · `Assets/Art` · `Assets/Audio` · `Assets/Materials` · `Assets/Settings` (URP asset + renderer)
- Karakterler: `Assets/Art/Characters/{Soldier,Zombie}/*_Rigged.fbx` = Mixamo'da riglenmiş düşük poligon mesh (Soldier 9k, Zombie 6k tri), Humanoid avatar (Zombie: Optimize Game Objects). Animasyonlar `Anim/*.fbx` (yalnızca kullanılanlar; tam set `_mixamo/anim/` altında, git'e dahil değil). Dokular `Textures/*_BaseColor.png` (max 1024, ASTC 6x6). Eski statik `*.glb` ve `Source~/` orijinalleri referans için duruyor.
- Animator: `Assets/Animation/Soldier.controller`, `Zombie.controller`, `UpperBody.mask` — builder üretir. Import: loop yalnız döngü kliplerinde; root rotation + height bake, XZ bake yalnız tek seferlik kliplerde (ölüm, saldırı, reload). Zombi "scream" için Mixamo **Zombie Scream** yok → `zombie agonizing` vekil; `_mixamo/anim/zombie/zombie scream.fbx` → `Assets/Art/Characters/Zombie/Anim/` kopyalanıp builder çalıştırılırsa otomatik kullanılır.
- Silah: `Assets/Art/Weapons/Rifle/Rifle.glb` — Quaternius Assault Rifle (CC0, poly.pizza), `Tools/blender_normalize_weapon.py` ile namlu +Z / yukarı +Y / 0.85 m. Prefab içinde ×1.1, RightGrip / LeftGrip / Muzzle noktaları builder'da (`GameSceneBuilder.Characters.cs`).
- Ortam: `Assets/Art/Environment/{Cars,Survival,Roads,Commercial,Industrial}` — Kenney (CC0, lisanslar klasörlerde) GLB'leri + kit başına `Textures/colormap.png`. Builder bunları düzleştirip gerçek boyuta ölçekler → `Assets/Prefabs/Env/*.prefab` (pivot alt-orta, BoxCollider, `EnvProp.radius`, yüksekler `Fadeable`). Toplam ~3 MB.
- Mixamo: `_mixamo/{Soldier,Zombie}.fbx` (T-pose, 1.8 m, origin ayakta, doku gömülü). Yeniden üret: `blender -b --factory-startup -P Tools/blender_export_mixamo.py -- <glb> <basecolor.png> <out.fbx> 1.8`.
- Script'ler `Game` asmdef'inde:
  - Core: GameManager, Pool, GameDefs (layer'lar, `Vault`, `Difficulty`), Haptics, GameAudio (+`AudioPrefs`), MusicDirector, ListenerFollow
  - Input: TouchControls · Player: PlayerController, SoldierRig (Animator + IK + silah yerleşimi), Weapon (şarjör/reload), Bullet · Enemies: Zombie, ZombieSpawner
  - World: ChunkManager (sonsuz şehir), FloatingOrigin, EnvProp, Fadeable · FX: CameraRig, Fx, HitFlash · UI: HUD, DamageNumbers
- **Sonsuz dünya:** 20x20 m chunk = bir şehir bloğu (kenarlarda yarım 7 m yol + sarı orta şerit/yaya geçidi, kaldırım, 9x9 iç alan). Oyuncu etrafında 5x5 aktif, chunk ve objeler pool'lu, seed'li (`ChunkManager.seed`, `randomSeed`). Mahalle = seed'li Perlin (`DistrictAt`): Cadde / Çarşı / Sanayi / Liman, her birinin zemin dokusu ve obje seti `DistrictSettings`'te. Yollarda araba/barikat/yanan varil, iç alanda bina veya prop.
- **Floating origin:** oyuncu 120 m'yi geçince tüm root objeler chunk katı kadar kaydırılır (`FloatingOrigin.OnShift` event'i; dünya pozisyonu tutan sistemler buna abone). Global konum: `FloatingOrigin.ToGlobal`.
- Spawn: kamera viewport'unun hemen dışı (yere izdüşüm), oyuncunun hareket yönüne ağırlıklı; 34 m'den uzakta kalan zombiler geri dönüştürülür.
- **Sahne kod ile üretilir:** `Assets/Editor/GameSceneBuilder.cs` (menü: Tools/Zombie Shooter/Rebuild Game Scene veya `-executeMethod GameSceneBuilder.Build`). Üretilen dokular, materyaller, tüm prefab'lar ve Game.unity'yi yeniden yazar — elle yapılan değişiklikler kaybolur, kalıcı değişiklikleri builder'a işle.
- Layer'lar: 8 Blocker (bina, konteyner, yanan varil, tank — vault edilmez, mermi durdurur), 9 Vaultable (kasa, araba, barikat, çöp konteyneri), 10 Zombie, 11 Player.
- Shader'lar `Assets/Shaders`: Game/FxAdditive, Game/FxAlpha, Game/Flash (URP unlit). Karakter/çevre: URP Simple Lit. Kameranın önünü kapatan binalar `Fadeable` ile yarı saydam olur.
- **Ses sistemi:** `GameAudio.Play(SfxId, pos, pitchMul, volumeMul)` tek giriş noktası (+ `OnSfx` event). `SfxEntry` bankası (builder'da `SoundBank()`): klip varyantları (aynısı arka arkaya çalmaz), voice limiti (aşınca en eski çalınır), minInterval, pitch ±%8 + volume varyasyonu, spatialBlend/rolloff (Linear 8→40 m; listener oyuncunun 2 m üstünde, `ListenerFollow`), **haptic ve music duck aynı entry'de** → atış/kill/hasar haptic'i sesle senkron; başka yerde `Haptics.Play` çağırma. Reload parçaları reload klibindeki animation event'lerden (`SoldierRig.OnReloadEvent`).
- Müzik: `MusicDirector` iki loop katmanı (calm ↔ intense) yakındaki zombi sayısı + kombo + ateşe göre crossfade; `Duck()`; ölümde `AudioLowPassFilter` ile ~550 Hz'e boğulur.
- Mixer: `Assets/Audio/GameMixer.mixer` (Master → Music / SFX / UI), exposed `MasterVolume/MusicVolume/SfxVolume/UiVolume`; kayıtlı ayarlar `AudioPrefs.Set/Get/Apply` (PlayerPrefs `vol_*`, 0..1 → dB). Mixer'ı builder internal `AudioMixerController` API'sıyla reflection üzerinden üretir.
- Ses import: SFX mono + Vorbis q0.5, ≤1 sn → Decompress On Load, uzun → Compressed In Memory; müzik Streaming q0.4. Ses dosyalarını yeniden üretmek: `python Tools/build_audio.py <indirilen_paketler> .` (numpy, scipy, soundfile).
- PlayMode testleri `Assets/Tests/PlayMode/GameSmokeTest.cs`: SpawnShootDieRestart (+ölümde müzik low-pass), VaultOverObstacle, FloatingOriginAndChunks, MagazineAndReload, AudioHooks; `CaptureScreens` [Explicit] gameplay + yakın çekim PNG'leri alır (env `CAPTURE_DEBUG=1` tüfeği kırmızı boyar ve konumunu loglar) (`-testFilter GameSmokeTest.CaptureScreens`, env `CAPTURE_DIR`, `-nographics` olmadan). Çalıştır: `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>`.

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
- [x] **Faz 3 — Animasyon + silah** (2026-11-15)
  - [x] Mixamo FBX'ler Humanoid import (Soldier/Zombie avatar), loop ayarları, root motion kapalı
  - [x] Asker Animator: 8 yönlü walk/run blend tree (aim-lokal), üst gövde nişan/reload katmanı, vault=jump, ölüm (önden/arkadan); kod bob/sallanma kaldırıldı
  - [x] Zombi Animator: idle/walk/run (koşucu), temasla saldırı (attack/punch), savrulma → dying, doğuşta scream (vekil: agonizing)
  - [x] CC0 tüfek (Quaternius), iki el IK, namlu ucundan mermi/muzzle flash
  - [x] 30'luk şarjör, otomatik + taktik reload, HUD mermi sayacı + reload barı
- [ ] Mixamo "Zombie Scream" indir (şu an agonizing vekil); zombi yürüme klibi yavaş (≈0.5 m/s) → daha hızlı bir walk klibi
- [ ] Ateş animasyonu/recoil için additive katman (şu an procedural)
- [x] **Faz 4 — Ses** (2026-11-15)
  - [x] CC0 SFX: tüfek (3), isabet et/kritik/beton/metal, zombi inleme(6)/çığlık(4)/saldırı(4)/ölüm(4), oyuncu hasar(4)/ölüm(2), ayak sesi(5), vault zıplama/iniş, reload parçaları (animation event), kill-streak, UI tık, ölüm jingle'ı, patlama
  - [x] Sentez: boş tetik, düşük mermi uyarısı, kombo ding (çarpanla yükselen pitch), kalp atışı (can < %30)
  - [x] Müzik: calm/intense katmanları yoğunluğa göre, ducking, ölümde low-pass
  - [x] Pooling (24 voice), voice limit, varyasyon, 3D rolloff, haptic senkronu, AudioMixer + kayıtlı volume
- [ ] Ayarlar menüsü (AudioPrefs hazır), patlayan varil (Explosion sesi + duck hazır)
- [ ] Özel zombi / boss dalgaları (laboratuvar mutasyonları, özgün tasarım; bazıları engel aşabilir)
- [ ] Cihazda performans profili (60 FPS hedefi, 70 zombi), gerekirse GPU instancing/LOD
- [ ] Silah çeşitleri / power-up, dalga yapısı, ana menü
