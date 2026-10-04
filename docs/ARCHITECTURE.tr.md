# Mimari

[English](ARCHITECTURE.md) · **Türkçe**

Elevare'nin nasıl kurulduğu ve neden böyle kurulduğu. Yapısal bir şeyi
değiştirmeden önce okunacak belge bu. Bir değişikliğin nasıl birleştirileceği
[CONTRIBUTING.tr.md](../CONTRIBUTING.tr.md) dosyasında.

## İki uygulama, tek veritabanı

```
┌──────────────┐    yazar    ┌─────────────┐    okur     ┌──────────────┐
│  CMS (Blazor)│ ──────────► │ PostgreSQL  │ ◄────────── │ Site (MVC)   │
│   yönetim    │             │  ElevareDB  │             │ ziyaretçiler │
└──────────────┘             └─────────────┘             └──────────────┘
```

İkisi ayrı süreçlerdir; birbiriyle API üzerinden değil, ortak veritabanı üzerinden
konuşur. Bunu düzgün tutan kural: **tabloları CMS kurar ve yazar, site sadece
okur.** Site migration çalıştırmaz, içeriğe yazmaz. Sitenin yazdığı tek şeyler
sadece eklenen kayıtlardır: ziyaret ve tıklama istatistikleri, form yanıtları ve
tarayıcı hata logları.

Site, CMS'in tablolarını `Domain/Entities/Public*` altındaki kendi salt okunur
sınıflarıyla modeller. Bunlar bilerek CMS'tekilerden daha dardır, sadece sitenin
ihtiyacı olan alanları içerir. Her iki modülde de birer `PublicSchemaContractTests`
paketi var; bu sınıfların CMS'in tablolarıyla hâlâ uyuştuğunu denetler. Böylece
CMS'te bir sütunun adı değişirse site çalışırken sessizce bozulmaz, derleme hata
verir.

## Katmanlar

Her uygulama aynı dört katmandan oluşur. Bağımlılıklar sadece içeri doğrudur:

```
Presentation  ──►  Infrastructure  ──►  Application  ──►  Domain
                                              └──────────►  SharedKernel
```

**`SharedKernel`**: İki modülün de üzerine kurulduğu `Result` / `Error` türleri,
domain soyutlamaları ve küçük yardımcı metotlar. Hiçbir framework'e bağlı değil.

**`Domain`**: Varlıklar (entity) ve onlara ait kurallar. Dışarıya hiçbir
bağımlılığı yok. `BaseEntity` denetim bilgisini taşır (oluşturma/güncelleme/silme
zamanları ve kullanıcıları, `IsDeleted`, `IsActive`); bunları veri katmanı
kendiliğinden doldurur. Hata *mesajları* da burada durur: ait oldukları varlığın
yanında `Error` sabitleri olarak. Bir handler hiçbir zaman yerinde mesaj uydurmaz.

**`Application`**: Her kullanım senaryosu için bir sınıf, CQRS tarzında:
`ICommand` / `IQuery` ve bunların handler'ları, MediatR ile çağrılır. İstekler
sırayla şu pipeline adımlarından geçer. Sıra önemli:

1. `DbConcurrencyGuardPipelineBehavior`: Scoped `DbContext`'e erişimi sıraya
   koyar. Blazor Server'da tek bir sayfa aynı anda birkaç istek gönderebilir ama
   EF'in context'i aynı anda birden fazla iş parçacığından kullanılamaz. DI scope
   başına bir semaphore bunu önler.
2. `RequestLoggingPipelineBehavior`: Her isteği ve sonucunu kaydeder.
3. `PermissionPipelineBehavior`: Handler çalışmadan önce `IRequirePermission`
   yetkisini denetler.
4. `ValidationPipelineBehavior`: FluentValidation kurallarını çalıştırır.
5. `PublicSiteCacheInvalidationPipelineBehavior`: Bir komut başarılı olduktan
   sonra (yani 6. adım onu kaydettikten sonra) siteye hazırladığı sayfaları atmasını
   söyler. Bkz. [Sitenin önbelleği](#sitenin-önbelleği).
6. `SaveChangesPipelineBehavior`: Handler başarılı döndükten sonra tek seferde
   kaydeder.

Bu son adım olduğu için çoğu komut handler'ı değişiklikleri hazırlayıp döner,
`SaveChangesAsync`'i kendisi çağırmaz. İstisna: cevabında veritabanının ürettiği
kimliğe (id) ihtiyaç duyan handler'lar. Bunlar kendileri kaydeder; pipeline adımı
o zaman zararsız şekilde boşa çalışır.

**`Infrastructure`**: Dış dünyayla konuşan her şey: JWT üretimi ve oturumdaki
JWT'yi başlığa taşıyan middleware, tekrar denemeli SMTP, dosya depolama (yerel ya
da S3 uyumlu), Hangfire işleri ve `DbContext`, Identity ve migration'larla birlikte
`Persistence`.

Redis (sitenin sayfa/sorgu önbelleği) kendi `IRedisConnection` sarmalayıcısının
arkasındadır (`Infrastructure/Caching/RedisConnection.cs`). Bu sarmalayıcı bağlantıyı
ilk ihtiyaçta kurar ve asla exception fırlatmaz. Redis'e ulaşılamıyorsa site
çökmez, "önbellek yok" gibi çalışmaya devam eder. Neden gerekli: ulaşılamayan bir
sunucuya düz `ConnectionMultiplexer.Connect` ya exception fırlatır (her istek 500
döner) ya da yalnızca `AbortOnConnectFail=false` ile her komutta kendi zaman
aşımına kadar bekler (her istek o süreyi baştan sona öder). Sarmalayıcının
`TryGetConnected` metodu, bağlantı gerçekten canlı değilse multiplexer yerine
`null` döner; çağıran taraf bunu normal bir önbellek ıskası gibi ele alır.

**`Presentation`**: Blazor yönetim paneli ve MVC site.

## Hatalar exception olarak taşınmaz

Handler'lar `Result` / `Result<T>` döner. Başarısızlık, sabit bir kodu olan bir
`Error` taşır; örneğin `PageInfo.TranslationAlreadyExists`. Arayüz bu kodu
`CmsLocalizer.Error` ile bir cümleye çevirir; cümleyi `ErrorMessages.resx` /
`ErrorMessages.tr.resx` içinde arar.

Bu yüzden hata kodu süs değildir, çeviri anahtarıdır. Kaynak dosyada karşılığı
olmayan bir kod İngilizce `Error.Description`'a düşer; Türk kullanıcı da İngilizce
mesaj okur. `ErrorMessageCoverageTests`, tanımlı bir kodun Türkçe mesajı yoksa
derlemeyi düşürür.

Exception'lar yine vardır ama gerçekten beklenmedik durumlar içindir. Genel hata
yakalayıcı onları yakalar ve 500'e çevirir. Site bunlara `NotFoundException`'ı
ekler; middleware'i bunu ziyaretçinin dilinde gerçek bir 404 sayfası olarak
gösterir.

Aynı kural kullanıcının okuduğu her şey için geçerli. Presentation'ın altındaki
hiçbir katman hazır cümle üretmez. Validator'lar hata kodu taşır; yapısal veri
denetleyicisi de cümle yerine bir anahtar ve parametrelerini döner. Eskiden Türkçe
metin dönüyordu; yani editörün hangi dilde okuyacağına domain katmanı karar
veriyordu ve İngilizce kullanan bir editör Türkçe SEO önerisi görüyordu.

## İstek pipeline'ları

İki uygulamada da sıra kritik. CMS'in güncel sırası
(`src/cms/Presentation/Wasm/Wasm/Program.cs`):

1. `UseWebAssemblyDebugging` (geliştirmede) / `UseExceptionHandler` + `UseHsts`
   (canlıda)
2. `UseForwardedHeaders` (`X-Forwarded-For`/`-Proto`): nginx ya da herhangi bir
   reverse proxy arkasında bu olmazsa her istek 127.0.0.1'den gelen düz HTTP gibi
   görünür; HTTPS algılama ve güvenli çerezler bozulur. Aşağıda `Request.Scheme`'i
   okuyan her şeyden önce çalışmalı.
3. `SecurityHeadersMiddleware`: Erken çalışır ki hata yanıtları dâhil her yanıt
   güvenlik başlıklarını taşısın.
4. `UseHttpsRedirection`, `UseStaticFiles`
5. `Accept-Language`'den dil algılama, sonra `UseRequestLocalization`
6. `UseSession`, sonra `JwtFromSessionMiddleware`: JWT'yi oturumdan alıp
   `Authorization` başlığına koyar.
7. `UseRateLimiter`, `UseAuthentication`, `UseAuthorization`, `UseAntiforgery`
8. `MapStaticAssets` (girişsiz erişilebilir; yoksa varsayılan yetki politikası
   Blazor'un kendi dosyalarını engeller), `MapRazorComponents`
9. Veritabanını kurma ve başlangıç verileri. Aşağıdaki Hangfire kayıtlarından
   **önce** olmalı; Hangfire yazacağı veritabanını kendisi oluşturamaz.
10. Hangfire paneli ve tekrarlanan işler, giriş/medya/dil uç noktaları, `/health`

Site (`src/web/Presentation/WebMvc/Program.cs`):

1. `UseForwardedHeaders`: CMS'teki sebeple aynı. En başta, çünkü aşağıdaki hata
   yakalama bile gerçek şemaya ihtiyaç duyar.
2. `GlobalExceptionHandlerMiddleware`: Kendisinden sonraki her şeyi görsün diye.
3. `SecurityHeadersMiddleware`
4. `UseHsts`, `UseRewriter(SeoRedirectRule)`, `UseHttpsRedirection` (sadece
   canlıda). Yeniden yazma kuralı alan adını (`www.`) ve yol harf büyüklüğünü tek
   biçime getirir ve adres düzeyinde HTTPS'e zorlar. Dosya uzantısı olan yolları
   atlar; yani statik dosyalar asla yeniden yazılmaz ya da yönlendirilmez, sadece
   gerçek sayfa adresleri.
5. `UseResponseCompression` (Brotli + gzip), `UseWebOptimizer`, uzun önbellek
   başlıklarıyla `UseStaticFiles` (`public,max-age=31536000,immutable`)
6. `MaintenanceModeMiddleware`: Statik dosyalardan sonra, böylece bakım sayfasının
   kendi CSS'i ve görselleri yine yüklenir. `/health` muaf.
7. `UseRouting`, `UseAuthorization`
8. `UseOutputCache`: Routing'den sonra olmalı, çünkü `[OutputCache]` endpoint
   bilgisidir. `UseRouting`'den önce konursa endpoint görmez, politika bulamaz ve
   hiçbir şeyi önbelleğe almaz (taşınana kadar durum tam olarak buydu).
9. `UseWebMarkupMin` (HTML küçültme): Output cache'in arkasında; böylece
   önbellekteki kopya küçültülmüş hâldir ve önbellekten gelen istek küçültücüye
   uğramaz.
10. `UseRateLimiter`: Output cache'ten sonra; önbellekten gelen bir sayfa hız
    sınırı hakkını harcamaz.
11. API uç noktaları (istatistik, log, form, arama, önbellek), sonra
    `/{language}/{slug}` için `MapDynamicControllerRoute`, en son sayfa
    controller'ına düşen yedek route.

## Çok dilli adresler

Varsayılan dil önek almaz (`/hakkimizda`); yayındaki diğer her dil kendi önekini
alır (`/en/about-us`). Varsayılan dilin önekli adresi (`/tr/hakkimizda`)
öneksiz adrese 301 ile yönlenir. `SlugRouteValueTransformer` bir adresi sayfaya
çevirir. `ExistingLanguageRouteConstraint` adresteki ilk parçanın bir dil kodu olup
olmadığına karar verir. İkisi de `ILanguageDirectory`'yi kullanır; bu, bellekte
tutulan ve `SiteStateRefreshHostedService` tarafından 30 saniyede bir yenilenen bir
anlık görüntüdür.

Bir dili yayına almanın ya da bakım modunu açıp kapatmanın sitede yeniden başlatma
gerektirmemesinin sebebi bu yenilemedir. Aşağıdaki önbellek temizleme çağrısı iki
anlık görüntüyü de anında yeniler. Pratikte CMS'teki bir değişiklik bir sonraki 30
saniyelik turu beklemeden, bir iki saniye içinde siteye yansır.

Varsayılan dili değiştirmek her sayfanın adresini değiştirir. CMS eski adresleri
yenilerine yönlendiren kuralları yazar; sayfa içeriklerindeki, şablonlardaki, SEO
alanlarındaki (canonical, `og:url`, yapısal veri) ve site ayarlarındaki eski
adresleri yenileriyle değiştirir (`SiteAddressMigration`); site haritasını da
hemen yeniden üretir.

## Sitenin önbelleği

Site iki seviyede önbellek tutar:

| Seviye | Ne | Süre | Nerede |
| --- | --- | --- | --- |
| Hazır sayfalar | `PageController.Index`'in küçültülmüş HTML yanıtının tamamı. Anahtar: adres ve sayfa listesi parametreleri (`page`, `tag`, `q` ve bloklara özel hâlleri; `ListingQuery`). Başka bir parametre (`utm_*`, `fbclid`) ne sayfayı değiştirir ne de yeni bir kayıt açar. | 1 saat | ASP.NET Core output cache (site sürecinin belleğinde), `Pages` politikası |
| Sorgular | Adrese göre sayfa, site ayarları, site kodları | 30 saniye | `ICacheService`: ayarlıysa Redis, değilse süreç belleği |

Bir saat ancak sayfaların kendiliğinden eskimesi gerekmediği için güvenlidir. Her
başarılı CMS komutundan sonra `PublicSiteCacheInvalidationPipelineBehavior`,
`IPublicSiteCacheInvalidator`'dan siteyi temizlemesini ister. Birbirine bir
saniyeden yakın gelen çağrılar (toplu düzenleme, birkaç komuttan oluşan bir kayıt)
tek bir `POST /api/cache/clear` olarak gider. Bu uç nokta sırasıyla
`ICacheService`'i boşaltır, dil ve bakım modu anlık görüntülerini yeniler ve ancak
ondan sonra `pages` output-cache etiketini atar. Böylece araya bir istek girip eski
veriden kurulmuş bir sayfayı yeniden önbelleğe koyamaz.

Bu bilerek "içeriği değiştiren komutlar listesi" değil, "her komut". Bir sayfa
sayfalardan, şablonlardan, medyadan, site ayarlarından, site kodlarından, dillerden,
yönlendirmelerden ve dahasından oluşur; bir liste eklenecek bir sonraki özelliği
kaçırırdı. Siteyi etkilemeyen bir komutun maliyeti bir sayfanın fazladan bir kez
oluşturulmasıdır. Çağrı siteye ulaşamazsa CMS bir uyarı loglar; o zaman en fazla
bir saat eski içerik görünebilir. CMS'i atlayan değişiklikler (veritabanını geri
yükleme, elle SQL düzeltmesi) için `/admin/cache` ekranındaki "Önbelleği Temizle"
butonu kullanılır.

Bir dilin adresleri ancak dil hem aktif **hem de** yayındaysa çalışır. Bu iki
anahtarın ne anlama geldiği [README](../README.tr.md) dosyasının Diller
bölümünde.

## Arka plan işleri

Hangfire, PostgreSQL'de saklanır ve CMS sürecinin içinde çalışır:

| İş | Ne zaman | Ne yapar |
| --- | --- | --- |
| `sitemap-generation` | 2 saatte bir | Site haritası XML'ini yeniden üretip `SitemapCaches`'e yazar |
| `reminder-check` | dakikada bir | Zamanı gelen hatırlatıcıları gönderir |
| `database-backup` | Pazar 02:00 UTC | Tam yedek alır, sonra eski yedekleri siler |
| `data-retention` | her gün 03:20 UTC | İstatistik, tıklama ve log tablolarını kırpar; süresi dolan Çöp Kutusu kayıtlarını kalıcı siler |

Temizlik işi bilerek haftalık yedekten *sonra* çalışır: yedek, temizliğin silmek
üzere olduğu satırları da alır.

## Entegrasyon sırları ve canlı ayar değişikliği

SMTP, CAPTCHA ve S3/CDN giriş bilgileri iki yerde durabilir: `appsettings.json`/ortam
değişkenlerinde (diğer ayarlar gibi açılışta bir kez okunur) **ya da** CMS'in
"Sırlar" ekranında. İkincisinde `IntegrationSecrets` tablosunda, iki uygulamanın
çerezler için zaten paylaştığı Data Protection anahtarlarıyla şifrelenmiş olarak
durur. İkisi de çalışır; ikisinde de değer varsa veritabanındaki kazanır.

Veritabanı tarafı sıradan bir `IOptions<T>` değil. "Sırlar" ekranından yapılan
kayıt iki süreçte de **hemen, yeniden başlatmadan** geçerli olur:

- `IntegrationSecretsConfigurationProvider` (`Presentation/*/Configuration/`, her
  uygulamada bir tane) özel ve *yeniden yüklenebilir* bir `IConfigurationProvider`.
  Tek seferlik `AddInMemoryCollection`'ın aksine `ReloadAsync()` tabloyu yeniden
  okur ve yapılandırma sisteminin kendi yeniden yükleme sinyalini tetikler;
  `IOptionsMonitor<T>` da bu sinyali dinler. Bu yüzden
  `EmailOptions`/`CaptchaOptions`/`ObjectStorageOptions` kullanan her yer
  `IOptions<T>` değil `IOptionsMonitor<T>` alır. `IOptions<T>` değeri DI nesneyi
  oluştururken bir kez yakalar ve yeni değeri hiç görmez.
- `Update`/`ClearIntegrationSecretCommandHandler`, kayıttan hemen sonra
  `IIntegrationSecretsReloader`'ı çağırır (CMS'in kendi kopyası için). Sadece
  `Captcha:*` anahtarı için (sitenin de okuduğu tek değer)
  `IIntegrationSecretsChangeNotifier` ayrıca sitede `POST /api/secrets/reload`
  çağırır. Bunu, önbellek temizlemenin süreçler arası geçmek için zaten kullandığı
  `Cache:ClearSecret` ile doğrulanan iç kanaldan yapar (`CacheClearService`/
  `CacheEndpoints` ile aynı desen).
- `ObjectStorage:Provider`'ı yerel disk ile S3 arasında değiştirmek sadece bir
  ayar değeri değildir. Normalde bu karar DI kaydı sırasında bir kez verilir: hangi
  `IBlobStorage` sınıfının kaydedileceği seçilir. Bunu da canlı yapmak için
  `LocalDiskBlobStorage` ve `S3BlobStorage` her zaman kayıtlı durur;
  `IBlobStorageFactory.Create(IServiceProvider)` her çağrıda güncel `Provider`
  değerine bakarak birini seçer. `IServiceProvider` bilerek parametre olarak
  verilir, factory'nin (singleton) constructor'ında yakalanmaz. Kök provider'ı
  tutan bir singleton, onunla Scoped bir servisi çözerse (burada isteğin kendi
  `DbContext`'ine bağlı `LocalDiskBlobStorage`) Development'ta yüksek sesle hata
  verir ama **Production'da uygulamanın ömrü boyunca tek bir nesneyi sessizce
  sabitler** (`ValidateScopes` tam olarak bu ayrıma göre açık/kapalıdır). Bir
  singleton'ın Scoped bir şeye ihtiyacı olduğunda bunu hatırlamakta fayda var.

## Yumuşak silme

Silinebilen her şey yumuşak silinir. `SaveChanges` bir `Remove` işlemini
`IsDeleted = true`'ya çevirir; genel bir sorgu filtresi de bu satırları her normal
sorgudan gizler. Unique index'ler `"IsDeleted" = false` filtresiyle kurulur;
silinmiş bir satır, yenisinin aynı adresi kullanmasını engellemez.

Kalıcı silme tek bir yerde var: `TrashPurge`. Ona Çöp Kutusu ekranından, "Çöp
kutusunu boşalt"tan ve temizlik işinden ulaşılır. Form yanıtı olan ya da alt
sayfası olan bir sayfayı silmeyi reddeder. Bkz. README'nin Çöp Kutusu bölümü.

`ICmsApplicationDbContext.RemovePermanently` yumuşak silmeyi atlamanın tek yoludur.
Bir bayrak değil de adlı bir metot olmasının sebebi şu: kalıcı silme yanlışlıkla
yazılamasın ve kodda aramayla kolayca bulunsun.

## Sayfa içeriği

Sayfalar GrapesJS ile hazırlanır. `PageContent` üç sütun tutar: oluşturulmuş HTML,
CSS ve editörün kendi proje JSON'u. Site kayıtlı HTML'i gösterir. Editör bileşen
ağacını JSON varsa ondan, yoksa HTML'den yeniden kurar.

Bu civarda bir şeye dokunmadan önce bilinmesi gereken iki sonuç:

- GrapesJS her kayıtta öğe id'lerini ve sınıflarını yeniden yazar; bu yüzden
  **ham HTML'i karşılaştırmak anlamsızdır**. Onay akışındaki fark ekranı bunun
  yerine okurun gördüğü metni karşılaştırır (`HtmlTextDiff`, AngleSharp ile).
- Bir sayfanın bekleyen düzenlemesi, yayındaki `GjsHtml`'den ayrı olarak
  `PreviewGjsHtml` içinde durur. Bir onay akışının bir değişikliği bekletirken
  yayındaki sayfanın sunulmaya devam etmesini sağlayan bu ayrımdır.

Aynı sayfa iki yerde açıksa, sonradan kaydeden diğerinin değişikliklerini fark
etmeden ezmesin diye editör sayfanın açıldığı andaki parmak izini
(`PageFingerprint`) kayda ekler. Sayfa arada başka yerden değiştiyse kayıt
`EditedElsewhere` hatasıyla reddedilir; kullanıcı güncel hâli yüklemeyi ya da
bilerek üzerine yazmayı seçer. Şablonlar için de aynısı geçerli.
