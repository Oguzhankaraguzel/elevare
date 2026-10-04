// MAIN GRAPESJS MODULE
window.grapesEditor = (function() {
    let editor = null;
    let assetsLoaded = false;
    let initializing = false;
    // The { updateMeta, reanalyze } accessor returned from elevare-seo.js's attach() call.
    let seoApi = null;
    // Active languages ({ code, isDefault }[]), pushed in from Blazor alongside
    // init() — read synchronously by elevare-blocks.js's Language Switcher block.
    let activeLanguages = [];
    // Site pages ({ id, label }[], label = full slug), pushed in from Blazor
    // alongside init() — read synchronously by elevare-blocks.js's Sayfa Listesi
    // block to build its "Kaynak Sayfa" select options, so the user picks a real
    // page instead of typing a database id they couldn't possibly know.
    let pageDirectory = [];
    // Real-world business details from Site Settings ({ mapEmbedUrl, workingHours }),
    // pushed in from Blazor BEFORE init() — read synchronously by elevare-blocks.js so
    // the Google Map and Local Business Info blocks open with this site's own address
    // instead of the sample one baked into their templates.
    let siteInfo = {};
    // The Blazor component reference, kept for the change/upload callbacks below.
    let blazorRef = null;
    let listingTagsPromise = null;
    // id -> { version, name } for every template, from GetPageTemplateVersionsQuery.
    // The page editor passes it; the template editor does not (it has no page).
    let templateVersions = {};
    // Whether the canvas has changed since the last save. Blazor is told ONCE per
    // dirty transition (not per keystroke) so the unsaved-changes guard costs a
    // single interop call, not one per edit.
    let canvasDirty = false;
    // The exported HTML+CSS as of the last save/load. GrapesJS's 'update' event is a
    // catch-all that also fires for things the user never did — the style manager
    // recomputing, a lazy image settling, the RTE attaching on hover, the SEO
    // analyzer walking the tree — so an editor left untouched would eventually
    // announce unsaved changes. Comparing the actual exported content is what makes
    // "dirty" mean "the content differs from what was loaded", same idea as
    // PageEditor.razor's CurrentSignature() for the form fields.
    let cleanSignature = null;
    // Coalesces the burst of 'update' events a single edit produces.
    let dirtyCheckTimer = null;
    // Guards against re-entrancy while an upload round-trip is in flight.
    let uploadInFlight = false;
    // GrapesJS version
    const VER = '0.21.10';
    // CSS files to load
    const CSS = [
        `https://unpkg.com/grapesjs@${VER}/dist/css/grapes.min.css`
    ];
    // GrapesJS core library
    const CORE = `https://unpkg.com/grapesjs@${VER}`;
    // All compatible plugins to load.
    //
    // Every entry pins an exact version, deliberately. These used to load as bare
    // "https://unpkg.com/<pkg>", which always resolves to whatever the package
    // published most recently — so a plugin author shipping a breaking release
    // broke this editor for everyone, with no change on our side to point at and
    // no way to roll back. GrapesJS core has always been pinned (VER above);
    // there was never a reason for its plugins not to be.
    //
    // Two packages were removed rather than pinned:
    //   grapesjs-typed    — needs the typed.js library, which nothing here loads.
    //                       The block it registers therefore threw on use, both in
    //                       the canvas and on the published page, and it was the
    //                       one block in the panel with no category (it landed in
    //                       an unnamed group). Bring it back WITH typed.js if the
    //                       animated-headline effect is ever actually wanted.
    //   grapesjs-indexeddb — a StorageManager adapter. storageManager is false
    //                       below (Blazor owns saving), so it wired itself to
    //                       nothing and cost a CDN round-trip per editor open.
    const PLUGIN_DEFS = [
        { pkg: 'grapesjs-blocks-basic', ver: '1.0.2' },
        { pkg: 'grapesjs-plugin-forms', ver: '2.0.6' },
        { pkg: 'grapesjs-preset-webpage', ver: '1.0.3' },
        { pkg: 'grapesjs-navbar', ver: '1.0.2' },
        { pkg: 'grapesjs-tabs', ver: '1.0.6' },
        { pkg: 'grapesjs-component-countdown', ver: '1.0.2' },
        { pkg: 'grapesjs-tooltip', ver: '0.1.8' },
        { pkg: 'grapesjs-style-bg', ver: '2.0.2' },
        { pkg: 'grapesjs-style-gradient', ver: '3.0.3' },
        { pkg: 'grapesjs-style-filter', ver: '1.0.2' },
        { pkg: 'grapesjs-custom-code', ver: '1.0.2' },
        { pkg: 'grapesjs-touch', ver: '0.1.1' },
        { pkg: 'grapesjs-plugin-export', ver: '1.0.12' },
        { pkg: 'grapesjs-parser-postcss', ver: '1.0.3' },
        { pkg: 'grapesjs-tui-image-editor', ver: '1.0.2' },
        { pkg: 'grapesjs-plugin-ckeditor', ver: '1.0.1' },
        // Custom Elevare plugins added
        { pkg: 'grapesElevareBlocks' }
    ];
    // Plugin-specific options
    const PLUGIN_OPTS = {
        'grapesjs-blocks-basic': { flexGrid: true },
        'grapesjs-tabs': { tabsBlock: { category: 'Extra' } },
        'grapesjs-preset-webpage': {
            blocksBasicOpts: { flexGrid: true },
            // The preset flips the right panel to the style manager on every
            // selection. With İçerik and Görünüm as two tabs of one panel, the
            // author's choice of tab has to survive a click on the canvas.
            showStylesOnChange: 0
        },
        'grapesjs-tui-image-editor': {
            config: {
                includeUI: { initMenu: 'filter' }
            }
        },
        'grapesjs-plugin-ckeditor': {
            options: {
                startupFocus: true,
                extraAllowedContent: '*(*);*{*}',
                allowedContent: true,
                versionCheck: false,
                // Not merely hidden from the toolbar — unloaded. All three are paid
                // third-party services that would otherwise send the page's text to
                // someone else's server (scayt/wsc start checking on their own, with
                // no button press needed).
                removePlugins: 'exportpdf,scayt,wsc',
                // The toolbar FLOATS, inside the canvas iframe, and must keep doing so.
                //
                // Docking it into a strip in the top document (CKEditor's sharedspace
                // plugin) was tried and reverted: it looks better, but every click on it
                // leaves the iframe, and a measured trace showed what follows —
                //   cke blur → GrapesJS rte:disable → command executes with an EMPTY
                //   selection
                // GrapesJS reads any blur of the editable as "editing finished" and tears
                // the rich-text editor down before the command arrives. Bold on a selected
                // word then wrapped nothing, leaving <strong> full of zero-width spaces at
                // position 0 and no visible caret. Locking the selection on blur stopped
                // the corruption but not the loss, because the editor itself was gone.
                // Living inside the iframe is what keeps the selection alive, so the
                // overlap is dealt with in CSS instead — see injectRteToolbarStyles.
                floatSpaceDockedOffsetY: 8,
                floatSpacePreferRight: true,
                // Everything the full build offers EXCEPT the buttons that would fight
                // the CMS or the browser. Each exclusion is deliberate:
                //   Save / NewPage / Preview / Print / Templates — the page editor
                //     already has its own Save, Önizle and Şablonlar. A second set
                //     doing something subtly different is worse than none.
                //   Image — bypasses the Media Library entirely: no upload record, no
                //     alt text, no CDN prefix. The Image block and drag-drop go through
                //     it; this button would quietly not.
                //   Form / Checkbox / Radio / TextField / Textarea / Select / Button /
                //     ImageButton / HiddenField — the CMS has managed Forms with stored
                //     submissions and reply templates. A raw CKEditor form posts nowhere.
                //   Iframe — embedding arbitrary third-party frames is gated behind the
                //     CustomCode.Author permission; an RTE button would hand it to every
                //     editor. Video/Map blocks cover the legitimate cases.
                //   Smiley — does not insert an emoji, it inserts
                //     <img src="https://cdn.ckeditor.com/…/smiley/images/…">, putting a
                //     third-party CDN on the published page for something a visitor's
                //     own font already draws. Typing or pasting a real emoji character
                //     works and costs nothing.
                //   Flash — dead everywhere; ExportPdf / Scayt — paid services that ship
                //     the page's content to a third party.
                //   PageBreak — print-only, meaningless on a web page.
                //   About — noise.
                // The essentials, and nothing that fights the site's design. Font,
                // FontSize, TextColor and BGColor are gone on purpose: each wrote an
                // inline style — a hex colour, a pixel size — that ignores the
                // palette in Site Codes and the dark theme with it (a slide's "İzle"
                // button unreadable in dark mode was exactly that). Colour and size
                // belong to the Görünüm tab, where they are written as rules and can
                // use the brand tokens. Styles, Table, Anchor, Source, Find, the
                // clipboard row and the rest went for the reason the panel got
                // crowded in the first place: nobody used them, everybody saw them.
                toolbar: [
                    { name: 'styles', items: ['Format'] },
                    { name: 'basicstyles', items: ['Bold', 'Italic', 'Underline', 'Strike', 'RemoveFormat'] },
                    { name: 'links', items: ['Link', 'Unlink'] },
                    { name: 'paragraph', items: ['NumberedList', 'BulletedList'] },
                    { name: 'align', items: ['JustifyLeft', 'JustifyCenter', 'JustifyRight'] },
                    // The CMS ships RTL languages (Language.IsRtl), so per-block text
                    // direction is a real need here, not a leftover from the demo.
                    { name: 'bidi', items: ['BidiLtr', 'BidiRtl'] },
                    { name: 'insert', items: ['SpecialChar', 'PasteText'] },
                    { name: 'undo', items: ['Undo', 'Redo'] }
                ],
                // Only the headings a content page legitimately uses. The default list
                // also offers <pre>, <address> and six presentational variants that
                // quietly wreck a page's heading outline — the very thing the SEO
                // panel then reports as a problem.
                format_tags: 'p;h1;h2;h3;h4'
            }
        }
    };

    // --------------------------------------------------------------
    // TURKISH LOCALE (GrapesJS core chrome — Style/Trait/Layer/Selector/Asset
    // managers, panel button tooltips)
    // --------------------------------------------------------------
    // GrapesJS ships its own official 'tr' locale (src/i18n/locale/tr.js,
    // upstream in the GrapesJS repo) — this starts from that exact file so it
    // never drifts from the community translation, and only ADDS what that
    // file leaves out. Comparing it against the upstream 'en.js' turned up
    // three real gaps: selectorManager.states (the "Hover / Click / Even-Odd"
    // row seen un-translated in the Style Manager), domComponents.names (the
    // Layer Manager's names for built-in node types), and two smaller labels
    // (assetManager.inputPlh, styleManager.fileButton). Everything else here
    // is the upstream file verbatim. Anything still missing falls through to
    // English via i18n.localeFallback below — this file does not need to be a
    // complete GrapesJS locale, only more complete than it was.
    //
    // This does NOT cover this project's OWN blocks (Hero Bölümü, Özellik
    // Izgarası, etc.) — those are translated separately by elevare-blocks.js's
    // own LABELS_TR/blockLabel(), which the en.js source itself points out is
    // the intended split ("GrapesJS core doesn't contain any block, so
    // [blockManager.labels] should be omitted from other locale files").
    const TR_LOCALE = {
        assetManager: {
            addButton: 'Görsel Ekle',
            inputPlh: 'http://resmin/yolu.jpg',
            modalTitle: 'Görsel Seçin',
            uploadTitle: 'Dosya yüklemek için buraya sürükleyin veya tıklayın',
        },
        domComponents: {
            names: {
                '': 'Kutu',
                wrapper: 'Gövde',
                text: 'Metin',
                comment: 'Yorum',
                image: 'Görsel',
                video: 'Video',
                label: 'Etiket',
                link: 'Bağlantı',
                map: 'Harita',
                tfoot: 'Tablo altı',
                tbody: 'Tablo gövdesi',
                thead: 'Tablo başlığı',
                table: 'Tablo',
                row: 'Tablo satırı',
                cell: 'Tablo hücresi',
            },
        },
        deviceManager: {
            device: 'Cihaz',
            devices: {
                desktop: 'Masaüstü',
                tablet: 'Tablet',
                mobileLandscape: 'Mobil Yatay',
                mobilePortrait: 'Mobil Dikey',
            },
        },
        panels: {
            buttons: {
                titles: {
                    preview: 'Önizleme',
                    fullscreen: 'Tam Ekran',
                    'sw-visibility': 'Bileşenleri Göster',
                    'export-template': 'Kodu Göster',
                    'open-sm': 'Stil Düzenleyiciyi Aç',
                    'open-tm': 'Ayarlar',
                    'open-layers': 'Katmanlar',
                    'open-blocks': 'Bloklar',
                },
            },
        },
        selectorManager: {
            selected: 'Seçili',
            emptyState: '- DURUM -',
            label: 'Sınıflar',
            states: {
                hover: 'Üzerine Gelince',
                active: 'Tıklanınca',
                'nth-of-type(2n)': 'Çift/Tek',
            },
        },
        styleManager: {
            empty: 'Stilini düzenlemek istediğiniz öğeyi seçiniz',
            layer: 'Katman',
            fileButton: 'Görseller',
            sectors: {
                general: 'Gelişmiş: yerleşim',
                layout: 'Düzen',
                typography: 'Yazı',
                decorations: 'Arka plan ve kenarlık',
                extra: 'Efektler',
                flex: 'Gelişmiş: flex',
                dimension: 'Boyut ve boşluk',
            },
            properties: {
                float: 'Kaydır',
                display: 'Görünüm',
                position: 'Pozisyon',
                top: 'Üst',
                right: 'Sağ',
                left: 'Sol',
                bottom: 'Alt',
                'z-index': 'Z Sırası',
                overflow: 'Taşma',
                cursor: 'İmleç',
                width: 'Genişlik',
                height: 'Yükseklik',
                'min-width': 'Min. Genişlik',
                'max-width': 'Maks. Genişlik',
                'min-height': 'Min. Yükseklik',
                'max-height': 'Maks. Yükseklik',
                margin: 'Margin',
                'margin-top': 'Margin Üst',
                'margin-right': 'Margin Sağ',
                'margin-left': 'Margin Sol',
                'margin-bottom': 'Margin Alt',
                padding: 'Padding',
                'padding-top': 'Padding Üst',
                'padding-left': 'Padding Sol',
                'padding-right': 'Padding Sağ',
                'padding-bottom': 'Padding Alt',
                'font-family': 'Font Tipi',
                'font-size': 'Font Boyutu',
                'font-weight': 'Font Kalınlığı',
                'letter-spacing': 'Harf Boşluğu',
                color: 'Renk',
                'line-height': 'Satır Boşluğu',
                'text-align': 'Yazı Hizalaması',
                'text-shadow': 'Yazı Gölgesi',
                'text-shadow-h': 'Yazı Gölgesi - Yatay',
                'text-shadow-v': 'Yazı Gölgesi - Dikey',
                'text-shadow-blur': 'Yazı Gölgesi Bulanıklığı',
                'text-shadow-color': 'Yazı Gölgesi Rengi',
                'border-top-left': 'Kenar Üst Sol',
                'border-top-right': 'Kenar Üst Sağ',
                'border-bottom-left': 'Kenar Alt Sol',
                'border-bottom-right': 'Kenar Alt Sağ',
                'border-radius-top-left': 'Köşe Yumuşaması Üst Sol',
                'border-radius-top-right': 'Köşe Yumuşaması Üst Sağ',
                'border-radius-bottom-left': 'Köşe Yumuşaması Alt Sol',
                'border-radius-bottom-right': 'Köşe Yumuşaması Alt Sağ',
                'border-radius': 'Köşe Yumuşaması',
                border: 'Kenar',
                'border-width': 'Kenar Kalınlığı',
                'border-style': 'Kenar Stili',
                'border-color': 'Kenar Rengi',
                'box-shadow': 'Kutu Gölgesi',
                'box-shadow-h': 'Kutu Gölgesi - Yatay',
                'box-shadow-v': 'Kutu Gölgesi - Dikey',
                'box-shadow-blur': 'Kutu Gölgesi Bulanıklığı',
                'box-shadow-spread': 'Kutu Gölgesi Dağılımı',
                'box-shadow-color': 'Kutu Gölgesi Rengi',
                'box-shadow-type': 'Kutu Gölgesi Tipi',
                background: 'Arkaplan',
                'background-image': 'Arkaplan Resmi',
                'background-repeat': 'Arkaplan Tekrarı',
                'background-position': 'Arkaplan Pozisyonu',
                'background-attachment': 'Arkaplan Eklentisi',
                'background-size': 'Arkaplan Boyutu',
                opacity: 'Saydamlık',
                transition: 'Geçiş',
                'transition-property': 'Geçiş Özelliği',
                'transition-duration': 'Geçiş Süresi',
                'transition-timing-function': 'Geçiş Zamanlaması Metodu',
                perspective: 'Perspektif',
                transform: 'Boyutlama',
                'transform-rotate-x': 'Yatay Yönlendirme',
                'transform-rotate-y': 'Dikey Yönlendirme',
                'transform-rotate-z': 'Hacimsel Yönlendirme',
                'transform-scale-x': 'Dikey Oran',
                'transform-scale-y': 'Yatay Oran',
                'transform-scale-z': 'Hacimsel Oran',
                'flex-direction': 'Flex Yönü',
                'flex-wrap': 'Flex Kesme',
                'justify-content': 'İçeriği Sığdır',
                'align-items': 'Öğeleri Hizala',
                'align-content': 'İçeriği Hizala',
                order: 'Sıra',
                'flex-basis': 'Flex Bazı',
                'flex-grow': 'Flex Büyüme',
                'flex-shrink': 'Flex Küçülme',
                'align-self': 'Kendini Hizala',
                'background-color': 'Arkaplan Rengi',
            },
        },
        traitManager: {
            empty: 'Özelliklerini düzenlemek istediğiniz öğeyi seçiniz',
            label: 'Bileşen Özellikleri',
            traits: {
                labels: {},
                attributes: {},
                options: {},
            },
        },
    };

    // --------------------------------------------------------------
    // EDITOR UI TEXT — every label this file puts on screen, in both languages
    // --------------------------------------------------------------
    // The panel used to read "Loading", "Title", "Aria Label", "Width (px,
    // CLS)" next to "Görsel URL", "Etiket", "Boyut": half English, half Turkish,
    // whichever language the line was written in. The CMS is bilingual; its
    // editor should be too. GrapesJS's own chrome is localised through
    // TR_LOCALE above; this is the same for what we add ourselves.
    const UI_TEXT = {
        tr: {
            blocks: 'Bloklar', layers: 'Katmanlar', content: 'İçerik', look: 'Görünüm',
            selected: 'Seçili öğe', nothing: 'Düzenlemek için tuvalde bir öğe seçin.',
            blockSearch: 'Blok ara…', recent: 'Son kullanılanlar', noBlocks: 'Eşleşen blok yok.',
            tplLinked: 'Bağlı şablon', tplEdit: 'Şablonu düzenle', tplUnlink: 'Bağı kes',
            tplHint: 'Bu içerik şablondan gelir ve burada değiştirilemez. Şablonu düzenleyin (yeni sekmede açılır) ya da bağı kesip bu sayfaya özel bir kopya yapın.',
            catContent: 'İçerik', catLink: 'Bağlantı', catSettings: 'Ayarlar', catAdvanced: 'Gelişmiş', id: 'Kimlik (id)',
            image: 'Görsel', pickImage: 'Kütüphaneden seç', imageUrl: 'Görsel adresi', alt: 'Alt metin (SEO)',
            title: 'Başlık (title)', loading: 'Yükleme', lazy: 'Gecikmeli (varsayılan)', eager: 'Hemen (sayfanın ilk görseli)',
            tag: 'Etiket', aria: 'Erişilebilir ad (aria-label)', iframeTitle: 'Başlık (erişilebilirlik)',
            sitePage: 'Site sayfası', href: 'Adres (URL)', target: 'Hedef', sameTab: 'Aynı sekme', newTab: 'Yeni sekme', rel: 'Rel',
            tagP: 'Paragraf (p)', tagH: 'Başlık', tagDiv: 'Blok (div)', tagSpan: 'Satır içi (span)',
            sizeWidth: 'Genişlik', sizeHeight: 'Yükseklik', sizeLock: 'Oranı kilitle: biri yazılınca diğeri dosyanın oranına göre dolar',
            sizeFile: 'Dosya', sizeRatio: 'oran', sizeUnknown: 'Dosya boyutu okunamadı', sizeFit: 'Orana uydur',
            sizeWarn: 'Oran dosyayla uyuşmuyor — görsel ezik ya da basık görünür. "Orana uydur" düzeltir.'
        },
        en: {
            blocks: 'Blocks', layers: 'Layers', content: 'Content', look: 'Style',
            selected: 'Selected element', nothing: 'Select an element on the canvas to edit it.',
            blockSearch: 'Search blocks…', recent: 'Recently used', noBlocks: 'No matching block.',
            tplLinked: 'Linked template', tplEdit: 'Edit template', tplUnlink: 'Cut the link',
            tplHint: 'This content comes from a template and cannot be changed here. Edit the template (opens in a new tab) or cut the link to make a copy for this page.',
            catContent: 'Content', catLink: 'Link', catSettings: 'Settings', catAdvanced: 'Advanced', id: 'Id',
            image: 'Image', pickImage: 'Choose from library', imageUrl: 'Image URL', alt: 'Alt text (SEO)',
            title: 'Title', loading: 'Loading', lazy: 'Lazy (default)', eager: 'Eager (first image on the page)',
            tag: 'Tag', aria: 'Accessible name (aria-label)', iframeTitle: 'Title (accessibility)',
            sitePage: 'Site page', href: 'Address (URL)', target: 'Target', sameTab: 'Same tab', newTab: 'New tab', rel: 'Rel',
            tagP: 'Paragraph (p)', tagH: 'Heading', tagDiv: 'Block (div)', tagSpan: 'Inline (span)',
            sizeWidth: 'Width', sizeHeight: 'Height', sizeLock: 'Lock ratio: type one, the other follows the file\'s proportions',
            sizeFile: 'File', sizeRatio: 'ratio', sizeUnknown: 'File size could not be read', sizeFit: 'Fit to ratio',
            sizeWarn: 'The proportions do not match the file — the image will look squashed or stretched. "Fit to ratio" repairs it.'
        }
    };
    const UI_LANG = (document.documentElement.lang || 'en').toLowerCase().startsWith('tr') ? 'tr' : 'en';
    const ui = (key) => (UI_TEXT[UI_LANG] && UI_TEXT[UI_LANG][key]) || UI_TEXT.en[key] || key;

    // --------------------------------------------------------------
    // CKEDITOR INLINE EDITING FIX
    // --------------------------------------------------------------
    // Root cause of "double click doesn't open the editor on <a> etc.":
    // CKEditor 4 only allows *inline* editing on tags listed in
    // CKEDITOR.dtd.$editable. By default that list does NOT contain
    // a, button, span, li, td, ... so the plugin refuses to attach.
    // Instead of blocking those tags, we EXTEND the DTD so inline
    // editing works everywhere it can meaningfully work.
    const CK_INLINE_EDITABLE = [
        'a', 'button', 'span', 'strong', 'em', 'b', 'i', 'u', 's', 'small',
        'sub', 'sup', 'label', 'li', 'dt', 'dd', 'td', 'th', 'figcaption',
        'summary', 'cite', 'q', 'time', 'mark', 'code', 'blockquote', 'address'
    ];
    // Where a paragraph may legally go, CKEditor wraps the text it finds in one
    // the moment it gains focus (autoParagraph): a logo strip's <li>MARKA 2</li>
    // came back as <li><p>MARKA 2</p></li>, and the <p>'s own margin made that
    // one logo taller than its neighbours. These are the one-line containers of
    // a layout — a list item, a table cell, a caption — not places to write
    // paragraphs in. A text block (div) keeps the wrapping: paragraphs are
    // what one writes there.
    const CK_NO_AUTO_PARAGRAPH = ['li', 'td', 'th', 'dt', 'dd', 'figcaption', 'label', 'address'];
    // A paste goes in as plain text wherever CKEditor's own insertion would break
    // the markup around it. It inserts by looking for the block around the caret;
    // in a root that cannot hold a block it finds the root itself — and SPLITS it.
    // Measured with 4.22.1: pasting "September" over "Eylül" in <time>12 Eylül
    // 2023</time> left <time>12 </time><time>September 2023</time>, the second
    // outside the editor, whatever the enter mode — and an HTML paste (a word copied
    // from a web page arrives as <p><span>…) hung the page outright, which is what
    // the byline's date did on the live English article. With the whole byline as
    // the root (a <p>), the same paste still cut the <time> in two around it.
    //   Roots that cannot hold a block — the inline ones, a paragraph, a heading —
    //   always take plain text, line breaks as spaces.
    //   Anywhere else a single line goes in as plain text at the caret, inside
    //   whatever it sits in; several paragraphs are CKEditor's to lay out, as before.
    // The browser inserts the text itself, which never splits anything.
    const CK_NO_BLOCK_ROOTS = [
        'a', 'button', 'span', 'strong', 'em', 'b', 'i', 'u', 's', 'small', 'sub', 'sup',
        'label', 'cite', 'q', 'time', 'mark', 'code', 'p', 'h1', 'h2', 'h3', 'h4', 'h5', 'h6', 'dt'
    ];
    function guardPaste(ck, noBlockRoot) {
        ck.on('paste', (e) => {
            const transfer = e.data.dataTransfer;
            let text = transfer && transfer.getData ? transfer.getData('text/plain', true) : '';
            if (!text) {
                const tmp = document.createElement('div');
                tmp.innerHTML = String(e.data.dataValue || '');
                text = tmp.textContent || '';
            }
            const lines = text.replace(/\r\n?/g, '\n').trim().split(/\n+/);
            if (!noBlockRoot && lines.length > 1) return;
            e.cancel();
            ck.fire('saveSnapshot');
            ck.document.$.execCommand('insertText', false, lines.map((l) => l.trim()).join(' '));
            ck.fire('saveSnapshot');
            ck.fire('change');
        }, null, null, 1);
    }
    function patchCkeditorDtd() {
        const CKE = window.CKEDITOR;
        if (!CKE || !CKE.dtd || !CKE.dtd.$editable) return;
        CK_INLINE_EDITABLE.forEach((t) => { CKE.dtd.$editable[t] = 1; });
        // Once per page: this runs on every editor start, and each call used to add
        // the listener again — the tenth editor opened ran it ten times.
        if (CKE.elevareHooked) return;
        CKE.elevareHooked = true;
        CKE.on('instanceCreated', (ev) => {
            const ck = ev.editor;
            ck.on('configLoaded', () => {
                const el = ck.element && ck.element.getName && ck.element.getName();
                if (el && CK_NO_AUTO_PARAGRAPH.includes(el)) ck.config.autoParagraph = false;
                guardPaste(ck, !!el && CK_NO_BLOCK_ROOTS.includes(el));
            });
        });
    }

    // grapesjs-plugin-ckeditor calls RichTextEditor.enable with the component's
    // Backbone-style VIEW, not a raw DOM element — its `tagName` is a function
    // (`view.tagName()`), not a string. Checking `typeof el.tagName === 'string'`
    // was therefore always false, silently aborting every double-click before
    // CKEditor was ever invoked. This resolves the tag name from either shape.
    function getElTagName(el) {
        if (!el) return '';
        if (typeof el.tagName === 'string') return el.tagName.toUpperCase();
        if (typeof el.tagName === 'function') {
            try {
                const t = el.tagName();
                if (typeof t === 'string') return t.toUpperCase();
            } catch (e) { /* fall through */ }
        }
        if (el.el) return getElTagName(el.el); // unwrap a Backbone-style view
        return '';
    }

    // Only true void/replaced elements stay blocked — they have no text
    // content, so an RTE on them is meaningless and crashes CKEditor.
    const RTE_BLOCKED_TAGS = [
        'IMG', 'IFRAME', 'INPUT', 'SELECT', 'TEXTAREA', 'VIDEO', 'AUDIO',
        'CANVAS', 'SVG', 'HR', 'BR', 'EMBED', 'OBJECT', 'SOURCE', 'TRACK', 'WBR'
    ];
    // Fallback "Text" trait for these tags (useful if CKEditor fails to load).
    // NOTE: writing via this trait replaces ALL children (icons included) —
    // inline editing is the preferred path now.
    const TEXT_TRAIT_TAGS = ['A', 'BUTTON', 'LABEL'];

    // GrapesJS only wires up double-click-to-edit when the component model's
    // `editable` flag is true. The built-in 'text' type sets this by default,
    // but leaf elements produced by other plugins (e.g. grapesjs-plugin-forms'
    // <label> components) or by the project-data restore path are not
    // guaranteed to have it set — so double-click silently does nothing, with
    // no console error. Force it on for any plain text-bearing leaf tag.
    const FORCE_EDITABLE_TAGS = [
        'P', 'SPAN', 'LABEL', 'LI', 'DT', 'DD', 'TD', 'TH', 'FIGCAPTION',
        'SUMMARY', 'STRONG', 'EM', 'B', 'I', 'U', 'S', 'SMALL', 'SUB', 'SUP',
        'BLOCKQUOTE', 'ADDRESS', 'CITE', 'Q', 'TIME', 'MARK', 'CODE',
        'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'A', 'BUTTON'
    ];

    // aria-label only makes sense on interactive elements with no visible text of
    // their own (an icon-only link/button) or on landmark tags that need a name to
    // tell siblings apart (a second <nav>, a <form>, a <dialog>). Offering it on
    // every element — a <div>, a <p>, a plain text leaf — invites the CMS operator
    // to set it on something that already has visible text, which makes a screen
    // reader announce the aria-label INSTEAD of that text (the browser treats
    // aria-label as an override, not an addition), silencing the real content.
    const ARIA_LABEL_TAGS = ['A', 'BUTTON', 'NAV', 'FORM', 'DIALOG'];

    function loadCss(href) {
        return new Promise((resolve) => {
            if (document.querySelector(`link[data-gjs="${href}"]`)) { resolve(); return; }
            const l = document.createElement('link');
            l.rel = 'stylesheet'; l.href = href; l.setAttribute('data-gjs', href);
            l.onload = () => resolve(); l.onerror = () => resolve();
            document.head.appendChild(l);
        });
    }
    function loadScript(src) {
        return new Promise((resolve) => {
            if (document.querySelector(`script[data-gjs="${src}"]`)) { resolve(); return; }

            // GrapesJS core, CKEditor and every plugin here are plain UMD bundles —
            // fine on their own, but if this SPA tab visited Site Codes earlier,
            // Monaco's RequireJS loader is still sitting on window.define with .amd
            // set. A UMD script sees that and registers itself through RequireJS
            // instead of setting window[pluginName] like resolvePlugin() below
            // expects — and RequireJS itself chokes trying to attribute several
            // unrelated anonymous define() calls to one script tag ("Can only have
            // one anonymous define call per script file"). That's what silently
            // breaks the whole editor (grapesjs core included) after a Site Codes
            // visit. Assets here load strictly one at a time (s.async = false, each
            // awaited before the next starts), so hiding window.define for just the
            // one script currently loading is enough — nothing else on this page
            // needs it, and Monaco's own lazy chunk loading only ever happens while
            // its own editor is mounted on a different route.
            const savedDefine = window.define;
            if (savedDefine) delete window.define;
            const restoreDefine = () => { if (savedDefine) window.define = savedDefine; };

            const s = document.createElement('script');
            s.src = src; s.async = false; s.setAttribute('data-gjs', src);
            s.onload = () => { restoreDefine(); resolve(); };
            s.onerror = () => { restoreDefine(); console.warn('GrapeJS asset failed:', src); resolve(); };
            document.head.appendChild(s);
        });
    }
    async function ensureAssets() {
        if (assetsLoaded) return;
        await Promise.all(CSS.map(loadCss));
        await loadScript('https://cdn.ckeditor.com/4.22.1/full-all/ckeditor.js');
        // Must run right after ckeditor.js loads and before any instance exists.
        patchCkeditorDtd();

        await loadScript(CORE);
        for (const def of PLUGIN_DEFS) {
            // Don't try to load our own local plugin from a remote source
            if (def.pkg.startsWith('grapesElevare')) continue;
            const src = def.url || ('https://unpkg.com/' + def.pkg + (def.ver ? '@' + def.ver : ''));
            await loadScript(src);
        }
        assetsLoaded = true;
    }
    function resolvePlugin(pkg) {
        const short = 'gjs-' + pkg.replace(/^grapesjs-/, '');
        if (typeof window[pkg] === 'function') return window[pkg];
        if (typeof window[short] === 'function') return window[short];
        if (window[pkg] && typeof window[pkg].default === 'function') return window[pkg].default;
        if (window[short] && typeof window[short].default === 'function') return window[short].default;
        return null;
    }
    function applyPlugins() {
        let applied = 0;
        for (const def of PLUGIN_DEFS) {
            const fn = resolvePlugin(def.pkg);
            if (!fn) { console.warn('GrapeJS plugin not loaded, skipping:', def.pkg); continue; }
            try {
                fn(editor, PLUGIN_OPTS[def.pkg] || {});
                applied++;
            } catch (e) {
                console.warn('GrapeJS plugin failed to apply:', def.pkg, e);
            }
        }
        console.info('GrapeJS plugins applied:', applied, '/', PLUGIN_DEFS.length);
    }
    // The Site Codes CSS the canvas loads, minus what this page has switched off
    // (see setExcludedSiteCodes below and CanvasPreviewEndpoints). Cache-busted
    // per call so a change in Site Codes shows on the next open, and a change in
    // the exclusions shows the moment it is made.
    let excludedSiteCodeIds = [];
    const CANVAS_PREVIEW_CSS_ID = 'elevare-canvas-preview-css';
    function canvasPreviewCssUrl() {
        const exclude = excludedSiteCodeIds.length ? `&exclude=${excludedSiteCodeIds.join(',')}` : '';
        return `${window.location.origin}/_canvas-preview.css?v=${Date.now()}${exclude}`;
    }

    const TPL_REF_CLASS = 'elevare-tpl-ref';
    function wrapTemplateHtml(templateId, html) {
        return `<div class="${TPL_REF_CLASS}" data-elevare-template-id="${templateId}">${html || ''}</div>`;
    }
    // Unlinked insertion: the block is an independent copy from here on (see
    // PageTemplate.IsLinked), but it still carries the id + the source's version
    // at the moment it was copied, purely so a later session can tell the user
    // "this changed since you copied it" without pulling in the new content.
    const TPL_SNAPSHOT_CLASS = 'elevare-tpl-snapshot';
    const STALE_DISMISS_KEY = 'elevare.staleTemplateDismissed';
    // Per screen: the same template can be out of date on one page and not another.
    const staleKey = (templateId) => location.pathname + '#' + templateId;
    function readDismissedStale() {
        try { return JSON.parse(localStorage.getItem(STALE_DISMISS_KEY) || '{}') || {}; }
        catch (e) { return {}; }
    }
    function wrapSnapshotTemplateHtml(templateId, snapshotVersion, html) {
        return `<div class="${TPL_SNAPSHOT_CLASS}" data-elevare-template-id="${templateId}" ` +
            `data-elevare-template-snapshot-at="${snapshotVersion}">${html || ''}</div>`;
    }

    // --------------------------------------------------------------
    // LINKED TEMPLATE — one source, so read-only on the page
    // --------------------------------------------------------------
    // A linked template's content on a page is not the page's to edit: the public
    // site swaps the wrapper's insides for the template's CURRENT content on every
    // request (TemplateResolutionService), and refreshLinkedTemplates does the
    // same in this editor on every open. So an edit made inside the wrapper here
    // was saved, then never shown anywhere and gone the next time the page was
    // opened — with nothing on screen to say so. The editor let it happen and
    // silently threw it away.
    //
    // Now the wrapper is a component type of its own. Its whole subtree is locked
    // (not selectable, hoverable, editable, draggable or droppable), so a click
    // anywhere on the header lands on the wrapper, whose toolbar offers the two
    // things that DO work: edit the template itself, or cut the link and turn this
    // instance into an ordinary copy (the same snapshot wrapper the "insert as
    // copy" path produces, stamped with the template's current version so the
    // staleness check keeps working). The wrapper can still be moved, copied and
    // deleted as a whole — those are page decisions.
    const TPL_REF_TYPE = 'elevare-linked-template';
    const TPL_EDIT_COMMAND = 'elevare:linked-template-edit';
    const TPL_UNLINK_COMMAND = 'elevare:linked-template-unlink';
    const TPL_LOCKED_PROPS = {
        selectable: false, hoverable: false, editable: false, draggable: false,
        droppable: false, copyable: false, removable: false, layerable: false
    };
    // Material Design Icons: pencil, lock-open-variant. Not content-copy for the
    // unlink: the toolbar's own clone button already draws that, and two identical
    // glyphs doing different things is worse than none. An opening lock is what
    // cutting the link feels like from the page's side — after it, everything
    // inside can be edited.
    const TPL_GLYPHS = {
        edit: 'M20.71,7.04C21.1,6.65 21.1,6 20.71,5.63L18.37,3.29C18,2.9 17.35,2.9 16.96,3.29L15.12,5.12L18.87,8.87M3,17.25V21H6.75L17.81,9.93L14.06,6.18L3,17.25Z',
        unlink: 'M18,1C15.24,1 13,3.24 13,6V8H4A2,2 0 0,0 2,10V20A2,2 0 0,0 4,22H16A2,2 0 0,0 18,20V10A2,2 0 0,0 16,8H15V6A3,3 0 0,1 18,3A3,3 0 0,1 21,6V8H23V6C23,3.24 20.76,1 18,1M10,13A2,2 0 0,1 12,15C12,16.11 11.11,17 10,17A2,2 0 0,1 8,15A2,2 0 0,1 10,13Z'
    };
    const tplGlyph = (name) =>
        `<svg viewBox="0 0 24 24" width="16" height="16" style="display:block" aria-hidden="true"><path fill="currentColor" d="${TPL_GLYPHS[name]}"/></svg>`;

    function lockLinkedContent(comp) {
        comp.components().forEach((child) => {
            child.set(TPL_LOCKED_PROPS);
            lockLinkedContent(child);
        });
    }

    // Replaces a component with freshly parsed HTML at the same spot. replaceWith
    // is guarded for the same reason as in elevare-blocks.js's swapIcon.
    function replaceWithHtml(comp, html) {
        if (typeof comp.replaceWith === 'function') {
            const r = comp.replaceWith(html);
            return Array.isArray(r) ? r[0] : r;
        }
        const parent = comp.parent();
        const at = parent.components().indexOf(comp);
        comp.remove();
        return parent.append(html, { at })[0];
    }

    // Removing a subtree makes GrapesJS garbage-collect the CSS rules that only it
    // used (keepUnusedStyles is off — see retagComponent). For a linked template
    // that is its whole look. Rather than guess which rules go — id rules, but also
    // any class, compound, pseudo-state or @media rule unique to the template —
    // snapshot the ENTIRE stylesheet before the removal and re-add it after: a rule
    // that survived merges back as a no-op, a rule that was dropped comes back. This
    // is what carries the template's styles onto the unlinked copy so no text loses
    // its colour and vanishes.
    function withPreservedCss(mutate) {
        const cssBefore = editor.getCss();
        const result = mutate();
        try { editor.Css.addRules(cssBefore); } catch (e) { /* re-add is best-effort */ }
        return result;
    }

    // Every linked wrapper on the page gets the template's CURRENT content — the
    // same thing the public site does per request — and comes out as the typed,
    // locked wrapper above. The type matters: a page saved before the type
    // existed restores its wrapper from project JSON with whatever type was
    // stored (a plain div), and isComponent only ever runs on HTML. Measured on
    // the live homepage — both wrappers came back untyped, unlocked, with the
    // ordinary toolbar. So a wrapper of the wrong type is rebuilt from HTML
    // rather than merely refilled.
    //
    // In passes, because a linked template can itself hold one (a menu template
    // with a linked search box): its current content only appears once the outer
    // one has been refilled, and it carries whatever copy of the inner template
    // was saved with it. The public site does the same (TemplateResolutionService).
    // A template that ends up inside itself is left as it is rather than nested
    // again, and the depth is capped either way.
    const TPL_MAX_DEPTH = 5;
    function linkedAncestorWithId(comp, id) {
        for (let p = comp.parent(); p; p = p.parent()) {
            if (p.getClasses().includes(TPL_REF_CLASS) && String(p.getAttributes()['data-elevare-template-id']) === String(id)) return true;
        }
        return false;
    }
    function refreshLinkedTemplateRefs(linkedTemplatesMap) {
        const done = new Set();
        for (let pass = 0; pass < TPL_MAX_DEPTH; pass++) {
            const refs = editor.getWrapper().find('.' + TPL_REF_CLASS).filter((c) => !done.has(c));
            if (!refs.length) break;
            refs.forEach((comp) => {
                done.add(comp);
                const id = comp.getAttributes()['data-elevare-template-id'];
                const tpl = id != null ? linkedTemplatesMap[id] : null;
                if (!tpl || linkedAncestorWithId(comp, id)) return;
                const tplHtml = tpl.gjsHtml ?? tpl.GjsHtml ?? '';
                const tplCss = tpl.gjsCss ?? tpl.GjsCss;
                if (comp.get('type') !== TPL_REF_TYPE) {
                    const fresh = replaceWithHtml(comp, wrapTemplateHtml(id, tplHtml));
                    if (fresh) done.add(fresh);
                } else {
                    comp.components(tplHtml);
                    // Fresh children from fresh HTML: locked again, like the type's init.
                    lockLinkedContent(comp);
                }
                if (tplCss) { try { editor.Css.addRules(tplCss); } catch (e) { } }
            });
        }
    }

    function installLinkedTemplateType() {
        const tr = (document.documentElement.lang || 'en').toLowerCase().startsWith('tr');
        const T = tr
            ? { name: 'Bağlı şablon', edit: 'Şablonu düzenle (yeni sekmede açılır)', unlink: 'Bağı kopar, bu sayfaya özel kopya yap' }
            : { name: 'Linked template', edit: 'Edit the template (opens in a new tab)', unlink: 'Cut the link, make this a copy for this page' };

        editor.Commands.add(TPL_EDIT_COMMAND, {
            run(ed) {
                const m = ed.getSelected();
                const id = m && m.getAttributes()['data-elevare-template-id'];
                // A new tab: the page here may hold unsaved work.
                if (id) window.open('/templates/edit/' + encodeURIComponent(id), '_blank', 'noopener');
            }
        });

        editor.Commands.add(TPL_UNLINK_COMMAND, {
            run(ed) {
                const m = ed.getSelected();
                if (!m || m.get('type') !== TPL_REF_TYPE) return;
                const id = m.getAttributes()['data-elevare-template-id'];
                const known = id != null ? templateVersions[id] : null;
                const version = (known && (known.version ?? known.Version)) || new Date().toISOString();
                // Re-parsed from its own HTML: the blocks inside come back as their
                // real types (data-elevare-block), unlocked, ids intact. Wrapped in
                // withPreservedCss because removing the linked wrapper garbage-collects
                // the subtree's CSS rules; the snapshot keeps the same ids/classes, so
                // re-adding the stylesheet puts every rule back and the styling holds.
                const added = withPreservedCss(() =>
                    replaceWithHtml(m, wrapSnapshotTemplateHtml(id, version, m.getInnerHTML())));
                if (added) ed.select(added);
            }
        });

        editor.DomComponents.addType(TPL_REF_TYPE, {
            isComponent: (el) => el.nodeType === 1 && el.classList && el.classList.contains(TPL_REF_CLASS),
            model: {
                defaults: { droppable: false, editable: false },
                init() {
                    const id = this.getAttributes()['data-elevare-template-id'];
                    const known = id != null ? templateVersions[id] : null;
                    const tplName = known && (known.name ?? known.Name);
                    this.set('name', T.name + (tplName ? ': ' + tplName : ''));
                    const tb = (this.get('toolbar') || []).slice();
                    if (!tb.some((t) => t.command === TPL_EDIT_COMMAND)) {
                        tb.unshift(
                            { attributes: { title: T.edit }, label: tplGlyph('edit'), command: TPL_EDIT_COMMAND },
                            { attributes: { title: T.unlink }, label: tplGlyph('unlink'), command: TPL_UNLINK_COMMAND });
                        this.set('toolbar', tb);
                    }
                    lockLinkedContent(this);
                }
            }
        });
    }
    // GrapesJS/CKEditor sometimes leaves behind non-real attributes starting with
    // "__" for internal bookkeeping (e.g. __p="undefined"). These aren't valid
    // HTML; strip them from the exported output.
    function sanitizeInternalAttrs(html) {
        if (!html) return html;
        return html.replace(/\s+__[a-zA-Z0-9_-]*="[^"]*"/g, '');
    }

    // For values interpolated into a double-quoted HTML attribute. Alt text comes
    // from the media library, where a person typed it — a single " in a caption is
    // ordinary punctuation, not an attack, but unescaped it still ends the
    // attribute early and turns the rest of the sentence into stray markup.
    function escapeAttr(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }


    // --------------------------------------------------------------
    // IMAGE SIZE TRAIT — width × height that keep the file's proportions
    // --------------------------------------------------------------
    // Two bare number boxes, Width and Height, were the whole story before, and
    // nothing related them to each other or to the picture: 36 × 36 typed for a
    // 480 × 262 logo was accepted without a word, the browser squeezed the image
    // to fit, and Lighthouse reported it ("Displays images with incorrect aspect
    // ratio") — measured on the live homepage. The file's own proportions are
    // known the moment it is on the canvas (naturalWidth / naturalHeight), so the
    // panel can do the arithmetic: with the lock on, a width typed here fills the
    // height in, and the other way round; "Orana uydur" corrects a pair that is
    // already wrong; a new picture chosen for the same slot re-fits the height.
    // The lock is remembered per component for the session only (a WeakMap, not a
    // model prop), so it never lands in the saved page.
    const IMG_SIZE_TRAIT = 'elevare-img-size';
    const RATIO_TOLERANCE = 0.02; // Lighthouse's own: 2 % off the file's ratio counts as distorted
    const ratioLocks = new WeakMap();
    const ratioLocked = (m) => (ratioLocks.has(m) ? ratioLocks.get(m) : true);
    // Remembered per component: every attribute written makes the view redraw the
    // element, and for the moment the redrawn <img> reloads its file `complete`
    // is false — measured: right after "Orana uydur" the next typed width found
    // no file size at all. Cleared when the src changes (see below).
    const naturalSizes = new WeakMap();
    const sizePanels = new WeakMap();

    // The absolute URL the component means to show; '' when it has none.
    function wantedSrc(m, el) {
        try { return new URL(m.get('src') || (el && el.getAttribute('src')) || '', el.ownerDocument.baseURI).href; }
        catch (e) { return ''; }
    }
    // The canvas <img>, when it actually shows the wanted file (see whenLoaded
    // for why that qualifier matters); null otherwise.
    function loadedCanvasImage(m) {
        const el = m && m.getEl && m.getEl();
        if (!el || el.tagName !== 'IMG' || !el.complete || !el.naturalWidth) return null;
        const want = wantedSrc(m, el);
        return (!want || el.currentSrc === want) ? el : null;
    }
    // The file's pixel size, once known; null until then.
    function naturalSize(m) {
        const el = loadedCanvasImage(m);
        if (el) {
            const size = { w: el.naturalWidth, h: el.naturalHeight };
            naturalSizes.set(m, size);
            return size;
        }
        return naturalSizes.get(m) || null;
    }
    const attrInt = (m, name) => {
        const v = parseInt(m.getAttributes()[name], 10);
        return Number.isFinite(v) && v > 0 ? v : 0;
    };
    // Pixel values of the component's own width/height styles, when both are px.
    // The style manager writes these (#id{width:36px;height:36px}) and they win
    // over the attributes in the browser — the live header logo was 66 × 36 by
    // attribute and still drawn 36 × 36 because of them.
    function styledBox(m) {
        const st = m.getStyle ? m.getStyle() : {};
        const px = (v) => (typeof v === 'string' && /^\d+(\.\d+)?px$/.test(v.trim())) ? parseFloat(v) : 0;
        const w = px(st.width), h = px(st.height);
        return w > 0 && h > 0 ? { w, h } : null;
    }
    // True when what the browser will draw — the styled box if there is one, else
    // the width/height attributes — is a different shape than the file.
    function sizeDistorted(m) {
        const nat = naturalSize(m);
        if (!nat) return false;
        const box = styledBox(m) || { w: attrInt(m, 'width'), h: attrInt(m, 'height') };
        if (!box.w || !box.h) return false;
        return Math.abs((box.w / box.h) - (nat.w / nat.h)) / (nat.w / nat.h) > RATIO_TOLERANCE;
    }
    // A mismatch that the visitor would actually see. With object-fit cover or
    // contain the browser crops or letterboxes instead of stretching — the slider
    // on the live homepage keeps a 1738 × 500 box over a 2730 × 1536 file that
    // way, on purpose — so there is nothing to warn about and nothing to refit.
    function distortionVisible(m) {
        if (!sizeDistorted(m)) return false;
        const el = m.getEl && m.getEl();
        const fit = el ? el.ownerDocument.defaultView.getComputedStyle(el).objectFit : '';
        return !fit || fit === 'fill';
    }
    // Sets the height from the width (or, with no width, adopts the file's size).
    // A styled box gets the same treatment: its width is kept, its height follows.
    function fitToRatio(m) {
        const nat = naturalSize(m);
        if (!nat) return false;
        const box = styledBox(m);
        if (box) m.addStyle({ height: Math.max(1, Math.round(box.w * nat.h / nat.w)) + 'px' });
        const w = attrInt(m, 'width');
        if (w) m.addAttributes({ width: String(w), height: String(Math.max(1, Math.round(w * nat.h / nat.w))) });
        else m.addAttributes({ width: String(nat.w), height: String(nat.h) });
        return true;
    }
    // Runs fn once the file's size is known — now, if the canvas <img> already
    // shows that very file, else after measuring it off-canvas. The canvas element
    // is not enough on its own: after a src change it keeps the OLD picture as its
    // current request (still `complete`, still the old naturalWidth) until the new
    // one arrives, and a lazy-loading image out of view, or in a background tab,
    // never loads at all. A detached Image() has neither problem, and the browser
    // serves it from the same cache the canvas fills.
    function whenLoaded(m, fn) {
        const el = m && m.getEl && m.getEl();
        if (!el || el.tagName !== 'IMG') return;
        if (loadedCanvasImage(m)) { fn(); return; }
        const want = wantedSrc(m, el);
        if (!want) return;
        const probe = new Image();
        probe.onload = () => {
            if (probe.naturalWidth) naturalSizes.set(m, { w: probe.naturalWidth, h: probe.naturalHeight });
            fn();
        };
        probe.src = want;
    }

    function installImageSizeTrait() {
        editor.TraitManager.addType(IMG_SIZE_TRAIT, {
            createInput({ trait }) {
                const wrap = document.createElement('div');
                wrap.className = 'elevare-img-size';
                wrap.innerHTML =
                    '<div class="elevare-img-size-row">' +
                        `<label><span>${ui('sizeWidth')}</span><input type="number" min="1" step="1" data-dim="width" placeholder="px"></label>` +
                        '<span class="elevare-img-size-x">×</span>' +
                        `<label><span>${ui('sizeHeight')}</span><input type="number" min="1" step="1" data-dim="height" placeholder="px"></label>` +
                        `<button type="button" class="elevare-img-size-lock" title="${ui('sizeLock')}"></button>` +
                    '</div>' +
                    '<div class="elevare-img-size-info"><span class="elevare-img-size-nat"></span>' +
                        `<button type="button" class="elevare-img-size-fit">${ui('sizeFit')}</button></div>` +
                    `<div class="elevare-img-size-warn">${ui('sizeWarn')}</div>`;
                wrap.querySelector('.elevare-img-size-lock').addEventListener('click', () => {
                    const m = trait.target;
                    ratioLocks.set(m, !ratioLocked(m));
                    if (ratioLocked(m) && attrInt(m, 'width') && attrInt(m, 'height')) fitToRatio(m);
                    renderImageSize(wrap, m);
                });
                wrap.querySelector('.elevare-img-size-fit').addEventListener('click', () => {
                    fitToRatio(trait.target);
                    renderImageSize(wrap, trait.target);
                });
                // Per box, on `change` (not `input`): a 1 → 12 → 120 being typed must
                // not rewrite the other box three times on the way.
                wrap.querySelectorAll('input[data-dim]').forEach((input) => {
                    input.addEventListener('change', () => {
                        const m = trait.target;
                        const dim = input.dataset.dim;
                        const v = parseInt(input.value, 10);
                        if (!Number.isFinite(v) || v <= 0) {
                            m.removeAttributes([dim]);
                        } else {
                            const attrs = { [dim]: String(v) };
                            const nat = naturalSize(m);
                            if (ratioLocked(m) && nat) {
                                attrs[dim === 'width' ? 'height' : 'width'] = String(Math.max(1, Math.round(
                                    dim === 'width' ? v * nat.h / nat.w : v * nat.w / nat.h)));
                            }
                            m.addAttributes(attrs);
                        }
                        renderImageSize(wrap, m);
                    });
                });
                return wrap;
            },
            // Everything is written straight to the attributes above; GrapesJS's own
            // value plumbing has nothing to carry for this trait.
            onEvent() { },
            onUpdate({ elInput, trait }) {
                sizePanels.set(trait.target, elInput);
                renderImageSize(elInput, trait.target);
                // A page still opening: the file's size arrives with the load event.
                whenLoaded(trait.target, () => renderImageSize(elInput, trait.target));
            }
        });

        // A different picture in the same slot: keep the width the author chose and
        // refit the height, so the swap never leaves a squashed image behind.
        editor.on('component:update:src', (m) => {
            if ((m.get('tagName') || '').toUpperCase() !== 'IMG') return;
            naturalSizes.delete(m);
            // Next tick: the view copies the new src to the canvas element after
            // this event, and until it has, `complete` still describes the old file.
            setTimeout(() => whenLoaded(m, () => {
                if (ratioLocked(m) && attrInt(m, 'width') && attrInt(m, 'height') && distortionVisible(m)) fitToRatio(m);
                renderImageSize(sizePanels.get(m), m);
            }), 0);
        });
    }

    function renderImageSize(wrap, m) {
        if (!wrap || !m) return;
        const w = attrInt(m, 'width'), h = attrInt(m, 'height');
        const wIn = wrap.querySelector('input[data-dim="width"]');
        const hIn = wrap.querySelector('input[data-dim="height"]');
        if (document.activeElement !== wIn) wIn.value = w || '';
        if (document.activeElement !== hIn) hIn.value = h || '';
        wrap.querySelector('.elevare-img-size-lock').classList.toggle('is-locked', ratioLocked(m));
        const nat = naturalSize(m);
        wrap.querySelector('.elevare-img-size-nat').textContent = nat
            ? `${ui('sizeFile')}: ${nat.w} × ${nat.h} px (${ui('sizeRatio')} ${(nat.w / nat.h).toFixed(2)})`
            : ui('sizeUnknown');
        wrap.querySelector('.elevare-img-size-fit').disabled = !nat;
        wrap.classList.toggle('is-distorted', distortionVisible(m));
    }

    // --------------------------------------------------------------
    // SIDE PANEL — one right-hand column, four icon tabs, our own grouping
    // --------------------------------------------------------------
    // GrapesJS's stock chrome puts Blocks, Layers, Traits and Styles behind
    // four icons in one right-hand column, and the preset switches to Styles on
    // every click. "Where do I change this?" had three answers (the gear, the
    // brush, the text toolbar) and the author had to know the editor's
    // internals to pick. The column and the icons stay — a second column was
    // tried and cost the canvas a third of the screen next to the CMS's own
    // sidebar and tool rail — but what is behind them is regrouped: İçerik
    // (what the selected element is: its text tag, image, link, block settings,
    // Advanced last), Görünüm (how it looks: classes and styles), Katmanlar,
    // Bloklar (searchable, recents on top). The managers are rendered into
    // these containers by GrapesJS itself (appendTo), so nothing about how they
    // work changed; only how they are arranged.
    //
    // The shell has to exist before grapesjs.init reads the appendTo selectors,
    // hence two steps: build, then wire after init.
    const SIDE = {
        right: 'pe-side-right',
        blocks: 'pe-blocks', layers: 'pe-layers', traits: 'pe-traits', selectors: 'pe-selectors', styles: 'pe-styles'
    };
    const SIDE_TABS = [
        { id: 'pe-traits', key: 'content', icon: 'bi-pencil-square' },
        { id: 'pe-styles', key: 'look', icon: 'bi-brush' },
        { id: 'pe-layers', key: 'layers', icon: 'bi-layers' },
        { id: 'pe-blocks', key: 'blocks', icon: 'bi-grid-3x3-gap' }
    ];
    const RECENT_KEY = 'elevare-recent-blocks';
    const RECENT_MAX = 6;

    function hasSidePanels() {
        return !!document.getElementById(SIDE.right);
    }

    function buildSidePanelShell() {
        const right = document.getElementById(SIDE.right);
        if (!right) return false;
        right.innerHTML =
            `<div class="pe-side-tabs" role="tablist">` +
                SIDE_TABS.map((tab, i) =>
                    `<button type="button" class="pe-side-tab${i === 0 ? ' is-active' : ''}" data-tab="${tab.id}" role="tab" title="${ui(tab.key)}" aria-label="${ui(tab.key)}">` +
                        `<i class="bi ${tab.icon}"></i><span>${ui(tab.key)}</span>` +
                    `</button>`).join('') +
            `</div>` +
            `<div class="pe-side-head">` +
                `<div class="pe-side-selected"><span class="pe-side-selected-label">${ui('selected')}</span><strong class="pe-side-selected-name"></strong></div>` +
                `<div class="pe-side-tpl" hidden>` +
                    `<div class="pe-side-tpl-title"><i class="bi bi-link-45deg"></i> <span>${ui('tplLinked')}</span>: <strong class="pe-side-tpl-name"></strong></div>` +
                    `<p class="pe-side-tpl-hint">${ui('tplHint')}</p>` +
                    `<div class="pe-side-tpl-actions">` +
                        `<button type="button" class="btn-cms btn-primary-cms btn-sm-cms" data-cmd="${TPL_EDIT_COMMAND}"><i class="bi bi-pencil"></i> ${ui('tplEdit')}</button>` +
                        `<button type="button" class="btn-cms btn-ghost btn-sm-cms" data-cmd="${TPL_UNLINK_COMMAND}"><i class="bi bi-scissors"></i> ${ui('tplUnlink')}</button>` +
                    `</div>` +
                `</div>` +
            `</div>` +
            `<div class="pe-side-nothing">${ui('nothing')}</div>` +
            `<div class="pe-side-pane is-active" id="${SIDE.traits}"></div>` +
            `<div class="pe-side-pane" id="${SIDE.styles}"><div id="${SIDE.selectors}"></div><div class="pe-styles-host"></div></div>` +
            `<div class="pe-side-pane" id="${SIDE.layers}"></div>` +
            `<div class="pe-side-pane" id="${SIDE.blocks}">` +
                `<input type="search" class="pe-block-search cms-input" placeholder="${ui('blockSearch')}" aria-label="${ui('blockSearch')}">` +
                `<div class="pe-blocks-host"></div>` +
                `<div class="pe-blocks-empty" hidden>${ui('noBlocks')}</div>` +
            `</div>`;
        return true;
    }

    // appendTo targets for grapesjs.init — only when the shell is on the page,
    // so a host without it (an old layout, a test page) gets GrapesJS's default UI.
    function sidePanelManagerConfig() {
        if (!hasSidePanels()) return {};
        return {
            blockManager: { appendTo: `#${SIDE.blocks} .pe-blocks-host` },
            layerManager: { appendTo: `#${SIDE.layers}` },
            traitManager: { appendTo: `#${SIDE.traits}` },
            // componentFirst: style the SELECTED element's own rule first, not its
            // class. The blocks ship inline styles that GrapesJS turns into per-id
            // rules (#ixxxx{font-size:…}) AND a shared class (.el-card-link). Without
            // this, the Style Manager edits the class while the higher-specificity id
            // rule keeps winning — so changing a card link's font size looked like it
            // did nothing. Styling the component's own id makes the change apply, and
            // the class chip is still there to style all instances at once on purpose.
            selectorManager: { appendTo: `#${SIDE.selectors}`, componentFirst: true },
            styleManagerAppendTo: `#${SIDE.styles} .pe-styles-host`
        };
    }

    function wireSidePanels() {
        if (!hasSidePanels()) return;
        const right = document.getElementById(SIDE.right);

        // The preset re-creates GrapesJS's "views" column (the four toggle
        // buttons and their container) after init regardless of the panel
        // config; both are empty shells here and the container would still take
        // 15 % of the canvas width.
        ['views', 'views-container'].forEach((id) => { try { editor.Panels.removePanel(id); } catch (e) { } });

        const showTab = (id) => {
            right.querySelectorAll('.pe-side-tab').forEach((b) => b.classList.toggle('is-active', b.dataset.tab === id));
            right.querySelectorAll('.pe-side-pane').forEach((pane) => pane.classList.toggle('is-active', pane.id === id));
            // The selected-element header belongs to the two element tabs only.
            right.classList.toggle('is-element-tab', id === SIDE.traits || id === SIDE.styles);
        };
        right.querySelectorAll('.pe-side-tab').forEach((tab) => tab.addEventListener('click', () => showTab(tab.dataset.tab)));
        showTab(SIDE.traits);
        // A click on the canvas while the block list is showing means "now this
        // one": switch to its content. From Katmanlar or Görünüm the author's
        // tab stays — selecting through the layer tree must not throw them out
        // of it, and styling several elements in a row must not flip back.
        editor.on('component:selected', () => {
            const active = right.querySelector('.pe-side-tab.is-active');
            if (active && active.dataset.tab === SIDE.blocks) showTab(SIDE.traits);
        });

        // The linked-template box: the two toolbar glyphs it replaces were the
        // only sign that a header came from a template, and easy to miss.
        right.querySelectorAll('[data-cmd]').forEach((btn) => {
            btn.addEventListener('click', () => { try { editor.runCommand(btn.dataset.cmd); } catch (e) { } });
        });

        const refreshSelection = () => {
            const m = editor.getSelected();
            right.classList.toggle('has-selection', !!m);
            const nameEl = right.querySelector('.pe-side-selected-name');
            const tplBox = right.querySelector('.pe-side-tpl');
            if (!m) { nameEl.textContent = ''; tplBox.hidden = true; return; }
            nameEl.textContent = m.getName ? m.getName() : (m.get('tagName') || '');
            const ref = m.get('type') === TPL_REF_TYPE ? m : (m.closest ? m.closest('.' + TPL_REF_CLASS) : null);
            if (ref) {
                const id = ref.getAttributes()['data-elevare-template-id'];
                const known = id != null ? templateVersions[id] : null;
                tplBox.querySelector('.pe-side-tpl-name').textContent = (known && (known.name ?? known.Name)) || ('#' + id);
                // The commands act on the selection; point it at the wrapper so
                // "edit" and "cut" mean the template, whichever locked child was hit.
                if (ref !== m) editor.select(ref);
                tplBox.hidden = false;
            } else {
                tplBox.hidden = true;
            }
        };
        editor.on('component:selected component:deselected component:toggled', refreshSelection);
        editor.on('load', refreshSelection);
        refreshSelection();

        installBlockSearch(right);
        installRecentBlocks();
    }

    // Filters the block list as you type: fifty blocks in six categories is
    // more than anyone scans. Matches the label text, case- and accent-tolerant.
    function installBlockSearch(side) {
        const input = side.querySelector('.pe-block-search');
        const empty = side.querySelector('.pe-blocks-empty');
        if (!input) return;
        const norm = (s) => (s || '').toLocaleLowerCase('tr').normalize('NFD').replace(/[\u0300-\u036f]/g, '');
        input.addEventListener('input', () => {
            const q = norm(input.value.trim());
            const host = side.querySelector('.pe-blocks-host');
            let shown = 0;
            host.querySelectorAll('.gjs-block').forEach((el) => {
                const hit = !q || norm(el.textContent).includes(q);
                el.style.display = hit ? '' : 'none';
                if (hit) shown++;
            });
            // A category with no hit is hidden; one with hits opens (the open
            // state lives on the model, so it is set there, not on the class).
            // Clearing the search puts the list back to how it opens: closed,
            // but for the recents.
            const cats = editor.BlockManager.getCategories();
            host.querySelectorAll('.gjs-block-category').forEach((catEl) => {
                const any = [...catEl.querySelectorAll('.gjs-block')].some((b) => b.style.display !== 'none');
                catEl.style.display = any ? '' : 'none';
                const title = (catEl.querySelector('.gjs-title') || {}).textContent || '';
                const model = cats.find((c) => (c.get('label') || c.getId()) === title.trim());
                if (model) model.set('open', q ? any : model.getId() === 'elevare-recent');
            });
            empty.hidden = shown > 0;
        });
    }

    // The last few blocks dragged in, as a category of their own at the top of
    // the list. A block used once is usually used again on the same page; the
    // categories below start collapsed (see the load handler) so this is what
    // the panel opens on.
    function installRecentBlocks() {
        const bm = editor.BlockManager;
        const load = () => { try { return JSON.parse(localStorage.getItem(RECENT_KEY) || '[]'); } catch (e) { return []; } };
        const save = (ids) => { try { localStorage.setItem(RECENT_KEY, JSON.stringify(ids)); } catch (e) { /* private mode */ } };
        const recentId = (id) => 'recent:' + id;
        // Categories render in the order they were first seen, which puts a
        // category created after load at the bottom; it is moved to the top.
        const hoist = () => {
            const host = document.querySelector('#' + SIDE.blocks + ' .pe-blocks-host');
            if (!host) return;
            const cat = [...host.querySelectorAll('.gjs-block-category')]
                .find((c) => ((c.querySelector('.gjs-title') || {}).textContent || '').trim() === ui('recent'));
            if (cat && cat.parentElement && cat.parentElement.firstElementChild !== cat) cat.parentElement.prepend(cat);
        };
        const sync = (ids) => {
            bm.getAll().filter((b) => String(b.getId()).startsWith('recent:')).forEach((b) => bm.remove(b.getId()));
            // Added in reverse so the newest ends up first in a category that
            // renders in insertion order.
            [...ids].reverse().forEach((id) => {
                const src = bm.get(id);
                if (!src) return;
                bm.add(recentId(id), { ...src.attributes, id: recentId(id), category: { id: 'elevare-recent', label: ui('recent'), open: true } });
            });
            setTimeout(hoist, 0);
        };
        editor.on('load', () => setTimeout(hoist, 0));
        editor.on('block:drag:stop', (comp, block) => {
            if (!block) return;
            let id = String(block.getId());
            if (id.startsWith('recent:')) id = id.slice('recent:'.length);
            const ids = [id, ...load().filter((x) => x !== id)].slice(0, RECENT_MAX);
            save(ids);
            sync(ids);
        });
        sync(load());
    }

    // --------------------------------------------------------------
    // IMAGE LOADING DEFAULT — lazy unless the author says otherwise
    // --------------------------------------------------------------
    // An <img> with no loading attribute is eager in every browser, so a page
    // whose author never touched the field loaded every picture up front and
    // the SEO panel nagged about "multiple eager images" on pages nobody had
    // made a choice on. The choice is now made for them: every image gets
    // loading="lazy" the moment it exists in the editor (dropped, pasted,
    // restored from a saved page), and the author switches the one picture at
    // the top to eager — which is also what the analyser's above-the-fold check
    // and the public site's LCP preload look for. Written as a real attribute so
    // the published HTML carries it; run before the clean-signature baseline on
    // load so opening an old page does not count as an edit.
    function defaultImageLoading(component) {
        if (!component) return;
        const apply = (m) => {
            if ((m.get('tagName') || '').toUpperCase() !== 'IMG') return;
            const attrs = m.getAttributes();
            if (!attrs.loading) m.addAttributes({ loading: 'lazy' });
        };
        // Walked through the model, not find(): find() is DOM-based and empty
        // before the view exists (see the size trait), and on load it must reach
        // every image of a restored page.
        (function walk(m) { apply(m); m.components().forEach(walk); })(component);
    }

    // --------------------------------------------------------------
    // TEXT TAG TRAIT — make this text an <h1>, an <h2>, a <p>…
    // --------------------------------------------------------------
    // The rich-text toolbar's Format menu (Normal / Heading 1 …) cannot do this:
    // it changes blocks INSIDE the element being edited, and CKEditor hides it
    // altogether when the element itself is a heading or a paragraph — the
    // common case here, since every text component is its own editable. So the
    // one thing an author most needs for a page's outline, "this title is the
    // H1", had no control at all. The tag is the component's own; changing it
    // means a new element, so the component is rebuilt from its JSON with the
    // new tagName and its id, styles and children carried across (the same
    // round trip the Buton block uses when it turns into a link).
    const TEXT_TAG_OPTIONS = [
        { id: 'p', name: ui('tagP') },
        ...[1, 2, 3, 4, 5, 6].map((n) => ({ id: 'h' + n, name: `${ui('tagH')} ${n} (h${n})` })),
        { id: 'div', name: ui('tagDiv') }, { id: 'span', name: ui('tagSpan') }
    ];
    const TEXT_TAG_SET = new Set(TEXT_TAG_OPTIONS.map((o) => o.id));

    function retagComponent(m, tag) {
        if (!m || !tag || !TEXT_TAG_SET.has(tag)) return;
        if ((m.get('tagName') || '').toLowerCase() === tag) return;
        const oldId = m.getId();
        // Rules keyed to the id go with the component when it is removed
        // (keepUnusedStyles is off); capture and put them back on the new one.
        const rules = editor.Css.getRules('#' + oldId).map((r) => ({
            selector: r.selectorsToString(), style: { ...r.getStyle() },
            atRuleType: r.get('atRuleType'), atRuleParams: r.get('atRuleParams')
        }));
        // toJSON().components is a live collection; the round trip detaches it.
        const json = JSON.parse(JSON.stringify(m.toJSON()));
        json.tagName = tag;
        delete json.elevareTag;
        const created = m.replaceWith(json)[0];
        if (!created) return;
        created.setId(oldId);
        rules.forEach((r) => editor.Css.setRule(r.selector, r.style, { atRuleType: r.atRuleType, atRuleParams: r.atRuleParams }));
        editor.select(created);
    }

    // --------------------------------------------------------------
    // SEO / ACCESSIBILITY TRAITS (per selected component)
    // --------------------------------------------------------------
    // Every trait here (without changeProp) reads/writes a REAL HTML
    // attribute, so existing values show up correctly in the panel.
    function installSeoTraits() {
        editor.on('component:selected', (model) => {
            const traits = model.get('traits');
            const tag = (model.get('tagName') || '').toUpperCase();
            // A trait the component already has (GrapesJS's own alt/href/id, a
            // block's) keeps its type and value but takes our label and group,
            // so the panel reads in one language and one order.
            const addOnce = (def) => {
                const existing = traits.where({ name: def.name })[0];
                if (existing) {
                    const patch = {};
                    if (def.label !== undefined) patch.label = def.label;
                    if (def.category !== undefined) patch.category = def.category;
                    if (def.elevareOrder !== undefined) patch.elevareOrder = def.elevareOrder;
                    existing.set(patch);
                } else {
                    traits.add(def);
                }
            };

            // See FORCE_EDITABLE_TAGS above: only touch genuine text leaves so
            // containers aren't made editable.
            //
            // "Leaf" is NOT "has no components". GrapesJS wraps even a plain string
            // in a child component of type 'textnode', so <p>Merhaba</p> reports one
            // child, not zero — which meant the old `!components().length` test was
            // true only for a completely EMPTY element and this whole safety net
            // never fired for the elements it exists for. Verified against the live
            // editor: every plain text leaf on the page already reported one
            // textnode child. What makes an element a text leaf is that ALL of its
            // children are textnodes; one <svg>, <img> or <span> among them means it
            // is a container and must stay untouched.
            const kids = model.components();
            const textOnly = kids.length === 0
                || kids.every((c) => c.get('type') === 'textnode');
            // Not the Kod Bloğu's code: it is locked on purpose and edited in its own
            // box — the rich-text editor would rewrite it (elevare-blocks.js).
            const inCodeBlock = !!(model.closest && model.closest('[data-elevare-code]'));
            if (FORCE_EDITABLE_TAGS.includes(tag) && !model.get('editable') && textOnly && !inCodeBlock) {
                model.set('editable', true);
            }

            // Text of any kind — a plain text component, or one of the tags an
            // author writes prose in — gets the tag switch. Not inside a linked
            // template on a page (that content is the template's to change), and
            // not on the library's own blocks, which own their markup.
            const lowerTag = tag.toLowerCase();
            const isText = model.get('type') === 'text' || (TEXT_TAG_SET.has(lowerTag) && textOnly);
            const inLinkedTemplate = !!(model.closest && model.closest('.' + TPL_REF_CLASS));
            if (isText && TEXT_TAG_SET.has(lowerTag) && !inLinkedTemplate && !model.getAttributes()['data-elevare-block']) {
                model.set('elevareTag', lowerTag, { silent: true });
                addOnce({ type: 'select', label: ui('tag'), name: 'elevareTag', changeProp: 1, options: TEXT_TAG_OPTIONS, category: ui('catContent') });
                if (!model.__elevareTagGuard) {
                    model.__elevareTagGuard = true;
                    model.on('change:elevareTag', () => retagComponent(model, model.get('elevareTag')));
                }
            }

            // See ARIA_LABEL_TAGS above: only offer it where it cannot silence real
            // visible text (icon-only links/buttons, or a landmark that needs a name).
            // GrapesJS's own id/title traits, and everything an author rarely
            // touches, sit under Gelişmiş; what they came for is on top.
            const ADV = ui('catAdvanced');
            traits.where({ name: 'id' }).forEach((tr) => tr.set({ category: ADV, label: ui('id') }));
            traits.where({ name: 'title' }).forEach((tr) => tr.set({ category: ADV, label: ui('title') }));
            if (ARIA_LABEL_TAGS.includes(tag)) {
                addOnce({ type: 'text', label: ui('aria'), name: 'aria-label', category: ADV });
            }

            if (tag === 'IMG') {
                // Swapping the picture had no home in this panel at all: the only
                // route was a double-click on the canvas, which cannot reach an
                // image with anything drawn over it and does not exist at all when
                // the image was selected from the Layer Manager. Both go first,
                // above alt/title, because "change this picture" is the reason
                // someone selects an image far more often than SEO metadata is.
                const C = ui('catContent');
                addOnce({
                    type: 'button', name: 'elevare-pick-image', label: ui('image'),
                    text: ui('pickImage'), full: true, category: C, elevareOrder: 1,
                    command: (ed) => openImagePicker(ed.getSelected())
                });
                // changeProp, not an attribute trait: the image component keeps its
                // source in the model property `src`, and a plain attribute trait
                // would write a second, shadowed one.
                addOnce({ type: 'text', label: ui('imageUrl'), name: 'src', changeProp: 1, category: C, elevareOrder: 2 });
                addOnce({ type: 'text', label: ui('alt'), name: 'alt', category: C, elevareOrder: 3 });
                // CLS: explicit width/height lets the browser reserve space — and,
                // see IMG_SIZE_TRAIT, in the file's own proportions.
                addOnce({ type: IMG_SIZE_TRAIT, label: false, name: IMG_SIZE_TRAIT, category: C, elevareOrder: 4 });
                // No "(default)" entry: an image without the attribute is eager by
                // browser default, and that is the wrong default for everything but
                // the one picture at the top — see defaultImageLoading.
                addOnce({
                    type: 'select', label: ui('loading'), name: 'loading', category: C, elevareOrder: 5,
                    options: [
                        { id: 'lazy', name: ui('lazy') },
                        { id: 'eager', name: ui('eager') }
                    ]
                });
            }

            if (tag === 'IFRAME') {
                traits.where({ name: 'title' }).forEach((tr) => tr.set({ category: ui('catContent'), label: ui('iframeTitle') }));
                addOnce({ type: 'text', label: ui('iframeTitle'), name: 'title', category: ui('catContent') });
                addOnce({
                    type: 'select', label: ui('loading'), name: 'loading', category: ui('catContent'),
                    options: [{ id: '', name: ui('eager') }, { id: 'lazy', name: ui('lazy') }]
                });
            }

            // The Buton block carries its own link fields (Bağlantı group) and swaps
            // its tag to match; a second Href/Target on the same panel would only
            // compete with them.
            if (tag === 'A' && !traits.where({ name: 'linkUrl' }).length) {
                // Pick a page of this site instead of typing its slug — the
                // searchable picker elevare-blocks.js registers ('elevare-page'),
                // the same one the Buton and Page Listing blocks use. Writes href;
                // the field below still shows and accepts anything.
                const LK = ui('catLink');
                addOnce({ type: 'elevare-page', label: ui('sitePage'), name: 'elevareLinkPage', changeProp: 1, category: LK });
                if (!model.__elevareLinkPageGuard) {
                    model.__elevareLinkPageGuard = true;
                    model.on('change:elevareLinkPage', () => {
                        const page = pageDirectory.find((p) => String(p.id) === String(model.get('elevareLinkPage')));
                        if (page) model.addAttributes({ href: '/' + page.label });
                    });
                }
                addOnce({ type: 'text', label: ui('href'), name: 'href', category: LK });
                addOnce({
                    type: 'select', label: ui('target'), name: 'target', category: LK,
                    options: [
                        { id: '', name: ui('sameTab') },
                        { id: '_blank', name: ui('newTab') }
                    ]
                });
                addOnce({ type: 'text', label: ui('rel'), name: 'rel', category: ADV });
                // Auto-hygiene: target=_blank without rel=noopener is both a
                // security and an SEO-analyzer finding — fix it as it happens.
                if (!model.__elevareRelGuard) {
                    model.__elevareRelGuard = true;
                    model.on('change:attributes', () => {
                        const a = model.getAttributes();
                        if (a.target === '_blank' && !/noopener/.test(a.rel || '')) {
                            model.addAttributes({ rel: ((a.rel || '') + ' noopener noreferrer').trim() });
                        }
                    });
                }
            }

            if (tag === 'INPUT' || tag === 'TEXTAREA') {
                addOnce({ type: 'text', label: 'Placeholder', name: 'placeholder' });
                addOnce({ type: 'text', label: 'Name', name: 'name' });
            }

            // Fallback text editing from the Settings panel for A/BUTTON.
            // The value is refreshed on EVERY selection so it always reflects
            // the latest inline-edited text.
            if (TEXT_TRAIT_TAGS.includes(tag)) {
                if (!traits.where({ name: 'text-content' }).length) {
                    traits.add({
                        type: 'text',
                        label: 'Text',
                        name: 'text-content',
                        changeProp: 1,
                    });
                    model.on('change:text-content', () => {
                        const val = model.get('text-content');
                        if (val != null) model.components(val);
                    });
                }
                const el = model.getEl();
                if (el) model.set('text-content', el.textContent || '', { silent: true });
            }

            // Reads top-down as an author thinks: what it is (İçerik), where it
            // goes (Bağlantı), the block's own settings, and last the things one
            // rarely touches (Gelişmiş). Categories render in first-seen order,
            // so the traits themselves are put in that order.
            // A block's own settings, which come without a group, get one —
            // GrapesJS renders ungrouped traits after every group, which would
            // put them below Gelişmiş.
            traits.forEach((tr) => { if (!tr.get('category')) tr.set({ category: ui('catSettings') }); });
            const rank = (tr) => {
                const c = tr.get('category');
                const id = c && typeof c === 'object' ? (c.id || c.label) : c;
                if (id === ui('catContent')) return 0;
                if (id === ui('catLink')) return 1;
                if (id === ADV) return 3;
                return 2;
            };
            const sub = (tr) => tr.get('elevareOrder') ?? 50;
            const ordered = [...traits.models].sort((a, b) => (rank(a) - rank(b)) || (sub(a) - sub(b)));
            if (ordered.some((tr, idx) => tr !== traits.models[idx])) traits.reset(ordered);
        });
    }

    // --------------------------------------------------------------
    // STYLE MANAGER GAPS
    // --------------------------------------------------------------
    // Things GrapesJS's defaults leave out that this CMS's users hit immediately.
    // Added to the built properties after init rather than declared in the
    // styleManager config, because that config uses buildProps — these properties do
    // not exist as objects until GrapesJS has built them. Anything that has to be in
    // place BEFORE a property's view is constructed (its type, for one) cannot be
    // done here and is declared in the config instead — see `position` there.
    function installStyleManagerExtras() {
        try {
            // `position: sticky` alone does nothing: the spec needs at least one
            // inset threshold to know where to stop the element, and the default
            // `top: auto` is not one. So picking sticky and then watching the header
            // scroll away is not a mistake the author made — it is what the CSS
            // says. The threshold is filled in once, and only when they have not
            // set one themselves, so a deliberate `top: 80px` is never overwritten.
            editor.on('component:styleUpdate:position', (component) => {
                try {
                    const style = component.getStyle();
                    if (style.position !== 'sticky') return;
                    if (style.top && style.top !== 'auto') return;
                    component.addStyle({ top: '0' });
                } catch (e) {
                    console.warn('sticky top default failed', e);
                }
            });

            // Extra shipped opacity/transition/transform. `transition` only animates a
            // property WHEN IT CHANGES, so on its own it produces no motion at all —
            // people reach for it expecting an animation and get nothing. These are
            // the pieces that actually make something move, and they are deliberately
            // NOT a replacement for the Animasyon trait: that one plays once as an
            // element scrolls into view, these run continuously.
            //
            // The keyframes themselves live in the site's base CSS (Site Codes), not
            // here — they have to exist on the published page, and a page only carries
            // the CSS it was saved with.
            const sm = editor.StyleManager;
            const already = sm.getSector('extra').get('properties').map((p) => p.get('property'));
            const add = (def) => { if (!already.includes(def.property)) sm.addProperty('extra', def, {}); };

            // transform without an origin is half a control: every rotate and scale
            // pivots from the centre and there was no way to say otherwise.
            add({ property: 'transform-origin', type: 'text', default: '' });
            add({
                property: 'animation-name', type: 'select', default: 'none',
                options: [
                    { id: 'none', label: 'Yok' },
                    { id: 'elevare-pulse', label: 'Nabız' },
                    { id: 'elevare-float', label: 'Süzülme' },
                    { id: 'elevare-spin', label: 'Dönme' }
                ]
            });
            add({ property: 'animation-duration', type: 'text', default: '2s' });
            add({
                property: 'animation-iteration-count', type: 'select', default: 'infinite',
                options: [{ id: 'infinite', label: 'Sürekli' }, { id: '1', label: 'Bir kez' }]
            });
        } catch (e) {
            console.warn('style manager extras failed', e);
        }
    }

    // --------------------------------------------------------------
    // BRAND COLOURS IN EVERY COLOUR PICKER
    // --------------------------------------------------------------
    // The site's palette, offered next to the ordinary colour picker. Picking one
    // writes `var(--elevare-primary, #2563eb)` — the variable, not the hex it
    // currently resolves to — so the element keeps following Site Codes when the
    // brand changes, exactly like the blocks do. That is the whole point: a hex
    // picked out of the same palette looks identical today and stops tracking the
    // brand tomorrow.
    //
    // The fallback after the comma is the value the SEEDED palette gives that token
    // — DatabaseSeeder's "Marka Renkleri (CSS Değişkenleri)" Site Code, `:root`
    // block. So "the variable is gone" degrades to "the site looks like a fresh
    // install", which is a state an author can recognise and recover from, rather
    // than to a colour that never belonged to this product.
    //
    // The light values, deliberately: a var() fallback is one static string and
    // cannot follow [data-theme="dark"]. `:root` is the base the dark block
    // overrides, so it is the one that means "default".
    //
    // Keep this list in step with the seeder. It is duplicated rather than fetched
    // because the picker has to name a colour before any request could answer, and
    // a palette the operator has edited is already being read live from the canvas
    // (see resolveBrandToken) — this half only ever shows up when the variable is
    // missing entirely.
    const BRAND_TOKENS = [
        { name: '--elevare-primary', fallback: '#000000', label: 'Ana renk' },
        { name: '--elevare-on-primary', fallback: '#ffffff', label: 'Ana renk üstü yazı' },
        { name: '--elevare-secondary', fallback: '#404040', label: 'İkincil renk' },
        { name: '--elevare-heading', fallback: '#111111', label: 'Başlık' },
        { name: '--elevare-text', fallback: '#111111', label: 'Gövde metni' },
        { name: '--elevare-text-2', fallback: '#333333', label: 'İkincil metin' },
        { name: '--elevare-muted', fallback: '#666666', label: 'Soluk metin' },
        { name: '--elevare-link', fallback: '#000000', label: 'Bağlantı' },
        { name: '--elevare-bg', fallback: '#ffffff', label: 'Sayfa zemini' },
        { name: '--elevare-surface', fallback: '#f5f5f5', label: 'Yüzey' },
        { name: '--elevare-surface-2', fallback: '#eeeeee', label: 'Yüzey 2' },
        { name: '--elevare-border', fallback: '#dddddd', label: 'Kenarlık' },
        { name: '--elevare-code-bg', fallback: '#1a1a1a', label: 'Kod bloğu zemini' }
    ];

    let openSwatchPanel = null;

    function closeSwatchPanel() {
        if (openSwatchPanel) {
            openSwatchPanel.remove();
            openSwatchPanel = null;
        }
    }

    /// The swatch shows what the token resolves to RIGHT NOW, read from the canvas —
    /// which loads Site Codes' CSS (see the canvas.styles config). So the list shows
    /// this site's actual brand colours, not the defaults this file happens to carry.
    /// The literal fallback is only for a canvas that is not ready or has no palette.
    function resolveBrandToken(token) {
        try {
            const doc = editor.Canvas.getDocument();
            if (doc && doc.documentElement) {
                const value = getComputedStyle(doc.documentElement).getPropertyValue(token.name).trim();
                if (value) return value;
            }
        } catch (e) {
            // Canvas iframe not mounted yet — fall through to the literal.
        }
        return token.fallback;
    }

    function buildSwatchPanel(property) {
        const panel = document.createElement('div');
        panel.className = 'elevare-swatch-panel';

        BRAND_TOKENS.forEach((token) => {
            const row = document.createElement('button');
            row.type = 'button';
            row.className = 'elevare-swatch-row';

            const chip = document.createElement('span');
            chip.className = 'elevare-swatch-chip';
            chip.style.background = resolveBrandToken(token);

            const label = document.createElement('span');
            label.className = 'elevare-swatch-label';
            label.textContent = token.label;

            const code = document.createElement('span');
            code.className = 'elevare-swatch-code';
            code.textContent = token.name.replace('--elevare-', '');

            row.append(chip, label, code);
            row.addEventListener('click', (event) => {
                event.stopPropagation();
                // upValue, not the field's own input: the colour field parses whatever
                // is typed into it and collapses `var(...)` to #000000. Measured.
                property.upValue('var(' + token.name + ', ' + token.fallback + ')');
                closeSwatchPanel();
            });
            panel.appendChild(row);
        });

        return panel;
    }

    function attachSwatchButton(property) {
        const el = property.view && property.view.el;
        if (!el || el.getAttribute('data-elevare-swatch')) return;
        const fields = el.querySelector('.gjs-fields');
        if (!fields) return;

        el.setAttribute('data-elevare-swatch', '1');

        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'elevare-swatch-btn';
        button.title = 'Marka renkleri';
        button.setAttribute('aria-label', 'Marka renkleri');
        button.addEventListener('click', (event) => {
            event.stopPropagation();
            const wasMine = openSwatchPanel && openSwatchPanel.parentElement === el;
            closeSwatchPanel();
            if (wasMine) return;   // second click on the same button closes it
            const panel = buildSwatchPanel(property);
            panel.addEventListener('click', (inner) => inner.stopPropagation());
            el.appendChild(panel);
            openSwatchPanel = panel;
        });

        fields.appendChild(button);
    }

    /// Colour properties are not all top-level: `border-color` lives inside the
    /// composite `border`, and the shadow colours inside their stacks. Those nested
    /// views exist as soon as the parent is built (detached until their layer is
    /// opened, and they bring the button with them when they are inserted), so one
    /// recursive pass covers every picker in the panel.
    function collectColorProperties(property, found) {
        if (property.getType && property.getType() === 'color') found.push(property);
        const children = property.getProperties ? property.getProperties() : [];
        children.forEach((child) => collectColorProperties(child, found));
    }

    function installBrandColorSwatches() {
        try {
            const properties = [];
            editor.StyleManager.getSectors().forEach((sector) => {
                sector.get('properties').forEach((property) => collectColorProperties(property, properties));
            });
            properties.forEach(attachSwatchButton);

            // Property views survive a selection change — the same DOM node is reused,
            // measured — so one pass is enough. These two only cover a sector being
            // rebuilt later, and attachSwatchButton is a no-op on anything it already
            // marked, so running again costs nothing.
            editor.on('component:selected', () => properties.forEach(attachSwatchButton));
            document.addEventListener('click', closeSwatchPanel);
            document.addEventListener('keydown', (event) => {
                if (event.key === 'Escape') closeSwatchPanel();
            });
        } catch (e) {
            console.warn('brand color swatches failed', e);
        }
    }

    // --------------------------------------------------------------
    // UNSAVED-CHANGES SIGNAL
    // --------------------------------------------------------------
    // What the canvas currently holds, in the same shape SaveAll would persist.
    // Returns null if the editor cannot export right now, which callers treat as
    // "cannot tell" rather than "changed".
    // --------------------------------------------------------------
    // CANVAS-ONLY PREVIEW NODES ("ghosts")
    // --------------------------------------------------------------
    // The page listing block used to add its preview cards to the page as real
    // components; pages saved then still carry them. They leave the editor's output
    // here: with other pages' titles in them, a saved page would be found by site
    // search for every article it lists. (The block now draws its preview as plain
    // canvas DOM, which never reaches the model — see elevare-blocks.js.)
    const GHOST_ATTR = 'data-elevare-ghost';
    function stripGhostHtml(html) {
        if (!html || html.indexOf(GHOST_ATTR) < 0) return html;
        const tpl = document.createElement('template');
        tpl.innerHTML = html;
        tpl.content.querySelectorAll('[' + GHOST_ATTR + ']').forEach((el) => el.remove());
        return tpl.innerHTML;
    }
    function stripGhostComponents(node) {
        if (Array.isArray(node)) {
            return node
                .filter((c) => !(c && c.attributes && Object.prototype.hasOwnProperty.call(c.attributes, GHOST_ATTR)))
                .map(stripGhostComponents);
        }
        if (node && typeof node === 'object') {
            const out = {};
            Object.keys(node).forEach((k) => { out[k] = stripGhostComponents(node[k]); });
            return out;
        }
        return node;
    }

    function contentSignature() {
        if (!editor) return null;
        try {
            return stripGhostHtml(editor.getHtml()) + '␟' + editor.getCss();
        } catch (e) {
            console.warn('content signature failed', e);
            return null;
        }
    }

    function captureCleanSignature() {
        cleanSignature = contentSignature();
    }

    function installDirtyTracking() {
        editor.on('update', () => {
            if (canvasDirty) return;   // already reported; stay quiet until markClean
            if (dirtyCheckTimer) clearTimeout(dirtyCheckTimer);
            dirtyCheckTimer = setTimeout(() => {
                dirtyCheckTimer = null;
                if (canvasDirty) return;

                const current = contentSignature();
                // A baseline we never managed to take, or an export that just failed,
                // is not evidence of an edit — staying clean here risks losing work,
                // but announcing a change nobody made trains people to click through
                // the warning, which loses far more.
                if (current === null || cleanSignature === null || current === cleanSignature) return;

                canvasDirty = true;
                if (blazorRef) {
                    blazorRef.invokeMethodAsync('OnEditorContentChanged')
                        .catch((e) => console.warn('dirty notify failed', e));
                }
            }, 250);
        });
    }

    // --------------------------------------------------------------
    // DRAG-AND-DROP IMAGE UPLOAD
    // --------------------------------------------------------------
    // Dropping an image file onto the canvas uploads it to the media library and
    // inserts it at the drop point. The bytes are NOT sent through JS interop —
    // they are handed to a hidden Blazor <InputFile>, whose streaming upload path
    // has no message-size ceiling and reuses the media library's existing
    // validation/persistence. See PageEditor.razor's HandleDroppedImage.
    const DROP_INPUT_ID = 'elevare-drop-input';
    const DROP_ACTIVE_CLASS = 'elevare-canvas-drop-active';
    // Where the file was dropped, so the <img> lands there and not at the end of
    // the page. Read back by insertDroppedImage once the upload returns.
    let dropPoint = null;
    // Both the canvas drop and the Select Image dialog's uploader go through the
    // same Blazor round-trip, and the URL comes back on one callback for both —
    // this is what tells them apart. 'canvas' inserts at the drop point; 'assets'
    // adds to the media list the dialog is showing.
    let uploadTarget = 'canvas';

    function hasImageFiles(dt) {
        if (!dt) return false;
        if (dt.files && dt.files.length) {
            return Array.prototype.some.call(dt.files, (f) => f.type.startsWith('image/'));
        }
        // During dragover the file list is not readable yet — only the item kinds are.
        return dt.items && Array.prototype.some.call(dt.items,
            (i) => i.kind === 'file' && (i.type || '').startsWith('image/'));
    }

    // Maps a canvas coordinate back to the GrapesJS component under it, so the
    // image can be inserted as a SIBLING of whatever was dropped on rather than
    // being nested inside a paragraph or heading.
    function componentAtPoint(x, y) {
        try {
            const doc = editor.Canvas.getDocument();
            let el = doc.elementFromPoint(x, y);
            while (el && el !== doc.body && el.nodeType === 1) {
                if (el.id) {
                    const found = editor.getWrapper().find('#' + el.id);
                    if (found && found.length) return found[0];
                }
                el = el.parentElement;
            }
        } catch (e) { /* fall through to the wrapper */ }
        return null;
    }

    function setDropHighlight(on) {
        try {
            const body = editor.Canvas.getBody();
            if (!body) return;
            body.classList.toggle(DROP_ACTIVE_CLASS, !!on);
        } catch (e) { /* the canvas may not be ready yet */ }
    }

    // Hands the dropped File to Blazor's hidden <InputFile>. Assigning a
    // DataTransfer's file list is the only supported way to populate an
    // <input type=file> programmatically.
    function deliverToBlazor(file) {
        const input = document.getElementById(DROP_INPUT_ID);
        if (!input) { console.warn('drop input not found'); return false; }
        try {
            const dt = new DataTransfer();
            dt.items.add(file);
            input.files = dt.files;
            input.dispatchEvent(new Event('change', { bubbles: true }));
            return true;
        } catch (e) {
            console.warn('could not hand the dropped file to Blazor', e);
            return false;
        }
    }

    // The highlight lives INSIDE the canvas iframe, which has its own document and
    // therefore does not inherit the CMS stylesheet — the rule has to be injected.
    function injectDropStyles() {
        try {
            const doc = editor.Canvas.getDocument();
            if (!doc || doc.getElementById('elevare-drop-style')) return;
            const style = doc.createElement('style');
            style.id = 'elevare-drop-style';
            style.textContent =
                '.' + DROP_ACTIVE_CLASS + '{outline:3px dashed #6366f1;outline-offset:-6px;' +
                'background:rgba(99,102,241,.06)!important;}';
            (doc.head || doc.documentElement).appendChild(style);
        } catch (e) { /* styling is cosmetic — never block the drop over it */ }
    }

    // The floating toolbar renders into the document that owns the element being
    // edited — the canvas iframe, which has none of the CMS stylesheet. Left alone,
    // CKEditor sizes it to that element, so on anything narrow (a button label, a
    // card heading, a table cell) the full button set wraps into a block several rows
    // tall that sits on top of the very text being typed. Giving it a floor width and
    // stopping the rows from wrapping is what keeps it a shallow bar above the
    // element instead — the toolbar can hang past a narrow element's edges, which is
    // fine, where covering the text is not. Injected here because that document is
    // created by GrapesJS and never sees app.css.
    // A <summary> (the FAQ / accordion question) is a native disclosure control:
    // the browser answers Space and Enter by toggling its <details>, and it does
    // so even while the summary is being edited inline — so the Space you press to
    // put a gap between two words is swallowed by the toggle and never reaches the
    // text. (Selecting the text and the arrow not disappearing is the CKEditor
    // fix from before; this is a second, separate browser behaviour.)
    //
    // While the summary is contentEditable we take those two keys back: Space
    // inserts a real space at the caret, Enter just ends the toggle (a heading is
    // one line, so no newline). Capture phase on the canvas document, so it runs
    // before the element's own default and can cancel it.
    function installSummaryEditKeys() {
        let doc;
        try { doc = editor.Canvas.getDocument(); } catch (e) { return; }
        if (!doc || doc.__elevareSummaryKeys) return;
        doc.__elevareSummaryKeys = true;
        doc.addEventListener('keydown', (e) => {
            const el = e.target;
            if (!el || el.tagName !== 'SUMMARY' || el.isContentEditable !== true) return;
            const isSpace = e.key === ' ' || e.key === 'Spacebar' || e.keyCode === 32;
            const isEnter = e.key === 'Enter' || e.keyCode === 13;
            if (!isSpace && !isEnter) return;
            e.preventDefault();   // stop the <details> toggle
            e.stopPropagation();
            if (isSpace) {
                // execCommand keeps CKEditor's model and the undo stack in sync,
                // which a raw text-node insert would not.
                try { el.ownerDocument.execCommand('insertText', false, ' '); }
                catch (err) { /* toggle is stopped regardless */ }
            }
        }, true);
    }

    // One responsive scale for everything the builder writes: the device bar's
    // Tablet edits (max-width: 991px) and Mobile edits (max-width: 767px) — the
    // Bootstrap/Tailwind split, where 768px (an iPad held upright) is the first
    // tablet width, not the last phone one. The plugins and our own blocks
    // shipped their own numbers (grapesjs-navbar and the basic grid collapse at
    // 768px, the listing and search blocks at 640px) and the device bar used to
    // write 575px and 992px, so a page ended up with four breakpoints and the
    // canvas's Tablet frame — exactly 768px wide — sat on the edge of two of them.
    // Every such rule is folded onto the two above as it enters the editor. Rules
    // that end up on the same selector and breakpoint are merged, the originally
    // narrower one winning, which is the order the cascade applied them in.
    const BREAKPOINT_REMAP = { 575: 767, 640: 767, 768: 767, 992: 991 };
    let normalizingBreakpoints = false;

    function ruleMaxWidth(rule) {
        if ((rule.get('atRuleType') || 'media') !== 'media') return null;
        const m = /^\(\s*max-width:\s*(\d+)px\s*\)$/.exec((rule.get('mediaText') || '').trim());
        return m ? parseInt(m[1], 10) : null;
    }

    function normalizeBreakpoints() {
        if (!editor || normalizingBreakpoints) return;
        const all = editor.Css.getAll();
        const groups = new Map();
        all.models.forEach((rule) => {
            const from = ruleMaxWidth(rule);
            if (from == null) return;
            const to = BREAKPOINT_REMAP[from] || ((from === 767 || from === 991) ? from : null);
            if (!to) return;
            const key = [to, rule.selectorsToString(), rule.get('selectorsAdd') || '',
                rule.get('state') || '', rule.get('important') ? 1 : 0].join('|');
            if (!groups.has(key)) groups.set(key, []);
            groups.get(key).push({ rule, from, to });
        });
        normalizingBreakpoints = true;
        try {
            groups.forEach((entries) => {
                if (entries.length === 1 && entries[0].from === entries[0].to) return;
                // Stable sort, widest first: later entries overwrite earlier ones.
                entries.sort((a, b) => b.from - a.from);
                const json = entries[0].rule.toJSON();
                json.style = Object.assign({}, ...entries.map((x) => x.rule.getStyle()));
                json.atRuleType = 'media';
                json.mediaText = `(max-width: ${entries[0].to}px)`;
                // Removed and re-added rather than edited in place: the canvas keeps
                // one <style> container per breakpoint, and a rule only lands in
                // the right one when it is added.
                entries.forEach((x) => all.remove(x.rule));
                all.add(json);
            });
        } finally {
            normalizingBreakpoints = false;
        }
    }

    // Blocks, pasted HTML and linked templates bring rules in after load too.
    function installBreakpointNormalizer() {
        let pending = 0;
        const schedule = () => {
            if (normalizingBreakpoints || pending) return;
            pending = setTimeout(() => { pending = 0; normalizeBreakpoints(); }, 0);
        };
        editor.Css.getAll().on('add reset', schedule);
    }

    // A device frame keeps its real width (375 / 768px) whatever the screen, so the
    // page lays out exactly as it will on that device. When the canvas is narrower
    // than that — a laptop with the side panels open — GrapesJS left the frame at
    // full width and the canvas cut its right side off. Scale it down to fit
    // instead (the layout width inside does not change, only how big it is drawn),
    // centred, and as tall as the canvas. GrapesJS zooms around the canvas centre;
    // the coords shift puts the scaled frame back at the top and in the middle.
    function installDeviceFit() {
        const cv = editor.Canvas.getElement();
        if (!cv) return;
        const MARGIN = 12;
        let pending = 0;
        // The window listener and the observer outlive this editor unless removed:
        // once it is destroyed (or replaced by a reload) they stop themselves.
        const owner = editor;
        let observer = null;
        const fit = () => {
            pending = 0;
            if (editor !== owner) {
                window.removeEventListener('resize', schedule);
                if (observer) observer.disconnect();
                return;
            }
            const frame = editor.Canvas.getFrameEl();
            const wrapper = frame && frame.parentElement;
            const device = editor.Devices.getSelected();
            const width = parseFloat(device && device.get('width'));
            const avail = cv.clientWidth, height = cv.clientHeight;
            if (!avail || !height) return;
            // Only when it genuinely does not fit: a frame that fits stays at 100%
            // (GrapesJS centres it), never at a blurry 99.7%.
            let scale = 1;
            if (width && width > avail) {
                scale = Math.max(0.1, Math.floor((avail - 2 * MARGIN) / width * 1000) / 1000);
            }
            editor.Canvas.setZoom(scale * 100);
            editor.Canvas.setCoords(scale < 1 ? scale * (avail - width) / 2 : 0,
                scale < 1 ? -(height / 2) * (1 - scale) : 0);
            if (wrapper) wrapper.style.height = scale < 1 ? Math.ceil(height / scale) + 'px' : '';
        };
        const schedule = () => { if (!pending) pending = setTimeout(fit, 0); };
        editor.on('change:device', schedule);
        // The observer catches the canvas changing size on its own (side menus
        // collapsing, preview, fullscreen); the window event is the fallback.
        if (window.ResizeObserver) {
            observer = new ResizeObserver(schedule);
            observer.observe(cv);
        }
        window.addEventListener('resize', schedule);
        schedule();
    }

    // GrapesJS's preview hides its own panels; the side panel is ours (a sibling
    // of #gjs), so it stayed. Take it out of the grid for the duration.
    function installPreviewChrome() {
        const wrap = editor.getContainer() && editor.getContainer().closest('.pe-editor-wrap');
        if (!wrap) return;
        const set = (on) => {
            wrap.classList.toggle('is-previewing', on);
            try { editor.refresh(); } catch (e) { }
        };
        editor.on('run:preview', () => set(true));
        editor.on('stop:preview', () => set(false));
    }

    function injectRteToolbarStyles() {
        try {
            const doc = editor.Canvas.getDocument();
            if (!doc || doc.getElementById('elevare-rte-style')) return;
            const style = doc.createElement('style');
            style.id = 'elevare-rte-style';
            style.textContent =
                '.cke_float{max-width:none!important;z-index:2147483600;}' +
                '.cke_float .cke_inner,.cke_float .cke_top{min-width:900px!important;}' +
                '.cke_float .cke_toolbox{display:flex!important;flex-wrap:nowrap!important;' +
                'align-items:center;white-space:nowrap;}' +
                '.cke_float .cke_top{padding:3px 5px!important;}' +
                // The toolbar above is now a fixed size, but CKEditor 4 edits the
                // element itself in place, and an INLINE element (a badge span, a
                // link) has no box of its own: empty, it has nowhere to put a caret.
                // These get a small inline-block box while CKEditor is attached.
                // Only these. This used to be every .cke_editable, with a 220px
                // minimum, which changed what it touched: a <summary> lost its
                // disclosure arrow (list-item became inline-block), a logo in the
                // flex row grew to 220px on double-click. And CKEditor keeps its
                // class on the element after the edit ends (the instance stays
                // alive for the next double-click), so those changes stayed.
                // Never reaches the saved HTML: the class lives on the DOM only.
                '.cke_editable:is(span,a,strong,em,b,i,u,s,small,sub,sup,label,cite,q,time,mark,code)' +
                '{display:inline-block!important;min-width:1.5em!important;min-height:1.2em!important;}';
            (doc.head || doc.documentElement).appendChild(style);
        } catch (e) { /* styling is cosmetic — never block editing over it */ }
    }

    function installImageDrop() {
        let doc;
        try { doc = editor.Canvas.getDocument(); } catch (e) { return; }
        if (!doc) return;

        // Capture phase: GrapesJS has its own drop handling for block drags, and we
        // must claim the event before it only when real image FILES are involved.
        doc.addEventListener('dragover', (e) => {
            if (!hasImageFiles(e.dataTransfer)) return;
            e.preventDefault();
            e.stopPropagation();
            e.dataTransfer.dropEffect = 'copy';
            setDropHighlight(true);
        }, true);

        doc.addEventListener('dragleave', (e) => {
            // relatedTarget is null when the pointer actually leaves the document,
            // as opposed to just crossing between child elements inside it.
            if (!e.relatedTarget) setDropHighlight(false);
        }, true);

        doc.addEventListener('drop', (e) => {
            if (!hasImageFiles(e.dataTransfer)) return;
            e.preventDefault();
            e.stopPropagation();
            setDropHighlight(false);

            if (uploadInFlight) return;

            const file = Array.prototype.find.call(
                e.dataTransfer.files, (f) => f.type.startsWith('image/'));
            if (!file) return;

            dropPoint = { x: e.clientX, y: e.clientY };
            uploadTarget = 'canvas';
            uploadInFlight = deliverToBlazor(file);
        }, true);

        // A miss (dropping outside the canvas) must not leave the highlight stuck on.
        doc.addEventListener('dragend', () => setDropHighlight(false), true);
    }

    // --------------------------------------------------------------
    // MEDIA LIBRARY <-> ASSET MANAGER
    // --------------------------------------------------------------
    // Double-clicking an image opens GrapesJS's own "Select Image" dialog. Left to
    // itself that dialog knows nothing about this CMS: it lists no images and its
    // uploader has nowhere to post, so the only way to place a picture was to open
    // the media library separately, copy a URL and paste it in by hand. These two
    // functions close that gap — the dialog lists the media library's images, and
    // uploading from inside it stores the file in the library like any other upload.

    // Re-read on every open rather than once at init: an image added elsewhere in
    // the CMS (or by a colleague) is then already there, without reloading the editor.
    // A search box above the Select Image list. GrapesJS's asset manager has none;
    // with a few dozen files the list is a scroll, with a few hundred it is
    // unusable. Filters on the name (the alt text the library shows) and the file
    // path, as you type. The list is re-rendered every time the dialog opens
    // (refreshAssets resets the collection), so the filter is re-applied whenever
    // the list's children change rather than once on open.
    function installAssetSearch() {
        const cont = document.querySelector('.gjs-am-assets-cont');
        if (!cont) { setTimeout(installAssetSearch, 50); return; }
        const list = cont.querySelector('.gjs-am-assets');
        if (!list) return;
        let box = cont.querySelector('.elevare-am-search');
        if (!box) {
            box = document.createElement('input');
            box.type = 'search';
            box.className = 'gjs-field elevare-am-search';
            box.placeholder = (document.documentElement.lang || 'en').toLowerCase().startsWith('tr')
                ? 'Görsel ara… (ad veya dosya adı)' : 'Search images… (name or file name)';
            box.autocomplete = 'off';
            cont.insertBefore(box, list);
            const apply = () => {
                const q = box.value.trim().toLowerCase();
                list.querySelectorAll('.gjs-am-asset').forEach((item) => {
                    const name = (item.querySelector('.gjs-am-name') || {}).textContent || '';
                    // The thumbnail is a background-image, not an <img>.
                    const preview = item.querySelector('.gjs-am-preview');
                    const src = preview ? (preview.style.backgroundImage || '') : '';
                    const hit = !q || name.toLowerCase().includes(q) || decodeURIComponent(src).toLowerCase().includes(q);
                    item.style.display = hit ? '' : 'none';
                });
            };
            box.addEventListener('input', apply);
            new MutationObserver(apply).observe(list, { childList: true });
        }
        setTimeout(() => box.focus(), 0);
    }

    async function refreshAssets() {
        // TemplateEditor initialises the editor without a .NET reference, so there
        // is nobody to ask — leave the dialog as GrapesJS built it.
        if (!editor || !blazorRef) return;

        let files = null;
        try {
            files = await blazorRef.invokeMethodAsync('LoadMediaAssets');
        } catch (e) {
            console.warn('media library load failed', e);
            return;
        }
        if (!Array.isArray(files)) return;

        // Remembered so picking an image can fill in its alt text (see
        // installAltTextAutofill). Keyed by path, because the same file is written
        // into pages as a relative URL but read back off a component as an absolute
        // one once the browser has resolved it.
        altTextByPath = {};
        files.forEach((f) => {
            if (f.src && f.alt) altTextByPath[toMediaPath(f.src)] = f.alt;
        });

        try {
            const am = editor.AssetManager;
            // Reset instead of add: a file deleted from the library has to stop
            // being offered here too, and add() alone would keep showing it.
            am.getAll().reset(files.map((f) => ({
                type: 'image',
                src: f.src,
                name: f.name
            })));
            // The dialog is already on screen by the time this resolves, and its view
            // does not redraw off the reset alone — without this the list stays
            // visibly empty while holding every asset.
            am.render(am.getAll().models);
        } catch (e) {
            console.warn('asset refresh failed', e);
        }
    }

    // Media-library alt text, keyed by upload path — filled by refreshAssets.
    let altTextByPath = {};

    // Reduces any form of a media URL (relative, absolute on either host, with a
    // query string) to the "/uploads/…" path the library keys on. Mirrors what
    // ResponsiveImageResolutionService does server-side, for the same reason: the
    // same file legitimately appears in several URL shapes.
    function toMediaPath(url) {
        if (!url) return '';
        let path = String(url);
        const schemeEnd = path.indexOf('://');
        if (schemeEnd >= 0) {
            const hostEnd = path.indexOf('/', schemeEnd + 3);
            path = hostEnd >= 0 ? path.slice(hostEnd) : '';
        }
        const cut = path.search(/[?#]/);
        if (cut >= 0) path = path.slice(0, cut);
        const marker = path.indexOf('/uploads/');
        return marker >= 0 ? path.slice(marker) : path;
    }

    // Writes the media library's alt text onto an image the moment its src is
    // pointed at a library file — whether that happened through the Select Image
    // dialog or by typing a URL into the image's own trait.
    //
    // Only fills a blank: an author who has written their own alt text (including a
    // deliberate empty one, which marks a decorative image) keeps it. The public
    // site applies the same rule again at render time, so an image whose alt text is
    // written in the library AFTER the page was saved still gets it.
    function installAltTextAutofill(ed) {
        ed.on('component:update:src', (component) => {
            try {
                if (!component || component.get('type') !== 'image') return;

                const alt = altTextByPath[toMediaPath(component.get('src'))];
                if (!alt) return;

                const attributes = component.getAttributes() || {};
                if (typeof attributes.alt === 'string' && attributes.alt.length > 0) return;

                component.addAttributes({ alt: alt });
            } catch (e) {
                console.warn('alt autofill failed', e);
            }
        });
    }

    // Opens the Select Image dialog for a component that is NOT the one being
    // double-clicked — the "Kütüphaneden Seç" trait button below. Double-click is
    // the only other way in, and it fails whenever anything overlays the image
    // (a slide caption, a card badge) or when the image was reached through the
    // Layer Manager rather than by clicking it on the canvas.
    //
    // open() raises the same 'run:open-assets' the command does, so refreshAssets
    // still fills the dialog from the media library — verified, not assumed.
    // Setting `src` on the model (not the attribute) is what the image component
    // actually reads, and it also triggers installAltTextAutofill, so picking a
    // library image fills its alt text in the same step.
    function openImagePicker(component) {
        if (!editor || !component) return;
        try {
            const am = editor.AssetManager;
            am.open({
                types: ['image'],
                select(asset, complete) {
                    component.set('src', asset.getSrc());
                    if (complete) am.close();
                }
            });
        } catch (e) {
            console.warn('image picker failed', e);
        }
    }

    // GrapesJS calls this for the dialog's own file input / drop area. The bytes go
    // to the same hidden Blazor <InputFile> the canvas drop uses, for the same
    // reason: interop has a message-size ceiling and the streaming upload does not.
    function onAssetUpload(e) {
        const list = (e && e.dataTransfer && e.dataTransfer.files) || (e && e.target && e.target.files);
        const file = list && Array.prototype.find.call(list, (f) => f.type.startsWith('image/'));
        if (!file || uploadInFlight) return;

        uploadTarget = 'assets';
        dropPoint = null;
        uploadInFlight = deliverToBlazor(file);
    }

    return {
        init: async function(containerId, html, css, data, linkedTemplatesMap, seoMeta, seoResources, dotNetRef, activeLanguagesList, pageDirectoryList, templateVersionsMap) {
            if (initializing) return false;
            initializing = true;
            seoApi = null;
            blazorRef = dotNetRef || null;
            canvasDirty = false;
            cleanSignature = null;
            if (dirtyCheckTimer) { clearTimeout(dirtyCheckTimer); dirtyCheckTimer = null; }
            uploadInFlight = false;
            uploadTarget = 'canvas';
            activeLanguages = Array.isArray(activeLanguagesList) ? activeLanguagesList : [];
            pageDirectory = Array.isArray(pageDirectoryList) ? pageDirectoryList : [];
            templateVersions = templateVersionsMap || {};
            try {
                const currentLang = (document.documentElement.lang || 'en').split('-')[0];
                PLUGIN_OPTS['grapesjs-plugin-ckeditor'].options.language = currentLang;
                await ensureAssets();
                if (!window.grapesjs) { console.error('grapesjs core failed to load'); return false; }
                // Idempotent — also covers the case where CKEditor was already
                // cached by the browser on a previous visit.
                patchCkeditorDtd();

                if (editor) { try { editor.destroy(); } catch (e) { } editor = null; }
                buildSidePanelShell();
                const sideConfig = sidePanelManagerConfig();
                editor = window.grapesjs.init({
                    container: '#' + containerId,
                    height: '100%',
                    fromElement: false,
                    storageManager: false,
                    // Only the top bar. Blocks/Layers/Traits/Styles render into the
                    // side panels (see SIDE), so GrapesJS's own "views" column and
                    // its four toggle buttons are not created at all.
                    panels: hasSidePanels() ? {
                        defaults: [
                            { id: 'commands', buttons: [] },
                            {
                                id: 'options',
                                buttons: [
                                    { id: 'sw-visibility', command: 'sw-visibility', context: 'sw-visibility', className: 'fa fa-square-o', active: true, togglable: true },
                                    { id: 'preview', command: 'preview', context: 'preview', className: 'fa fa-eye' },
                                    { id: 'fullscreen', command: 'fullscreen', context: 'fullscreen', className: 'fa fa-arrows-alt' },
                                    { id: 'export-template', command: 'export-template', className: 'fa fa-code' }
                                ]
                            }
                        ]
                    } : undefined,
                    blockManager: sideConfig.blockManager,
                    layerManager: sideConfig.layerManager,
                    traitManager: sideConfig.traitManager,
                    selectorManager: sideConfig.selectorManager,
                    // assets are filled in on every open by refreshAssets, from the
                    // CMS media library. dropzone stays off because dropping onto the
                    // canvas is already handled by installImageDrop, which places the
                    // image where it was dropped instead of opening this dialog.
                    assetManager: { assets: [], uploadFile: onAssetUpload, dropzone: false },
                    // GrapesJS's own default sectors, made explicit rather than left
                    // implicit, because the defaults are missing properties an editor
                    // reaches for constantly and there was no other place to add them:
                    // Boyut (dimension) ships width/height/max-width/min-height but
                    // never min-width or max-height, and General has no z-index — the
                    // one property an absolutely-positioned dropdown panel (Mega Menu)
                    // or a Pop-up needs to sit above its neighbours — nor overflow or
                    // cursor. Everything else here is copied unchanged from what
                    // GrapesJS 0.21's own StyleManager.getSectors() actually returns,
                    // so nothing an editor already relies on moves or disappears.
                    // Ordered by how often a page author reaches for each: what
                    // the text looks like, how much room it takes, its background
                    // and edges, effects — and only then the layout machinery
                    // (display, position, flex), closed, under "Gelişmiş".
                    styleManager: {
                        appendTo: sideConfig.styleManagerAppendTo,
                        sectors: [
                            {
                                id: 'typography', name: 'Typography', open: true,
                                buildProps: ['font-family', 'font-size', 'font-weight', 'letter-spacing', 'color', 'line-height', 'text-align', 'text-shadow']
                            },
                            {
                                id: 'dimension', name: 'Dimension', open: false,
                                buildProps: ['width', 'height', 'min-width', 'max-width', 'min-height', 'max-height', 'margin', 'padding']
                            },
                            {
                                id: 'decorations', name: 'Decorations', open: false,
                                buildProps: ['background-color', 'border-radius', 'border', 'box-shadow', 'background']
                            },
                            {
                                id: 'extra', name: 'Extra', open: false,
                                buildProps: ['opacity', 'transition', 'transform']
                            },
                            {
                                id: 'general', name: 'General', open: false,
                                buildProps: ['display', 'float', 'position', 'top', 'right', 'left', 'bottom', 'z-index', 'overflow', 'cursor'],
                                // `position` is overridden rather than built, for two
                                // reasons that both need the definition to exist BEFORE
                                // the property's view is constructed.
                                //
                                // GrapesJS ships it as a radio group with four options
                                // and no `sticky` — a sticky header, the single most
                                // common thing anyone wants from a site menu, could not
                                // be built without hand-writing CSS. Adding the option
                                // is only half of it: five radio buttons do not fit the
                                // panel, so they render clipped and unreadable. A select
                                // holds any number of options in the same space, and
                                // `full` keeps the row on a line of its own instead of
                                // sharing one with the next property.
                                //
                                // This used to be done afterwards, in
                                // installStyleManagerExtras, with `property.set({ type:
                                // 'select' })`. That silently did nothing to the field:
                                // the view class is chosen from the type when the view is
                                // built, and by then it already had been. The option list
                                // changed, the radio group stayed.
                                properties: [{
                                    property: 'position', type: 'select', full: true, default: 'static',
                                    options: [
                                        { id: 'static' }, { id: 'relative' }, { id: 'absolute' },
                                        { id: 'fixed' }, { id: 'sticky' }
                                    ]
                                }]
                            },
                            {
                                id: 'flex', name: 'Flex', open: false,
                                buildProps: ['flex-direction', 'flex-wrap', 'justify-content', 'align-items', 'align-content', 'order', 'flex-basis', 'flex-grow', 'flex-shrink', 'align-self']
                            }
                        ]
                    },
                    // GrapesJS strips <script> tags and on*= handlers out of parsed
                    // HTML by default. That silently emptied the "Import Code" modal
                    // of the very thing it exists for — an analytics snippet or chat
                    // widget pasted there reached the server with its script gone,
                    // and no message said so. Authoring custom code is already a
                    // privilege (CustomCode.Author, enforced server-side by
                    // CustomCodeGuard); the parser must not also quietly veto it.
                    allowScripts: 1,
                    parser: {
                        optionsHtml: { allowUnsafeAttr: true },
                        // What counts as text when HTML is parsed back into components:
                        // GrapesJS's own list plus the formatting the rich-text editor
                        // writes. Without <font>, a byline whose author CKEditor had
                        // wrapped in <font color><b> came back from a copy as a plain
                        // box, and "Yazar:" in it could no longer be edited at all.
                        textTags: ['br', 'b', 'i', 'u', 'a', 'ul', 'ol', 'font', 'strong', 'em', 's', 'small', 'sub', 'sup', 'mark']
                    },
                    i18n: {
                        locale: currentLang,
                        localeFallback: 'en',
                        messages: { tr: TR_LOCALE }
                    },
                    // Explicit devices: the SEO analyzer's mobile-overflow test
                    // relies on the 'Mobile portrait' device existing. widthMedia is
                    // the one responsive scale (see BREAKPOINT_REMAP): Tablet covers
                    // 768–991px, Mobile everything below 768px. Each frame is drawn
                    // at the narrow-ish real width of its range (an upright iPad, a
                    // phone).
                    deviceManager: {
                        devices: [
                            { id: 'desktop', name: 'Desktop', width: '' },
                            { id: 'tablet', name: 'Tablet', width: '768px', widthMedia: '991px' },
                            { id: 'mobile', name: 'Mobile portrait', width: '375px', widthMedia: '767px' }
                        ]
                    },
                    canvas: {
                        styles: [
                            // Site Codes' own CSS (brand color variables, critical CSS,
                            // base styles, and anything else an operator has added —
                            // Bootstrap Icons included, if they want it) — without this,
                            // blocks that read var(--elevare-*) fall back to their literal
                            // defaults inside the canvas and never show the site's real,
                            // current theme. Cache-busted per load so a change in Site
                            // Codes shows up on the next page-builder open, not whenever
                            // the browser feels like revalidating a stylesheet.
                            //
                            // Deliberately the ONLY entry: a hardcoded Bootstrap Icons
                            // CDN link used to sit here too, so an icon block always
                            // looked right in the canvas even when Site Codes had never
                            // been given that stylesheet — which meant an operator could
                            // remove Bootstrap Icons from Site Codes and see every icon
                            // on the live site vanish while the editor kept showing them
                            // perfectly, with nothing here to explain the mismatch. The
                            // canvas must load exactly what Site Codes says and nothing
                            // more, so what an editor sees while building is what a
                            // visitor actually gets.
                            canvasPreviewCssUrl()
                        ]
                    }
                });
                applyPlugins();

                // --- FULLSCREEN OVERRIDE ---
                // The built-in command fullscreens the GrapesJS container (#gjs)
                // alone, so our right-hand panel — a sibling of #gjs, not a child —
                // is left behind. Target .pe-editor-wrap instead: it holds the canvas
                // AND the panel, so both fill the screen. The panel is a grid column,
                // so nothing about the layout changes, it just gets the whole viewport.
                (function overrideFullscreen() {
                    const getWrap = () => (editor.getContainer() && editor.getContainer().closest
                        ? editor.getContainer().closest('.pe-editor-wrap') : null)
                        || document.querySelector('.pe-editor-wrap');
                    editor.Commands.add('fullscreen', {
                        run() {
                            const wrap = getWrap();
                            const req = wrap && (wrap.requestFullscreen || wrap.webkitRequestFullscreen || wrap.msRequestFullscreen);
                            if (req) { try { const p = req.call(wrap); if (p && p.catch) p.catch(() => { }); } catch (e) { } }
                        },
                        stop() {
                            const exit = document.exitFullscreen || document.webkitExitFullscreen || document.msExitFullscreen;
                            if (exit && (document.fullscreenElement || document.webkitFullscreenElement)) {
                                try { const p = exit.call(document); if (p && p.catch) p.catch(() => { }); } catch (e) { }
                            }
                        }
                    });
                    // Exiting with Esc (or the browser's own control) never calls stop(),
                    // so the toolbar button would stay stuck "on"; keep it in sync, and
                    // let the canvas recompute its size for the new viewport.
                    const onChange = () => {
                        const active = !!(document.fullscreenElement || document.webkitFullscreenElement);
                        const btn = editor.Panels.getButton('options', 'fullscreen');
                        if (btn && btn.get('active') !== active) btn.set('active', active);
                        try { editor.refresh(); } catch (e) { }
                    };
                    document.addEventListener('fullscreenchange', onChange);
                    document.addEventListener('webkitfullscreenchange', onChange);
                })();

                // --- 1. ROBUST CKEDITOR ERROR PREVENTION ---
                // Second double-click on the same text could not type. The plugin
                // keeps the CKEditor instance alive between edits and, on re-enable,
                // sets contentEditable back to true only if the instance does not
                // already report focus — but the single click that precedes the
                // double-click has already given it focus (the editable listens
                // for it even while not editable), so contentEditable stayed
                // "false" from the previous disable. The element is editable
                // whenever enable was asked for; say so regardless.
                const customRte = editor.RichTextEditor && editor.RichTextEditor.customRte;
                if (customRte && typeof customRte.enable === 'function') {
                    const pluginEnable = customRte.enable;
                    customRte.enable = function (el, rte) {
                        const result = pluginEnable.call(this, el, rte);
                        if (el && el.contentEditable !== 'true') el.contentEditable = 'true';
                        return result;
                    };
                    // What CKEditor hands back is parsed into components again, the way
                    // GrapesJS's own editor does it. Without this the plugin's default
                    // kept it as one static HTML string: every link, span and <time>
                    // inside a text edited once stopped being a component — the Article
                    // block's date trait could no longer find its <time>, and the
                    // editor's internal data-gjs-type/draggable attributes went into
                    // the saved content.
                    customRte.parseContent = true;
                }
                if (editor.RichTextEditor && editor.RichTextEditor.enable) {
                    const origEnable = editor.RichTextEditor.enable.bind(editor.RichTextEditor);
                    editor.RichTextEditor.enable = function(el, ...args) {
                        // Only truly non-textual elements are blocked now; a/button/
                        // span/li/... are inline-editable thanks to the DTD patch.
                        if (RTE_BLOCKED_TAGS.includes(getElTagName(el))) return;
                        // Error-catching block
                        try {
                            return origEnable(el, ...args);
                        } catch (err) {
                            console.warn('CKEditor could not initialize on:', el, err);
                            return null;
                        }
                    };
                }
                // --- 2. ADD SEO / ACCESSIBILITY TRAITS ---
                installImageSizeTrait();
                installSeoTraits();
                installStyleManagerExtras();
                installBrandColorSwatches();
                installLinkedTemplateType();
                wireSidePanels();

                let restored = false;
                if (data) {
                    try {
                        editor.loadProjectData(JSON.parse(data));
                        restored = true;
                    } catch (e) {
                        console.warn('gjs data parse failed', e);
                    }
                }
                if (!restored) {
                    if (css) editor.setStyle(css);
                    if (html) editor.setComponents(html);
                }
                normalizeBreakpoints();
                // Fires every time the Select Image dialog is opened — by a
                // double-click on an image, or by an image trait's picker.
                editor.on('run:open-assets', () => { refreshAssets(); installAssetSearch(); });

                // Registered before the first asset is picked, so the very first
                // image placed in a session already gets its alt text.
                installAltTextAutofill(editor);

                // The library is normally read when the Select Image dialog opens.
                // Reading it once up front as well is what lets an image inserted by
                // any other route — a block that ships with an image, a URL typed
                // into a trait — find its alt text without the dialog ever opening.
                refreshAssets();

                editor.on('load', () => {
                    this.refreshLinkedTemplates(linkedTemplatesMap);
                    // Before the clean baseline below, so folding old breakpoints
                    // never makes a page open as "unsaved".
                    normalizeBreakpoints();
                    installBreakpointNormalizer();
                    installDeviceFit();
                    installPreviewChrome();
                    defaultImageLoading(editor.getWrapper());
                    editor.on('component:add', (m) => defaultImageLoading(m));
                    // Both need the canvas iframe to exist, which is only guaranteed
                    // once 'load' has fired.
                    installImageDrop();
                    injectDropStyles();
                    injectRteToolbarStyles();
                    installSummaryEditKeys();
                    // Restoring content and resolving linked templates both count as
                    // GrapesJS "updates"; baseline AFTER them so the editor neither
                    // opens dirty nor treats its own setup as the user's edit.
                    canvasDirty = false;
                    captureCleanSignature();
                    installDirtyTracking();

                    const stale = this.checkStaleSnapshotTemplates(templateVersionsMap);
                    if (stale.length && blazorRef) {
                        blazorRef.invokeMethodAsync('OnStaleTemplatesDetected', stale)
                            .catch((e) => console.warn('OnStaleTemplatesDetected failed', e));
                    }
                });
                if (!hasSidePanels()) { try { editor.runCommand('open-blocks'); } catch (e) { } }
                else {
                    // Six categories open at once is a wall; closed, the list is a
                    // table of contents — and "Son kullanılanlar" stays open on top.
                    editor.BlockManager.getCategories().each((cat) => { if (cat.getId() !== 'elevare-recent') cat.set('open', false); });
                }
                // Component outlines ON by default. Drag-and-drop was hard to aim
                // precisely because the containers are invisible until you hover
                // exactly right — dropping a logo into a navbar means hitting the
                // one <div> that accepts it, and without outlines there is nothing
                // on screen showing where it starts or ends. The toolbar's
                // "Bileşenleri Göster" button still turns it back off.
                try { editor.runCommand('sw-visibility'); } catch (e) { }
                // --- 3. ATTACH THE SEO ANALYSIS MODULE (elevare-seo.js) ---
                //
                // Only where there is a page to analyse. seoResources is the tell:
                // PageEditor passes a full set of localized check strings, and
                // TemplateEditor passes null because it has no SEO panel to fill them
                // from. Attaching there anyway put an "SEO 42" badge on the toolbar of
                // a screen editing a HEADER — scored as if the fragment were a whole
                // page, so it complained about a missing H1, missing meta description,
                // thin content and no structured data, every one of which is correct
                // for a template and impossible to fix there. A number that can only
                // ever be wrong teaches people to ignore the number.
                const hasSeoPanel = !!seoResources;
                if (hasSeoPanel && window.elevareSeoAnalyzer && typeof window.elevareSeoAnalyzer.attach === 'function') {
                    try { seoApi = window.elevareSeoAnalyzer.attach(editor, currentLang, seoMeta || {}, seoResources, dotNetRef || null); }
                    catch (e) { console.warn('SEO analyzer attach failed', e); }
                }
                return true;
            } catch (e) {
                console.error('GrapeJS init error:', e);
                return false;
            } finally {
                initializing = false;
            }
        },
        isActive: function() {
            return !!editor;
        },
        // Raw editor instance — for devtools/diagnostics only; app code should go
        // through the wrapper functions on this module.
        getEditor: function() {
            return editor;
        },
        getActiveLanguages: function() {
            return activeLanguages;
        },
        getPageDirectory: function() {
            return pageDirectory;
        },
        setSiteInfo: function(info) {
            siteInfo = info || {};
        },
        // Called by PageEditor with the page's current exclusions — at load, and
        // again whenever the Site Codes dialog changes them. Swaps the canvas's
        // Site Codes stylesheet for one without the excluded rows, so a style the
        // page will not carry stops being shown while it is being built. The
        // <link> is the one GrapesJS created from canvas.styles; found by href
        // since it carries no id of its own.
        setExcludedSiteCodes: function(ids) {
            excludedSiteCodeIds = Array.isArray(ids) ? ids.map((x) => parseInt(x, 10)).filter((x) => !isNaN(x)) : [];
            if (!editor) return;
            const doc = editor.Canvas.getDocument();
            if (!doc) return;
            const link = [...doc.querySelectorAll('link[rel="stylesheet"]')]
                .find((l) => (l.getAttribute('href') || '').includes('/_canvas-preview.css'));
            if (link) link.setAttribute('href', canvasPreviewCssUrl());
        },
        getSiteInfo: function() {
            return siteInfo;
        },
        insertHtml: function(html, css) {
            if (!editor) return false;
            const target = editor.getSelected() || editor.getWrapper();
            if (html) target.append(html);
            if (css) { try { editor.Css.addRules(css); } catch (e) { console.warn('addRules failed', e); } }
            return true;
        },
        insertLinkedTemplate: function(templateId, html, css) {
            if (!editor) return false;
            const target = editor.getSelected() || editor.getWrapper();
            target.append(wrapTemplateHtml(templateId, html));
            if (css) { try { editor.Css.addRules(css); } catch (e) { console.warn('addRules failed', e); } }
            return true;
        },
        insertSnapshotTemplate: function(templateId, snapshotVersion, html, css) {
            if (!editor) return false;
            const target = editor.getSelected() || editor.getWrapper();
            target.append(wrapSnapshotTemplateHtml(templateId, snapshotVersion, html));
            if (css) { try { editor.Css.addRules(css); } catch (e) { console.warn('addRules failed', e); } }
            return true;
        },
        // Compares each unlinked-template copy's stamped version against the
        // template's current content version (GetPageTemplateVersionsQuery — moves
        // only when the template's OWN content changes, not its linked parts) and
        // reports each template whose copy is behind, once per template: id, name,
        // when this copy was taken and when the template last changed. Content is
        // never touched; this is purely informational (see StaleTemplateNotice).
        //
        // Skipped: copies INSIDE a linked template. Those belong to that template —
        // every page using it would otherwise be told about a copy it cannot edit —
        // and the template's own editor reports them. Also skipped: what the editor
        // was told to stop mentioning for that version (dismissStaleTemplate).
        checkStaleSnapshotTemplates: function(templateVersionsMap) {
            if (!editor || !templateVersionsMap) return [];
            try {
                const dismissed = readDismissedStale();
                const refs = editor.getWrapper().find('.' + TPL_SNAPSHOT_CLASS);
                const stale = new Map();
                refs.forEach((comp) => {
                    for (let p = comp.parent(); p; p = p.parent()) {
                        if (p.getClasses().includes(TPL_REF_CLASS)) return;
                    }
                    const attrs = comp.getAttributes();
                    const id = attrs['data-elevare-template-id'];
                    const snapshotAt = attrs['data-elevare-template-snapshot-at'];
                    const current = id != null ? templateVersionsMap[id] : null;
                    if (!current || !snapshotAt) return;
                    const currentVersion = current.version ?? current.Version;
                    const name = current.name ?? current.Name;
                    if (!currentVersion || !(new Date(currentVersion) > new Date(snapshotAt))) return;
                    if (dismissed[staleKey(id)] === String(currentVersion)) return;
                    const prev = stale.get(String(id));
                    // Several copies of one template: the oldest says the most.
                    if (!prev || new Date(snapshotAt) < new Date(prev.copiedAt)) {
                        stale.set(String(id), { id: parseInt(id, 10), name: name || ('#' + id), copiedAt: snapshotAt, changedAt: String(currentVersion) });
                    }
                });
                return Array.from(stale.values());
            } catch (e) {
                console.warn('checkStaleSnapshotTemplates failed', e);
                return [];
            }
        },
        // "Got it, do not mention it again" — for this page (or template) and this
        // version of the source. A later change to the template is news again.
        // Per browser on purpose: it is a reading convenience, not page state.
        dismissStaleTemplate: function(templateId, version) {
            try {
                const all = readDismissedStale();
                all[staleKey(templateId)] = String(version);
                localStorage.setItem(STALE_DISMISS_KEY, JSON.stringify(all));
            } catch (e) { /* storage unavailable: the notice just comes back */ }
        },
        refreshLinkedTemplates: function(linkedTemplatesMap) {
            if (!editor || !linkedTemplatesMap) return;
            try {
                refreshLinkedTemplateRefs(linkedTemplatesMap);
            } catch (e) {
                console.warn('refreshLinkedTemplates failed', e);
            }
        },
        getData: function() {
            if (!editor) return null;
            return {
                html: sanitizeInternalAttrs(stripGhostHtml(editor.getHtml())),
                css: editor.getCss(),
                data: JSON.stringify(stripGhostComponents(editor.getProjectData()))
            };
        },
        // What a page listing block will list — see GetListingPreview on the Blazor
        // side. Null where there is nothing to ask (no editor host, a template).
        getListingPreview: function(params) {
            if (!blazorRef) return Promise.resolve(null);
            return blazorRef.invokeMethodAsync('GetListingPreview', params).catch((e) => {
                console.warn('GetListingPreview failed', e);
                return null;
            });
        },
        // What a Sayfa Yolu / Önceki-Sonraki block will show on this page (the
        // public site fills both in). Null in a template, or when it cannot be asked.
        getBreadcrumbPreview: function() {
            if (!blazorRef) return Promise.resolve(null);
            return blazorRef.invokeMethodAsync('GetBreadcrumbPreview').catch(() => null);
        },
        getAdjacentPreview: function() {
            if (!blazorRef) return Promise.resolve(null);
            return blazorRef.invokeMethodAsync('GetAdjacentPreview').catch(() => null);
        },
        // The code block's coloured HTML, from the same highlighter the public site
        // runs (HighlightCode on the Blazor side). Null when it cannot be asked.
        highlightCode: function(code, language) {
            if (!blazorRef) return Promise.resolve(null);
            return blazorRef.invokeMethodAsync('HighlightCode', code, language).catch((e) => {
                console.warn('HighlightCode failed', e);
                return null;
            });
        },
        // The site's tags, asked once per editor session.
        getListingTags: function() {
            if (!blazorRef) return Promise.resolve([]);
            if (!listingTagsPromise) {
                listingTagsPromise = blazorRef.invokeMethodAsync('GetListingTags').catch((e) => {
                    console.warn('GetListingTags failed', e);
                    listingTagsPromise = null;
                    return [];
                });
            }
            return listingTagsPromise;
        },
        // What the page's content offers the Social Sharing panel's "Otomatik
        // doldur", read from the canvas as it is now (unsaved edits included): the
        // first real image — no linked header/footer, nothing hidden or decorative,
        // no data: placeholder, no SVG, nothing under 200px wide — and the Article
        // block's date, both as written and as its datetime attribute.
        getShareSuggestions: function() {
            if (!editor) return null;
            const doc = new DOMParser().parseFromString('<body>' + editor.getHtml() + '</body>', 'text/html');
            doc.querySelectorAll('.elevare-tpl-ref, body > header, body > footer, body > nav, script, noscript, template, [hidden], [aria-hidden="true"]')
                .forEach(function (el) { el.remove(); });
            let image = null;
            doc.querySelectorAll('img[src]').forEach(function (img) {
                if (image) return;
                const src = (img.getAttribute('src') || '').trim();
                const width = parseInt(img.getAttribute('width'), 10);
                if (!src || /^data:/i.test(src) || /\.svg$/i.test(src) || (width && width < 200)) return;
                image = src;
            });
            const time = doc.querySelector('[data-elevare-article] [data-elevare-article-date]');
            return {
                contentImage: image,
                articleDateAttribute: time ? time.getAttribute('datetime') : null,
                articleDateText: time ? (time.parentElement || time).textContent : null
            };
        },
        // Called as the Blazor SEO Settings panel's focus keyword/meta description
        // change, to keep the JS analysis engine's state up to date (see PageEditor.razor).
        updateSeoMeta: function(meta) {
            if (seoApi) seoApi.updateMeta(meta);
        },
        // Called by the "Re-analyze" button in the Blazor SEO panel.
        reanalyzeSeo: function() {
            if (seoApi) seoApi.reanalyze();
        },
        // Called after a successful save: the canvas now matches the server, so the
        // next edit should raise the unsaved flag again.
        markClean: function() {
            // A save can move the page (a new parent): the canvas previews that depend
            // on where it sits ask again.
            if (editor) editor.trigger('elevare:saved');
            canvasDirty = false;
            if (dirtyCheckTimer) { clearTimeout(dirtyCheckTimer); dirtyCheckTimer = null; }
            captureCleanSignature();
        },
        // Called by Blazor once a dropped image has been uploaded and has a real URL.
        // Inserted as a SIBLING of whatever was under the cursor, so dropping onto a
        // paragraph puts the image beside it rather than inside it.
        insertDroppedImage: function(url, alt) {
            uploadInFlight = false;
            if (!editor || !url) return false;

            // Uploaded from inside the Select Image dialog: there was no drop point,
            // and the user is still looking at the list — put it at the top of that
            // list rather than dropping it onto the page behind the dialog.
            if (uploadTarget === 'assets') {
                uploadTarget = 'canvas';
                try {
                    editor.AssetManager.add({ type: 'image', src: url, name: alt || url }, { at: 0 });
                    return true;
                } catch (e) {
                    console.warn('asset add failed', e);
                    return false;
                }
            }

            const html = `<img src="${escapeAttr(url)}" alt="${escapeAttr(alt)}" style="max-width:100%;height:auto" />`;
            try {
                const target = dropPoint ? componentAtPoint(dropPoint.x, dropPoint.y) : null;
                const parent = target && target.parent ? target.parent() : null;

                if (parent && typeof target.index === 'function') {
                    parent.append(html, { at: target.index() + 1 });
                } else {
                    editor.getWrapper().append(html);
                }
                return true;
            } catch (e) {
                console.warn('image insert failed, appending at the end instead', e);
                try { editor.getWrapper().append(html); return true; } catch (e2) { return false; }
            } finally {
                dropPoint = null;
            }
        },
        // Releases the in-flight guard when an upload fails, so the next drop is not
        // silently ignored.
        cancelDroppedImage: function() {
            uploadInFlight = false;
            dropPoint = null;
            uploadTarget = 'canvas';
        },
        destroy: function() {
            if (editor) { try { editor.destroy(); } catch (e) { } editor = null; }
            seoApi = null;
            blazorRef = null;
            canvasDirty = false;
            cleanSignature = null;
            if (dirtyCheckTimer) { clearTimeout(dirtyCheckTimer); dirtyCheckTimer = null; }
            uploadInFlight = false;
            dropPoint = null;
            uploadTarget = 'canvas';
            // A page's own state must not follow the author into the next editor: the
            // template editor never sets exclusions, so it opened with the Site Codes
            // the last page had switched off missing from its canvas. The tag list is
            // asked again too — tags added in between would otherwise never show.
            excludedSiteCodeIds = [];
            listingTagsPromise = null;
        }
    };
})();