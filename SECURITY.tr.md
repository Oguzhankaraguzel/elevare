# Güvenlik Politikası

[English](SECURITY.md) · **Türkçe**

## Güvenlik açığı bildirme

Lütfen herkese açık bir issue ile değil, gizli olarak bildirin. Açık bir issue,
açığı herkese duyurmak demektir ve saldırganlara düzeltmeden önce ulaşır.

Bu depodaki GitHub [gizli güvenlik açığı bildirimi](https://github.com/Oguzhankaraguzel/elevare/security/advisories/new)
özelliğini kullanın. Bu size açık değilse, proje sahibinin GitHub profilindeki
e-posta adresine konu satırında `SECURITY` yazarak gönderin.

Elinizde olduğu kadarıyla şunları eklemeniz işe yarar: bir saldırganın ne
yapabildiği, hatayı yeniden oluşturma adımları, iki uygulamadan hangisinin
etkilendiği ve denediğiniz sürüm ya da commit. Kavram kanıtı (proof of concept)
yardımcı olur; çalışan bir saldırı kodu gerekmez.

Bu kişisel bir proje, fonlanan bir proje değil. İlk cevabı saatler içinde değil
birkaç gün içinde bekleyin; ödül programı da yok. Doğrulanan açıkların düzeltilmesi
her şeyden önce gelir.

Lütfen başkalarının Elevare kurulumlarına otomatik tarama araçları çalıştırmayın.

## Desteklenen sürümler

Sadece `master` üzerindeki son sürüm desteklenir. Eski sürümlere geriye dönük
güvenlik düzeltmesi yapılmaz.

## Elevare'nin kendi yaptıkları

Bir kurulumu değerlendirenler için, kutudan çıkan korumalar:

- **Yanıt başlıkları.** İki uygulamada da: `nosniff`, `Referrer-Policy`, kısıtlayıcı
  bir `Permissions-Policy` ve sayfanın başka sitelere gömülmesine karşı
  `X-Frame-Options` ile CSP `frame-ancestors`.
- **Giriş.** Identity hesap kilitleme (5 hatalı deneme, 15 dakika) ve IP başına hız
  sınırı (5 dakikada 20 deneme) ile korunur. İlki tek bir hesabın şifresini tahmin
  etmeye, ikincisi tek bir şifreyi birçok hesapta denemeye karşıdır.
- **Hız sınırları.** Sitenin yazma ve sorgu uç noktalarında: form gönderme, arama,
  ziyaret istatistiği, tarayıcı hata bildirimleri.
- **Yetkilendirme.** Sadece arayüzde değil, sunucuda MediatR pipeline'ında
  (`IRequirePermission`) denetlenir. Bir butonu gizlemek, kullanıcı ile işlem
  arasındaki tek engel asla değildir.
- **Ham HTML ve script.** Sayfa içeriğinde bunları sadece `CustomCode.Author`
  yetkisi olan roller kaydedebilir; geri kalan her şey temizlenir.
- **Şifreler.** ASP.NET Core Identity ile hash'lenir. Gizli değerler ayarlardan
  okunur ve hiçbiri çalışan bir varsayılanla gelmez; boşlarsa sistem başlamaz.

## Bilinen eksikler

Açıkça yazıyoruz, çünkü sadece güçlü yanlarını sayan bir güvenlik politikası işe
yaramaz:

- **`script-src` Content-Security-Policy yok.** Site Kodları, yöneticinin kendi
  sayfalarına analiz ve çerez onay script'leri eklemesi için var. İşe yarayacak
  kadar sıkı bir script politikası bu özelliği bozardı. Doğru çözüm, yöneticinin
  izin verdiği kaynakları kendisinin tanımlaması; bu henüz yapılmadı.
- **Hangfire paneli** `Hangfire.Access` yetkisine bağlı. Yetki, giriş anındaki
  kopyaya değil, rollerin güncel yetki önbelleğine göre denetlenir; yani geri alınan
  bir yetki hemen geçerli olur. Panelin kendine ait ayrı bir şifresi yok.
- **Yüklenen dosyaların** uzantısı, bildirilen içerik türüyle uyuşmalı. Denetim,
  dosyayı sunan uç noktanın kullandığı aynı tabloyla yapılır. Bu uyum aranmasaydı
  `evil.html` adlı bir dosya `image/png` olarak yüklenip CMS'in kendi adresinden
  `text/html` olarak geri sunulabilirdi. Dosyalar **virüs taramasından geçmez.** SVG'yi
  açmadan önce iyi düşünün: script taşıyabilen bir XML'dir ve doğrudan açıldığında
  kendisi olarak çalışır.
- **Çok kiracılı yapı hedef değil.** CMS erişimi olan herkes, rolünün izin verdiği
  her site ayarına ulaşır; içerik sahipleri arasında bir ayırma sınırı yok.

## Sizin güvenliğinizi etkileyen kurulum notları

- Uygulamaları TLS (HTTPS) arkasına koyun. Geliştirme dışında HSTS açık ve bunu
  varsayıyor.
- Reverse proxy arkasındaysanız `RateLimiting:TrustForwardedForHeader` değerini
  `true` yapın. **Ama sadece** proxy arkasındaysanız. Başlığı ezen bir proxy yoksa
  `X-Forwarded-For` değerini saldırgan istediği gibi yazar; ona güvenmek hız
  sınırını açık görünürken fiilen kapatır.
- Canlıya çıkmadan önce `Cache:ClearSecret`, `Jwt:SecretKey` ve
  `Preview:SigningKey` değerlerini test için ürettiklerinizden farklı değerlerle
  değiştirin ve bunları sürüm kontrolünün dışında tutun.
- Veritabanı kullanıcısının süper kullanıcı yetkisine ihtiyacı yok.
  `docker-compose.yml` içindeki resmî `postgres` imajı, `POSTGRES_USER`'da adı geçen
  kullanıcıyı (varsayılan `elevare`) otomatik olarak süper kullanıcı yapar. Yerel
  geliştirmede pratik, ama gerçek bir kurulumda daha dar yetkili bir kullanıcı
  oluşturup `ConnectionStrings:DefaultConnection`'ı ona bağlayın.
