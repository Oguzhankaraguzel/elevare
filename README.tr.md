# Elevare

[English](README.md) · **Türkçe**

[![CI](https://github.com/Oguzhankaraguzel/elevare/actions/workflows/ci.yml/badge.svg)](https://github.com/Oguzhankaraguzel/elevare/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/)

.NET 9 ile yazılmış bir içerik yönetim sistemi (CMS) ve bu sistemin yayınladığı
web sitesi.

İki ayrı uygulama var:

- **CMS**: Blazor Server ile yazılmış yönetim paneli. Sayfalar GrapesJS ile
  sürükle-bırak hazırlanır. Onay akışı, rol bazlı yetkiler, çok dilli içerik ve
  zamanlanmış işler burada.
- **Site**: ASP.NET Core MVC uygulaması. CMS'te yayınlananı ziyaretçiye gösterir.

İkisi aynı PostgreSQL veritabanını kullanır. Kural basit: **tabloları CMS kurar
ve yazar, site sadece okur.**

```
├── src
│   ├── SharedKernel      # İki uygulamanın ortak kullandığı kod
│   ├── cms               # CMS (Core, Infrastructure, Presentation)
│   └── web               # Site (Core, Infrastructure, Presentation)
├── tests
│   ├── cms               # CMS testleri
│   └── web               # Site testleri
└── Elevare.sln
```

## Neler yapabiliyor

- **Sayfayı görerek hazırlama.** GrapesJS ile blokları sürükleyip bırakırsınız.
  Sık kullanılan parçaları şablon olarak saklarsınız. Sayfa yayına çıkmadan önce
  başkasına gönderebileceğiniz bir önizleme linki alırsınız.
- **Onay akışı.** Adımları ve her adımda kimin onaylayacağını siz belirlersiniz.
  Değişiklik karşılaştırması HTML'e değil, okurun gördüğü metne bakar. Böylece
  GrapesJS'in her kayıtta HTML'i yeniden yazması sahte fark üretmez.
- **Rol bazlı yetkiler.** Yetkiler panelden değiştirilebilir ve sunucuda
  denetlenir. Bir butonu gizlemek, işlemi engelleyen tek şey değildir.
- **Çok dilli içerik.** Her dilin kendi adres öneki (`/en/...`) ve kendi yayın
  ayarı var. Sayfalar ekranında hangi sayfanın hangi dilde çevirisi olduğu tek
  tabloda görünür.
- **SEO araçları.** Sayfa başına meta etiketleri ve yapısal veri, panelden
  düzenlenen `robots.txt` ve `llms.txt`, otomatik site haritası. Yönlendirmeler
  ekranı ölü hedefleri ve döngüleri kendisi bulup gösterir.
- **Sosyal medya paylaşımı.** Her sayfa için Open Graph etiketlerinin tamamı
  (her `og:type` ve ona özel alanlar, görsel/video/ses ayrıntıları, dil) ve X'in
  dört kart türü. Editörde paylaşım kartının önizlemesi var.
- **Gereksiz ağır olmayan görseller.** Yüklenen her görselin bir ana kopyası
  saklanır (en fazla 2560 px, dik çevrilmiş, %90 kalitede JPEG). Yanına WebP ve
  küçültülmüş kopyaları üretilir. Site bunları `<picture>`/`srcset` ile sunar;
  `sizes` değerini sayfanın kendi CSS'inden çıkarır. Tek bir görselden tüm favicon
  boyutları üretilir. Editördeki genişlik/yükseklik alanı görselin oranını korur.
  SEO kontrolü yanlış oranda gösterilen görselleri yakalar.
- **Formlar.** Gelen yanıtlar panelde listelenir. Hazır cevap şablonları, spam
  koruması ve dosya eki var (en fazla üç dosya, 10 MB, içeriğine bakılarak kabul
  edilen beş tür). Dosyalar herkese açık değildir; yalnızca yetkisi olan indirir.
- **Etiket yönetimi.** Her etiketin kaç yerde kullanıldığı görünür. Yazım
  hatasıyla oluşmuş bir etiket fark edilir ve silinir, listede sonsuza dek kalmaz.
- **İşletme araçları.** Zamanlanmış yedekleme, eski verilerin temizlenmesi, iş
  takip ekranı, önbellek kontrolü ve kimin neyi sildiğini kaydeden bir Çöp Kutusu.

## Ekran görüntüleri

Sayfa editörü. Sürükle-bırak bloklar, anlık SEO puanı ve yayına çıkmadan
paylaşılabilen önizleme linki:

![Sayfa editörü](docs/screenshots/page-editor.png)

Yönlendirmeler. Ekran önce sorunları söyler: ölü hedef, döngü, art arda zincir.
Bozuk bir yönlendirme başka türlü fark edilmez; ziyaretçi 404'e düşene kadar
kimse görmez.

![Yönlendirme yönetimi](docs/screenshots/redirects.png)

Yetkiler rol bazında ve panelden değiştirilebilir. Buton gizlenerek değil,
sunucuda denetlenir:

![Roller ve yetkiler](docs/screenshots/permissions.png)

Her dilin iki ayrı anahtarı var: CMS'te yazılabilir mi, sitede yayında mı. Böylece
bitmemiş bir çeviri sızmaz, bitmiş bir çeviri de yanlışlıkla gizli kalmaz:

![Dil yönetimi](docs/screenshots/languages.png)

Sayfalar listesi aynı zamanda bir çeviri tablosudur. Yazılabilen ama henüz
yayında olmayan dil işaretlenir. Böylece "Yayında" görünen ama aslında
açılmayan bir sayfa, tam da bakacağınız ekranda kendini belli eder:

![Sayfalar ve çeviriler](docs/screenshots/pages.png)

Editörde "bunu nereden değiştiririm?" sorusunun cevabı hep aynıdır. Sağda tek bir
sütun ve dört sekme var: seçili öğenin ne olduğu için **İçerik**, nasıl
göründüğü için **Görünüm**, sonra **Katmanlar** ve **Bloklar**. Sayfanın kendisine
ait her şey (SEO, sosyal kartlar, yapısal veri, etiketler, diller, site kodları)
tek bir **Sayfa Ayarları** panelinde. Yardımcı araçlar yüzen pencerelerde açılır
ve istediğiniz yere taşınır: medya kütüphanesi, görev panosu, notlar ve
hatırlatıcılar, WhatsApp/kampanya linki oluşturucu, JSON-LD oluşturucu. Hiçbiri
üzerinde çalıştığınız sayfayı kapatmaz.

![Editör araçları](docs/screenshots/tools.png)

Belgeler: [Mimari](docs/ARCHITECTURE.tr.md) ·
[Katkıda bulunma](CONTRIBUTING.tr.md) · [Güvenlik](SECURITY.tr.md) ·
[Değişiklik geçmişi](CHANGELOG.md) (İngilizce)

Çalıştırmanın üç yolu var: **Docker** (en hızlısı), elinizdeki bir PostgreSQL ile
`dotnet run`, ya da IIS'e klasör olarak yayınlama. Size uyanı seçin.

---

## Yol A: Docker

Docker Desktop (ya da Docker Engine ve Compose v2) yeterli. Başka bir şey
gerekmez: ne .NET SDK ne de PostgreSQL kurmanız gerekir.

```bash
cp .env.example .env
```

`.env` dosyasını açıp boş alanları doldurun. Biri bile boşsa sistem başlamaz. Bu
bilerek böyle: hiçbir kurulum varsayılan bir şifreyle çıkmasın diye.

| Değişken | Ne işe yarar |
| --- | --- |
| `POSTGRES_PASSWORD` | `POSTGRES_USER` adlı PostgreSQL kullanıcısının şifresi. |
| `JWT_SECRET` | Yönetici oturumlarını imzalar. **En az 32 karakter** olmalı, yoksa CMS açılırken hata verir. |
| `PREVIEW_SIGNING_KEY` | Yayınlanmamış sayfaların önizleme linklerini imzalar. CMS'te ve sitede **aynı** değer olmalı, yoksa önizleme linkleri çalışmaz. |
| `CACHE_CLEAR_SECRET` | CMS'in "site önbelleğini temizle" isteğini doğrular. İki uygulamada da aynı olmalı. Boş bırakılırsa site her temizleme isteğini 401 ile reddeder. |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | İlk yönetici hesabı. Boş veritabanıyla ilk açılışta oluşturulur. |
| `ADMIN_USERNAME` | Verilmezse `superadmin` olur. |

İki anahtarı elinizdeki araçla üretebilirsiniz:

```bash
openssl rand -base64 48
```

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
```

Sonra:

```bash
docker compose up -d --build
```

İlk derleme birkaç dakika sürer. Bittiğinde:

| | Adres |
| --- | --- |
| CMS (yönetim) | <http://localhost:5150> |
| Site | <http://localhost:5160> |

Portları `.env` içinden değiştirebilirsiniz (`CMS_PORT`, `WEB_PORT`, `DB_PORT`,
`REDIS_PORT`).

İşe yarar komutlar:

```bash
docker compose logs -f cms
```

```bash
docker compose down
```

```bash
docker compose down -v
```

`down` her şeyi durdurur, veriler kalır. `down -v` ayrıca volume'leri de siler:
veritabanı, yüklenen medya, yedekler ve şifreleme anahtarları gider. Gerçekten
sıfırdan bir ilk kurulum istiyorsanız bunu kullanın.

### Compose neleri başlatıyor

- **db**: PostgreSQL 15 (Alpine). Uygulamalar veritabanı hazır olana kadar
  bekler, yani `up` dediğinizde yarış olmaz.
- **cache**: Redis. Zorunlu değil. CMS önbellek türünü Site Ayarları'ndan seçer,
  site Redis olmadan da sorunsuz çalışır. Burada olmasının sebebi, Redis'e geçmenin
  bir ayar değişikliği olması; ayrıca sunucu kurmakla uğraşmazsınız.
- **cms**: Açılırken tabloları kurar ve başlangıç verilerini ekler, sonra
  yönetim panelini sunar.
- **web**: Site.

Volume'ler: `db-data`, `db-backups`, `cms-uploads`, `cms-keys`. `db-backups`
bilerek `db-data`'dan ayrı tutuluyor (bkz. [Ayarlar](#ayarlar)). Yalnızca `cms`
konteynerine bağlı, çünkü PostgreSQL'de veritabanının kendi içinden yedek alan bir
komut yok (SQL Server'daki gibi). Yedeği `DatabaseBackupService`, CMS
konteynerinin içinden `pg_dump` çalıştırarak alır.

### Geliştirme araçları (isteğe bağlı)

`docker-compose.dev-tools.yml`, normalde gerçek bir dış servis gerektirecek iki
şey için iki konteyner ekler. **Yalnızca geliştirme içindir.** Başka bir ortama
göre ayarlanmadılar; Seq bilerek hiç kimlik doğrulama olmadan çalışır.

```bash
docker compose -f docker-compose.yml -f docker-compose.dev-tools.yml up -d
```

| | Ne sağlar | Adres |
| --- | --- | --- |
| **Mailpit** | CMS'in gönderdiği tüm e-postaları yakalar. SMTP'yi ve Site Ayarları'ndaki gönderen adresini gerçek bir posta kutusu olmadan deneyebilirsiniz. | <http://localhost:8025> |
| **Seq** | İki uygulamanın da logları tek yerde, alanlara göre aranabilir. Örnek: `Application = 'Elevare.Web' and RequestPath like '/urun%'`. Her kayıtta `Application` alanı olduğu için iki uygulamanın logları karışmaz. | <http://localhost:5341> |

Portlar `.env` içinde `MAILPIT_UI_PORT` ve `SEQ_UI_PORT`. Seq geçmişini `seq-data`
volume'ünde tutar; yeniden derleme okuduğunuz logları silmez. Ek dosyayı vermeden
(`docker compose up -d`) başlatırsanız iki uygulama da loglarını yine konsola
yazar ve bu iki konteyner durur.

---

## Yol B: Docker olmadan

[.NET 9 SDK](https://dotnet.microsoft.com/download) ve erişebildiğiniz bir
PostgreSQL gerekir (bilgisayarınızda kurulu, Docker'da ya da uzak bir sunucuda).

**1. Uygulamaları veritabanına bağlayın.** İki `appsettings.json` dosyasında da
şu bağlantı var:

```
Host=localhost;Port=5432;Database=ElevareDB;Username=postgres;Password=postgres
```

Varsayılan ayarlarla kurulmuş yerel bir PostgreSQL'de olduğu gibi çalışır.
Farklıysa değiştirin; nasıl yapılacağı aşağıda [Ayarlar](#ayarlar) bölümünde.

**2. Gizli değerleri girin.** En az şunlar gerekli: `Jwt:SecretKey` (32+
karakter), `Preview:SigningKey` ve `Cache:ClearSecret` (ikisi de iki uygulamada
aynı değer) ve ilk yönetici hesabı.

En kolayı, uygulamaların yanındaki iki örnek dosyayı kopyalamak:

```bash
cp src/cms/Presentation/Wasm/Wasm/appsettings.Development.json.example src/cms/Presentation/Wasm/Wasm/appsettings.Development.json
```

```bash
cp src/web/Presentation/WebMvc/appsettings.Development.json.example src/web/Presentation/WebMvc/appsettings.Development.json
```

Bu kopyalar git'e girmez; içine yazdıklarınız sizin bilgisayarınızda kalır.
Gizli değerlerin hiç dosyada durmasını istemiyorsanız `dotnet user-secrets` aynı
işi görür:

```bash
dotnet user-secrets --project src/cms/Presentation/Wasm/Wasm set "Jwt:SecretKey" "<32+ karakter>"
```

```bash
dotnet user-secrets --project src/cms/Presentation/Wasm/Wasm set "Seed:SuperAdmin:UserName" "superadmin"
```

```bash
dotnet user-secrets --project src/cms/Presentation/Wasm/Wasm set "Seed:SuperAdmin:Email" "siz@example.com"
```

```bash
dotnet user-secrets --project src/cms/Presentation/Wasm/Wasm set "Seed:SuperAdmin:Password" "<şifreniz>"
```

```bash
dotnet user-secrets --project src/web/Presentation/WebMvc set "Preview:SigningKey" "<CMS'tekiyle aynı değer>"
```

**3. Çalıştırın.** CMS açılırken veritabanını kendisi oluşturur ve tabloları
kurar. Ayrıca bir migration adımı yok.

```bash
dotnet run --project src/cms/Presentation/Wasm/Wasm/Wasm.csproj --launch-profile "CMS (https)"
```

```bash
dotnet run --project src/web/Presentation/WebMvc/WebMvc.csproj --launch-profile "Web (https)"
```

Visual Studio'da **Elevare (CMS + Web)** profili ikisini birden başlatır.

| Uygulama | HTTPS | HTTP |
| --- | --- | --- |
| CMS (yönetim) | `https://localhost:7150` | `http://localhost:5150` |
| Site | `https://localhost:7160` | `http://localhost:5160` |

### "Address already in use" hatası

Düzgün durdurulmayıp zorla kapatılan bir debug oturumu portu tutmaya devam eder:

```powershell
Get-Process Wasm,WebMvc -ErrorAction SilentlyContinue | Stop-Process -Force
```

---

## Yol C: IIS'e klasör olarak yayınlama

Ne Docker ne `dotnet run`: her uygulamayı bir klasöre yayınlarsınız, IIS çalıştırır.
Projeler kendi `web.config` dosyalarını üretir, IIS'in onları tanıması için ek bir
şey gerekmez.

```bash
dotnet publish src/cms/Presentation/Wasm/Wasm/Wasm.csproj -c Release -o C:\inetpub\elevare-cms
```

```bash
dotnet publish src/web/Presentation/WebMvc/WebMvc.csproj -c Release -o C:\inetpub\elevare-web
```

Sunucuda [.NET 9 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/9.0)
kurulu olmalı. Sadece runtime yetmez; IIS'in ihtiyaç duyduğu ASP.NET Core Module
bu paketle gelir.

`appsettings.Development.json` yayın çıktısına bilerek konmaz. Yerel bağlantı
bilginiz, imza anahtarlarınız ve ilk yönetici şifreniz sunucuya gitmez.

### Ayarları verme

[Ayarlar](#ayarlar) bölümündeki her şey burada da geçerli. Ayarları uygulama
havuzunda ortam değişkeni olarak ya da `web.config` içinde verin:

```xml
<aspNetCore processPath="dotnet" arguments=".\Wasm.dll" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    <environmentVariable name="ConnectionStrings__DefaultConnection" value="Host=localhost;Port=5432;Database=ElevareDB;Username=elevare;Password=..." />
    <environmentVariable name="Jwt__SecretKey" value="..." />
    <environmentVariable name="Preview__SigningKey" value="..." />
    <environmentVariable name="Cache__ClearSecret" value="..." />
    <environmentVariable name="DataProtection__KeyPath" value="C:\inetpub\elevare-keys" />
  </environmentVariables>
</aspNetCore>
```

PostgreSQL, SQL Server'daki `Trusted_Connection` gibi Windows kimliğiyle giriş
yapmaz; bağlantı bilgisindeki kullanıcı adı ve şifreyi kullanır. Yukarıdaki
kullanıcının veritabanında kendi girişi ve yetkileri olmalı.

### Yapmazsanız başınızı ağrıtacak üç şey

**`DataProtection:KeyPath` ayarını verin.** Verilmezse şifreleme anahtarları
kullanıcı profiline yazılır. Profil yüklemeyen bir uygulama havuzunda ise
anahtarlar sadece bellekte durur. Sonuç: havuz her yeniden başladığında tüm
yöneticilerin oturumu düşer ve açık sayfalardaki form güvenlik anahtarları
geçersiz olur. Bu ayarı, uygulama havuzunun yazabildiği bir klasöre yönlendirin.

**Uygulama havuzunu sürekli çalışır yapın, yoksa zamanlanmış işler durur.**
Hangfire CMS'in içinde çalışır. IIS varsayılan olarak 20 dakika istek gelmeyen
uygulamayı kapatır. Biri paneli tekrar açana kadar site haritası üretilmez,
hatırlatıcı gitmez, yedek alınmaz, eski veri temizlenmez. Uygulama havuzunun
Gelişmiş Ayarlar'ında **Start Mode** değerini `AlwaysRunning`, **Idle Time-out**
değerini `0` yapın. Sonra IIS'in *Application Initialization* özelliğini açın ve
sitede `preloadEnabled="true"` verin.

**Uygulama havuzu kimliğine yazma izni verin.** Şu klasörlere: CMS'in
`wwwroot\uploads` klasörü, `DataProtection:KeyPath` klasörü ve `Backup:Directory`
(ya da boşsa uygulamanın kendi klasörü altındaki varsayılan yer). PostgreSQL'de
SQL Server'daki gibi `BACKUP DATABASE` komutu yok. `DatabaseBackupService` yedeği
uygulamanın içinden `pg_dump`/`pg_restore` çalıştırarak alır. Yani
`postgresql-client` araçlarının PATH'te olması ve o klasöre yazabilmesi gereken,
veritabanı servis hesabı değil **uygulama havuzu kimliğidir**.

`Backup:Directory` boşsa yedekler uygulamanın yanındaki bir klasöre yazılır.
Denemek için olur ama asıl kurulumda yedekleri PostgreSQL verisinin durduğu
diskten farklı bir diske (ya da ağ paylaşımına) yönlendirin. Yoksa tek bir disk
gittiğinde hem veritabanı hem tüm yedekleri birlikte gider.

### Bilinen kısıtlama

Her uygulama **kendi sitesinin kökünde** çalışmak ister. CMS'i bir alt klasörde
(`https://example.com/cms`) yayınlamak işe yaramaz: linkleri kökten başlar ve alan
adının köküne gider. Her uygulamaya ayrı bir site ya da alt alan adı verin.

---

## Her push'ta otomatik yayın

`master`'a yapılan her push kendini yayına alabilir. `.github/workflows/ci.yml` önce
testlerin, güvenlik açığı taramasının ve iki imaj derlemesinin geçmesini bekler.
Sonra sunucuya bağlanıp sizin elle yazacağınız komutların aynısını çalıştırır:

```bash
git fetch origin master
git reset --hard origin/master
docker compose up -d --build
docker image prune -f
```

Bu adım siz açana kadar kapalıdır; böylece projeyi fork'layan biri olmayan bir
sunucuya bağlanmaya çalışmaz. *Settings → Secrets and variables → Actions* altına iki
**değişken** ekleyin: `true` değerli `DEPLOY_ENABLED` ve sunucudaki proje klasörünü
gösteren `DEPLOY_PATH`. Bir de dört **gizli değer** ekleyin:

| Ad | Ne |
| --- | --- |
| `DEPLOY_HOST` | Sunucunun adresi ya da IP'si. |
| `DEPLOY_USER` | SSH kullanıcısı. `docker` grubunda olmalı ve proje klasörünün sahibi olmalı. |
| `DEPLOY_SSH_KEY` | Açık anahtarı o kullanıcının `~/.ssh/authorized_keys` dosyasında olan özel anahtar. Kendi anahtarınızı kullanmayın, bu iş için yenisini üretin. |
| `DEPLOY_PORT` | SSH portu. |

İlk çalıştırmadan önce bilmeniz gereken iki şey:

- **`git reset --hard` sunucuda yapılmış değişiklikleri siler.** Git'in takip
  ettiği her dosya için geçerli. `.env` takip edilmediği için korunur; sunucuda elle
  düzenlediğiniz bir ayar dosyası korunmaz.
- **İmajları sunucu derler.** Her yayında birkaç dakika işlemci harcar; çoğu
  CMS'in Blazor WebAssembly tarafı içindir. Bu sorun olursa çözüm, imajları CI'da
  derleyip sunucunun hazır imajı çekmesidir. Daha büyük bir değişiklik; derleme
  süresi gerçekten can sıkana kadar gerek yok.

---

## İlk açılış

Hangi yolu seçtiyseniz seçin, boş bir veritabanıyla ilk açılışta şunlar olur:

1. Veritabanı oluşturulur ve tek migration (`InitialCreate`) uygulanır.
2. Roller eklenir: SuperAdmin, Admin, Editor, Author, Viewer, Developer.
3. Yönetici hesabı oluşturulur, **ama sadece** `Seed:SuperAdmin:UserName`,
   `Email` ve `Password` üçü de verilmişse. Biri eksikse hiçbir şey oluşturulmaz.
   Bu bilerek böyle: bir yazım hatası yüzünden sizin seçmediğiniz bir şifreyle
   hesap açılmasın.
4. İki dil, site ayarları listesi, bakım sayfası ve üç hazır form cevap şablonu
   eklenir.

Yönetici ayarlanmamışsa ve kullanıcı tablosu boşsa CMS açılırken bunu söyleyen bir
hata yazar. Üç değeri girip yeniden başlatın.

**Giriş için kullanıcı adını da e-postayı da kullanabilirsiniz.** Giriş alanında
da öyle yazar. Şifre sizin verdiğiniz şifredir.

Yeni bir kurulumda bozukluk sanılabilecek ama aslında bilerek yapılmış iki şey:

- **Site bakım modunda açılır.** `Advanced.MaintenanceModeEnabled` başta `true`
  gelir. Siz **Site Ayarları → Sistem**'den kapatana kadar site bakım sayfasıyla
  `503` döner. Amaç şu: içeriği olmayan bir site yayında olmasın. Kapattığınızda
  bir iki saniye içinde (site o an ulaşılamıyorsa en fazla 30 saniyede) yeniden
  başlatmaya gerek kalmadan açılır.
- **`/sitemap.xml` siz bir şey yayınlayana kadar boştur.** Açılışta bir kez, sonra
  iki saatte bir üretilir. Yani dosya hemen vardır, sadece içinde listelenecek bir
  şey yoktur.

---

## Diller: CMS'te açık ve sitede yayında

Her dilin birbirinden bağımsız iki anahtarı var. Bunları karıştırırsanız ya
bitmemiş bir çeviri sitede görünür ya da bitmiş bir çevirinin neden görünmediğini
merak edersiniz:

| Anahtar | CMS'teki sütun | Ne yapar |
| --- | --- | --- |
| **Aktif** | "CMS'te" | Bu dilde içerik yazılabilir. Sayfalar bu dile çevrilebilir. Sitede ne olacağıyla ilgisi yoktur. |
| **Yayında** | "Sitede" | Dil yayındadır. Adres öneki çalışır, dil seçicide görünür, sayfaları aramada çıkar. |

Ziyaretçinin bir şey görmesi için ikisinin de açık olması gerekir. Bu sayede bir
çevirmen yeni bir dilde istediği kadar çalışır, hiçbir şey dışarı sızmaz. "Bu dili
siteden kaldır" da tek bir anahtardır ve editörlerin kendi taslaklarına erişimini
kesmez.

Kurulumla gelen iki dilde iki anahtar da açıktır; ilk sayfayı yayınladığınız anda
site onu gösterebilir. **Sonradan eklediğiniz dil yayında olmadan başlar.**
Anahtarın amacı da bu. Çeviri hazır olunca o kutuyu işaretlersiniz.

Yeni bir dil **aktif ama yayında değil** olarak oluşturulur. Yayına aldığınızda
sitede bir iki saniye içinde görünür, yeniden başlatma gerekmez.

Varsayılan dili değiştirmek her sayfanın adresini değiştirir: varsayılan dil
önek almaz (`/hakkimda`), diğer diller kendi önekini alır (`/en/about`). Eski
adresler yenilerine 301 ile yönlenir. Sayfalardaki linkler, paylaşım adresleri ve
yapısal veri de yeni adreslere göre güncellenir. Kaydettikten sonra CMS kaç adresin
taşındığını söyler.

CMS'te kaydedilen her değişiklik de böyle hemen yansır. Site hazırladığı sayfaları
bir saate kadar saklar ama CMS her başarılı kayıttan sonra siteye "bunları at" der.
Değişiklik bir sonraki sayfa açılışında görünür. **Önbellek** ekranındaki "Önbelleği
Temizle" butonu, CMS dışında yapılan değişiklikler içindir (veritabanını geri
yükleme, elle düzeltme gibi). Ayrıntılar
[docs/ARCHITECTURE.tr.md](docs/ARCHITECTURE.tr.md#sitenin-önbelleği) içinde.

**Sayfalar** ekranında, yazılabilen ama yayında olmayan bir dilin kodu üstü çizili
göz simgesiyle işaretlenir. "Yayında" görünen ama aslında açılmayan bir sayfa
böylece göze çarpar.

## Şablonlar: bağlı ve kopya

Şablon, bir kez hazırlayıp birçok sayfada kullandığınız GrapesJS içeriğidir: üst
menü, alt bilgi, WhatsApp butonu gibi. Sayfaya nasıl eklendiği, şablonu sonradan
düzenlemenin ne anlama geldiğini belirler:

| | **Bağlı** (`IsLinked`) | **Kopya** |
| --- | --- | --- |
| Sayfada ne durur | Sadece bir işaret: `<div class="elevare-tpl-ref" data-elevare-template-id="…">` | İçeriğin kendisi. `elevare-tpl-snapshot` ile sarılır ve o anki şablon sürümü üstüne yazılır. |
| Sitede | Her istekte şablonun **güncel** hâli konur (`TemplateResolutionService`). Şablonu düzenlersiniz, tüm sayfalar takip eder; sayfaları tekrar kaydetmeniz gerekmez. | Kopyalanan neyse o. Şablon değişse de bu değişmez. |
| Sayfa editöründe | **Salt okunur.** Üzerinde herhangi bir yere tıklamak şablonun tamamını seçer. Araç çubuğunda *şablonu düzenle* (şablon editörünü yeni sekmede açar) ve *bağı kopar* (bu örneği kopyaya çevirir) var. Bütün olarak taşıma, çoğaltma ve silme yine çalışır. | Normal içerik gibi yerinde düzenlenir. Kaynak şablonun içeriği o zamandan beri değiştiyse editörün üstünde bir uyarı çıkar: kopyanın ne zaman alındığını ve şablonun ne zaman değiştiğini söyler, *şablonu aç* ve *bir daha gösterme* (o sürüm için) seçeneklerini sunar. Değişikliği asla kendiliğinden getirmez. |

İkisi iç içe kullanılabilir. Bir kopya şablonun içinde bağlı şablonlar olabilir.
Örneğin içinde sitenin bağlı üst menüsü ve alt bilgisi olan bir makale düzeni:
bundan oluşturulan sayfa düzenin kendi kopyasını alır, üst menü ve alt bilgi ise
bağlı kalır ve siteyle birlikte değişir. Bağlı bir şablonun içinde başka bir bağlı
şablon da olabilir (içinde bağlı arama kutusu olan bir menü gibi). Hem site hem
editör bunları kat kat, en fazla beş seviyeye kadar çözer. Kendi içine düşen bir
şablon ikinci kez yerleştirilmez.

"Şablon değişti" uyarısı yalnızca kopyanın gerçekten aldığı kısım değişince çıkar
(`PageTemplate.ContentChangedAt`). Bir düzeni sadece içindeki üst menü değişti diye
tekrar kaydetmek, o düzenden yapılmış her sayfaya uyarı göstermez; o sayfalar üst
menüyü zaten canlı alıyor. Bağlı bir şablonun içindeki kopyalar o şablona aittir;
uyarıları onu kullanan her sayfada değil, şablonun kendi editöründe çıkar.

Her yerde aynı olması gereken şeyler (üst menü, alt bilgi) için bağlıyı seçin. Bir
sayfanın kendine has bir hâli olacaksa kopyayı seçin. Bağı koparmak geri alınmaz;
şablona geri dönmek için kopyayı silip şablonu yeniden ekleyin.

Bağlı içerik sayfada neden kilitli: içi her istekte ve editör her açıldığında
şablonun güncel hâliyle değiştirilir. Kilit olmasaydı oraya yaptığınız düzenleme
kaydedilir ama hiç görünmez, sayfayı bir sonraki açışınızda da kaybolurdu; ekranda
da bunu söyleyen hiçbir şey olmazdı. Kilit bunu imkânsız kılar.

### Editörde gizli paneller

Bazı bloklar ziyaretçi bir şey yapana kadar bir kısmını gizler: Mega Menü'nün
açılır kısmı, Arama Kutusu'nun sonuçları, Pop-up, Yan Panel. Kanvasta da sitedeki
gibi kapalı başlarlar. Bloğun araç çubuğundaki ▼/▲ butonu tek birini açıp kapatır.
Üst çubuktaki ⇕ butonu hepsini birden açar ya da kapatır. Katmanlar panelinden
kapalı bir panelin içindeki bir şeyi seçerseniz panel açılır. Bu açık/kapalı
durumların hiçbiri kaydedilmez. SSS'nin cevapları ve Akordeon'un bölümleri tam
tersi: kanvasta yerinde düzenlenebilsinler diye açık durur, sitede kapalı çıkar.
Bir bölümün sitede açık gelmesini istiyorsanız "Başlangıçta açık" kutusunu
işaretleyin.

## Sayfa listeleri

"Sayfa Listesi" bloğu sayfaları listeler. Seçenekler:

- bulunduğu sayfanın alt sayfaları (en sık kullanılan: "Makaleler" sayfasında
  makaleleri listelemek),
- bulunduğu sayfanın kardeşleri (aynı üst sayfadaki diğer makaleler),
- seçtiğiniz bir sayfanın alt sayfaları,
- sayfayla aynı dildeki tüm yayında sayfalar (sistem sayfaları ve listenin kendi
  sayfası hariç).

İsterseniz sadece sayfayla ortak etiketi olanları gösterebilirsiniz. Blokta tek bir
kart bulunur; her sonuç bu karttan çoğaltılır. Kartın alanları her sayfadan
doldurulur: başlık, açıklama (yoksa makalenin ilk paragrafı), paylaşım görseli
(yoksa içerikteki ilk görsel, o da yoksa bloğun, en son sitenin varsayılan
görseli), yayın tarihi ve etiketler. Sayfada karşılığı olmayan bir alan örnek
metinle bırakılmaz, kaldırılır.

**Sadece yayındaki sayfalar listelenir.** Editörde kanvas, bloğun sitede
gerçekten göstereceği kartları gösterir. Dışarıda kalan sayfalar (taslak,
arşivlenmiş, onay bekleyen, pasif) soluk görünür. Sağdaki ayarlar kaç sayfanın
listeleneceğini söyler ve listelenmeyecek olanlara link verir. Bu önizlemenin
hiçbiri sayfayla kaydedilmez.

**Tarih ve sıra.** Kartlarda görünen tarih ve "en yeni/en eski" sıralaması
sayfanın yayın tarihine bakar (`PageInfo.PublishedAt`). Bu tarih, sayfadaki Makale
bloğunda yazan tarihtir; yoksa sayfanın ilk yayına çıktığı andır. Her kayıtta ve
durum değişikliğinde güncellenir; mevcut sayfalar için açılışta doldurulur.

**Adresler.** Sayfadaki ilk listeyi `?page=2`, `?tag=csharp` ve `?q=…` yönetir.
Sonraki listeler aynı adların sonuna bloğun kimliği eklenmiş hâlini kullanır
(`?page-ilist2=2`); böylece her liste kendi sayfalamasını yapar. Adresteki başka
hiçbir şey linklere, arama formuna ya da canonical adrese taşınmaz, sayfa
önbelleğinde de ayrı tutulmaz. 1. sayfa, sayfanın kendi adresidir. Son sayfadan
sonrası 404 döner. Arama sonuçları `noindex, follow` olur. Listelenen sayfalar
yapısal veriye `ItemList` olarak eklenir (Kategori sayfasının taslak yapısal
verisi `CollectionPage` kullanır). Arama başlık ve açıklamada, büyük/küçük harfe
bakmadan yapılır.

## Blog ve okuma blokları

| Blok | Ne yapar |
| --- | --- |
| Kod Bloğu | Kodu dili, isteğe bağlı dosya adı, satır numaraları ve "Kopyala" butonuyla gösterir. Kod düz bir metin kutusunda düzenlenir ("Kodu düzenle" ya da çift tıklama). Renklendirme sunucuda yapılır (`CodeHighlighter`: C#, JavaScript, TypeScript, JSON, HTML, XML, CSS, SQL, Bash, PowerShell, Python). Siteye renklendirme script'i yüklenmez; renk stilleri sadece kod içeren sayfalara yazılır. |
| Önceki / Sonraki Yazı | Aynı üst sayfa altında, yayın tarihine göre bu sayfadan hemen önce ve hemen sonra yayınlanan sayfalara link verir (`AdjacentPageResolutionService`). |
| İlgili Yazılar | Hazır ayarlı bir Sayfa Listesi: bu sayfanın kardeşlerinden, onunla ortak etiketi olan üç tanesi. |
| Altyazılı Görsel | Altyazılı `<figure>`. Tıklayınca büyük açılır. Galeri bloğunda da aynı "Tıklayınca büyüt" anahtarı var; görseller arasında ok tuşlarıyla geçilir. |
| Okuma İlerleme Çubuğu | Okurun makalenin (ya da sayfanın) neresinde olduğunu gösteren ince bir çubuk. |
| Sekmeler | Erişilebilir sekmeler (ok tuşları, Home/End çalışır). Script yüklenmezse tüm paneller alt alta görünür. |
| Yukarı Çık Butonu | Bir ekran boyu aşağı inince belirir. |

Makale bloğu, tarihinin yanında okuma süresini gösterebilir ("6 dk okuma"). Site bu
süreyi gerçekten yayınlanan kelimeleri sayarak hesaplar (`ContentEnhancer`).

Her dilin bir makale akışı (RSS) var: varsayılan dil için `/feed.xml`, diğerleri
için `/{kod}/feed.xml`. Türü Makale olan sayfalar, yayın tarihine göre en yeniden
eskiye sıralanır. Her sayfanın `<head>` kısmında bu akışa link verilir; akış
okuyucuları ve Google Discover'ın "Takip et" özelliği bunu kullanır.

## Google ve paylaşım blokları

"Google & Paylaşım" grubundaki bloklar dışarıya giden butonlardır. Adresleri
birkaç alandan kurulur, elle URL yazmazsınız. Üçüncü taraftan hiçbir şey yüklemezler
(script yok, çerez onayı gerekmez, güvenlik politikasına ekleme gerekmez):

| Blok | Nereye götürür |
| --- | --- |
| Google Tercihli Kaynak | Google'da bu siteyi "tercih edilen kaynak" seçme ekranına (`google.com/preferences/source?q=<alan adı>`). Başka bir adres yazılmadıysa Site Ayarları'ndaki site adresi kullanılır. Google sadece alan adı ya da alt alan adı kabul eder, yol kabul etmez. Bu seçeneğin çıkması için sitenin Google'ın kaynak tercihleri aracında tanınıyor olması gerekir. |
| Google Haberler'de Takip Et | Yayının Google Haberler sayfasına (adresi Publisher Center'dan alınır). |
| Google'da Yorum Yaz | Google İşletme Profili'nin "yorum yaz" kutusuna; Place ID ile. |
| Yol Tarifi Al | Site Ayarları'ndaki adrese (ya da yazdığınız başka bir adrese) Google Haritalar yol tarifine. |
| Takvime Ekle | Bir etkinlik için Google Takvim'e; Apple Takvim ve Outlook için `.ics` dosyası da verir. |
| YouTube Abone Ol | Site Ayarları'ndaki kanala, YouTube'un kendi "Abone olunsun mu?" sorusuyla. |
| Paylaşım Butonları | X, LinkedIn, Facebook, WhatsApp, Telegram, e-posta, linki kopyala ve cihazın kendi paylaşım menüsü. Paylaşılan adres sayfanın canonical adresidir; sayfada küçük bir script doldurur. |

## Paylaşım etiketleri: ne yazarsanız o çıkar

Sosyal Paylaşım paneli (Sayfa Ayarları › Sosyal Paylaşım) bir sayfanın tüm Open
Graph ve X alanlarını tutar. Site tam olarak bunları yazar. Boş bir alan için etiket
yazılmaz; yazarın haberi olmadan arka planda hiçbir şey doldurulmaz.

"Otomatik doldur" butonu boş alanları, bilinen bilgilerden bir kere doldurur:
sayfa başlığı ve açıklaması (yoksa Site Ayarları'ndaki varsayılan açıklama),
içerikteki ilk görsel (yoksa Site Ayarları'ndaki varsayılan paylaşım görseli) ve
medya kütüphanesinden onun boyutu, türü ve alt metni, sayfanın adresi, dili, yayın
ve güncelleme tarihleri, etiketleri, site adı ve X profili.

SEO analizi paylaşım etiketlerini alanlarda yazdığı hâliyle kontrol eder: başlık,
açıklama ya da görsel eksik mi, görselin boyutu yazılmış mı, görsel çok mu küçük,
açıklaması var mı.

## Site kodları ve bir sayfada kapatma

Site Kodları, sitenin her sayfasına eklenen kod parçalarıdır: temanın temel CSS'i
ve marka renkleri, çerez onay bandı, analiz kodu, canlı destek balonu gibi. Her
birinin adı, sırası ve konumu (head başı/sonu, body başı/sonu) vardır; her biri
site genelinde açılıp kapatılabilir.

Bir sayfa bunlardan bazılarını kendisi için kapatabilir. Sayfa editöründe **Sayfa
Ayarları**'ndaki **Site Kodları** sekmesi kodları konumlarına ve adlarına göre
listeler. İşaretlediğiniz satır "bu sayfada olmasın" demektir. Listede sadece adlar
görünür; kodun kendisi yine Site Kodları ekranında, `CustomCode.Author` yetkisinin
arkasındadır.

Bu bilerek sadece hariç tutmadır. Site kodu tanımı gereği her sayfada çalışır,
sayfa da istisnadır. Kendine özel kodu olması gereken sayfa için editörde özel kod
bloğu var; "sayfaya özel site kodu" zaten o olurdu. Hariç tutmalar
`PageInfoSiteCodeExclusions` tablosunda durur. İki taraftan da silinince
kendiliğinden silinirler; yani Çöp Kutusu'ndan kalıcı silinen bir kod, hariç
tutmalarını da götürür. Hariç tutma hem sitede (404/500 sayfaları dâhil) hem de
editör kanvasında geçerlidir.

## Çöp Kutusu

Sayfa, şablon, medya dosyası, dil, site ayarı, site kodu, görev, not, hatırlatıcı
ya da toplu içerik düzenlemesi silindiğinde aslında silinmez: kayıt durur ve
**Çöp Kutusu**'ndan geri getirilebilir. Bunun kimsenin kullanamadığı bir arşive
dönüşmemesi için dört kural var.

**Bir adres için tek bir silinmiş kayıt.** Bir sayfayı sildiğinizde, aynı adres ve
dilde daha önce silinmiş kayıt varsa o tamamen silinir. Çöp Kutusu "son silmeyi geri
al" işini görür, "her denemeyi göster" işini değil. Bu kural olmasaydı sil/yeniden
oluştur döngüsü, asla geri getirilemeyecek bir yığın kayıt bırakırdı; çünkü o
adresi yayındaki bir sayfa geri almış olurdu.

**Yeniden oluşturmadan önce sorar.** Silinmiş bir sürümü olan dilde yeni bir çeviri
oluşturursanız CMS onun ne zaman ve kim tarafından silindiğini gösterir ve iki
seçenek sunar: içeriği ve geçmişiyle geri getir ya da sıfırdan başla. Sıfırdan
başlarsanız eski kayıt tamamen silinir; yeni sayfa o adresi aldıktan sonra eski
kayıt zaten hiç geri getirilemezdi.

**Geri getirilen site kodu kapalı gelir.** Çöp Kutusu'ndan dönen diğer her şey,
dönüp bakacağınız bir şey olarak gelir. Site kodu ise bir sonraki sayfa açılışında
her ziyaretçinin tarayıcısında çalışacak bir script olarak gelir. Bu yüzden kaydı
geri getirmek ve kodu yeniden yayına almak iki ayrı karardır: kayıt döner, açık
olma durumu (`IsEnabled`) dönmez. Ne yayınlayacağınızı okuduktan sonra Site Kodları
ekranından açarsınız.

**Kalıcı silme gerçekten siler, ama gerektiğinde reddeder.** Her satırda *Kalıcı
sil* var, araç çubuğunda da *Çöp kutusunu boşalt*. İkisi de başka bir şeyi
de götürecek sayfaları atlar:

| Ne zaman engellenir | Neden |
| --- | --- |
| Sayfanın form yanıtları varsa | Bunlar ziyaretçilerin gönderdiği başvurulardır, içerik değil. |
| Sayfanın hâlâ alt sayfaları varsa | Alt sayfaların üst sayfa bağlantısı boş bırakılamaz. |

Buton pasif olur, sebebi üzerine gelince görünür. "Çöp kutusunu boşalt" kaç kaydı
sildiğini *ve* kaçını koruduğunu söyler. Söylemeseydi, ekranda satır bırakan bir
boşaltma bozuk buton gibi görünürdü.

**Otomatik temizlik.** `Retention:TrashDays` (varsayılan 30) kadar gün geçen
silinmiş sayfaları her gece çalışan temizlik işi kalıcı olarak siler. `0` yaparsanız
Çöp Kutusu hiç boşalmaz. Aynı güvenlik kontrolleri burada da geçerli; zamanlayıcı
ziyaretçi verisini sessizce silemez.

## Yönlendirmeler

Yayındaki bir sayfanın adresini değiştirdiğinizde, sayfayı sildiğinizde ya da
arşivlediğinizde CMS kendiliğinden bir yönlendirme kuralı yazar. Böylece eski
adres çalışmaya devam eder; var olan her link ve arama sonucu 404'e dönmez.
Varsayılan dil değiştiğinde taşınan adresler için de aynısı olur. **İçerik →
Yönlendirmeler** ekranı bunları listeler. Ayrıca hiçbir sayfa düzenlemesinin
üretemeyeceği kuralları buradan eklersiniz: bu CMS'ten önceki eski adresler,
kampanya kısa linkleri ya da hiçbir yere gitmemesi, `410 Gone` dönmesi gereken
emekli bir sayfa.

Ekran önce sorunları gösterir. Bozuk bir yönlendirme sessizdir: CMS'te her şey
yolunda görünür, 404'ü yalnızca ziyaretçi görür:

| Durum | Anlamı |
| --- | --- |
| **Ölü hedef** | Gidilen yer yayında bir sayfa değil. Kural ziyaretçiyi 404'e gönderiyor. |
| **Döngü** | Zincir başladığı yere dönüyor. Ziyaretçi hiçbir yere varamıyor. |
| **Zincir** | Birden fazla adım var. Çalışır ama her adım bir gidiş-dönüş demek ve arama motorları bir yerden sonra takip etmeyi bırakır. |
| **410 Gone** | Bilerek kaldırılmış, yerine bir şey yok. |
| **Dış adres** | Başka bir siteye gidiyor; CMS bunu kontrol edemez. |

Sayfa adresi değişince oluşan kural o sayfaya *bağlı* kalır: hedefi sayfanın
güncel adresini takip eder. Adresi iki kez değiştirseniz de geride eskimiş bir
zincir kalmaz. Böyle bir kuralı elle düzenlerseniz bağ kopar; ekran bunu
kaydetmeden önce söyler.

Yayındaki bir sayfanın kendi adresine denk gelen (o sayfayı gizleyecek) ya da bir
döngüyü kapatan kural kaydedilmez.

## Güvenlik

Uygulamaların kendi yaptığı korumalar. Böylece düz Kestrel ile çalışan bir kurulum,
güçlendirilmiş bir proxy arkasındakinden belirgin şekilde zayıf olmaz.

**Yanıt başlıkları.** İki uygulama da hata sayfaları dâhil her yanıta şunları
ekler: `X-Content-Type-Options: nosniff`, `Referrer-Policy:
strict-origin-when-cross-origin`, kamera, mikrofon, ödeme ve USB'yi kapatan bir
`Permissions-Policy`. Sayfanın başka bir sitenin içine gömülmesine karşı da hem
`X-Frame-Options` hem CSP `frame-ancestors` var: CMS için `none` (hiçbir yer onu
gömmemeli), site için `self`.

Bilerek **`script-src` politikası yok.** Site Kodları, yöneticinin sayfalara Google
Analytics, Tag Manager, Meta Pixel ya da çerez onay bandı eklemesi için var. İşe
yarayacak kadar sıkı bir script politikası, yapıştırılan ilk kodda bunu bozardı.
Doğrusu, yöneticinin izin verdiği kaynakları kendisinin tanımlaması. Bu bir başlık
değil, bir özellik; henüz yapılmadı.

**Girişte iki ayrı koruma var**, çünkü iki saldırı farklı:

| Katman | Neye karşı |
| --- | --- |
| Hesap kilitleme: 5 hatalı denemeden sonra 15 dakika (Identity) | Tek bir hesabın şifresini tahmin etmeye; denemeler nereden gelirse gelsin |
| Hız sınırı: IP başına 5 dakikada 20 deneme (`RateLimiting:LoginPermitLimit`) | Tek bir şifreyi birçok hesapta denemeye; hesap başına sayaç bunu görmez |

Kilitlenen hesaba kilitlendiği söylenir. Ona tekrar "şifre yanlış" deyip boşuna
denemeye devam ettirilmez. Bir reverse proxy arkasındaysanız
`RateLimiting:TrustForwardedForHeader` değerini `true` yapın. Ama **sadece** o
durumda: proxy yoksa o başlığı saldırgan istediği gibi yazar ve ona güvenmek hız
sınırını açık görünürken fiilen kapatır.

Herhangi bir reverse proxy arkasında (nginx, IIS'in ARR'si, yük dengeleyici)
ayrıca `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` verin. Verilmezse uygulama
`X-Forwarded-*` başlıklarındaki asıl şemayı ve adresi görmez, her isteği düz HTTP
sanar. Güvenli çerezler, `IsHttps` kontrolleri ve HTTPS'e yönlendiren her şey
sessizce yanlış çalışır. Docker Compose dosyası bunu zaten veriyor; Yol B ya da C
için kendiniz ekleyin.

Sitede form gönderme, arama, ziyaret istatistiği ve tarayıcı hata bildirimleri için
ayrı sınırlar var. Sayfa açma bilerek sınırsız: ani bir trafik artışı gerçek
ziyaretçilere 429 hatası olarak dönmesin.

## Ayarlar

Her ayar yapılandırmadan okunur; kodda sabit bir değer yok. ASP.NET Core ayarları
şu sırayla okur, sonraki öncekini ezer: `appsettings.json` →
`appsettings.{Ortam}.json` → user secrets (sadece Development) → ortam
değişkenleri.

Ortam değişkenlerinde JSON yolundaki `:` yerine `__` yazılır: `Jwt:SecretKey`,
`Jwt__SecretKey` olur. `docker-compose.yml` de bunu kullanır.

### Zorunlu

| Anahtar | Hangi uygulama | Not |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | ikisi de | İki uygulama aynı veritabanını kullanır. |
| `Jwt:SecretKey` | CMS | 32+ karakter; daha kısaysa uygulama açılmaz. |
| `Jwt:Issuer`, `Jwt:Audience` | CMS | |
| `Preview:SigningKey` | ikisi de | İkisinde birebir aynı olmalı. |
| `Cache:ClearSecret` | ikisi de | İkisinde birebir aynı olmalı. Boş gelir, kendiniz verin. |
| `Seed:SuperAdmin:UserName` / `:Email` / `:Password` | CMS | Sadece ilk açılışta kullanılır. Üçü birden ya da hiçbiri. |

### İsteğe bağlı

| Anahtar | Hangi uygulama | Ne yapar |
| --- | --- | --- |
| `Seed:Editor:*` | CMS | `Seed:SuperAdmin` ile aynı yapıda; ikinci bir hesap oluşturur. |
| `DataProtection:KeyPath` | CMS | Şifreleme anahtarlarının klasörü. Verilmezse framework'ün varsayılanı kullanılır; konteyner dışında sorun olmaz. |
| `Email:*` | ikisi de | SMTP sunucusu, port, kullanıcı bilgisi, gönderen. Verilmezse CMS form cevabı gönderemez. CMS'te **Site Ayarları → Sırlar** ekranından da girilebilir (`SecretsManage` yetkisi); orada değer varsa bu ayarın yerine o kullanılır. Bkz. [Entegrasyon sırları](#entegrasyon-sırları). |
| `FileStorage:*` | ikisi de | Yükleme klasörü, 50 MB boyut sınırı, izin verilen dosya türleri. |
| `ObjectStorage:*` | CMS | `Local` (varsayılan) ya da S3 uyumlu bir depolama. `Email:*` gibi Sırlar ekranından da verilebilir ve oradaki değer önce gelir. |
| `Backup:*` | CMS | Klasör, kaç yedek tutulacağı, zaman aşımı. Klasör boşsa uygulamanın yanındaki bir klasör kullanılır. |
| `Retention:*` | CMS | Ziyaret istatistiklerinin ve logların ne kadar tutulacağı (varsayılan ikisi de 90 gün) ve Çöp Kutusu'nun silinenleri ne kadar tutacağı (`TrashDays`, 30; `0` hiç silmez). |
| `Redis:ConnectionString` | ikisi de | Sitede Redis önbelleği; CMS'te ayrıca yönetici oturumlarının yeniden başlatmadan sonra da kalmasını sağlar. **CMS'i sitenin kullandığından farklı bir Redis veritabanına bağlayın** (örneğin sonuna `,defaultDatabase=1` ekleyin). Site önbelleğini temizlemek tüm veritabanında `FLUSHDB` çalıştırır; ikisi aynı veritabanını kullanıyorsa bu, tüm yöneticilerin oturumunu da siler. Docker Compose dosyası bunu zaten ayarlıyor. |
| `Captcha:SecretKey` | site | Site Ayarları'nda seçilen CAPTCHA sağlayıcısıyla birlikte çalışır. Sırlar ekranından da verilebilir ve oradaki değer önce gelir. O değeri çözmek için CMS'in şifreleme anahtarları gerekir; bu yüzden `docker-compose.yml`, `cms-keys` volume'ünü `web` konteynerine de salt okunur bağlar. |
| `RateLimiting:*` | site | Sitedeki uç noktaların hız sınırları. |
| `Serilog:*` | ikisi de | Log seviyeleri ve logların nereye yazılacağı. Konsola yazma her zaman açık; `docker compose logs` bunu okur. |
| `Serilog:SeqUrl`, `Serilog:SeqApiKey` | ikisi de | İsteğe bağlı. Boşsa (varsayılan) loglar sadece konsola gider. Bir Seq adresi verilirse açılışta ikinci bir hedef eklenir ve iki uygulama da logları oraya gönderir. Bu hedef **sadece** adres verildiğinde eklenir; çünkü hiçbir yere gitmeyen bir Seq adresi sessizce başarısız olur: loglar biriktirilir, atılır ve her şey ayarlıymış gibi görünür. `docker compose -f docker-compose.yml -f docker-compose.dev-tools.yml up -d` yerel bir Seq başlatır ve iki uygulamayı ona bağlar; arayüzü <http://localhost:5341> adresinde. |

Kuruluma değil *siteye* ait ayarlar (site adı, logo, SEO varsayılanları, analiz
kodları, bakım modu, önbellek türü, CDN adresi) veritabanında durur ve CMS'te
**Site Ayarları**, **Site Kodları** ve **SEO** ekranlarından düzenlenir;
`appsettings.json`'dan değil.

#### Entegrasyon sırları

Bu ikisinin arasında üçüncü bir tür var: CMS dışındaki sistemlerin gerçek giriş
bilgileri (SMTP şifresi, CAPTCHA gizli anahtarı, S3/CDN erişim anahtarları).
Bunlar iki yoldan verilebilir: yukarıdaki gibi `appsettings.json`/ortam
değişkenleriyle ya da CMS'te **Site Ayarları → Sırlar** ekranından (`SecretsManage`
yetkisi gerekir; varsayılan olarak SuperAdmin'de var). Hangisinde değer varsa o
kullanılır; ikisinde de varsa veritabanındaki kazanır.

Normal Site Ayarları'nın aksine bunlar asla düz metin olarak saklanmaz. CMS'in
oturumlar için zaten kullandığı ASP.NET Data Protection anahtarlarıyla şifrelenir.
Kaydedilen değer bir daha ekranda gösterilmez, sadece yenisiyle değiştirilebilir.
Dosya/ortam değişkeni yolunun aksine bu ekrandan girilen değer hemen geçerli olur,
yeniden başlatma gerekmez; `ObjectStorage:Provider`'ı yerel disk ile S3 arasında
değiştirmek dâhil. CAPTCHA gizli anahtarını kaydettiğinizde CMS siteye iç ağ
üzerinden haber verir (önbellek temizlemenin kullandığı aynı `Cache:ClearSecret`
kanalıyla); site de bir iki saniye içinde yeni değeri alır. Bu haber ulaşmazsa
(örneğin site o an kapalıysa) CMS bunu söyler; site geri gelince tekrar kaydedersiniz.

---

## Derleme ve test

```bash
dotnet build Elevare.sln
```

```bash
dotnet test Elevare.sln
```

Testler komut ve sorgu işleyicilerini, HTML temizleyiciyi, önizleme linki
imzalayıcısını kapsar. Ayrıca CMS'in veritabanı yapısıyla sitenin okuduğu
modellerin birbirinden kopmadığını denetleyen sözleşme testleri var.

Derleme `TreatWarningsAsErrors` ile çalışır, yani her uyarı hatadır. Projenin
bilerek kapattığı analiz kuralları kökteki `.editorconfig` dosyasında.

### Migration'lar

CMS açılırken bekleyen migration'ları kendisi uygular; günlük işte bir şey
çalıştırmanız gerekmez. Yeni bir tane eklemek için:

```bash
dotnet ef migrations add <Ad> --project src/cms/Infrastructure/Persistence --startup-project src/cms/Presentation/Wasm/Wasm
```

Üretilen dosyalar olduğu gibi commit'lenebilir: migration klasörünün kendi
`.editorconfig` dosyası EF'in ürettiği kod için stil kurallarını gevşetiyor.

---

## Notlar

- **Redis varsa yönetici oturumları yeniden başlatmadan sonra da kalır.** JWT
  oturumun içinde durur; oturumun nerede tutulduğu, yeniden başlatmada ne olacağını
  belirler. `Redis:ConnectionString` verirseniz (Docker kurulumu verir) oturumlar
  konteynerden uzun yaşar. Vermezseniz bellekte tutulur; tek bir sunucuda, nadiren
  yeniden başlatılan bir kurulumda bu da yeterlidir. Her iki durumda da
  `DataProtection:KeyPath` çerezin yeniden oluşturmadan sonra da çözülebilmesini
  sağlar; kaybolan oturum, açık sayfalarda şifreleme hataları yerine temiz bir
  şekilde giriş sayfasına yönlendirir. CMS'e ayrı bir Redis veritabanı numarası
  verin (Docker kurulumundaki `,defaultDatabase=1` gibi). Sitenin önbellek
  temizlemesi tüm veritabanında `FLUSHDB` çalıştırır; aynı veritabanını
  paylaşırlarsa biri önbelleği temizlediği anda tüm yöneticilerin oturumu düşer.
- **Site, CMS'in kendisinden önde olmasını tolere eder.** Site geçici veritabanı
  hatalarında tekrar dener. İlk açılışta tablolar kurulurken art arda 500 hatası
  vermek yerine bekler.
- **`/health`** iki uygulamada da giriş gerektirmeden `200 Healthy` döner. Site
  bakım modundayken de `200` döner: bakım, sitenin durumuyla ilgilidir, uygulamanın
  çalışıp çalışmadığıyla değil.

---

## Katkıda bulunma

Hata bildirimleri, fikirler ve pull request'ler memnuniyetle karşılanır. Bu projeye
özgü kurallar için [CONTRIBUTING.tr.md](CONTRIBUTING.tr.md) dosyasına bakın. Bir
güvenlik açığı bulduysanız [SECURITY.tr.md](SECURITY.tr.md) dosyasına bakın (lütfen
bunlar için herkese açık issue açmayın).

## Lisans

[MIT](LICENSE) © Oğuzhan Karagüzel
