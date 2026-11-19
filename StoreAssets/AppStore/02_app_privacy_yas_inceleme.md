# App Privacy, yaş derecelendirmesi, inceleme notu

## 1) App Privacy (App Store Connect → App Privacy → Get Started)

Kodda ne var (kontrol edildi): hesap / giriş yok, kayıt yalnız cihazda (`save.json`), kendi sunucumuz ve analitik SDK'mız yok. Veri toplayan üçüncü taraflar:
- **Unity LevelPlay** (ödüllü reklam; ironSource + Unity Ads) — IDFA (yalnız ATT izni verilirse), cihaz bilgisi, reklam etkileşimi, IP'den kaba konum, çökme/performans verisi.
- **Unity Gaming Services** (yalnız Co-op): anonim oturum (rastgele oyuncu kimliği), Relay/Lobby bağlantısı.
- **Satın alımlar:** ödeme Apple'da; uygulama yalnız ürün kimliğini ve işlem kimliğini cihazda tutar, kimseye göndermez → "Purchase History" **toplanmıyor**.

**"Do you or your third-party partners collect data from this app?" → Yes**

| Veri türü | Kullanım amacı | Kullanıcıya bağlı mı? | İzleme (tracking) için mi? |
|---|---|---|---|
| Identifiers → **Device ID** (IDFA / IDFV) | Third-Party Advertising | Hayır | **Evet** |
| Identifiers → **User ID** (Co-op anonim kimlik) | App Functionality | Hayır | Hayır |
| Usage Data → **Advertising Data** | Third-Party Advertising | Hayır | **Evet** |
| Usage Data → **Product Interaction** | Third-Party Advertising, Analytics | Hayır | Hayır |
| Location → **Coarse Location** (IP'den) | Third-Party Advertising | Hayır | Hayır |
| Diagnostics → **Crash Data**, **Performance Data** | App Functionality, Analytics | Hayır | Hayır |

Toplanmayanlar: kişi bilgisi (ad, e-posta, telefon), sağlık, finans, hassas bilgi, kişiler, kullanıcı içeriği, tarama / arama geçmişi, kesin konum, satın alma geçmişi.

> Not: LevelPlay'in güncel "Apple App Privacy" sayfasıyla son bir kez karşılaştır (ağ adaptörü eklenirse liste değişir). Şu an yalnız LevelPlay çekirdeği + Unity Ads kullanılıyor.

## 2) Yaş derecelendirmesi (Age Rating)

| Soru | Cevap |
|---|---|
| Cartoon or Fantasy Violence | **Frequent / Intense** (sürekli zombi vurma, karikatür tarzı) |
| Realistic Violence | None |
| Prolonged Graphic or Sadistic Realistic Violence | None |
| Horror / Fear Themes | **Infrequent / Mild** (zombiler, mutantlar) |
| Profanity, Sexual Content, Nudity, Alcohol/Tobacco/Drugs | None |
| Gambling / Simulated Gambling / Contests | None |
| Medical / Treatment Information | None |
| Unrestricted Web Access | No |
| User-Generated Content / Messaging / Chat | No (Co-op'ta sohbet yok) |
| In-App Purchases | **Yes** |
| Advertising | **Yes** (yalnız isteğe bağlı ödüllü reklam) |
| Made for Kids | **No** |

Beklenen sonuç: **12+** (yeni sistemde 13+). Apple hesaplar; çıkan derece bu civarda olmalı.

## 3) App Review Information

- **Sign-in required:** No (hesap yok).
- **Contact:** OZT Digital, oztdigitaldestek@gmail.com, (telefon numaranı yaz).
- **Notes (EN, kopyala):**
```
Holdout: Last City is a top-down zombie shooter. No account or login is needed.

Controls: left half of the screen = move (floating joystick), right half = hold to fire, drag to aim.

Modes: Story (6 chapters), Endless, Hard and Co-op. Co-op needs 2–4 devices: one taps PLAY > CO-OP > CREATE ROOM and shares the 6-character code, the others enter it and tap JOIN; the host taps START. Single-player modes cover all features without a second device.

In-app purchases (Shop > Gems): gem packs (consumable), Starter Pack and VIP (non-consumable). Gems only buy cosmetic characters/costumes, gold and a one-time continue; no gameplay power is sold. Restore Purchases: Settings > Restore Purchases.

Ads: only optional rewarded ads (Unity LevelPlay): 2x end-of-run gold, one continue per run, daily free gems. The App Tracking Transparency prompt appears before the ad SDK starts; if declined, ads are non-personalised.
```

## 4) Yayın seçenekleri
- **Version Release:** "Manually release this version" (onaydan sonra sen yayınla) önerilir.
- **Fiyat:** Ücretsiz (Price Schedule → Free), tüm ülkeler veya Türkiye + seçtiklerin.
- **Build:** TestFlight'taki son build (test modu kapalı olan) seçilmeli — bkz. aşağı.
- **IAP:** build sayfasında "In-App Purchases and Subscriptions" bölümüne 6 ürünü ekle (ilk sürümle birlikte incelenir).
