# Playtest 02 — oyuncu gözüyle (2026-11-19, yayın öncesi)

Kaynak: `PlaytestBot` human stratejisi, grafikli, timescale 3, ekran görüntüleri her 45 sn. Tur: 289 sn, dalga 6, 214 kill, %60 isabet, Kasap + helikopter iniş noktasında öldü. Kartlar: Delici ×2, Ateş hızı ×3, Maks can, Patlayan leş.

## İyi
- Vuruş hissi (kan, kritik sayısı, "MÜKEMMEL!" aktif reload, kombo çarpanı).
- Değişen şehir, renkli mahalleler, variller.
- Kasap girişi (isim + kamera) "büyük an" hissi.
- Hedefler (fişek → kule → helikopter) yön veriyor.
- Ölüm / canlanma ekranı net ve adil.

## Düzeltilecekler (öncelik sırasıyla)
1. **Sağ üst HUD çakışması (hata):** telsiz kutusu + görev satırı kombo / skorun üstüne biniyor; Kasap giriş yazısı telsiz kutusunun altında kalıyor.
2. **Dalga 6 yığılması:** boss + helikopter iniş noktası + kalabalık aynı anda → boss dalgasında helikopter görevi verme (ya da boss ölünce ver).
3. **Zorluk eğrisi:** dalga 1–2 çok sakin (3–10 zombi), dalga 3'te 23'e sıçrıyor → başı biraz kalabalık, ortası yumuşak.
4. **Bina altı görüş:** askerin üstündeki büyük çatı ekranın yarısını kapatıyor; kesik daire küçük kalıyor → daha geniş kesik / çatıyı tamamen saydamlaştır.
5. **Doldurma sıklığı:** 289 sn'de 60 reload, ekranda sık 0/40 → başlangıç şarjörü / reload hızı ya da otomatik taktik reload eşiği.
6. **Kart çeşitliliği:** teklifler hep hasar / ateş hızı ağırlıklı → yönlü build'ler (taret, bomba, kritik) ve daha belirgin seçenekler.

## Oyuncu istekleri (yeni özellik)
- İlk tur 3 adımlık rehber (sürükle-nişan, DOLDUR zamanlaması, varile ateş).
- Dalga sonu küçük ödül anı (altın patlaması); boss'tan kostüm / silah parçası.
- Gizli kart sinerjileri (ör. pompalı + delici = alev mermisi).
- Game Center liderlik tablosu (sonsuz / zor dalga rekoru) + başarımlar.
- Haftalık etkinlik: herkese aynı seed'li şehir + kural (ör. yalnız pompalı).
- Kan lekeleri zamanla solsun (kalabalıkta birikiyor).

## Yayından sonraya kalan teknik işler
- Co-op'ta kopan oyuncunun geri bağlanması, Game Center / Play Games eşleştirmesi.
- Ateş geri tepme animasyonu, daha hızlı zombi yürüme klibi.
