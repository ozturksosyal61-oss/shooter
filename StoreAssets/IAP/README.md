# Holdout — App Store Connect uygulama içi satın alımlar

Her klasörde:
- `promo_1024.png` → **Promotional Image / Image** (1024×1024, opak RGB; dilden bağımsız, yazı yalnız sayı / VIP)
- `review_screenshot.png` → **Review Information → Screenshot** (mağaza ekranı, 2688×1242)

Görselleri yeniden üret: CaptureUI testi (`CAPTURE_DIR=<klasör>`), sonra `python Tools/make_iap_images.py . <klasör>`.

Fiyat: **Price Schedule** → temel ülke Türkiye, aşağıdaki TL fiyatı (diğer ülkeler otomatik).
Ürün kimlikleri oyunla birebir aynı olmalı (`Economy.Products`).

| Klasör | Reference Name | Product ID | Tür | Fiyat |
|---|---|---|---|---|
| gems_small | Gems Small | `com.oztdigital.holdout.gems_small` | Consumable | 49,99 TL |
| gems_medium | Gems Medium | `com.oztdigital.holdout.gems_medium` | Consumable | 99,99 TL |
| gems_large | Gems Large | `com.oztdigital.holdout.gems_large` | Consumable | 149,99 TL |
| gems_huge | Gems Huge | `com.oztdigital.holdout.gems_huge` | Consumable | 299,99 TL |
| starter_pack | Starter Pack | `com.oztdigital.holdout.starter_pack` | Non-Consumable | 79,99 TL |
| vip | VIP | `com.oztdigital.holdout.vip` | Non-Consumable | 199,99 TL |

## Localizations (Display Name ≤ 30, Description ≤ 55 karakter)

### gems_small
- **TR** — Ad: `Avuç Dolusu Elmas` · Açıklama: `70 elmas. Karakter, kostüm ve altın için.`
- **EN** — Name: `Handful of Gems` · Description: `70 gems for characters, costumes and gold.`

### gems_medium
- **TR** — Ad: `Elmas Kesesi` · Açıklama: `150 elmas. Karakter, kostüm ve altın için.`
- **EN** — Name: `Pouch of Gems` · Description: `150 gems for characters, costumes and gold.`

### gems_large
- **TR** — Ad: `Elmas Sandığı` · Açıklama: `250 elmas, bir yeni karakter için yeterli.`
- **EN** — Name: `Chest of Gems` · Description: `250 gems, enough for one new character.`

### gems_huge
- **TR** — Ad: `Elmas Hazinesi` · Açıklama: `550 elmas. En avantajlı paket.`
- **EN** — Name: `Gem Hoard` · Description: `550 gems. The best value pack.`

### starter_pack
- **TR** — Ad: `Başlangıç Paketi` · Açıklama: `150 elmas + 2000 altın + Komando kostümü.`
- **EN** — Name: `Starter Pack` · Description: `150 gems + 2000 gold + Commando costume.`

### vip
- **TR** — Ad: `VIP` · Açıklama: `Reklamsız ödüller ve tur sonu +%25 altın.`
- **EN** — Name: `VIP` · Description: `Ad-free rewards and +25% end-of-run gold.`

## Review Notes (hepsi için aynı, EN)
```
Gems are a soft currency spent in the in-game shop (Shop → Gems / Characters).
They buy cosmetic characters and costumes, gold, and a one-time continue after death.
No gameplay power is sold. Non-consumables (Starter Pack, VIP) can be restored in Settings → Restore Purchases.
```

## Notlar
- Family Sharing: kapalı.
- İlk sürümde ürünler build ile birlikte incelemeye gönderilir (build sayfası → In-App Purchases → hepsini seç).
- Google Play Console: aynı ID'ler ve aynı TL fiyatları; görsel istemez.
