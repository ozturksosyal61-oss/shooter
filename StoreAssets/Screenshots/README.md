# Holdout — App Store ekran görüntüleri

- `tr/` ve `en/`: 8 görsel, her biri iki boyutta: **2556 × 1179** ve **2622 × 1206** (iPhone 6.1" / 6.3", yatay) + **2752 × 2064** (iPad 13", yatay; üstte başlık, altta oyun karesi). Yükleme sırası dosya numarası (01 = ilk görünen).
- Oyun arayüzü dilin kendisiyle çekildi (TR görsellerde Türkçe HUD, EN görsellerde İngilizce).
- Tanıtım videosu için arayüzsüz kareler: `../VideoStills/` (2556 × 1179 ve 1920 × 886; menü / kart ekranları `ui/` altında).
- Yeniden üretmek: `CaptureStore` testi (`STORE_LANG=tr|en`, `CAPTURE_DIR`, grafikli Unity) → `python Tools/make_store_art.py . <tr_klasörü> <en_klasörü>`.
