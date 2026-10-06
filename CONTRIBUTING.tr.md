# Katkıda bulunma

[English](CONTRIBUTING.md) · **Türkçe**

Projeyle ilgilendiğiniz için teşekkürler. Bu belge Elevare'ye özgü kuralları
anlatıyor: bazılarını derleme hataları size er geç öğretir, bazılarını ise hiçbir
derleyici denetlemez.

Sistemin genel yapısını öğrenmek için
[docs/ARCHITECTURE.tr.md](docs/ARCHITECTURE.tr.md) dosyasını okuyun.

## Projeyi çalıştırma

[README](README.tr.md) dosyasındaki yöntemlerin hepsi işinizi görür. Docker daha
kısa yoldur ve Docker'dan başka bir şey gerektirmez. `dotnet run` için .NET 9 SDK ve
erişebildiğiniz bir PostgreSQL sunucusu gerekir.

Yerel ayarlar her uygulamanın yanındaki `appsettings.Development.json` dosyasında
tutulur. Bu dosyalar git'e eklenmez; yanlarındaki `.example` dosyasını kopyalayıp
kendi değerlerinizi girin. Yaptığınız değişiklik yeni bir ayar gerektiriyorsa o
ayarı `.example` dosyasına da ekleyin; aksi hâlde projeyi sonradan indiren kişi
böyle bir ayarın varlığından haberdar olmaz.

```bash
dotnet build Elevare.sln
```

```bash
dotnet test Elevare.sln
```

Pull request açmadan önce ikisinin de başarılı olması gerekir. CI tam olarak bu iki
komutu çalıştırır; bunlara ek olarak iki Docker imajını derler ve bilinen güvenlik
açığı bulunan bağımlılıkları tarar.

## Derlemeyi bozacak durumlar

**Uyarılar hata olarak kabul edilir.** `TreatWarningsAsErrors` açıktır; Sonar, .NET
analiz kuralları ve IDE stil kuralları devrededir. Projede bilerek kapatılan
kurallar, kök dizindeki `.editorconfig` dosyasında gerekçesini açıklayan bir yorumla
birlikte kapatılmıştır. Bir kuralı satır içinde susturmak istediğinizde önce kodun
size bir şey anlatmaya çalışıp çalışmadığını düşünün.

**.NET 9 SDK ile derleyin.** Projeler `net9.0` hedefler. Daha yeni bir preview SDK,
.NET 9 derleyicisinin reddedeceği bazı kodları sorunsuz derler (kullanılmayan
`async` ifadeleri, bazı Razor yapıları gibi); hata da ancak CI'da ya da Docker
derlemesinde ortaya çıkar. Bilgisayarınızda preview SDK kuruluysa kontrol etmenin
en hızlı yolu Docker derlemesidir.

**EF migration'ları üretildiği hâliyle commit'lenir.** Migration klasöründeki kendi
`.editorconfig` dosyası, EF'in ürettiği kod için stil kurallarını gevşetir; bu
yüzden elle düzeltmeniz gereken bir şey yoktur:

```bash
dotnet ef migrations add YeniMigrationAdi --project src/cms/Infrastructure/Persistence --startup-project src/cms/Presentation/Wasm/Wasm
```

Derleme yine de üretilmiş bir `new Guid("00000000-…")` varsayılan değerine takılırsa
(S4581), bunu `Guid.Empty` ile değiştirin. Modelin ifade edemediği bir şey gerektiren
migration'lar (bir PostgreSQL eklentisi, fonksiyon tabanlı bir index gibi)
`migrationBuilder.Sql(...)` ile yazılır ve gerekçesini açıklayan bir yorum içerir.

## Derleyicinin denetlemediği kurallar

**Handler'lar exception fırlatmaz, `Result` döner.** Başarısız bir sonuç
`Result.Failure(SomethingErrors.Reason)` şeklinde döndürülür; hata sabiti de ilgili
olduğu varlığın yanında tanımlanır. Çağrıldığı yerde yeni bir `Error` oluşturmayın:
hata kodu aynı zamanda çeviri anahtarıdır ve yerinde uydurulmuş bir kodun karşılığı
olan bir mesaj bulunmaz.

**Her hata kodunun iki dilde de mesajı olmalıdır.** Mesajı hem `ErrorMessages.resx`
*hem de* `ErrorMessages.tr.resx` dosyasına ekleyin. Unutursanız
`ErrorMessageCoverageTests` derlemeyi başarısız kılar. Bunun sebebi, mesaj
bulunamadığında İngilizce açıklamanın gösterilmesidir; bunu görecek tek kişi de
yeni yazdığınız hata durumuyla karşılaşan Türkçe kullanıcıdır.

Arayüz metinleri için de aynı kural geçerlidir: `CmsMessages.resx` ve
`CmsMessages.tr.resx`.

**Kod ve yorumlar İngilizce yazılır, arayüz iki dillidir.** Commit mesajları iki
dilden biriyle yazılabilir.

**Yorumlar kodun ne yaptığını değil, neden öyle yaptığını açıklar.** Kodda oldukça
fazla yorum vardır, ama neredeyse hepsi kodun kendisinin cevaplayamayacağı bir
soruyu cevaplar: neden bu sırayla yapılıyor, neden yanlış görünse de aslında doğru,
daha önce neyin bozulduğu gibi. Üstündeki satırı tekrar eden bir yorum, kod
incelemesinde sorgulanır.

## Testler

Yeni bir davranış, testiyle birlikte gelmelidir. Testler `tests/cms` ve `tests/web`
klasörlerindedir ve EF'in in-memory sağlayıcısı üzerinde gerçek `DbContext` ile
çalışır. Böylece bir handler testi elle yazılmış bir sahte nesneyi değil, canlıda
kullanılan gerçek eşlemeleri ve sorgu filtrelerini sınar.

In-memory sağlayıcının desteklemediği iki şey var; ikisi de bu projede tecrübeyle
öğrenildi:

- Unique index'leri ve filtreli index'leri yok sayar.
- `ExecuteUpdate` / `ExecuteDelete` desteklemez. Test edilebilir olması gereken kod
  bunun yerine change tracker üzerinden çalışmalıdır.

Test adlarını cümle gibi yazın: `A_deleted_translation_can_be_created_again`. Testin
neden yazıldığı açık değilse, hangi gerçek hatayı önlemek için yazıldığını bir
yorumla belirtin.

## Pull request'ler

Küçük ve tek bir konuya odaklanan pull request'ler, büyük ve her şeyi kapsayanlardan
daha iyidir. Neyi değiştirdiğinizi, neden değiştirdiğinizi ve nasıl
doğruladığınızı yazın. "Derleme başarılı" bir doğrulama sayılmaz, çünkü bunu CI
zaten kontrol ediyor.

Yapısal bir değişiklik planlıyorsanız önce bir issue açın. Bir yaklaşım üzerinde baştan
anlaşmak, tamamlanmış bir branch üzerinde anlaşmazlığa düşmekten çok daha az zahmetlidir.

## Güvenlik

Lütfen güvenlik açıkları için herkese açık bir issue açmayın. Gizli bildirim yolu
[SECURITY.tr.md](SECURITY.tr.md) dosyasında anlatılıyor.
