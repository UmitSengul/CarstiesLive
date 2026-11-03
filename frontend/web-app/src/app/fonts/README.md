# Gömülü font: Inter (değişken)

`InterVariable.woff2` — Inter değişken fontu (tüm ağırlıklar 100–900, tek dosya).

- Kaynak: <https://rsms.me/inter/>
- Lisans: SIL Open Font License 1.1 (<https://github.com/rsms/inter/blob/master/LICENSE.txt>)
- Neden repoda: build sırasında `fonts.googleapis.com`'a erişim gerektirmemek
  (kapalı/ağsız ortamlarda `next/font/google` build'i kırıyor) + daha hızlı ilk boyama.
- Türkçe karakterler (ğ, ş, ı, İ, ç, ö, ü) Latin-Ext kapsamında mevcuttur.
- Güncelleme: aynı adla dosyayı yenileyip commit et; `layout.tsx` içindeki
  `localFont({ src: "./fonts/InterVariable.woff2" })` kaydı otomatik kullanır.
