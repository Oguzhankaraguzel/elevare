namespace Domain.Entities.StructuredData;

/// <summary>
/// The schema.org types the builder offers, and the properties it offers for each.
/// <para>
/// Deliberately a curated subset, not a mirror of schema.org. The full vocabulary
/// is thousands of types deep and changes continuously; shipping all of it would
/// bury the handful an editor actually needs and would rot the moment schema.org
/// moved. Anything not listed here is still reachable — the builder can add a node
/// of any type and any property through the freeform key/value editor, so this
/// catalogue speeds up the common case without becoming a ceiling.
/// </para>
/// <para>
/// Property choices follow Google's own "recommended properties" guidance rather
/// than schema.org's full definitions, because the recommended set is what
/// actually affects how a page is understood.
/// </para>
/// </summary>
public static class SchemaCatalog
{
    // ── Reusable property groups ──────────────────────────────────────────────

    /// <summary>
    /// Author/publisher accept either a person or an organisation, and the choice
    /// is meaningful: a byline by a named journalist and one by a newsroom are
    /// different claims about who stands behind the content.
    /// </summary>
    private static readonly string[] PersonOrOrganization = ["Person", "Organization"];

    private static readonly SchemaProperty[] PostalAddressProperties =
    [
        new("streetAddress", "Sokak/Cadde", SchemaPropertyKind.Text, Recommended: true),
        new("addressLocality", "İlçe/Şehir", SchemaPropertyKind.Text, Recommended: true),
        new("addressRegion", "İl/Bölge"),
        new("postalCode", "Posta Kodu"),
        new("addressCountry", "Ülke Kodu", SchemaPropertyKind.Text, Recommended: true, Hint: "İki harfli ISO kodu, örn. TR"),
    ];

    // ── Site-level types ──────────────────────────────────────────────────────

    public static readonly SchemaTemplate Organization = new(
        "Organization",
        "Kuruluş",
        "Siteyi işleten kurum. Sosyal profilleri ve iletişim bilgilerini arama motorlarına bağlar.",
        [
            new("name", "Ad", SchemaPropertyKind.Text, Recommended: true),
            new("url", "Web Sitesi", SchemaPropertyKind.Url, Recommended: true),
            new("logo", "Logo", SchemaPropertyKind.Url, Recommended: true),
            new("description", "Açıklama", SchemaPropertyKind.LongText),
            new("telephone", "Telefon"),
            new("email", "E-posta"),
            new("address", "Adres", SchemaPropertyKind.Entity, AcceptedTypes: ["PostalAddress"]),
            new("sameAs", "Sosyal Profiller", SchemaPropertyKind.TextList, Recommended: true,
                Hint: "Her satıra bir profil adresi. Arama motorlarının bu hesapların size ait olduğunu anlamasını sağlar."),
        ],
        IdFragment: "#organization");

    public static readonly SchemaTemplate WebSite = new(
        "WebSite",
        "Web Sitesi",
        "Sitenin kendisi. Site içi aramayı arama sonuçlarına taşıyabilir.",
        [
            new("name", "Site Adı", SchemaPropertyKind.Text, Recommended: true),
            new("url", "Adres", SchemaPropertyKind.Url, Recommended: true),
            new("inLanguage", "Dil", SchemaPropertyKind.Text),
            new("publisher", "Yayıncı", SchemaPropertyKind.Entity, AcceptedTypes: PersonOrOrganization),
        ],
        IdFragment: "#website");

    public static readonly SchemaTemplate WebPage = new(
        "WebPage",
        "Sayfa",
        "Bu sayfanın kendisi. Diğer düğümlerin bağlandığı çapa.",
        [
            new("name", "Başlık", SchemaPropertyKind.Text, Recommended: true),
            new("url", "Adres", SchemaPropertyKind.Url, Recommended: true),
            new("description", "Açıklama", SchemaPropertyKind.LongText, Recommended: true),
            new("inLanguage", "Dil"),
            new("datePublished", "Yayın Tarihi", SchemaPropertyKind.DateTime),
            new("dateModified", "Güncelleme Tarihi", SchemaPropertyKind.DateTime),
            new("primaryImageOfPage", "Öne Çıkan Görsel", SchemaPropertyKind.Url),
        ],
        IdFragment: "#webpage");

    public static readonly SchemaTemplate BreadcrumbList = new(
        "BreadcrumbList",
        "Sayfa Yolu",
        "Sayfanın site hiyerarşisindeki yeri. Üst sayfa zincirinden otomatik doldurulur.",
        [
            new("itemListElement", "Halkalar", SchemaPropertyKind.EntityList, AcceptedTypes: ["ListItem"]),
        ],
        IdFragment: "#breadcrumb");

    // ── Content types ─────────────────────────────────────────────────────────

    private static SchemaProperty[] ArticleProperties() =>
    [
        new("headline", "Başlık", SchemaPropertyKind.Text, Recommended: true,
            Hint: "Kısa tutun — uzun başlıklar arama sonucunda kesilir."),
        new("description", "Özet", SchemaPropertyKind.LongText, Recommended: true),
        new("image", "Görsel", SchemaPropertyKind.TextList, Recommended: true,
            Hint: "Her satıra bir görsel. Google 16:9, 4:3 ve 1:1 oranlarında birer sürüm önerir."),
        new("author", "Yazar", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: PersonOrOrganization,
            Hint: "Birden fazla yazar varsa her biri ayrı düğüm olmalı, tek alanda birleştirilmemeli."),
        new("publisher", "Yayıncı", SchemaPropertyKind.Entity, AcceptedTypes: PersonOrOrganization),
        new("datePublished", "Yayın Tarihi", SchemaPropertyKind.DateTime, Recommended: true),
        new("dateModified", "Güncelleme Tarihi", SchemaPropertyKind.DateTime, Recommended: true),
        new("articleSection", "Bölüm/Kategori"),
        new("keywords", "Anahtar Kelimeler", SchemaPropertyKind.TextList),
        new("inLanguage", "Dil"),
    ];

    public static readonly SchemaTemplate Article = new(
        "Article", "Makale", "Genel amaçlı yazı içeriği.", ArticleProperties(), IdFragment: "#article");

    public static readonly SchemaTemplate BlogPosting = new(
        "BlogPosting", "Blog Yazısı", "Blog akışına ait yazı.", ArticleProperties(), IdFragment: "#article");

    public static readonly SchemaTemplate NewsArticle = new(
        "NewsArticle", "Haber", "Gazetecilik ürünü haber içeriği.", ArticleProperties(), IdFragment: "#article");

    public static readonly SchemaTemplate FaqPage = new(
        "FAQPage",
        "Sıkça Sorulan Sorular",
        "Soru-cevap listesi. Sayfada FAQ bloğu varsa sorular otomatik doldurulur.",
        [
            new("mainEntity", "Sorular", SchemaPropertyKind.EntityList, Recommended: true, AcceptedTypes: ["Question"]),
        ],
        IdFragment: "#faq",
        Note: "Google, FAQ zengin sonuçlarını 7 Mayıs 2026'da tamamen kaldırdı. İşaretleme hâlâ geçerli ve "
            + "Google sayfayı anlamak için okumaya devam ediyor, ancak arama sonucunda açılır soru listesi artık görünmüyor.");

    public static readonly SchemaTemplate Question = new(
        "Question",
        "Soru",
        "Tek bir soru ve kabul edilen cevabı.",
        [
            new("name", "Soru", SchemaPropertyKind.Text, Recommended: true),
            new("acceptedAnswer", "Cevap", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: ["Answer"]),
        ]);

    public static readonly SchemaTemplate Answer = new(
        "Answer", "Cevap", "Bir sorunun cevabı.",
        [new("text", "Metin", SchemaPropertyKind.LongText, Recommended: true)]);

    public static readonly SchemaTemplate ListItem = new(
        "ListItem", "Liste Öğesi", "Sıralı listede tek bir halka.",
        [
            new("position", "Sıra", SchemaPropertyKind.Number, Recommended: true),
            new("name", "Ad", SchemaPropertyKind.Text, Recommended: true),
            new("item", "Adres", SchemaPropertyKind.Url),
        ]);

    public static readonly SchemaTemplate LocalBusiness = new(
        "LocalBusiness",
        "Yerel İşletme",
        "Fiziksel adresi olan işletme. Harita ve yerel aramalarda kullanılır.",
        [
            new("name", "İşletme Adı", SchemaPropertyKind.Text, Recommended: true),
            new("image", "Görsel", SchemaPropertyKind.Url, Recommended: true),
            new("url", "Web Sitesi", SchemaPropertyKind.Url),
            new("telephone", "Telefon", SchemaPropertyKind.Text, Recommended: true),
            new("email", "E-posta"),
            new("address", "Adres", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: ["PostalAddress"]),
            new("openingHours", "Çalışma Saatleri", SchemaPropertyKind.TextList,
                Hint: "Her satıra bir aralık, örn. Mo-Fr 09:00-18:00"),
            new("priceRange", "Fiyat Aralığı", SchemaPropertyKind.Text, Hint: "Örn. ₺₺ veya 100₺-500₺"),
            new("geo", "Koordinat", SchemaPropertyKind.Entity, AcceptedTypes: ["GeoCoordinates"]),
        ],
        IdFragment: "#localbusiness");

    public static readonly SchemaTemplate PostalAddress = new(
        "PostalAddress", "Posta Adresi", "Yapılandırılmış adres.", PostalAddressProperties);

    public static readonly SchemaTemplate ContactPoint = new(
        "ContactPoint", "İletişim Noktası", "Bir kuruluşa nasıl ulaşılacağı.",
        [
            new("contactType", "İletişim Türü", SchemaPropertyKind.Text, Recommended: true,
                Hint: "Örn. customer service, technical support, billing support."),
            new("telephone", "Telefon", SchemaPropertyKind.Text, Recommended: true),
            new("email", "E-posta", SchemaPropertyKind.Text),
            new("areaServed", "Hizmet Bölgesi", SchemaPropertyKind.Text,
                Hint: "Ülke kodu ya da bölge adı, örn. TR."),
            new("availableLanguage", "Diller", SchemaPropertyKind.TextList),
        ]);

    public static readonly SchemaTemplate GeoCoordinates = new(
        "GeoCoordinates", "Koordinat", "Enlem ve boylam.",
        [
            new("latitude", "Enlem", SchemaPropertyKind.Number, Recommended: true),
            new("longitude", "Boylam", SchemaPropertyKind.Number, Recommended: true),
        ]);

    public static readonly SchemaTemplate Person = new(
        "Person",
        "Kişi",
        "Bir insan — yazar, kurucu, ekip üyesi.",
        [
            new("name", "Ad Soyad", SchemaPropertyKind.Text, Recommended: true),
            new("url", "Profil Adresi", SchemaPropertyKind.Url, Recommended: true,
                Hint: "Bu kişiyi benzersiz tanımlayan bir sayfa."),
            new("image", "Fotoğraf", SchemaPropertyKind.Url),
            new("jobTitle", "Unvan"),
            new("description", "Kısa Biyografi", SchemaPropertyKind.LongText),
            new("email", "E-posta"),
            new("worksFor", "Çalıştığı Kurum", SchemaPropertyKind.Entity, AcceptedTypes: ["Organization"]),
            new("sameAs", "Sosyal Profiller", SchemaPropertyKind.TextList),
        ],
        IdFragment: "#person");

    public static readonly SchemaTemplate Event = new(
        "Event",
        "Etkinlik",
        "Belirli tarihte gerçekleşen etkinlik.",
        [
            new("name", "Etkinlik Adı", SchemaPropertyKind.Text, Recommended: true),
            new("startDate", "Başlangıç", SchemaPropertyKind.DateTime, Recommended: true),
            new("endDate", "Bitiş", SchemaPropertyKind.DateTime),
            new("eventAttendanceMode", "Katılım Şekli", SchemaPropertyKind.Text,
                Hint: "https://schema.org/OfflineEventAttendanceMode | OnlineEventAttendanceMode | MixedEventAttendanceMode"),
            new("eventStatus", "Durum", SchemaPropertyKind.Text,
                Hint: "https://schema.org/EventScheduled | EventCancelled | EventPostponed"),
            new("location", "Yer", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: ["Place", "VirtualLocation"]),
            new("image", "Görsel", SchemaPropertyKind.TextList),
            new("description", "Açıklama", SchemaPropertyKind.LongText),
            new("organizer", "Düzenleyen", SchemaPropertyKind.Entity, AcceptedTypes: PersonOrOrganization),
            new("offers", "Bilet", SchemaPropertyKind.Entity, AcceptedTypes: ["Offer"]),
        ],
        IdFragment: "#event");

    public static readonly SchemaTemplate Place = new(
        "Place", "Yer", "Fiziksel konum.",
        [
            new("name", "Ad", SchemaPropertyKind.Text, Recommended: true),
            new("address", "Adres", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: ["PostalAddress"]),
        ]);

    public static readonly SchemaTemplate Offer = new(
        "Offer", "Teklif/Fiyat", "Bir ürün veya biletin satış bilgisi.",
        [
            new("price", "Fiyat", SchemaPropertyKind.Number, Recommended: true),
            new("priceCurrency", "Para Birimi", SchemaPropertyKind.Text, Recommended: true, Hint: "ISO kodu, örn. TRY"),
            new("availability", "Stok Durumu", SchemaPropertyKind.Text,
                Hint: "https://schema.org/InStock | OutOfStock | PreOrder"),
            new("url", "Satın Alma Adresi", SchemaPropertyKind.Url),
            new("validFrom", "Geçerlilik Başlangıcı", SchemaPropertyKind.DateTime),
        ]);

    public static readonly SchemaTemplate Product = new(
        "Product",
        "Ürün",
        "Satılan bir ürün. Fiyat ve stok bilgisiyle arama sonuçlarında görünebilir.",
        [
            new("name", "Ürün Adı", SchemaPropertyKind.Text, Recommended: true),
            new("image", "Görseller", SchemaPropertyKind.TextList, Recommended: true),
            new("description", "Açıklama", SchemaPropertyKind.LongText, Recommended: true),
            new("sku", "Stok Kodu"),
            new("brand", "Marka", SchemaPropertyKind.Entity, AcceptedTypes: ["Brand", "Organization"]),
            new("offers", "Fiyat/Stok", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: ["Offer"]),
            new("aggregateRating", "Puan Özeti", SchemaPropertyKind.Entity, AcceptedTypes: ["AggregateRating"]),
        ],
        IdFragment: "#product");

    /// <summary>
    /// An image with its size — what the draft writes for a logo and a page's
    /// images, since Google reads the dimensions. Listed so such a value is edited
    /// field by field instead of as a line of JSON.
    /// </summary>
    public static readonly SchemaTemplate ImageObject = new(
        "ImageObject", "Görsel", "Bir görsel ve boyutları.",
        [
            new("url", "Adres", SchemaPropertyKind.Url, Recommended: true),
            new("width", "Genişlik (px)", SchemaPropertyKind.Number),
            new("height", "Yükseklik (px)", SchemaPropertyKind.Number),
            new("caption", "Açıklama"),
            new("encodingFormat", "Dosya Türü", SchemaPropertyKind.Text, Hint: "Örn. image/jpeg, image/webp"),
        ]);

    public static readonly SchemaTemplate AggregateRating = new(
        "AggregateRating", "Puan Özeti", "Toplu değerlendirme puanı.",
        [
            new("ratingValue", "Puan", SchemaPropertyKind.Number, Recommended: true),
            new("reviewCount", "Değerlendirme Sayısı", SchemaPropertyKind.Number, Recommended: true),
            new("bestRating", "En Yüksek Puan", SchemaPropertyKind.Number),
        ]);

    public static readonly SchemaTemplate Service = new(
        "Service",
        "Hizmet",
        "Sunulan bir hizmet.",
        [
            new("name", "Hizmet Adı", SchemaPropertyKind.Text, Recommended: true),
            new("description", "Açıklama", SchemaPropertyKind.LongText, Recommended: true),
            new("provider", "Sağlayan", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: PersonOrOrganization),
            new("areaServed", "Hizmet Bölgesi"),
            new("serviceType", "Hizmet Türü"),
            new("offers", "Fiyat", SchemaPropertyKind.Entity, AcceptedTypes: ["Offer"]),
        ],
        IdFragment: "#service");

    public static readonly SchemaTemplate VideoObject = new(
        "VideoObject",
        "Video",
        "Sayfadaki video. Arama sonuçlarında küçük görsel ve süre gösterebilir.",
        [
            new("name", "Başlık", SchemaPropertyKind.Text, Recommended: true),
            new("description", "Açıklama", SchemaPropertyKind.LongText, Recommended: true),
            new("thumbnailUrl", "Kapak Görseli", SchemaPropertyKind.TextList, Recommended: true),
            new("uploadDate", "Yükleme Tarihi", SchemaPropertyKind.DateTime, Recommended: true),
            new("duration", "Süre", SchemaPropertyKind.Text, Hint: "ISO 8601 süre, örn. PT2M30S"),
            new("contentUrl", "Video Dosyası", SchemaPropertyKind.Url),
            new("embedUrl", "Gömme Adresi", SchemaPropertyKind.Url),
        ],
        IdFragment: "#video");

    public static readonly SchemaTemplate Course = new(
        "Course",
        "Kurs",
        "Eğitim içeriği.",
        [
            new("name", "Kurs Adı", SchemaPropertyKind.Text, Recommended: true),
            new("description", "Açıklama", SchemaPropertyKind.LongText, Recommended: true),
            new("provider", "Sağlayan Kurum", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: ["Organization"]),
            new("url", "Kurs Adresi", SchemaPropertyKind.Url),
        ],
        IdFragment: "#course");

    public static readonly SchemaTemplate JobPosting = new(
        "JobPosting",
        "İş İlanı",
        "Açık pozisyon. Google for Jobs'ta görünebilir.",
        [
            new("title", "Pozisyon", SchemaPropertyKind.Text, Recommended: true),
            new("description", "İlan Metni", SchemaPropertyKind.LongText, Recommended: true),
            new("datePosted", "İlan Tarihi", SchemaPropertyKind.Date, Recommended: true),
            new("validThrough", "Son Başvuru", SchemaPropertyKind.Date),
            new("hiringOrganization", "İşveren", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: ["Organization"]),
            new("jobLocation", "Çalışma Yeri", SchemaPropertyKind.Entity, Recommended: true, AcceptedTypes: ["Place"]),
            new("employmentType", "Çalışma Şekli", SchemaPropertyKind.Text, Hint: "FULL_TIME, PART_TIME, CONTRACTOR…"),
        ],
        IdFragment: "#jobposting");

    public static readonly SchemaTemplate Recipe = new(
        "Recipe",
        "Tarif",
        "Yemek tarifi.",
        [
            new("name", "Tarif Adı", SchemaPropertyKind.Text, Recommended: true),
            new("image", "Görseller", SchemaPropertyKind.TextList, Recommended: true),
            new("description", "Açıklama", SchemaPropertyKind.LongText),
            new("recipeIngredient", "Malzemeler", SchemaPropertyKind.TextList, Recommended: true),
            new("recipeInstructions", "Adımlar", SchemaPropertyKind.TextList, Recommended: true),
            new("cookTime", "Pişirme Süresi", SchemaPropertyKind.Text, Hint: "ISO 8601, örn. PT30M"),
            new("prepTime", "Hazırlık Süresi", SchemaPropertyKind.Text),
            new("recipeYield", "Porsiyon"),
            new("author", "Yazar", SchemaPropertyKind.Entity, AcceptedTypes: PersonOrOrganization),
        ],
        IdFragment: "#recipe");

    public static readonly SchemaTemplate SoftwareApplication = new(
        "SoftwareApplication",
        "Uygulama",
        "Yazılım veya mobil uygulama.",
        [
            new("name", "Uygulama Adı", SchemaPropertyKind.Text, Recommended: true),
            new("operatingSystem", "İşletim Sistemi", SchemaPropertyKind.Text, Recommended: true),
            new("applicationCategory", "Kategori", SchemaPropertyKind.Text, Recommended: true),
            new("offers", "Fiyat", SchemaPropertyKind.Entity, AcceptedTypes: ["Offer"]),
            new("aggregateRating", "Puan Özeti", SchemaPropertyKind.Entity, AcceptedTypes: ["AggregateRating"]),
        ],
        IdFragment: "#softwareapplication");

    public static readonly SchemaTemplate ContactPage = new(
        "ContactPage", "İletişim Sayfası", "İletişim bilgilerini barındıran sayfa.",
        [
            new("name", "Başlık", SchemaPropertyKind.Text),
            new("url", "Adres", SchemaPropertyKind.Url),
            new("description", "Açıklama", SchemaPropertyKind.LongText),
        ],
        IdFragment: "#contactpage");

    public static readonly SchemaTemplate AboutPage = new(
        "AboutPage", "Hakkımızda Sayfası", "Kurumu anlatan sayfa.",
        [
            new("name", "Başlık", SchemaPropertyKind.Text),
            new("url", "Adres", SchemaPropertyKind.Url),
            new("description", "Açıklama", SchemaPropertyKind.LongText),
        ],
        IdFragment: "#aboutpage");

    public static readonly SchemaTemplate CollectionPage = new(
        "CollectionPage", "Liste Sayfası",
        "Başka sayfaları listeleyen sayfa (kategori, blog ana sayfası). Listelenen sayfalar yayındaki sitede ayrıca ItemList olarak eklenir.",
        [
            new("name", "Başlık", SchemaPropertyKind.Text, Recommended: true),
            new("url", "Adres", SchemaPropertyKind.Url, Recommended: true),
            new("description", "Açıklama", SchemaPropertyKind.LongText, Recommended: true),
            new("inLanguage", "Dil"),
            new("datePublished", "Yayın Tarihi", SchemaPropertyKind.DateTime),
            new("dateModified", "Güncelleme Tarihi", SchemaPropertyKind.DateTime),
        ],
        IdFragment: "#collectionpage");

    /// <summary>
    /// Everything the "add node" picker offers, in the order it is shown —
    /// page-defining types first, supporting types (address, offer, rating…) after,
    /// since those are usually reached by filling a property rather than added
    /// directly.
    /// </summary>
    public static readonly IReadOnlyList<SchemaTemplate> All =
    [
        Organization, WebSite, WebPage, BreadcrumbList,
        Article, BlogPosting, NewsArticle,
        FaqPage, LocalBusiness, Person, Event, Product, Service,
        VideoObject, Course, JobPosting, Recipe, SoftwareApplication,
        ContactPage, AboutPage, CollectionPage,
        // Supporting types
        Question, Answer, ListItem, PostalAddress, ContactPoint, GeoCoordinates, Place, Offer, AggregateRating, ImageObject,
    ];

    /// <summary>
    /// Looks a template up by its schema type. Returns null for a type the
    /// catalogue does not cover — which is not an error: the builder still shows
    /// and preserves such a node, it just cannot offer ready-made fields for it.
    /// </summary>
    public static SchemaTemplate? Find(string? type) =>
        string.IsNullOrWhiteSpace(type)
            ? null
            : All.FirstOrDefault(t => string.Equals(t.Type, type, StringComparison.OrdinalIgnoreCase));
}
