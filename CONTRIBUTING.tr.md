# Katkıda bulunma

[English](CONTRIBUTING.md) · **Türkçe**

İlginiz için teşekkürler. Bu dosya Elevare'ye özgü şeyleri anlatıyor: bazılarını
derleme hatası size zor yoldan öğretir, bazılarını ise hiçbir derleyici denetlemez.

Sistemin nasıl kurulduğu için [docs/ARCHITECTURE.tr.md](docs/ARCHITECTURE.tr.md)
dosyasını okuyun.

## Çalıştırmak

[README](README.tr.md) dosyasındaki iki yol da olur. Docker daha kısa ve Docker'ın
kendisinden başka bir şey istemez. `dotnet run` için .NET 9 SDK ve erişebildiğiniz
bir PostgreSQL gerekir.

Yerel ayarlar her uygulamanın yanındaki `appsettings.Development.json` dosyasında
durur. Bu dosyalar git'e girmez; yanlarındaki `.example` dosyasını kopyalayıp kendi
değerlerinizi yazın. Değişikliğiniz yeni bir ayar gerektiriyorsa onu `.example`
dosyasına da ekleyin. Yoksa projeyi sonra indiren kişi o ayarın varlığından
haberdar olmaz.

```bash
dotnet build Elevare.sln
```

```bash
dotnet test Elevare.sln
```

Pull request açmadan önce ikisi de geçmeli. CI tam olarak bunları çalıştırır; ayrıca
iki Docker imajını derler ve bilinen güvenlik açığı olan bağımlılıkları tarar.

## Derlemeyi düşürecek şeyler

**Uyarılar hata sayılır.** `TreatWarningsAsErrors` açık; Sonar, .NET analiz
kuralları ve IDE stil kuralları devrede. Projenin bilerek kapattığı kurallar kökteki
`.editorconfig` dosyasında, nedenini anlatan bir yorumla birlikte kapatılmış. Bir
kuralı satır içinde susturmak istiyorsanız önce kodun size bir şey söyleyip
söylemediğini düşünün.

**.NET 9 SDK ile derleyin.** Projeler `net9.0` hedefliyor. Daha yeni bir preview
SDK, .NET 9 derleyicisinin reddedeceği bazı şeyleri sorunsuz derler (kullanılmayan
`async`, bazı Razor yapıları). Hata da ancak CI'da ya da Docker derlemesinde ortaya
çıkar. Bilgisayarınızda preview SDK varsa kontrol etmenin en hızlı yolu Docker
derlemesi.

**EF migration'ları üretildiği gibi commit'lenir.** Migration klasörünün kendi
`.editorconfig` dosyası var ve EF'in ürettiği kod için stil kurallarını gevşetiyor;
elle çevrilecek bir şey yok:

```bash
dotnet ef migrations add YeniMigrationAdi --project src/cms/Infrastructure/Persistence --startup-project src/cms/Presentation/Wasm/Wasm
```

Derleme yine de üretilmiş bir `new Guid("00000000-…")` varsayılanına takılırsa
(S4581), onu `Guid.Empty` ile değiştirin. Modelin ifade edemediği bir şey gereken
migration (bir eklenti, fonksiyonlu bir indeks) `migrationBuilder.Sql(...)` ile
yazılır ve nedenini anlatan bir yorum taşır.

## Derleyicinin denetlemediği kurallar

**Handler'lar exception fırlatmaz, `Result` döner.** Başarısızlık
`Result.Failure(SomethingErrors.Reason)` şeklinde döner; hata sabiti de anlattığı
varlığın yanında durur. Çağrıldığı yerde yeni bir `Error` oluşturmayın: hata kodu
aynı zamanda çeviri anahtarıdır ve yerinde uydurulmuş bir kodun mesajı olmaz.

**Her hata kodunun iki dilde de mesajı olmalı.** Hem `ErrorMessages.resx` hem
`ErrorMessages.tr.resx` dosyasına ekleyin. Unutursanız `ErrorMessageCoverageTests`
derlemeyi düşürür. Çünkü yedek mesaj İngilizce açıklamadır ve onu görecek tek kişi,
yeni hata yolunuza düşen Türk kullanıcıdır.

Arayüz metinleri için de aynısı geçerli: `CmsMessages.resx` / `CmsMessages.tr.resx`.

**Kod ve yorumlar İngilizce, arayüz iki dilli.** Commit mesajları iki dilde de
olabilir.

**Yorumlar ne yapıldığını değil, neden yapıldığını anlatır.** Kodda epey yorum var,
ama neredeyse hepsi kodun cevaplayamayacağı bir soruyu cevaplar: neden bu sıra,
neden yanlış görünüp aslında doğru, geçen sefer ne bozulmuştu. Üstündeki satırı
tekrar eden bir yorum incelemede sorulur.

## Testler

Yeni bir davranış testiyle gelir. Testler `tests/cms` ve `tests/web` altında. EF'in
in-memory sağlayıcısı üzerinde gerçek `DbContext` ile çalışırlar; yani bir handler
testi elle yazılmış bir taklidi değil, gerçek eşlemeyi ve sorgu filtrelerini
çalıştırır.

In-memory sağlayıcının yapamadığı iki şey var; ikisi de burada zor yoldan öğrenildi:

- Unique index'leri ve filtreli index'leri yok sayar.
- `ExecuteUpdate` / `ExecuteDelete` desteklemez. Test edilebilmesi gereken kod
  bunun yerine change tracker üzerinden gitmeli.

Testlere cümle gibi ad verin: `A_deleted_translation_can_be_created_again`. Nedeni
açık değilse, testin hangi gerçek hatayı önlemek için yazıldığını bir yorumla
söyleyin.

## Pull request'ler

Küçük ve tek konuya odaklı olan, büyük ve her şeyi kapsayandan iyidir. Neyi
değiştirdiğinizi, neden değiştirdiğinizi ve nasıl doğruladığınızı yazın. "Derleme
geçiyor" doğrulama sayılmaz; onu CI zaten yapıyor.

Yapısal bir şey planlıyorsanız önce bir issue açın. Bir yaklaşım üzerinde
anlaşamamak, bitmiş bir branch üzerinde anlaşamamaktan çok daha ucuzdur.

## Güvenlik

Lütfen bir güvenlik açığı için herkese açık issue açmayın. Gizli bildirim yolu
[SECURITY.tr.md](SECURITY.tr.md) dosyasında.
