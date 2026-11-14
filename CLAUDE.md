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
- Karakterler: `Assets/Art/Characters/{Soldier,Zombie}/*.glb`. Orijinal dosyalar `Source~/` altında (EXT_texture_webp; glTFast desteklemediği için dokular PNG'ye çevrildi).

## Kurallar
- Kullanıcıya onay sorma; adımları kendin yap.
- **Push yok** (yalnızca yerel commit).
- İş bitince **kısa rapor** ver.
- Unity batchmode: `"C:/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe" -batchmode -nographics -projectPath . -executeMethod X.Y -quit -logFile <log>` (Unity editörü açıkken çalışmaz).

## İlerleme
- [x] Proje kurulumu: URP, glTFast, Input System, Player Settings (landscape, bundle id), klasörler, Game sahnesi, git
- [ ] Oyuncu hareketi + floating joystick
- [ ] Ateş / nişan (sağ yarı, auto-aim)
- [ ] Zombi spawn + AI + pooling
- [ ] Vault sistemi
- [ ] Skor + kombo + UI
- [ ] Vuruş hissi (juice)
- [ ] Mixamo rig + Humanoid animasyonlar
