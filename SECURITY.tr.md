# Güvenlik Politikası

[English](SECURITY.md) · **Türkçe**

## Güvenlik açığı bildirme

Lütfen güvenlik açıklarını herkese açık bir issue ile değil, gizli olarak bildirin.
Herkese açık bir issue, açığı kamuya duyurmak anlamına gelir ve açık düzeltilmeden
önce saldırganların eline geçer.

Bunun için bu depodaki GitHub [gizli güvenlik açığı bildirimi](https://github.com/Oguzhankaraguzel/elevare/security/advisories/new)
özelliğini kullanın. Bu özelliğe erişiminiz yoksa, proje sahibinin GitHub
profilindeki e-posta adresine konu satırına `SECURITY` yazarak e-posta gönderin.

Bildiriminize, elinizdeki bilgiler ölçüsünde şunları eklemeniz faydalı olur: bir
saldırganın bu açıkla neler yapabileceği, sorunu yeniden oluşturma adımları, iki
uygulamadan hangisinin etkilendiği ve test ettiğiniz sürüm ya da commit. Bir kavram
kanıtı (proof of concept) işimizi kolaylaştırır; çalışan bir saldırı kodu
göndermeniz gerekmez.

Bu, herhangi bir fonla desteklenmeyen kişisel bir projedir. İlk yanıtı saatler
içinde değil, birkaç gün içinde bekleyin; ödül programı da bulunmuyor. Bununla
birlikte doğrulanan açıkların düzeltilmesi her zaman önceliklidir.

Lütfen başkalarına ait Elevare kurulumlarında otomatik tarama araçları
çalıştırmayın.

## Desteklenen sürümler

Yalnızca `master` dalındaki en son sürüm desteklenir. Eski sürümler için geriye
dönük güvenlik düzeltmesi yayınlanmaz.

## Elevare'nin sağladığı korumalar

Bir kurulumu değerlendirenler için, kurulumla birlikte hazır gelen korumalar:

- **Yanıt başlıkları.** İki uygulama da `nosniff`, `Referrer-Policy`, kısıtlayıcı
  bir `Permissions-Policy` başlıklarını ve sayfanın başka sitelerin içine
  gömülmesine (clickjacking) karşı `X-Frame-Options` ile CSP `frame-ancestors`
  başlıklarını gönderir.
- **Giriş.** Identity'nin hesap kilitleme özelliği (5 hatalı deneme sonrası 15
  dakika) ve IP başına hız sınırı (5 dakikada 20 deneme) ile korunur. İlki tek bir
  hesabın şifresini tahmin etmeye, ikincisi aynı şifreyi çok sayıda hesapta denemeye
  (password spraying) karşı koruma sağlar.
- **Hız sınırları.** Sitenin veri yazan ve sorgu yapan uç noktalarında uygulanır:
  form gönderme, arama, ziyaret istatistikleri ve tarayıcı hata bildirimleri.
- **Yetkilendirme.** Yalnızca arayüzde değil, sunucuda MediatR pipeline'ı içinde
  (`IRequirePermission`) denetlenir. Kullanıcı ile bir işlem arasındaki tek engel
  hiçbir zaman bir butonun gizlenmesi değildir.
- **Ham HTML ve script.** Sayfa içeriğine bunları yalnızca `CustomCode.Author`
  yetkisine sahip roller kaydedebilir; diğer bütün içerik temizlenir.
- **Şifreler.** ASP.NET Core Identity ile hash'lenir. Gizli değerler
  yapılandırmadan okunur ve hiçbiri kullanılabilir bir varsayılan değerle gelmez;
  bu değerler boşsa sistem başlamaz.

## Bilinen eksikler

Bunları açıkça belirtiyoruz, çünkü yalnızca güçlü yanları sıralayan bir güvenlik
politikası kimsenin işine yaramaz:

- **`script-src` Content-Security-Policy yok.** Site Kodları, yöneticinin kendi
  sayfalarına analiz ve çerez onayı script'leri ekleyebilmesi için var. Gerçekten
  koruma sağlayacak kadar sıkı bir script politikası bu özelliği bozardı. Doğru
  çözüm, izin verilen kaynakları yöneticinin kendisinin tanımlamasıdır; bu henüz
  geliştirilmedi.
- **Hangfire paneli** `Hangfire.Access` yetkisiyle korunur. Yetki, giriş anında
  alınan kopyaya göre değil, rol yetkilerinin güncel önbelleğine göre denetlenir;
  yani geri alınan bir yetki hemen geçerli olur. Panelin ayrıca kendine ait bir
  şifresi yoktur.
- **Yüklenen dosyaların** uzantısı, bildirilen içerik türüyle uyumlu olmalıdır. Bu
  kontrol, dosyaları sunan uç noktanın kullandığı tabloyla yapılır. Bu uyum
  aranmasaydı `evil.html` adlı bir dosya `image/png` olarak yüklenip CMS'in kendi
  adresinden `text/html` olarak sunulabilirdi. Dosyalar **virüs taramasından
  geçirilmez.** SVG desteğini açmadan önce iyi düşünün: SVG, script içerebilen bir
  XML dosyasıdır ve doğrudan açıldığında tarayıcıda kendi başına çalışır.
- **Çok kiracılı (multi-tenant) bir yapı hedeflenmiyor.** CMS'e erişimi olan herkes,
  rolünün izin verdiği bütün site ayarlarına ulaşabilir; içerik sahipleri arasında
  bir ayrım sınırı yoktur.

## Kurulumunuzun güvenliğini etkileyen notlar

Bu maddeler Elevare'nin değil, sizin kurulumunuzun güvenliğini ilgilendirir:

- Uygulamaları TLS (HTTPS) arkasında çalıştırın. Geliştirme ortamı dışında HSTS
  açıktır ve HTTPS kullanıldığını varsayar.
- Reverse proxy arkasında çalışıyorsanız `RateLimiting:TrustForwardedForHeader`
  değerini `true` yapın; **ancak yalnızca** proxy arkasındaysanız. Bu başlığın
  değerini kendisi yazan bir proxy yoksa `X-Forwarded-For` başlığını saldırgan
  istediği gibi doldurabilir. Bu başlığa güvenmek, hız sınırını açık gibi
  gösterirken fiilen devre dışı bırakır.
- Canlıya çıkmadan önce `Cache:ClearSecret`, `Jwt:SecretKey` ve `Preview:SigningKey`
  değerlerini test için ürettiğiniz değerlerden farklı değerlerle değiştirin ve
  bunları sürüm kontrolüne eklemeyin.
- Veritabanı kullanıcısının süper kullanıcı (superuser) yetkisine ihtiyacı yoktur.
  `docker-compose.yml` içindeki resmî `postgres` imajı, `POSTGRES_USER` ile
  belirtilen kullanıcıyı (varsayılan olarak `elevare`) otomatik olarak süper
  kullanıcı yapar. Bu, yerel geliştirmede pratiktir; ancak gerçek bir kurulumda daha
  kısıtlı yetkilere sahip bir kullanıcı oluşturup
  `ConnectionStrings:DefaultConnection` ayarını o kullanıcıya göre düzenleyin.
