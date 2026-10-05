# Mimari

[English](ARCHITECTURE.md) · **Türkçe**

Bu belge Elevare'nin nasıl tasarlandığını ve tasarım kararlarının arkasındaki
gerekçeleri anlatır. Yapısal bir değişiklik yapmadan önce okunması gereken belge
budur. Bir değişikliğin projeye nasıl dahil edileceği ise
[CONTRIBUTING.tr.md](../CONTRIBUTING.tr.md) dosyasında anlatılıyor.

## İki uygulama, tek veritabanı

```
┌──────────────┐    yazar    ┌─────────────┐    okur     ┌──────────────┐
│  CMS (Blazor)│ ──────────► │ PostgreSQL  │ ◄────────── │ Site (MVC)   │
│   yönetim    │             │  ElevareDB  │             │ ziyaretçiler │
└──────────────┘             └─────────────┘             └──────────────┘
```

İki uygulama ayrı süreçler olarak çalışır ve birbirleriyle bir API üzerinden değil,
ortak veritabanı üzerinden haberleşir. Bu yapının bozulmamasını sağlayan kural
şudur: **tabloları CMS oluşturur ve veriyi CMS yazar; site yalnızca okur.** Site
migration çalıştırmaz ve içeriğe yazmaz. Sitenin yazdığı veriler yalnızca yeni
kayıt olarak eklenen türdendir: sayfa görüntüleme ve tıklama istatistikleri, form
yanıtları ve tarayıcı hata logları.

Site, CMS'in tablolarını `Domain/Entities/Public*` altındaki kendi salt okunur
sınıflarıyla modeller. Bu sınıflar bilerek CMS'tekilerden daha dar tutulmuştur ve
yalnızca sitenin ihtiyaç duyduğu alanları içerir. Her iki modülde de birer
`PublicSchemaContractTests` test paketi bulunur ve bu sınıfların CMS'in tablolarıyla
hâlâ uyuştuğunu doğrular. Böylece CMS'te bir sütunun adı değiştiğinde site çalışma
anında fark edilmeden bozulmaz; hata daha derleme aşamasında yakalanır.

## Katmanlar

Her iki uygulama da aynı dört katmandan oluşur. Bağımlılıklar yalnızca içe
doğrudur:

```
Presentation  ──►  Infrastructure  ──►  Application  ──►  Domain
                                              └──────────►  SharedKernel
```

**`SharedKernel`:** İki modülün de üzerine kurulduğu `Result` / `Error` türleri,
domain soyutlamaları ve küçük yardımcı metotlar. Hiçbir framework'e bağımlı
değildir.

**`Domain`:** Varlıklar (entity) ve bu varlıklara ait kurallar. Dışarıya hiçbir
bağımlılığı yoktur. `BaseEntity`, denetim bilgilerini taşır (oluşturma, güncelleme
ve silme zamanları ile bu işlemleri yapan kullanıcılar, `IsDeleted`, `IsActive`);
bu alanlar veri katmanı tarafından otomatik doldurulur. Hata *mesajları* da burada,
ait oldukları varlığın yanında `Error` sabitleri olarak tanımlanır. Böylece bir
handler hiçbir zaman kendi hata mesajını uydurmaz.

**`Application`:** Her kullanım senaryosu için bir sınıf bulunur ve CQRS yaklaşımı
izlenir: `ICommand` / `IQuery` ve bunların handler'ları MediatR ile çağrılır.
İstekler aşağıdaki pipeline adımlarından bu sırayla geçer ve sıra önemlidir:

1. `DbConcurrencyGuardPipelineBehavior`: Scoped `DbContext`'e erişimi sıraya
   koyar. Blazor Server'da tek bir sayfa aynı anda birden fazla istek
   gönderebilir, ancak EF'in context'i thread-safe değildir. Her DI scope'u için
   tutulan bir semaphore bu çakışmayı önler.
2. `RequestLoggingPipelineBehavior`: Her isteği ve sonucunu kaydeder.
3. `PermissionPipelineBehavior`: Handler çalışmadan önce `IRequirePermission` ile
   tanımlanan yetkiyi denetler.
4. `ValidationPipelineBehavior`: FluentValidation kurallarını çalıştırır.
5. `PublicSiteCacheInvalidationPipelineBehavior`: Bir komut başarıyla
   tamamlandıktan sonra (yani 6. adım değişiklikleri kaydettikten sonra) siteden
   önbellekteki sayfaları temizlemesini ister. Bkz.
   [Sitenin önbelleği](#sitenin-önbelleği).
6. `SaveChangesPipelineBehavior`: Handler başarılı döndükten sonra
   değişiklikleri tek seferde kaydeder.

Kayıt işlemi bu son adımda yapıldığı için çoğu komut handler'ı yalnızca
değişiklikleri hazırlayıp döner, `SaveChangesAsync`'i kendisi çağırmaz. İstisna,
yanıtında veritabanının ürettiği kimliğe (id) ihtiyaç duyan handler'lardır. Bunlar
kaydı kendileri yapar; bu durumda pipeline adımı kaydedecek bir şey bulamaz ve
hiçbir etkisi olmaz.

**`Infrastructure`:** Dış dünyayla iletişim kuran her şey burada bulunur: JWT
üretimi ve oturumdaki JWT'yi istek başlığına taşıyan middleware, yeniden deneme
destekli SMTP, dosya depolama (yerel ya da S3 uyumlu), Hangfire görevleri ve
`DbContext`, Identity ile migration'ları içeren `Persistence` projesi.

Redis (sitenin sayfa ve sorgu önbelleği) kendi `IRedisConnection` sarmalayıcısının
arkasında çalışır (`Infrastructure/Caching/RedisConnection.cs`). Bu sarmalayıcı
bağlantıyı ilk ihtiyaç duyulduğunda kurar ve hiçbir zaman exception fırlatmaz.
Redis'e ulaşılamadığında site çökmez, önbellek yokmuş gibi çalışmaya devam eder.
Bunun gerekli olmasının sebebi şudur: ulaşılamayan bir sunucuya doğrudan
`ConnectionMultiplexer.Connect` ile bağlanmaya çalışmak ya exception fırlatır (bu
durumda her istek 500 döner) ya da yalnızca `AbortOnConnectFail=false` kullanılırsa
her komut kendi zaman aşımı dolana kadar bekler (bu durumda her istek bu süreyi
baştan sona bekler). Sarmalayıcının `TryGetConnected` metodu, bağlantı gerçekten
açık değilse multiplexer yerine `null` döner ve çağıran taraf bunu sıradan bir
önbellek ıskası olarak ele alır.

**`Presentation`:** Blazor yönetim paneli ve MVC site.

## Hatalar exception olarak taşınmaz

Handler'lar `Result` ya da `Result<T>` döner. Başarısız bir sonuç, değişmeyen bir
koda sahip bir `Error` taşır; örneğin `PageInfo.TranslationAlreadyExists`. Arayüz bu
kodu `CmsLocalizer.Error` ile kullanıcının okuyacağı bir cümleye çevirir; cümle
`ErrorMessages.resx` ve `ErrorMessages.tr.resx` dosyalarında aranır.

Bu yüzden hata kodu süs değil, çeviri anahtarıdır. Kaynak dosyada karşılığı olmayan
bir kod için İngilizce `Error.Description` gösterilir ve Türkçe kullanan biri
İngilizce bir mesajla karşılaşır. `ErrorMessageCoverageTests`, tanımlanmış bir
kodun Türkçe mesajı yoksa derlemeyi başarısız kılar.

Exception'lar yine de kullanılır, ama yalnızca gerçekten beklenmedik durumlar için.
Bunları genel hata yakalayıcı yakalar ve 500 hatasına çevirir. Site bunlara ek
olarak `NotFoundException` kullanır; sitenin middleware'i bunu ziyaretçinin dilinde
gerçek bir 404 sayfası olarak gösterir.

Aynı kural kullanıcının okuduğu her metin için geçerlidir. Presentation katmanının
altındaki hiçbir katman kullanıcıya gösterilecek hazır bir cümle üretmez.
Validator'lar hata kodu taşır; yapısal veri doğrulayıcısı da cümle yerine bir
anahtar ve parametrelerini döner. Önceden bu doğrulayıcı doğrudan Türkçe metin
döndürüyordu; bu da editörün hangi dilde okuyacağına domain katmanının karar vermesi
demekti ve İngilizce kullanan bir editör Türkçe SEO önerileri görüyordu.

## İstek pipeline'ları

Her iki uygulamada da middleware sırası kritiktir. CMS'teki güncel sıra
(`src/cms/Presentation/Wasm/Wasm/Program.cs`):

1. `UseWebAssemblyDebugging` (geliştirme ortamında) / `UseExceptionHandler` +
   `UseHsts` (canlı ortamda)
2. `UseForwardedHeaders` (`X-Forwarded-For` / `-Proto`): nginx ya da başka bir
   reverse proxy arkasında bu adım olmazsa her istek 127.0.0.1'den gelen düz bir
   HTTP isteği gibi görünür; HTTPS algılama ve güvenli çerezler bozulur.
   `Request.Scheme`'i okuyan her adımdan önce çalışmalıdır.
3. `SecurityHeadersMiddleware`: Hata yanıtları dahil her yanıtın güvenlik
   başlıklarını taşıması için erken çalışır.
4. `UseHttpsRedirection`, `UseStaticFiles`
5. `Accept-Language` başlığından dil algılama, ardından `UseRequestLocalization`
6. `UseSession`, ardından `JwtFromSessionMiddleware`: JWT'yi oturumdan alıp
   `Authorization` başlığına yerleştirir.
7. `UseRateLimiter`, `UseAuthentication`, `UseAuthorization`, `UseAntiforgery`
8. `MapStaticAssets` (girişsiz erişilebilir; aksi hâlde varsayılan yetki politikası
   Blazor'un kendi framework dosyalarını engellerdi), `MapRazorComponents`
9. Veritabanını oluşturma ve başlangıç verilerini ekleme. Aşağıdaki Hangfire
   kayıtlarından **önce** yapılmalıdır, çünkü Hangfire yazacağı veritabanını
   kendisi oluşturamaz.
10. Hangfire paneli ve tekrarlanan görevler, giriş/medya/dil uç noktaları,
    `/health`

Site (`src/web/Presentation/WebMvc/Program.cs`):

1. `UseForwardedHeaders`: CMS'teki gerekçeyle aynı. En başta yer alır, çünkü
   aşağıdaki hata yakalama adımı bile isteğin gerçek şemasını bilmelidir.
2. `GlobalExceptionHandlerMiddleware`: Kendisinden sonra gelen her adımı
   kapsaması için.
3. `SecurityHeadersMiddleware`
4. `UseHsts`, `UseRewriter(SeoRedirectRule)`, `UseHttpsRedirection` (yalnızca
   canlı ortamda). Yeniden yazma kuralı alan adını (`www.`) ve yoldaki büyük/küçük
   harf kullanımını tek bir biçime getirir ve HTTPS'i adres düzeyinde zorunlu
   kılar. Dosya uzantısı içeren yolları atlar; yani statik dosyalar hiçbir zaman
   yeniden yazılmaz ya da yönlendirilmez, bu kural yalnızca gerçek sayfa
   adreslerine uygulanır.
5. `UseResponseCompression` (Brotli + gzip), `UseWebOptimizer` ve uzun süreli
   önbellek başlıklarıyla `UseStaticFiles` (`public,max-age=31536000,immutable`)
6. `MaintenanceModeMiddleware`: Statik dosyalardan sonra çalışır, böylece bakım
   sayfasının kendi CSS'i ve görselleri de yüklenebilir. `/health` bu kuraldan
   muaftır.
7. `UseRouting`, `UseAuthorization`
8. `UseOutputCache`: Routing'den sonra gelmelidir, çünkü `[OutputCache]` bir
   endpoint özelliğidir. `UseRouting`'den önce konursa endpoint'i göremez, politika
   bulamaz ve hiçbir şeyi önbelleğe almaz (bu adım yeri değiştirilene kadar tam
   olarak böyle çalışıyordu).
9. `UseWebMarkupMin` (HTML küçültme): Output cache'ten sonra gelir; böylece
   önbellekte küçültülmüş kopya tutulur ve önbellekten gelen istekler küçültme
   işleminden tekrar geçmez.
10. `UseRateLimiter`: Output cache'ten sonra gelir; önbellekten sunulan bir sayfa
    hız sınırı kotasını tüketmez.
11. API uç noktaları (istatistik, log, form, arama, önbellek), ardından
    `/{language}/{slug}` için `MapDynamicControllerRoute` ve en sonda isteği sayfa
    controller'ına yönlendiren yedek route.

## Çok dilli adresler

Varsayılan dil önek almaz (`/hakkimizda`); yayındaki diğer bütün diller kendi
öneklerini alır (`/en/about-us`). Varsayılan dilin önekli adresi
(`/tr/hakkimizda`), öneksiz adrese 301 ile yönlendirilir.
`SlugRouteValueTransformer` bir adresi ilgili sayfaya çözümler.
`ExistingLanguageRouteConstraint` ise adresteki ilk parçanın bir dil kodu olup
olmadığına karar verir. İkisi de `ILanguageDirectory`'yi kullanır; bu, bellekte
tutulan ve `SiteStateRefreshHostedService` tarafından 30 saniyede bir yenilenen
bir anlık görüntüdür.

Bir dili yayına almanın ya da bakım modunu açıp kapatmanın sitede yeniden başlatma
gerektirmemesi bu yenileme sayesindedir. Aşağıda anlatılan önbellek temizleme
çağrısı da iki anlık görüntüyü hemen yeniler. Bu yüzden CMS'te yapılan bir
değişiklik, bir sonraki 30 saniyelik yenilemeyi beklemeden, pratikte bir iki saniye
içinde siteye yansır.

Varsayılan dili değiştirmek bütün sayfaların adresini değiştirir. CMS bu durumda
eski adreslerden yenilerine yönlendirme kuralları oluşturur; sayfa içeriklerindeki,
şablonlardaki, SEO alanlarındaki (canonical, `og:url`, yapısal veri) ve site
ayarlarındaki eski adresleri yenileriyle değiştirir (`SiteAddressMigration`) ve
site haritasını hemen yeniden oluşturur.

## Sitenin önbelleği

Site iki seviyede önbellek kullanır:

| Seviye | İçerik | Süre | Konum |
| --- | --- | --- | --- |
| Hazır sayfalar | `PageController.Index`'in döndürdüğü küçültülmüş HTML yanıtının tamamı. Anahtar, adres ile sayfa listesi parametrelerinden (`page`, `tag`, `q` ve bunların bloklara özel hâlleri; `ListingQuery`) oluşur. Başka bir parametre (`utm_*`, `fbclid`) sayfayı değiştirmez ve önbellekte yeni bir kayıt oluşturmaz. | 1 saat | ASP.NET Core output cache (site sürecinin belleğinde), `Pages` politikası |
| Sorgu sonuçları | Adrese göre sayfa, site ayarları, site kodları | 30 saniye | `ICacheService`: ayarlanmışsa Redis, aksi hâlde süreç belleği |

Bir saatlik süre, ancak sayfaların kendiliğinden eskimesine gerek olmadığı için
güvenlidir. Her başarılı CMS komutundan sonra
`PublicSiteCacheInvalidationPipelineBehavior`, `IPublicSiteCacheInvalidator`
üzerinden sitenin önbelleğinin temizlenmesini ister. Birbirinden bir saniyeden kısa
aralıkla gelen istekler (toplu bir düzenleme ya da birkaç komuttan oluşan bir kayıt
işlemi) tek bir `POST /api/cache/clear` çağrısı olarak gönderilir. Bu uç nokta
sırasıyla `ICacheService`'i boşaltır, dil ve bakım modu anlık görüntülerini yeniler
ve ancak ondan sonra `pages` etiketli output cache kayıtlarını siler. Bu sıralama,
arada gelen bir isteğin eski veriyle oluşturulmuş bir sayfayı yeniden önbelleğe
koymasını engeller.

Önbellek, yalnızca içeriği değiştiren belirli komutlardan sonra değil, bilerek her
komuttan sonra temizlenir. Bir sayfa; sayfalar, şablonlar, medya, site ayarları,
site kodları, diller, yönlendirmeler ve daha fazlasından beslenir. Böyle bir liste,
ileride eklenecek ilk özelliği gözden kaçırırdı. Siteyi etkilemeyen bir komutun
bedeli yalnızca bir sayfanın fazladan bir kez oluşturulmasıdır. Temizleme çağrısı
siteye ulaşamazsa CMS bir uyarı kaydı yazar; bu durumda sitede en fazla bir saat
eski içerik görünebilir. CMS'i devre dışı bırakan değişiklikler için (veritabanını
geri yükleme, elle yapılan bir SQL düzeltmesi) `/admin/cache` ekranındaki
"Önbelleği Temizle" butonu kullanılır.

Bir dilin adresleri ancak o dil hem aktif **hem de** yayındaysa çalışır. Bu iki
anahtarın ne anlama geldiği [README](../README.tr.md) dosyasının Diller bölümünde
anlatılıyor.

## Arka plan görevleri

Hangfire, verilerini PostgreSQL'de saklar ve CMS sürecinin içinde çalışır:

| Görev | Zamanlama | Ne yapar |
| --- | --- | --- |
| `sitemap-generation` | 2 saatte bir | Site haritası XML'ini yeniden oluşturup `SitemapCaches` tablosuna yazar |
| `reminder-check` | dakikada bir | Zamanı gelen hatırlatıcıları gönderir |
| `database-backup` | Pazar 02:00 UTC | Tam yedek alır, ardından eski yedekleri siler |
| `data-retention` | her gün 03:20 UTC | İstatistik, tıklama ve log tablolarındaki eski kayıtları temizler; süresi dolan Çöp Kutusu kayıtlarını kalıcı olarak siler |

Temizlik görevi bilerek haftalık yedekten *sonra* çalışır; böylece temizliğin
silmek üzere olduğu kayıtlar da yedekte yer alır.

## Entegrasyon sırları ve canlı ayar değişikliği

SMTP, CAPTCHA ve S3/CDN giriş bilgileri iki yerde tutulabilir: `appsettings.json`
ya da ortam değişkenlerinde (diğer ayarlar gibi açılışta bir kez okunur) **veya**
CMS'in "Sırlar" ekranında. İkinci yolda değerler `IntegrationSecrets` tablosunda,
iki uygulamanın çerezler için zaten ortak kullandığı Data Protection anahtarlarıyla
şifrelenmiş olarak saklanır. İki yöntem de çalışır; ikisinde de değer varsa
veritabanındaki geçerli olur.

Veritabanı tarafı sıradan bir `IOptions<T>` ile okunmaz. "Sırlar" ekranından
yapılan bir kayıt, iki süreçte de **hemen ve yeniden başlatma gerektirmeden**
geçerli olur:

- `IntegrationSecretsConfigurationProvider` (`Presentation/*/Configuration/`
  altında, her uygulamada bir tane), özel olarak yazılmış ve *yeniden
  yüklenebilen* bir `IConfigurationProvider`'dır. Tek seferlik
  `AddInMemoryCollection`'ın aksine `ReloadAsync()` metodu tabloyu yeniden okur ve
  yapılandırma sisteminin kendi yeniden yükleme sinyalini tetikler;
  `IOptionsMonitor<T>` da bu sinyali dinler. Bu yüzden
  `EmailOptions`, `CaptchaOptions` ya da `ObjectStorageOptions` kullanan her sınıf
  bunları `IOptions<T>` olarak değil, `IOptionsMonitor<T>` olarak alır.
  `IOptions<T>`, değeri DI nesneyi oluştururken bir kez okur ve sonraki
  değişiklikleri hiçbir zaman görmez.
- `Update`/`ClearIntegrationSecretCommandHandler`, kaydın hemen ardından CMS'in
  kendi kopyasını güncellemek için `IIntegrationSecretsReloader`'ı çağırır.
  Yalnızca `Captcha:*` anahtarları için (sitenin de okuduğu tek değer bu)
  `IIntegrationSecretsChangeNotifier` ayrıca sitedeki `POST /api/secrets/reload`
  uç noktasını çağırır. Bu çağrı, önbellek temizleme özelliğinin süreçler arası
  iletişim için zaten kullandığı ve `Cache:ClearSecret` ile doğrulanan iç kanal
  üzerinden yapılır (`CacheClearService` / `CacheEndpoints` ile aynı yapı).
- `ObjectStorage:Provider` değerini yerel disk ile S3 arasında değiştirmek, sıradan
  bir ayar değişikliği değildir. Normalde bu karar DI kaydı sırasında bir kez
  verilir ve hangi `IBlobStorage` sınıfının kaydedileceği o anda seçilir. Bu kararı
  da canlı değiştirilebilir yapmak için `LocalDiskBlobStorage` ve `S3BlobStorage`
  her zaman kayıtlı tutulur; `IBlobStorageFactory.Create(IServiceProvider)` her
  çağrıda güncel `Provider` değerine bakarak bunlardan birini seçer.
  `IServiceProvider` bilerek metot parametresi olarak alınır, singleton olan
  factory'nin constructor'ında saklanmaz. Kök provider'ı tutan bir singleton, onu
  kullanarak Scoped bir servis çözerse (burada, isteğin kendi `DbContext`'ine
  bağımlı olan `LocalDiskBlobStorage`), Development ortamında açıkça hata verir;
  ancak **Production ortamında uygulama boyunca tek bir nesneyi fark ettirmeden
  sabitler** (`ValidateScopes` varsayılan olarak tam da bu ayrıma göre açık ya da
  kapalıdır). Bir singleton'ın Scoped bir servise ihtiyaç duyduğu her durumda bunu
  akılda tutmakta fayda var.

## Yumuşak silme

Silinebilen her şey yumuşak silinir (soft delete). `SaveChanges`, bir `Remove`
işlemini `IsDeleted = true` atamasına çevirir; genel bir sorgu filtresi de bu
kayıtları bütün normal sorgulardan gizler. Unique index'ler `"IsDeleted" = false`
filtresiyle oluşturulur; böylece silinmiş bir kayıt, yeni bir kaydın aynı adresi
kullanmasını engellemez.

Kalıcı silme yalnızca tek bir yerde yapılır: `TrashPurge`. Buna Çöp Kutusu
ekranından, "Çöp kutusunu boşalt" işleminden ve temizlik görevinden ulaşılır. Form
yanıtı olan ya da alt sayfası bulunan bir sayfayı silmeyi reddeder. Ayrıntılar için
README'deki Çöp Kutusu bölümüne bakın.

`ICmsApplicationDbContext.RemovePermanently`, yumuşak silmeyi atlamanın tek yoludur.
Bunun bir bayrak değil de ayrı bir metot olarak tanımlanmasının sebebi, kalıcı
silmenin yanlışlıkla yazılmasını önlemek ve kodda kolayca aranıp bulunabilmesini
sağlamaktır.

## Sayfa içeriği

Sayfalar GrapesJS ile hazırlanır. `PageContent` üç sütun tutar: oluşturulan HTML,
CSS ve editörün kendi proje JSON'u. Site kayıtlı HTML'i gösterir. Editör ise
bileşen ağacını JSON varsa ondan, yoksa HTML'den yeniden oluşturur.

Bu kısımda bir değişiklik yapmadan önce bilinmesi gereken iki şey var:

- GrapesJS her kayıtta öğelerin id'lerini ve sınıflarını yeniden yazar; bu yüzden
  **ham HTML'i karşılaştırmanın bir anlamı yoktur**. Onay akışındaki fark ekranı
  bunun yerine okurun gördüğü metni karşılaştırır (`HtmlTextDiff`, AngleSharp ile).
- Bir sayfanın onay bekleyen düzenlemesi, yayındaki `GjsHtml`'den ayrı olarak
  `PreviewGjsHtml` alanında tutulur. Onay akışı bir değişikliği bekletirken
  yayındaki sayfanın sunulmaya devam edebilmesi bu ayrım sayesindedir.

Aynı sayfa iki farklı yerde açıksa, sonradan kaydeden kişinin diğerinin
değişikliklerini fark etmeden ezmemesi için editör, sayfa açıldığı andaki parmak
izini (`PageFingerprint`) kayıtla birlikte gönderir. Sayfa bu arada başka bir yerde
değiştirilmişse kayıt `EditedElsewhere` hatasıyla reddedilir; kullanıcı güncel
hâli yüklemeyi ya da bilerek üzerine yazmayı seçer. Şablonlar için de aynı kural
geçerlidir.
