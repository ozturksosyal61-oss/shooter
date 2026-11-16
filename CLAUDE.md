# Holdout — OZT Digital

Unity 6000.6.7f1 · URP 17.6 · glTFast · Input System (yalnızca yeni sistem) · Android + iOS
Bundle id: `com.oztdigital.holdout` (henüz mağazaya yüklenmedi) · Sahneler: `Assets/Scenes/Menu.unity` (build index 0) → `Game.unity` (1)

## Ad ve dil
- **Oyun adı:** "Holdout" (Product Name / ana ekran adı, iOS CFBundleDisplayName `IosPostBuild`, Android label). **Mağaza adı:** TR "Holdout: Zombi Avı", EN "Holdout: Zombie Survival" (`game.title`, `game.store_title`).
- **"Son Devriye"** (EN "Last Patrol") = kampanya / hikâye adı (`game.story`, menüde Kampanya alt yazısı).
- Türkçe + İngilizce; dil ayarlardan anında değişir.
- **Tüm oyun içi metinler tek localization tablosundan:** `Assets/Scripts/Core/Loc.cs` (`Loc.T(key)`, `Loc.F(key, args)`, `Loc.Upper` Türkçe i/İ uyumlu; sütunlar tr/en; dil kayıt dosyasında `settings.lang`, `Settings.Apply` → `Loc.Current`). Sabit UI etiketleri `LocText` (builder'da `L(text, key)`). Kodda/builder'da oyuncuya görünen düz string yazma — anahtar ekle. Kart metinleri `card.<CardId>.title/.1/.2/.3` (+ opsiyonel `.icon`). Yeni karakter gerekirse `Warmup.Glyphs`'e ekle.

## Yayın ayarları
- `Assets/Editor/ProjectSetup.cs` (builder çağırır): ad "Holdout", bundle `com.oztdigital.holdout`, sürüm **0.1.0**, iOS build **2** (Android versionCode 2; her TestFlight yüklemesinde artır), minimum iOS **15.0**, yalnız yatay (sol/sağ).
- **İkon:** `Assets/Art/Icon/AppIcon.png` (1024², opak RGB, kare; kaynak masaüstü `ikon.png`) → varsayılan + iOS tüm boyutlar + App Store marketing 1024; Android adaptive (`AppIcon_Foreground.png` şeffaf, %66 güvenli alan + `AppIcon_Background.png` düz #23261F); Unity 6.6'da round/legacy tipleri kaldırıldı → eski launcher'lar varsayılan ikonu kullanır.
- `IosPostBuild` (her iOS build'de Info.plist): `CADisableMinimumFrameDurationOnPhone` (ProMotion), `CFBundleDisplayName` Holdout, `ITSAppUsesNonExemptEncryption` false.
- iOS build: `Unity.exe -batchmode -nographics -projectPath . -buildTarget iOS -executeMethod ProjectSetup.BuildIos -quit -logFile <log>` → `Builds/iOS` (Xcode projesi), zip `Builds/Holdout_iOS_<sürüm>.zip`. `Builds/` git'e girmez.
- Uzak depo: `https://github.com/ozturksosyal61-oss/shooter.git`, dal `main`.

## Menü, ayarlar, kayıt (F10)
- **Açılış:** Unity splash → OZT Digital splash (menüde, oturum başına bir kez) → **ana menü** (`MainMenu`): HOLDOUT logosu (haki dolgu, turuncu + mürekkep kontur, zehir yeşili gölge; Rubik), arkada canlı şehir (`BuildCity` ortak; zombisiz) + yavaş dönen kamera (`MenuCamera`), sağ üstte altın / elmas (`Profile`). Butonlar: OYNA → Kampanya (kilitli, "YAKINDA") / Sonsuz; MAĞAZA (F13), günlük giriş + görevler; Sonsuz → silah seçimi; AYARLAR.
- **Sahne geçişi:** `LoadingScreen.Load(scene)` — `Resources/LoadingScreen` prefab'ı (DontDestroyOnLoad), rastgele ipucu (`tip.1..10`), ilerleme barı, en az 1.1 sn.
- **Ayarlar** (`SettingsPanel`, menü + duraklatma; `Settings.Apply`): müzik, efekt, titreşim (`Haptics.Enabled`), FPS 30/60/90/120 (yalnız ekranın desteklediği; `Settings.SupportedFps`, iOS ProMotion için `CADisableMinimumFrameDurationOnPhone`), grafik düşük/orta/yüksek (URP render scale ×0.7/0.85/1, gölge mesafesi ×0/0.6/1, küçük partiküller ×0.45/0.75/1), joystick boyutu (0.75–1.35) + saydamlığı (0.3–1), dil TR/EN (anında), destek `https://www.oztdigital.com.tr/holdout/support`, gizlilik `https://www.oztdigital.com.tr/holdout/privacy`, kullanım koşulları `https://www.oztdigital.com.tr/holdout/terms` (`SettingsPanel` sabitleri; e-posta `oztdigitaldestek@gmail.com` panelde yazılı), ilerlemeyi sıfırla (onaylı; ayarlar korunur).
- **Duraklatma** (`PauseMenu`, `GameManager.SetPaused`): sol üst buton → DEVAM / AYARLAR / ANA MENÜ; uygulama arka plana gidince otomatik. `timeScale` 0, `AudioListener.pause`, `TouchControls.Paused`. Duraklat / menü butonları `TouchControls.uiButtons` ile dokunuşu yutar; uGUI için sahnelerde `EventSystem` + `InputSystemUIInputModule`.
- **Kayıt** (`SaveSystem`): tek JSON (`persistentDataPath/save.json`) — `SaveData` { version, savedAt, tutorialDone, settings, records, wallet, campaign, inventory }. Atomik yazım (tmp → değiştir), önceki sürüm `save.bak`; ana dosya bozuksa yedek, ikisi de bozuksa yeni veri. `CurrentVersion` + `Migrate` adımları; ilk açılışta eski PlayerPrefs anahtarları içe aktarılır. Depolama `ISaveStore` (`FileSaveStore`, testlerde `MemorySaveStore`; bulut kaydı aynı arayüzü uygular, çakışmada `savedAt`). PlayerPrefs artık kullanılmaz.

## Ekonomi + mağaza (F13)
- **Kurallar** `Economy` (saf, EditMode `EconomyTests`; fiyat tablosu `docs/economy.md`), **state tek noktadan** `Profile` (para, envanter, yükseltme, seçimler, günlük durum; `Profile.Grant(gold, gems, reason)` her kaynak için — kampanya ödülleri (F12) de buradan). Altın (oynayarak) + elmas (IAP, az miktar giriş/reklam).
- **Kazanç:** tur sonu altını `RunRewards` (dalga/kill/görev/boss/skor × `Profile.GoldMultiplier`; ölüm ekranında sayarak artar, coin sesi), 7 günlük giriş (`DailyLoginPanel`, UTC gün, gün kaçarsa baştan), günde 3 görev (`QuestsPanel`, havuzdan seed'li).
- **Silahlar** (`WeaponId`: M4 başlangıç, SMG, Pompalı, Ağır makineli, Keskin nişancı; Quaternius Ultimate Gun Pack CC0, `Assets/Art/Weapons/*`): tur öncesi seçim (`LoadoutPanel`), `WeaponLoadout` prefab'da silah modelini/tutma noktalarını/istatistikleri/sesi (`SfxId.ShootSmg` vb., `Tools/synth_guns.py`) uygular. Kalıcı yükseltme hasar/şarjör/reload × 5 seviye (`Economy.UpgradeCost`). Test/bot: `WeaponLoadout.Override`.
- **Kostümler** (`CostumeId`, yalnız görünüş): Standart, Orman, Çöl, Gece, Kutup (altın), Altın (elmas), Komando (yalnız başlangıç paketi); dokular `Tools/make_costumes.py`.
- **Bonuslar:** başlangıç canı (+10 × 3), başlangıç kartı (1 tur, tüketilir), altın +%10.
- **Mağaza** `ShopUI`: sekmeler Silahlar / Kostümler / Bonuslar / Elmas, 3D dönen önizleme (`PreviewStage`, layer 12 Preview, kendi kamerası → `PreviewRT`; `-nographics`'te kamera kapalı), kilitli/sahip/seçili durumları, satın alma onayı.
- **IAP** `StoreService` (`IStoreBackend`: `UnityIapBackend` Unity IAP 5, editör/testte `FakeStoreBackend`); ürünler `Economy.Products` (`com.oztdigital.holdout.gems_small/medium/large`, `starter_pack`, `vip`); ayarlarda "Satın alımları geri yükle".
- **Ödüllü reklam** `AdService` (`IAdBackend`, Unity LevelPlay, `AdConfig.TestMode`; anahtarlar `REPLACE_*` — yayından önce doldur): tur sonu 2x altın, ölünce turda 1 kez devam (alternatif 20 elmas), günlük 5 elmas. iOS ATT (`NSUserTrackingUsageDescription`, reddedilirse kişiselleştirilmemiş), COPPA kapalı. VIP: reklamsız ödül + %25 altın.
- **Kayıt:** `SaveData.CurrentVersion` 2 (göç), dosya XOR+base64 + SHA-256 imza (`SaveSystem.Hash`); imza tutmazsa yedeğe düşer. Testler gerçek kayda dokunmaz (`MemorySaveStore`).

## Kurgu — "Son Devriye" (kampanya / hikâye)
- Düşmüş bir şehirde hayatta kalan **tek asker**. Telsizden söz verilen kurtarma helikopteri bir türlü gelmiyor; asker devriyeye devam ediyor.
- **Sonsuz şehir:** yürüdükçe mahalleler değişir — **cadde** (dükkânlar, terk edilmiş arabalar; sıcak ışık), **çarşı** (tezgâhlar, şemsiyeler, sandıklar; turuncu), **sanayi** (fabrikalar, tanklar, varil; gri-mavi), **liman** (konteyner yığınları; yeşilimsi sis). Bloklar arasında parklar, meydanlar (asker heykeli / obelisk / çeşme), dar ara sokaklar, yıkıntılar ve simge yapılar (benzin istasyonu, karakol, hastane girişi, otobüs durağı).
- **Hikâye:** Merkez telsizle görev verir (işaret fişeği, sinyal kulesi, ikmal sandığı); 3 görevden sonra helikopter iniş noktası açılır — helikopter iner ama kalabalık yüzünden **askeri bekleyemeden kalkar** ("Helikopter yine indi... ama seni bekleyemedi"), devriye sürer.
- **Bosslar:** bir laboratuvar kazasından çıkan mutasyonlar. Tasarımlar **özgün** olmalı; Left 4 Dead (Tank, Witch, Boomer vb.) veya başka bir oyunun düşmanlarının kopyası/yeniden adlandırılmış hâli **olmaz**.

## Oyun özeti
- **Tür:** Landscape (yalnızca sol/sağ yatay), top-down, sonsuz dalga zombi shooter.
- **Kontroller:**
  - Ekranın **sol yarısı**: yarı saydam **floating joystick** (dokunulan yerde belirir) → hareket.
  - Ekranın **sağ yarısı**: **basılı tut = ateş**. Sürükleme varsa nişan sürükleme yönünde (360°); sürükleme yoksa **en yakın zombiye otomatik nişan**.
- **Hareket:** Alçak engellere (kasa, araba, barikat, çöp konteyneri) koşunca **otomatik vault** — yalnızca joystick ≥ %90 itilmişken ve hareket yönü engel yüzeyine ±35° dikken. Vault **yalnızca oyuncuya** özgü.
- **Ölüm ekranı:** skor, süre, dalga, kill, en iyi kombo; rekor kırınca "YENİ REKOR!" + jingle; büyük TEKRAR butonu (≤1 sn'de aktif, sadece buton/Enter/Space yeniden başlatır). Rekorlar kayıt dosyasında (`SaveData.records`). Ölüm ekranında ANA MENÜ butonu. İlk açılışta 5 sn sol/sağ kontrol ipucu (`tutorial_done`).
- **Zombiler engel aşmaz:** etraflarından dolaşırlar (whisker steering). **İleride bazı boss'lar engel aşabilecek** (atlama/ezip geçme) — bu bir boss yeteneği olarak tasarlanır, sıradan zombilere verilmez.
- **Dalgalar:** `WaveDirector` — dalga (30–45 sn spawn) → 9 sn nefes arası (spawn yok, can 5/sn yenilenir) → daha büyük dalga. Zorluk yalnızca dalga numarasına bağlı (dalga 4–6'da yoğunluk artışı yavaşlatılmış, `Difficulty.DensityStep`; `Difficulty.ForWave`: spawn aralığı, eşzamanlı zombi, can, hız, koşucu oranı, temas hasarı). Denge botlarla yapılır (`docs/balance.md`). **İleride özel zombi / boss dalgaları** eklenecek.
- **Özel zombiler (F7, laboratuvar mutasyonları, dalga 3'ten itibaren karışık; her 7. dalga "özel dalga"):** ayak altında renkli halka, doğuşta kendi sesi, ekran dışındayken kenar oku (`ThreatIndicators`).
  - **Kalkanlı** (kırmızı, ~1.4x geniş): yavaş, ~8x can; önden gelen mermiyi araba kapısı kalkanı saptırır (metal sekme + kıvılcım), arkadan/yandan normal hasar; yavaş döner → yandan dolaş. Patlamalar kalkanı deler.
  - **Bombalı** (turuncu, ~1.3x şişkin): Drunk Walk, yakında koşar, ~2 m'de ~1 sn şişip patlar (alan hasarı zombilere de); vurularak öldürülünce de patlar, zincirleme patlar.
  - **Dilci** (mor, ~1.3x uzun): 8–10 m'de durur, 0.6 sn kırmızı nişan çizgisi, dil fırlatır; isabette oyuncuyu çeker (ters itmek yavaşlatır), dile/dilciye `breakDamage` hasar → kopar. Yakalıyken auto-aim dilciye kilitlenir.
- **Roguelite (F6):** kill → XP (özeller 5–8, koşucu 2, yürüyen 1) → seviye → oyun ~%6 hıza düşer, karartma + "SEVİYE ATLADIN!", 3 büyük kart sırayla dönerek gelir (nadirlik şeridi + parıltı, büyük ikon, isim, açıklama, seviye noktaları ●●○; nadirlik: sıradan/nadir/epik, ağırlık 60/30/12), üzerine gelince/dokununca büyür, dokunarak (editörde 1/2/3) seçilir → seçilen kart parlar, oyun hemen devam eder. **Kart güvencesi:** ilk 2 seçimde tekliflerde en az bir hasar kartı (`Upgrades.DamageCards`: delici, ateş hızı, çift mermi, kritik, patlayan leş, pompalı). 15 kart, en fazla 3 seviye (`Upgrades.Cards`): delici mermi, ateş hızı, şarjör, reload, çift mermi, kritik, kill'de patlama, maks can, can çalma, hareket hızı, mıknatıs, pompalı (otomatik yakın), taret (30 sn, 45 sn'de bir; canı var, yakınındaki zombiler ona saldırır, süresi/canı bitince küçük patlama; üstünde dairesel süre + can barı; aynı anda 1, seviye 3'te 2), otomatik bomba, dalga arası tam can. Alınan kartlar HUD'da ikon + seviye noktalarıyla.
- **Hikâye hedefleri (F8, `ObjectiveDirector` + `Objectives`):** dalga 2'den itibaren her 3 dalgada bir yeni hedef (aynı anda tek hedef): **fişek topla** (karakolda, dokun-al), **sinyal kulesini çalıştır** (6 m alanda 20 sn dayan; dışarı çıkınca ilerleme durur), **ikmal sandığı aç** (hastane girişi / benzin istasyonu, 2 sn başında dur; can < %60 → tam can, yoksa kart seçimi "İKMAL!"). Hedef yeri: yakında (3–6 chunk) aynı tip simge yapı varsa o, yoksa 4 chunk ötede chunk'ın tipi zorlanır (`ChunkManager.ForceType`). Tamamlayınca bonus puan (250 + 25×dalga) + XP (20 + 3×dalga). Telsiz: sağ üstte kısa yazı (daktilo efekti, kuyruk) + cızırtı sesi; mesajda yön ("kuzeydeki karakolda"). Hedef işareti: ekrandaysa hedefin üstünde ikon + mesafe, değilse ekran kenarında yön oku + mesafe; sağ üstte görev satırı + tutma ilerlemesi; dünyada ışık sütunu + alan halkası. Alanda dayanırken spawn aralığı ×0.85.
- **Helikopter:** ilk helikopter **2 görev** sonrası (~4. dk), sonrakiler 3 görevde bir (`Objectives.TasksForHelicopter`) → hemen **iniş noktası** (helipad chunk'ı, 7.5 m alan, 30 sn dayan). Son 9 sn'de helikopter uçarak gelip iner (ilerlemeye bağlı; alandan çıkınca havada bekler); tamamlanınca kısa hikâye mesajı + "HELİKOPTER KALKTI" banner'ı, helikopter havalanıp gider; büyük ödül (1000 + 500×önceki helikopter + 50×dalga puan, XP, tam can, 3.5 sn sonra kart seçimi). Döngü baştan başlar (sonsuz mod). Kampanya temeli: `IObjectivePlan` (şimdilik `EndlessPlan`; bölümler kendi planını verir), `ObjectiveDirector.ForceIssue`.
- **Boss — Kasap (F9, `ButcherZombie` + `BossDirector` + saf `BossRules`):** dalga 6'da ilk, sonra her 5 dalgada bir — 11, 16… (`BossRules.IsBossWave`). ~2.7x boy (4.9 m), gözlerde turuncu parıltı (dokudaki kırmızı göz texel'lerinden yerleşir), altında büyük kırmızı halka. Giriş: boss dalga başından 2.5 sn sonra görüş dışında doğar, kamera 2.2 sn ona döner + yaklaşır (`CameraRig.Focus`), ekranda "KASAP" + kükreme, telsiz uyarısı, boss müzik katmanı (`MusicDirector.SetBoss`). Ekran üstünde büyük boss can barı (`BossHUD`, gecikmeli beyaz bar, öfkede turuncu). Boss yaşarken dalga bitmez, normal spawn aralığı ×4, eşzamanlı ×0.2. Durum makinesi: **Yürüme** (yavaş, uzaksa yetişir) → **Hücum** (1 sn telegraph: kükreme + yerde kırmızı şerit, son %30'da yön kilitlenir; 15 m/s; ilk 0.4 sn oyuncuya 110°/sn sınırlı dönüşle bükülür, sonra düz; araba/kasa/barikat/küçük engelleri `PropTosser` ile savurur, varilleri patlatır, zombileri savurur + hasar; binaya (EnvProp yarıçapı ≥ 1.6 veya EnvProp'suz blocker) çarparsa 2 sn **Sersem**) → **Yorgun** 3 sn (2x hasar, sarı parıltı + "ZAYIF!") · **Yer vuruşu** (oyuncu 4.5 m içindeyse 0.8 sn büyüyen halka → 5.5 m şok dalgası; vault ya da halkadan çıkmak kaçırır; ardından 2 sn kalan mavi şok halkası, içinde hareket ×0.4) · **Enkaz fırlatma** (oyuncu ≥ 12 m: 0.7 sn hazırlık, `BossProjectile` yay çizerek hedef + hız öngörüsüne düşer, yerde küçülen kırmızı iniş halkası, 2.8 m / 24 hasar; öfkede 2 parça; 3.5 sn bekleme) · çok yakında itme (temas hasarı ×0.8). Hasar: hücum 28, yer vuruşu 26. Can: taban zombi canı × dalga can çarpanı × 85 (× 1 + 0.3 × önceki boss). Can < %50 **öfke**: hız ×1.35, bekleme ×0.55, arka arkaya **2 hücum** (ikincisi 0.45 sn telegraph). Ölüm: yavaş çekim (0.25x, 1.4 sn) + güçlü sarsıntı, ödül 2000 + 1000×önceki boss + 100×dalga puan, 80 + 10×dalga XP, 2.2 sn sonra **epik kart garantili** seçim ("KASAP DÜŞTÜ!", `Progression.GrantPick(epic)`, `Upgrades.Offer(.., Rarity.Epic)`). Komutlar: `ForceCharge(point)` / `ForceSlam()` / `ForceThrow()`. Oyuncu yavaşlatma: `PlayerController.Slow`.
- **Patlayan varil:** kırmızı varil (turuncu zemin halkası) yollarda/sanayi/limanda; vurunca patlar (zombilere ve oyuncuya alan hasarı, savurma, güçlü sarsıntı, duck + haptic), zincirleme patlar.
- **Okunabilirlik:** kamera 17.5 m (açı 57°, F6.5'te 14 m'den uzaklaştı), askerin altında parlak camgöbeği halka (ölçek 1.9), yakındaki (11 m) ekran dışı zombilere beyaz ok (80 px), hasar sayıları yalnızca kritikte, askeri örten binada asker etrafında dairesel kesik (`Game/EnvCutout`, `CameraRig` → `_CutCircle`), ölüm ekranı %86 karartma.
- **Can:** nefes arasında yenilenme + zombilerden düşük ihtimalle can paketi (`Pickups`, %5 / can < %50 iken %12, mıknatıs 5 m, +25).
- **Kombo:** kill bazlı, 3 sn pencere; çarpan basamakları `GameManager.MultiplierSteps` (5, 12, 22, 35, 50, 70, 95, 125, 160 kill → x2…x10). Hasar almak komboyu kırar (cam sesi + çarpan sallanıp düşer). Skor: kill 10/15 × çarpan, dalga bonusu 50 × dalga.
- **Silah:** 40'lık şarjör; otomatik / taktik / elle reload (sağ alttaki DOLDUR butonu, editörde R). **Aktif reload:** reload sırasında tatlı noktada (%42–60) tekrar basınca anında biter + sonraki şarjör %15 hasar; yanlış anda basınca 0.6 sn tutukluk.
- **Görsel:** Çizgi film tarzı asker + zombi; kamera ~55–60° eğik, oyuncuyu takip eder.
- **Skor + kombo** sistemi (ardışık öldürmeler çarpan verir).
- **Öncelik: vuruş hissi** — hit flash, knockback, kamera sarsıntısı, hit-stop, partikül, ses.
- **Ses:** CC0 kaynaklar (Kenney, OpenGameArt) + Python sentezi; her dosyanın kaynağı/lisansı `CREDITS.md`'de. Lisansı CC0 veya ticari kullanıma açık olmayan ses **eklenmez**. Toplam ses bütçesi ≤ 6 MB (şu an ~2.3 MB). Özel zombi / kart sesleri: `python Tools/synth_specials.py .`; telsiz / görev / helikopter: `python Tools/synth_objectives.py .`; boss sesleri + boss müziği: `python Tools/synth_boss.py .` (Vorbis'e blok blok yazar; tek parça yazım libsndfile'ı çökertiyor)
- **Performans:** Hedef 60 FPS mobil; mermi/zombi/efektler için **object pooling**, Update'te allocation yok.
- **Animasyon:** Mixamo Humanoid (Soldier / Zombie avatar), root motion kapalı. Asker: alt gövde 8 yönlü locomotion (hareket yönü nişan yönüne göre lokal → geri geri koşarken ateş), üst gövde avatar mask ile nişan/reload, vault = jump, 2 ölüm animasyonu. Zombi: idle/walk/run, temas anında saldırı, doğarken çığlık, ölümde önce savrulma sonra dying.
- **Silah:** Tüfek göğüs önünde nişan yönünde tutulur, iki el IK ile kabza / el kundağına; mermi ve muzzle flash namlu ucundan. 30'luk şarjör, boşalınca otomatik reload (1.5 sn, animasyon hızı senkron), reload sırasında ateş yok; tetik bırakılıp 1 sn geçince ve şarjör yarıdan azsa taktik reload.

## Görsel stil (UI)
- Çizgi film / askeri: haki yeşil (`UiKhaki` 0.34/0.40/0.22, koyu `UiKhakiDark`) + turuncu vurgu (`UiOrange`), krem yazı, kalın koyu kontur (`UiInk`), hafif gölge, yuvarlatılmış köşeler. Tüm HUD + kart ekranı aynı parçalarla: `UI_Panel` (9-slice, kontur + iç gölge), `UI_Flat`, `UI_Shadow`, `UI_Glow`, noktalar, kalın halka — builder üretir (`GameSceneBuilder.Icons.cs`).
- Font: **Rubik ExtraBold** (OFL, `Assets/Art/UI/Fonts`, değişken fonttan statik kesit; tüm Türkçe karakterler). Yazılar Outline (kalınlık boyuta göre) + Shadow. Fontta olmayan semboller (→ ● ○ ♥) metinde kullanılmaz; ikon/sprite kullan.
- İkonlar: 15 kart + kalp/yıldız/kafatası, SDF vektör (beyaz iki ton + koyu kontur, runtime'da nadirlik rengiyle tint). Kenney ikon paketleri ortamdan indirilemedi; istenirse sonra değiştirilebilir. `Upgrades.cardSprites` / `IconOf`.
- Can barı: çerçeve, iç oluk, hasarda geride kalan beyaz gecikmeli bar, kalp ikonu (hasarda/düşük canda atar), sayı. XP barı turuncu, seviye atlamada parlar, yıldız rozet seviye sayısıyla döner. Kart rozetleri: çerçeveli, köşede seviye sayısı, alınınca parlama.

## Yapı
- `Assets/Scripts` · `Assets/Prefabs` (+`Env/`) · `Assets/Scenes` · `Assets/Art` · `Assets/Audio` · `Assets/Materials` · `Assets/Settings` (URP asset + renderer)
- Özel zombiler: `Assets/Art/Characters/{ShieldZombie,BombZombie,TongueZombie}/` — `*_Rigged.fbx` (~20k tri, Mixamo), `Anim/` (Mutant Walking/Punch/Roaring/Dying · Drunk Walk/Zombie Running/Agonizing · Sneaking Forward/Frisbee Throw/Pulling A Rope; normal zombi avatarı üzerinden retarget, idle/hit/dying normal zombiden), `Textures/*_BaseColor.png`. Kaynak glb'ler `_mixamo/Source~/`, karşılaştırmalar `_mixamo/preview/` (`Tools/blender_preview_compare.py`). Controller'lar builder'da `BuildMonsterController`.
- Boss: `Assets/Art/Characters/Kasap/` — `Kasap_Rigged.fbx` (Mixamo, optimize kapalı: göz parıltısı Head kemiğinde), `Anim/kasap {walking,run,roaring,jump attack,breathing idle,dying}.fbx` (kaynak `_mixamo/anim/Kasap/`, kendi avatarı üzerinden), `Textures/Kasap_BaseColor.png`. Builder: `GameSceneBuilder.Boss.cs` (prefab, telegraph quad'ları, `BossHUD`, `BossDirector`, `PropTosser`). Klipler yerinde (root hız 0) → hız `bossSpeed`; yer vuruşu çarpma anı klip uzunluğunun yarısı varsayılır, `animator.speed` telegraph'a göre ayarlanır.
- Karakterler: `Assets/Art/Characters/{Soldier,Zombie}/*_Rigged.fbx` = Mixamo'da riglenmiş düşük poligon mesh (Soldier 9k, Zombie 6k tri), Humanoid avatar (Zombie: Optimize Game Objects). Animasyonlar `Anim/*.fbx` (yalnızca kullanılanlar; tam set `_mixamo/anim/` altında, git'e dahil değil). Dokular `Textures/*_BaseColor.png` (max 1024, ASTC 6x6). Eski statik `*.glb` ve `Source~/` orijinalleri referans için duruyor.
- Animator: `Assets/Animation/Soldier.controller`, `Zombie.controller`, `UpperBody.mask` — builder üretir. Import: loop yalnız döngü kliplerinde; root rotation + height bake, XZ bake yalnız tek seferlik kliplerde (ölüm, saldırı, reload). Zombi "scream" için Mixamo **Zombie Scream** yok → `zombie agonizing` vekil; `_mixamo/anim/zombie/zombie scream.fbx` → `Assets/Art/Characters/Zombie/Anim/` kopyalanıp builder çalıştırılırsa otomatik kullanılır.
- Silah: `Assets/Art/Weapons/Rifle/Rifle.glb` — Quaternius Assault Rifle (CC0, poly.pizza), `Tools/blender_normalize_weapon.py` ile namlu +Z / yukarı +Y / 0.85 m. Prefab içinde ×1.1, RightGrip / LeftGrip / Muzzle noktaları builder'da (`GameSceneBuilder.Characters.cs`).
- Ortam: `Assets/Art/Environment/{Cars,Survival,Roads,Commercial,Industrial,Suburban,Graveyard}` — Kenney (CC0, lisanslar klasörlerde) GLB'leri + kit başına `Textures/colormap.png`. Builder bunları düzleştirip gerçek boyuta ölçekler → `Assets/Prefabs/Env/*.prefab` (pivot alt-orta, BoxCollider, `EnvProp.radius`, yüksekler `Fadeable`; ağaçlarda ince gövde collider'ı). Prosedürel (primitive) prefab'lar `GameSceneBuilder.City.cs`'te: benzin istasyonu sundurması/tabelası, otobüs durağı, çeşme, obelisk, asker heykeli (Soldier mesh'i idle pozda bake → `StatueSoldier.asset`), sinyal kulesi, jeneratör, helipad, hastane tabelası; hedef objeleri `ObjFlare`, `ObjSupply`, `Helicopter`. Toplam ~4 MB (sınır: ortam ≤ 5 MB). Ek Kenney kiti gerekirse: kenney.nl sayfasındaki `data-download-id` → `/download-link/<id>` (User-Agent gerekli).
- Mixamo: `_mixamo/{Soldier,Zombie}.fbx` (T-pose, 1.8 m, origin ayakta, doku gömülü). Yeniden üret: `blender -b --factory-startup -P Tools/blender_export_mixamo.py -- <glb> <basecolor.png|-> <out.fbx> <boy> [üçgen] [nobake] [seams]` (`-` = gömülü doku). Üçgen verilince **varsayılan**: kaynak + decimate + yeniden kaynak, yeni UV, orijinal yüksek poligondan 1024 diffuse bake (selected-to-active), normaller dışa, tek mesh, transform apply. `seams` (dikişleri kesip orijinal UV'yi korumak) Mixamo'da mesh'i parçalara ayırıp şekil/doku bozuyor — kullanma. Kontrol: `Tools/blender_preview_compare.py` → `_mixamo/preview/`.
- Script'ler `Game` asmdef'inde:
  - Core: Loc (localization tablosu), GameManager (+`Killed` event, `SetSlow`, `SlowMo`, `AddBonus`), WaveDirector, Objectives (saf kurallar, `EndlessPlan`/`IObjectivePlan`), ObjectiveDirector (hedef state'i + telsiz event'leri), BossRules (saf boss kuralları), BossDirector (boss dalgası, giriş, ödül, event'ler), Pool, GameDefs (layer'lar, `Vault`, `WaveParams`, `Difficulty` + özel zombi oran/limitleri), Explosions (ortak alan hasarı + `OnExplosion`), Progression (XP/seviye), Upgrades (kartlar + aktif kartlar), Haptics, GameAudio (+`AudioPrefs`), MusicDirector, ListenerFollow, Warmup
  - Input: TouchControls (`Blocked`, `CardPressed`) · Player: PlayerController (+dil yakalaması `Grab`), SoldierRig, WeaponLoadout, Weapon (şarjör/reload, `pierce`, `extraBullets`, `SpawnBullet`), Bullet (delme), Turret
  - Enemies: Zombie (taban; `UpdateBehaviour`/`Deflects`/`OnKilled` kancaları, `kind`, `score`, `xp`), ShieldZombie, BombZombie, TongueZombie (+TongueHitbox), ButcherZombie (Kasap boss), ZombieSpawner (tür başına pool, `TrySpawnSpecial`, `SpawnBoss`; boss geri dönüştürülmez)
  - World: CityLayout (saf yol ağı + chunk tipi), ChunkManager (sonsuz şehir, atlas zemin, tip yerleşimleri, `ForceType`/`FindType`), DistrictAtmosphere, ObjectiveVisuals (ışık sütunu, alan halkası, fişek/sandık), HelicopterFx, FloatingOrigin, EnvProp, Fadeable (kesik materyaline geçer), ExplosiveBarrel, Pickups, PropTosser (savrulan engeller, slot dizisi) · FX: CameraRig (+`Focus`), Fx (+`Explosion`), HitFlash · UI: HUD (+XP/seviye/kart ikonları, `ShowBanner`), DamageNumbers, CardPicker, ThreatIndicators, ObjectiveHUD (telsiz, görev satırı, hedef işareti/kenar oku), BossHUD (boss barı, giriş yazısı, ZAYIF!), MainMenu, SettingsPanel, PauseMenu, LoadingScreen (sahne geçişi), ShopUI, LoadoutPanel, DailyLoginPanel, QuestsPanel (her MonoBehaviour kendi adındaki dosyada — yoksa sahnede gömülü script olur, Awake çalışmaz; `ScriptFileTests`), PreviewStage · Core ayrıca: SaveSystem (+SaveData, ISaveStore), Settings, Economy, Profile, RunRewards, StoreService, AdService · World ayrıca: MenuCamera
- **Sonsuz dünya:** 20x20 m chunk, oyuncu etrafında 5x5 aktif, chunk ve objeler pool'lu, seed'li (`ChunkManager.seed`, `randomSeed`). Mahalle = seed'li Perlin (`DistrictAt`): Cadde / Çarşı / Sanayi / Liman (obje seti `DistrictSettings`). **Yol ağı** saf/seed'li `CityLayout` (EditMode testli): yollar chunk kenarlarında (yarım 7 m yol + kaldırım); dikey hatlar sonsuz caddeler (her çift hat + bazı tek hatlar), yatay sokaklar yalnızca bazı satırlarda ve iki cadde arası açıklık atlanabilir (T kavşak) → çoğu chunk kavşaksız düz cadde, her yol komşuya bağlanır, çıkmaz yok; kavşaklarda yaya geçidi + trafik lambası. Yol engelleri (araba/barikat/varil) yalnız yol olan kenarlarda. **Chunk tipleri** (`ChunkType`, mahalleye göre ağırlık `CityLayout.TypeWeights`): Block (bina veya prop), Park (çim, ağaç, bank, alçak çit, saksı, lamba), Plaza (karo, heykel/obelisk/çeşme, banklar), Alley (yalnız G/K kenarı yolsuzsa; iki sıra dar bina arasında 5 m sokak + çöp/varil), Rubble (çatlak zemin ve yol, yıkık bina, kaya, kırık duvar, yanık araç, enkaz), simge yapılar GasStation / PoliceStation / Hospital (en az bir yol kenarı; hedef noktası `CityLayout.SiteLocal` boş tutulur), hedef yerleri SignalTower / Helipad (yalnız `ForceType` ile). Spawn çevresi 2x2 chunk hep Block. Otobüs durağı yol kenarında %12. **Zemin:** chunk başına tek mesh, `Ground_Atlas.png` (1024², 4x4 hücre, 8 px pay; `GroundAtlas`): yol şeridi / zebra / çatlak, kavşak köşesi, düğüm köşesi, 4 mahalle + park / meydan / yıkıntı / ara sokak / beton iç alan → chunk başına 1 draw call. **Mahalle tonu:** `DistrictAtmosphere` ambient + güneş rengi + üstel sis (Liman en yoğun), oyuncunun mahallesine yumuşak geçiş (yalnız kozmetik; `Game/EnvCutout` sisi destekler).
- **Floating origin:** oyuncu 120 m'yi geçince tüm root objeler chunk katı kadar kaydırılır (`FloatingOrigin.OnShift` event'i; dünya pozisyonu tutan sistemler buna abone). Global konum: `FloatingOrigin.ToGlobal`.
- Spawn: kamera viewport'unun hemen dışı (yere izdüşüm, `viewMargin` 0.05 + 1 m), oyuncunun hareket yönüne ağırlıklı; 40 m'den uzakta kalan zombiler geri dönüştürülür. Kamera uzaklaşınca spawn da uzaklaşır → denge değişir (F6.5'te human botu 400+ sn'ye çıktı; margin küçültülüp `AlivePerWave` 6 yapıldı).
- **Sahne kod ile üretilir:** `Assets/Editor/GameSceneBuilder.cs` (menü: Tools/Zombie Shooter/Rebuild Game Scene veya `-executeMethod GameSceneBuilder.Build`). Üretilen dokular, materyaller, tüm prefab'lar, `Resources/LoadingScreen.prefab`, Menu.unity + Game.unity, build sahne listesi ve Player Settings (ad, bundle id, splash) yeniden yazılır — elle yapılan değişiklikler kaybolur, kalıcı değişiklikleri builder'a işle.
- Layer'lar: 8 Blocker (bina, konteyner, yanan varil, tank — vault edilmez, mermi durdurur), 9 Vaultable (kasa, araba, barikat, çöp konteyneri), 10 Zombie, 11 Player, 12 Preview (mağaza önizlemesi).
- Shader'lar `Assets/Shaders`: Game/FxAdditive, Game/FxAlpha, Game/Flash (URP unlit). Karakter/çevre: URP Simple Lit. Kameranın önünü kapatan binalar `Fadeable` ile yarı saydam olur.
- **Ses sistemi:** `GameAudio.Play(SfxId, pos, pitchMul, volumeMul)` tek giriş noktası (+ `OnSfx` event). `SfxEntry` bankası (builder'da `SoundBank()`): klip varyantları (aynısı arka arkaya çalmaz), voice limiti (aşınca en eski çalınır), minInterval, pitch ±%8 + volume varyasyonu, spatialBlend/rolloff (Linear 8→40 m; listener oyuncunun 2 m üstünde, `ListenerFollow`), **haptic ve music duck aynı entry'de** → atış/kill/hasar haptic'i sesle senkron; başka yerde `Haptics.Play` çağırma. Reload parçaları reload klibindeki animation event'lerden (`SoldierRig.OnReloadEvent`).
- Müzik: `MusicDirector` iki loop katmanı (calm ↔ intense) yakındaki zombi sayısı + kombo + ateşe göre crossfade; `Duck()`; ölümde `AudioLowPassFilter` ile ~550 Hz'e boğulur.
- Mixer: `Assets/Audio/GameMixer.mixer` (Master → Music / SFX / UI), exposed `MasterVolume/MusicVolume/SfxVolume/UiVolume`; ayarlar `AudioPrefs.Set/Get/Apply` (kayıttaki `settings.music` / `settings.sfx` — efekt SFX + UI gruplarını sürer; 0..1 → dB). Mixer'ı builder internal `AudioMixerController` API'sıyla reflection üzerinden üretir.
- Ses import: SFX mono + Vorbis q0.5, ≤1 sn → Decompress On Load, uzun → Compressed In Memory; müzik Streaming q0.4. Ses dosyalarını yeniden üretmek: `python Tools/build_audio.py <indirilen_paketler> .` (numpy, scipy, soundfile).
- **Takılma önleme:** `Warmup` ilk karede tüm materyalleri (quad + skinned örnek) gizli RT'ye bir kez render eder ve UI font atlasını doldurur → ilk kullanımda shader/PSO derleme takılması yok. Şüpheli yollar `ProfilerMarker`'lı (`Game.ChunkBuild`, `Game.PoolGrow`); `PlaytestBot` 100 ms üstü kareleri marker değerleriyle loglar.
- Test hızlandırma: `GameManager.TimeScale` (oyunda 1; smoke testleri 3–4, botlar 8). `TouchControls` en önce çalışır (`DefaultExecutionOrder(-100)`); ekrandaki butonlar (DOLDUR, TEKRAR) üstlerine düşen dokunuşu yutar.
- PlayMode testleri `Assets/Tests/PlayMode/GameSmokeTest.cs`: SpawnShootDieRestart (+ölümde müzik low-pass, TEKRAR butonu), VaultOverObstacle, FloatingOriginAndChunks, MagazineAndReload, ManualAndActiveReload, WavesBreatherAndCombo, AudioHooks, SpecialZombies, CardsBarrelsReadability, CityAndObjectives (chunk tipleri, atlas zemin, sis, 3 hedef + helikopter, telsiz, kenar oku), BossButcher (hücum/şerit, yorgunluk 2x, engel savurma, duvar sersemlemesi, yer vuruşu + kaçış, öfke, ödül + epik kart); [Explicit] CaptureBoss / CaptureSpecials / CaptureF6 / CaptureCity (HUD'lu PNG); `PlaytestBot.Play(strateji)` [Explicit, Category Balance] denge botları (bkz. Kurallar → test kuralları, `docs/balance.md`); smoke testleri kart seçmez (`LoadGame` doğal seviye atlamayı kapatır); `CaptureScreens` [Explicit] gameplay + yakın çekim PNG'leri alır (env `CAPTURE_DEBUG=1` tüfeği kırmızı boyar ve konumunu loglar) (`-testFilter GameSmokeTest.CaptureScreens`, env `CAPTURE_DIR`, `-nographics` olmadan). Çalıştır: `Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults <xml> -logFile <log>`.

## Kurallar
- Kullanıcıya onay sorma; adımları kendin yap.
- **Push yok** (yalnızca yerel commit) — kullanıcı açıkça isterse `origin main`'e push.
- Prompt sonundaki **"Builder, CLAUDE.md, commit, push yok"** = builder'ı çalıştır, CLAUDE.md'yi güncelle, commit at; **yalnızca push yapma**.
- "Mor kart" = **epik** nadirlik (`Rarity.Epic`).
- İş bitince **kısa rapor** ver.
- **Ağa hazır mimari:** yeni sistemleri ileride host-authoritative co-op'a taşınabilecek şekilde yaz: oyun mantığı (state + kurallar) görselden/sesten ayrı (görsel event dinler, state'i değiştirmez); rastgelelik seed'li ve sistem başına kendi `System.Random`'ı (`UnityEngine.Random` yalnız kozmetik efektlerde); state tek noktadan (manager/servis) değişir, girdi komut olarak gelir (dokunuş → niyet: hareket vektörü, nişan, ateş, reload). Saf formüller statik ve test edilebilir (`Difficulty`, `Upgrades.*Mul`, `Upgrades.Offer`, `GameManager.MultiplierFor`).
- **Test kuralları (süre):**
  - Her fazda yalnızca **EditMode birim testleri + PlayMode smoke testleri** (~1.5 dk). EditMode: `Assets/Tests/EditMode/LogicTests.cs` (zorluk formülü, XP eğrisi, kombo basamakları, kart teklif/nadirlik/formüller, localization tablosu) — formül/kart/XP mantığı değişince buraya test ekle.
  - **Bot denge testleri yalnızca dengeyi etkileyen değişikliklerde** (zombi, kart, hasar, spawn, silah).
  - Botlar **tek Unity oturumunda sırayla** (`PlaytestBot.Play` parametreli: turret, human, kite, explore), ekran görüntüsü kapalı, `-nographics`, **timescale 8** (`Time.captureDeltaTime` her kareyi tam 1/60 sn oyun süresine sabitler → fizik adımıyla (0.02) uyumlu ve timescale'den bağımsız; timescale yalnızca hız üst sınırı, bu makinede gerçekte ~4x, 4 bot ≈ 5 dk), **600 sn tavan**; bot özetinde boss satırı (`boss=doğan/yenilen`, `bossFight`, `bossHpLeft`, hücum/yer vuruşu isabetleri). `PLAYTEST_BOSS=1` (+`PLAYTEST_BOSS_LEVEL`, vars. 8): seviye atlatıp ilk boss dalgasının nefes arasına atlar (yalnız boss dövüşünü ölçmek için). 8x ile 3x aynı seed'le doğrulandı (fark koşu varyansı içinde, bkz. `docs/balance.md`). Komut: `PLAYTEST_TIMESCALE=8 CAPTURE_DIR=<klasör> Unity.exe -batchmode -nographics -projectPath . -runTests -testPlatform PlayMode -testFilter PlaytestBot.Play -testResults <xml> -logFile <log>`; opsiyonel `PLAYTEST_STRATEGIES=turret,human`, `PLAYTEST_SEED=<n>`, `PLAYTEST_MAXTIME`, `PLAYTEST_SHOTS=1`.
  - Aynı anda tek Unity; her koşudan sonra süreç kapanmış olmalı.
- Unity batchmode: `"C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod X.Y -quit -logFile <log>` (Unity editörü açıkken çalışmaz).

## Token tasarrufu
- Dosyayı tamamen okuma; önce grep/arama, sonra yalnızca ilgili satır aralığı. Aynı dosyayı tekrar okuma.
- Unity/test/Blender çıktısını tamamen basma: yalnızca hata/uyarı ve özet satırları (tail/grep). Uzun logları dosyaya yaz, gerekirse oradan ara.
- Ekran görüntüsü yalnızca görsel değişiklik yapılan işlerde ve en fazla 2–3 kare.
- Builder'ı faz başına mümkünse bir kez çalıştır; deneme-yanılma için küçük EditMode testleri kullan.
- Bot testleri yalnızca denge etkileyen işlerde, tek oturumda.
- Rapor en fazla 15 satır: yapılanlar, sapmalar, benden beklenen. Kod parçası, uzun tablo, tekrar eden açıklama yok.
- Belirsizlikte en makul seçimi yap, rapora tek satır not düş; soru sorma.

## Yol haritası
Oyuncu incelemesi: `docs/playtest-01.md` (bot ile oynatma: `PlaytestBot`).
- **F5 — Oyun döngüsü:** dalgalar + nefes arası, can yenilenmesi/paketi, kill bazlı kombo, 40'lık şarjör + elle/aktif reload, sıkı vault, zengin ölüm ekranı, ilk açılış ipucu, takılma düzeltmesi.
- **F6 — Roguelite + okunabilirlik:** ✔ (2026-11-15)
- **F7 — Özel zombiler:** ✔ Kalkanlı, Bombalı, Dilci (2026-11-15). Sonra: boss'lar (engel aşabilen).
- **F8 — Şehir çeşitliliği + hedefler:** ✔ (2026-11-16) kavşaksız caddeler, park/meydan/ara sokak/yıkıntı, simge yapılar, mahalle ışık/sis tonu; telsiz, fişek/kule/ikmal hedefleri, helikopter iniş noktası.
- **F9 — İlk boss Kasap:** ✔ (2026-11-16). Sonra: yeni boss'lar (laboratuvar mutasyonları, özgün tasarım; bazıları engel aşar — atlama/ezip geçme).
- **Kampanya + sonsuz mod:** kampanya haritası, bölüm hedefleri (hayatta kal / bölgeye ulaş / fişek yak / kurtar), bölüm başına 1–3 yıldız; mevcut oyun "sonsuz mod" olarak kalır.
- **Mağaza:** kazanılan para (oyun içi) + ücretli para; **kozmetik / kolaylık ağırlıklı, pay-to-win yok** (güç satılmaz); ödüllü reklam (ör. ölümde devam, ödül ikiye katlama).
- **Co-op (ileride, 2–4 kişi):** önce oda kodu ile, sonra Game Center / Google Play Games eşleştirme; host-authoritative (bkz. Kurallar → ağa hazır mimari).
- **F6.5 — Görsel cila:** ✔ (2026-11-15) kamera 17.5 m, ortak UI stili + font + ikonlar, yeni kart ekranı, can/XP barları, kart rozetleri, taret canı/hedeflenmesi, kart güvencesi.
- **F10 — İsim + menü + ayarlar + kayıt:** ✔ (2026-11-16).
- **F13 — Ekonomi + mağaza:** ✔ (2026-11-16). Kampanya F12.
- **Sonra:** meta ilerleme (silah açma, görevler), mağaza, kampanya.

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
- [x] **Faz 5 — Oyun döngüsü** (2026-11-15)
  - [x] `docs/playtest-01.md` (oyuncu incelemesi) + yol haritası
  - [x] Dalgalar + nefes arası + "DALGA N" banner'ı + geri sayım; zorluk dalgaya bağlı, botlarla dengelendi (sabit ~100 sn, akıllı 507 sn)
  - [x] Can: nefes arası yenilenme + mıknatıslı can paketi
  - [x] Kill bazlı kombo, zorlaşan çarpan basamakları, hasar alınca kırılma (cam sesi + düşen çarpan), küçük skor değerleri
  - [x] 40'lık şarjör, DOLDUR butonu, aktif reload (tatlı nokta / tutukluk)
  - [x] Sıkı vault (≥%90 itme, ±35°)
  - [x] Ölüm ekranı (istatistik, rekor kutlaması, TEKRAR), ilk açılış ipucu
  - [x] Takılma: ilk kullanımda shader derleme → `Warmup` ile giderildi (soğuk cache'le 255 sn turda 0 takılma)
- [x] **Faz 7 — Özel zombiler** (2026-11-15)
  - [x] Kalkanlı / Bombalı / Dilci: Mixamo rig + retarget klipler, davranışlar, renkli halka, doğuş sesi, ekran dışı ok
  - [x] Dalga 3'ten karışık spawn, dalgayla artan oran + tür limiti, her 7. dalga özel dalga
  - [x] Ortak `Explosions` (zombilere + oyuncuya, duck/sarsıntı/haptic), sentez/CC0-türevi sesler (`Tools/synth_specials.py`)
- [x] **Faz 6 — Roguelite + okunabilirlik** (2026-11-15)
  - [x] XP/seviye, yavaşlayan 3 kart seçimi, 15 kart × 3 seviye, nadirlik renkleri, HUD ikonları
  - [x] Patlayan varil (zincirleme), taret, otomatik bomba, pompalı
  - [x] Kamera 14 m, asker halkası, ekran dışı tehdit okları, yalnız kritik hasar sayısı, dairesel bina kesiti, koyu ölüm ekranı
  - [x] "human" bot (%60 aktif reload), tüm botlar kart seçer; `docs/balance.md`
- [x] **Faz 8 — Şehir + hikâye hedefleri** (2026-11-16)
  - [x] `CityLayout`: sonsuz caddeler + seyrek sokaklar (kavşaksız uzun caddeler, T kavşaklar, çıkmaz yok), chunk tipleri (park, meydan, ara sokak, yıkıntı, benzinlik, karakol, hastane, sinyal kulesi, helipad), otobüs durağı, trafik lambası
  - [x] Atlas zemin (chunk başına tek mesh), mahalle ışık/sis tonu (`DistrictAtmosphere`)
  - [x] Kenney Suburban + Graveyard (CC0) + ek araç/enkaz modelleri, prosedürel yapılar, asker heykeli
  - [x] `ObjectiveDirector`: fişek / kule / ikmal (her 3 dalgada), 3 görev → helikopter (iner, bekleyemeden kalkar); bonus puan + XP; telsiz + kenar oku + mesafe; sentez sesler
  - [x] Botlar hedeflere gider (turret hariç), `docs/balance.md` F8 tablosu
- [x] **Faz 10 — Holdout: menü, ayarlar, kayıt** (2026-11-16)
  - [x] Ad "Holdout", bundle `com.oztdigital.holdout`; Menu + Game sahneleri, yükleme ekranı (ipuçları), OZT splash
  - [x] Ana menü (logo, canlı şehir, altın/elmas, Oyna/Kampanya kilitli/Sonsuz, Mağaza kilitli, Ayarlar), ayarlar paneli, duraklatma + arka planda otomatik duraklatma
  - [x] Tek JSON kayıt (sürüm + göç, yedek, `ISaveStore`); EditMode kayıt/FPS testleri, PlayMode `MenuSettingsPauseFlow`, [Explicit] `CaptureMenu`
- [x] **Faz 13 — Ekonomi + mağaza** (2026-11-16)
  - [x] Altın/elmas, tur sonu altını (sayaç), 7 günlük giriş, günlük 3 görev
  - [x] 5 silah (ayrı ses/his), tur öncesi seçim, kalıcı yükseltmeler; 7 kostüm; kalıcı bonuslar
  - [x] Mağaza (4 sekme, 3D önizleme, onay), Unity IAP + sahte mağaza, geri yükleme, LevelPlay ödüllü reklam (test), ATT
  - [x] Kayıt v2 (göç + imza), `docs/economy.md`, EditMode `EconomyTests`, PlayMode `ShopLoadoutAndRunRewards`
- [ ] Kampanya (F12)
- [x] **Faz 9 — İlk boss "Kasap"** (2026-11-16)
  - [x] Mixamo rig + 6 klip, ~2.7x, göz parıltısı, kırmızı halka; hücum (şerit, savurma, duvar sersemlemesi), yer vuruşu, yorgunluk 2x, öfke
  - [x] Boss dalgası (10, 15, 20…), kamera girişi + isim + boss müziği, boss barı, azaltılmış spawn; ödül + yavaş çekim + epik kart
  - [x] İlk helikopter 2 görevde; sentez sesler (`Tools/synth_boss.py`); bot tavanı 600 sn, botlar hücumdan/yer vuruşundan kaçar
- [ ] Yeni boss'lar (laboratuvar mutasyonları, özgün tasarım; bazıları engel aşabilir)
- [ ] Cihazda performans profili (60 FPS hedefi, 70 zombi), gerekirse GPU instancing/LOD
- [ ] Silah çeşitleri / power-up, dalga yapısı, ana menü
