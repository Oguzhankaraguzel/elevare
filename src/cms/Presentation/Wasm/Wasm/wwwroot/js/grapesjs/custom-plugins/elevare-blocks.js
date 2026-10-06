// ==========================================
// ELEVARE UI KIT — Smart Blocks v2.1 (SEO & CWV Optimized)
//
// Architecture changes vs v1 & v2.0:
// 1. NO full re-render on trait change. (Incremental mutation via syncRepeat).
// 2. Interactive blocks use isolated canvas+export scripts.
// 3. Strict SEO & Core Web Vitals (CWV) compliance:
//    - Added 'imageLoading' (Lazy/Eager) trait to all image-heavy blocks to prevent LCP penalties.
//    - Converted background-images (Slider, Before/After, Video Facade) to real <img> tags with object-fit for Google Image Indexing.
//    - Removed generic link texts ("Learn more", "Click here") replacing them with descriptive placeholders.
//    - Added hreflang support to language switcher.
//
// Placeholder links are href="#" again (v2.1 briefly used href="/"): an
// unedited block should point nowhere, not silently ship every visitor who
// clicks its "Add to cart" or social icon to the homepage. Blocks whose
// links a resolver overwrites server-side — Sayfa Yolu (breadcrumb), Dil
// Değiştirici, Sayfa Listesi — are unaffected either way. The one deliberate
// exception is the Navbar block's own brand/logo link, which stays "/".
// ==========================================
window.grapesElevareBlocks = function (editor, opts = {}) {
    const bm = editor.BlockManager;
    const comps = editor.DomComponents;
    const category = opts.category || 'Elevare UI Kit';

    // ---- shared palette ----
    // Bound to the --elevare-* custom properties seeded into Site Codes ("Marka
    // Renkleri (CSS Değişkenleri)") wherever this file already had a matching
    // token — same visual result today (the fallback after the comma is this
    // constant's own original literal), but every block dropped onto a page from
    // now on re-reads the variable live: changing it in Site Codes recolors every
    // such block already on every page, not just ones placed afterwards, because
    // a CSS custom property is resolved by the browser on every render, not baked
    // in once at block-creation time.
    //
    // What stays a literal, and the one test that decides it: a colour may be
    // fixed only when the surface UNDER it is fixed too. The footer paints itself
    // #111827 in every theme, so its light grey links are correct as literals; a
    // date on a themed card is not, and reads --elevare-muted instead. The same
    // test explains NAVY (its own section's background), the alert boxes (fixed
    // tint plus fixed text), the star gold and the placeholder SVG (a data URI
    // cannot read a custom property at all). Anything sitting on a surface the
    // palette controls has to read the palette, or the two drift apart the moment
    // a brand colour changes — which is exactly how body copy ended up at 1.72:1
    // against a dark background.
    const BLUE = 'var(--elevare-primary, #2563eb)', GREEN = 'var(--elevare-secondary, #16a34a)',
        NAVY = '#1e3a8a', DARK = 'var(--elevare-heading, #1f2937)', MUTED = 'var(--elevare-muted, #6b7280)',
        SOFT = '#9ca3af', BORDER = 'var(--elevare-border, #e5e7eb)', BG = 'var(--elevare-surface, #f9fafb)',
        WHITE = 'var(--elevare-bg, #ffffff)',
        // Panels, cards, navbars, inputs — anything that used to be a literal white
        // box. This is what makes a block follow the site's theme instead of staying
        // light while the page around it goes dark.
        SURFACE = 'var(--elevare-surface, #ffffff)',
        // One step up from SURFACE: the grey chip a secondary button, a social
        // circle or a close button sits on. These were literal #f3f4f6, which is
        // the failure the dark-mode pass was meant to end — a fixed light box
        // under text that DOES follow the theme, so in dark mode the label turned
        // near-white on near-white and vanished.
        SURFACE2 = 'var(--elevare-surface-2, #f3f4f6)',
        // The text that sits ON a primary-coloured button, badge or pill. It has
        // to be its own variable rather than a literal white, because the thing
        // behind it is --elevare-primary and the brand gets to choose that: white
        // on the seeded dark theme's primary (#ffffff) is white on white, and on an
        // amber primary it is unreadable long before that. A palette that picks a
        // light primary sets this dark, and every primary button follows.
        ON_PRIMARY = 'var(--elevare-on-primary, #ffffff)',
        // Body copy. --elevare-text is the palette's reading colour; the old literal
        // #4b5563 stayed near-black on a dark background.
        TEXT = 'var(--elevare-text, #374151)', TEXT2 = 'var(--elevare-text-2, #4b5563)';

    // --------------------------------------------------
    // PLAIN <button> — survives raw-HTML import as-is
    // --------------------------------------------------
    // grapesjs-plugin-forms claims every bare <button> tag as its own Button
    // component (defaulting its content to the literal text "Send", discarding
    // whatever was actually inside — icons included) the moment one is parsed
    // from imported/raw HTML, which is exactly how custom code and this file's
    // own generated markup both bring buttons in. A real, semantic <button> that
    // just needs to keep its own content (an icon-only toggle, for instance)
    // couldn't survive that without becoming a plain <div role="button"> instead
    // — worse markup to work around a naming collision. Marking one with
    // data-elevare-btn opts it into a plain component type instead: registered
    // after grapesjs-plugin-forms (this whole file loads as the last plugin, see
    // grapes-editor.js's PLUGIN_DEFS), so GrapesJS checks it first and the tag
    // keeps being a real, screen-reader-focusable button with whatever markup it
    // already had — no forced default content, no trait injection.
    comps.addType('elevare-plain-button', {
        isComponent: (el) => el.tagName === 'BUTTON' && el.hasAttribute('data-elevare-btn'),
        model: { defaults: { tagName: 'button', droppable: true } }
    });

    // The opt-in above only helps someone who knows about it; anyone else's
    // <button> (tabs, menu toggles, icon buttons in custom code) still went
    // through the forms plugin's own init, which keeps the content only when it
    // is a single text node and otherwise replaces it with "Send" — a span, an
    // icon or an aria-labelled empty button was wiped, silently and for good once
    // the page was saved. Re-registering the same type keeps everything else the
    // plugin defines and replaces only that init:
    //   - nothing inside and no accessible name: a button fresh from the forms
    //     blocks, which carry no content of their own — gets the default text;
    //   - a single text node: editable through the "text" trait, as before;
    //   - anything else: left exactly as it is, and the "text" trait goes,
    //     since setting it would replace the whole content with plain text.
    comps.addType('button', {
        model: {
            init() {
                const children = this.components();
                const only = children.length === 1 ? children.at(0) : null;
                if (only && only.is('textnode')) {
                    this.set('text', only.get('content'));
                } else if (children.length === 0 && !hasAccessibleName(this)) {
                    this.__onTextChange();
                } else {
                    this.removeTrait('text');
                    return;
                }
                this.on('change:text', this.__onTextChange);
            }
        }
    });

    function hasAccessibleName(component) {
        const attrs = component.getAttributes();
        return ['aria-label', 'aria-labelledby', 'title'].some((name) => (attrs[name] || '').trim() !== '');
    }

    // --------------------------------------------------
    // BLOCK NAME TRANSLATIONS
    // --------------------------------------------------
    const LOCALE = (document.documentElement.lang || 'en').split('-')[0].toLowerCase();
    const LABELS_TR = {
        'elevare-hero': 'Hero Bölümü',
        'elevare-hero-image': 'Görselli Hero',
        'elevare-features': 'Özellik Izgarası',
        'elevare-pricing': 'Fiyat Planları',
        'elevare-cta': 'Eylem Çağrısı',
        'elevare-testimonials': 'Referanslar',
        'elevare-slider': 'Görsel Kaydırıcı',
        'elevare-beforeafter': 'Öncesi / Sonrası',
        'elevare-faq': 'SSS (Sıkça Sorulan Sorular)',
        'elevare-accordion': 'Açılır Kapanır Bölümler',
        'elevare-cards': 'Kart Izgarası',
        'elevare-team': 'Ekip Bölümü',
        'elevare-logos': 'Logo Şeridi',
        'elevare-stats': 'İstatistik Sayacı',
        'elevare-timeline': 'Zaman Çizelgesi',
        'elevare-gallery': 'Galeri (Masonry)',
        'elevare-contact': 'İletişim Formu',
        'elevare-file-field': 'Dosya Alanı',
        'elevare-video': 'Video Yerleştirme',
        'elevare-map': 'Google Harita',
        'elevare-footer': 'Alt Bilgi',
        'elevare-newsletter': 'Bülten Kaydı',
        'elevare-progress': 'İlerleme Çubukları',
        'elevare-navbar': 'Menü Çubuğu',
        'elevare-breadcrumbs': 'Sayfa Yolu',
        'elevare-article': 'Makale',
        'elevare-split': 'Görsel + Metin',
        'elevare-steps': 'Adımlar / Nasıl Çalışır',
        'elevare-checklist': 'Kontrol Listesi',
        'elevare-table': 'Karşılaştırma Tablosu',
        'elevare-announce': 'Duyuru Çubuğu',
        'elevare-authorbio': 'Yazar Tanıtımı',
        'elevare-toc': 'İçindekiler',
        'elevare-quote': 'Alıntı',
        'elevare-localinfo': 'Yerel İşletme Bilgisi',
        'elevare-appbtns': 'Uygulama Butonları',
        'elevare-social': 'Sosyal Bağlantılar',
        'elevare-alert': 'Uyarı / Bilgi Kutusu',
        'elevare-spacer': 'Boşluk / Ayırıcı',
        'elevare-icon-button': 'İkonlu Buton',
        'elevare-theme-switch': 'Tema Değiştirici',
        'elevare-videofacade': 'Video (Hafif Yükleme)',
        'elevare-popup': 'Pop-up',
        'elevare-multistep-form': 'Çok Adımlı Form',
        'elevare-lang-switcher': 'Dil Değiştirici',
        'elevare-megamenu': 'Mega Menü',
        'elevare-page-listing': 'Sayfa Listesi',
        'elevare-search-box': 'Arama Kutusu',
        'elevare-whatsapp': 'WhatsApp Butonu',
        'elevare-sidebar': 'Yan Panel',
        'elevare-preferred-source': 'Google Tercihli Kaynak',
        'elevare-share': 'Paylaşım Butonları',
        'elevare-google-review': 'Google\'da Yorum Yaz',
        'elevare-directions': 'Yol Tarifi Al',
        'elevare-add-calendar': 'Takvime Ekle',
        'elevare-youtube-subscribe': 'YouTube Abone Ol',
        'elevare-google-news': 'Google Haberler\'de Takip Et',
        'elevare-code': 'Kod Bloğu',
        'elevare-prevnext': 'Önceki / Sonraki Yazı',
        'elevare-related-posts': 'İlgili Yazılar',
        'elevare-figure': 'Altyazılı Görsel',
        'elevare-reading-progress': 'Okuma İlerleme Çubuğu',
        'elevare-tabs': 'Sekmeler',
        'elevare-back-to-top': 'Yukarı Çık Butonu'
    };
    const blockLabel = (id, fallback) =>
        (LOCALE === 'tr' && LABELS_TR[id]) ? LABELS_TR[id] : fallback;

    // --------------------------------------------------
    // SUB-CATEGORIES — 48 blocks in one "Elevare UI Kit" bucket meant scrolling
    // past everything to find one block. Grouped by what an editor is actually
    // trying to build, not by markup shape, so the split matches how someone
    // searches for a block rather than how it happens to be implemented.
    // --------------------------------------------------
    const CATEGORY_LABELS = {
        hero: { en: 'Elevare — Hero & Showcase', tr: 'Elevare — Hero & Vitrin' },
        content: { en: 'Elevare — Content', tr: 'Elevare — İçerik' },
        blog: { en: 'Elevare — Blog & Article', tr: 'Elevare — Blog & Yazı' },
        media: { en: 'Elevare — Media', tr: 'Elevare — Medya' },
        forms: { en: 'Elevare — Forms & Interaction', tr: 'Elevare — Form & Etkileşim' },
        nav: { en: 'Elevare — Navigation & Site Structure', tr: 'Elevare — Navigasyon & Site Yapısı' },
        google: { en: 'Elevare — Google & Sharing', tr: 'Elevare — Google & Paylaşım' }
    };
    const CATEGORY_MAP = {
        'elevare-hero': 'hero', 'elevare-hero-image': 'hero', 'elevare-cta': 'hero',
        'elevare-announce': 'hero', 'elevare-popup': 'hero', 'elevare-alert': 'hero',

        'elevare-features': 'content', 'elevare-pricing': 'content', 'elevare-testimonials': 'content',
        'elevare-team': 'content', 'elevare-logos': 'content', 'elevare-stats': 'content',
        'elevare-timeline': 'content', 'elevare-gallery': 'content', 'elevare-split': 'content',
        'elevare-cards': 'content', 'elevare-accordion': 'content',
        'elevare-steps': 'content', 'elevare-checklist': 'content', 'elevare-table': 'content',

        'elevare-article': 'blog', 'elevare-toc': 'blog',
        'elevare-authorbio': 'blog', 'elevare-quote': 'blog', 'elevare-localinfo': 'blog',

        'elevare-slider': 'media', 'elevare-beforeafter': 'media', 'elevare-video': 'media',
        'elevare-videofacade': 'media', 'elevare-map': 'media',

        'elevare-contact': 'forms', 'elevare-newsletter': 'forms', 'elevare-multistep-form': 'forms',
        'elevare-file-field': 'forms', 'elevare-faq': 'forms', 'elevare-search-box': 'forms',
        'elevare-icon-button': 'forms', 'elevare-theme-switch': 'forms', 'elevare-whatsapp': 'forms',

        'elevare-navbar': 'nav', 'elevare-breadcrumbs': 'nav', 'elevare-footer': 'nav',
        'elevare-megamenu': 'nav', 'elevare-lang-switcher': 'nav', 'elevare-page-listing': 'nav',
        'elevare-progress': 'nav', 'elevare-social': 'nav', 'elevare-appbtns': 'nav',
        'elevare-spacer': 'nav', 'elevare-sidebar': 'nav',

        'elevare-preferred-source': 'google', 'elevare-share': 'google', 'elevare-google-review': 'google',
        'elevare-directions': 'google', 'elevare-add-calendar': 'google', 'elevare-youtube-subscribe': 'google',
        'elevare-google-news': 'google',

        'elevare-code': 'blog', 'elevare-prevnext': 'blog', 'elevare-related-posts': 'blog',
        'elevare-figure': 'media', 'elevare-reading-progress': 'blog', 'elevare-tabs': 'content',
        'elevare-back-to-top': 'nav'
    };
    const blockCategory = (id) => {
        const key = CATEGORY_MAP[id];
        const labels = key && CATEGORY_LABELS[key];
        if (!labels) return category; // fallback: the plain 'Elevare UI Kit' bucket
        return { id: 'elevare-cat-' + key, label: LOCALE === 'tr' ? labels.tr : labels.en };
    };

    // Read on every call, not captured once at plugin-load time: Blazor pushes this
    // in with setSiteInfo, and an editor that reloads its settings mid-session (or
    // pushes them a second time) would otherwise keep serving whatever happened to
    // be there the moment this file ran. The one thing that genuinely cannot be
    // live is a trait's `value:` default — GrapesJS reads those when the type is
    // registered — so those still reflect load time, and only load time.
    const siteInfo = () => (window.grapesEditor && window.grapesEditor.getSiteInfo)
        ? (window.grapesEditor.getSiteInfo() || {}) : {};
    const siteValue = (key, fallback) => {
        const v = siteInfo()[key];
        return (typeof v === 'string' && v.trim().length) ? v.trim() : fallback;
    };

    // Text a block writes INTO the page — a placeholder, "Sonuç bulunamadı.", a
    // screen-reader label — is in the PAGE's language, not the CMS's (LOCALE is for
    // the editor's own labels). The public site puts these same defaults into the
    // page's language again on every render (BlockTextLocalizer), so a block placed
    // before this existed, or copied between languages, still reads right.
    const contentLang = () => (siteValue('pageLanguage', LOCALE) || 'en').toLowerCase().startsWith('tr') ? 'tr' : 'en';
    const CT = (tr, en) => (contentLang() === 'tr' ? tr : en);

    // Contact + social details, already filled in on the Site Settings screen. A
    // block that ships a sample address or a social icon pointing at "#" reads as
    // finished when it is not, and every one of these blocks used to do exactly
    // that while the real values sat one screen away.
    const contactValue = (key, fallback) => {
        const contact = siteInfo().contact;
        const v = contact ? contact[key] : null;
        return (typeof v === 'string' && v.trim().length) ? v.trim() : fallback;
    };

    // A block's parts, found in its component tree. GrapesJS's own find() asks the
    // canvas DOM, which does not exist yet while a block is being set up (init,
    // onLoad, onInit) — so there it found nothing, and said nothing: the Article
    // block's date trait always read the default, the Kod Bloğu was never locked.
    // Simple selectors only — a tag, .class, [attr], [attr="value"] — comma-separated.
    const descendants = (m) => m.components().reduce((all, c) => all.concat([c], descendants(c)), []);
    const findModels = (m, selector) => {
        const tests = selector.split(',').map((s) => s.trim()).map((s) => {
            const attr = /^\[([\w-]+)(?:=["']?([^"'\]]*)["']?)?\]$/.exec(s);
            if (attr) {
                return (c) => {
                    const v = c.getAttributes()[attr[1]];
                    return attr[2] === undefined ? v !== undefined : String(v) === attr[2];
                };
            }
            if (s[0] === '.') return (c) => c.getClasses().indexOf(s.slice(1)) >= 0;
            return (c) => String(c.get('tagName') || '').toLowerCase() === s.toLowerCase();
        });
        return descendants(m).filter((c) => tests.some((t) => t(c)));
    };

    // The site's own pages (id + full slug), pushed in by the editor alongside
    // siteInfo. What lets a link be picked from a list instead of a slug being
    // typed by hand — and typed wrong.
    const sitePages = () => (window.grapesEditor && window.grapesEditor.getPageDirectory)
        ? (window.grapesEditor.getPageDirectory() || []) : [];
    const sitePageHref = (pageId) => {
        const page = pageId ? sitePages().find((p) => String(p.id) === String(pageId)) : null;
        return page ? '/' + page.label : '';
    };

    // --------------------------------------------------
    // PAGE PICKER TRAIT — type 'elevare-page'
    // --------------------------------------------------
    // A <select> of every page is fine for five pages and useless for five
    // hundred. This is a text field with a <datalist> behind it: type, the browser
    // filters the site's pages as you go, pick one — native, keyboard-friendly,
    // nothing to scroll through. The value it stores is the page's id (what the
    // Page Listing block and the Buton block already keep); what it shows is the
    // path. Works as a prop (changeProp) or an attribute alike, through
    // setTargetValue. Also used from grapes-editor.js for every plain <a>.
    const PAGE_TRAIT = 'elevare-page';
    editor.TraitManager.addType(PAGE_TRAIT, {
        // A text field over a custom filter list. This used to lean on a native
        // <datalist>, which reads well but is unreliable: on several browsers/OS the
        // suggestion popup simply never appears, so the "search pages" field looked
        // broken — you could type but nothing to pick. This builds its own dropdown
        // (ordinary DOM, styled in app.css), so it shows and filters everywhere. The
        // menu sits in normal flow rather than absolutely positioned, so the trait
        // panel's own scroll never clips it.
        createInput() {
            const wrap = document.createElement('div');
            wrap.className = 'gjs-field elevare-page-pick';
            const input = document.createElement('input');
            input.type = 'text';
            input.autocomplete = 'off';
            input.placeholder = LOCALE === 'tr' ? 'Sayfa ara…' : 'Search pages…';
            const menu = document.createElement('div');
            menu.className = 'elevare-page-menu';
            menu.hidden = true;
            wrap.appendChild(input);
            wrap.appendChild(menu);

            const render = () => {
                const q = input.value.trim().toLowerCase().replace(/^\//, '');
                const matched = sitePages()
                    .filter((p) => {
                        const path = ('/' + p.label).toLowerCase();
                        const title = String(p.title || '').toLowerCase();
                        return !q || path.indexOf(q) >= 0 || title.indexOf(q) >= 0;
                    })
                    .slice(0, 40);
                menu.innerHTML = '';
                matched.forEach((p) => {
                    const item = document.createElement('button');
                    item.type = 'button';
                    item.className = 'elevare-page-item';
                    // The path, and the page's title next to it — "/" alone says little.
                    const path = document.createElement('span');
                    path.textContent = '/' + p.label;
                    item.appendChild(path);
                    if (p.title) {
                        const title = document.createElement('span');
                        title.className = 'elevare-page-item-title';
                        title.textContent = p.title;
                        item.appendChild(title);
                    }
                    // mousedown, not click: it fires before the input's blur, so the
                    // pick registers instead of the menu closing out from under it.
                    item.addEventListener('mousedown', (ev) => {
                        ev.preventDefault();
                        input.value = '/' + p.label;
                        menu.hidden = true;
                        input.dispatchEvent(new Event('change', { bubbles: true }));
                    });
                    menu.appendChild(item);
                });
                menu.hidden = matched.length === 0;
            };

            input.addEventListener('focus', render);
            input.addEventListener('input', render);
            input.addEventListener('blur', () => setTimeout(() => { menu.hidden = true; }, 160));
            input.addEventListener('keydown', (ev) => { if (ev.key === 'Escape') menu.hidden = true; });
            return wrap;
        },
        onEvent({ elInput, trait }) {
            const input = elInput.querySelector('input');
            const typed = input.value.trim();
            if (!typed) { trait.setTargetValue(''); return; }
            const page = sitePages().find((p) => '/' + p.label === typed);
            // Not one of the site's pages: leave the stored value alone and show it
            // again, rather than silently keeping a path that points nowhere.
            if (page) trait.setTargetValue(String(page.id));
            else input.value = sitePageHref(readPageTraitValue(trait));
        },
        onUpdate({ elInput, trait }) {
            elInput.querySelector('input').value = sitePageHref(readPageTraitValue(trait));
        }
    });
    // --------------------------------------------------
    // DATE TRAIT — type 'elevare-date'
    // --------------------------------------------------
    // The browser's own date picker instead of a text box asking for "ISO:
    // 2026-01-01" — nothing to get wrong, and the value is always yyyy-MM-dd.
    const DATE_TRAIT = 'elevare-date';
    editor.TraitManager.addType(DATE_TRAIT, {
        createInput() {
            const input = document.createElement('input');
            input.type = 'date';
            input.className = 'gjs-field';
            return input;
        },
        onEvent({ elInput, trait }) {
            trait.setTargetValue(elInput.value || '');
        },
        onUpdate({ elInput, trait }) {
            elInput.value = String(readPageTraitValue(trait) || '').slice(0, 10);
        }
    });

    // Same, with a time: the browser's date-and-time picker, value yyyy-MM-ddTHH:mm.
    const DATETIME_TRAIT = 'elevare-datetime';
    editor.TraitManager.addType(DATETIME_TRAIT, {
        createInput() {
            const input = document.createElement('input');
            input.type = 'datetime-local';
            input.className = 'gjs-field';
            return input;
        },
        onEvent({ elInput, trait }) {
            trait.setTargetValue(elInput.value || '');
        },
        onUpdate({ elInput, trait }) {
            elInput.value = String(readPageTraitValue(trait) || '').slice(0, 16);
        }
    });

    const readPageTraitValue = (trait) => {
        if (typeof trait.getTargetValue === 'function') return trait.getTargetValue();
        const target = trait.target;
        return trait.get('changeProp') ? target.get(trait.get('name')) : target.getAttributes()[trait.get('name')];
    };

    // --------------------------------------------------
    // HINTED TEXT TRAIT — type 'elevare-hint-text'
    // --------------------------------------------------
    // A text box with a line under it saying what goes in, where to find it and
    // what happens when it is left empty. For the fields whose value comes from
    // somewhere else — a Google Place ID, a Publisher Center address — where the
    // label alone leaves the author guessing.
    const HINT_TEXT_TRAIT = 'elevare-hint-text';
    editor.TraitManager.addType(HINT_TEXT_TRAIT, {
        createInput({ trait }) {
            const wrap = document.createElement('div');
            const input = document.createElement('input');
            input.type = 'text';
            input.className = 'gjs-field';
            input.placeholder = trait.get('placeholder') || '';
            wrap.appendChild(input);
            const hint = trait.get('hint');
            if (hint) {
                const note = document.createElement('div');
                note.textContent = hint;
                note.style.cssText = 'font-size:11px;line-height:1.45;opacity:.75;margin-top:4px;white-space:normal;';
                wrap.appendChild(note);
            }
            return wrap;
        },
        onEvent({ elInput, trait }) {
            trait.setTargetValue(elInput.querySelector('input').value || '');
        },
        onUpdate({ elInput, trait }) {
            elInput.querySelector('input').value = String(readPageTraitValue(trait) || '');
        }
    });

    /// Street, district, region, country — whichever of them are actually filled in.
    const siteAddressLine = (fallback) => {
        const parts = ['address', 'addressLocality', 'addressRegion', 'addressCountry']
            .map((k) => contactValue(k, ''))
            .filter((p) => p.length);
        return parts.length ? parts.join(', ') : fallback;
    };

    const rep = (n, fn) => Array.from({ length: Math.max(0, n | 0) }, (_, i) => fn(i)).join('');

    // --------------------------------------------------
    // BLOCK-LEVEL CSS — rules a block needs on every page it is on
    // --------------------------------------------------
    // Added once at plugin time AND again after the page loads. The second one is
    // the one that matters: loadProjectData replaces the editor's stylesheet with
    // whatever the page was saved with, so a rule added before it is simply gone
    // on every page saved earlier — it only ever reached pages saved AFTER the
    // rule was written. Measured on the live homepage: the slide-caption rule was
    // deployed, and getCss() still did not have it. Re-adding is idempotent; the
    // composer merges into an existing rule with the same selector/state/media.
    const BLOCK_CSS = [];
    const blockCss = (css) => {
        BLOCK_CSS.push(css);
        editor.Css.addRules(css);
    };
    editor.on('load', () => BLOCK_CSS.forEach((css) => { try { editor.Css.addRules(css); } catch (e) { /* keep the rest */ } }));
    const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
    const uid = () => 'el' + Math.random().toString(36).slice(2, 8);
    const SR = 'position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap;';

    // --------------------------------------------------
    // PLACEHOLDER IMAGE
    // --------------------------------------------------
    // Every image-bearing block used to ship with a https://picsum.photos/... URL.
    // That is a third-party photo service, and a block whose picture the author
    // never got round to replacing carried it all the way onto the published page:
    // a request to someone else's server on every visit, the visitor's IP handed to
    // it, and a picture that silently changes (or 404s) whenever that service feels
    // like it. A self-contained data: URI has none of those properties — it needs no
    // network at all, renders identically offline, cannot be tracked, and reads
    // obviously as "replace me" instead of passing for finished artwork.
    //
    // Kept deliberately small (a rect and a label): these are inlined into the saved
    // HTML, so every extra byte is a byte in the page. encodeURIComponent is what
    // makes the "#" in the colours safe — raw, it would be read as a fragment and
    // truncate the whole URI.
    const ph = (w, h, label) => {
        const fontSize = Math.max(12, Math.round(Math.min(w, h) / 9));
        return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(
            `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">` +
            `<rect width="100%" height="100%" fill="#e5e7eb"/>` +
            `<text x="50%" y="50%" fill="#9ca3af" font-family="system-ui,-apple-system,sans-serif" ` +
            `font-size="${fontSize}" text-anchor="middle" dominant-baseline="middle">${label}</text></svg>`);
    };

    // --------------------------------------------------
    // ICONS — inline SVG, no icon font
    // --------------------------------------------------
    // These blocks used to render <i class="bi bi-*"> and rely on Bootstrap
    // Icons being loaded separately. That stylesheet is not guaranteed to exist
    // on the live site at all — it is only ever there if an operator added it
    // to Site Codes themselves (see _canvas-preview.css) — so a block that
    // needed it could look complete in the builder and ship with every icon
    // silently missing. Each path below is copied verbatim from the Bootstrap
    // Icons 1.11.3 source so the glyph itself is unchanged; only the delivery
    // mechanism is — inline markup that paints wherever it is dropped, the
    // same reasoning as the Theme Switch block's hand-drawn sun/moon.
    const ICONS = {
        'check2': '<path d="M13.854 3.646a.5.5 0 0 1 0 .708l-7 7a.5.5 0 0 1-.708 0l-3.5-3.5a.5.5 0 1 1 .708-.708L6.5 10.293l6.646-6.647a.5.5 0 0 1 .708 0"/>',
        'check2-circle': '<path d="M2.5 8a5.5 5.5 0 0 1 8.25-4.764.5.5 0 0 0 .5-.866A6.5 6.5 0 1 0 14.5 8a.5.5 0 0 0-1 0 5.5 5.5 0 1 1-11 0"/><path d="M15.354 3.354a.5.5 0 0 0-.708-.708L8 9.293 5.354 6.646a.5.5 0 1 0-.708.708l3 3a.5.5 0 0 0 .708 0z"/>',
        'check-lg': '<path d="M12.736 3.97a.733.733 0 0 1 1.047 0c.286.289.29.756.01 1.05L7.88 12.01a.733.733 0 0 1-1.065.02L3.217 8.384a.757.757 0 0 1 0-1.06.733.733 0 0 1 1.047 0l3.052 3.093 5.4-6.425z"/>',
        'star-fill': '<path d="M3.612 15.443c-.386.198-.824-.149-.746-.592l.83-4.73L.173 6.765c-.329-.314-.158-.888.283-.95l4.898-.696L7.538.792c.197-.39.73-.39.927 0l2.184 4.327 4.898.696c.441.062.612.636.282.95l-3.522 3.356.83 4.73c.078.443-.36.79-.746.592L8 13.187l-4.389 2.256z"/>',
        'quote': '<path d="M12 12a1 1 0 0 0 1-1V8.558a1 1 0 0 0-1-1h-1.388q0-.527.062-1.054.093-.558.31-.992t.559-.683q.34-.279.868-.279V3q-.868 0-1.52.372a3.3 3.3 0 0 0-1.085.992 4.9 4.9 0 0 0-.62 1.458A7.7 7.7 0 0 0 9 7.558V11a1 1 0 0 0 1 1zm-6 0a1 1 0 0 0 1-1V8.558a1 1 0 0 0-1-1H4.612q0-.527.062-1.054.094-.558.31-.992.217-.434.559-.683.34-.279.868-.279V3q-.868 0-1.52.372a3.3 3.3 0 0 0-1.085.992 4.9 4.9 0 0 0-.62 1.458A7.7 7.7 0 0 0 3 7.558V11a1 1 0 0 0 1 1z"/>',
        'geo-alt': '<path d="M12.166 8.94c-.524 1.062-1.234 2.12-1.96 3.07A32 32 0 0 1 8 14.58a32 32 0 0 1-2.206-2.57c-.726-.95-1.436-2.008-1.96-3.07C3.304 7.867 3 6.862 3 6a5 5 0 0 1 10 0c0 .862-.305 1.867-.834 2.94M8 16s6-5.686 6-10A6 6 0 0 0 2 6c0 4.314 6 10 6 10"/><path d="M8 8a2 2 0 1 1 0-4 2 2 0 0 1 0 4m0 1a3 3 0 1 0 0-6 3 3 0 0 0 0 6"/>',
        'telephone': '<path d="M3.654 1.328a.678.678 0 0 0-1.015-.063L1.605 2.3c-.483.484-.661 1.169-.45 1.77a17.6 17.6 0 0 0 4.168 6.608 17.6 17.6 0 0 0 6.608 4.168c.601.211 1.286.033 1.77-.45l1.034-1.034a.678.678 0 0 0-.063-1.015l-2.307-1.794a.68.68 0 0 0-.58-.122l-2.19.547a1.75 1.75 0 0 1-1.657-.459L5.482 8.062a1.75 1.75 0 0 1-.46-1.657l.548-2.19a.68.68 0 0 0-.122-.58zM1.884.511a1.745 1.745 0 0 1 2.612.163L6.29 2.98c.329.423.445.974.315 1.494l-.547 2.19a.68.68 0 0 0 .178.643l2.457 2.457a.68.68 0 0 0 .644.178l2.189-.547a1.75 1.75 0 0 1 1.494.315l2.306 1.794c.829.645.905 1.87.163 2.611l-1.034 1.034c-.74.74-1.846 1.065-2.877.702a18.6 18.6 0 0 1-7.01-4.42 18.6 18.6 0 0 1-4.42-7.009c-.362-1.03-.037-2.137.703-2.877z"/>',
        'phone': '<path d="M11 1a1 1 0 0 1 1 1v12a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V2a1 1 0 0 1 1-1zM5 0a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V2a2 2 0 0 0-2-2z"/><path d="M8 14a1 1 0 1 0 0-2 1 1 0 0 0 0 2"/>',
        'envelope': '<path d="M0 4a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2zm2-1a1 1 0 0 0-1 1v.217l7 4.2 7-4.2V4a1 1 0 0 0-1-1zm13 2.383-4.708 2.825L15 11.105zm-.034 6.876-5.64-3.471L8 9.583l-1.326-.795-5.64 3.47A1 1 0 0 0 2 13h12a1 1 0 0 0 .966-.741M1 11.105l4.708-2.897L1 5.383z"/>',
        'chevron-down': '<path fill-rule="evenodd" d="M1.646 4.646a.5.5 0 0 1 .708 0L8 10.293l5.646-5.647a.5.5 0 0 1 .708.708l-6 6a.5.5 0 0 1-.708 0l-6-6a.5.5 0 0 1 0-.708"/>',
        'list': '<path fill-rule="evenodd" d="M2.5 12a.5.5 0 0 1 .5-.5h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5m0-4a.5.5 0 0 1 .5-.5h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5m0-4a.5.5 0 0 1 .5-.5h10a.5.5 0 0 1 0 1H3a.5.5 0 0 1-.5-.5"/>',
        'x-lg': '<path d="M2.146 2.854a.5.5 0 1 1 .708-.708L8 7.293l5.146-5.147a.5.5 0 0 1 .708.708L8.707 8l5.147 5.146a.5.5 0 0 1-.708.708L8 8.707l-5.146 5.147a.5.5 0 0 1-.708-.708L7.293 8z"/>',
        'play-fill': '<path d="m11.596 8.697-6.363 3.692c-.54.313-1.233-.066-1.233-.697V4.308c0-.63.692-1.01 1.233-.696l6.363 3.692a.802.802 0 0 1 0 1.393"/>',
        'lightning-charge': '<path d="M11.251.068a.5.5 0 0 1 .227.58L9.677 6.5H13a.5.5 0 0 1 .364.843l-8 8.5a.5.5 0 0 1-.842-.49L6.323 9.5H3a.5.5 0 0 1-.364-.843l8-8.5a.5.5 0 0 1 .615-.09zM4.157 8.5H7a.5.5 0 0 1 .478.647L6.11 13.59l5.732-6.09H9a.5.5 0 0 1-.478-.647L9.89 2.41z"/>',
        'shield-check': '<path d="M5.338 1.59a61 61 0 0 0-2.837.856.48.48 0 0 0-.328.39c-.554 4.157.726 7.19 2.253 9.188a10.7 10.7 0 0 0 2.287 2.233c.346.244.652.42.893.533q.18.085.293.118a1 1 0 0 0 .101.025 1 1 0 0 0 .1-.025q.114-.034.294-.118c.24-.113.547-.29.893-.533a10.7 10.7 0 0 0 2.287-2.233c1.527-1.997 2.807-5.031 2.253-9.188a.48.48 0 0 0-.328-.39c-.651-.213-1.75-.56-2.837-.855C9.552 1.29 8.531 1.067 8 1.067c-.53 0-1.552.223-2.662.524zM5.072.56C6.157.265 7.31 0 8 0s1.843.265 2.928.56c1.11.3 2.229.655 2.887.87a1.54 1.54 0 0 1 1.044 1.262c.596 4.477-.787 7.795-2.465 9.99a11.8 11.8 0 0 1-2.517 2.453 7 7 0 0 1-1.048.625c-.28.132-.581.24-.829.24s-.548-.108-.829-.24a7 7 0 0 1-1.048-.625 11.8 11.8 0 0 1-2.517-2.453C1.928 10.487.545 7.169 1.141 2.692A1.54 1.54 0 0 1 2.185 1.43 63 63 0 0 1 5.072.56"/><path d="M10.854 5.146a.5.5 0 0 1 0 .708l-3 3a.5.5 0 0 1-.708 0l-1.5-1.5a.5.5 0 1 1 .708-.708L7.5 7.793l2.646-2.647a.5.5 0 0 1 .708 0"/>',
        'rocket': '<path d="M8 8c.828 0 1.5-.895 1.5-2S8.828 4 8 4s-1.5.895-1.5 2S7.172 8 8 8"/><path d="M11.953 8.81c-.195-3.388-.968-5.507-1.777-6.819C9.707 1.233 9.23.751 8.857.454a3.5 3.5 0 0 0-.463-.315A2 2 0 0 0 8.25.064.55.55 0 0 0 8 0a.55.55 0 0 0-.266.073 2 2 0 0 0-.142.08 4 4 0 0 0-.459.33c-.37.308-.844.803-1.31 1.57-.805 1.322-1.577 3.433-1.774 6.756l-1.497 1.826-.004.005A2.5 2.5 0 0 0 2 12.202V15.5a.5.5 0 0 0 .9.3l1.125-1.5c.166-.222.42-.4.752-.57.214-.108.414-.192.625-.281l.198-.084c.7.428 1.55.635 2.4.635s1.7-.207 2.4-.635q.1.044.196.083c.213.09.413.174.627.282.332.17.586.348.752.57l1.125 1.5a.5.5 0 0 0 .9-.3v-3.298a2.5 2.5 0 0 0-.548-1.562zM12 10.445v.055c0 .866-.284 1.585-.75 2.14.146.064.292.13.425.199.39.197.8.46 1.1.86L13 14v-1.798a1.5 1.5 0 0 0-.327-.935zM4.75 12.64C4.284 12.085 4 11.366 4 10.5v-.054l-.673.82a1.5 1.5 0 0 0-.327.936V14l.225-.3c.3-.4.71-.664 1.1-.861.133-.068.279-.135.425-.199M8.009 1.073q.096.06.226.163c.284.226.683.621 1.09 1.28C10.137 3.836 11 6.237 11 10.5c0 .858-.374 1.48-.943 1.893C9.517 12.786 8.781 13 8 13s-1.517-.214-2.057-.607C5.373 11.979 5 11.358 5 10.5c0-4.182.86-6.586 1.677-7.928.409-.67.81-1.082 1.096-1.32q.136-.113.236-.18Z"/><path d="M9.479 14.361c-.48.093-.98.139-1.479.139s-.999-.046-1.479-.139L7.6 15.8a.5.5 0 0 0 .8 0z"/>',
        'graph-up': '<path fill-rule="evenodd" d="M0 0h1v15h15v1H0zm14.817 3.113a.5.5 0 0 1 .07.704l-4.5 5.5a.5.5 0 0 1-.74.037L7.06 6.767l-3.656 5.027a.5.5 0 0 1-.808-.588l4-5.5a.5.5 0 0 1 .758-.06l2.609 2.61 4.15-5.073a.5.5 0 0 1 .704-.07"/>',
        'globe': '<path d="M0 8a8 8 0 1 1 16 0A8 8 0 0 1 0 8m7.5-6.923c-.67.204-1.335.82-1.887 1.855A8 8 0 0 0 5.145 4H7.5zM4.09 4a9.3 9.3 0 0 1 .64-1.539 7 7 0 0 1 .597-.933A7.03 7.03 0 0 0 2.255 4zm-.582 3.5c.03-.877.138-1.718.312-2.5H1.674a7 7 0 0 0-.656 2.5zM4.847 5a12.5 12.5 0 0 0-.338 2.5H7.5V5zM8.5 5v2.5h2.99a12.5 12.5 0 0 0-.337-2.5zM4.51 8.5a12.5 12.5 0 0 0 .337 2.5H7.5V8.5zm3.99 0V11h2.653c.187-.765.306-1.608.338-2.5zM5.145 12q.208.58.468 1.068c.552 1.035 1.218 1.65 1.887 1.855V12zm.182 2.472a7 7 0 0 1-.597-.933A9.3 9.3 0 0 1 4.09 12H2.255a7 7 0 0 0 3.072 2.472M3.82 11a13.7 13.7 0 0 1-.312-2.5h-2.49c.062.89.291 1.733.656 2.5zm6.853 3.472A7 7 0 0 0 13.745 12H11.91a9.3 9.3 0 0 1-.64 1.539 7 7 0 0 1-.597.933M8.5 12v2.923c.67-.204 1.335-.82 1.887-1.855q.26-.487.468-1.068zm3.68-1h2.146c.365-.767.594-1.61.656-2.5h-2.49a13.7 13.7 0 0 1-.312 2.5m2.802-3.5a7 7 0 0 0-.656-2.5H12.18c.174.782.282 1.623.312 2.5zM11.27 2.461c.247.464.462.98.64 1.539h1.835a7 7 0 0 0-3.072-2.472c.218.284.418.598.597.933M10.855 4a8 8 0 0 0-.468-1.068C9.835 1.897 9.17 1.282 8.5 1.077V4z"/>',
        'linkedin': '<path d="M0 1.146C0 .513.526 0 1.175 0h13.65C15.474 0 16 .513 16 1.146v13.708c0 .633-.526 1.146-1.175 1.146H1.175C.526 16 0 15.487 0 14.854zm4.943 12.248V6.169H2.542v7.225zm-1.2-8.212c.837 0 1.358-.554 1.358-1.248-.015-.709-.52-1.248-1.342-1.248S2.4 3.226 2.4 3.934c0 .694.521 1.248 1.327 1.248zm4.908 8.212V9.359c0-.216.016-.432.08-.586.173-.431.568-.878 1.232-.878.869 0 1.216.662 1.216 1.634v3.865h2.401V9.25c0-2.22-1.184-3.252-2.764-3.252-1.274 0-1.845.7-2.165 1.193v.025h-.016l.016-.025V6.169h-2.4c.03.678 0 7.225 0 7.225z"/>',
        'twitter-x': '<path d="M12.6.75h2.454l-5.36 6.142L16 15.25h-4.937l-3.867-5.07-4.425 5.07H.316l5.733-6.57L0 .75h5.063l3.495 4.633L12.601.75Zm-.86 13.028h1.36L4.323 2.145H2.865z"/>',
        'github': '<path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27s1.36.09 2 .27c1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8"/>',
        'facebook': '<path d="M16 8.049c0-4.446-3.582-8.05-8-8.05C3.58 0-.002 3.603-.002 8.05c0 4.017 2.926 7.347 6.75 7.951v-5.625h-2.03V8.05H6.75V6.275c0-2.017 1.195-3.131 3.022-3.131.876 0 1.791.157 1.791.157v1.98h-1.009c-.993 0-1.303.621-1.303 1.258v1.51h2.218l-.354 2.326H9.25V16c3.824-.604 6.75-3.934 6.75-7.951"/>',
        'instagram': '<path d="M8 0C5.829 0 5.556.01 4.703.048 3.85.088 3.269.222 2.76.42a3.9 3.9 0 0 0-1.417.923A3.9 3.9 0 0 0 .42 2.76C.222 3.268.087 3.85.048 4.7.01 5.555 0 5.827 0 8.001c0 2.172.01 2.444.048 3.297.04.852.174 1.433.372 1.942.205.526.478.972.923 1.417.444.445.89.719 1.416.923.51.198 1.09.333 1.942.372C5.555 15.99 5.827 16 8 16s2.444-.01 3.298-.048c.851-.04 1.434-.174 1.943-.372a3.9 3.9 0 0 0 1.416-.923c.445-.445.718-.891.923-1.417.197-.509.332-1.09.372-1.942C15.99 10.445 16 10.173 16 8s-.01-2.445-.048-3.299c-.04-.851-.175-1.433-.372-1.941a3.9 3.9 0 0 0-.923-1.417A3.9 3.9 0 0 0 13.24.42c-.51-.198-1.092-.333-1.943-.372C10.443.01 10.172 0 7.998 0zm-.717 1.442h.718c2.136 0 2.389.007 3.232.046.78.035 1.204.166 1.486.275.373.145.64.319.92.599s.453.546.598.92c.11.281.24.705.275 1.485.039.843.047 1.096.047 3.231s-.008 2.389-.047 3.232c-.035.78-.166 1.203-.275 1.485a2.5 2.5 0 0 1-.599.919c-.28.28-.546.453-.92.598-.28.11-.704.24-1.485.276-.843.038-1.096.047-3.232.047s-2.39-.009-3.233-.047c-.78-.036-1.203-.166-1.485-.276a2.5 2.5 0 0 1-.92-.598 2.5 2.5 0 0 1-.6-.92c-.109-.281-.24-.705-.275-1.485-.038-.843-.046-1.096-.046-3.233s.008-2.388.046-3.231c.036-.78.166-1.204.276-1.486.145-.373.319-.64.599-.92s.546-.453.92-.598c.282-.11.705-.24 1.485-.276.738-.034 1.024-.044 2.515-.045zm4.988 1.328a.96.96 0 1 0 0 1.92.96.96 0 0 0 0-1.92m-4.27 1.122a4.109 4.109 0 1 0 0 8.217 4.109 4.109 0 0 0 0-8.217m0 1.441a2.667 2.667 0 1 1 0 5.334 2.667 2.667 0 0 1 0-5.334"/>',
        'apple': '<path d="M11.182.008C11.148-.03 9.923.023 8.857 1.18c-1.066 1.156-.902 2.482-.878 2.516s1.52.087 2.475-1.258.762-2.391.728-2.43m3.314 11.733c-.048-.096-2.325-1.234-2.113-3.422s1.675-2.789 1.698-2.854-.597-.79-1.254-1.157a3.7 3.7 0 0 0-1.563-.434c-.108-.003-.483-.095-1.254.116-.508.139-1.653.589-1.968.607-.316.018-1.256-.522-2.267-.665-.647-.125-1.333.131-1.824.328-.49.196-1.422.754-2.074 2.237-.652 1.482-.311 3.83-.067 4.56s.625 1.924 1.273 2.796c.576.984 1.34 1.667 1.659 1.899s1.219.386 1.843.067c.502-.308 1.408-.485 1.766-.472.357.013 1.061.154 1.782.539.571.197 1.111.115 1.652-.105.541-.221 1.324-1.059 2.238-2.758q.52-1.185.473-1.282"/>',
        'google-play': '<path d="M14.222 9.374c1.037-.61 1.037-2.137 0-2.748L11.528 5.04 8.32 8l3.207 2.96zm-3.595 2.116L7.583 8.68 1.03 14.73c.201 1.029 1.36 1.61 2.303 1.055zM1 13.396V2.603L6.846 8zM1.03 1.27l6.553 6.05 3.044-2.81L3.333.215C2.39-.341 1.231.24 1.03 1.27"/>',
        'youtube': '<path d="M8.051 1.999h.089c.822.003 4.987.033 6.11.335a2.01 2.01 0 0 1 1.415 1.42c.101.38.172.883.22 1.402l.01.104.022.26.008.104c.065.914.073 1.77.074 1.957v.075c-.001.194-.01 1.108-.082 2.06l-.008.105-.009.104c-.05.572-.124 1.14-.235 1.558a2.01 2.01 0 0 1-1.415 1.42c-1.16.312-5.569.334-6.18.335h-.142c-.309 0-1.587-.006-2.927-.052l-.17-.006-.087-.004-.171-.007-.171-.007c-1.11-.049-2.167-.128-2.654-.26a2.01 2.01 0 0 1-1.415-1.419c-.111-.417-.185-.986-.235-1.558L.09 9.82l-.008-.104A31 31 0 0 1 0 7.68v-.123c.002-.215.01-.958.064-1.778l.007-.103.003-.052.008-.104.022-.26.01-.104c.048-.519.119-1.023.22-1.402a2.01 2.01 0 0 1 1.415-1.42c.487-.13 1.544-.21 2.654-.26l.17-.007.172-.006.086-.003.171-.007A100 100 0 0 1 7.858 2zM6.4 5.209v4.818l4.157-2.408z"/>',
        'info-circle-fill': '<path d="M8 16A8 8 0 1 0 8 0a8 8 0 0 0 0 16m.93-9.412-1 4.705c-.07.34.029.533.304.533.194 0 .487-.07.686-.246l-.088.416c-.287.346-.92.598-1.465.598-.703 0-1.002-.422-.808-1.319l.738-3.468c.064-.293.006-.399-.287-.47l-.451-.081.082-.381 2.29-.287zM8 5.5a1 1 0 1 1 0-2 1 1 0 0 1 0 2"/>',
        'check-circle-fill': '<path d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0m-3.97-3.03a.75.75 0 0 0-1.08.022L7.477 9.417 5.384 7.323a.75.75 0 0 0-1.06 1.06L6.97 11.03a.75.75 0 0 0 1.079-.02l3.992-4.99a.75.75 0 0 0-.01-1.05z"/>',
        'exclamation-triangle-fill': '<path d="M8.982 1.566a1.13 1.13 0 0 0-1.96 0L.165 13.233c-.457.778.091 1.767.98 1.767h13.713c.889 0 1.438-.99.98-1.767zM8 5c.535 0 .954.462.9.995l-.35 3.507a.552.552 0 0 1-1.1 0L7.1 5.995A.905.905 0 0 1 8 5m.002 6a1 1 0 1 1 0 2 1 1 0 0 1 0-2"/>',
        'x-circle-fill': '<path d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0M5.354 4.646a.5.5 0 1 0-.708.708L7.293 8l-2.647 2.646a.5.5 0 0 0 .708.708L8 8.707l2.646 2.647a.5.5 0 0 0 .708-.708L8.707 8l2.647-2.646a.5.5 0 0 0-.708-.708L8 7.293z"/>',
        // Added for the Buton block's icon picker — same Bootstrap Icons 1.11.3
        // source as everything above, fetched rather than hand-copied.
        'arrow-right': '<path fill-rule="evenodd" d="M1 8a.5.5 0 0 1 .5-.5h11.793l-3.147-3.146a.5.5 0 0 1 .708-.708l4 4a.5.5 0 0 1 0 .708l-4 4a.5.5 0 0 1-.708-.708L13.293 8.5H1.5A.5.5 0 0 1 1 8"/>',
        'arrow-left': '<path fill-rule="evenodd" d="M15 8a.5.5 0 0 0-.5-.5H2.707l3.147-3.146a.5.5 0 1 0-.708-.708l-4 4a.5.5 0 0 0 0 .708l4 4a.5.5 0 0 0 .708-.708L2.707 8.5H14.5A.5.5 0 0 0 15 8"/>',
        'box-arrow-up-right': '<path fill-rule="evenodd" d="M8.636 3.5a.5.5 0 0 0-.5-.5H1.5A1.5 1.5 0 0 0 0 4.5v10A1.5 1.5 0 0 0 1.5 16h10a1.5 1.5 0 0 0 1.5-1.5V7.864a.5.5 0 0 0-1 0V14.5a.5.5 0 0 1-.5.5h-10a.5.5 0 0 1-.5-.5v-10a.5.5 0 0 1 .5-.5h6.636a.5.5 0 0 0 .5-.5"/>  <path fill-rule="evenodd" d="M16 .5a.5.5 0 0 0-.5-.5h-5a.5.5 0 0 0 0 1h3.793L6.146 9.146a.5.5 0 1 0 .708.708L15 1.707V5.5a.5.5 0 0 0 1 0z"/>',
        'download': '<path d="M.5 9.9a.5.5 0 0 1 .5.5v2.5a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-2.5a.5.5 0 0 1 1 0v2.5a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2v-2.5a.5.5 0 0 1 .5-.5"/>  <path d="M7.646 11.854a.5.5 0 0 0 .708 0l3-3a.5.5 0 0 0-.708-.708L8.5 10.293V1.5a.5.5 0 0 0-1 0v8.793L5.354 8.146a.5.5 0 1 0-.708.708z"/>',
        'cart3': '<path d="M0 1.5A.5.5 0 0 1 .5 1H2a.5.5 0 0 1 .485.379L2.89 3H14.5a.5.5 0 0 1 .49.598l-1 5a.5.5 0 0 1-.465.401l-9.397.472L4.415 11H13a.5.5 0 0 1 0 1H4a.5.5 0 0 1-.491-.408L2.01 3.607 1.61 2H.5a.5.5 0 0 1-.5-.5M3.102 4l.84 4.479 9.144-.459L13.89 4zM5 12a2 2 0 1 0 0 4 2 2 0 0 0 0-4m7 0a2 2 0 1 0 0 4 2 2 0 0 0 0-4m-7 1a1 1 0 1 1 0 2 1 1 0 0 1 0-2m7 0a1 1 0 1 1 0 2 1 1 0 0 1 0-2"/>',
        'whatsapp': '<path d="M13.601 2.326A7.85 7.85 0 0 0 7.994 0C3.627 0 .068 3.558.064 7.926c0 1.399.366 2.76 1.057 3.965L0 16l4.204-1.102a7.9 7.9 0 0 0 3.79.965h.004c4.368 0 7.926-3.558 7.93-7.93A7.9 7.9 0 0 0 13.6 2.326zM7.994 14.521a6.6 6.6 0 0 1-3.356-.92l-.24-.144-2.494.654.666-2.433-.156-.251a6.56 6.56 0 0 1-1.007-3.505c0-3.626 2.957-6.584 6.591-6.584a6.56 6.56 0 0 1 4.66 1.931 6.56 6.56 0 0 1 1.928 4.66c-.004 3.639-2.961 6.592-6.592 6.592m3.615-4.934c-.197-.099-1.17-.578-1.353-.646-.182-.065-.315-.099-.445.099-.133.197-.513.646-.627.775-.114.133-.232.148-.43.05-.197-.1-.836-.308-1.592-.985-.59-.525-.985-1.175-1.103-1.372-.114-.198-.011-.304.088-.403.087-.088.197-.232.296-.346.1-.114.133-.198.198-.33.065-.134.034-.248-.015-.347-.05-.099-.445-1.076-.612-1.47-.16-.389-.323-.335-.445-.34-.114-.007-.247-.007-.38-.007a.73.73 0 0 0-.529.247c-.182.198-.691.677-.691 1.654s.71 1.916.81 2.049c.098.133 1.394 2.132 3.383 2.992.47.205.84.326 1.129.418.475.152.904.129 1.246.08.38-.058 1.171-.48 1.338-.943.164-.464.164-.86.114-.943-.049-.084-.182-.133-.38-.232"/>',
        'calendar-event': '<path d="M11 6.5a.5.5 0 0 1 .5-.5h1a.5.5 0 0 1 .5.5v1a.5.5 0 0 1-.5.5h-1a.5.5 0 0 1-.5-.5z"/>  <path d="M3.5 0a.5.5 0 0 1 .5.5V1h8V.5a.5.5 0 0 1 1 0V1h1a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H2a2 2 0 0 1-2-2V3a2 2 0 0 1 2-2h1V.5a.5.5 0 0 1 .5-.5M1 4v10a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V4z"/>',
        'clock': '<path d="M8 3.5a.5.5 0 0 0-1 0V9a.5.5 0 0 0 .252.434l3.5 2a.5.5 0 0 0 .496-.868L8 8.71z"/>  <path d="M8 16A8 8 0 1 0 8 0a8 8 0 0 0 0 16m7-8A7 7 0 1 1 1 8a7 7 0 0 1 14 0"/>',
        'chat-dots': '<path d="M5 8a1 1 0 1 1-2 0 1 1 0 0 1 2 0m4 0a1 1 0 1 1-2 0 1 1 0 0 1 2 0m3 1a1 1 0 1 0 0-2 1 1 0 0 0 0 2"/>  <path d="m2.165 15.803.02-.004c1.83-.363 2.948-.842 3.468-1.105A9 9 0 0 0 8 15c4.418 0 8-3.134 8-7s-3.582-7-8-7-8 3.134-8 7c0 1.76.743 3.37 1.97 4.6a10.4 10.4 0 0 1-.524 2.318l-.003.011a11 11 0 0 1-.244.637c-.079.186.074.394.273.362a22 22 0 0 0 .693-.125m.8-3.108a1 1 0 0 0-.287-.801C1.618 10.83 1 9.468 1 8c0-3.192 3.004-6 7-6s7 2.808 7 6-3.004 6-7 6a8 8 0 0 1-2.088-.272 1 1 0 0 0-.711.074c-.387.196-1.24.57-2.634.893a11 11 0 0 0 .398-2"/>',
        'search': '<path d="M11.742 10.344a6.5 6.5 0 1 0-1.397 1.398h-.001q.044.06.098.115l3.85 3.85a1 1 0 0 0 1.415-1.414l-3.85-3.85a1 1 0 0 0-.115-.1zM12 6.5a5.5 5.5 0 1 1-11 0 5.5 5.5 0 0 1 11 0"/>',
        'heart': '<path d="m8 2.748-.717-.737C5.6.281 2.514.878 1.4 3.053c-.523 1.023-.641 2.5.314 4.385.92 1.815 2.834 3.989 6.286 6.357 3.452-2.368 5.365-4.542 6.286-6.357.955-1.886.838-3.362.314-4.385C13.486.878 10.4.28 8.717 2.01zM8 15C-7.333 4.868 3.279-3.04 7.824 1.143q.09.083.176.171a3 3 0 0 1 .176-.17C12.72-3.042 23.333 4.867 8 15"/>',
        'share': '<path d="M13.5 1a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3M11 2.5a2.5 2.5 0 1 1 .603 1.628l-6.718 3.12a2.5 2.5 0 0 1 0 1.504l6.718 3.12a2.5 2.5 0 1 1-.488.876l-6.718-3.12a2.5 2.5 0 1 1 0-3.256l6.718-3.12A2.5 2.5 0 0 1 11 2.5m-8.5 4a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3m11 5.5a1.5 1.5 0 1 0 0 3 1.5 1.5 0 0 0 0-3"/>',
        'printer': '<path d="M2.5 8a.5.5 0 1 0 0-1 .5.5 0 0 0 0 1"/>  <path d="M5 1a2 2 0 0 0-2 2v2H2a2 2 0 0 0-2 2v3a2 2 0 0 0 2 2h1v1a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2v-1h1a2 2 0 0 0 2-2V7a2 2 0 0 0-2-2h-1V3a2 2 0 0 0-2-2zM4 3a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2H4zm1 5a2 2 0 0 0-2 2v1H2a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h12a1 1 0 0 1 1 1v3a1 1 0 0 1-1 1h-1v-1a2 2 0 0 0-2-2zm7 2v3a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1v-3a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1"/>',
        'person': '<path d="M8 8a3 3 0 1 0 0-6 3 3 0 0 0 0 6m2-3a2 2 0 1 1-4 0 2 2 0 0 1 4 0m4 8c0 1-1 1-1 1H3s-1 0-1-1 1-4 6-4 6 3 6 4m-1-.004c-.001-.246-.154-.986-.832-1.664C11.516 10.68 10.289 10 8 10s-3.516.68-4.168 1.332c-.678.678-.83 1.418-.832 1.664z"/>',
        'lock': '<path d="M8 1a2 2 0 0 1 2 2v4H6V3a2 2 0 0 1 2-2m3 6V3a3 3 0 0 0-6 0v4a2 2 0 0 0-2 2v5a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2M5 8h6a1 1 0 0 1 1 1v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V9a1 1 0 0 1 1-1"/>',
        'gear': '<path d="M8 4.754a3.246 3.246 0 1 0 0 6.492 3.246 3.246 0 0 0 0-6.492M5.754 8a2.246 2.246 0 1 1 4.492 0 2.246 2.246 0 0 1-4.492 0"/>  <path d="M9.796 1.343c-.527-1.79-3.065-1.79-3.592 0l-.094.319a.873.873 0 0 1-1.255.52l-.292-.16c-1.64-.892-3.433.902-2.54 2.541l.159.292a.873.873 0 0 1-.52 1.255l-.319.094c-1.79.527-1.79 3.065 0 3.592l.319.094a.873.873 0 0 1 .52 1.255l-.16.292c-.892 1.64.901 3.434 2.541 2.54l.292-.159a.873.873 0 0 1 1.255.52l.094.319c.527 1.79 3.065 1.79 3.592 0l.094-.319a.873.873 0 0 1 1.255-.52l.292.16c1.64.893 3.434-.902 2.54-2.541l-.159-.292a.873.873 0 0 1 .52-1.255l.319-.094c1.79-.527 1.79-3.065 0-3.592l-.319-.094a.873.873 0 0 1-.52-1.255l.16-.292c.893-1.64-.902-3.433-2.541-2.54l-.292.159a.873.873 0 0 1-1.255-.52zm-2.633.283c.246-.835 1.428-.835 1.674 0l.094.319a1.873 1.873 0 0 0 2.693 1.115l.291-.16c.764-.415 1.6.42 1.184 1.185l-.159.292a1.873 1.873 0 0 0 1.116 2.692l.318.094c.835.246.835 1.428 0 1.674l-.319.094a1.873 1.873 0 0 0-1.115 2.693l.16.291c.415.764-.42 1.6-1.185 1.184l-.291-.159a1.873 1.873 0 0 0-2.693 1.116l-.094.318c-.246.835-1.428.835-1.674 0l-.094-.319a1.873 1.873 0 0 0-2.692-1.115l-.292.16c-.764.415-1.6-.42-1.184-1.185l.159-.291A1.873 1.873 0 0 0 1.945 8.93l-.319-.094c-.835-.246-.835-1.428 0-1.674l.319-.094A1.873 1.873 0 0 0 3.06 4.377l-.16-.292c-.415-.764.42-1.6 1.185-1.184l.292.159a1.873 1.873 0 0 0 2.692-1.115z"/>',
        'plus-lg': '<path fill-rule="evenodd" d="M8 2a.5.5 0 0 1 .5.5v5h5a.5.5 0 0 1 0 1h-5v5a.5.5 0 0 1-1 0v-5h-5a.5.5 0 0 1 0-1h5v-5A.5.5 0 0 1 8 2"/>',
        'chevron-right': '<path fill-rule="evenodd" d="M4.646 1.646a.5.5 0 0 1 .708 0l6 6a.5.5 0 0 1 0 .708l-6 6a.5.5 0 0 1-.708-.708L10.293 8 4.646 2.354a.5.5 0 0 1 0-.708"/>',
        'send': '<path d="M15.854.146a.5.5 0 0 1 .11.54l-5.819 14.547a.75.75 0 0 1-1.329.124l-3.178-4.995L.643 7.184a.75.75 0 0 1 .124-1.33L15.314.037a.5.5 0 0 1 .54.11ZM6.636 10.07l2.761 4.338L14.13 2.576zm6.787-8.201L1.591 6.602l4.339 2.76z"/>',
        'bell': '<path d="M8 16a2 2 0 0 0 2-2H6a2 2 0 0 0 2 2M8 1.918l-.797.161A4 4 0 0 0 4 6c0 .628-.134 2.197-.459 3.742-.16.767-.376 1.566-.663 2.258h10.244c-.287-.692-.502-1.49-.663-2.258C12.134 8.197 12 6.628 12 6a4 4 0 0 0-3.203-3.92zM14.22 12c.223.447.481.801.78 1H1c.299-.199.557-.553.78-1C2.68 10.2 3 6.88 3 6c0-2.42 1.72-4.44 4.005-4.901a1 1 0 1 1 1.99 0A5 5 0 0 1 13 6c0 .88.32 4.2 1.22 6"/>',
        'bookmark': '<path d="M2 2a2 2 0 0 1 2-2h8a2 2 0 0 1 2 2v13.5a.5.5 0 0 1-.777.416L8 13.101l-5.223 2.815A.5.5 0 0 1 2 15.5zm2-1a1 1 0 0 0-1 1v12.566l4.723-2.482a.5.5 0 0 1 .554 0L13 14.566V2a1 1 0 0 0-1-1z"/>',
        'camera': '<path d="M15 12a1 1 0 0 1-1 1H2a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1h1.172a3 3 0 0 0 2.12-.879l.83-.828A1 1 0 0 1 6.827 3h2.344a1 1 0 0 1 .707.293l.828.828A3 3 0 0 0 12.828 5H14a1 1 0 0 1 1 1zM2 4a2 2 0 0 0-2 2v6a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2h-1.172a2 2 0 0 1-1.414-.586l-.828-.828A2 2 0 0 0 9.172 2H6.828a2 2 0 0 0-1.414.586l-.828.828A2 2 0 0 1 3.172 4z"/>  <path d="M8 11a2.5 2.5 0 1 1 0-5 2.5 2.5 0 0 1 0 5m0 1a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7M3 6.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0"/>',
        'trophy': '<path d="M2.5.5A.5.5 0 0 1 3 0h10a.5.5 0 0 1 .5.5q0 .807-.034 1.536a3 3 0 1 1-1.133 5.89c-.79 1.865-1.878 2.777-2.833 3.011v2.173l1.425.356c.194.048.377.135.537.255L13.3 15.1a.5.5 0 0 1-.3.9H3a.5.5 0 0 1-.3-.9l1.838-1.379c.16-.12.343-.207.537-.255L6.5 13.11v-2.173c-.955-.234-2.043-1.146-2.833-3.012a3 3 0 1 1-1.132-5.89A33 33 0 0 1 2.5.5m.099 2.54a2 2 0 0 0 .72 3.935c-.333-1.05-.588-2.346-.72-3.935m10.083 3.935a2 2 0 0 0 .72-3.935c-.133 1.59-.388 2.885-.72 3.935M3.504 1q.01.775.056 1.469c.13 2.028.457 3.546.87 4.667C5.294 9.48 6.484 10 7 10a.5.5 0 0 1 .5.5v2.61a1 1 0 0 1-.757.97l-1.426.356a.5.5 0 0 0-.179.085L4.5 15h7l-.638-.479a.5.5 0 0 0-.18-.085l-1.425-.356a1 1 0 0 1-.757-.97V10.5A.5.5 0 0 1 9 10c.516 0 1.706-.52 2.57-2.864.413-1.12.74-2.64.87-4.667q.045-.694.056-1.469z"/>',
        'book': '<path d="M1 2.828c.885-.37 2.154-.769 3.388-.893 1.33-.134 2.458.063 3.112.752v9.746c-.935-.53-2.12-.603-3.213-.493-1.18.12-2.37.461-3.287.811zm7.5-.141c.654-.689 1.782-.886 3.112-.752 1.234.124 2.503.523 3.388.893v9.923c-.918-.35-2.107-.692-3.287-.81-1.094-.111-2.278-.039-3.213.492zM8 1.783C7.015.936 5.587.81 4.287.94c-1.514.153-3.042.672-3.994 1.105A.5.5 0 0 0 0 2.5v11a.5.5 0 0 0 .707.455c.882-.4 2.303-.881 3.68-1.02 1.409-.142 2.59.087 3.223.877a.5.5 0 0 0 .78 0c.633-.79 1.814-1.019 3.222-.877 1.378.139 2.8.62 3.681 1.02A.5.5 0 0 0 16 13.5v-11a.5.5 0 0 0-.293-.455c-.952-.433-2.48-.952-3.994-1.105C10.413.809 8.985.936 8 1.783"/>',
        'briefcase': '<path d="M6.5 1A1.5 1.5 0 0 0 5 2.5V3H1.5A1.5 1.5 0 0 0 0 4.5v8A1.5 1.5 0 0 0 1.5 14h13a1.5 1.5 0 0 0 1.5-1.5v-8A1.5 1.5 0 0 0 14.5 3H11v-.5A1.5 1.5 0 0 0 9.5 1zm0 1h3a.5.5 0 0 1 .5.5V3H6v-.5a.5.5 0 0 1 .5-.5m1.886 6.914L15 7.151V12.5a.5.5 0 0 1-.5.5h-13a.5.5 0 0 1-.5-.5V7.15l6.614 1.764a1.5 1.5 0 0 0 .772 0M1.5 4h13a.5.5 0 0 1 .5.5v1.616L8.129 7.948a.5.5 0 0 1-.258 0L1 6.116V4.5a.5.5 0 0 1 .5-.5"/>',
        'truck': '<path d="M0 3.5A1.5 1.5 0 0 1 1.5 2h9A1.5 1.5 0 0 1 12 3.5V5h1.02a1.5 1.5 0 0 1 1.17.563l1.481 1.85a1.5 1.5 0 0 1 .329.938V10.5a1.5 1.5 0 0 1-1.5 1.5H14a2 2 0 1 1-4 0H5a2 2 0 1 1-3.998-.085A1.5 1.5 0 0 1 0 10.5zm1.294 7.456A2 2 0 0 1 4.732 11h5.536a2 2 0 0 1 .732-.732V3.5a.5.5 0 0 0-.5-.5h-9a.5.5 0 0 0-.5.5v7a.5.5 0 0 0 .294.456M12 10a2 2 0 0 1 1.732 1h.768a.5.5 0 0 0 .5-.5V8.35a.5.5 0 0 0-.11-.312l-1.48-1.85A.5.5 0 0 0 13.02 6H12zm-9 1a1 1 0 1 0 0 2 1 1 0 0 0 0-2m9 0a1 1 0 1 0 0 2 1 1 0 0 0 0-2"/>',
        'eye': '<path d="M16 8s-3-5.5-8-5.5S0 8 0 8s3 5.5 8 5.5S16 8 16 8M1.173 8a13 13 0 0 1 1.66-2.043C4.12 4.668 5.88 3.5 8 3.5s3.879 1.168 5.168 2.457A13 13 0 0 1 14.828 8q-.086.13-.195.288c-.335.48-.83 1.12-1.465 1.755C11.879 11.332 10.119 12.5 8 12.5s-3.879-1.168-5.168-2.457A13 13 0 0 1 1.172 8z"/>  <path d="M8 5.5a2.5 2.5 0 1 0 0 5 2.5 2.5 0 0 0 0-5M4.5 8a3.5 3.5 0 1 1 7 0 3.5 3.5 0 0 1-7 0"/>',
        'telegram': '<path d="M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0M8.287 5.906q-1.168.486-4.666 2.01-.567.225-.595.442c-.03.243.275.339.69.47l.175.055c.408.133.958.288 1.243.294q.39.01.868-.32 3.269-2.206 3.374-2.23c.05-.012.12-.026.166.016s.042.12.037.141c-.03.129-1.227 1.241-1.846 1.817-.193.18-.33.307-.358.336a8 8 0 0 1-.188.186c-.38.366-.664.64.015 1.088.327.216.589.393.85.571.284.194.568.387.936.629q.14.092.27.187c.331.236.63.448.997.414.214-.02.435-.22.547-.82.265-1.417.786-4.486.906-5.751a1.4 1.4 0 0 0-.013-.315.34.34 0 0 0-.114-.217.53.53 0 0 0-.31-.093c-.3.005-.763.166-2.984 1.09"/>',
        'link-45deg': '<path d="M4.715 6.542 3.343 7.914a3 3 0 1 0 4.243 4.243l1.828-1.829A3 3 0 0 0 8.586 5.5L8 6.086a1 1 0 0 0-.154.199 2 2 0 0 1 .861 3.337L6.88 11.45a2 2 0 1 1-2.83-2.83l.793-.792a4 4 0 0 1-.128-1.287z"/> <path d="M6.586 4.672A3 3 0 0 0 7.414 9.5l.775-.776a2 2 0 0 1-.896-3.346L9.12 3.55a2 2 0 1 1 2.83 2.83l-.793.792c.112.42.155.855.128 1.287l1.372-1.372a3 3 0 1 0-4.243-4.243z"/>',
        'newspaper': '<path d="M0 2.5A1.5 1.5 0 0 1 1.5 1h11A1.5 1.5 0 0 1 14 2.5v10.528c0 .3-.05.654-.238.972h.738a.5.5 0 0 0 .5-.5v-9a.5.5 0 0 1 1 0v9a1.5 1.5 0 0 1-1.5 1.5H1.497A1.497 1.497 0 0 1 0 13.5zM12 14c.37 0 .654-.211.853-.441.092-.106.147-.279.147-.531V2.5a.5.5 0 0 0-.5-.5h-11a.5.5 0 0 0-.5.5v11c0 .278.223.5.497.5z"/> <path d="M2 3h10v2H2zm0 3h4v3H2zm0 4h4v1H2zm0 2h4v1H2zm5-6h2v1H7zm3 0h2v1h-2zM7 8h2v1H7zm3 0h2v1h-2zm-3 2h2v1H7zm3 0h2v1h-2zm-3 2h2v1H7zm3 0h2v1h-2z"/>',
        'sign-turn-right-fill': '<path d="M9.05.435c-.58-.58-1.52-.58-2.1 0L.436 6.95c-.58.58-.58 1.519 0 2.098l6.516 6.516c.58.58 1.519.58 2.098 0l6.516-6.516c.58-.58.58-1.519 0-2.098zM9 8.466V7H7.5A1.5 1.5 0 0 0 6 8.5V11H5V8.5A2.5 2.5 0 0 1 7.5 6H9V4.534a.25.25 0 0 1 .41-.192l2.36 1.966c.12.1.12.284 0 .384L9.41 8.658A.25.25 0 0 1 9 8.466"/>',
        'arrow-up': '<path fill-rule="evenodd" d="M8 15a.5.5 0 0 0 .5-.5V2.707l3.146 3.147a.5.5 0 0 0 .708-.708l-4-4a.5.5 0 0 0-.708 0l-4 4a.5.5 0 1 0 .708.708L7.5 2.707V14.5a.5.5 0 0 0 .5.5"/>',
        'code-slash': '<path d="M10.478 1.647a.5.5 0 1 0-.956-.294l-4 13a.5.5 0 0 0 .956.294zM4.854 4.146a.5.5 0 0 1 0 .708L1.707 8l3.147 3.146a.5.5 0 0 1-.708.708l-3.5-3.5a.5.5 0 0 1 0-.708l3.5-3.5a.5.5 0 0 1 .708 0m6.292 0a.5.5 0 0 0 0 .708L14.293 8l-3.147 3.146a.5.5 0 0 0 .708.708l3.5-3.5a.5.5 0 0 0 0-.708l-3.5-3.5a.5.5 0 0 0-.708 0"/>',
        'pencil': '<path d="M12.146.146a.5.5 0 0 1 .708 0l3 3a.5.5 0 0 1 0 .708l-10 10a.5.5 0 0 1-.168.11l-5 2a.5.5 0 0 1-.65-.65l2-5a.5.5 0 0 1 .11-.168zM11.207 2.5 13.5 4.793 14.793 3.5 12.5 1.207zm1.586 3L10.5 3.207 4 9.707V10h.5a.5.5 0 0 1 .5.5v.5h.5a.5.5 0 0 1 .5.5v.5h.293zm-9.761 5.175-.106.106-1.528 3.821 3.821-1.528.106-.106A.5.5 0 0 1 5 12.5V12h-.5a.5.5 0 0 1-.5-.5V11h-.5a.5.5 0 0 1-.468-.325"/>',
    };

    /**
     * Inline SVG standing in for `<i class="bi bi-${name}">`. `width`/`height`
     * are set in `em` so the glyph still scales off `font-size` the same way an
     * icon font did — the caller's existing `style="font-size:...;color:..."`
     * moves over unchanged, `fill="currentColor"` reads the same `color`.
     * <para>
     * GrapesJS only auto-recognises an element as double-click-editable "text"
     * when every child is plain text — a nested <svg> (its own recognised
     * component type) disqualifies the parent, silently. An `<i>` icon font
     * glyph never tripped this, so the SVG swap made containers that mix an icon
     * with a bare text node stop being editable.
     * <para>
     * Forcing `data-gjs-type="text"` back on was the first answer and it was the
     * wrong one: it restores editing by making the container ONE text component,
     * which swallows the icon — the <svg> stops being a component at all, so it
     * cannot be selected, and there is no way left to delete it. Both places that
     * used it (the Mega Menu trigger, the Pricing Plans checklist items) hit that.
     * <para>
     * The fix that actually works is structural: give the label its own <span> so
     * the icon and the text are SIBLING components. The span is editable because
     * all of ITS children are text, and the icon stays an ordinary component you
     * can select and remove. Wrap the label — never type the container.
     * </para>
     */
    // width/height are ATTRIBUTES as well as CSS, and that redundancy is the whole
    // point. The CSS is what sizes the glyph normally (1em, so it tracks the
    // surrounding font) — but GrapesJS moves inline styles into a rule keyed by the
    // component's id, and any moment the element exists without that rule the SVG
    // has no intrinsic size and stretches to fill its container. Measured in the
    // page editor with the canvas stylesheets off: a 16px social icon became
    // 1717x1717. The attributes are a floor the CSS then refines, and they cannot
    // be separated from the element the way a rule can.
    const icon = (name, style) =>
        `<svg viewBox="0 0 16 16" width="16" height="16" fill="currentColor" aria-hidden="true" style="width:1em;height:1em;display:inline-block;vertical-align:-0.125em;flex-shrink:0;${style || ''}">${ICONS[name] || ''}</svg>`;

    /**
     * Swaps the glyph of an existing <svg> component by replacing the WHOLE element
     * with freshly built markup.
     *
     * The obvious way — `svgComponent.components(ICONS[name])` — is broken, and
     * silently. Several glyphs here are two or three <path> elements, and GrapesJS
     * parses that fragment as HTML, where <path> is an unknown element and therefore
     * NOT self-closing: `<path d="A"/><path d="B"/>` comes back as ONE component
     * with the second path nested INSIDE the first. Verified against the live
     * editor's own parser. The affected glyphs were exactly the multi-path ones —
     * Alert's "info" and "warning" types, Icon Button's location/phone/rocket — so
     * picking one of those drew a mangled shape.
     *
     * A complete `<svg>…</svg>` string parses correctly (it becomes an `svg`
     * component with `svg-in` children), so replacing the element sidesteps the
     * fragment problem entirely. `replaceWith` is guarded because it is the one
     * GrapesJS API here that is not on the documented Component surface of every
     * 0.21.x patch; the fallback does the same thing by hand, at the same index so
     * the icon does not jump to the end of its parent.
     */
    const swapIcon = (svgComponent, svgHtml) => {
        if (!svgComponent || !svgHtml) return null;
        if (typeof svgComponent.replaceWith === 'function') {
            const replaced = svgComponent.replaceWith(svgHtml);
            return Array.isArray(replaced) ? replaced[0] : replaced;
        }
        const parent = svgComponent.parent && svgComponent.parent();
        if (!parent) return null;
        const at = parent.components().indexOf(svgComponent);
        svgComponent.remove();
        return parent.append(svgHtml, { at: at < 0 ? undefined : at })[0];
    };

    // --------------------------------------------------
    // SMART TYPE FACTORY
    // --------------------------------------------------
    // Every block stamps its id here; see isComponent in smart().
    const BLOCK_ATTR = 'data-elevare-block';

    function smart(def) {
        const typeId = def.id + '-type';
        const localizedLabel = blockLabel(def.id, def.label);
        const propDefaults = {};
        (def.traits || []).forEach((t) => {
            if (t.changeProp && t.value != null) propDefaults[t.name] = t.value;
        });

        comps.addType(typeId, {
            // Recognises the block again when it comes back as plain HTML — which is
            // how a template lands on a page (insertLinkedTemplate /
            // insertSnapshotTemplate hand over gjsHtml, not the project JSON), and
            // how raw import works. Without this a Search Box copied from the Üst
            // Menü template arrived on the homepage as an anonymous <div>: no
            // script, so its results panel sat permanently open; no traits, no
            // overlay toggle. The stamp is a data attribute because that is the one
            // thing that survives the HTML round trip; it ships to the live page,
            // where it is inert.
            isComponent: (el) =>
                (el.nodeType === 1 && el.getAttribute && el.getAttribute(BLOCK_ATTR) === def.id)
                    ? { type: typeId, elevareParsed: true }
                    : false,
            model: {
                defaults: {
                    tagName: def.tag || 'section',
                    name: def.name || localizedLabel,
                    droppable: true,
                    style: def.style || {},
                    attributes: { [BLOCK_ATTR]: def.id, ...(def.attrs || {}) },
                    traits: def.traits || [],
                    ...(def.script ? { script: def.script } : {}),
                    // Same object as the definition's, on purpose: toJSON drops a
                    // prop whose value === its default, so this never reaches the
                    // saved page. See the overlay section below smart().
                    ...(def.overlay ? { elevareOverlay: def.overlay } : {}),
                    ...propDefaults
                },
                init() {
                    const m = this;
                    // Set by isComponent, read once, not kept: it would otherwise be
                    // saved with the page and mark the block as freshly parsed forever.
                    const parsed = !!m.get('elevareParsed');
                    m.unset('elevareParsed', { silent: true });
                    // Blocks saved before the stamp existed get it on their next
                    // save, so a template edited once from now on copies correctly.
                    if (!m.getAttributes()[BLOCK_ATTR]) m.addAttributes({ [BLOCK_ATTR]: def.id });
                    (def.repeats || []).forEach((rp) => {
                        m.on('change:' + rp.trait, () => {
                            syncRepeat(m, rp);
                            if (rp.after) rp.after(m);
                        });
                    });
                    Object.entries(def.on || {}).forEach(([prop, fn]) => {
                        m.on('change:' + prop, () => fn(m, m.get(prop)));
                    });
                    if (!m.components().length && def.template) {
                        const html = def.template(m);
                        if (html) m.components(html);
                    }
                    // Not for a parsed block: its trait props are this type's DEFAULTS,
                    // not what the author chose (those never made it into the HTML),
                    // and onInit would write the defaults back over the markup — the
                    // WhatsApp href, the accordion's exclusivity, the card grid's
                    // column count. The markup and CSS already hold the real values;
                    // a trait changed from here on applies from that moment.
                    if (def.onInit && !parsed) def.onInit(m);
                    // The other way round: reads what the markup holds into the
                    // trait's prop, so the panel shows the page's real value.
                    if (def.onLoad) def.onLoad(m);
                    if (def.overlay) attachOverlayToolbar(m);
                }
            },
            ...(def.view ? { view: def.view } : {})
        });

        bm.add(def.id, {
            // fs-2 (was fs-3): next to the built-in Basic/Forms blocks — GrapesJS's
            // own, not ours — these read noticeably smaller at the old size.
            label: `<i class="bi ${def.icon} fs-2"></i><br>${localizedLabel}`,
            category: blockCategory(def.id),
            content: { type: typeId }
        });
    }

    function syncRepeat(model, rp) {
        let n = parseInt(model.get(rp.trait), 10);
        if (isNaN(n)) return;
        n = clamp(n, rp.min != null ? rp.min : 1, rp.max != null ? rp.max : 12);
        const cont = model.find(rp.container)[0];
        if (!cont) return;
        while (cont.components().length > n) {
            cont.components().at(cont.components().length - 1).remove();
        }
        while (cont.components().length < n) {
            cont.append(rp.item(model, cont.components().length));
        }
    }

    // --------------------------------------------------
    // HIDDEN-UNTIL-TRIGGERED PANELS — Mega Menu, Search Box, Pop-up, Side Panel
    // --------------------------------------------------
    // A dropdown panel, a results list, a modal, a slide-in: none of these is on
    // screen until a visitor does something, and the canvas used to compensate by
    // forcing every one of them OPEN, permanently. The pop-up sat in the page flow
    // pushing everything below it down; the mega menu's panel lay across whatever
    // block came next. The canvas was the one place the page never looked like
    // itself.
    //
    // Now they start CLOSED in the canvas too, exactly as on the live site, and
    // open on demand from a ▼/▲ button the block's floating toolbar gains (see
    // attachOverlayToolbar), or all at once from a button in the editor's top bar
    // (OVERLAY_ALL_COMMAND). Selecting anything inside a closed panel — from the
    // Layers panel, say — opens it as well, so a hidden element is never one you
    // cannot reach, and a freshly dropped block opens once so what was just added
    // is actually visible.
    //
    // Not eyes: the top bar already has one (Önizle) and the Layers panel another
    // (visibility), and a third eye meaning a third thing would be noise. These
    // are the Material "menu-down/up" triangle for one panel and "unfold more/less"
    // for all of them — expand/collapse, which is what they do. Both glyphs sit in
    // the label at once and CSS (app.css, .elevare-tlb-overlay / .elevare-pn-overlays)
    // shows the one matching the state, so a state change never re-renders a button.
    //
    // The state is a Set of open blocks here and ONE <style> element in the canvas
    // document, rebuilt from that Set (syncOverlayCss): a closed panel is hidden by
    // an #id rule in that sheet, and a panel that opens somewhere other than where
    // the visitor sees it (the Side Panel opens in place, the Pop-up in the flow)
    // gets its canvas-only placement from the same sheet. Nothing touches the
    // model and nothing is inline on the element — both were tried and both
    // fail: an inline style is rewritten from the model on every trait change
    // (measured — changing the Side Panel's edge wiped the panel's inline display),
    // and a model prop or class would be saved with the page. A sheet the model
    // knows nothing about cannot be exported and cannot be overwritten.
    //
    // `overlay` on a block definition names the panels:
    //   { panels: [{ sel, display, canvasCss }] } — children of the block;
    //     `display` forces an open panel's display where its own rule is silent
    //     (the Pop-up needs flex; the Side Panel's backdrop stays none, there is
    //     nothing to edit on a dimmer), `canvasCss` the canvas-only placement
    //   { self: true, display, canvasCss }        — the block itself is the panel
    const OVERLAY_COMMAND = 'elevare:overlay-toggle';
    const OVERLAY_ALL_COMMAND = 'elevare:overlays-toggle-all';
    const OVERLAY_STYLE_ID = 'elevare-overlay-canvas-css';
    // Material Design Icons (24-unit grid, the same set grapesjs-preset-webpage
    // draws the rest of the top bar with): menu-down, menu-up, unfold-more-
    // horizontal, unfold-less-horizontal.
    const OVERLAY_GLYPHS = {
        openOne: 'M7,10L12,15L17,10H7Z',
        closeOne: 'M7,15L12,10L17,15H7Z',
        openAll: 'M12,18.17L8.83,15L7.42,16.41L12,21L16.59,16.41L15.17,15M12,5.83L15.17,9L16.58,7.59L12,3L7.41,7.59L8.83,9L12,5.83Z',
        closeAll: 'M16.59,5.41L15.17,4L12,7.17L8.83,4L7.41,5.41L12,10M7.41,18.59L8.83,20L12,16.83L15.17,20L16.58,18.59L12,14L7.41,18.59Z'
    };
    const overlayGlyph = (name, cls) =>
        `<svg class="${cls}" viewBox="0 0 24 24" aria-hidden="true"><path fill="currentColor" d="${OVERLAY_GLYPHS[name]}"/></svg>`;
    const overlayRoots = new Set();
    const openOverlays = new Set();

    // Direct children only: a Search Box dropped inside a Mega Menu panel must not
    // have its results list counted as one of the menu's own panels.
    const overlayPanels = (m) => {
        const ov = m.get('elevareOverlay');
        if (!ov) return [];
        if (ov.self) return [{ comp: m, display: ov.display || '', canvasCss: ov.canvasCss || '' }];
        return ov.panels.flatMap((p) =>
            m.find(p.sel).filter((c) => c.parent() === m)
                .map((c) => ({ comp: c, display: p.display || '', canvasCss: p.canvasCss || '' })));
    };

    const syncOverlayCss = () => {
        const doc = editor.Canvas.getDocument();
        if (!doc || !doc.head) return;
        let style = doc.getElementById(OVERLAY_STYLE_ID);
        if (!style) {
            style = doc.createElement('style');
            style.id = OVERLAY_STYLE_ID;
            doc.head.appendChild(style);
        }
        let css = '';
        overlayRoots.forEach((m) => {
            // Gone from the page (deleted, or an undo) — forget it.
            if (!m.collection && m !== editor.getWrapper()) { overlayRoots.delete(m); openOverlays.delete(m); return; }
            const open = openOverlays.has(m);
            overlayPanels(m).forEach(({ comp, display, canvasCss }) => {
                const id = comp.getId();
                if (!open) css += `#${id}{display:none !important;}`;
                else if (display) css += `#${id}{display:${display} !important;}`;
                if (canvasCss) css += `#${id}{${canvasCss}}`;
            });
        });
        if (style.textContent !== css) style.textContent = css;
    };

    const isOverlayOpen = (m) => openOverlays.has(m);
    const liveOverlayRoots = () => [...overlayRoots].filter((m) => m.collection);
    const allOverlaysOpen = () => {
        const roots = liveOverlayRoots();
        return roots.length > 0 && roots.every(isOverlayOpen);
    };

    // The two buttons' glyphs follow the state through a class, never a re-render.
    const syncOverlayButtons = () => {
        const tb = editor.Canvas.getToolbarEl && editor.Canvas.getToolbarEl();
        const sel = editor.getSelected();
        if (tb) tb.classList.toggle('elevare-overlay-open', !!sel && isOverlayOpen(sel));
        const all = document.querySelector('.gjs-pn-btn.elevare-pn-overlays');
        if (all) all.classList.toggle('elevare-all-open', allOverlaysOpen());
    };

    const setOverlayOpen = (m, open) => {
        if (open) openOverlays.add(m); else openOverlays.delete(m);
        syncOverlayCss();
        syncOverlayButtons();
    };

    editor.Commands.add(OVERLAY_COMMAND, {
        run(ed) {
            const m = ed.getSelected();
            if (m && m.get('elevareOverlay')) setOverlayOpen(m, !isOverlayOpen(m));
        }
    });

    // Open every panel on the page when any is closed; close them all otherwise.
    // Deliberately has no `stop`: GrapesJS treats a panel button whose command
    // cannot stop as a plain click, not a latch, so each press runs this again.
    editor.Commands.add(OVERLAY_ALL_COMMAND, {
        run() {
            const open = !allOverlaysOpen();
            liveOverlayRoots().forEach((m) => { if (open) openOverlays.add(m); else openOverlays.delete(m); });
            syncOverlayCss();
            // After the click settles: GrapesJS rewrites the button's className
            // when it resets `active`, which would drop the state class set now.
            setTimeout(syncOverlayButtons, 0);
        }
    });

    // Into the top bar, right after the component-outline toggle it belongs with.
    // The panel is grapesjs-preset-webpage's; guarded because a test harness may
    // load this file without it.
    const optionsPanel = editor.Panels.getPanel('options');
    if (optionsPanel) {
        optionsPanel.get('buttons').add({
            id: 'elevare-overlays',
            className: 'elevare-pn-overlays',
            command: OVERLAY_ALL_COMMAND,
            attributes: { title: LOCALE === 'tr' ? 'Gizli panellerin tümünü aç / kapat' : 'Open / close every hidden panel' },
            label: overlayGlyph('openAll', 'elevare-ico-open') + overlayGlyph('closeAll', 'elevare-ico-close')
        }, { at: 1 });
    }

    // The toolbar GrapesJS builds (parent / move / clone / delete) is kept; the
    // toggle goes in front of it. `toolbar` is dropped from the saved JSON, so this
    // has to run on every model init — restored pages included — which smart() does.
    const attachOverlayToolbar = (m) => {
        const tb = (m.get('toolbar') || []).slice();
        if (tb.some((t) => t.command === OVERLAY_COMMAND)) return;
        tb.unshift({
            attributes: { class: 'elevare-tlb-overlay', title: LOCALE === 'tr' ? 'Gizli paneli aç / kapat' : 'Open / close the hidden panel' },
            label: overlayGlyph('openOne', 'elevare-ico-open') + overlayGlyph('closeOne', 'elevare-ico-close'),
            command: OVERLAY_COMMAND
        });
        m.set('toolbar', tb);
    };

    // Rendered into the canvas — restored pages, drops, undo, pastes alike — is
    // the moment a block's panels need their closed rule.
    editor.on('component:mount', (m) => {
        if (!m.get('elevareOverlay')) return;
        overlayRoots.add(m);
        syncOverlayCss();
    });
    editor.on('component:remove', (m) => {
        if (!overlayRoots.has(m)) return;
        overlayRoots.delete(m);
        openOverlays.delete(m);
        syncOverlayCss();
        syncOverlayButtons();
    });
    // The floating toolbar is rebuilt on every selection change; its class is not.
    editor.on('component:toggled', () => syncOverlayButtons());
    // The frame's document is new after a load; the sheet has to be put back.
    editor.on('canvas:frame:load', () => setTimeout(syncOverlayCss, 0));

    // --------------------------------------------------
    // <details> — always open in the canvas (SSS, Açılır Kapanır Bölümler)
    // --------------------------------------------------
    // A closed <details> hides everything but its <summary>, and in the canvas it
    // stays closed: a click there selects the component instead of toggling it, so
    // the answer under a question could be reached only through the Layers panel
    // and never edited in place. The opposite of the overlays above — these sit in
    // the page flow and open ones cost nothing, so they are simply held open.
    //
    // Held open on the DOM, never the model: a model `open` attribute would be
    // exported and every item would ship expanded. GrapesJS rewrites an element's
    // attributes from the model on every trait change (the accordion's "Tek
    // Seferde Bir Bölüm" does exactly that to every item), which strips the DOM
    // `open` again — the observer puts it back. Export reads the model, so none
    // of this reaches the page; "Başlangıçta açık" (the trait below) is how an
    // author ships one open on purpose.
    //
    // `name` is stripped from the DOM the same way, and has to be: it is what
    // makes a group of <details> exclusive, and the browser enforces it — open one,
    // the others close. Holding all of them open against that is an endless
    // exchange of close-and-reopen; the first version of this hung the editor the
    // moment the accordion was switched to "Tek Seferde Bir Bölüm". Exclusivity is
    // a live-site behaviour anyway; in the canvas every section is for editing.
    const holdDetailsOpen = () => {
        const doc = editor.Canvas.getDocument();
        const win = editor.Canvas.getWindow();
        if (!doc || !doc.body || !win || doc.body.elevareDetailsHeld) return;
        doc.body.elevareDetailsHeld = true;
        // Only ever a <details>: the observer also reports `name` on form fields.
        const hold = (d) => {
            if (!d.matches || !d.matches('details')) return;
            if (d.hasAttribute('name')) d.removeAttribute('name');
            if (!d.open) d.open = true;
        };
        const holdAll = (root) => {
            hold(root);
            if (root.querySelectorAll) root.querySelectorAll('details').forEach(hold);
        };
        holdAll(doc.body);
        new win.MutationObserver((records) => {
            records.forEach((r) => {
                if (r.type === 'attributes') hold(r.target);
                else r.addedNodes.forEach(holdAll);
            });
        }).observe(doc.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['open', 'name'] });
    };
    editor.on('load', holdDetailsOpen);
    editor.on('canvas:frame:load', () => setTimeout(holdDetailsOpen, 0));

    // Walks up from the selection so a Search Box inside a Mega Menu opens both.
    editor.on('component:selected', (model) => {
        const chain = [];
        for (let n = model; n; n = n.parent && n.parent()) chain.push(n);
        chain.forEach((node) => {
            const ov = node.get('elevareOverlay');
            if (!ov) return;
            const inside = ov.self || overlayPanels(node).some(({ comp }) => chain.includes(comp));
            if (inside && !isOverlayOpen(node)) setOverlayOpen(node, true);
        });
    });

    editor.on('block:drag:stop', (dropped) => {
        const list = Array.isArray(dropped) ? dropped : [dropped];
        list.forEach((m) => {
            if (!m || !m.get || !m.get('elevareOverlay')) return;
            setOverlayOpen(m, true);
            editor.select(m);
        });
    });

    // ==================================================
    // CANVAS/EXPORT SCRIPTS
    // ==================================================
    const sliderScript = function () {
        var root = this;
        var slidesEl = root.querySelector('.el-slides');
        if (!slidesEl) return;
        var idx = 0, timer = null;
        function slides() { return slidesEl.children; }
        function go(i) {
            var n = slides().length; if (!n) return;
            idx = ((i % n) + n) % n;
            slidesEl.style.transform = 'translateX(-' + (idx * 100) + '%)';
            var dots = root.querySelectorAll('.el-slider-dot');
            for (var k = 0; k < dots.length; k++) dots[k].style.opacity = (k === idx ? '1' : '.45');
        }
        function buildDots() {
            var c = root.querySelector('.el-slider-dots'); if (!c) return;
            c.innerHTML = '';
            for (var k = 0; k < slides().length; k++) {
                (function (kk) {
                    var b = document.createElement('button');
                    b.type = 'button';
                    b.className = 'el-slider-dot';
                    b.setAttribute('aria-label', 'Slayt ' + (kk + 1));
                    // 24px is the smallest target WCAG 2.5.8 / Lighthouse accept; the
                    // visible dot is the 10px disc painted in the middle of it.
                    b.style.cssText = 'width:24px;height:24px;border-radius:50%;border:0;padding:0;margin:0 2px;cursor:pointer;' +
                        'background:radial-gradient(circle, ${SURFACE} 5px, transparent 5.5px);opacity:.45;';
                    b.addEventListener('click', function () { go(kk); restart(); });
                    c.appendChild(b);
                })(k);
            }
            go(Math.min(idx, Math.max(0, slides().length - 1)));
        }
        function restart() {
            if (timer) { clearInterval(timer); timer = null; }
            var iv = parseInt(root.getAttribute('data-interval') || '0', 10);
            if (root.getAttribute('data-autoplay') === 'yes' && iv > 300) {
                timer = setInterval(function () { go(idx + 1); }, iv);
            }
        }
        var prev = root.querySelector('.el-slider-prev');
        var next = root.querySelector('.el-slider-next');
        if (prev) prev.addEventListener('click', function () { go(idx - 1); restart(); });
        if (next) next.addEventListener('click', function () { go(idx + 1); restart(); });
        if (window.MutationObserver) {
            new MutationObserver(buildDots).observe(slidesEl, { childList: true });
            new MutationObserver(restart).observe(root, { attributes: true, attributeFilter: ['data-autoplay', 'data-interval'] });
        }
        buildDots();
        restart();
    };

    const beforeAfterScript = function () {
        var root = this;
        var before = root.querySelector('.ba-before');
        var handle = root.querySelector('.el-ba-handle');
        var area = root.querySelector('.ba-slider');
        if (!before || !handle || !area) return;
        function setPos(clientX) {
            var r = area.getBoundingClientRect();
            var x = Math.max(0, Math.min(clientX - r.left, r.width));
            var p = r.width ? (x / r.width) * 100 : 50;
            before.style.clipPath = 'inset(0 ' + (100 - p) + '% 0 0)';
            handle.style.left = p + '%';
        }
        var drag = false;
        handle.addEventListener('pointerdown', function (e) {
            drag = true;
            try { handle.setPointerCapture(e.pointerId); } catch (err) { }
            e.preventDefault();
        });
        handle.addEventListener('pointermove', function (e) { if (drag) setPos(e.clientX); });
        handle.addEventListener('pointerup', function () { drag = false; });
        handle.addEventListener('pointercancel', function () { drag = false; });
        area.addEventListener('click', function (e) { setPos(e.clientX); });
    };

    const navbarScript = function () {
        var root = this;
        var btn = root.querySelector('.el-nav-toggle');
        var menu = root.querySelector('.el-nav-links');
        if (!btn || !menu) return;
        btn.addEventListener('click', function () {
            var open = menu.classList.toggle('open');
            btn.setAttribute('aria-expanded', open ? 'true' : 'false');
        });
    };

    const announceScript = function () {
        var root = this;
        var btn = root.querySelector('.el-announce-close');
        if (!btn) return;
        btn.addEventListener('click', function () {
            try { var fe = window.frameElement; if (fe && (fe.className || '').indexOf('gjs-') > -1) return; } catch (e) { }
            root.style.display = 'none';
        });
    };

    const videoFacadeScript = function () {
        var root = this;
        var btn = root.querySelector('.el-vf-play');
        var box = root.querySelector('.el-vf-box');
        if (!btn || !box) return;
        btn.addEventListener('click', function () {
            try { var fe = window.frameElement; if (fe && (fe.className || '').indexOf('gjs-') > -1) return; } catch (e) { }
            var src = root.getAttribute('data-video');
            if (!src) return;
            var f = document.createElement('iframe');
            f.src = src + (src.indexOf('?') > -1 ? '&' : '?') + 'autoplay=1';
            f.title = root.getAttribute('data-video-title') || 'Video';
            f.setAttribute('allow', 'autoplay; encrypted-media; picture-in-picture');
            f.setAttribute('allowfullscreen', '');
            f.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;border:0;z-index:3;';
            box.appendChild(f);
        });
    };

    // `this` is the button's own DOM node (see smart()'s script wiring) — the
    // same read-flip-write on documentElement's data-theme + localStorage the
    // header's built-in toggle already uses, so a page can carry either (or
    // both) and they stay in sync automatically; there is only ever one
    // mechanism, just more than one place a visitor can trigger it from.
    const themeSwitchScript = function () {
        var root = this;
        root.addEventListener('click', function () {
            var el = document.documentElement;
            var next = el.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
            el.setAttribute('data-theme', next);
            try { localStorage.setItem('elevare-theme', next); } catch (e) { /* private mode, etc. */ }
        });
    };

    // ==================================================
    // 1. HERO SECTION (SEO Friendly Links)
    // ==================================================
    const heroBtn = (i) => i === 0
        ? `<a href="#" style="padding:14px 32px;background-color:${BLUE};color:${ON_PRIMARY};text-decoration:none;border-radius:6px;font-weight:600;font-size:1.1rem;">Projeye Başlayın</a>`
        : `<a href="#" style="padding:14px 32px;background:transparent;color:${BLUE};text-decoration:none;border-radius:6px;font-weight:600;font-size:1.1rem;border:2px solid ${BLUE};">Detayları İnceleyin</a>`;

    smart({
        id: 'elevare-hero', label: 'Hero Section', icon: 'bi-layout-text-window-reverse',
        style: { padding: '120px 20px', 'background-color': BG, color: DARK },
        traits: [
            { type: 'select', name: 'alignment', label: 'Alignment', options: [{ id: 'left', name: 'Left' }, { id: 'center', name: 'Center' }], value: 'center', changeProp: 1 },
            { type: 'number', name: 'btnCount', label: 'Button Count', min: 0, max: 2, value: 2, changeProp: 1 }
        ],
        repeats: [{ trait: 'btnCount', container: '.el-hero-btns', min: 0, max: 2, item: (m, i) => heroBtn(i) }],
        on: {
            alignment: (m, v) => {
                const inner = m.find('.el-hero-inner')[0];
                if (inner) inner.addStyle({ 'text-align': v });
                const btns = m.find('.el-hero-btns')[0];
                if (btns) btns.addStyle({ 'justify-content': v === 'center' ? 'center' : 'flex-start' });
            }
        },
        template: (m) => `
            <div class="el-hero-inner" style="max-width:800px;margin:0 auto;text-align:${m.get('alignment')};">
                <h1 style="font-size:3.5rem;font-weight:800;margin-bottom:24px;line-height:1.2;">Dijital Varlığınızı Bizimle Büyütün</h1>
                <p style="font-size:1.25rem;color:${TEXT2};margin-bottom:40px;line-height:1.6;">Web sitenizi zahmetsizce yönetin ve ölçeklendirin. Yüksek performans ve esneklik için tasarlanmış yenilikçi mimari.</p>
                <div class="el-hero-btns" style="display:flex;gap:16px;justify-content:${m.get('alignment') === 'center' ? 'center' : 'flex-start'};">${rep(m.get('btnCount'), heroBtn)}</div>
            </div>`
    });

    // ==================================================
    // 2. FEATURES GRID
    // ==================================================
    const FEAT_ICONS = ['lightning-charge', 'shield-check', 'phone', 'rocket', 'graph-up', 'globe'];
    const FEAT_COLORS = ['#2563eb', '#16a34a', '#dc2626', '#9333ea', '#ea580c', '#0891b2'];
    const FEAT_BGS = ['#eff6ff', '#f0fdf4', '#fef2f2', '#faf5ff', '#fff7ed', '#ecfeff'];
    const featCard = (i) => `
        <li style="flex:1;min-width:300px;padding:20px;">
            <div style="width:64px;height:64px;background-color:${FEAT_BGS[i % 6]};border-radius:50%;display:flex;align-items:center;justify-content:center;margin:0 auto 24px;">${icon(FEAT_ICONS[i % 6], `font-size:2rem;color:${FEAT_COLORS[i % 6]};`)}</div>
            <h3 style="font-size:1.5rem;font-weight:600;margin-bottom:16px;color:${DARK};">Öne Çıkan Özellik ${i + 1}</h3>
            <p style="color:${MUTED};line-height:1.6;">Ürününüzün veya hizmetinizin temel faydasını burada vurgulayın. Ziyaretçileriniz için akılda kalıcı bir açıklama girin.</p>
        </li>`;

    smart({
        id: 'elevare-features', label: 'Features Grid', icon: 'bi-grid-1x2',
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [{ type: 'number', name: 'cardCount', label: 'Feature Count', min: 1, max: 6, value: 3, changeProp: 1 }],
        repeats: [{ trait: 'cardCount', container: '.el-feat-grid', min: 1, max: 6, item: (m, i) => featCard(i) }],
        template: (m) => `
            <div style="max-width:1200px;margin:0 auto;">
                <div style="text-align:center;margin-bottom:60px;">
                    <h2 style="font-size:2.5rem;font-weight:700;color:${DARK};margin-bottom:16px;">Neden Bizi Seçmelisiniz?</h2>
                    <p style="font-size:1.1rem;color:${MUTED};max-width:600px;margin:0 auto;">İşletmenizi çevrimiçi ortamda yönetmek için ihtiyacınız olan her şey tek bir platformda.</p>
                </div>
                <ul class="el-feat-grid" style="display:flex;flex-wrap:wrap;gap:40px;text-align:center;list-style:none;margin:0;padding:0;">${rep(m.get('cardCount'), featCard)}</ul>
            </div>`
    });

    // ==================================================
    // 3. PRICING TABLE
    // ==================================================
    const PRICE_TITLES = ['Başlangıç', 'Profesyonel', 'Kurumsal', 'Özel'];
    const PRICE_VALS = ['29', '99', '149', '299'];
    const priceCard = (i, hi) => `
        <div class="el-price-card" data-card-index="${i}" style="flex:1;min-width:280px;max-width:350px;padding:${hi ? '50px 30px' : '40px 30px'};border:${hi ? '2px solid ' + BLUE : '1px solid ' + BORDER};border-radius:12px;background:${SURFACE};box-shadow:${hi ? '0 20px 25px -5px rgba(0,0,0,0.1)' : 'none'};position:relative;">
            <div class="popular-badge" style="display:${hi ? 'block' : 'none'};position:absolute;top:-14px;left:50%;transform:translateX(-50%);background:${BLUE};color:${ON_PRIMARY};padding:4px 16px;border-radius:20px;font-size:0.875rem;font-weight:600;z-index:2;">En Çok Tercih Edilen</div>
            <h3 style="font-size:1.25rem;font-weight:600;color:${hi ? BLUE : TEXT2};margin-bottom:16px;">${PRICE_TITLES[i % 4]}</h3>
            <div style="margin-bottom:24px;"><span style="font-size:3rem;font-weight:700;color:${DARK};line-height:1.2;">₺${PRICE_VALS[i % 4]}</span><span style="color:${MUTED};font-size:1rem;">/ay</span></div>
            <p style="color:${MUTED};margin-bottom:24px;">Bu planın kısa açıklaması ve hedef kitlesi.</p>
            <ul style="list-style:none;padding:0;margin-bottom:32px;color:${TEXT2};line-height:2.5;">
                <li>${icon('check2', `color:${BLUE};margin-right:12px;font-weight:bold;`)}<span>Kritik Özellik 1</span></li>
                <li>${icon('check2', `color:${BLUE};margin-right:12px;font-weight:bold;`)}<span>Kritik Özellik 2</span></li>
            </ul>
            <a href="#" class="el-price-btn" style="display:block;text-align:center;padding:12px;background:${hi ? BLUE : SURFACE2};color:${hi ? ON_PRIMARY : DARK};text-decoration:none;border-radius:6px;font-weight:600;">Planı Seç: ${PRICE_TITLES[i % 4]}</a>
        </div>`;

    function applyPricingHighlight(m) {
        const hi = parseInt(m.get('highlightIndex'), 10) || 0;
        const n = parseInt(m.get('tierCount'), 10) || 0;
        m.find('.el-price-card').forEach((card) => {
            const idx = parseInt(card.getAttributes()['data-card-index'], 10);
            const on = (idx + 1) === hi && hi > 0 && hi <= n;
            const badge = card.find('.popular-badge')[0];
            if (badge) badge.addStyle({ display: on ? 'block' : 'none' });
            card.addStyle(on
                ? { border: '2px solid ' + BLUE, 'box-shadow': '0 20px 25px -5px rgba(0,0,0,0.1)', padding: '50px 30px' }
                : { border: '1px solid ' + BORDER, 'box-shadow': 'none', padding: '40px 30px' });
            const btn = card.find('.el-price-btn')[0];
            if (btn) btn.addStyle(on ? { background: BLUE, color: ON_PRIMARY } : { background: SURFACE2, color: DARK });
        });
    }

    smart({
        id: 'elevare-pricing', label: 'Pricing Plans', icon: 'bi-tags',
        style: { padding: '80px 20px', 'background-color': BG },
        traits: [
            { type: 'number', name: 'tierCount', label: 'Plan Count', min: 1, max: 4, value: 2, changeProp: 1 },
            { type: 'number', name: 'highlightIndex', label: 'Highlight Plan (1-4, 0=None)', min: 0, max: 4, value: 2, changeProp: 1 }
        ],
        repeats: [{ trait: 'tierCount', container: '.pricing-cards', min: 1, max: 4, item: (m, i) => priceCard(i, false), after: applyPricingHighlight }],
        on: { highlightIndex: (m) => applyPricingHighlight(m) },
        template: (m) => {
            const n = parseInt(m.get('tierCount'), 10) || 2;
            const hi = parseInt(m.get('highlightIndex'), 10) || 0;
            return `
            <div style="max-width:1200px;margin:0 auto;">
                <div style="text-align:center;margin-bottom:60px;">
                    <h2 style="font-size:2.5rem;font-weight:700;color:${DARK};margin-bottom:16px;">Şeffaf ve Esnek Fiyatlandırma</h2>
                    <p style="font-size:1.1rem;color:${MUTED};">Gizli ücret yok. İstediğiniz zaman iptal edin.</p>
                </div>
                <div class="pricing-cards" style="display:flex;flex-wrap:wrap;gap:30px;justify-content:center;align-items:flex-start;">${rep(n, (i) => priceCard(i, (i + 1) === hi))}</div>
            </div>`;
        }
    });

    // ==================================================
    // 4. CTA BANNER
    // ==================================================
    smart({
        id: 'elevare-cta', label: 'CTA Banner', icon: 'bi-megaphone',
        style: { padding: '60px 20px', 'background-color': NAVY },
        traits: [{
            type: 'select', name: 'theme', label: 'Theme', options: [
                { id: '#1e3a8a', name: 'Brand Blue' },
                { id: '#111827', name: 'Dark Slate' },
                { id: '#f3f4f6', name: 'Light Gray' }
            ], value: '#1e3a8a', changeProp: 1
        }],
        on: {
            theme: (m, theme) => {
                m.addStyle({ 'background-color': theme });
                const isLight = theme === '#f3f4f6';
                const title = m.find('.el-cta-title')[0];
                const desc = m.find('.el-cta-desc')[0];
                const btn = m.find('.el-cta-btn')[0];
                if (title) title.addStyle({ color: isLight ? '#111827' : '#ffffff' });
                if (desc) desc.addStyle({ color: isLight ? '#4b5563' : '#bfdbfe' });
                if (btn) btn.addStyle({ 'background-color': isLight ? BLUE : '#ffffff', color: isLight ? ON_PRIMARY : NAVY });
            }
        },
        template: () => `
            <div style="max-width:800px;margin:0 auto;text-align:center;">
                <h2 class="el-cta-title" style="font-size:2.5rem;font-weight:700;margin-bottom:20px;color:#ffffff;">İşletmenizi Zirveye Taşımaya Hazır mısınız?</h2>
                <p class="el-cta-desc" style="font-size:1.1rem;color:#bfdbfe;margin-bottom:32px;">Dijital varlıklarını dönüştüren binlerce mutlu müşterimiz arasına katılın.</p>
                <a href="#" class="el-cta-btn" style="display:inline-block;padding:14px 32px;background-color:#ffffff;color:${NAVY};text-decoration:none;border-radius:6px;font-weight:600;font-size:1.1rem;">Ücretsiz Denemeye Başlayın</a>
            </div>`
    });

    // ==================================================
    // 5. TESTIMONIALS (CWV: loading lazy/eager)
    // ==================================================
    const testiCard = (i, m) => `
        <li style="flex:1;min-width:300px;">
        <figure style="margin:0;padding:32px;background:${BG};border-radius:12px;border:1px solid ${BORDER};">
            <div style="color:#fbbf24;margin-bottom:16px;font-size:1.25rem;" aria-label="5 üzerinden 5 yıldız">
                ${rep(5, () => icon('star-fill'))}
            </div>
            <blockquote style="margin:0 0 24px;color:${TEXT2};font-size:1.1rem;font-style:italic;line-height:1.6;">Şimdiye kadar kullandığımız en iyi platform. Öğrenme süreci sıfır ve performansı gerçekten olağanüstü.</blockquote>
            <figcaption style="display:flex;align-items:center;gap:16px;">
                <img src="${ph(96, 96, 'Fotoğraf')}" alt="Müşteri ${i + 1} Profil Fotoğrafı" width="48" height="48" loading="${m.get('imageLoading') || 'lazy'}" style="width:48px;height:48px;border-radius:50%;object-fit:cover;">
                <div>
                    <h4 style="margin:0;color:${DARK};font-size:1rem;font-weight:600;">Müşteri ${i + 1}</h4>
                    <span style="color:${MUTED};font-size:0.875rem;">Şirket / Ünvan</span>
                </div>
            </figcaption>
        </figure>
        </li>`;

    smart({
        id: 'elevare-testimonials', label: 'Testimonials', icon: 'bi-chat-quote',
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [
            { type: 'number', name: 'cardCount', label: 'Card Count', min: 1, max: 6, value: 2, changeProp: 1 },
            { type: 'select', name: 'imageLoading', label: 'Görsel Yükleme (SEO)', options: [{ id: 'lazy', name: 'Lazy (Sayfa Altı)' }, { id: 'eager', name: 'Eager (Hero)' }], value: 'lazy', changeProp: 1 }
        ],
        repeats: [{ trait: 'cardCount', container: '.el-testi-grid', min: 1, max: 6, item: (m, i) => testiCard(i, m) }],
        on: { imageLoading: (m, v) => m.find('img').forEach(img => img.addAttributes({ loading: v })) },
        template: (m) => `
            <div style="max-width:1200px;margin:0 auto;">
                <div style="text-align:center;margin-bottom:60px;">
                    <h2 style="font-size:2.5rem;font-weight:700;color:${DARK};margin-bottom:16px;">Müşterilerimiz Ne Diyor?</h2>
                </div>
                <ul class="el-testi-grid" style="display:flex;flex-wrap:wrap;gap:30px;list-style:none;margin:0;padding:0;">${rep(m.get('cardCount'), (i) => testiCard(i, m))}</ul>
            </div>`
    });

    // ==================================================
    // 6. IMAGE SLIDER (SEO Friendly <img> tags)
    // ==================================================
    // The caption used to be a plain `position:absolute;inset:0` flex box, which
    // meant it covered the WHOLE slide — so every click and double-click on a slide
    // landed on the caption, never on the picture behind it. The image could not be
    // swapped from the canvas at all: double-clicking opened the rich-text editor on
    // the caption instead of the Select Image dialog, and picking the <img> out of
    // the Layer Manager got you a Settings panel with nothing in it that changed the
    // picture. (That second half is fixed in grapes-editor.js, which now gives every
    // image a "Kütüphaneden Seç" button and a URL field.)
    //
    // pointer-events:none on the box, auto on the text itself: the caption still
    // centres over the slide and is still editable by clicking the words, while
    // every other part of the slide belongs to the image again. Verified against the
    // canvas' own hit-testing — elementFromPoint at a slide corner returns the img,
    // and over the caption returns the caption.
    // The caption layer covers the whole slide, so it passes pointer events through
    // — a click on empty caption area reaches the image behind it, which is what
    // lets the image be selected in the canvas. Its CHILDREN get them back by the
    // rule below the block rather than inline on the one <span> here: a link an
    // author added next to that span inherited `none` and could not be clicked on
    // the live site at all (measured — the click landed on the <img>).
    const slideDiv = (i) => {
        const h = 500 + i;
        return `<div class="el-slide" style="width:100%;flex-shrink:0;position:relative;height:500px;background:#000;">
            <img src="${ph(1200, h, 'Slayt görseli')}" alt="Slayt ${i + 1} Görseli" style="width:100%;height:100%;object-fit:cover;opacity:0.6;">
            <div class="el-slide-caption" style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;pointer-events:none;">
                <span style="color:#fff;font-size:2rem;font-weight:700;text-align:center;padding:0 24px;">Slayt ${i + 1} Başlığı</span>
            </div>
        </div>`;
    };

    smart({
        id: 'elevare-slider', label: 'Image Slider', icon: 'bi-images',
        style: { position: 'relative', overflow: 'hidden', padding: '0', 'background-color': '#111827' },
        attrs: { 'data-autoplay': 'no', 'data-interval': '4000' },
        traits: [
            { type: 'number', name: 'slideCount', label: 'Slide Count', min: 1, max: 6, value: 3, changeProp: 1 },
            { type: 'select', name: 'data-autoplay', label: 'Autoplay', options: [{ id: 'no', name: 'Off' }, { id: 'yes', name: 'On' }] },
            { type: 'number', name: 'data-interval', label: 'Interval (ms)' }
        ],
        repeats: [{ trait: 'slideCount', container: '.el-slides', min: 1, max: 6, item: (m, i) => slideDiv(i) }],
        script: sliderScript,
        template: (m) => `
            <div class="el-slides" style="display:flex;transition:transform .5s ease;">${rep(m.get('slideCount'), (i) => slideDiv(i))}</div>
            <button data-elevare-btn type="button" class="el-slider-prev" aria-label="Önceki slayt" data-gjs-draggable="false" style="position:absolute;top:50%;transform:translateY(-50%);left:10px;background:rgba(255,255,255,.6);color:#111827;border:0;padding:10px 14px;cursor:pointer;font-size:1.4rem;border-radius:6px;z-index:10;">&#10094;</button>
            <button data-elevare-btn type="button" class="el-slider-next" aria-label="Sonraki slayt" data-gjs-draggable="false" style="position:absolute;top:50%;transform:translateY(-50%);right:10px;background:rgba(255,255,255,.6);color:#111827;border:0;padding:10px 14px;cursor:pointer;font-size:1.4rem;border-radius:6px;z-index:10;">&#10095;</button>
            <div class="el-slider-dots" style="position:absolute;left:0;right:0;bottom:10px;text-align:center;z-index:10;"></div>`
    });

    // Everything placed inside a slide caption is clickable — a link, a button, the
    // title — while the caption itself still lets clicks through to the image. A
    // rule, not inline, so it also covers sliders already on pages once they are
    // saved again; an inherited `none` yields to any rule that sets the property.
    blockCss('.el-slide-caption > * { pointer-events: auto; }');

    // ==================================================
    // 7. BEFORE / AFTER (SEO Friendly <img> tags)
    // ==================================================
    smart({
        id: 'elevare-beforeafter', label: 'Before / After', icon: 'bi-arrows-angle-expand',
        tag: 'div', name: 'Before/After',
        style: { position: 'relative', width: '100%', 'max-width': '700px', margin: '0 auto', overflow: 'hidden' },
        traits: [
            { type: 'text', name: 'beforeUrl', label: 'Before Image URL', value: ph(900, 500, 'Öncesi'), changeProp: 1 },
            { type: 'text', name: 'afterUrl', label: 'After Image URL', value: ph(900, 500, 'Sonrası'), changeProp: 1 }
        ],
        on: {
            beforeUrl: (m, v) => { const t = m.find('.ba-before')[0]; if (t && v) t.addAttributes({ src: v }); },
            afterUrl: (m, v) => { const t = m.find('.ba-after')[0]; if (t && v) t.addAttributes({ src: v }); }
        },
        script: beforeAfterScript,
        template: (m) => `
            <style>.el-ba-handle::after{content:'';position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);width:34px;height:34px;background:${SURFACE};border-radius:50%;box-shadow:0 2px 6px rgba(0,0,0,.35);}</style>
            <div class="ba-slider" style="position:relative;width:100%;height:400px;overflow:hidden;border-radius:8px;background:#000;">
                <img class="ba-after" src="${m.get('afterUrl')}" alt="Uygulama Sonrası" loading="lazy" style="position:absolute;top:0;left:0;width:100%;height:100%;object-fit:cover;z-index:1;">
                <img class="ba-before" src="${m.get('beforeUrl')}" alt="Uygulama Öncesi" loading="lazy" style="position:absolute;top:0;left:0;width:100%;height:100%;object-fit:cover;clip-path:inset(0 50% 0 0);z-index:2;">
                <div class="el-ba-handle" data-gjs-draggable="false" role="slider" aria-label="Karşılaştırma sürgüsü" aria-valuemin="0" aria-valuemax="100" aria-valuenow="50" style="position:absolute;top:0;bottom:0;left:50%;width:4px;background:${SURFACE};cursor:ew-resize;z-index:3;transform:translateX(-50%);touch-action:none;"></div>
            </div>
            <p style="text-align:center;margin-top:8px;color:${MUTED};">Karşılaştırmak için sürgüyü kaydırın</p>`
    });

    // ==================================================
    // 8. FAQ ACCORDION (carries FAQPage structured-data markers)
    // ==================================================
    // There used to be TWO FAQ blocks: this accordion, and a near-identical "FAQ
    // (Schema)" whose only real difference was that it carried the
    // data-elevare-faq-* hints SchemaNodeFactory.BuildFaq reads. That made
    // structured data an opt-in an author had to know about in advance — pick the
    // wrong one of two identically-named blocks and the page silently produced no
    // FAQPage node, with nothing on screen to say so.
    //
    // The markers live on the ordinary block now, so writing an FAQ is all it takes.
    // <details>/<summary> is kept as the visible form because it collapses natively,
    // is keyboard accessible for free, and carries the markers just as well as the
    // plain divs the schema variant used.
    //
    // For collapsible content that is NOT questions and answers, use the Açılır
    // Kapanır Bölümler block instead — same interaction, deliberately no markers, so
    // it cannot tell search engines the page answers questions it never asked.
    const faqItem = (i) => `
        <details data-elevare-faq-item style="margin-bottom:16px;border:1px solid ${BORDER};border-radius:8px;padding:16px;">
            <summary data-elevare-faq-question style="font-weight:600;cursor:pointer;">Sıkça Sorulan Soru ${i + 1}?</summary>
            <p data-elevare-faq-answer style="margin-top:8px;color:${TEXT2};">Sorunun cevabını buraya girin. Kullanıcılarınıza yardımcı olacak net ve doyurucu bilgiler verin.</p>
        </details>`;

    smart({
        id: 'elevare-faq', label: 'FAQ', icon: 'bi-patch-question',
        attrs: { 'data-elevare-faq': '' },
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [{ type: 'number', name: 'itemCount', label: 'Soru Sayısı', min: 1, max: 10, value: 3, changeProp: 1 }],
        repeats: [{ trait: 'itemCount', container: '.el-faq-list', min: 1, max: 10, item: (m, i) => faqItem(i) }],
        template: (m) => `
            <div style="max-width:800px;margin:0 auto;">
                <h2 style="font-size:2.5rem;font-weight:700;color:${DARK};text-align:center;margin-bottom:40px;">Sıkça Sorulan Sorular</h2>
                <div class="el-faq-list">${rep(m.get('itemCount'), faqItem)}</div>
            </div>`
    });

    // ==================================================
    // 8b. GENERIC ACCORDION — collapsible content, deliberately NOT an FAQ
    // ==================================================
    // Added alongside the FAQ merge above, and needed because of it: now that the FAQ
    // block always emits FAQPage markers, using it for "Teslimat Bilgileri" or a list
    // of programme sessions would put questions into the page's structured data that
    // nobody asked. This is the same interaction with none of the meaning.
    // Browsers close a sibling <details> only when they share a name. The name is
    // derived from THIS block's own component id so two accordions dropped on one
    // page stay independent of each other rather than fighting over one group.
    const applyAccordionExclusive = (m) => {
        const grouped = m.get('exclusive') === 'yes';
        const groupName = 'el-acc-' + (typeof m.getId === 'function' ? m.getId() : 'group');
        findModels(m, 'details').forEach((d) => {
            if (grouped) d.addAttributes({ name: groupName });
            else d.removeAttributes('name');
        });
    };

    const accordionItem = (i) => `
        <details style="border:1px solid ${BORDER};border-radius:8px;padding:16px 20px;margin-bottom:12px;background:${SURFACE};">
            <summary style="font-weight:600;cursor:pointer;color:${DARK};">Bölüm Başlığı ${i + 1}</summary>
            <div style="margin-top:10px;color:${TEXT2};line-height:1.7;">
                <p style="margin:0;">Bu bölümün içeriğini buraya yazın. Uzun metinleri katlayarak sayfayı kısa tutabilir, okuyucunun aradığı başlığa doğrudan gitmesini sağlayabilirsiniz.</p>
            </div>
        </details>`;

    smart({
        id: 'elevare-accordion', label: 'Accordion', icon: 'bi-chevron-bar-expand',
        style: { padding: '60px 20px', 'background-color': WHITE },
        traits: [
            { type: 'number', name: 'sectionCount', label: 'Bölüm Sayısı', min: 1, max: 10, value: 3, changeProp: 1 },
            // <details> is independent by default: opening one leaves the others open.
            // A shared name attribute makes the browser close the previous one — the
            // classic accordion. Off by default because "let me read two at once" is
            // the more forgiving behaviour, and older browsers ignore the attribute
            // entirely and simply keep today's behaviour.
            {
                type: 'select', name: 'exclusive', label: 'Tek Seferde Bir Bölüm', value: 'no', changeProp: 1,
                options: [{ id: 'no', name: 'Hayır (çoklu açılabilir)' }, { id: 'yes', name: 'Evet' }]
            }
        ],
        repeats: [{ trait: 'sectionCount', container: '.el-acc-list', min: 1, max: 10, item: (m, i) => accordionItem(i), after: (m) => applyAccordionExclusive(m) }],
        on: { exclusive: (m) => applyAccordionExclusive(m) },
        onInit: (m) => applyAccordionExclusive(m),
        template: (m) => `
            <div style="max-width:800px;margin:0 auto;">
                <div class="el-acc-list">${rep(m.get('sectionCount'), accordionItem)}</div>
            </div>`
    });

    // ==================================================
    // 9. TEAM SECTION
    // ==================================================
    const teamCard = (i, m) => `
        <li style="flex:1;min-width:250px;background:${SURFACE};padding:32px;border-radius:12px;text-align:center;box-shadow:0 4px 6px rgba(0,0,0,0.05);">
            <img src="${ph(192, 192, 'Portre')}" alt="Ekip Üyesi ${i + 1}" width="96" height="96" loading="${m.get('imageLoading') || 'lazy'}" style="width:96px;height:96px;border-radius:50%;object-fit:cover;margin:0 auto 20px;display:block;">
            <h3 style="font-size:1.25rem;font-weight:600;color:${DARK};">Ekip Üyesi ${i + 1}</h3>
            <p style="color:${MUTED};font-size:0.95rem;margin-bottom:16px;">Pozisyon / Ünvan</p>
            <div style="display:flex;justify-content:center;gap:12px;">
                <a href="#" aria-label="Ekip üyesi LinkedIn profili" style="color:${BLUE};">${icon('linkedin')}</a>
                <a href="#" aria-label="Ekip üyesi X profili" style="color:${BLUE};">${icon('twitter-x')}</a>
                <a href="#" aria-label="Ekip üyesi GitHub profili" style="color:${BLUE};">${icon('github')}</a>
            </div>
        </li>`;

    smart({
        id: 'elevare-team', label: 'Team Section', icon: 'bi-people',
        style: { padding: '80px 20px', 'background-color': BG },
        traits: [
            { type: 'number', name: 'memberCount', label: 'Member Count', min: 1, max: 6, value: 3, changeProp: 1 },
            { type: 'select', name: 'imageLoading', label: 'Görsel Yükleme (SEO)', options: [{ id: 'lazy', name: 'Lazy' }, { id: 'eager', name: 'Eager' }], value: 'lazy', changeProp: 1 }
        ],
        repeats: [{ trait: 'memberCount', container: '.el-team-grid', min: 1, max: 6, item: (m, i) => teamCard(i, m) }],
        on: { imageLoading: (m, v) => m.find('img').forEach(img => img.addAttributes({ loading: v })) },
        template: (m) => `
            <div style="max-width:1200px;margin:0 auto;">
                <h2 style="text-align:center;font-size:2.5rem;font-weight:700;margin-bottom:48px;">Ekibimizle Tanışın</h2>
                <ul class="el-team-grid" style="display:flex;flex-wrap:wrap;gap:30px;justify-content:center;list-style:none;margin:0;padding:0;">${rep(m.get('memberCount'), (i) => teamCard(i, m))}</ul>
            </div>`
    });

    // ==================================================
    // 10. LOGO CLOUD / PARTNERS
    // ==================================================
    const logoItem = (i) => `<li style="padding:20px;opacity:0.7;filter:grayscale(100%);font-weight:700;color:${MUTED};">MARKA ${i + 1}</li>`;

    smart({
        id: 'elevare-logos', label: 'Logo Cloud', icon: 'bi-building',
        style: { padding: '60px 20px', 'background-color': WHITE },
        traits: [{ type: 'number', name: 'logoCount', label: 'Logo Count', min: 2, max: 8, value: 5, changeProp: 1 }],
        repeats: [{ trait: 'logoCount', container: '.el-logo-row', min: 2, max: 8, item: (m, i) => logoItem(i) }],
        template: (m) => `
            <div style="max-width:1200px;margin:0 auto;text-align:center;">
                <p style="color:${MUTED};margin-bottom:24px;text-transform:uppercase;letter-spacing:2px;font-size:0.9rem;">Sektör Liderlerinin Güvendiği Markalar</p>
                <ul class="el-logo-row" style="display:flex;flex-wrap:wrap;justify-content:center;align-items:center;gap:40px;list-style:none;margin:0;padding:0;">${rep(m.get('logoCount'), logoItem)}</ul>
            </div>`
    });

    // ==================================================
    // 11. STATS COUNTER
    // ==================================================
    const STAT_NUMS = ['2,450', '18', '99.9%', '150+'];
    const STAT_LABELS = ['Tamamlanan Proje', 'Yıllık Deneyim', 'Müşteri Memnuniyeti', 'Uzman Personel'];
    const statItem = (i) => `
        <li style="flex:1;min-width:200px;padding:20px;text-align:center;">
            <div style="font-size:3rem;font-weight:800;margin-bottom:8px;">${STAT_NUMS[i % 4]}</div>
            <div style="font-size:1.1rem;opacity:0.8;">${STAT_LABELS[i % 4]}</div>
        </li>`;

    smart({
        id: 'elevare-stats', label: 'Stats Counter', icon: 'bi-graph-up-arrow',
        style: { padding: '80px 20px', 'background-color': NAVY, color: '#fff' },
        traits: [{ type: 'number', name: 'statCount', label: 'Stat Count', min: 1, max: 4, value: 3, changeProp: 1 }],
        repeats: [{ trait: 'statCount', container: '.el-stats-row', min: 1, max: 4, item: (m, i) => statItem(i) }],
        template: (m) => `
            <ul class="el-stats-row" style="max-width:1000px;margin:0 auto;display:flex;flex-wrap:wrap;gap:20px;justify-content:center;list-style:none;padding:0;">${rep(m.get('statCount'), statItem)}</ul>`
    });

    // ==================================================
    // 12. VERTICAL TIMELINE
    // ==================================================
    const tlItem = (i) => {
        const right = i % 2 !== 0;
        return `
        <li style="display:flex;align-items:center;margin-bottom:40px;flex-direction:${right ? 'row-reverse' : 'row'};">
            <div style="flex:1;text-align:${right ? 'right' : 'left'};padding:0 20px;">
                <div style="background:${SURFACE2};padding:20px;border-radius:8px;display:inline-block;">
                    <h4 style="margin:0;font-weight:600;">Dönüm Noktası ${i + 1}</h4>
                    <p style="color:${MUTED};margin:8px 0 0;">Başarı ve kilometre taşlarının açıklaması.</p>
                    <small style="color:${MUTED};">202${i % 10}</small>
                </div>
            </div>
            <div style="width:16px;height:16px;background:${BLUE};border-radius:50%;z-index:1;margin:0 10px;flex-shrink:0;"></div>
            <div style="flex:1;padding:0 20px;"></div>
        </li>`;
    };

    // The empty side shipped without the card side's padding. flex:1 shares out the
    // space left after padding, so the card side came out 40px wider and every dot
    // sat 20px off the line. Blocks saved that way are evened out when opened.
    const evenTimelineSides = (m) => findModels(m, '.el-tl-list').forEach((list) => list.components().forEach((li) => {
        const sides = li.components().filter((c) => (c.getStyle() || {}).flex);
        if (sides.length !== 2) return;
        const padding = sides[0].getStyle()['padding'] || sides[1].getStyle()['padding'];
        if (!padding) return;
        sides.forEach((side) => { if (side.getStyle()['padding'] !== padding) side.addStyle({ padding }); });
    }));

    smart({
        id: 'elevare-timeline', label: 'Timeline', icon: 'bi-clock-history',
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [{ type: 'number', name: 'eventCount', label: 'Event Count', min: 1, max: 6, value: 3, changeProp: 1 }],
        repeats: [{ trait: 'eventCount', container: '.el-tl-list', min: 1, max: 6, item: (m, i) => tlItem(i) }],
        onLoad: evenTimelineSides,
        template: (m) => `
            <div style="max-width:800px;margin:0 auto;position:relative;">
                <div style="position:absolute;left:50%;transform:translateX(-50%);width:2px;height:100%;background:${BORDER};top:0;"></div>
                <h2 style="text-align:center;font-size:2.5rem;margin-bottom:48px;">Yolculuğumuz</h2>
                <ol class="el-tl-list" style="list-style:none;margin:0;padding:0;">${rep(m.get('eventCount'), tlItem)}</ol>
            </div>`
    });

    // ==================================================
    // 13. GALLERY / MASONRY (CWV Optimized)
    // ==================================================
    const galleryItem = (i, m) => {
        const h = 400 + (i % 3) * 100;
        return `<li style="break-inside:avoid;margin-bottom:16px;"><img src="${ph(600, h, 'Görsel')}" alt="Galeri görseli ${i + 1}" width="600" height="${h}" loading="${m.get('imageLoading') || 'lazy'}" style="width:100%;height:auto;border-radius:8px;display:block;"></li>`;
    };

    smart({
        id: 'elevare-gallery', label: 'Gallery Masonry', icon: 'bi-collection',
        style: { padding: '80px 20px', 'background-color': BG },
        traits: [
            { type: 'number', name: 'imageCount', label: 'Image Count', min: 3, max: 12, value: 6, changeProp: 1 },
            // column-count has no Style Manager field — masonry layouts have no
            // dedicated sector anywhere in GrapesJS's defaults — so, same as Mega
            // Menu's panelColumns, this stays a trait rather than a hardcoded value
            // with no control at all.
            { type: 'number', name: 'columns', label: 'Sütun Sayısı', min: 2, max: 6, value: 3, changeProp: 1 },
            { type: 'select', name: 'imageLoading', label: 'Görsel Yükleme (SEO)', options: [{ id: 'lazy', name: 'Lazy' }, { id: 'eager', name: 'Eager' }], value: 'lazy', changeProp: 1 }
        ],
        repeats: [{ trait: 'imageCount', container: '.el-gallery-cols', min: 3, max: 12, item: (m, i) => galleryItem(i, m) }],
        on: {
            imageLoading: (m, v) => m.find('img').forEach(img => img.addAttributes({ loading: v })),
            columns: (m, v) => { const cols = m.find('.el-gallery-cols')[0]; if (cols) cols.addStyle({ 'column-count': String(clamp(parseInt(v, 10) || 3, 2, 6)) }); }
        },
        template: (m) => `
            <div style="max-width:1200px;margin:0 auto;">
                <h2 style="text-align:center;font-size:2.5rem;margin-bottom:40px;">Çalışmalarımız</h2>
                <ul class="el-gallery-cols" style="column-count:${clamp(parseInt(m.get('columns'), 10) || 3, 2, 6)};column-gap:16px;list-style:none;margin:0;padding:0;">${rep(m.get('imageCount'), (i) => galleryItem(i, m))}</ul>
            </div>`
    });

    // ==================================================
    // 14. CONTACT FORM
    // ==================================================
    smart({
        id: 'elevare-contact', label: 'Contact Form', icon: 'bi-envelope',
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [
            { type: 'text', name: 'nameLabel', label: 'Name Label', value: 'Adınız Soyadınız', changeProp: 1 },
            { type: 'text', name: 'emailLabel', label: 'Email Label', value: 'E-posta Adresiniz', changeProp: 1 },
            { type: 'text', name: 'messageLabel', label: 'Message Label', value: 'Mesajınız', changeProp: 1 },
            { type: 'text', name: 'buttonText', label: 'Button Text', value: 'Mesaj Gönder', changeProp: 1 }
        ],
        on: {
            nameLabel: (m, v) => { const t = m.find('.el-c-lbl-name')[0]; if (t && v != null) t.components(v); },
            emailLabel: (m, v) => { const t = m.find('.el-c-lbl-email')[0]; if (t && v != null) t.components(v); },
            messageLabel: (m, v) => { const t = m.find('.el-c-lbl-msg')[0]; if (t && v != null) t.components(v); },
            buttonText: (m, v) => { const t = m.find('.el-c-btn')[0]; if (t && v != null) t.components(v); }
        },
        template: (m) => {
            const u = uid();
            return `
            <div style="max-width:600px;margin:0 auto;">
                <h2 style="text-align:center;margin-bottom:32px;">Bize Ulaşın</h2>
                <form data-elevare-managed-form data-elevare-form-name="İletişim Formu" style="display:flex;flex-direction:column;gap:16px;">
                    <div style="display:flex;flex-direction:column;gap:6px;">
                        <label class="el-c-lbl-name" for="${u}-name" style="font-weight:600;font-size:0.9rem;color:${DARK};">${m.get('nameLabel')}</label>
                        <input id="${u}-name" type="text" name="name" autocomplete="name" style="padding:12px;border:1px solid ${BORDER};border-radius:6px;background:${SURFACE};color:${TEXT};">
                    </div>
                    <div style="display:flex;flex-direction:column;gap:6px;">
                        <label class="el-c-lbl-email" for="${u}-email" style="font-weight:600;font-size:0.9rem;color:${DARK};">${m.get('emailLabel')}</label>
                        <input id="${u}-email" type="email" name="email" autocomplete="email" style="padding:12px;border:1px solid ${BORDER};border-radius:6px;background:${SURFACE};color:${TEXT};">
                    </div>
                    <div style="display:flex;flex-direction:column;gap:6px;">
                        <label class="el-c-lbl-msg" for="${u}-msg" style="font-weight:600;font-size:0.9rem;color:${DARK};">${m.get('messageLabel')}</label>
                        <textarea id="${u}-msg" name="message" rows="4" style="padding:12px;border:1px solid ${BORDER};border-radius:6px;background:${SURFACE};color:${TEXT};"></textarea>
                    </div>
                    <button data-elevare-btn type="submit" class="el-c-btn" style="background:${BLUE};color:${ON_PRIMARY};padding:12px;border:none;border-radius:6px;font-weight:600;cursor:pointer;">${m.get('buttonText')}</button>
                </form>
            </div>`;
        }
    });

    // ==================================================
    // 14b. FILE FIELD — an attachment on any managed form
    // ==================================================
    // Dropped inside a form (the Contact Form, a Multi-step Form, or one built
    // from the Forms blocks). The public site accepts the file under a fixed
    // policy — see SharedKernel FormAttachmentPolicy: three files at most, ten
    // megabytes each, PDF/JPG/PNG/WebP/DOCX by their bytes — and the hint under
    // the field says so, so the visitor is told the limits before choosing. What
    // the author chooses here is narrower, never wider: the type set is a
    // subset of the policy's, written into `accept` (a first, client-side filter;
    // the server decides).
    const FILE_ACCEPT = {
        all: '.pdf,.jpg,.jpeg,.png,.webp,.docx',
        documents: '.pdf,.docx',
        images: '.jpg,.jpeg,.png,.webp'
    };
    const FILE_HINT = {
        all: 'PDF, JPG, PNG, WebP, DOCX · en fazla 10 MB',
        documents: 'PDF, DOCX · en fazla 10 MB',
        images: 'JPG, PNG, WebP · en fazla 10 MB'
    };
    smart({
        id: 'elevare-file-field', label: 'File Field', icon: 'bi-paperclip', tag: 'div',
        style: { display: 'flex', 'flex-direction': 'column', gap: '6px' },
        traits: [
            { type: 'text', name: 'fieldLabel', label: 'Etiket', value: 'Dosya ekleyin', changeProp: 1 },
            { type: 'text', name: 'fieldName', label: 'Alan adı (name)', value: 'dosya', changeProp: 1 },
            {
                type: 'select', name: 'fileTypes', label: 'Kabul edilen türler', value: 'all', changeProp: 1,
                options: [{ id: 'all', name: 'Belge ve görsel' }, { id: 'documents', name: 'Yalnızca belge (PDF, DOCX)' }, { id: 'images', name: 'Yalnızca görsel' }]
            },
            { type: 'checkbox', name: 'multiple', label: 'Birden fazla dosya (en fazla 3)', value: false, changeProp: 1 },
            { type: 'checkbox', name: 'required', label: 'Zorunlu', value: false, changeProp: 1 }
        ],
        on: {
            fieldLabel: (m, v) => { const l = m.find('.el-file-label')[0]; if (l && v != null) l.components(v); },
            fieldName: (m, v) => { const i = m.find('.el-file-input')[0]; if (i && v) i.addAttributes({ name: v }); },
            fileTypes: (m, v) => {
                const i = m.find('.el-file-input')[0]; const h = m.find('.el-file-hint')[0];
                if (i) i.addAttributes({ accept: FILE_ACCEPT[v] || FILE_ACCEPT.all });
                if (h) h.components(FILE_HINT[v] || FILE_HINT.all);
            },
            multiple: (m, v) => { const i = m.find('.el-file-input')[0]; if (i) v ? i.addAttributes({ multiple: '' }) : i.removeAttributes(['multiple']); },
            required: (m, v) => { const i = m.find('.el-file-input')[0]; if (i) v ? i.addAttributes({ required: '' }) : i.removeAttributes(['required']); }
        },
        template: (m) => {
            const u = uid();
            const types = m.get('fileTypes') || 'all';
            return `
                <label class="el-file-label" for="${u}-file" style="font-weight:600;font-size:0.9rem;color:${DARK};">${m.get('fieldLabel')}</label>
                <input id="${u}-file" class="el-file-input" type="file" name="${m.get('fieldName') || 'dosya'}" accept="${FILE_ACCEPT[types]}" style="padding:10px;border:1px dashed ${BORDER};border-radius:6px;background:${SURFACE};color:${TEXT};">
                <small class="el-file-hint" style="font-size:0.8rem;color:${MUTED};">${FILE_HINT[types]}</small>`;
        }
    });

    // ==================================================
    // 15. VIDEO EMBED (responsive iframe)
    // ==================================================
    smart({
        id: 'elevare-video', label: 'Video Embed', icon: 'bi-camera-video',
        style: { padding: '80px 20px', 'background-color': BG },
        traits: [
            // about:blank, not a sample YouTube id. The old default was a real,
            // recognisable video (the Rickroll) that a page shipping unedited would
            // have embedded on a live customer site. An empty string is not the
            // alternative either — a browser resolves src="" against the current
            // document, so the block would embed the page inside itself.
            { type: 'text', name: 'videoUrl', label: 'Embed URL', value: 'about:blank', changeProp: 1 },
            { type: 'text', name: 'videoTitle', label: 'Video Title (a11y)', value: 'Tanıtım Videosu', changeProp: 1 }
        ],
        on: {
            videoUrl: (m, v) => { const f = m.find('.el-video-frame')[0]; if (f && v) f.addAttributes({ src: v }); },
            videoTitle: (m, v) => { const f = m.find('.el-video-frame')[0]; if (f && v) f.addAttributes({ title: v }); }
        },
        template: (m) => `
            <div style="max-width:800px;margin:0 auto;">
                <div style="position:relative;padding-bottom:56.25%;height:0;overflow:hidden;border-radius:10px;">
                    <iframe class="el-video-frame" src="${m.get('videoUrl')}" title="${m.get('videoTitle')}" loading="lazy" style="position:absolute;top:0;left:0;width:100%;height:100%;border:0;" allowfullscreen></iframe>
                </div>
            </div>`
    });

    // ==================================================
    // 16. GOOGLE MAP
    // ==================================================
    smart({
        id: 'elevare-map', label: 'Google Map', icon: 'bi-geo-alt',
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [{ type: 'text', name: 'mapAddress', label: 'Google Maps Embed URL', value: siteValue('mapEmbedUrl', 'https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3151.8354345093747!2d144.95373531531615!3d-37.81627917975171!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x6ad642af0f11fd81%3A0xf577b1b1e1b1e1b1!2sMelbourne%20CBD!5e0!3m2!1sen!2sau!4v1610000000000'), changeProp: 1 }],
        on: {
            mapAddress: (m, v) => { const f = m.find('.el-map-frame')[0]; if (f && v) f.addAttributes({ src: v }); }
        },
        template: (m) => `
            <div style="max-width:1000px;margin:0 auto;">
                <iframe class="el-map-frame" src="${m.get('mapAddress')}" title="Location map" width="100%" height="400" style="border:0;" allowfullscreen="" loading="lazy"></iframe>
            </div>`
    });

    // ==================================================
    // 17. FOOTER
    // ==================================================
    const FOOT_TITLES = ['Ürünler', 'Kurumsal', 'Kaynaklar', 'Yasal'];
    const footCol = (i) => `
        <div style="flex:1;min-width:150px;">
            <h4 style="font-weight:600;margin-bottom:16px;">${FOOT_TITLES[i % 4]}</h4>
            <ul style="list-style:none;padding:0;line-height:2;margin:0;">
                <li><a href="#" style="color:${SOFT};text-decoration:none;">Bağlantı ${i + 1}.1</a></li>
                <li><a href="#" style="color:${SOFT};text-decoration:none;">Bağlantı ${i + 1}.2</a></li>
                <li><a href="#" style="color:${SOFT};text-decoration:none;">Bağlantı ${i + 1}.3</a></li>
            </ul>
        </div>`;

    smart({
        id: 'elevare-footer', label: 'Footer', icon: 'bi-arrow-bar-down',
        tag: 'footer',
        style: { padding: '60px 20px', 'background-color': '#111827', color: '#f3f4f6' },
        traits: [
            // Same reasoning and same fallback chain as Navbar's brandText: the real
            // site name from Site Settings, never this file's own product name.
            { type: 'text', name: 'brandText', label: 'Brand Text', value: siteValue('siteName', 'Site Adı'), changeProp: 1 },
            { type: 'number', name: 'columnCount', label: 'Link Columns', min: 1, max: 4, value: 3, changeProp: 1 }
        ],
        on: {
            brandText: (m, v) => { const h = m.find('.el-foot-brand')[0]; if (h && v != null) h.components(v); }
        },
        repeats: [{ trait: 'columnCount', container: '.el-foot-cols', min: 1, max: 4, item: (m, i) => footCol(i) }],
        template: (m) => `
            <div style="max-width:1200px;margin:0 auto;display:flex;flex-wrap:wrap;gap:40px;justify-content:space-between;">
                <div style="flex:2;min-width:200px;">
                    <h3 class="el-foot-brand" style="font-size:1.5rem;margin-bottom:12px;">${m.get('brandText') || siteValue('siteName', 'Site Adı')}</h3>
                    <p style="color:${SOFT};">Daha iyi dijital deneyimler inşa ediyoruz.</p>
                    <div style="margin-top:16px;display:flex;gap:12px;">
                        <a href="${contactValue('facebook', '#')}" aria-label="Facebook" style="color:${SOFT};">${icon('facebook')}</a>
                        <a href="${contactValue('x', '#')}" aria-label="X (Twitter)" style="color:${SOFT};">${icon('twitter-x')}</a>
                        <a href="${contactValue('instagram', '#')}" aria-label="Instagram" style="color:${SOFT};">${icon('instagram')}</a>
                        <a href="${contactValue('linkedIn', '#')}" aria-label="LinkedIn" style="color:${SOFT};">${icon('linkedin')}</a>
                        <a href="${contactValue('youtube', '#')}" aria-label="YouTube" style="color:${SOFT};">${icon('youtube')}</a>
                    </div>
                </div>
                <div class="el-foot-cols" style="flex:3;min-width:280px;display:flex;flex-wrap:wrap;gap:40px;">${rep(m.get('columnCount'), footCol)}</div>
            </div>
            <div style="text-align:center;margin-top:40px;padding-top:20px;border-top:1px solid #374151;color:${SOFT};font-size:0.9rem;">&copy; ${new Date().getFullYear()} ${m.get('brandText') || siteValue('siteName', 'Site Adı')}. Tüm hakları saklıdır.</div>`
    });

    // ==================================================
    // 18. NEWSLETTER
    // ==================================================
    // Sign-ups land in Formlar as the "Bülten Aboneliği" form; sending is left to a
    // mail service, so the block only collects addresses — with the visitor's
    // consent, which a commercial newsletter needs on record. The privacy link is
    // drawn only once it has an address: a "#" link reads as finished when it is not.
    const hasClass = (c, cls) => c.getClasses().indexOf(cls) >= 0;
    const setNewsletterPrivacy = (m, url) => {
        const row = descendants(m).find((c) => hasClass(c, 'el-nl-consent-row'));
        if (!row) return;
        descendants(row).filter((c) => hasClass(c, 'el-nl-privacy')).forEach((a) => a.remove());
        const href = String(url || '').trim().replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');
        if (href) {
            row.append(`<a class="el-nl-privacy" href="${href}" style="color:inherit;text-decoration:underline;white-space:nowrap;">${CT('Ayrıntılar', 'Details')}</a>`);
        }
    };
    smart({
        id: 'elevare-newsletter', label: 'Newsletter', icon: 'bi-newspaper',
        style: { padding: '80px 20px', 'background-color': NAVY, color: '#ffffff' },
        traits: [
            { type: 'text', name: 'placeholder', label: 'Email Placeholder', value: CT('E-posta adresiniz', 'Your email address'), changeProp: 1 },
            { type: 'text', name: 'buttonText', label: 'Button Text', value: CT('Abone Ol', 'Subscribe'), changeProp: 1 },
            { type: 'text', name: 'privacyUrl', label: LOCALE === 'tr' ? 'Gizlilik sayfası adresi' : 'Privacy page address', value: '', changeProp: 1 }
        ],
        on: {
            placeholder: (m, v) => { const i = m.find('.el-nl-input')[0]; if (i && v != null) i.addAttributes({ placeholder: v, 'aria-label': v }); },
            buttonText: (m, v) => { const b = m.find('.el-nl-btn')[0]; if (b && v != null) b.components(v); },
            privacyUrl: (m, v) => setNewsletterPrivacy(m, v)
        },
        onLoad: (m) => {
            const a = descendants(m).find((c) => hasClass(c, 'el-nl-privacy'));
            if (a) m.set('privacyUrl', a.getAttributes().href || '', { silent: true });
        },
        template: (m) => `
            <div style="max-width:500px;margin:0 auto;text-align:center;">
                <h2 class="el-nl-title" style="font-size:2rem;margin-bottom:16px;">${CT('Gelişmelerden Haberdar Olun', 'Stay in the Loop')}</h2>
                <p class="el-nl-text" style="margin-bottom:24px;opacity:0.8;">${CT('En son haberler ve güncellemeler için bültenimize kayıt olun.', 'Sign up for our newsletter to get the latest news and updates.')}</p>
                <form data-elevare-managed-form data-elevare-form-name="Bülten Aboneliği" style="display:flex;flex-wrap:wrap;gap:8px;">
                    <input class="el-nl-input" type="email" name="email" autocomplete="email" required placeholder="${m.get('placeholder')}" aria-label="${m.get('placeholder')}" style="flex:1;min-width:200px;padding:12px;border:none;border-radius:6px;background:#ffffff;color:#18181b;">
                    <button data-elevare-btn type="submit" class="el-nl-btn" style="background:${SURFACE};color:${NAVY};border:none;padding:12px 24px;border-radius:6px;font-weight:600;cursor:pointer;">${m.get('buttonText')}</button>
                    <div class="el-nl-consent-row" style="flex-basis:100%;display:flex;flex-wrap:wrap;gap:4px 8px;align-items:flex-start;text-align:left;font-size:0.85rem;opacity:0.85;margin-top:4px;">
                        <label class="el-nl-consent" style="display:flex;gap:8px;align-items:flex-start;cursor:pointer;">
                            <input type="checkbox" name="consent" value="yes" required style="margin-top:3px;flex-shrink:0;">
                            <span class="el-nl-consent-text">${CT('Bülten e-postalarını almayı kabul ediyorum; istediğim zaman ayrılabilirim.', 'I agree to receive the newsletter and can unsubscribe at any time.')}</span>
                        </label>
                    </div>
                </form>
            </div>`
    });

    // ==================================================
    // 19. PROGRESS BARS
    // ==================================================
    const PROG_LABELS = ['Tasarım', 'Yazılım', 'SEO', 'Pazarlama', 'Destek', 'Bulut'];
    const PROG_WIDTHS = [90, 85, 75, 80, 70, 65];
    const progItem = (i) => `
        <li style="margin-bottom:24px;">
            <div style="display:flex;justify-content:space-between;margin-bottom:8px;">
                <span style="font-weight:600;">${PROG_LABELS[i % 6]}</span>
                <span>${PROG_WIDTHS[i % 6]}%</span>
            </div>
            <div style="background:${BORDER};border-radius:10px;height:8px;" role="progressbar" aria-valuenow="${PROG_WIDTHS[i % 6]}" aria-valuemin="0" aria-valuemax="100" aria-label="${PROG_LABELS[i % 6]}">
                <div style="width:${PROG_WIDTHS[i % 6]}%;background:${BLUE};height:100%;border-radius:10px;"></div>
            </div>
        </li>`;

    smart({
        id: 'elevare-progress', label: 'Progress Bars', icon: 'bi-bar-chart-fill',
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [{ type: 'number', name: 'barCount', label: 'Bar Count', min: 1, max: 6, value: 3, changeProp: 1 }],
        repeats: [{ trait: 'barCount', container: '.el-prog-list', min: 1, max: 6, item: (m, i) => progItem(i) }],
        template: (m) => `
            <div style="max-width:600px;margin:0 auto;">
                <h2 style="text-align:center;font-size:2.5rem;margin-bottom:40px;">Uzmanlık Alanlarımız</h2>
                <ul class="el-prog-list" style="list-style:none;margin:0;padding:0;">${rep(m.get('barCount'), progItem)}</ul>
            </div>`
    });

    // ==================================================
    // 20. NAVBAR / HEADER
    // ==================================================
    const navLink = (i) => `<li><a href="#" style="color:${DARK};text-decoration:none;font-weight:500;">Sayfa ${i + 1}</a></li>`;

    smart({
        id: 'elevare-navbar', label: 'Navbar', icon: 'bi-window',
        tag: 'header',
        style: { position: 'relative', padding: '16px 20px', 'background-color': SURFACE, 'border-bottom': '1px solid ' + BORDER },
        traits: [
            // The logo is almost always uploaded through the Media Library, which
            // already asked for alt text at upload time (see PageEditor.razor's
            // GetMediaFileAltTextQuery lookup feeding setSiteInfo) — that description
            // is what a screen reader should say, not the site's name repeated on
            // every single page. Only when no such record exists (a raw URL typed in,
            // or no logo at all) does the site name from Site Settings stand in.
            { type: 'text', name: 'brandText', label: 'Brand Text', value: siteValue('logoAlt', siteValue('siteName', 'Site Adı')), changeProp: 1 },
            { type: 'number', name: 'linkCount', label: 'Link Count', min: 1, max: 6, value: 4, changeProp: 1 }
        ],
        on: {
            // The logo (when Site Settings has one) is a plain <img>, not text, so
            // editing Brand Text only ever touches its alt — the image itself is set
            // once at drop time and from then on is an ordinary image on the canvas,
            // replaceable/removable like any other.
            brandText: (m, v) => {
                const b = m.find('.el-nav-brand')[0]; if (!b || v == null) return;
                const img = b.find ? b.find('img')[0] : null;
                if (img) img.addAttributes({ alt: v }); else b.components(v);
            }
        },
        repeats: [{ trait: 'linkCount', container: '.el-nav-links', min: 1, max: 6, item: (m, i) => navLink(i) }],
        script: navbarScript,
        template: (m) => {
            const logoUrl = siteValue('logoUrl', '');
            const brand = m.get('brandText') || siteValue('logoAlt', siteValue('siteName', 'Site Adı'));
            const brandInner = logoUrl
                ? `<img src="${logoUrl}" alt="${brand}" style="max-height:36px;width:auto;display:block;">`
                : brand;
            return `
            <style>
                .el-navbar-in{max-width:1200px;margin:0 auto;display:flex;align-items:center;justify-content:space-between;gap:24px;}
                .el-nav-links{display:flex;gap:24px;list-style:none;margin:0;padding:0;}
                .el-nav-toggle{display:none;background:none;border:0;font-size:1.6rem;cursor:pointer;color:${DARK};}
                @media (max-width: 767px){
                    .el-nav-toggle{display:block;}
                    .el-nav-links{display:none;position:absolute;top:100%;left:0;right:0;background:${SURFACE};flex-direction:column;padding:16px 20px;border-bottom:1px solid ${BORDER};z-index:20;}
                    .el-nav-links.open{display:flex;}
                }
            </style>
            <div class="el-navbar-in">
                <a href="/" class="el-nav-brand" data-gjs-droppable="true" style="font-size:1.4rem;font-weight:800;color:${DARK};text-decoration:none;display:inline-flex;align-items:center;gap:10px;min-height:36px;min-width:40px;">${brandInner}</a>
                <button data-elevare-btn type="button" class="el-nav-toggle" aria-label="Mobil menüyü aç" aria-expanded="false" data-gjs-draggable="false">${icon('list')}</button>
                <nav aria-label="Ana Navigasyon">
                    <ul class="el-nav-links">${rep(m.get('linkCount'), navLink)}</ul>
                </nav>
            </div>`;
        }
    });

    // ==================================================
    // 21. BREADCRUMBS
    // ==================================================
    smart({
        id: 'elevare-breadcrumbs', label: 'Breadcrumbs', icon: 'bi-signpost-split',
        tag: 'div',
        attrs: { 'data-elevare-breadcrumb': '', 'data-elevare-breadcrumb-home': CT('Ana Sayfa', 'Home') },
        style: { padding: '14px 20px', 'background-color': BG, 'border-bottom': '1px solid ' + BORDER },
        traits: [
            {
                type: 'text', name: 'data-elevare-breadcrumb-home', label: 'Ana sayfa etiketi',
                placeholder: 'Boş bırakılırsa ana sayfa halkası eklenmez'
            }
        ],
        template: () => `
            <nav aria-label="Sayfa Yolu" style="max-width:1200px;margin:0 auto;">
                <ol class="elevare-breadcrumb-list" style="display:flex;flex-wrap:wrap;gap:8px;list-style:none;margin:0;padding:0;font-size:0.9rem;align-items:center;">
                    <li data-elevare-crumb-template style="display:flex;align-items:center;gap:8px;">
                        <a href="#" data-elevare-crumb-link style="color:${BLUE};text-decoration:none;">Sayfa Adı</a>
                        <span aria-hidden="true" data-elevare-crumb-sep style="color:${MUTED};">/</span>
                    </li>
                </ol>
            </nav>`
    });

    // ---- Canvas previews of what the public site fills in ------------------------
    // Sayfa Yolu, Önceki / Sonraki Yazı and an Article's reading time are written by
    // the public site on every request (BreadcrumbResolutionService,
    // AdjacentPageResolutionService, ContentEnhancer); the block itself carries only
    // a template crumb or sample text, so the canvas showed "Sayfa Adı /" where the
    // page will have its real trail. The canvas now shows what this page will get —
    // drawn into the canvas only (never the model, never saved), the way the page
    // listing's cards are, and drawn again whenever GrapesJS redraws the block.
    const PREVIEW_T = LOCALE === 'tr' ? {
        crumbTemplate: 'Adım şablonu — tasarımı buradan',
        noPrev: 'Önceki yazı yok — yayında gösterilmez',
        noNext: 'Sonraki yazı yok — yayında gösterilmez',
        noAdjacent: 'Önceki/sonraki yazı yok — blok yayında gösterilmez'
    } : {
        crumbTemplate: 'Crumb template — design it here',
        noPrev: 'No previous post — not shown on the site',
        noNext: 'No next post — not shown on the site',
        noAdjacent: 'No previous/next post — the block is not shown on the site'
    };
    const GHOST = 'data-elevare-ghost';
    const GHOST_NOTE = 'data-elevare-ghost-hidden';
    const EDITING_ATTR = 'data-elevare-bc-editing';
    // undefined: not asked yet; null: nothing to show (a template, or no answer).
    let breadcrumbData;
    let adjacentData;
    // A preview can still be pending when the editor is torn down (reloaded after a
    // save that moved the page); by then there is no canvas to draw into.
    const canvasDocument = () => {
        try { return editor.Canvas && editor.Canvas.getDocument && editor.Canvas.getDocument(); }
        catch (_) { return null; }
    };
    const askPreview = (name) => {
        const ge = window.grapesEditor;
        return ge && typeof ge[name] === 'function' ? ge[name]() : Promise.resolve(null);
    };
    const withoutEditorMarks = (el) => {
        [el, ...el.querySelectorAll('*')].forEach((n) => {
            [...n.classList].filter((c) => c.startsWith('gjs-')).forEach((c) => n.classList.remove(c));
            [...n.attributes].filter((a) => a.name.startsWith('data-gjs') || a.name === 'id').forEach((a) => n.removeAttribute(a.name));
        });
        return el;
    };
    const selectionInside = (el) => editor.getSelectedAll().some((c) => {
        const cel = c.getEl && c.getEl();
        return cel && (cel === el || el.contains(cel));
    });

    const drawBreadcrumbs = (doc) => {
        doc.querySelectorAll('[data-elevare-breadcrumb]').forEach((block) => {
            block.querySelectorAll(`[${GHOST}]`).forEach((n) => n.remove());
            if (selectionInside(block)) block.setAttribute(EDITING_ATTR, '');
            else block.removeAttribute(EDITING_ATTR);
            const template = block.querySelector('[data-elevare-crumb-template]');
            if (!template || !breadcrumbData) return;
            const home = (block.getAttribute('data-elevare-breadcrumb-home') || '').trim();
            const crumbs = [
                ...(home && !breadcrumbData.isHome ? [{ title: home, path: breadcrumbData.homePath }] : []),
                ...(breadcrumbData.crumbs || [])
            ];
            crumbs.forEach((crumb, i) => {
                const last = i === crumbs.length - 1;
                const li = withoutEditorMarks(template.cloneNode(true));
                li.removeAttribute('data-elevare-crumb-template');
                li.setAttribute(GHOST, '');
                li.style.pointerEvents = 'none';
                const link = li.matches('[data-elevare-crumb-link]') ? li : li.querySelector('[data-elevare-crumb-link]');
                if (link) {
                    link.textContent = crumb.title;
                    if (last) { link.removeAttribute('href'); link.setAttribute('aria-current', 'page'); }
                    else link.setAttribute('href', crumb.path);
                }
                if (last) { const sep = li.querySelector('[data-elevare-crumb-sep]'); if (sep) sep.remove(); }
                template.before(li);
            });
        });
    };

    const drawAdjacent = (doc) => {
        doc.querySelectorAll('[data-elevare-block="elevare-prevnext"]').forEach((block) => {
            block.removeAttribute(GHOST_NOTE);
            if (!adjacentData) return;
            const sides = { prev: adjacentData.previous, next: adjacentData.next };
            block.querySelectorAll('[data-elevare-adjacent]').forEach((link) => {
                const side = link.getAttribute('data-elevare-adjacent');
                const target = sides[side];
                link.removeAttribute(GHOST_NOTE);
                if (target) link.querySelectorAll('[data-elevare-field="title"]').forEach((f) => { if (f.textContent !== target.title) f.textContent = target.title; });
                else link.setAttribute(GHOST_NOTE, side === 'prev' ? PREVIEW_T.noPrev : PREVIEW_T.noNext);
            });
            if (!sides.prev && !sides.next) block.setAttribute(GHOST_NOTE, PREVIEW_T.noAdjacent);
        });
    };

    // The public site's count: words of the Article's body, 200 a minute, at least 1.
    const READING_WORDS = /[\p{L}\p{N}]+(?:['’]\p{L}+)?/gu;
    const drawReadingTimes = (doc) => {
        doc.querySelectorAll('[data-elevare-article-reading-time]').forEach((slot) => {
            const scope = slot.closest('[data-elevare-article]') || doc.body;
            const body = scope.querySelector('[data-elevare-article-body]') || scope;
            const minutes = Math.max(1, Math.round((body.textContent.match(READING_WORDS) || []).length / 200));
            const text = CT(`${minutes} dk okuma`, `${minutes} min read`);
            if (slot.textContent !== text) slot.textContent = text;
        });
    };

    const PREVIEW_STYLE_ID = 'elevare-preview-canvas-css';
    const installPreviewCss = (doc) => {
        if (!doc.head || doc.getElementById(PREVIEW_STYLE_ID)) return;
        const style = doc.createElement('style');
        style.id = PREVIEW_STYLE_ID;
        // The template crumb steps aside while the real trail is shown, and comes
        // back — labelled — while the block is being edited, to be styled.
        style.textContent =
            // !important: the crumb's own display comes from an id rule (#…{display:flex}),
            // which outranks any chain of attribute selectors.
            `[data-elevare-breadcrumb]:has([${GHOST}]):not([${EDITING_ATTR}]) [data-elevare-crumb-template]{display:none!important}` +
            `[data-elevare-breadcrumb] [data-elevare-crumb-template]{position:relative;outline:1px dashed #3b82f6;outline-offset:2px}` +
            `[data-elevare-breadcrumb]:has([${GHOST}]) [data-elevare-crumb-template]::after{content:${JSON.stringify(PREVIEW_T.crumbTemplate)};position:absolute;left:0;top:-18px;white-space:nowrap;font:600 10px/1.7 system-ui,sans-serif;padding:0 6px;border-radius:4px;background:#3b82f6;color:#fff;pointer-events:none}` +
            `[${GHOST_NOTE}]{position:relative}`;
        doc.head.appendChild(style);
    };

    let previewTimer = null;
    const drawPreviews = () => {
        clearTimeout(previewTimer);
        previewTimer = setTimeout(() => {
            const doc = canvasDocument();
            if (!doc) return;
            installPreviewCss(doc);
            drawBreadcrumbs(doc);
            drawAdjacent(doc);
            drawReadingTimes(doc);
        }, 80);
    };
    const loadPreviews = () => {
        const wrapper = editor.getWrapper();
        if (!wrapper) return;
        if (breadcrumbData === undefined && findModels(wrapper, '[data-elevare-breadcrumb]').length) {
            breadcrumbData = null;
            askPreview('getBreadcrumbPreview').then((d) => { breadcrumbData = d || null; drawPreviews(); });
        }
        if (adjacentData === undefined && findModels(wrapper, '[data-elevare-adjacent]').length) {
            adjacentData = null;
            askPreview('getAdjacentPreview').then((d) => { adjacentData = d || null; drawPreviews(); });
        }
        drawPreviews();
    };
    editor.on('load', loadPreviews);
    editor.on('canvas:frame:load', () => setTimeout(loadPreviews, 0));
    editor.on('component:add', loadPreviews);
    ['component:update', 'component:mount', 'component:toggled'].forEach((evt) => editor.on(evt, drawPreviews));
    editor.on('elevare:saved', () => { breadcrumbData = undefined; adjacentData = undefined; loadPreviews(); });

    // The byline's date, both halves at once: the datetime search engines read and
    // the text readers see. Setting only the attribute (as this used to) let the
    // two drift apart — a page read "13 Ağustos 2023" and said 2026-01-01. Written
    // in the byline's own language, told by its wording, not the CMS's.
    const ARTICLE_MONTHS = {
        tr: ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'],
        en: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December']
    };
    function writeArticleDate(m, iso) {
        const t = m.find('[data-elevare-article-date]')[0];
        const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso || '');
        if (!t || !match) return;
        const year = +match[1], month = +match[2], day = +match[3];
        const parent = t.parent();
        const line = (parent && parent.getEl() && parent.getEl().textContent) || '';
        const english = /^\s*(by|author)\b/i.test(line) || new RegExp('\\b(' + ARTICLE_MONTHS.en.join('|') + ')\\b').test(line);
        const text = english
            ? `${ARTICLE_MONTHS.en[month - 1]} ${day}, ${year}`
            : `${day} ${ARTICLE_MONTHS.tr[month - 1]} ${year}`;
        t.addAttributes({ datetime: iso.slice(0, 10) });
        t.components(text);
        // A day typed just before the <time> (as happens when the text is edited
        // on the canvas) would now read twice: "13 13 Ağustos 2023".
        if (parent) {
            const siblings = parent.components();
            const prev = siblings.at(siblings.indexOf(t) - 1);
            if (prev && prev.get('type') === 'textnode') {
                const content = String(prev.get('content') || '');
                const trimmed = content.replace(/\s*\d{1,2}\.?\s*$/, ' ');
                if (trimmed !== content) prev.set('content', trimmed);
            }
        }
    }

    // A byline the rich-text editor rewrote, put right: found on the live English
    // article, where the author <span> had become <font color><b>name</b></font>.
    // <font> was no text to GrapesJS, so the line came back from a copy as a plain
    // box — "Yazar:" could not be edited — and the author marker the structured data
    // reads was gone. The line is rebuilt as text, with the author back in its span
    // (bold and dark, as the block writes it). Only a line that needs it.
    function repairByline(m) {
        const time = findModels(m, '[data-elevare-article-date]')[0];
        const line = time && time.parent();
        if (!line || line === m || line.get('type') === 'text') return;
        const tmp = document.createElement('div');
        tmp.innerHTML = line.toHTML();
        const p = tmp.firstElementChild;
        if (!p) return;
        if (!p.querySelector('[data-elevare-article-author]')) {
            const author = [...p.children].find((el) => !el.matches('[data-elevare-article-date], .el-reading-time'));
            if (author) {
                const span = document.createElement('span');
                span.setAttribute('data-elevare-article-author', '');
                span.setAttribute('style', `font-weight:600;color:${DARK};`);
                span.textContent = author.textContent;
                author.replaceWith(span);
            }
        }
        const html = p.outerHTML;
        // After the block's own setup, not in the middle of it.
        setTimeout(() => {
            if (!line.parent()) return;
            if (typeof line.replaceWith === 'function') { line.replaceWith(html); return; }
            const parent = line.parent();
            const at = parent.components().indexOf(line);
            line.remove();
            parent.append(html, { at });
        }, 0);
    }

    // "· 6 dk okuma" after the byline's date. The number is the public site's to
    // write (ContentEnhancer counts the words actually published); this only places
    // the slot, with a sample the editor can see.
    function writeReadingTime(m, on) {
        const existing = m.find('.el-reading-time');
        if (!on) { existing.forEach((c) => c.remove()); return; }
        if (existing.length) return;
        const time = m.find('[data-elevare-article-date]')[0];
        const line = time && time.parent();
        if (!line) return;
        const sample = CT('5 dk okuma', '5 min read');
        line.append(`<span class="el-reading-time"> &middot; <span data-elevare-article-reading-time>${sample}</span></span>`);
    }

    // ==================================================
    // 22. ARTICLE SECTION
    // ==================================================
    smart({
        id: 'elevare-article', label: 'Article', icon: 'bi-file-richtext',
        tag: 'article',
        // The marker itself, read by the CMS's Structured Data builder to
        // pre-fill an Article node from this content — same pattern as the FAQ
        // block's data-elevare-faq-*, so the two can never say something different.
        attrs: { 'data-elevare-article': '' },
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [
            { type: DATE_TRAIT, name: 'dateIso', label: LOCALE === 'tr' ? 'Yayın Tarihi' : 'Published', value: '2026-01-01', changeProp: 1 },
            { type: 'checkbox', name: 'showReadingTime', label: LOCALE === 'tr' ? 'Okuma süresini göster' : 'Show reading time', value: false, changeProp: 1 }
        ],
        on: {
            dateIso: (m, v) => writeArticleDate(m, v),
            showReadingTime: (m, v) => writeReadingTime(m, v === true || v === 'true')
        },
        // The traits show what the page actually carries, not this type's defaults.
        onLoad: (m) => {
            const t = findModels(m, '[data-elevare-article-date]')[0];
            const iso = t && String(t.getAttributes().datetime || '').slice(0, 10);
            if (iso && /^\d{4}-\d{2}-\d{2}$/.test(iso)) m.set('dateIso', iso, { silent: true });
            m.set('showReadingTime', findModels(m, '[data-elevare-article-reading-time]').length > 0, { silent: true });
            repairByline(m);
        },
        template: (m) => `
            <div style="max-width:760px;margin:0 auto;">
                <header style="margin-bottom:24px;">
                    <h2 data-elevare-article-headline style="font-size:2.2rem;font-weight:800;color:${DARK};margin:0 0 12px;line-height:1.25;">Makale Ana Başlığı Buraya Gelecek</h2>
                    <p style="color:${MUTED};font-size:0.95rem;margin:0;">Yazar: <span data-elevare-article-author style="font-weight:600;color:${DARK};">Yazar Adı</span> &middot; <time class="el-article-date" data-elevare-article-date datetime="${m.get('dateIso')}">1 Ocak 2026</time></p>
                </header>
                <div data-elevare-article-body style="color:${TEXT};line-height:1.8;font-size:1.05rem;">
                    <p>Açılış paragrafı. Okuyucunun dikkatini çekecek temel argümanı buraya girin. Arama motorları genellikle ilk 100 kelimeye ağırlık verir, bu nedenle odak anahtar kelimenizi burada doğal bir şekilde geçirmeye özen gösterin.</p>
                    <h3 style="font-size:1.4rem;color:${DARK};margin:32px 0 12px;">Alt Başlık</h3>
                    <p>İçerik metni devam ediyor. Paragrafları kısa tutun ve okumayı kolaylaştırmak için alt başlıklar kullanın.</p>
                </div>
            </div>`
    });

    // ==================================================
    // 23. CARD GRID — one neutral grid, no subject baked in
    // ==================================================
    // Replaces two blocks that were the same layout wearing different words: "Blog
    // Kartları" (image, title, excerpt, "Makaleyi Oku") and "Ürün Kartları" (image,
    // title, description, price, "Sepete Ekle"). Both were removed.
    //
    // Blog Kartları duplicated Sayfa Listesi, which already builds the same cards
    // from the site's REAL pages instead of placeholder text that has to be edited
    // by hand and then re-edited every time a post is added. Ürün Kartları was worse
    // than redundant: "Sepete Ekle" promises a cart, and this CMS has no orders, no
    // payment and no basket — a visitor clicking it reached nothing.
    //
    // What is left is the part that was actually useful in both: a responsive grid of
    // picture-title-text-link cards that means whatever the author types into it —
    // services, references, branches, team, downloads. Nothing here mentions articles
    // or money, so neither promise gets made by accident.
    const cardItem = (i, m) => `
        <li class="el-card" style="background:${SURFACE};border:1px solid ${BORDER};border-radius:12px;overflow:hidden;display:flex;flex-direction:column;">
            <img class="el-card-img" src="${ph(640, 360, 'Görsel')}" alt="" width="640" height="360" loading="${m.get('imageLoading') || 'lazy'}" style="width:100%;height:auto;display:${m.get('showImage') === 'false' ? 'none' : 'block'};">
            <div style="padding:20px;display:flex;flex-direction:column;gap:10px;flex:1;">
                <h3 style="margin:0;font-size:1.15rem;line-height:1.4;color:${DARK};">Kart Başlığı ${i + 1}</h3>
                <p style="margin:0;color:${MUTED};line-height:1.6;font-size:0.95rem;">Bu kartın kısa açıklaması. Ne sunduğunuzu bir iki cümleyle anlatın.</p>
                <a class="el-card-link" href="#" style="margin-top:auto;color:${BLUE};font-weight:600;text-decoration:none;font-size:0.95rem;display:${m.get('showLink') === 'false' ? 'none' : 'inline-block'};">Detaylar <span aria-hidden="true">&rarr;</span></a>
            </div>
        </li>`;

    // grid-template-columns has no Style Manager field, same as Mega Menu's panel and
    // Gallery's masonry — so the column count needs a trait or it cannot be changed
    // at all. auto-fit/minmax is what keeps it responsive without a media query: the
    // stated count is a maximum, and the row folds on its own when there is no room.
    const applyCardLayout = (m) => {
        const grid = findModels(m, '.el-card-grid')[0];
        if (!grid) return;
        const cols = clamp(parseInt(m.get('columns'), 10) || 3, 1, 4);
        grid.addStyle({
            display: 'grid',
            'grid-template-columns': `repeat(auto-fit, minmax(min(100%, ${Math.round(1100 / cols)}px), 1fr))`,
            gap: '24px'
        });
    };

    const applyCardParts = (m) => {
        const showImage = m.get('showImage') !== 'false';
        const showLink = m.get('showLink') !== 'false';
        findModels(m, '.el-card-img').forEach((img) => img.addStyle({ display: showImage ? 'block' : 'none' }));
        findModels(m, '.el-card-link').forEach((a) => a.addStyle({ display: showLink ? 'inline-block' : 'none' }));
    };

    smart({
        id: 'elevare-cards', label: 'Card Grid', icon: 'bi-grid',
        style: { padding: '80px 20px', 'background-color': BG },
        traits: [
            { type: 'number', name: 'cardCount', label: 'Kart Sayısı', min: 1, max: 12, value: 3, changeProp: 1 },
            { type: 'number', name: 'columns', label: 'Sütun Sayısı', min: 1, max: 4, value: 3, changeProp: 1 },
            { type: 'checkbox', name: 'showImage', label: 'Görsel Göster', valueTrue: 'true', valueFalse: 'false', value: 'true', changeProp: 1 },
            { type: 'checkbox', name: 'showLink', label: 'Bağlantı Göster', valueTrue: 'true', valueFalse: 'false', value: 'true', changeProp: 1 },
            { type: 'select', name: 'imageLoading', label: 'Görsel Yükleme (SEO)', options: [{ id: 'lazy', name: 'Lazy' }, { id: 'eager', name: 'Eager' }], value: 'lazy', changeProp: 1 }
        ],
        repeats: [{ trait: 'cardCount', container: '.el-card-grid', min: 1, max: 12, item: (m, i) => cardItem(i, m), after: (m) => applyCardParts(m) }],
        on: {
            columns: (m) => applyCardLayout(m),
            showImage: (m) => applyCardParts(m),
            showLink: (m) => applyCardParts(m),
            imageLoading: (m, v) => m.find('img').forEach((img) => img.addAttributes({ loading: v }))
        },
        onInit: (m) => applyCardLayout(m),
        template: (m) => {
            const cols = clamp(parseInt(m.get('columns'), 10) || 3, 1, 4);
            return `
            <div style="max-width:1200px;margin:0 auto;">
                <h2 style="text-align:center;font-size:2.5rem;font-weight:700;color:${DARK};margin-bottom:48px;">Bölüm Başlığı</h2>
                <ul class="el-card-grid" style="display:grid;grid-template-columns:repeat(auto-fit, minmax(min(100%, ${Math.round(1100 / cols)}px), 1fr));gap:24px;list-style:none;margin:0;padding:0;">${rep(m.get('cardCount'), (i) => cardItem(i, m))}</ul>
            </div>`;
        }
    });

    // ==================================================
    // 24. SPLIT CONTENT (CWV Optimized)
    // ==================================================
    smart({
        id: 'elevare-split', label: 'Image + Text', icon: 'bi-layout-split',
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [
            {
                type: 'select', name: 'reverse', label: 'Image Position', options: [
                    { id: 'row', name: 'Sol' }, { id: 'row-reverse', name: 'Sağ' }
                ], value: 'row', changeProp: 1
            },
            {
                type: 'select', name: 'imageLoading', label: 'Görsel Yükleme (SEO)', options: [
                    { id: 'lazy', name: 'Lazy (Sayfa Altı)' },
                    { id: 'eager', name: 'Eager (Hero/Üst)' }
                ], value: 'lazy', changeProp: 1
            }
        ],
        on: {
            reverse: (m, v) => { const t = m.find('.el-split')[0]; if (t) t.addStyle({ 'flex-direction': v }); },
            imageLoading: (m, v) => { const img = m.find('img')[0]; if (img) img.addAttributes({ loading: v }); }
        },
        template: (m) => `
            <div class="el-split" style="max-width:1100px;margin:0 auto;display:flex;flex-wrap:wrap;gap:48px;align-items:center;flex-direction:${m.get('reverse')};">
                <div style="flex:1;min-width:300px;">
                    <img src="${ph(720, 520, 'Görsel')}" alt="Özelliğin açıklayıcı görseli" width="720" height="520" loading="${m.get('imageLoading')}" style="width:100%;height:auto;border-radius:12px;display:block;">
                </div>
                <div style="flex:1;min-width:300px;">
                    <h2 style="font-size:2.2rem;font-weight:700;color:${DARK};margin:0 0 16px;">Ürününüzün veya Hizmetinizin Etkileyici Faydası</h2>
                    <p style="color:${MUTED};line-height:1.7;margin:0 0 24px;">Burada temel avantajları bir veya iki cümleyle açıklayın. Ziyaretçinin ilgisini çekecek vurucu kelimeler kullanın.</p>
                    <a href="#" style="display:inline-block;padding:12px 28px;background:${BLUE};color:${ON_PRIMARY};text-decoration:none;border-radius:6px;font-weight:600;">Hizmetleri İnceleyin</a>
                </div>
            </div>`
    });

    // ==================================================
    // 25. STEPS / HOW IT WORKS
    // ==================================================
    const stepItem = (i) => `
        <li style="display:flex;gap:20px;margin-bottom:28px;align-items:flex-start;">
            <span style="flex-shrink:0;width:44px;height:44px;border-radius:50%;background:${BLUE};color:${ON_PRIMARY};display:flex;align-items:center;justify-content:center;font-weight:700;font-size:1.1rem;" aria-hidden="true">${i + 1}</span>
            <div>
                <h3 style="margin:0 0 6px;font-size:1.2rem;color:${DARK};">Adım ${i + 1}</h3>
                <p style="margin:0;color:${MUTED};line-height:1.6;">Bu adımda sürecin nasıl ilerleyeceğini net ve anlaşılır bir şekilde tarif edin.</p>
            </div>
        </li>`;

    smart({
        id: 'elevare-steps', label: 'Steps / How It Works', icon: 'bi-list-ol',
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [{ type: 'number', name: 'stepCount', label: 'Step Count', min: 2, max: 6, value: 3, changeProp: 1 }],
        repeats: [{ trait: 'stepCount', container: '.el-steps-list', min: 2, max: 6, item: (m, i) => stepItem(i) }],
        template: (m) => `
            <div style="max-width:720px;margin:0 auto;">
                <h2 style="text-align:center;font-size:2.5rem;font-weight:700;color:${DARK};margin-bottom:48px;">Nasıl Çalışır?</h2>
                <ol class="el-steps-list" style="list-style:none;margin:0;padding:0;">${rep(m.get('stepCount'), stepItem)}</ol>
            </div>`
    });

    // ==================================================
    // 26. ICON CHECKLIST
    // ==================================================
    const checkItem = (i) => `
        <li style="display:flex;gap:12px;align-items:flex-start;margin-bottom:14px;">
            ${icon('check2-circle', `color:${GREEN};font-size:1.3rem;`)}
            <span style="color:${TEXT};line-height:1.6;">Dahil olan hizmet veya avantaj ${i + 1} buraya gelecek.</span>
        </li>`;

    smart({
        id: 'elevare-checklist', label: 'Checklist', icon: 'bi-check2-square',
        style: { padding: '80px 20px', 'background-color': BG },
        traits: [{ type: 'number', name: 'itemCount', label: 'Item Count', min: 2, max: 10, value: 4, changeProp: 1 }],
        repeats: [{ trait: 'itemCount', container: '.el-check-list', min: 2, max: 10, item: (m, i) => checkItem(i) }],
        template: (m) => `
            <div style="max-width:640px;margin:0 auto;">
                <h2 style="font-size:2rem;font-weight:700;color:${DARK};margin-bottom:28px;">Neler Dahil?</h2>
                <ul class="el-check-list" style="list-style:none;margin:0;padding:0;">${rep(m.get('itemCount'), checkItem)}</ul>
            </div>`
    });

    // ==================================================
    // 27. COMPARISON TABLE
    // ==================================================
    const tableRow = (i) => `
        <tr>
            <th scope="row" style="text-align:left;padding:12px 16px;border-bottom:1px solid ${BORDER};font-weight:600;color:${DARK};">Özellik ${i + 1}</th>
            <td style="padding:12px 16px;border-bottom:1px solid ${BORDER};text-align:center;color:${GREEN};">${icon('check-lg')}<span style="${SR}">Dahil</span></td>
            <td style="padding:12px 16px;border-bottom:1px solid ${BORDER};text-align:center;color:${MUTED};">&mdash;<span style="${SR}">Dahil değil</span></td>
        </tr>`;

    smart({
        id: 'elevare-table', label: 'Comparison Table', icon: 'bi-table',
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [{ type: 'number', name: 'rowCount', label: 'Row Count', min: 2, max: 10, value: 4, changeProp: 1 }],
        repeats: [{ trait: 'rowCount', container: '.el-table-body', min: 2, max: 10, item: (m, i) => tableRow(i) }],
        template: (m) => `
            <div style="max-width:800px;margin:0 auto;overflow-x:auto;">
                <h2 style="text-align:center;font-size:2.5rem;font-weight:700;color:${DARK};margin-bottom:40px;">Planları Karşılaştırın</h2>
                <table style="width:100%;border-collapse:collapse;background:${SURFACE};border:1px solid ${BORDER};border-radius:12px;">
                    <caption style="${SR}">Planlar arası özellik karşılaştırması</caption>
                    <thead>
                        <tr style="background:${BG};">
                            <th scope="col" style="text-align:left;padding:14px 16px;color:${DARK};">Özellikler</th>
                            <th scope="col" style="padding:14px 16px;color:${DARK};">Pro Plan</th>
                            <th scope="col" style="padding:14px 16px;color:${DARK};">Temel Plan</th>
                        </tr>
                    </thead>
                    <tbody class="el-table-body">${rep(m.get('rowCount'), tableRow)}</tbody>
                </table>
            </div>`
    });

    // ==================================================
    // ==================================================
    // 29. ANNOUNCEMENT BAR
    // ==================================================
    smart({
        id: 'elevare-announce', label: 'Announcement Bar', icon: 'bi-megaphone-fill',
        tag: 'div',
        attrs: { role: 'region', 'aria-label': 'Duyuru' },
        style: { position: 'relative', display: 'flex', 'align-items': 'center', 'justify-content': 'center', padding: '10px 48px', 'background-color': NAVY, color: '#ffffff', 'font-size': '0.95rem', 'text-align': 'center' },
        traits: [{
            type: 'select', name: 'theme', label: 'Theme', options: [
                { id: '#1e3a8a', name: 'Brand Blue' }, { id: '#111827', name: 'Dark' }, { id: '#16a34a', name: 'Green' }, { id: '#dc2626', name: 'Red' }
            ], value: '#1e3a8a', changeProp: 1
        }],
        on: { theme: (m, v) => m.addStyle({ 'background-color': v }) },
        script: announceScript,
        template: () => `
            <p style="margin:0;">&#127881; 500 TL üzeri alışverişlerde kargo bedava &mdash; <a href="#" style="color:#fff;font-weight:700;">Kampanyayı İncele</a></p>
            <button data-elevare-btn type="button" class="el-announce-close" aria-label="Duyuruyu gizle" data-gjs-draggable="false" style="position:absolute;right:12px;top:50%;transform:translateY(-50%);background:none;border:0;color:#fff;font-size:1rem;cursor:pointer;padding:6px;">${icon('x-lg')}</button>`
    });

    // ==================================================
    // 30. AUTHOR BIO BOX (CWV Optimized)
    // ==================================================
    smart({
        id: 'elevare-authorbio', label: 'Author Bio', icon: 'bi-person-badge',
        tag: 'aside',
        // Read by the Structured Data builder into the page's author (a Person with
        // job title, bio, portrait and profile links) — same pattern as the Article
        // block's hints. Bios placed before these existed are read by their shape.
        attrs: { 'data-elevare-author': '' },
        style: { padding: '40px 20px', 'background-color': WHITE },
        traits: [
            { type: 'select', name: 'imageLoading', label: 'Görsel Yükleme', options: [{ id: 'lazy', name: 'Lazy' }, { id: 'eager', name: 'Eager' }], value: 'lazy', changeProp: 1 }
        ],
        on: { imageLoading: (m, v) => { const img = m.find('img')[0]; if (img) img.addAttributes({ loading: v }); } },
        template: (m) => `
            <div style="max-width:760px;margin:0 auto;display:flex;gap:20px;align-items:flex-start;border:1px solid ${BORDER};border-radius:12px;padding:24px;background:${BG};flex-wrap:wrap;">
                <img data-elevare-author-image src="${ph(160, 160, 'Portre')}" alt="Yazarın portresi" width="80" height="80" loading="${m.get('imageLoading')}" style="width:80px;height:80px;border-radius:50%;object-fit:cover;flex-shrink:0;">
                <div style="flex:1;min-width:240px;">
                    <p style="margin:0 0 4px;font-size:0.8rem;text-transform:uppercase;letter-spacing:1px;color:${MUTED};">Yazar</p>
                    <h3 data-elevare-author-name style="margin:0 0 4px;font-size:1.2rem;color:${DARK};">Yazar Adı</h3>
                    <p data-elevare-author-role style="margin:0 0 10px;color:${MUTED};font-size:0.9rem;">Kıdemli Uzman, 10+ Yıllık Deneyim</p>
                    <p data-elevare-author-bio style="margin:0 0 12px;color:${TEXT2};line-height:1.6;">Yazarın bu konudaki uzmanlığını ve güvenilirliğini kanıtlayan bir iki cümlelik tanıtım metni.</p>
                    <div style="display:flex;gap:12px;">
                        <a href="#" aria-label="Yazarın LinkedIn Profili" style="color:${BLUE};">${icon('linkedin')}</a>
                        <a href="#" aria-label="Yazarın X Profili" style="color:${BLUE};">${icon('twitter-x')}</a>
                    </div>
                </div>
            </div>`
    });

    // ==================================================
    // 31. TABLE OF CONTENTS
    // ==================================================
    const tocItem = (i) => `<li><a href="#section-${i + 1}" style="color:${BLUE};text-decoration:none;">Bölüm Başlığı ${i + 1}</a></li>`;

    smart({
        id: 'elevare-toc', label: 'Table of Contents', icon: 'bi-list-nested',
        tag: 'div',
        style: { padding: '20px' },
        traits: [{ type: 'number', name: 'linkCount', label: 'Link Count', min: 2, max: 10, value: 4, changeProp: 1 }],
        repeats: [{ trait: 'linkCount', container: '.el-toc-list', min: 2, max: 10, item: (m, i) => tocItem(i) }],
        template: (m) => `
            <nav aria-label="İçindekiler" style="border:1px solid ${BORDER};border-radius:10px;padding:20px 24px;background:${BG};max-width:480px;margin:0 auto;">
                <h2 style="font-size:1.05rem;margin:0 0 12px;color:${DARK};">Bu Sayfada</h2>
                <ol class="el-toc-list" style="margin:0;padding-left:20px;line-height:2;">${rep(m.get('linkCount'), tocItem)}</ol>
            </nav>`
    });

    // ==================================================
    // 32. BLOCKQUOTE / PULLQUOTE
    // ==================================================
    smart({
        id: 'elevare-quote', label: 'Blockquote', icon: 'bi-quote',
        style: { padding: '60px 20px', 'background-color': WHITE },
        traits: [{ type: 'text', name: 'citeUrl', label: 'Cite URL (source)', value: '', changeProp: 1 }],
        on: {
            citeUrl: (m, v) => { const q = m.find('.el-quote-bq')[0]; if (q) q.addAttributes({ cite: v || '' }); }
        },
        template: () => `
            <figure style="max-width:700px;margin:0 auto;text-align:center;">
                ${icon('quote', `font-size:2.4rem;color:${BLUE};`)}
                <blockquote class="el-quote-bq" style="margin:0 0 16px;font-size:1.4rem;line-height:1.6;color:${DARK};font-style:italic;">
                    <p style="margin:0;">Başlamak için mükemmel olmayı beklemeyin, mükemmel olmak için başlayın.</p>
                </blockquote>
                <figcaption style="color:${MUTED};">&mdash; Yazar Adı, <cite style="font-style:normal;">Kaynak Adı</cite></figcaption>
            </figure>`
    });

    // ==================================================
    // 33. LOCAL BUSINESS INFO
    // ==================================================
    smart({
        id: 'elevare-localinfo', label: 'Local Business Info', icon: 'bi-shop',
        style: { padding: '80px 20px', 'background-color': BG },
        traits: [{ type: 'text', name: 'phone', label: 'Telefon', value: contactValue('phone', '+90 212 000 00 00'), changeProp: 1 }],
        on: {
            phone: (m, v) => {
                const a = m.find('.el-biz-tel')[0];
                if (a && v) { a.addAttributes({ href: 'tel:' + v.replace(/[^+\d]/g, '') }); a.components(v); }
            }
        },
        template: (m) => `
            <div style="max-width:640px;margin:0 auto;background:${SURFACE};border:1px solid ${BORDER};border-radius:12px;padding:32px;">
                <h2 style="margin:0 0 16px;font-size:1.6rem;color:${DARK};">${siteValue('siteName', 'İşletme Adı')}</h2>
                <address style="margin:0 0 8px;color:${TEXT2};line-height:1.6;font-style:normal;">
                    ${icon('geo-alt', `color:${BLUE};margin-right:8px;`)}
                    <span>${siteAddressLine('Örnek Sokak No:123, İstanbul, TR')}</span>
                </address>
                <p style="margin:0 0 8px;color:${TEXT2};">
                    ${icon('telephone', `color:${BLUE};margin-right:8px;`)}
                    <a class="el-biz-tel" href="tel:${m.get('phone').replace(/[^+\d]/g, '')}" style="color:${BLUE};text-decoration:none;">${m.get('phone')}</a>
                </p>
                <p style="margin:0 0 16px;color:${TEXT2};">
                    ${icon('envelope', `color:${BLUE};margin-right:8px;`)}
                    <a href="mailto:${contactValue('email', 'info@example.com')}" style="color:${BLUE};text-decoration:none;">${contactValue('email', 'info@example.com')}</a>
                </p>
                <h3 style="margin:0 0 8px;font-size:1.05rem;color:${DARK};">Çalışma Saatleri</h3>
                <ul style="list-style:none;margin:0;padding:0;color:${TEXT2};line-height:1.9;">
                    ${siteValue('workingHours', '')
                ? `<li>${siteValue('workingHours', '')}</li>`
                : `<li>Pazartesi &ndash; Cuma: 09:00 &ndash; 18:00</li>
                    <li>Cumartesi: 10:00 &ndash; 14:00</li>
                    <li>Pazar: Kapalı</li>`}
                </ul>
            </div>`
    });

    // ==================================================
    // 34. APP DOWNLOAD BUTTONS
    // ==================================================
    smart({
        id: 'elevare-appbtns', label: 'App Buttons', icon: 'bi-phone-vibrate',
        style: { padding: '60px 20px', 'background-color': WHITE },
        template: () => `
            <div style="max-width:700px;margin:0 auto;text-align:center;">
                <h2 style="font-size:2rem;font-weight:700;color:${DARK};margin:0 0 12px;">Uygulamayı İndirin</h2>
                <p style="color:${MUTED};margin:0 0 28px;">iOS ve Android için kullanılabilir.</p>
                <ul style="display:flex;gap:16px;justify-content:center;flex-wrap:wrap;list-style:none;margin:0;padding:0;">
                    <li><a href="#" aria-label="App Store'dan İndirin" style="display:inline-flex;align-items:center;gap:12px;background:#111827;color:#fff;text-decoration:none;padding:12px 22px;border-radius:10px;min-width:190px;">
                        ${icon('apple', 'font-size:1.8rem;')}
                        <span style="text-align:left;line-height:1.2;"><span style="display:block;font-size:0.7rem;opacity:.8;">App Store'dan</span><strong style="font-size:1.1rem;">İndirin</strong></span>
                    </a></li>
                    <li><a href="#" aria-label="Google Play'den Alın" style="display:inline-flex;align-items:center;gap:12px;background:#111827;color:#fff;text-decoration:none;padding:12px 22px;border-radius:10px;min-width:190px;">
                        ${icon('google-play', 'font-size:1.6rem;')}
                        <span style="text-align:left;line-height:1.2;"><span style="display:block;font-size:0.7rem;opacity:.8;">Google Play'den</span><strong style="font-size:1.1rem;">Alın</strong></span>
                    </a></li>
                </ul>
            </div>`
    });

    // ==================================================
    // 35. SOCIAL LINKS ROW
    // ==================================================
    // `setting` names the Site Settings entry each icon links to, so a profile that
    // is already filled in on the Contact & Social screen arrives as a working link
    // rather than an icon pointing at "#" that nobody remembers to fix.
    const SOCIALS = [
        { key: 'facebook', cls: 'el-soc-fb', icon: 'facebook', label: 'Facebook', setting: 'facebook' },
        { key: 'twitter', cls: 'el-soc-x', icon: 'twitter-x', label: 'X (Twitter)', setting: 'x' },
        { key: 'instagram', cls: 'el-soc-ig', icon: 'instagram', label: 'Instagram', setting: 'instagram' },
        { key: 'linkedin', cls: 'el-soc-li', icon: 'linkedin', label: 'LinkedIn', setting: 'linkedIn' },
        { key: 'youtube', cls: 'el-soc-yt', icon: 'youtube', label: 'YouTube', setting: 'youtube' }
    ];
    const socialHref = (s) => contactValue(s.setting, '#');
    const setHref = (m, sel, v) => { const a = m.find(sel)[0]; if (a && v != null) a.addAttributes({ href: v }); };

    smart({
        id: 'elevare-social', label: 'Social Links', icon: 'bi-share',
        // <nav>, not a bare <div>: a row of profile links IS navigation, and the
        // label is what tells a screen reader this nav apart from the site's main
        // one (SeoAnalyzer's own navAria check flags unlabelled sibling navs).
        tag: 'nav',
        attrs: { 'aria-label': 'Sosyal medya' },
        style: { padding: '40px 20px', 'background-color': WHITE },
        traits: SOCIALS.map((s) => ({ type: 'text', name: s.key, label: s.label + ' URL', value: socialHref(s), changeProp: 1 })),
        on: SOCIALS.reduce((acc, s) => { acc[s.key] = (m, v) => setHref(m, '.' + s.cls, v); return acc; }, {}),
        template: () => `
            <ul style="display:flex;gap:14px;justify-content:center;flex-wrap:wrap;list-style:none;margin:0;padding:0;">
                ${SOCIALS.map((s) => `<li><a class="${s.cls}" href="${socialHref(s)}" aria-label="${s.label}" style="width:44px;height:44px;display:inline-flex;align-items:center;justify-content:center;border-radius:50%;background:${SURFACE2};color:${DARK};font-size:1.2rem;text-decoration:none;">${icon(s.icon)}</a></li>`).join('')}
            </ul>`
    });

    // ==================================================
    // 36. ALERT / NOTICE
    // ==================================================
    // One builder for both the initial template and the type swap, so the replaced
    // element cannot drift from the one the block shipped with (same class, same
    // sizing) — swapIcon finds it again by that very class.
    const alertIcon = (name) =>
        `<svg class="el-alert-icon" viewBox="0 0 16 16" width="19" height="19" fill="currentColor" aria-hidden="true" ` +
        `style="width:1.2rem;height:1.2rem;flex-shrink:0;margin-top:2px;">${ICONS[name] || ''}</svg>`;

    const ALERTS = {
        info: { bg: '#eff6ff', bd: '#bfdbfe', fg: '#1e40af', ic: 'info-circle-fill' },
        success: { bg: '#f0fdf4', bd: '#bbf7d0', fg: '#166534', ic: 'check-circle-fill' },
        warning: { bg: '#fffbeb', bd: '#fde68a', fg: '#92400e', ic: 'exclamation-triangle-fill' },
        danger: { bg: '#fef2f2', bd: '#fecaca', fg: '#991b1b', ic: 'x-circle-fill' }
    };

    smart({
        id: 'elevare-alert', label: 'Alert / Notice', icon: 'bi-exclamation-circle',
        tag: 'div',
        attrs: { role: 'alert' },
        style: { display: 'flex', gap: '12px', 'align-items': 'flex-start', 'max-width': '760px', margin: '24px auto', padding: '16px 20px', 'background-color': '#eff6ff', border: '1px solid #bfdbfe', 'border-radius': '10px', color: '#1e40af' },
        traits: [{
            type: 'select', name: 'alertType', label: 'Type', options: [
                { id: 'info', name: 'Bilgi' }, { id: 'success', name: 'Başarı' },
                { id: 'warning', name: 'Uyarı' }, { id: 'danger', name: 'Hata' }
            ], value: 'info', changeProp: 1
        }],
        on: {
            // The glyph swaps by replacing the whole <svg> rather than toggling a
            // class — there is no icon font anymore to pick a different glyph out
            // of, just whichever markup is inside. See swapIcon for why the element
            // is replaced instead of its children being rewritten.
            alertType: (m, v) => {
                const c = ALERTS[v] || ALERTS.info;
                m.addStyle({ 'background-color': c.bg, border: '1px solid ' + c.bd, color: c.fg });
                swapIcon(m.find('.el-alert-icon')[0], alertIcon(c.ic));
            }
        },
        template: () => `
            ${alertIcon('info-circle-fill')}
            <div>
                <p style="margin:0 0 4px;font-weight:700;">Önemli Duyuru</p>
                <p style="margin:0;line-height:1.6;">Ziyaretçiniz için önemli olan bilgi metni buraya girilmelidir. Kısa ve aksiyon alınabilir olmasına özen gösterin.</p>
            </div>`
    });

    // ==================================================
    // 37. SPACER / DIVIDER
    // ==================================================
    smart({
        id: 'elevare-spacer', label: 'Spacer / Divider', icon: 'bi-distribute-vertical',
        tag: 'div',
        style: { height: '60px', 'border-top': 'none' },
        traits: [
            { type: 'number', name: 'spaceHeight', label: 'Height (px)', min: 8, max: 240, value: 60, changeProp: 1 },
            { type: 'select', name: 'lineStyle', label: 'Divider Line', options: [{ id: 'none', name: 'None' }, { id: 'line', name: 'Line' }], value: 'none', changeProp: 1 }
        ],
        on: {
            spaceHeight: (m, v) => { const h = parseInt(v, 10); if (!isNaN(h)) m.addStyle({ height: clamp(h, 8, 240) + 'px' }); },
            lineStyle: (m, v) => m.addStyle({ 'border-top': v === 'line' ? '1px solid ' + BORDER : 'none' })
        },
        template: () => ''
    });

    // ==================================================
    // 37b. BUTTON — a real <button>, icon optional
    // ==================================================
    // Was "İkonlu Buton", which always drew an icon and only offered ten of them.
    // The icon is now opt-in ("Yok" is the default), the list is far longer, and it
    // can sit on either side of the label — so this one block covers a plain text
    // button, an icon-only button and everything between, instead of being a
    // special case next to the plain buttons the other blocks hardcode.
    //
    // The label lives in its own <span> and the icon in its own <svg> sibling, which
    // is what lets the order be swapped by moving one node rather than re-rendering,
    // and what keeps the label double-click editable (a bare text node next to an
    // <svg> is not — see the icon() docs).
    const BUTTON_ICONS = [
        { id: '', name: 'Yok' },
        { id: 'arrow-right', name: 'Sağ ok' }, { id: 'arrow-left', name: 'Sol ok' },
        { id: 'chevron-right', name: 'İnce sağ ok' }, { id: 'chevron-down', name: 'Aşağı ok' },
        { id: 'box-arrow-up-right', name: 'Dış bağlantı' }, { id: 'download', name: 'İndir' },
        { id: 'send', name: 'Gönder' }, { id: 'envelope', name: 'E-posta' },
        { id: 'telephone', name: 'Telefon' }, { id: 'whatsapp', name: 'WhatsApp' },
        { id: 'chat-dots', name: 'Sohbet' }, { id: 'calendar-event', name: 'Takvim' },
        { id: 'clock', name: 'Saat' }, { id: 'geo-alt', name: 'Konum' },
        { id: 'search', name: 'Arama' }, { id: 'cart3', name: 'Sepet' },
        { id: 'heart', name: 'Kalp' }, { id: 'star-fill', name: 'Yıldız' },
        { id: 'share', name: 'Paylaş' }, { id: 'printer', name: 'Yazdır' },
        { id: 'person', name: 'Kullanıcı' }, { id: 'lock', name: 'Kilit' },
        { id: 'gear', name: 'Ayarlar' }, { id: 'plus-lg', name: 'Artı' },
        { id: 'check2', name: 'Onay' }, { id: 'play-fill', name: 'Oynat' },
        { id: 'bell', name: 'Bildirim' }, { id: 'bookmark', name: 'Yer imi' },
        { id: 'camera', name: 'Kamera' }, { id: 'book', name: 'Kitap' },
        { id: 'briefcase', name: 'Çanta' }, { id: 'truck', name: 'Kargo' },
        { id: 'trophy', name: 'Ödül' }, { id: 'eye', name: 'Göz' },
        { id: 'pencil', name: 'Kalem' }, { id: 'rocket', name: 'Roket' },
        { id: 'lightning-charge', name: 'Şimşek' }, { id: 'globe', name: 'Dünya' }
    ];

    // Icon and label are rebuilt together rather than patched in place: the icon can
    // appear, disappear or change sides, and rebuilding both is simpler to reason
    // about than four separate mutations — and it is why the label is read back off
    // the DOM first, so text the author typed on the canvas survives the rebuild.
    // Rebuilds a button's insides as an icon and a label that are SEPARATE
    // components — never one text component containing an <svg>. That distinction is
    // the whole point: `data-gjs-type="text"` on a container makes its children part
    // of one editable string, so the icon inside stops being a component you can
    // select, and there is then no way to delete it at all. Keeping them siblings
    // means the label is double-click editable AND the icon can simply be selected
    // and removed like anything else.
    //
    // `host` is the element the content belongs to, which is not always the block
    // itself — the Mega Menu's trigger is a button nested inside its block.
    const renderButtonContent = (host, label, iconName, position) => {
        if (!host) return;
        const glyph = iconName && ICONS[iconName] ? icon(iconName) : '';
        const text = `<span class="el-btn-label">${label}</span>`;
        host.components(position === 'right' ? text + glyph : glyph + text);
        ensureAccessibleName(host, label);
    };

    // Text a component would read as, from the model — the canvas element may not
    // exist yet when this runs (init of a restored page).
    const modelText = (comp) => {
        if (!comp) return '';
        if (comp.get('type') === 'textnode') return comp.get('content') || '';
        return comp.components().map(modelText).join('');
    };

    // A button that is only an icon has no accessible name: a screen reader says
    // "button" and nothing else, and Lighthouse fails the page for it. It happens
    // easily here — clear the label, keep the glyph — so the name is supplied from
    // the label the author last had (or the block's default) as aria-label, and
    // taken away again the moment there is visible text, which is the better name.
    // Marked so an aria-label the author set by hand (the Aria Label trait) is
    // never touched.
    const ensureAccessibleName = (host, fallbackName) => {
        if (!host) return;
        const attrs = host.getAttributes();
        const visible = modelText(host).trim();
        const auto = attrs['data-elevare-auto-label'] !== undefined;
        if (visible) {
            if (auto) host.removeAttributes(['aria-label', 'data-elevare-auto-label']);
            return;
        }
        if (attrs['aria-label'] && !auto) return;
        const name = (fallbackName || '').trim();
        if (name) host.addAttributes({ 'aria-label': name, 'data-elevare-auto-label': '' });
    };

    // The first descendant carrying a class, found through the MODEL. Component
    // .find() searches the view's DOM, and at init of a restored page there is no
    // view yet: it returns nothing, silently. That is how the Üst Menü's icon-only
    // trigger stayed without a name on the live site after the check above was
    // added — onInit asked find() for it and got an empty list.
    const childWithClass = (comp, cls) => {
        if (!comp) return null;
        if (comp.getClasses && comp.getClasses().includes(cls)) return comp;
        const kids = comp.components ? comp.components() : null;
        if (!kids) return null;
        for (let i = 0; i < kids.length; i++) {
            const hit = childWithClass(kids.at(i), cls);
            if (hit) return hit;
        }
        return null;
    };

    // Reads the label back off the DOM first so text typed on the canvas survives a
    // rebuild triggered by changing the icon.
    const liveLabel = (host, fallback) => {
        const span = host && host.find ? host.find('.el-btn-label')[0] : null;
        const live = span && span.getEl && span.getEl() ? span.getEl().textContent : null;
        return (live != null && live.trim().length) ? live : fallback;
    };

    const applyButtonContent = (m) => renderButtonContent(
        m, liveLabel(m, m.get('btnLabel') || 'Buton'), m.get('iconName') || '', m.get('iconPosition'));

    // A button that goes somewhere is a link. The block used to be a <button> and
    // nothing else, with no field for an address at all — the one thing most
    // people drop a button for. Giving a <button> a JavaScript redirect would have
    // "worked" and been wrong in every way that matters: search engines do not
    // follow it, middle-click and "open in new tab" do nothing, a screen reader
    // announces a button, not a destination. So the tag follows the link: an
    // address set here makes the element an <a href>, cleared again it is a
    // <button> (the right thing for a pop-up or side-panel trigger). The look is
    // identical either way — same id, same rule.
    //
    // The swap is a replaceWith of the block's own JSON with the tag changed. A
    // view is created around its tag and does not re-render on `tagName`; the
    // JSON route keeps every prop (label, icon, the link fields themselves), the
    // children and the id, so the styles already on the page stay attached.
    const BUTTON_LINK_CATEGORY = { id: 'link', label: 'Bağlantı' };
    const applyButtonLink = (m) => {
        const url = String(m.get('linkUrl') || '').trim();
        const href = url || sitePageHref(m.get('linkPage'));
        const newTab = m.get('linkTarget') === '_blank';
        const wantTag = href ? 'a' : 'button';
        const attrs = { ...m.getAttributes() };
        delete attrs.class;
        ['href', 'target', 'rel', 'type', 'data-elevare-btn'].forEach((k) => { delete attrs[k]; });
        if (href) {
            attrs.href = href;
            if (newTab) { attrs.target = '_blank'; attrs.rel = 'noopener noreferrer'; }
        } else {
            attrs.type = 'button';
            attrs['data-elevare-btn'] = '';
        }
        if (m.get('tagName') === wantTag) {
            m.removeAttributes(['href', 'target', 'rel', 'type', 'data-elevare-btn']);
            m.addAttributes(attrs);
            return;
        }
        // An <a> would otherwise pick up the page's link underline; the block's
        // own rule (an old one predates this line) says nothing about it.
        if (wantTag === 'a') m.addStyle({ 'text-decoration': 'none' });
        // Through JSON.stringify on purpose: toJSON() hands back `components` as a
        // live collection, and given that object GrapesJS builds one empty <div>
        // in place of the children (measured). Serialising turns it into plain
        // child JSON, which it reads correctly.
        const json = JSON.parse(JSON.stringify(m.toJSON()));
        json.tagName = wantTag;
        json.attributes = attrs;
        const oldId = m.getId();
        const wasSelected = editor.getSelected() === m;
        // Removing a component takes its #id rules with it (keepUnusedStyles is
        // off) — every one of them, the hover state and the tablet/mobile
        // overrides included. Measured: the new element came back display:inline,
        // rule gone. They are captured here and put back on the new element.
        const rules = editor.Css.getRules('#' + oldId).map((r) => ({
            // The full selector, ":hover" and all — setRule reads the state from
            // the selector string, not from an option.
            selector: r.selectorsToString(),
            style: { ...r.getStyle() },
            atRuleType: r.get('atRuleType') || '',
            atRuleParams: r.get('mediaText') || ''
        }));
        let added;
        if (typeof m.replaceWith === 'function') {
            const r = m.replaceWith(json);
            added = Array.isArray(r) ? r[0] : r;
        } else {
            const parent = m.parent();
            const at = parent.components().indexOf(m);
            m.remove();
            added = parent.append(json, { at })[0];
        }
        if (!added) return;
        // The type's default attributes are merged back in on creation, so the
        // ones that make sense only on a <button> have to go again; and the id has
        // to be the old one, or the rule that styles it no longer matches.
        if (wantTag === 'a') added.removeAttributes(['type', 'data-elevare-btn']);
        if (added.getId() !== oldId && typeof added.setId === 'function') added.setId(oldId);
        rules.forEach((r) => {
            const opts = r.atRuleType ? { atRuleType: r.atRuleType, atRuleParams: r.atRuleParams } : {};
            editor.Css.setRule(r.selector, r.style, opts);
        });
        if (wasSelected) editor.select(added);
    };

    smart({
        id: 'elevare-icon-button', label: 'Button', icon: 'bi-hand-index',
        tag: 'button',
        attrs: { type: 'button', 'data-elevare-btn': '' },
        style: {
            display: 'inline-flex', 'align-items': 'center', gap: '8px',
            padding: '10px 18px', 'border-radius': '8px', border: '1px solid ' + BORDER,
            background: WHITE, color: DARK, cursor: 'pointer', 'font-size': '0.95rem', 'font-weight': '600',
            'text-decoration': 'none'
        },
        traits: [
            { type: 'text', name: 'btnLabel', label: 'Buton Yazısı', value: 'Buton', changeProp: 1 },
            { type: 'select', name: 'iconName', label: 'İkon', value: '', changeProp: 1, options: BUTTON_ICONS },
            {
                type: 'select', name: 'iconPosition', label: 'İkon Konumu', value: 'left', changeProp: 1,
                options: [{ id: 'left', name: 'Solda' }, { id: 'right', name: 'Sağda' }]
            },
            { type: PAGE_TRAIT, name: 'linkPage', label: 'Site Sayfası', category: BUTTON_LINK_CATEGORY, value: '', changeProp: 1 },
            { type: 'text', name: 'linkUrl', label: 'Adres (URL)', category: BUTTON_LINK_CATEGORY, value: '', changeProp: 1, placeholder: 'https://… tel:… mailto:… — sayfa seçiminin yerine geçer' },
            {
                type: 'select', name: 'linkTarget', label: 'Hedef', category: BUTTON_LINK_CATEGORY, value: '', changeProp: 1,
                options: [{ id: '', name: 'Aynı sekme' }, { id: '_blank', name: 'Yeni sekme' }]
            }
        ],
        // Restored pages too: a label cleared on the canvas never fires a trait
        // change, so the name is checked on every init.
        onInit: (m) => ensureAccessibleName(m, m.get('btnLabel') || 'Buton'),
        on: {
            iconName: (m) => applyButtonContent(m),
            iconPosition: (m) => applyButtonContent(m),
            linkPage: (m) => applyButtonLink(m),
            linkUrl: (m) => applyButtonLink(m),
            linkTarget: (m) => applyButtonLink(m),
            // The label trait writes only the label, so an icon already chosen is not
            // dropped when someone renames the button.
            btnLabel: (m, v) => {
                const span = m.find('.el-btn-label')[0];
                if (span) span.components(v || 'Buton'); else applyButtonContent(m);
                ensureAccessibleName(m, v || 'Buton');
            }
        },
        template: (m) => {
            const name = m.get('iconName') || '';
            const glyph = name && ICONS[name] ? icon(name) : '';
            const text = `<span class="el-btn-label">${m.get('btnLabel') || 'Buton'}</span>`;
            return m.get('iconPosition') === 'right' ? text + glyph : glyph + text;
        }
    });

    // ==================================================
    // 37c. THEME SWITCH — dark/light toggle, no icon-font dependency
    // ==================================================
    // Bootstrap Icons only ever exists on the public site because Site Codes
    // loads it there — a public-facing block that needed it would silently
    // break the moment that row is disabled or removed. The two glyphs here
    // are inline SVG instead: no external stylesheet, no font request, crisp
    // at any zoom, and colored by `currentColor` so they already follow
    // whatever `color` the button resolves to (DARK, i.e. --elevare-heading).
    smart({
        id: 'elevare-theme-switch', label: 'Theme Switch', icon: 'bi-circle-half',
        tag: 'button',
        attrs: {
            type: 'button', 'data-elevare-btn': '', class: 'elevare-theme-switch',
            'aria-label': 'Karanlık / aydınlık tema'
        },
        style: {
            display: 'inline-flex', 'align-items': 'center', 'justify-content': 'center',
            width: '40px', height: '40px', padding: '0', flex: '0 0 auto',
            'border-radius': '8px', border: '1px solid ' + BORDER,
            background: WHITE, color: DARK, cursor: 'pointer'
        },
        script: themeSwitchScript,
        template: () => `
            <svg class="elevare-theme-switch-moon" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79Z"></path></svg>
            <svg class="elevare-theme-switch-sun" viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="4"></circle><path d="M12 2v2M12 20v2M4.22 4.22l1.42 1.42M18.36 18.36l1.42 1.42M2 12h2M20 12h2M4.22 19.78l1.42-1.42M18.36 5.64l1.42-1.42"></path></svg>`
    });

    // Attribute-selector rule (not a per-component style): which of the two
    // glyphs is visible depends on the <html> element's own data-theme, set by
    // the "Tema Başlatma" Site Codes script on load and flipped by the click
    // handler above — matching class name, matching mechanism, everywhere it
    // is used on the site.
    blockCss(
        '.elevare-theme-switch-sun { display: none; }' +
        '[data-theme="dark"] .elevare-theme-switch-moon { display: none; }' +
        '[data-theme="dark"] .elevare-theme-switch-sun { display: inline-block; }'
    );

    // ==================================================
    // 38. VIDEO FACADE (SEO Friendly <img> tags)
    // ==================================================
    smart({
        id: 'elevare-videofacade', label: 'Video (Lazy Facade)', icon: 'bi-play-btn',
        // Empty rather than a sample video id — see the Video Embed block above for
        // why. Here it can genuinely be empty: nothing is embedded until the play
        // button is pressed, and videoFacadeScript already returns early with no
        // data-video, so an unedited block simply does nothing instead of playing
        // someone else's video on a customer's site.
        attrs: { 'data-video': '', 'data-video-title': 'Tanıtım videosu' },
        style: { padding: '80px 20px', 'background-color': WHITE },
        traits: [
            { type: 'text', name: 'data-video', label: 'Embed URL' },
            { type: 'text', name: 'data-video-title', label: 'Video Başlığı (SEO/A11y)' },
            { type: 'text', name: 'thumbUrl', label: 'Kapak Görseli URL', value: ph(800, 450, 'Kapak görseli'), changeProp: 1 }
        ],
        on: {
            thumbUrl: (m, v) => { const img = m.find('.el-vf-thumb')[0]; if (img && v) img.addAttributes({ src: v }); }
        },
        script: videoFacadeScript,
        template: (m) => `
            <div style="max-width:800px;margin:0 auto;">
                <div class="el-vf-box" style="position:relative;padding-bottom:56.25%;height:0;overflow:hidden;border-radius:10px;background:#000;">
                    <img class="el-vf-thumb" src="${m.get('thumbUrl')}" alt="Video kapak resmi" loading="lazy" style="position:absolute;top:0;left:0;width:100%;height:100%;object-fit:cover;z-index:1;">
                    <button data-elevare-btn type="button" class="el-vf-play" aria-label="Videoyu oynat" data-gjs-draggable="false" style="position:absolute;top:50%;left:50%;transform:translate(-50%,-50%);width:72px;height:72px;border-radius:50%;border:0;background:${BLUE};color:${ON_PRIMARY};font-size:1.8rem;cursor:pointer;display:flex;align-items:center;justify-content:center;z-index:2;">${icon('play-fill')}</button>
                </div>
            </div>`
    });

    // ==================================================
    // ==================================================
    // 40. POP-UP
    // ==================================================
    const popupScript = function () {
        var root = this;
        function inCanvas() {
            try { var fe = window.frameElement; return !!(fe && (fe.className || '').indexOf('gjs-') > -1); }
            catch (e) { return false; }
        }
        // The canvas hides and places it itself (the overlay sheet, see the
        // section above smart()); a visitor's click is what shows it there.
        if (!inCanvas()) root.style.display = 'none';
    };

    smart({
        id: 'elevare-popup', label: 'Pop-up', icon: 'bi-window-stack',
        tag: 'div', name: 'Pop-up',
        attrs: { 'data-elevare-popup-id': 'popup-1' },
        style: { position: 'fixed', inset: '0', 'z-index': '9999', 'align-items': 'center', 'justify-content': 'center', background: 'rgba(0,0,0,.5)' },
        traits: [
            { type: 'text', name: 'data-elevare-popup-id', label: 'Pop-up ID' },
            { type: 'checkbox', name: 'showClose', label: 'Kapatma Butonu Göster', value: true, changeProp: 1 }
        ],
        on: {
            showClose: (m, v) => { const btn = m.find('.elevare-popup-close')[0]; if (btn) btn.addStyle({ display: v ? 'flex' : 'none' }); }
        },
        script: popupScript,
        // In the canvas it opens in the page flow, where its Layers entry sits,
        // rather than fixed over everything — editable without covering the canvas.
        overlay: { self: true, display: 'flex', canvasCss: 'position:relative !important;inset:auto !important;z-index:auto !important;' },
        template: () => `
            <div class="elevare-popup-box" style="background:${SURFACE};border-radius:10px;padding:32px;max-width:480px;width:90%;position:relative;">
                <button data-elevare-btn type="button" class="elevare-popup-close" data-elevare-popup-close aria-label="Kapat" style="position:absolute;top:12px;right:12px;width:32px;height:32px;border-radius:50%;border:0;background:${SURFACE2};cursor:pointer;font-size:1.2rem;line-height:1;display:flex;align-items:center;justify-content:center;">&times;</button>
                <h3 style="margin-top:0;">Pop-up Başlığı</h3>
                <p>Bu içeriği tamamen özelleştirebilirsiniz — resim, buton, form ekleyebilirsiniz.</p>
            </div>`
    });

    // ==================================================
    // 41. MULTI-STEP FORM
    // ==================================================
    const multiStepFormScript = function () {
        var root = this;
        function inCanvas() {
            try { var fe = window.frameElement; return !!(fe && (fe.className || '').indexOf('gjs-') > -1); }
            catch (e) { return false; }
        }
        if (!inCanvas()) return;
        var steps = root.querySelectorAll('[data-elevare-step]');
        for (var i = 0; i < steps.length; i++) steps[i].style.display = 'flex';
    };

    // One step of the form. Every step carries the same three controls and
    // syncStepNav decides which of them this step keeps, so a step never has to
    // know whether it is the first, the last or one in between: the author sets
    // the count and the "Adım 2 / 4" heading, the Geri/İleri pair and the one
    // Gönder button all follow. Steps after the first start hidden; the live
    // runtime (elevare-interactions.js) walks them, and multiStepFormScript
    // shows them all in the canvas so each can be edited.
    const stepField = (label, name, type) => `
        <div style="display:flex;flex-direction:column;gap:6px;">
            <label style="font-weight:600;font-size:0.9rem;">${label}</label>
            <input type="${type}" name="${name}" style="padding:12px;border:1px solid ${BORDER};border-radius:6px;background:${SURFACE};color:${TEXT};">
        </div>`;
    const stepPrevBtn = () => `<button data-elevare-btn type="button" data-elevare-step-prev style="background:${SURFACE2};color:${DARK};padding:12px 24px;border:none;border-radius:6px;font-weight:600;cursor:pointer;">Geri</button>`;
    const stepNextBtn = () => `<button data-elevare-btn type="button" data-elevare-step-next style="background:${BLUE};color:${ON_PRIMARY};padding:12px 24px;border:none;border-radius:6px;font-weight:600;cursor:pointer;">İleri</button>`;
    const stepSubmitBtn = () => `<button data-elevare-btn type="submit" style="background:${BLUE};color:${ON_PRIMARY};padding:12px 24px;border:none;border-radius:6px;font-weight:600;cursor:pointer;">Gönder</button>`;
    const formStep = (i, n) => {
        const fields = i === 0
            ? stepField('Ad Soyad', 'name', 'text') + stepField('Telefon', 'phone', 'tel')
            : i === n - 1
                ? stepField('E-posta', 'email', 'email') + `
        <div style="display:flex;flex-direction:column;gap:6px;">
            <label style="font-weight:600;font-size:0.9rem;">Mesaj</label>
            <textarea name="message" rows="4" style="padding:12px;border:1px solid ${BORDER};border-radius:6px;background:${SURFACE};color:${TEXT};"></textarea>
        </div>`
                : stepField(`Alan ${i + 1}`, `field${i + 1}`, 'text');
        return `
            <div data-elevare-step="${i + 1}" style="display:${i === 0 ? 'flex' : 'none'};flex-direction:column;gap:16px;">
                <h3 data-elevare-step-title style="margin:0;">Adım ${i + 1} / ${n}</h3>${fields}
                <div data-elevare-step-nav style="display:flex;gap:10px;">${i > 0 ? stepPrevBtn() : ''}${i < n - 1 ? stepNextBtn() : stepSubmitBtn()}</div>
            </div>`;
    };
    function syncStepNav(m) {
        const steps = m.find('[data-elevare-step]');
        const n = steps.length;
        steps.forEach((step, i) => {
            step.addAttributes({ 'data-elevare-step': String(i + 1) });
            const title = step.find('[data-elevare-step-title]')[0] || step.find('h3')[0];
            if (title) title.components(`Adım ${i + 1} / ${n}`);
            // Pages saved before the nav row existed: the buttons sit in a plain
            // last <div>, or (first step) straight in the step.
            const last = step.components().last();
            const nav = step.find('[data-elevare-step-nav]')[0]
                || (last && last.get('tagName') === 'div' && last.find('button').length ? last : null)
                || (step.find('[data-elevare-step-next],[data-elevare-step-prev]').length ? step : null);
            if (!nav) return;
            const want = { prev: i > 0, next: i < n - 1, submit: i === n - 1 };
            const has = {
                prev: nav.find('[data-elevare-step-prev]')[0],
                next: nav.find('[data-elevare-step-next]')[0],
                submit: nav.find('[type="submit"]')[0]
            };
            // Removals first so the nav row ends as Geri · İleri or Geri · Gönder.
            ['prev', 'next', 'submit'].forEach((k) => { if (has[k] && !want[k]) has[k].remove(); });
            if (want.prev && !has.prev) nav.components().add(stepPrevBtn(), nav === step ? {} : { at: 0 });
            if (want.next && !has.next) nav.append(stepNextBtn());
            if (want.submit && !has.submit) nav.append(stepSubmitBtn());
            // A step added in the canvas is hidden by its own style, like the ones
            // that came with the block; show it the same DOM-only way the script does.
            const el = step.getEl();
            if (el) el.style.display = 'flex';
        });
    }

    smart({
        id: 'elevare-multistep-form', label: 'Multi-Step Form', icon: 'bi-list-ol',
        style: { padding: '80px 20px', 'background-color': WHITE },
        script: multiStepFormScript,
        traits: [{ type: 'number', name: 'stepCount', label: 'Adım Sayısı', min: 2, max: 6, value: 2, changeProp: 1 }],
        // n = 0: a step added later is a plain middle step (one generic field);
        // syncStepNav then gives it the right heading and buttons.
        repeats: [{ trait: 'stepCount', container: 'form', min: 2, max: 6, item: (m, i) => formStep(i, 0), after: syncStepNav }],
        template: (m) => `
            <div style="max-width:600px;margin:0 auto;">
                <form data-elevare-managed-form data-elevare-form-name="Çok Adımlı Form" style="display:flex;flex-direction:column;gap:16px;">${rep(m.get('stepCount'), (i) => formStep(i, m.get('stepCount')))}
                </form>
            </div>`
    });

    // ==================================================
    // 42. LANGUAGE SWITCHER (Added hreflang support)
    // ==================================================
    // The flag comes from the icon picked per language on the Diller screen, and is
    // only ever decorative here: the language's own code sits right next to it in
    // the default mode, and in flag-only mode the anchor still carries a
    // screen-reader label, because a flag is a country and a language is not — a
    // reader who cannot see the image must still be told which language this is.
    const langLink = (l, mode) => {
        const showFlag = mode !== 'code' && !!l.flagUrl;
        const showCode = mode !== 'flag' || !l.flagUrl;
        const flag = showFlag
            ? `<img src="${l.flagUrl}" alt="" width="20" height="14" loading="lazy" style="width:20px;height:14px;object-fit:cover;border-radius:2px;display:block;">`
            : '';
        const label = showCode
            ? `<span>${l.code}</span>`
            : `<span style="${SR}">${l.flagAlt || l.code}</span>`;
        // hreflang is crucial for multi-language SEO indexing
        return `<a href="#" data-elevare-lang-slot="${l.code}" hreflang="${l.code}" style="display:inline-flex;align-items:center;gap:6px;text-decoration:none;color:${DARK};font-weight:600;text-transform:uppercase;font-size:0.85rem;">${flag}${label}</a>`;
    };

    // Each link is wrapped in its own <li>: the switcher is a list of the
    // languages this page exists in. LanguageSwitcherResolutionService removes the
    // <li> along with the <a> when a language has no target, so an unreachable
    // language leaves no empty bullet behind.
    const langList = (mode) => {
        const languages = (window.grapesEditor && window.grapesEditor.getActiveLanguages)
            ? window.grapesEditor.getActiveLanguages() : [];
        const list = languages.length ? languages : [{ code: 'tr' }, { code: 'en' }];
        return list.map((l) => `<li>${langLink(l, mode)}</li>`).join('');
    };

    smart({
        id: 'elevare-lang-switcher', label: 'Language Switcher', icon: 'bi-translate',
        tag: 'nav',
        attrs: { 'aria-label': 'Dil seçimi' },
        style: { display: 'flex', gap: '12px', padding: '12px 20px', 'align-items': 'center' },
        traits: [{
            type: 'select', name: 'langDisplay', label: 'Gösterim', value: 'flag-code', changeProp: 1,
            options: [
                { id: 'flag-code', name: 'Bayrak + Kod' },
                { id: 'flag', name: 'Sadece Bayrak' },
                { id: 'code', name: 'Sadece Kod' }
            ]
        }],
        on: {
            langDisplay: (m, v) => {
                const box = m.find('[data-elevare-lang-list]')[0];
                if (box) box.components(langList(v || 'flag-code'));
            }
        },
        template: (m) => `<ul data-elevare-lang-list style="display:flex;gap:12px;align-items:center;list-style:none;margin:0;padding:0;">${langList(m.get('langDisplay') || 'flag-code')}</ul>`
    });

    // ==================================================
    // 43. MEGA MENU
    // ==================================================
    const megaMenuScript = function () {
        var root = this;
        function inCanvas() {
            try { var fe = window.frameElement; return !!(fe && (fe.className || '').indexOf('gjs-') > -1); }
            catch (e) { return false; }
        }
        var panel = root.querySelector('[data-elevare-dropdown-panel]');
        // The canvas closes it itself (the overlay sheet); the eye button opens it.
        if (panel && !inCanvas()) panel.style.display = 'none';
    };

    // Shared with the Side Panel, whose trigger is the same kind of button.
    const applyTriggerContent = (m, selector = '.el-mm-trigger', fallback = 'Kategoriler') => {
        const trigger = m.find(selector)[0];
        renderButtonContent(trigger, liveLabel(trigger, m.get('triggerLabel') || fallback),
            m.get('triggerIcon') || '', m.get('triggerIconPosition') || 'right');
    };

    smart({
        id: 'elevare-megamenu', label: 'Mega Menu', icon: 'bi-grid-3x3-gap',
        // A dropdown menu is a navigation landmark, not a generic box.
        tag: 'nav',
        attrs: { 'aria-label': 'Kategori menüsü' },
        style: { position: 'relative', display: 'inline-block' },
        traits: [
            { type: 'text', name: 'menuId', label: 'Menü ID', value: 'megamenu-1', changeProp: 1 },
            // grid-template-columns has no Style Manager field of its own — no
            // built-in CSS property panel offers one — so without this trait the
            // panel's column count could only ever be changed by hand-editing the
            // page's HTML. This is the same reasoning as Gallery Masonry's
            // `columns` trait for its column-count: a number a Style Manager
            // sector cannot expose still needs a real control somewhere.
            { type: 'number', name: 'panelColumns', label: 'Panel Sütun Sayısı', min: 1, max: 4, value: 3, changeProp: 1 },
            // The trigger is a real <button> now (see the template below), so it gets
            // the same label/icon controls the Buton block has — including "Yok",
            // which is what removes the caret. It used to be a text-typed <div> with
            // the chevron baked into that text: editable as a string, but the icon
            // inside was not a component at all, so there was no way to select or
            // delete it. That is the bug this replaces, not a styling preference.
            { type: 'text', name: 'triggerLabel', label: 'Menü Yazısı', value: 'Kategoriler', changeProp: 1 },
            { type: 'select', name: 'triggerIcon', label: 'Menü İkonu', value: 'chevron-down', changeProp: 1, options: BUTTON_ICONS },
            {
                type: 'select', name: 'triggerIconPosition', label: 'İkon Konumu', value: 'right', changeProp: 1,
                options: [{ id: 'left', name: 'Solda' }, { id: 'right', name: 'Sağda' }]
            }
        ],
        on: {
            menuId: (m, v) => {
                const trigger = m.find('[data-elevare-dropdown-trigger]')[0];
                const panel = m.find('[data-elevare-dropdown-panel]')[0];
                if (trigger && v) trigger.addAttributes({ 'data-elevare-dropdown-trigger': v });
                if (panel && v) panel.addAttributes({ 'data-elevare-dropdown-panel': v });
            },
            panelColumns: (m, v) => {
                const panel = m.find('[data-elevare-dropdown-panel]')[0];
                if (panel) panel.addStyle({ 'grid-template-columns': `repeat(${clamp(parseInt(v, 10) || 3, 1, 4)}, 1fr)` });
            },
            triggerLabel: (m, v) => {
                const span = m.find('.el-btn-label')[0];
                if (span) span.components(v || 'Kategoriler');
                ensureAccessibleName(childWithClass(m, 'el-mm-trigger'), v || 'Kategoriler');
            },
            triggerIcon: (m) => applyTriggerContent(m),
            triggerIconPosition: (m) => applyTriggerContent(m)
        },
        script: megaMenuScript,
        overlay: { panels: [{ sel: '[data-elevare-dropdown-panel]' }] },
        onInit: (m) => ensureAccessibleName(childWithClass(m, 'el-mm-trigger'), m.get('triggerLabel') || 'Kategoriler'),
        template: (m) => {
            const id = m.get('menuId') || 'megamenu-1';
            const cols = clamp(parseInt(m.get('panelColumns'), 10) || 3, 1, 4);
            const tIcon = m.get('triggerIcon');
            const tGlyph = tIcon && ICONS[tIcon] ? icon(tIcon, 'font-size:0.7rem;') : '';
            const triggerGlyph = m.get('triggerIconPosition') === 'left' ? '' : tGlyph;
            const leadGlyph = m.get('triggerIconPosition') === 'left' ? tGlyph : '';
            return `
            <button type="button" data-elevare-btn data-elevare-dropdown-trigger="${id}" class="el-mm-trigger" style="display:inline-flex;align-items:center;gap:6px;padding:10px 16px;background:none;border:0;font-weight:600;cursor:pointer;color:${DARK};font-size:1rem;font-family:inherit;">
                ${leadGlyph}<span class="el-btn-label">${m.get('triggerLabel') || 'Kategoriler'}</span>${triggerGlyph}
            </button>
            <div data-elevare-dropdown-panel="${id}" style="position:absolute;top:100%;left:0;min-width:600px;background:${SURFACE};border-radius:10px;box-shadow:0 10px 40px rgba(0,0,0,.15);padding:24px;display:grid;grid-template-columns:repeat(${cols}, 1fr);gap:24px;z-index:100;">
                <div>
                    <h4 style="margin:0 0 12px;font-size:0.8rem;text-transform:uppercase;letter-spacing:.05em;color:${MUTED};">Kategori 1</h4>
                    <a href="#" style="display:block;padding:6px 0;color:${DARK};text-decoration:none;">Bağlantı 1</a>
                    <a href="#" style="display:block;padding:6px 0;color:${DARK};text-decoration:none;">Bağlantı 2</a>
                    <a href="#" style="display:block;padding:6px 0;color:${DARK};text-decoration:none;">Bağlantı 3</a>
                </div>
                <div>
                    <h4 style="margin:0 0 12px;font-size:0.8rem;text-transform:uppercase;letter-spacing:.05em;color:${MUTED};">Kategori 2</h4>
                    <a href="#" style="display:block;padding:6px 0;color:${DARK};text-decoration:none;">Bağlantı 1</a>
                    <a href="#" style="display:block;padding:6px 0;color:${DARK};text-decoration:none;">Bağlantı 2</a>
                    <a href="#" style="display:block;padding:6px 0;color:${DARK};text-decoration:none;">Bağlantı 3</a>
                </div>
                <div>
                    <h4 style="margin:0 0 12px;font-size:0.8rem;text-transform:uppercase;letter-spacing:.05em;color:${MUTED};">Öne Çıkan</h4>
                    <div style="border-radius:8px;overflow:hidden;background:${BG};">
                        <div style="height:100px;background:${BORDER};display:flex;align-items:center;justify-content:center;color:${MUTED};font-size:0.8rem;">Görsel Alanı</div>
                        <div style="padding:10px;">
                            <div style="font-weight:600;font-size:0.85rem;">Kampanya Başlığı</div>
                            <div style="font-size:0.75rem;color:${MUTED};">Kısa açıklama</div>
                        </div>
                    </div>
                </div>
            </div>`;
        }
    });

    // ==================================================
    // 44. SAYFA LİSTESİ (Page Listing)
    // ==================================================
    const listingFind = (m, selector) => m.find(selector)[0];

    const applyListingLayout = (m) => {
        const layout = m.get('listingLayout') || 'list';
        const cols = clamp(parseInt(m.get('columns'), 10) || 3, 1, 6);
        const items = listingFind(m, '.elevare-listing-items');
        if (items) {
            if (layout === 'grid') {
                items.addStyle({ display: 'grid', 'grid-template-columns': `repeat(${cols}, 1fr)`, 'column-count': 'auto', gap: '16px' });
            } else if (layout === 'masonry') {
                items.addStyle({ display: 'block', 'column-count': String(cols), 'column-gap': '16px' });
            } else {
                items.addStyle({ display: 'flex', 'flex-direction': 'column', 'column-count': 'auto', gap: '16px' });
            }
        }
        m.addAttributes({ 'data-elevare-layout': layout });
    };

    const toggleListingSection = (m, selector, prop, attr, displayOn) => {
        const on = m.get(prop) === 'true';
        const el = listingFind(m, selector);
        if (el) el.addStyle({ display: on ? displayOn : 'none' });
        m.addAttributes({ [attr]: on ? 'true' : 'false' });
    };

    const applyListingPaginationStyle = (m) => {
        const style = m.get('paginationStyle') || 'numeric-arrows';
        const showArrows = style === 'numeric-arrows' || style === 'truncated-arrows' || style === 'prev-next';
        const showNumbers = style !== 'prev-next';
        const prev = listingFind(m, '[data-elevare-prev-template]');
        const next = listingFind(m, '[data-elevare-next-template]');
        const num = listingFind(m, '[data-elevare-page-link-template]');
        if (prev) prev.addStyle({ display: showArrows ? 'block' : 'none' });
        if (next) next.addStyle({ display: showArrows ? 'block' : 'none' });
        if (num) num.addStyle({ display: showNumbers ? 'block' : 'none' });
        m.addAttributes({ 'data-elevare-pagination-style': style });
    };


    // --------------------------------------------------
    // LISTING PREVIEW — what the block will list, in the canvas
    // --------------------------------------------------
    // The block holds ONE card: the template every result is cloned from on the
    // public site. On its own it read as "this list has one item" — which is exactly
    // what an author saw while a second article sat unlisted as a draft. So the
    // canvas now shows the real cards (asked of the CMS: GetListingPreview, the
    // public site's own rules), dims and labels the pages that are left out and
    // why, and the trait panel says the same in words.
    //
    // The cards are plain DOM clones of the template's element, never components:
    // they carry the same ids, so every CSS rule — responsive ones included — styles
    // them exactly like the template; and being outside the model they are never
    // saved, never an undo step and never an unsaved change.
    const LISTING_T = LOCALE === 'tr' ? {
        template: 'Kart şablonu — tasarımı buradan',
        empty: 'Sonuç yoksa görünür',
        loading: 'Önizleme yükleniyor…',
        willList: (n) => n === 0 ? 'Bu ayarlarla listelenecek yayında sayfa yok.' : `Yayında ${n} sayfa listelenecek.`,
        hiddenIntro: (n) => `${n} sayfa yayında olmadığı için listelenmiyor:`,
        reasons: { draft: 'Taslak', archived: 'Arşivlendi', pending: 'Onay bekliyor', inactive: 'Pasif' },
        hiddenBadge: (r) => `${r} — listelenmez`,
        inTemplate: 'Şablonda önizleme yok: liste, bu şablonun eklendiği sayfada dolar.',
        noSource: 'Önizleme için bir kaynak sayfa seçin.',
        tagNone: 'Yok — tüm sayfalar',
        infoLabel: 'Listelenecekler'
    } : {
        template: 'Card template — design it here',
        empty: 'Shown when nothing matches',
        loading: 'Loading preview…',
        willList: (n) => n === 0 ? 'No published page matches these settings.' : `${n} published page(s) will be listed.`,
        hiddenIntro: (n) => `${n} page(s) are not listed because they are not live:`,
        reasons: { draft: 'Draft', archived: 'Archived', pending: 'Awaiting approval', inactive: 'Inactive' },
        hiddenBadge: (r) => `${r} — not listed`,
        inTemplate: 'No preview in a template: the list fills in on the page it is placed on.',
        noSource: 'Pick a source page to see a preview.',
        tagNone: 'None — every page',
        infoLabel: 'Will list'
    };
    const LISTING_MAX_CARDS = 12;
    const LISTING_MAX_HIDDEN_CARDS = 3;
    const LISTING_GHOST = 'data-elevare-ghost';
    const listingPreviews = new WeakMap();   // component → last GetListingPreview answer
    const listingTimers = new WeakMap();        // component → pending preview request
    const listingRenderTimers = new WeakMap();  // component → pending card redraw
    const listingInfoEls = new WeakMap();    // component → info trait element
    const listingPerPage = (m) => clamp(parseInt(m.getAttributes()['data-elevare-items-per-page'], 10) || parseInt(m.get('itemsPerPage'), 10) || 10, 1, 100);
    const listingOf = (c) => {
        for (let p = c; p; p = p.parent && p.parent()) {
            if (p.getAttributes && p.getAttributes()[BLOCK_ATTR] === 'elevare-page-listing') return p;
        }
        return null;
    };

    const fillListingCard = (templateEl, item, hiddenReason) => {
        const card = templateEl.cloneNode(true);
        card.removeAttribute('data-elevare-item-template');
        card.setAttribute(LISTING_GHOST, '');
        card.setAttribute('aria-hidden', 'true');
        card.style.pointerEvents = 'none';
        [card, ...card.querySelectorAll('*')].forEach((el) => {
            [...el.classList].filter((cls) => cls.startsWith('gjs-')).forEach((cls) => el.classList.remove(cls));
            [...el.attributes].filter((a) => a.name.startsWith('data-gjs')).forEach((a) => el.removeAttribute(a.name));
        });
        if (hiddenReason) card.setAttribute('data-elevare-ghost-hidden', LISTING_T.hiddenBadge(hiddenReason));
        if (!item) return card;
        // The same fills as the public site (PageListingResolutionService): a field
        // the page has nothing for goes, rather than keeping the template's sample.
        card.querySelectorAll('[data-elevare-field]').forEach((f) => {
            switch (f.getAttribute('data-elevare-field')) {
                case 'title': f.textContent = item.title || ''; break;
                case 'summary': if (item.summary) f.textContent = item.summary; else f.remove(); break;
                case 'date': if (item.dateText) f.textContent = item.dateText; else f.remove(); break;
                case 'tags': if (item.tags) f.textContent = item.tags; else f.remove(); break;
                case 'image':
                    if (item.image) { f.setAttribute('src', item.image); f.removeAttribute('srcset'); }
                    else if (/^data:/i.test(f.getAttribute('src') || '')) f.remove();
                    break;
            }
        });
        return card;
    };

    const renderListingCards = (m) => {
        const container = listingFind(m, '.elevare-listing-items');
        const template = listingFind(m, '[data-elevare-item-template]');
        const containerEl = container && container.getEl && container.getEl();
        const templateEl = template && template.getEl && template.getEl();
        if (!containerEl || !templateEl) return;
        [...containerEl.children].filter((n) => n.hasAttribute(LISTING_GHOST)).forEach((n) => n.remove());

        const preview = listingPreviews.get(m);
        const cards = [];
        if (preview && ((preview.items || []).length || (preview.hidden || []).length)) {
            (preview.items || []).slice(0, LISTING_MAX_CARDS).forEach((item) => cards.push(fillListingCard(templateEl, item, null)));
            (preview.hidden || []).slice(0, LISTING_MAX_HIDDEN_CARDS).forEach((h) =>
                cards.push(fillListingCard(templateEl, { title: h.title }, LISTING_T.reasons[h.reason] || h.reason)));
        } else {
            // Nothing to show yet (a template, a new page): at least the page's shape.
            for (let i = 1; i < Math.min(listingPerPage(m), LISTING_MAX_CARDS); i++) cards.push(fillListingCard(templateEl, null, null));
        }
        let after = templateEl;
        cards.forEach((card) => { after.after(card); after = card; });
    };

    const scheduleListingCards = (m) => {
        clearTimeout(listingRenderTimers.get(m));
        listingRenderTimers.set(m, setTimeout(() => renderListingCards(m), 60));
    };

    const renderListingInfo = (m) => {
        const el = listingInfoEls.get(m);
        if (!el) return;
        const preview = listingPreviews.get(m);
        const attrs = m.getAttributes();
        el.textContent = '';
        const line = (text, cls) => { const p = document.createElement('div'); p.textContent = text; if (cls) p.className = cls; el.appendChild(p); return p; };
        if (preview === undefined) { line(LISTING_T.loading); return; }
        if (preview === null) {
            line(attrs['data-elevare-source'] === 'specific' && !attrs['data-elevare-source-page-id'] ? LISTING_T.noSource : LISTING_T.inTemplate);
            return;
        }
        line(LISTING_T.willList(preview.publishedCount || 0), 'elevare-listing-info-count');
        const hidden = preview.hidden || [];
        if (hidden.length) {
            line(LISTING_T.hiddenIntro(hidden.length));
            const list = document.createElement('ul');
            hidden.forEach((h) => {
                const li = document.createElement('li');
                const a = document.createElement('a');
                a.href = '/pages/edit/' + encodeURIComponent(h.id);
                a.target = '_blank';
                a.rel = 'noopener';
                a.textContent = h.title;
                li.appendChild(a);
                li.appendChild(document.createTextNode(' — ' + (LISTING_T.reasons[h.reason] || h.reason)));
                list.appendChild(li);
            });
            el.appendChild(list);
        }
    };

    const requestListingPreview = (m) => {
        const ge = window.grapesEditor;
        if (!ge || typeof ge.getListingPreview !== 'function') return;
        clearTimeout(listingTimers.get(m));
        listingTimers.set(m, setTimeout(async () => {
            const attrs = m.getAttributes();
            const source = attrs['data-elevare-source'] || 'self';
            const sourcePageId = parseInt(attrs['data-elevare-source-page-id'], 10) || null;
            let preview = null;
            if (source !== 'specific' || sourcePageId) {
                preview = await ge.getListingPreview({
                    source, sourcePageId,
                    relatedTags: attrs['data-elevare-related-tags'] === 'true',
                    sort: attrs['data-elevare-sort'] || 'newest',
                    tagSlug: attrs['data-elevare-default-tag'] || null,
                    take: Math.min(listingPerPage(m), LISTING_MAX_CARDS)
                });
            }
            listingPreviews.set(m, preview || null);
            renderListingCards(m);
            renderListingInfo(m);
        }, 250));
    };

    // Canvas-only labels: which card is the template, which note only shows when
    // nothing matches, and why a dimmed card will not be listed.
    const LISTING_CANVAS_STYLE_ID = 'elevare-listing-canvas-css';
    const listingCanvasCss = () => {
        const q = (text) => JSON.stringify(text);
        const badge = 'position:absolute;right:8px;font:600 10px/1.7 system-ui,sans-serif;letter-spacing:.02em;padding:0 6px;border-radius:4px;pointer-events:none;z-index:2;';
        return `.elevare-page-listing [data-elevare-item-template],.elevare-page-listing [data-elevare-empty-template],[${LISTING_GHOST}]{position:relative}` +
            `.elevare-page-listing [data-elevare-item-template]::after{content:${q(LISTING_T.template)};${badge}top:-9px;background:#3b82f6;color:#fff}` +
            `.elevare-page-listing [data-elevare-empty-template]::after{content:${q(LISTING_T.empty)};${badge}top:-9px;background:#6b7280;color:#fff}` +
            `[data-elevare-ghost-hidden]{opacity:.55}` +
            `[data-elevare-ghost-hidden]::after{content:attr(data-elevare-ghost-hidden);${badge}top:8px;background:#f59e0b;color:#111}`;
    };
    const installListingCanvasCss = () => {
        const doc = editor.Canvas.getDocument && editor.Canvas.getDocument();
        if (!doc || !doc.head || doc.getElementById(LISTING_CANVAS_STYLE_ID)) return;
        const style = doc.createElement('style');
        style.id = LISTING_CANVAS_STYLE_ID;
        style.textContent = listingCanvasCss();
        doc.head.appendChild(style);
    };
    const allListings = () => editor.getWrapper() ? editor.getWrapper().find(`[${BLOCK_ATTR}="elevare-page-listing"]`) : [];
    editor.on('canvas:frame:load', () => setTimeout(() => { installListingCanvasCss(); allListings().forEach(scheduleListingCards); }, 0));
    editor.on('load', () => { installListingCanvasCss(); allListings().forEach(scheduleListingCards); });
    // GrapesJS re-renders a component's element when it changes; the cards next to
    // the template are redrawn after any change inside the block.
    ['component:update', 'component:add'].forEach((evt) => editor.on(evt, (c) => {
        const listing = c && listingOf(c);
        if (listing) scheduleListingCards(listing);
    }));
    editor.on('component:mount', (c) => { if (listingOf(c) === c) { installListingCanvasCss(); scheduleListingCards(c); } });
    // Coming back to the block (after publishing a page in another tab, say) asks again.
    editor.on('component:selected', (c) => { if (c && listingOf(c) === c) requestListingPreview(c); });

    // The trait panel's summary: how many pages will be listed, and which are left out.
    const LISTING_INFO_TRAIT = 'elevare-listing-info';
    editor.TraitManager.addType(LISTING_INFO_TRAIT, {
        noLabel: true,
        createInput({ trait }) {
            const el = document.createElement('div');
            el.className = 'elevare-listing-info';
            const target = trait.target || trait.get('target');
            if (target) { listingInfoEls.set(target, el); setTimeout(() => renderListingInfo(target), 0); }
            return el;
        },
        onUpdate({ trait }) {
            const target = trait.target || trait.get('target');
            if (target) renderListingInfo(target);
        }
    });

    // The default tag as a pick from the site's tags, not a slug to know by heart.
    const LISTING_TAG_TRAIT = 'elevare-listing-tag';
    editor.TraitManager.addType(LISTING_TAG_TRAIT, {
        createInput({ trait }) {
            const wrap = document.createElement('div');
            wrap.className = 'gjs-field gjs-select';
            const select = document.createElement('select');
            wrap.appendChild(select);
            const fill = (tags, current) => {
                select.textContent = '';
                const none = document.createElement('option');
                none.value = '';
                none.textContent = LISTING_T.tagNone;
                select.appendChild(none);
                (tags || []).forEach((t) => {
                    const o = document.createElement('option');
                    o.value = t.slug;
                    o.textContent = t.name;
                    select.appendChild(o);
                });
                // A slug set before the tag was renamed or deleted stays visible, not lost.
                if (current && ![...select.options].some((o) => o.value === current)) {
                    const o = document.createElement('option');
                    o.value = current;
                    o.textContent = current;
                    select.appendChild(o);
                }
                select.value = current || '';
            };
            const current = () => (typeof trait.getTargetValue === 'function' ? trait.getTargetValue() : '') || '';
            fill([], current());
            const ge = window.grapesEditor;
            if (ge && typeof ge.getListingTags === 'function') ge.getListingTags().then((tags) => fill(tags, current()));
            return wrap;
        },
        onEvent({ elInput, trait }) {
            trait.setTargetValue(elInput.querySelector('select').value || '');
        },
        onUpdate({ elInput, trait }) {
            const select = elInput.querySelector('select');
            const value = (typeof trait.getTargetValue === 'function' ? trait.getTargetValue() : '') || '';
            if (![...select.options].some((o) => o.value === value) && value) {
                const o = document.createElement('option');
                o.value = value;
                o.textContent = value;
                select.appendChild(o);
            }
            select.value = value;
        }
    });

    // "Kaynak Sayfa" only means something for the "a specific page" source.
    const LISTING_SOURCE_PAGE_TRAIT = { type: PAGE_TRAIT, name: 'data-elevare-source-page-id', label: 'Kaynak Sayfa', category: { id: 'source', label: 'Veri Kaynağı' } };
    const syncListingSourceTrait = (m) => {
        const specific = m.getAttributes()['data-elevare-source'] === 'specific';
        const has = !!(m.getTrait && m.getTrait(LISTING_SOURCE_PAGE_TRAIT.name));
        if (specific && !has && m.addTrait) m.addTrait(LISTING_SOURCE_PAGE_TRAIT, { at: 2 });
        else if (!specific && has && m.removeTrait) m.removeTrait(LISTING_SOURCE_PAGE_TRAIT.name);
    };

    // Reads what the markup says into the trait props (a block that arrived as HTML —
    // from a template — has only its type's defaults there), drops preview cards an
    // older version saved as components, and starts listening.
    const loadListing = (m) => {
        const attrs = m.getAttributes();
        const props = {};
        const perPage = parseInt(attrs['data-elevare-items-per-page'], 10);
        if (perPage > 0) props.itemsPerPage = perPage;
        ['show-search:showSearch', 'show-tag-filter:showTagFilter', 'show-pagination:showPagination'].forEach((pair) => {
            const [attr, prop] = pair.split(':');
            const v = attrs['data-elevare-' + attr];
            if (v === 'true' || v === 'false') props[prop] = v;
        });
        if (attrs['data-elevare-pagination-style']) props.paginationStyle = attrs['data-elevare-pagination-style'];
        if (attrs['data-elevare-layout']) props.listingLayout = attrs['data-elevare-layout'];
        m.set(props, { silent: true });
        // A block that arrives with a section switched off (the "İlgili Yazılar"
        // preset) shows it off in the canvas too; the public site drops it either way.
        if (attrs['data-elevare-show-search'] === 'false') toggleListingSection(m, '.elevare-listing-search', 'showSearch', 'data-elevare-show-search', 'flex');
        if (attrs['data-elevare-show-tag-filter'] === 'false') toggleListingSection(m, '.elevare-listing-tagcloud', 'showTagFilter', 'data-elevare-show-tag-filter', 'flex');
        if (attrs['data-elevare-show-pagination'] === 'false') toggleListingSection(m, '.elevare-listing-pagination', 'showPagination', 'data-elevare-show-pagination', 'flex');

        const container = listingFind(m, '.elevare-listing-items');
        if (container) {
            container.components().filter((c) => c.getAttributes()[LISTING_GHOST] !== undefined).forEach((c) => c.remove());
        }

        syncListingSourceTrait(m);
        m.on('change:attributes:data-elevare-source', () => syncListingSourceTrait(m));
        m.on('change:attributes:data-elevare-source change:attributes:data-elevare-source-page-id change:attributes:data-elevare-sort change:attributes:data-elevare-default-tag change:attributes:data-elevare-related-tags',
            () => { listingPreviews.delete(m); renderListingInfo(m); requestListingPreview(m); });
        requestListingPreview(m);
    };

    smart({
        id: 'elevare-page-listing', label: 'Page Listing', icon: 'bi-grid-3x3-gap-fill',
        tag: 'div',
        style: { padding: '40px 20px' },
        attrs: {
            class: 'elevare-page-listing',
            'data-elevare-source': 'self',
            'data-elevare-source-page-id': '',
            'data-elevare-layout': 'list',
            'data-elevare-items-per-page': '10',
            'data-elevare-sort': 'newest',
            'data-elevare-show-search': 'true',
            'data-elevare-show-tag-filter': 'true',
            'data-elevare-default-tag': '',
            'data-elevare-show-pagination': 'true',
            'data-elevare-pagination-style': 'numeric-arrows'
        },
        traits: [
            { type: LISTING_INFO_TRAIT, name: 'elevareListingInfo', label: LISTING_T.infoLabel, category: { id: 'source', label: 'Veri Kaynağı' }, changeProp: 1 },
            {
                type: 'select', name: 'data-elevare-source', label: 'Veri Kaynağı', category: { id: 'source', label: 'Veri Kaynağı' }, options: [
                    { id: 'self', name: 'Bu sayfanın alt sayfaları' },
                    { id: 'siblings', name: 'Bu sayfanın kardeşleri (aynı üst sayfa)' },
                    { id: 'specific', name: 'Belirli bir sayfanın alt sayfaları' },
                    { id: 'all', name: 'Tüm yayınlanmış sayfalar' }
                ]
            },
            // "Related": only pages that share a tag with this one.
            { type: 'checkbox', name: 'data-elevare-related-tags', label: LOCALE === 'tr' ? 'Yalnızca ortak etiketliler' : 'Only pages sharing a tag', valueTrue: 'true', valueFalse: 'false', category: { id: 'source', label: 'Veri Kaynağı' } },
            LISTING_SOURCE_PAGE_TRAIT,
            {
                type: 'select', name: 'data-elevare-sort', label: 'Sıralama', category: { id: 'source', label: 'Veri Kaynağı' }, options: [
                    { id: 'newest', name: 'En Yeni' },
                    { id: 'oldest', name: 'En Eski' },
                    { id: 'title-asc', name: 'Başlık (A-Z)' },
                    { id: 'title-desc', name: 'Başlık (Z-A)' }
                ]
            },
            { type: 'number', name: 'itemsPerPage', label: 'Sayfa Başına Öğe', category: { id: 'source', label: 'Veri Kaynağı' }, min: 1, max: 100, value: 10, changeProp: 1 },
            { type: LISTING_TAG_TRAIT, name: 'data-elevare-default-tag', label: 'Varsayılan Etiket', category: { id: 'source', label: 'Veri Kaynağı' } },
            { type: 'text', name: 'data-elevare-default-image', label: 'Varsayılan Kart Görseli', category: { id: 'layout', label: 'Görünüm' } },
            { type: 'checkbox', name: 'showSearch', label: 'Arama Kutusu Göster', category: { id: 'search', label: 'Arama' }, valueTrue: 'true', valueFalse: 'false', value: 'true', changeProp: 1 },
            { type: 'checkbox', name: 'showTagFilter', label: 'Etiket Filtresi Göster', category: { id: 'tags', label: 'Etiketler' }, valueTrue: 'true', valueFalse: 'false', value: 'true', changeProp: 1 },
            {
                type: 'select', name: 'listingLayout', label: 'Kart Düzeni', category: { id: 'layout', label: 'Görünüm' }, value: 'list', changeProp: 1, options: [
                    { id: 'list', name: 'Liste (alt alta)' },
                    { id: 'grid', name: 'Izgara (yan yana)' },
                    { id: 'masonry', name: 'Masonry (tuğla dizilimi)' }
                ]
            },
            { type: 'number', name: 'columns', label: 'Sütun Sayısı', category: { id: 'layout', label: 'Görünüm' }, min: 1, max: 6, value: 3, changeProp: 1 },
            { type: 'checkbox', name: 'showPagination', label: 'Sayfalama Göster', category: { id: 'pagination', label: 'Sayfalama' }, valueTrue: 'true', valueFalse: 'false', value: 'true', changeProp: 1 },
            {
                type: 'select', name: 'paginationStyle', label: 'Sayfalama Biçimi', category: { id: 'pagination', label: 'Sayfalama' }, value: 'numeric-arrows', changeProp: 1, options: [
                    { id: 'numeric', name: 'Sadece numaralar (1 2 3)' },
                    { id: 'numeric-arrows', name: 'Ok + numaralar (« 1 2 3 »)' },
                    { id: 'truncated', name: 'Kısaltılmış (1 2 … 10)' },
                    { id: 'truncated-arrows', name: 'Ok + kısaltılmış (« 1 2 … 10 »)' },
                    { id: 'prev-next', name: 'Sadece oklar (« »)' }
                ]
            }
        ],
        on: {
            showSearch: (m) => toggleListingSection(m, '.elevare-listing-search', 'showSearch', 'data-elevare-show-search', 'flex'),
            showTagFilter: (m) => toggleListingSection(m, '.elevare-listing-tagcloud', 'showTagFilter', 'data-elevare-show-tag-filter', 'flex'),
            showPagination: (m) => toggleListingSection(m, '.elevare-listing-pagination', 'showPagination', 'data-elevare-show-pagination', 'flex'),
            listingLayout: (m) => applyListingLayout(m),
            columns: (m) => applyListingLayout(m),
            paginationStyle: (m) => applyListingPaginationStyle(m),
            itemsPerPage: (m) => {
                m.addAttributes({ 'data-elevare-items-per-page': String(clamp(parseInt(m.get('itemsPerPage'), 10) || 10, 1, 100)) });
                requestListingPreview(m);
            }
        },
        onLoad: (m) => loadListing(m),
        template: () => `
            <form class="elevare-listing-search" method="get" style="display:flex;gap:8px;margin-bottom:24px;">
                <input type="text" name="q" placeholder="${CT('Arama...', 'Search...')}" aria-label="${CT('Bu listede ara', 'Search this list')}" style="flex:1;padding:10px 14px;border:1px solid ${BORDER};border-radius:6px;background:${SURFACE};color:${TEXT};">
                <button data-elevare-btn type="submit" style="padding:10px 20px;border:none;border-radius:6px;background:${BLUE};color:${ON_PRIMARY};font-weight:600;cursor:pointer;">${CT('Ara', 'Search')}</button>
            </form>

            <div class="elevare-listing-tagcloud" style="display:flex;gap:8px;flex-wrap:wrap;margin-bottom:24px;">
                <a href="#" data-elevare-tag-all style="display:inline-block;padding:6px 14px;border-radius:20px;text-decoration:none;font-size:0.85rem;font-weight:600;color:${ON_PRIMARY};background:${BLUE};">${CT('Tümü', 'All')}</a>
                <a href="#" data-elevare-tag-chip-template style="display:inline-block;padding:6px 14px;border-radius:20px;text-decoration:none;font-size:0.85rem;color:${DARK};background:${BG};">${CT('Örnek Etiket', 'Sample Tag')}</a>
            </div>

            <div class="elevare-listing-items" style="display:flex;flex-direction:column;gap:16px;">
                <a href="#" data-elevare-item-template style="display:flex;gap:16px;padding:16px;border:1px solid ${BORDER};border-radius:8px;text-decoration:none;color:${DARK};background:${SURFACE};">
                    <img data-elevare-field="image" src="${ph(200, 140, 'Görsel')}" alt="" loading="lazy" style="width:160px;height:110px;object-fit:cover;border-radius:6px;flex-shrink:0;">
                    <div>
                        <span data-elevare-field="date" style="display:block;font-size:0.72rem;color:${MUTED};margin-bottom:4px;">17.07.2026</span>
                        <h3 data-elevare-field="title" style="margin:0 0 6px;font-size:1.05rem;font-weight:600;">${CT('Örnek Sayfa Başlığı', 'Sample Page Title')}</h3>
                        <p data-elevare-field="summary" style="margin:0 0 6px;font-size:0.9rem;color:${MUTED};">${CT('Bu sayfanın kısa açıklaması burada görünecek.', 'A short description of the page appears here.')}</p>
                        <span data-elevare-field="tags" style="display:block;font-size:0.72rem;color:${BLUE};">${CT('Kategori, Etiket', 'Category, Tag')}</span>
                    </div>
                </a>
                <div data-elevare-empty-template style="padding:24px;text-align:center;color:${MUTED};font-size:0.9rem;">${CT('Sonuç bulunamadı.', 'No results found.')}</div>
            </div>

            <ul class="elevare-listing-pagination" style="display:flex;gap:8px;list-style:none;padding:0;margin:24px 0 0;flex-wrap:wrap;">
                <li data-elevare-prev-template><a href="#" style="display:inline-block;padding:8px 14px;border:1px solid ${BORDER};border-radius:6px;text-decoration:none;color:${DARK};">«</a></li>
                <li data-elevare-page-link-template><a href="#" style="display:inline-block;padding:8px 14px;border:1px solid ${BLUE};border-radius:6px;text-decoration:none;color:${ON_PRIMARY};background:${BLUE};font-weight:600;">1</a></li>
                <li data-elevare-next-template><a href="#" style="display:inline-block;padding:8px 14px;border:1px solid ${BORDER};border-radius:6px;text-decoration:none;color:${DARK};">»</a></li>
            </ul>`
    });

    blockCss(
        '@media (max-width: 767px) {' +
        ' .elevare-page-listing .elevare-listing-items { grid-template-columns: 1fr !important; column-count: 1 !important; }' +
        ' .elevare-page-listing .elevare-listing-search { flex-direction: column !important; }' +
        ' .elevare-page-listing .elevare-listing-items a { flex-direction: column !important; }' +
        ' .elevare-page-listing .elevare-listing-items img { width: 100% !important; height: auto !important; }' +
        ' }'
    );

    // ==================================================
    // 45. ARAMA KUTUSU (Search Box)
    // ==================================================
    const searchBoxScript = function () {
        var root = this;
        function inCanvas() {
            try { var fe = window.frameElement; return !!(fe && (fe.className || '').indexOf('gjs-') > -1); }
            catch (e) { return false; }
        }
        // In the canvas the results box and the sample rows that style its items
        // start hidden like the live panel (the overlay sheet); the eye shows them.
        if (inCanvas()) return;

        var form = root.querySelector('form');
        var input = form ? form.querySelector('input[name="q"]') : root.querySelector('input[name="q"]');
        var templatesHolder = root.querySelector('.elevare-search-templates');
        var itemTemplate = root.querySelector('[data-elevare-search-item-template]');
        var groupTemplate = root.querySelector('[data-elevare-search-group-template]');
        var emptyTemplate = root.querySelector('[data-elevare-search-empty-template]');
        var output = root.querySelector('.elevare-search-results-output');
        if (!input || !itemTemplate || !output) return;

        itemTemplate.parentNode.removeChild(itemTemplate);
        if (groupTemplate) groupTemplate.parentNode.removeChild(groupTemplate);
        if (emptyTemplate) emptyTemplate.parentNode.removeChild(emptyTemplate);
        if (templatesHolder) templatesHolder.style.display = 'none';

        if (form) form.addEventListener('submit', function (e) { e.preventDefault(); });

        // The panel is display:none until there is something to show, otherwise an
        // empty bordered box hangs under the input on every page load. The open
        // value is read from the element instead of hard-coded: the Sonuç Görünümü
        // trait switches this between flex and grid, and hard-coding one would
        // silently undo that choice the first time the panel opened.
        var openDisplay = output.style.display || '';
        output.style.display = 'none';

        function clearOutput() {
            while (output.firstChild) output.removeChild(output.firstChild);
            output.style.display = 'none';
        }

        function renderHighlighted(container, text, term) {
            container.textContent = '';
            if (!term) { container.appendChild(document.createTextNode(text)); return; }
            var lower = text.toLowerCase(), lowerTerm = term.toLowerCase(), i = 0;
            while (true) {
                var idx = lower.indexOf(lowerTerm, i);
                if (idx === -1) { container.appendChild(document.createTextNode(text.slice(i))); break; }
                if (idx > i) container.appendChild(document.createTextNode(text.slice(i, idx)));
                var mark = document.createElement('mark');
                mark.className = 'elevare-search-highlight';
                mark.textContent = text.slice(idx, idx + term.length);
                container.appendChild(mark);
                i = idx + term.length;
            }
        }

        function renderItem(container, item, term) {
            var clone = itemTemplate.cloneNode(true);
            clone.removeAttribute('data-elevare-search-item-template');
            var anchor = clone.tagName === 'A' ? clone : clone.querySelector('a');
            if (anchor) anchor.setAttribute('href', item.path || '/');
            var titleEl = clone.querySelector('[data-elevare-field="title"]');
            if (titleEl) renderHighlighted(titleEl, item.title || '', term);
            var excerptEl = clone.querySelector('[data-elevare-field="excerpt"]');
            if (excerptEl) renderHighlighted(excerptEl, item.excerpt || '', term);
            var categoryEl = clone.querySelector('[data-elevare-field="category"]');
            if (categoryEl) categoryEl.textContent = item.category || '';
            container.appendChild(clone);
        }

        function renderResults(items, term) {
            clearOutput();
            output.style.display = openDisplay;
            if (!items || !items.length) {
                if (emptyTemplate) output.appendChild(emptyTemplate.cloneNode(true));
                return;
            }

            var layout = root.getAttribute('data-elevare-layout') || 'list';
            if (layout === 'grouped-cards') {
                var groups = {}, order = [];
                items.forEach(function (it) {
                    var key = it.category || '';
                    if (!groups[key]) { groups[key] = []; order.push(key); }
                    groups[key].push(it);
                });
                order.forEach(function (key) {
                    if (groupTemplate) {
                        var g = groupTemplate.cloneNode(true);
                        g.removeAttribute('data-elevare-search-group-template');
                        var field = g.querySelector('[data-elevare-field="category"]') || g;
                        field.textContent = key || '—';
                        output.appendChild(g);
                    }
                    groups[key].forEach(function (it) { renderItem(output, it, term); });
                });
            } else {
                items.forEach(function (it) { renderItem(output, it, term); });
            }
        }

        var debounceTimer = null;
        var lastRequestId = 0;
        input.addEventListener('input', function () {
            clearTimeout(debounceTimer);
            var term = input.value.trim();
            debounceTimer = setTimeout(function () {
                if (term.length < 2) { clearOutput(); return; }
                var requestId = ++lastRequestId;
                var lang = document.documentElement.lang || 'tr';
                var max = root.getAttribute('data-elevare-max-results') || '12';
                fetch('/api/search?q=' + encodeURIComponent(term) + '&lang=' + encodeURIComponent(lang) + '&max=' + encodeURIComponent(max))
                    .then(function (r) { return r.ok ? r.json() : []; })
                    .then(function (items) {
                        if (requestId !== lastRequestId) return;
                        renderResults(items, term);
                    })
                    .catch(function () { });
            }, 300);
        });

        input.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') { clearOutput(); input.blur(); }
        });
        document.addEventListener('click', function (e) {
            if (!root.contains(e.target)) clearOutput();
        });
    };

    const applySearchLayout = (m) => {
        const layout = m.get('searchLayout') || 'list';
        const cols = clamp(parseInt(m.get('searchColumns'), 10) || 3, 1, 6);
        const output = listingFind(m, '.elevare-search-results-output');
        if (output) {
            if (layout === 'cards' || layout === 'grouped-cards') {
                output.addStyle({ display: 'grid', 'grid-template-columns': `repeat(${cols}, 1fr)`, gap: '16px' });
            } else {
                output.addStyle({ display: 'flex', 'flex-direction': 'column', gap: '12px' });
            }
        }
        m.addAttributes({ 'data-elevare-layout': layout });
    };

    smart({
        id: 'elevare-search-box', label: 'Search Box', icon: 'bi-search',
        tag: 'div',
        // Anchors the results panel, which is absolutely positioned (see the
        // template). Without this the panel would escape to the nearest positioned
        // ancestor — in a menu, usually the header itself, so it would span the
        // whole bar instead of sitting under the input.
        style: { position: 'relative' },
        attrs: {
            class: 'elevare-search-box',
            'data-elevare-layout': 'list',
            'data-elevare-max-results': '12'
        },
        traits: [
            {
                type: 'select', name: 'searchLayout', label: 'Sonuç Görünümü', category: { id: 'layout', label: 'Görünüm' }, value: 'list', changeProp: 1, options: [
                    { id: 'list', name: 'Düz Liste' },
                    { id: 'cards', name: 'Kartlar' },
                    { id: 'grouped-cards', name: 'Kategoriye Göre Gruplanmış Kartlar' }
                ]
            },
            { type: 'number', name: 'searchColumns', label: 'Sütun Sayısı (Kartlar)', category: { id: 'layout', label: 'Görünüm' }, min: 1, max: 6, value: 3, changeProp: 1 },
            { type: 'number', name: 'data-elevare-max-results', label: 'Maksimum Sonuç', category: { id: 'results', label: 'Sonuçlar' }, min: 3, max: 30 }
        ],
        on: {
            searchLayout: (m) => applySearchLayout(m),
            searchColumns: (m) => applySearchLayout(m)
        },
        script: searchBoxScript,
        overlay: { panels: [{ sel: '.elevare-search-results-output' }, { sel: '.elevare-search-templates' }] },
        template: () => `
            <form style="display:flex;gap:8px;margin-bottom:16px;">
                <input type="text" name="q" placeholder="${CT('Ara...', 'Search...')}" aria-label="${CT('Sitede ara', 'Search the site')}" style="flex:1;padding:10px 14px;border:1px solid ${BORDER};border-radius:6px;background:${SURFACE};color:${TEXT};">
            </form>
            <div class="elevare-search-results-output" style="position:absolute;top:100%;left:0;right:0;min-width:280px;z-index:100;max-height:60vh;overflow:auto;background:${SURFACE};border:1px solid ${BORDER};border-radius:10px;box-shadow:0 10px 40px rgba(0,0,0,.15);padding:12px;display:flex;flex-direction:column;gap:12px;"></div>
            <div class="elevare-search-templates">
                <a href="#" data-elevare-search-item-template style="display:flex;flex-direction:column;gap:4px;padding:14px;border:1px solid ${BORDER};border-radius:8px;text-decoration:none;color:${DARK};background:${SURFACE};">
                    <span data-elevare-field="category" style="font-size:0.7rem;font-weight:600;color:${BLUE};text-transform:uppercase;letter-spacing:.03em;">${CT('Kategori', 'Category')}</span>
                    <h4 data-elevare-field="title" style="margin:0;font-size:1rem;font-weight:600;">${CT('Örnek Sonuç Başlığı', 'Sample Result Title')}</h4>
                    <p data-elevare-field="excerpt" style="margin:0;font-size:0.85rem;color:${MUTED};">${CT('Eşleşen içerik burada kısa bir alıntı olarak görünecek.', 'A short excerpt of the matching content appears here.')}</p>
                </a>
                <div data-elevare-search-group-template style="grid-column:1/-1;margin:12px 0 4px;font-size:0.75rem;font-weight:700;color:${MUTED};text-transform:uppercase;letter-spacing:.04em;">${CT('Kategori Adı', 'Category Name')}</div>
                <div data-elevare-search-empty-template style="padding:20px;text-align:center;color:${MUTED};font-size:0.9rem;">${CT('Sonuç bulunamadı.', 'No results found.')}</div>
            </div>`
    });

    blockCss(
        '.elevare-search-highlight { font-weight: 700; background: transparent; padding: 0; color: inherit; }' +
        '@media (max-width: 767px) {' +
        // min-width keeps the panel readable when the input is a narrow slot in a
        // menu bar, but on a phone that same floor pushes it off the right edge.
        ' .elevare-search-box .elevare-search-results-output { grid-template-columns: 1fr !important; min-width: 0 !important; }' +
        ' }'
    );

    // ==================================================
    // 47. WHATSAPP BUTONU — floating, position picked from a trait
    // ==================================================
    // WhatsApp green and the white glyph on it are literals on purpose: the surface
    // is a third party's brand colour, fixed in every theme, so the palette test at
    // the top of this file says its text is fixed too.
    const WA_POSITIONS = [
        { id: 'bottom-right', name: 'Sağ alt' }, { id: 'bottom-center', name: 'Alt orta' }, { id: 'bottom-left', name: 'Sol alt' },
        { id: 'middle-right', name: 'Sağ orta' }, { id: 'middle-left', name: 'Sol orta' },
        { id: 'top-right', name: 'Sağ üst' }, { id: 'top-center', name: 'Üst orta' }, { id: 'top-left', name: 'Sol üst' }
    ];

    // Every edge is reset before the chosen ones are set, so switching from
    // "Sağ alt" to "Sol üst" does not leave the old bottom/right in the rule
    // fighting the new top/left. The centred positions pin at 50% and pull back by
    // half the button — the same translate trick as the video facade's play button.
    const applyWhatsAppPosition = (m) => {
        const pos = (m.get('waPosition') || 'bottom-right').split('-');
        const gap = clamp(parseInt(m.get('waOffset'), 10) || 0, 0, 200) + 'px';
        const style = { top: 'auto', bottom: 'auto', left: 'auto', right: 'auto', transform: 'none' };
        const shift = [];
        if (pos[0] === 'top') style.top = gap;
        else if (pos[0] === 'bottom') style.bottom = gap;
        else { style.top = '50%'; shift.push('translateY(-50%)'); }
        if (pos[1] === 'left') style.left = gap;
        else if (pos[1] === 'right') style.right = gap;
        else { style.left = '50%'; shift.push('translateX(-50%)'); }
        if (shift.length) style.transform = shift.join(' ');
        m.addStyle(style);
    };

    // wa.me wants digits only; people paste "+90 (555) 123 45 67".
    const applyWhatsAppHref = (m) => {
        const digits = String(m.get('waPhone') || '').replace(/\D/g, '');
        const text = String(m.get('waMessage') || '').trim();
        m.addAttributes({ href: 'https://wa.me/' + digits + (text ? '?text=' + encodeURIComponent(text) : '') });
    };

    smart({
        id: 'elevare-whatsapp', label: 'WhatsApp Button', icon: 'bi-whatsapp',
        tag: 'a',
        attrs: { href: '#', target: '_blank', rel: 'noopener noreferrer', 'aria-label': 'WhatsApp ile yazın', class: 'elevare-whatsapp' },
        style: {
            position: 'fixed', bottom: '24px', right: '24px', 'z-index': '9990',
            width: '56px', height: '56px', 'border-radius': '50%',
            background: '#25d366', color: '#ffffff', 'font-size': '1.7rem',
            display: 'flex', 'align-items': 'center', 'justify-content': 'center',
            'box-shadow': '0 6px 20px rgba(0,0,0,.25)', 'text-decoration': 'none'
        },
        traits: [
            { type: 'text', name: 'waPhone', label: 'Telefon (ülke koduyla)', value: contactValue('whatsapp', '905551234567'), changeProp: 1 },
            { type: 'text', name: 'waMessage', label: 'Hazır Mesaj', value: 'Merhaba, bilgi almak istiyorum.', changeProp: 1 },
            { type: 'select', name: 'waPosition', label: 'Konum', value: 'bottom-right', changeProp: 1, options: WA_POSITIONS },
            { type: 'number', name: 'waOffset', label: 'Kenar Boşluğu (px)', value: 24, min: 0, max: 200, changeProp: 1 }
        ],
        on: {
            waPhone: (m) => applyWhatsAppHref(m),
            waMessage: (m) => applyWhatsAppHref(m),
            waPosition: (m) => applyWhatsAppPosition(m),
            waOffset: (m) => applyWhatsAppPosition(m)
        },
        // The href is derived, never typed: recomputed on every init so a number
        // filled in on Site Settings after the block was placed still reaches it.
        onInit: (m) => applyWhatsAppHref(m),
        template: () => icon('whatsapp')
    });

    // ==================================================
    // 48. YAN PANEL (Side Panel) — a button that slides a panel in from an edge
    // ==================================================
    const sidebarScript = function () {
        var root = this;
        function inCanvas() {
            try { var fe = window.frameElement; return !!(fe && (fe.className || '').indexOf('gjs-') > -1); }
            catch (e) { return false; }
        }
        // The canvas handles both itself (the overlay sheet, see above smart()).
        if (inCanvas()) return;
        var panel = root.querySelector('[data-elevare-sidebar-panel]');
        var backdrop = root.querySelector('[data-elevare-sidebar-backdrop]');
        if (panel) panel.style.display = 'none';
        if (backdrop) backdrop.style.display = 'none';
    };

    const sidebarPanel = (m) => m.find('[data-elevare-sidebar-panel]')[0];

    smart({
        id: 'elevare-sidebar', label: 'Side Panel', icon: 'bi-layout-sidebar-inset',
        tag: 'div',
        attrs: { class: 'elevare-sidebar' },
        style: { position: 'relative', display: 'inline-block' },
        traits: [
            { type: 'text', name: 'sidebarId', label: 'Panel ID', value: 'sidebar-1', changeProp: 1 },
            {
                type: 'select', name: 'sidebarSide', label: 'Açıldığı Kenar', value: 'left', changeProp: 1,
                options: [{ id: 'left', name: 'Sol' }, { id: 'right', name: 'Sağ' }]
            },
            { type: 'number', name: 'sidebarWidth', label: 'Genişlik (px)', value: 320, min: 200, max: 640, changeProp: 1 },
            { type: 'checkbox', name: 'showBackdrop', label: 'Arka Planı Karart', value: true, changeProp: 1 },
            { type: 'checkbox', name: 'showClose', label: 'Kapatma Butonu Göster', value: true, changeProp: 1 },
            { type: 'text', name: 'triggerLabel', label: 'Buton Yazısı', value: 'Menü', changeProp: 1 },
            { type: 'select', name: 'triggerIcon', label: 'Buton İkonu', value: 'list', changeProp: 1, options: BUTTON_ICONS },
            {
                type: 'select', name: 'triggerIconPosition', label: 'İkon Konumu', value: 'left', changeProp: 1,
                options: [{ id: 'left', name: 'Solda' }, { id: 'right', name: 'Sağda' }]
            }
        ],
        on: {
            sidebarId: (m, v) => {
                if (!v) return;
                const trigger = m.find('[data-elevare-sidebar-trigger]')[0];
                const panel = sidebarPanel(m);
                const backdrop = m.find('[data-elevare-sidebar-backdrop]')[0];
                if (trigger) trigger.addAttributes({ 'data-elevare-sidebar-trigger': v });
                if (panel) panel.addAttributes({ 'data-elevare-sidebar-panel': v });
                if (backdrop) backdrop.addAttributes({ 'data-elevare-sidebar-backdrop': v });
            },
            // The attribute is what the slide-in rule below keys on; the edge
            // itself is a style so the Style Manager shows the truth.
            sidebarSide: (m, v) => {
                const panel = sidebarPanel(m);
                const side = v === 'right' ? 'right' : 'left';
                if (panel) {
                    panel.addStyle({ left: side === 'left' ? '0' : 'auto', right: side === 'right' ? '0' : 'auto' });
                    panel.addAttributes({ 'data-elevare-side': side });
                }
            },
            sidebarWidth: (m, v) => {
                const panel = sidebarPanel(m);
                if (panel) panel.addStyle({ width: clamp(parseInt(v, 10) || 320, 200, 640) + 'px' });
            },
            // display in the RULE, not inline: the runtime clears the inline value
            // to open, and the rule is what then decides whether there is a backdrop.
            showBackdrop: (m, v) => {
                const backdrop = m.find('[data-elevare-sidebar-backdrop]')[0];
                if (backdrop) backdrop.addStyle({ display: v ? 'block' : 'none' });
            },
            showClose: (m, v) => {
                const btn = m.find('[data-elevare-sidebar-close]')[0];
                if (btn) btn.addStyle({ display: v ? 'flex' : 'none' });
            },
            triggerLabel: (m, v) => {
                const span = m.find('.el-btn-label')[0];
                if (span) span.components(v || 'Menü');
                ensureAccessibleName(childWithClass(m, 'el-sb-trigger'), v || 'Menü');
            },
            triggerIcon: (m) => applyTriggerContent(m, '.el-sb-trigger', 'Menü'),
            triggerIconPosition: (m) => applyTriggerContent(m, '.el-sb-trigger', 'Menü')
        },
        script: sidebarScript,
        onInit: (m) => ensureAccessibleName(childWithClass(m, 'el-sb-trigger'), m.get('triggerLabel') || 'Menü'),
        // In the editor the panel opens where the visitor sees it: pinned to its
        // edge, full height, over the page. Only the closed-state slide (the
        // translateX rule below) is undone, since nothing in the canvas ever adds
        // is-open. The button is an ordinary component — drag it anywhere on the
        // page, into a header say; the ID is what ties it to the panel, not
        // proximity. The backdrop never shows in the canvas: a full-screen dimmer
        // with nothing on it to edit.
        overlay: {
            panels: [
                { sel: '[data-elevare-sidebar-panel]', canvasCss: 'transform:none !important;' },
                { sel: '[data-elevare-sidebar-backdrop]', display: 'none' }
            ]
        },
        template: (m) => {
            const id = m.get('sidebarId') || 'sidebar-1';
            const side = m.get('sidebarSide') === 'right' ? 'right' : 'left';
            const width = clamp(parseInt(m.get('sidebarWidth'), 10) || 320, 200, 640);
            const tIcon = m.get('triggerIcon');
            const tGlyph = tIcon && ICONS[tIcon] ? icon(tIcon) : '';
            const trailGlyph = m.get('triggerIconPosition') === 'right' ? tGlyph : '';
            const leadGlyph = m.get('triggerIconPosition') === 'right' ? '' : tGlyph;
            const link = (text) => `<a href="#" style="display:block;padding:10px 0;color:${DARK};text-decoration:none;border-bottom:1px solid ${BORDER};">${text}</a>`;
            return `
            <button type="button" data-elevare-btn data-elevare-sidebar-trigger="${id}" class="el-sb-trigger" style="display:inline-flex;align-items:center;gap:6px;padding:10px 16px;background:none;border:0;font-weight:600;cursor:pointer;color:${DARK};font-size:1rem;font-family:inherit;">
                ${leadGlyph}<span class="el-btn-label">${m.get('triggerLabel') || 'Menü'}</span>${trailGlyph}
            </button>
            <div data-elevare-sidebar-backdrop="${id}" style="position:fixed;inset:0;background:rgba(0,0,0,.5);z-index:9997;display:block;"></div>
            <aside data-elevare-sidebar-panel="${id}" data-elevare-side="${side}" aria-label="Yan panel" style="position:fixed;top:0;bottom:0;${side}:0;width:${width}px;max-width:90vw;background:${SURFACE};color:${TEXT};box-shadow:0 10px 40px rgba(0,0,0,.2);z-index:9998;padding:24px;overflow:auto;display:flex;flex-direction:column;gap:8px;">
                <button data-elevare-btn type="button" data-elevare-sidebar-close aria-label="Kapat" style="align-self:flex-end;width:32px;height:32px;border-radius:50%;border:0;background:${SURFACE2};color:${DARK};cursor:pointer;font-size:1.2rem;line-height:1;display:flex;align-items:center;justify-content:center;flex-shrink:0;">&times;</button>
                <h3 style="margin:0 0 8px;color:${DARK};">Panel Başlığı</h3>
                ${link('Bağlantı 1')}${link('Bağlantı 2')}${link('Bağlantı 3')}
                <p style="margin:16px 0 0;font-size:0.9rem;color:${MUTED};">Buraya menü, iletişim bilgisi, form — dilediğiniz her şeyi ekleyebilirsiniz.</p>
            </aside>`;
        }
    });

    // The slide itself. Keyed on the closed state so a panel with no script at all
    // (the canvas overrides transform inline) still lands where it should.
    blockCss(
        '.elevare-sidebar [data-elevare-sidebar-panel] { transition: transform .25s ease; }' +
        '.elevare-sidebar [data-elevare-sidebar-panel][data-elevare-side="left"]:not(.is-open) { transform: translateX(-100%); }' +
        '.elevare-sidebar [data-elevare-sidebar-panel][data-elevare-side="right"]:not(.is-open) { transform: translateX(100%); }' +
        '.elevare-sidebar [data-elevare-sidebar-backdrop] { transition: opacity .25s ease; opacity: 0; }' +
        '.elevare-sidebar [data-elevare-sidebar-backdrop].is-open { opacity: 1; }' +
        '@media (prefers-reduced-motion: reduce) {' +
        ' .elevare-sidebar [data-elevare-sidebar-panel], .elevare-sidebar [data-elevare-sidebar-backdrop] { transition: none; }' +
        '}'
    );

    // ==================================================
    // 46. GÖRSELLİ HERO (Yeni Eklenen SEO Uyumlu Blok)
    // ==================================================
    smart({
        id: 'elevare-hero-image', label: 'Hero with Image', icon: 'bi-image-alt',
        style: { padding: '80px 20px', 'background-color': BG },
        traits: [
            { type: 'select', name: 'imageLoading', label: 'Görsel Yükleme', options: [{ id: 'lazy', name: 'Lazy' }, { id: 'eager', name: 'Eager (Hero İçin Önerilir)' }], value: 'eager', changeProp: 1 },
            { type: 'select', name: 'reverse', label: 'Düzen', options: [{ id: 'row', name: 'Metin Solda' }, { id: 'row-reverse', name: 'Metin Sağda' }], value: 'row', changeProp: 1 }
        ],
        on: {
            imageLoading: (m, v) => { const img = m.find('img')[0]; if (img) img.addAttributes({ loading: v }); },
            reverse: (m, v) => { const t = m.find('.el-hero-img-wrap')[0]; if (t) t.addStyle({ 'flex-direction': v }); }
        },
        template: (m) => `
            <div class="el-hero-img-wrap" style="max-width:1200px;margin:0 auto;display:flex;flex-wrap:wrap;gap:40px;align-items:center;flex-direction:${m.get('reverse')};">
                <div style="flex:1;min-width:300px;">
                    <h1 style="font-size:3.2rem;font-weight:800;color:${DARK};line-height:1.2;margin-bottom:20px;">Fikirlerinizi Gerçeğe Dönüştürün</h1>
                    <p style="font-size:1.2rem;color:${MUTED};margin-bottom:32px;line-height:1.6;">Dijital dünyada fark yaratmak için ihtiyacınız olan tüm araçlar burada. Profesyonel çözümlerimizle hemen tanışın.</p>
                    <div style="display:flex;gap:16px;flex-wrap:wrap;">
                        <a href="#" style="padding:14px 32px;background-color:${BLUE};color:${ON_PRIMARY};text-decoration:none;border-radius:6px;font-weight:600;font-size:1.1rem;">Hemen Başlayın</a>
                        <a href="#" style="padding:14px 32px;background:transparent;color:${DARK};text-decoration:none;border-radius:6px;font-weight:600;font-size:1.1rem;border:1px solid ${BORDER};">Daha Fazla Bilgi</a>
                    </div>
                </div>
                <div style="flex:1;min-width:300px;">
                    <img src="${ph(800, 600, 'Görsel')}" alt="Hizmetlerimizi anlatan temsili görsel" width="800" height="600" loading="${m.get('imageLoading')}" style="width:100%;height:auto;border-radius:16px;box-shadow:0 20px 25px -5px rgba(0,0,0,0.1);display:block;">
                </div>
            </div>`
    });

    // ==================================================
    // 49. GOOGLE & PAYLAŞIM — link-out buttons with the addresses built for you
    // ==================================================
    // Every one of these is a plain link (one is a small script) to an address a
    // third party documents: nothing is loaded from Google or anyone else, so no
    // consent banner, no CSP entry and no layout shift come with them. The address
    // is always derived from traits — a Place ID, a date — never typed as a URL,
    // because the URL formats are exactly what people get wrong. A field left empty
    // takes the site's own value from Site Settings where there is one (the
    // domain, the address, the YouTube channel, the Google News address, the Place
    // ID); with none, the button links nowhere rather than somewhere made up. The
    // link is worked out again every time the page is opened, so a value set on
    // Site Settings later reaches a button already placed — on its next save. Brand colours
    // (Google, YouTube) are literals on purpose, as with the WhatsApp button: they
    // are a third party's, fixed in every theme.

    // Google's four-colour "G", as on its own sign-in and badge artwork.
    const GOOGLE_G = '<svg viewBox="0 0 48 48" width="18" height="18" aria-hidden="true" style="width:18px;height:18px;flex-shrink:0;display:block;">' +
        '<path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/>' +
        '<path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/>' +
        '<path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"/>' +
        '<path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.15 1.45-4.92 2.3-8.16 2.3-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/></svg>';

    // Google's own button look: light is white on a grey hairline, dark the inverse.
    const GOOGLE_BUTTON_THEMES = {
        light: { background: '#ffffff', color: '#1f1f1f', border: '1px solid #dadce0' },
        dark: { background: '#131314', color: '#e3e3e3', border: '1px solid #8e918f' }
    };
    const GOOGLE_BUTTON_STYLE = {
        display: 'inline-flex', 'align-items': 'center', gap: '10px', padding: '10px 18px',
        'border-radius': '999px', 'text-decoration': 'none', 'font-weight': '500', 'font-size': '15px',
        'line-height': '1.3', 'font-family': 'inherit', ...GOOGLE_BUTTON_THEMES.light
    };
    const THEME_OPTIONS = [{ id: 'light', name: LOCALE === 'tr' ? 'Açık' : 'Light' }, { id: 'dark', name: LOCALE === 'tr' ? 'Koyu' : 'Dark' }];
    const applyGoogleTheme = (m) => m.addStyle(GOOGLE_BUTTON_THEMES[m.get('btnTheme')] || GOOGLE_BUTTON_THEMES.light);
    const setLabel = (m, v) => { const span = m.find('.el-btn-label')[0]; if (span && v) span.components(v); };
    const hostOf = (text) => {
        const v = String(text || '').trim();
        if (!v) return '';
        try { return new URL(/^[a-z]+:\/\//i.test(v) ? v : 'https://' + v).hostname; } catch (e) { return ''; }
    };

    // ---- Google "preferred source" -------------------------------------------
    // Google Search lets a reader mark a site as a preferred source for Top
    // Stories (and AI Mode); this button takes them straight to that choice for
    // this site. The deeplink form from Google's publisher guide: works without
    // Google's script, which would otherwise load on every page carrying the
    // button. Only a domain or subdomain is accepted — never a path — so the
    // address is reduced to its host whatever is typed.
    const applyPreferredSourceHref = (m) => {
        const host = hostOf(m.get('psDomain')) || siteValue('publicSiteHost', '');
        m.addAttributes({ href: host ? 'https://www.google.com/preferences/source?q=' + encodeURIComponent(host) : '#' });
    };
    const PS_LABEL = CT('Google\'da tercih edilen kaynak olarak ekleyin', 'Add us as a preferred source on Google');

    smart({
        id: 'elevare-preferred-source', label: 'Google Preferred Source', icon: 'bi-google',
        tag: 'a',
        attrs: { href: '#', target: '_blank', rel: 'noopener noreferrer', class: 'el-google-btn' },
        style: GOOGLE_BUTTON_STYLE,
        traits: [
            {
                type: HINT_TEXT_TRAIT, name: 'psDomain', label: LOCALE === 'tr' ? 'Alan adı' : 'Domain', value: '', changeProp: 1,
                placeholder: siteValue('publicSiteHost', 'ornek.com'),
                hint: LOCALE === 'tr'
                    ? 'Boş bırakın: sitenin kendi alan adı kullanılır. Yalnızca başka bir site için yazın — sadece alan adı, yol değil (ör. ornek.com).'
                    : 'Leave empty to use this site\'s own domain. Fill in only for another site — the domain alone, no path (e.g. example.com).'
            },
            { type: 'text', name: 'btnLabel', label: LOCALE === 'tr' ? 'Buton Yazısı' : 'Label', value: PS_LABEL, changeProp: 1 },
            { type: 'select', name: 'btnTheme', label: LOCALE === 'tr' ? 'Tema' : 'Theme', value: 'light', changeProp: 1, options: THEME_OPTIONS }
        ],
        on: {
            psDomain: (m) => applyPreferredSourceHref(m),
            btnLabel: (m, v) => setLabel(m, v),
            btnTheme: (m) => applyGoogleTheme(m)
        },
        // Derived, never typed: recomputed on every init so the site's address, set
        // on Site Settings after the button was placed, still reaches it.
        onInit: (m) => applyPreferredSourceHref(m),
        template: (m) => `${GOOGLE_G}<span class="el-btn-label">${m.get('btnLabel') || PS_LABEL}</span>`
    });

    // ---- Google News "follow" ---------------------------------------------------
    // The publication's own Google News address, which only Publisher Center knows —
    // so it is pasted, and only checked for being a Google News address.
    const applyGoogleNewsHref = (m) => {
        const v = String(m.get('gnUrl') || '').trim() || contactValue('googleNews', '');
        m.addAttributes({ href: /^https:\/\/news\.google\.com\//i.test(v) ? v : '#' });
    };
    const GN_LABEL = CT('Google Haberler\'de takip edin', 'Follow us on Google News');

    smart({
        id: 'elevare-google-news', label: 'Follow on Google News', icon: 'bi-newspaper',
        tag: 'a',
        attrs: { href: '#', target: '_blank', rel: 'noopener noreferrer', class: 'el-google-btn' },
        style: GOOGLE_BUTTON_STYLE,
        traits: [
            {
                type: HINT_TEXT_TRAIT, name: 'gnUrl', label: LOCALE === 'tr' ? 'Google Haberler adresi' : 'Google News address', value: '', changeProp: 1,
                placeholder: 'https://news.google.com/publications/…',
                hint: LOCALE === 'tr'
                    ? 'Google News Publisher Center\'da yayınınızın sayfasındaki adres. Boşsa Site Ayarları\'ndaki Google Haberler adresi kullanılır; o da yoksa buton bir yere bağlanmaz.'
                    : 'The address of your publication\'s page in Google News Publisher Center. Empty: the Google News address from Site Settings; with none there, the button links nowhere.'
            },
            { type: 'text', name: 'btnLabel', label: LOCALE === 'tr' ? 'Buton Yazısı' : 'Label', value: GN_LABEL, changeProp: 1 },
            { type: 'select', name: 'btnTheme', label: LOCALE === 'tr' ? 'Tema' : 'Theme', value: 'light', changeProp: 1, options: THEME_OPTIONS }
        ],
        on: {
            gnUrl: (m) => applyGoogleNewsHref(m),
            btnLabel: (m, v) => setLabel(m, v),
            btnTheme: (m) => applyGoogleTheme(m)
        },
        onInit: (m) => applyGoogleNewsHref(m),
        template: (m) => `${GOOGLE_G}<span class="el-btn-label">${m.get('btnLabel') || GN_LABEL}</span>`
    });

    // ---- Google review --------------------------------------------------------
    // Opens the "write a review" box of a Google Business Profile directly — the
    // shortest way from a happy customer to a review. Needs the business's Place ID
    // (Google's "Place ID Finder" shows it; it starts with "ChIJ").
    const applyGoogleReviewHref = (m) => {
        const id = String(m.get('grPlaceId') || '').trim() || contactValue('googlePlaceId', '');
        m.addAttributes({ href: id ? 'https://search.google.com/local/writereview?placeid=' + encodeURIComponent(id) : '#' });
    };
    const GR_LABEL = CT('Bizi Google\'da değerlendirin', 'Review us on Google');
    const STAR = '<svg viewBox="0 0 16 16" width="14" height="14" fill="#fbbc04" aria-hidden="true" style="width:14px;height:14px;display:block;">' + ICONS['star-fill'] + '</svg>';

    smart({
        id: 'elevare-google-review', label: 'Google Review', icon: 'bi-star-half',
        tag: 'a',
        attrs: { href: '#', target: '_blank', rel: 'noopener noreferrer', class: 'el-google-btn' },
        style: GOOGLE_BUTTON_STYLE,
        traits: [
            {
                type: HINT_TEXT_TRAIT, name: 'grPlaceId', label: 'Google Place ID', value: '', changeProp: 1,
                placeholder: 'ChIJ…',
                hint: LOCALE === 'tr'
                    ? 'Google İşletme Profilinizin kimliği; Google\'ın "Place ID Finder" sayfasında işletmenizi arayınca görünür. Boşsa Site Ayarları\'ndaki Place ID kullanılır; o da yoksa buton bir yere bağlanmaz.'
                    : 'Your Google Business Profile\'s ID; Google\'s "Place ID Finder" page shows it when you search for the business. Empty: the Place ID from Site Settings; with none there, the button links nowhere.'
            },
            { type: 'text', name: 'btnLabel', label: LOCALE === 'tr' ? 'Buton Yazısı' : 'Label', value: GR_LABEL, changeProp: 1 },
            { type: 'select', name: 'btnTheme', label: LOCALE === 'tr' ? 'Tema' : 'Theme', value: 'light', changeProp: 1, options: THEME_OPTIONS }
        ],
        on: {
            grPlaceId: (m) => applyGoogleReviewHref(m),
            btnLabel: (m, v) => setLabel(m, v),
            btnTheme: (m) => applyGoogleTheme(m)
        },
        onInit: (m) => applyGoogleReviewHref(m),
        template: (m) => `${GOOGLE_G}<span class="el-btn-label">${m.get('btnLabel') || GR_LABEL}</span>` +
            `<span aria-hidden="true" style="display:inline-flex;gap:1px;">${STAR}${STAR}${STAR}${STAR}${STAR}</span>`
    });

    // ---- Directions -------------------------------------------------------------
    // Google Maps' documented "Maps URLs" directions form: opens the Maps app on a
    // phone, the website elsewhere, with the route to the business already asked for.
    const applyDirectionsHref = (m) => {
        const dest = String(m.get('dirDestination') || '').trim() || siteAddressLine('');
        m.addAttributes({ href: dest ? 'https://www.google.com/maps/dir/?api=1&destination=' + encodeURIComponent(dest) : '#' });
    };
    const DIR_LABEL = CT('Yol tarifi al', 'Get directions');

    smart({
        id: 'elevare-directions', label: 'Get Directions', icon: 'bi-sign-turn-right',
        tag: 'a',
        attrs: { href: '#', target: '_blank', rel: 'noopener noreferrer' },
        style: {
            display: 'inline-flex', 'align-items': 'center', gap: '8px', padding: '12px 22px', 'border-radius': '8px',
            background: BLUE, color: ON_PRIMARY, 'text-decoration': 'none', 'font-weight': '600', 'font-size': '1rem'
        },
        traits: [
            {
                type: HINT_TEXT_TRAIT, name: 'dirDestination', label: LOCALE === 'tr' ? 'Varış' : 'Destination', value: '', changeProp: 1,
                placeholder: siteAddressLine('41.0082,28.9784'),
                hint: LOCALE === 'tr'
                    ? 'Adres ya da enlem,boylam (ör. 41.0082,28.9784). Boşsa Site Ayarları\'ndaki adres kullanılır; o da yoksa buton bir yere bağlanmaz.'
                    : 'An address, or lat,lng (e.g. 41.0082,28.9784). Empty: the address from Site Settings; with none there, the button links nowhere.'
            },
            { type: 'text', name: 'btnLabel', label: LOCALE === 'tr' ? 'Buton Yazısı' : 'Label', value: DIR_LABEL, changeProp: 1 }
        ],
        on: {
            dirDestination: (m) => applyDirectionsHref(m),
            btnLabel: (m, v) => setLabel(m, v)
        },
        onInit: (m) => applyDirectionsHref(m),
        template: (m) => `${icon('sign-turn-right-fill')}<span class="el-btn-label">${m.get('btnLabel') || DIR_LABEL}</span>`
    });

    // ---- YouTube subscribe ------------------------------------------------------
    // The channel page with ?sub_confirmation=1, which opens YouTube's own
    // "Subscribe?" prompt — the documented way to link a subscribe action.
    const applyYoutubeHref = (m) => {
        const channel = String(m.get('ytChannel') || '').trim() || contactValue('youtube', '');
        if (!/^https:\/\/(www\.|m\.)?youtube\.com\//i.test(channel)) { m.addAttributes({ href: '#' }); return; }
        const url = channel.replace(/[?&]sub_confirmation=1/, '');
        m.addAttributes({ href: url + (url.includes('?') ? '&' : '?') + 'sub_confirmation=1' });
    };
    const YT_LABEL = CT('YouTube\'da abone olun', 'Subscribe on YouTube');

    smart({
        id: 'elevare-youtube-subscribe', label: 'YouTube Subscribe', icon: 'bi-youtube',
        tag: 'a',
        attrs: { href: '#', target: '_blank', rel: 'noopener noreferrer' },
        style: {
            display: 'inline-flex', 'align-items': 'center', gap: '8px', padding: '10px 20px', 'border-radius': '999px',
            background: '#ff0000', color: '#ffffff', 'text-decoration': 'none', 'font-weight': '600', 'font-size': '15px'
        },
        traits: [
            {
                type: HINT_TEXT_TRAIT, name: 'ytChannel', label: LOCALE === 'tr' ? 'Kanal adresi' : 'Channel address', value: '', changeProp: 1,
                placeholder: 'https://www.youtube.com/@kanal',
                hint: LOCALE === 'tr'
                    ? 'Kanalınızın tam adresi. Boşsa Site Ayarları\'ndaki YouTube adresi kullanılır; o da yoksa buton bir yere bağlanmaz.'
                    : 'Your channel\'s full address. Empty: the YouTube address from Site Settings; with none there, the button links nowhere.'
            },
            { type: 'text', name: 'btnLabel', label: LOCALE === 'tr' ? 'Buton Yazısı' : 'Label', value: YT_LABEL, changeProp: 1 }
        ],
        on: {
            ytChannel: (m) => applyYoutubeHref(m),
            btnLabel: (m, v) => setLabel(m, v)
        },
        onInit: (m) => applyYoutubeHref(m),
        template: (m) => `${icon('youtube', 'font-size:1.2rem;')}<span class="el-btn-label">${m.get('btnLabel') || YT_LABEL}</span>`
    });

    // ---- Add to calendar --------------------------------------------------------
    // Two links for one event: Google Calendar's documented "TEMPLATE" address, and
    // an .ics file (Apple Calendar, Outlook, anything else) carried in the link
    // itself. Times are entered in the editor's own time zone and stored as UTC, so
    // every reader's calendar shows the event at the right local time.
    const calUtc = (value) => {
        const d = new Date(value);
        return isNaN(d.getTime()) ? '' : d.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
    };
    const icsText = (v) => String(v || '').replace(/\\/g, '\\\\').replace(/;/g, '\\;').replace(/,/g, '\\,').replace(/\r?\n/g, '\\n');
    const applyCalendarLinks = (m) => {
        const start = calUtc(m.get('calStart'));
        const endValue = m.get('calEnd') || (m.get('calStart') ? new Date(new Date(m.get('calStart')).getTime() + 3600000).toISOString() : '');
        const end = calUtc(endValue) || start;
        const title = String(m.get('calTitle') || '').trim();
        const place = String(m.get('calLocation') || '').trim();
        const details = String(m.get('calDetails') || '').trim();
        const google = findModels(m, '[data-el-cal="google"]')[0];
        const ics = findModels(m, '[data-el-cal="ics"]')[0];
        if (!start || !title) {
            if (google) google.addAttributes({ href: '#' });
            if (ics) ics.addAttributes({ href: '#' });
            return;
        }
        const q = new URLSearchParams({ action: 'TEMPLATE', text: title, dates: start + '/' + end });
        if (details) q.set('details', details);
        if (place) q.set('location', place);
        if (google) google.addAttributes({ href: 'https://calendar.google.com/calendar/render?' + q.toString() });
        const file = ['BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//Elevare//Calendar//TR', 'BEGIN:VEVENT',
            'UID:' + start + '-' + title.replace(/[^A-Za-z0-9]/g, '').slice(0, 24) + '@elevare',
            'DTSTAMP:' + start, 'DTSTART:' + start, 'DTEND:' + end, 'SUMMARY:' + icsText(title),
            place ? 'LOCATION:' + icsText(place) : null, details ? 'DESCRIPTION:' + icsText(details) : null,
            'END:VEVENT', 'END:VCALENDAR'].filter(Boolean).join('\r\n');
        if (ics) ics.addAttributes({ href: 'data:text/calendar;charset=utf-8,' + encodeURIComponent(file), download: 'etkinlik.ics' });
    };
    const calLinkStyle = `display:inline-flex;align-items:center;gap:8px;padding:10px 18px;border-radius:8px;text-decoration:none;font-weight:600;font-size:0.95rem;border:1px solid ${BORDER};background:${SURFACE};color:${DARK};`;

    smart({
        id: 'elevare-add-calendar', label: 'Add to Calendar', icon: 'bi-calendar-plus',
        tag: 'div',
        attrs: { role: 'group', 'aria-label': CT('Takvime ekle', 'Add to calendar') },
        style: { display: 'flex', gap: '10px', 'flex-wrap': 'wrap', padding: '12px 0' },
        traits: [
            { type: 'text', name: 'calTitle', label: LOCALE === 'tr' ? 'Etkinlik adı' : 'Event name', value: '', changeProp: 1 },
            { type: DATETIME_TRAIT, name: 'calStart', label: LOCALE === 'tr' ? 'Başlangıç' : 'Starts', value: '', changeProp: 1 },
            { type: DATETIME_TRAIT, name: 'calEnd', label: LOCALE === 'tr' ? 'Bitiş (boşsa 1 saat)' : 'Ends (empty: 1 hour)', value: '', changeProp: 1 },
            { type: 'text', name: 'calLocation', label: LOCALE === 'tr' ? 'Yer' : 'Location', value: siteAddressLine(''), changeProp: 1 },
            { type: 'text', name: 'calDetails', label: LOCALE === 'tr' ? 'Açıklama' : 'Details', value: '', changeProp: 1 }
        ],
        on: ['calTitle', 'calStart', 'calEnd', 'calLocation', 'calDetails'].reduce((acc, k) => { acc[k] = (m) => applyCalendarLinks(m); return acc; }, {}),
        onInit: (m) => applyCalendarLinks(m),
        template: () => `
            <a data-el-cal="google" href="#" target="_blank" rel="noopener noreferrer" style="${calLinkStyle}">${GOOGLE_G}<span class="el-btn-label">${CT('Google Takvim', 'Google Calendar')}</span></a>
            <a data-el-cal="ics" href="#" style="${calLinkStyle}">${icon('calendar-event')}<span class="el-btn-label">Apple / Outlook (.ics)</span></a>`
    });

    // ---- Share buttons ----------------------------------------------------------
    // The page's own address is only known on the page, so a small script fills
    // the links in: the canonical address when there is one (a share of "?utm=…"
    // should not spread the utm), else the current one. Each network's share
    // address is its documented one; the copy button uses the clipboard, and the
    // "Paylaş" button the device's own share sheet where there is one (phones).
    const shareScript = function () {
        var root = this;
        var canonical = document.querySelector('link[rel="canonical"]');
        var url = (canonical && canonical.href) || location.href.split('#')[0];
        var title = document.title;
        var u = encodeURIComponent(url), t = encodeURIComponent(title);
        var targets = {
            x: 'https://x.com/intent/post?url=' + u + '&text=' + t,
            linkedin: 'https://www.linkedin.com/sharing/share-offsite/?url=' + u,
            facebook: 'https://www.facebook.com/sharer/sharer.php?u=' + u,
            whatsapp: 'https://wa.me/?text=' + t + '%20' + u,
            telegram: 'https://t.me/share/url?url=' + u + '&text=' + t,
            email: 'mailto:?subject=' + t + '&body=' + u
        };
        root.querySelectorAll('[data-elevare-share]').forEach(function (a) {
            var href = targets[a.getAttribute('data-elevare-share')];
            if (href) a.setAttribute('href', href);
        });
        var copy = root.querySelector('[data-elevare-share-copy]');
        if (copy) {
            copy.addEventListener('click', function () {
                if (!navigator.clipboard) return;
                navigator.clipboard.writeText(url).then(function () {
                    copy.setAttribute('data-copied', copy.getAttribute('data-elevare-copied-text') || 'OK');
                    setTimeout(function () { copy.removeAttribute('data-copied'); }, 2000);
                });
            });
        }
        var native = root.querySelector('[data-elevare-share-native]');
        if (native) {
            if (navigator.share) {
                native.hidden = false;
                native.addEventListener('click', function () { navigator.share({ title: title, url: url }).catch(function () { }); });
            } else {
                native.hidden = true;
            }
        }
    };

    const SHARE_NETWORKS = [
        { key: 'x', label: 'X', icon: 'twitter-x' },
        { key: 'linkedin', label: 'LinkedIn', icon: 'linkedin' },
        { key: 'facebook', label: 'Facebook', icon: 'facebook' },
        { key: 'whatsapp', label: 'WhatsApp', icon: 'whatsapp' },
        { key: 'telegram', label: 'Telegram', icon: 'telegram' },
        { key: 'email', label: 'Email', trLabel: 'E-posta', icon: 'envelope' }
    ];
    const shareItemStyle = `width:40px;height:40px;display:inline-flex;align-items:center;justify-content:center;border-radius:50%;background:${SURFACE2};color:${DARK};font-size:1.05rem;text-decoration:none;border:0;cursor:pointer;`;
    const shareToggle = (m, selector, on) => {
        const el = m.find(selector)[0];
        if (el) el.addStyle({ display: on === false || on === 'false' ? 'none' : 'inline-flex' });
    };

    smart({
        id: 'elevare-share', label: 'Share Buttons', icon: 'bi-share-fill',
        tag: 'div',
        attrs: { class: 'elevare-share', role: 'group', 'aria-label': CT('Bu sayfayı paylaş', 'Share this page') },
        style: { display: 'flex', 'align-items': 'center', gap: '10px', 'flex-wrap': 'wrap', padding: '16px 0' },
        script: shareScript,
        traits: [
            { type: 'text', name: 'shareLabel', label: LOCALE === 'tr' ? 'Başlık' : 'Label', value: CT('Paylaş:', 'Share:'), changeProp: 1 },
            ...SHARE_NETWORKS.map((n) => ({ type: 'checkbox', name: 'share_' + n.key, label: LOCALE === 'tr' && n.trLabel ? n.trLabel : n.label, value: true, changeProp: 1 })),
            { type: 'checkbox', name: 'share_copy', label: LOCALE === 'tr' ? 'Bağlantıyı kopyala' : 'Copy link', value: true, changeProp: 1 }
        ],
        on: {
            shareLabel: (m, v) => { const l = m.find('.el-share-label')[0]; if (l) l.components(v || ''); },
            share_copy: (m, v) => shareToggle(m, '[data-elevare-share-copy]', v),
            ...SHARE_NETWORKS.reduce((acc, n) => { acc['share_' + n.key] = (m, v) => shareToggle(m, `[data-elevare-share="${n.key}"]`, v); return acc; }, {})
        },
        template: (m) => `
            <span class="el-share-label" style="font-weight:600;color:${DARK};margin-right:4px;">${m.get('shareLabel') || CT('Paylaş:', 'Share:')}</span>
            ${SHARE_NETWORKS.map((n) => `<a data-elevare-share="${n.key}" href="#"${n.key === 'email' ? '' : ' target="_blank" rel="noopener noreferrer"'} aria-label="${n.key === 'email' ? CT('E-posta ile paylaş', 'Share by email') : CT(n.label + ' ile paylaş', 'Share on ' + n.label)}" style="${shareItemStyle}">${icon(n.icon)}</a>`).join('')}
            <button type="button" data-elevare-share-copy data-elevare-copied-text="${CT('Kopyalandı', 'Copied')}" aria-label="${CT('Bağlantıyı kopyala', 'Copy link')}" style="${shareItemStyle}">${icon('link-45deg')}</button>
            <button type="button" data-elevare-share-native hidden aria-label="${CT('Paylaş', 'Share')}" style="${shareItemStyle}">${icon('share')}</button>`
    });

    blockCss(
        '.elevare-share [data-elevare-share-copy]{position:relative}' +
        '.elevare-share [data-copied]::after{content:attr(data-copied);position:absolute;bottom:calc(100% + 6px);left:50%;transform:translateX(-50%);' +
        'white-space:nowrap;font-size:12px;font-weight:600;padding:3px 8px;border-radius:6px;background:#111827;color:#fff;pointer-events:none}' +
        '.elevare-share [hidden]{display:none !important}'
    );

    // ==================================================
    // 50. BLOG & READING — code, neighbours, related posts, figures, progress
    // ==================================================
    // Canvas-only looks for the blocks below: the public site gets its styling from
    // the server (the code theme — ContentEnhancer) or from the runtime
    // (elevare-interactions.js), so the canvas needs its own to show them as they
    // will be. Written into the canvas document, never saved with the page.
    const BLOG_CANVAS_STYLE_ID = 'elevare-blog-canvas-css';
    const blogCanvasCss = () => {
        const q = (t) => JSON.stringify(t);
        return '.elevare-code{border-radius:10px;overflow:hidden;background:#1e1e1e;color:#d4d4d4;margin:20px 0;font-size:14px}' +
            '.elevare-code .el-code-head{display:flex;align-items:center;gap:10px;padding:8px 14px;background:#2d2d2d;color:#a0a0a0;font-size:12px}' +
            '.elevare-code .el-code-lang{font-weight:700;text-transform:uppercase;letter-spacing:.04em}' +
            '.elevare-code .el-code-copy{margin-left:auto;background:transparent;border:1px solid #4a4a4a;color:#cfcfcf;border-radius:6px;padding:3px 10px;font-size:12px}' +
            '.elevare-code pre{margin:0;padding:16px 18px;overflow:auto;line-height:1.6}' +
            '.elevare-code code{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;white-space:pre;color:#d4d4d4;background:none}' +
            // The token colours — the same as the public site's (ContentEnhancer).
            '.elevare-code .tk-k,.elevare-code .tk-g{color:#569cd6}.elevare-code .tk-s{color:#ce9178}.elevare-code .tk-c{color:#6a9955;font-style:italic}' +
            '.elevare-code .tk-n{color:#b5cea8}.elevare-code .tk-t{color:#4ec9b0}.elevare-code .tk-f{color:#dcdcaa}.elevare-code .tk-p{color:#9cdcfe}' +
            // All tab panels at once, each labelled, so every one can be edited.
            `[data-elevare-tab-panel]{display:block !important;border:1px dashed #94a3b8;border-radius:6px;margin-top:8px;position:relative}` +
            `[data-elevare-tab-panel]::before{content:${q(LOCALE === 'tr' ? 'Sekme ' : 'Tab ')} attr(data-elevare-tab-panel);position:absolute;top:-9px;left:8px;font:600 10px/1.7 system-ui,sans-serif;padding:0 6px;border-radius:4px;background:#64748b;color:#fff}` +
            // The progress bar and the back-to-top button are hidden or empty until
            // the visitor scrolls; in the canvas they show, in place, to be styled.
            '[data-elevare-reading-progress]{position:relative !important}' +
            '[data-elevare-reading-progress-bar]{width:35% !important}' +
            '[data-elevare-back-to-top]{opacity:1 !important;visibility:visible !important}';
    };
    const installBlogCanvasCss = () => {
        const doc = editor.Canvas.getDocument && editor.Canvas.getDocument();
        if (!doc || !doc.head || doc.getElementById(BLOG_CANVAS_STYLE_ID)) return;
        const style = doc.createElement('style');
        style.id = BLOG_CANVAS_STYLE_ID;
        style.textContent = blogCanvasCss();
        doc.head.appendChild(style);
    };
    editor.on('canvas:frame:load', () => setTimeout(installBlogCanvasCss, 0));
    editor.on('load', installBlogCanvasCss);

    // ---- Code block ------------------------------------------------------------
    // The code is kept as plain text in <pre><code>: the public site colours it
    // (CodeHighlighter), the copy button is the runtime's. Typing code on the
    // canvas would go through the rich-text editor, which rewrites what it touches,
    // so the code is edited in a plain text box — "Kodu düzenle" — instead.
    const CODE_LANGUAGES = [
        { id: 'csharp', name: 'C#' }, { id: 'javascript', name: 'JavaScript' }, { id: 'typescript', name: 'TypeScript' },
        { id: 'json', name: 'JSON' }, { id: 'html', name: 'HTML' }, { id: 'xml', name: 'XML' }, { id: 'css', name: 'CSS' },
        { id: 'sql', name: 'SQL' }, { id: 'bash', name: 'Bash' }, { id: 'powershell', name: 'PowerShell' },
        { id: 'python', name: 'Python' }, { id: 'plaintext', name: LOCALE === 'tr' ? 'Düz metin' : 'Plain text' }
    ];
    const codeLanguageName = (id) => (CODE_LANGUAGES.find((l) => l.id === id) || CODE_LANGUAGES[0]).name;
    const escapeHtml = (t) => String(t).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    const SAMPLE_CODE = 'public static int CountBits(int value)\n{\n    int count = 0;\n    while (value != 0)\n    {\n        count += value & 1;\n        value >>= 1;\n    }\n    return count;\n}';

    // The block's parts, found in its component tree rather than with find(): find
    // asks the canvas DOM, which does not exist yet while the block is being set up
    // (init, onLoad) — exactly when the lock below has to go on. With find, it never
    // did: a double-click opened the rich-text editor inside the code, which
    // rewrote its markup.
    const codeOf = (m) => descendants(m).find((c) => c.get('tagName') === 'code');
    const plainText = (c) => {
        const tmp = document.createElement('div');
        tmp.innerHTML = c ? c.getInnerHTML() : '';
        return tmp.textContent;
    };
    const codeTextOf = (m) => plainText(codeOf(m));
    const setCode = (m, text) => {
        const code = codeOf(m);
        if (code) code.components(escapeHtml(text));
    };
    const lockCode = (m) => {
        descendants(m)
            .filter((c) => c.get('tagName') === 'pre' || c.get('tagName') === 'code')
            .forEach((c) => c.set({ editable: false, droppable: false }));
    };

    // Colours the code on the canvas the way the public site will. The coloured
    // HTML goes into the canvas element only; the model keeps the plain text, which
    // is what gets saved.
    const paintCode = (m) => {
        const code = codeOf(m);
        const el = code && code.getEl && code.getEl();
        const ge = window.grapesEditor;
        if (!el || !ge || typeof ge.highlightCode !== 'function') return;
        const text = el.textContent;
        ge.highlightCode(text, m.get('codeLanguage') || 'csharp').then((html) => {
            // Not if the code was redrawn while the answer was on its way.
            if (html != null && code.getEl() === el && el.textContent === text) el.innerHTML = html;
        });
    };

    const openCodeEditor = (m) => {
        const T = LOCALE === 'tr'
            ? { title: 'Kodu düzenle', language: 'Dil', save: 'Kaydet', cancel: 'Vazgeç' }
            : { title: 'Edit code', language: 'Language', save: 'Save', cancel: 'Cancel' };
        const wrap = document.createElement('div');
        wrap.className = 'elevare-code-editor';
        const select = document.createElement('select');
        select.style.cssText = 'padding:4px 8px;border-radius:6px;border:1px solid #475569;background:#1e1e1e;color:#d4d4d4;';
        CODE_LANGUAGES.forEach((l) => { const o = document.createElement('option'); o.value = l.id; o.textContent = l.name; select.appendChild(o); });
        select.value = m.get('codeLanguage') || 'csharp';
        const area = document.createElement('textarea');
        area.value = codeTextOf(m);
        area.spellcheck = false;
        area.setAttribute('aria-label', T.title);
        area.style.cssText = 'width:100%;min-height:360px;font:13px/1.55 ui-monospace,SFMono-Regular,Consolas,monospace;tab-size:4;white-space:pre;padding:12px;border-radius:6px;border:1px solid #475569;background:#1e1e1e;color:#d4d4d4;box-sizing:border-box;';
        // Tab indents instead of leaving the box — this is code.
        area.addEventListener('keydown', (e) => {
            if (e.key !== 'Tab') return;
            e.preventDefault();
            const start = area.selectionStart, end = area.selectionEnd;
            area.value = area.value.slice(0, start) + '    ' + area.value.slice(end);
            area.selectionStart = area.selectionEnd = start + 4;
        });
        const row = document.createElement('div');
        row.style.cssText = 'display:flex;gap:8px;align-items:center;margin:0 0 10px;';
        const label = document.createElement('label');
        label.textContent = T.language;
        row.append(label, select);
        const actions = document.createElement('div');
        actions.style.cssText = 'display:flex;gap:8px;justify-content:flex-end;margin-top:10px;';
        const cancel = document.createElement('button');
        cancel.type = 'button';
        cancel.className = 'gjs-btn-prim';
        cancel.textContent = T.cancel;
        const save = document.createElement('button');
        save.type = 'button';
        save.className = 'gjs-btn-prim';
        save.textContent = T.save;
        actions.append(cancel, save);
        wrap.append(row, area, actions);
        cancel.addEventListener('click', () => editor.Modal.close());
        save.addEventListener('click', () => {
            setCode(m, area.value.replace(/\s+$/, ''));
            m.set('codeLanguage', select.value);
            lockCode(m);
            editor.Modal.close();
            setTimeout(() => paintCode(m), 0);
        });
        editor.Modal.open({ title: T.title, content: wrap });
        setTimeout(() => area.focus(), 50);
    };

    // A button in the trait panel: "Kodu düzenle".
    const CODE_EDIT_TRAIT = 'elevare-code-edit';
    editor.TraitManager.addType(CODE_EDIT_TRAIT, {
        noLabel: true,
        createInput({ trait }) {
            const btn = document.createElement('button');
            btn.type = 'button';
            btn.className = 'gjs-btn-prim';
            btn.style.cssText = 'width:100%;';
            btn.textContent = LOCALE === 'tr' ? 'Kodu düzenle' : 'Edit code';
            btn.addEventListener('click', () => { const t = trait.target || trait.get('target'); if (t) openCodeEditor(t); });
            return btn;
        }
    });

    smart({
        id: 'elevare-code', label: 'Code Block', icon: 'bi-code-square',
        tag: 'div',
        attrs: { class: 'elevare-code', 'data-elevare-code': '', 'data-language': 'csharp', 'data-line-numbers': 'false' },
        style: {},
        traits: [
            { type: CODE_EDIT_TRAIT, name: 'codeEdit', label: ' ', changeProp: 1 },
            { type: 'select', name: 'codeLanguage', label: LOCALE === 'tr' ? 'Dil' : 'Language', value: 'csharp', changeProp: 1, options: CODE_LANGUAGES },
            { type: 'text', name: 'codeFile', label: LOCALE === 'tr' ? 'Dosya adı (isteğe bağlı)' : 'File name (optional)', value: '', changeProp: 1 },
            { type: 'checkbox', name: 'codeLineNumbers', label: LOCALE === 'tr' ? 'Satır numaraları' : 'Line numbers', value: false, changeProp: 1 }
        ],
        on: {
            codeLanguage: (m, v) => {
                m.addAttributes({ 'data-language': v || 'csharp' });
                const lang = m.find('.el-code-lang')[0];
                if (lang) lang.components(codeLanguageName(v));
                const code = codeOf(m);
                if (code) code.setClass(['language-' + (v || 'csharp')]);
                paintCode(m);
            },
            codeFile: (m, v) => {
                const file = m.find('.el-code-file')[0];
                if (file) file.components(escapeHtml(v || ''));
            },
            codeLineNumbers: (m, v) => m.addAttributes({ 'data-line-numbers': v === true || v === 'true' ? 'true' : 'false' })
        },
        onLoad: (m) => {
            const a = m.getAttributes();
            m.set({ codeLanguage: a['data-language'] || 'csharp', codeLineNumbers: a['data-line-numbers'] === 'true' }, { silent: true });
            const file = descendants(m).find((c) => c.getClasses().indexOf('el-code-file') >= 0);
            if (file) m.set('codeFile', plainText(file) || '', { silent: true });
            lockCode(m);
        },
        view: {
            onRender() {
                // Double-click opens "Kodu düzenle": the code is never typed on the
                // canvas. Once per view — a redraw keeps the same element.
                if (!this.elevareCodeDblClick) {
                    this.elevareCodeDblClick = true;
                    this.el.addEventListener('dblclick', (e) => {
                        e.preventDefault();
                        e.stopPropagation();
                        openCodeEditor(this.model);
                    });
                }
                paintCode(this.model);
            }
        },
        template: () => `
            <div class="el-code-head">
                <span class="el-code-lang">C#</span>
                <span class="el-code-file"></span>
                <button type="button" class="el-code-copy" data-elevare-code-copy data-copied-text="${CT('Kopyalandı', 'Copied')}" aria-label="${CT('Kodu kopyala', 'Copy code')}">${CT('Kopyala', 'Copy')}</button>
            </div>
            <pre><code class="language-csharp">${escapeHtml(SAMPLE_CODE)}</code></pre>`
    });

    // ---- Previous / next post --------------------------------------------------
    // The public site fills both links (AdjacentPageResolutionService): the pages
    // published just before and after this one under the same parent.
    const adjLinkStyle = `flex:1;min-width:220px;max-width:48%;display:flex;flex-direction:column;gap:4px;padding:16px 18px;border:1px solid ${BORDER};border-radius:10px;text-decoration:none;color:${DARK};background:${SURFACE};`;
    smart({
        id: 'elevare-prevnext', label: 'Previous / Next Post', icon: 'bi-arrow-left-right',
        tag: 'nav',
        attrs: { class: 'elevare-prevnext', 'aria-label': CT('Yazılar arasında gezinme', 'Post navigation') },
        style: { display: 'flex', 'justify-content': 'space-between', gap: '16px', 'flex-wrap': 'wrap', 'max-width': '760px', margin: '40px auto', padding: '0 20px' },
        template: () => `
            <a data-elevare-adjacent="prev" href="#" style="${adjLinkStyle}">
                <span class="el-adj-label" style="font-size:0.8rem;color:${MUTED};">${CT('← Önceki yazı', '← Previous post')}</span>
                <span data-elevare-field="title" style="font-weight:700;">${CT('Önceki yazının başlığı', 'Previous post title')}</span>
            </a>
            <a data-elevare-adjacent="next" href="#" style="${adjLinkStyle}text-align:right;align-items:flex-end;">
                <span class="el-adj-label" style="font-size:0.8rem;color:${MUTED};">${CT('Sonraki yazı →', 'Next post →')}</span>
                <span data-elevare-field="title" style="font-weight:700;">${CT('Sonraki yazının başlığı', 'Next post title')}</span>
            </a>`
    });

    // ---- Related posts: a page listing, preset ----------------------------------
    // Not a block of its own: the Sayfa Listesi block set to "this page's siblings,
    // only those sharing a tag, three of them, nothing else" — everything the
    // listing can do (preview, styling, fields) is there to adjust.
    bm.add('elevare-related-posts', {
        label: `<i class="bi bi-collection fs-2"></i><br>${blockLabel('elevare-related-posts', 'Related Posts')}`,
        category: blockCategory('elevare-related-posts'),
        content: {
            type: 'elevare-page-listing-type',
            attributes: {
                'data-elevare-source': 'siblings',
                'data-elevare-related-tags': 'true',
                'data-elevare-items-per-page': '3',
                'data-elevare-show-search': 'false',
                'data-elevare-show-tag-filter': 'false',
                'data-elevare-show-pagination': 'false'
            }
        }
    });

    // ---- Figure with caption ---------------------------------------------------
    // <figure> + <figcaption>: the caption belongs to the picture for screen readers
    // and image search alike. "Tıklayınca büyüt" opens it full size (runtime).
    const applyFigureZoom = (m, on) => {
        if (on) m.addAttributes({ 'data-elevare-lightbox': '' });
        else m.removeAttributes('data-elevare-lightbox');
    };
    smart({
        id: 'elevare-figure', label: 'Figure with Caption', icon: 'bi-image',
        tag: 'figure',
        attrs: { class: 'elevare-figure', 'data-elevare-lightbox': '' },
        style: { margin: '24px auto', 'max-width': '760px', padding: '0 20px' },
        traits: [
            { type: 'checkbox', name: 'zoom', label: LOCALE === 'tr' ? 'Tıklayınca büyüt' : 'Zoom on click', value: true, changeProp: 1 },
            { type: 'select', name: 'imageLoading', label: 'Görsel Yükleme', options: [{ id: 'lazy', name: 'Lazy' }, { id: 'eager', name: 'Eager' }], value: 'lazy', changeProp: 1 }
        ],
        on: {
            zoom: (m, v) => applyFigureZoom(m, v === true || v === 'true'),
            imageLoading: (m, v) => { const img = m.find('img')[0]; if (img) img.addAttributes({ loading: v }); }
        },
        onLoad: (m) => m.set('zoom', m.getAttributes()['data-elevare-lightbox'] !== undefined, { silent: true }),
        template: (m) => `
            <img src="${ph(1200, 675, 'Görsel')}" alt="${CT('Görseli anlatan kısa açıklama', 'A short description of the image')}" width="1200" height="675" loading="${m.get('imageLoading')}" style="width:100%;height:auto;display:block;border-radius:10px;">
            <figcaption style="margin-top:8px;font-size:0.9rem;color:${MUTED};text-align:center;">${CT('Görsel altyazısı', 'Image caption')}</figcaption>`
    });

    // ---- Reading progress bar --------------------------------------------------
    smart({
        id: 'elevare-reading-progress', label: 'Reading Progress', icon: 'bi-hourglass-split',
        tag: 'div',
        attrs: { 'data-elevare-reading-progress': '', 'aria-hidden': 'true' },
        style: { position: 'fixed', top: '0', left: '0', right: '0', height: '4px', 'z-index': '9995', background: 'transparent' },
        traits: [
            { type: 'number', name: 'barHeight', label: LOCALE === 'tr' ? 'Kalınlık (px)' : 'Thickness (px)', value: 4, min: 2, max: 12, changeProp: 1 },
            { type: 'select', name: 'barEdge', label: LOCALE === 'tr' ? 'Konum' : 'Position', value: 'top', changeProp: 1, options: [{ id: 'top', name: LOCALE === 'tr' ? 'Üst' : 'Top' }, { id: 'bottom', name: LOCALE === 'tr' ? 'Alt' : 'Bottom' }] }
        ],
        on: {
            barHeight: (m, v) => m.addStyle({ height: clamp(parseInt(v, 10) || 4, 2, 12) + 'px' }),
            barEdge: (m, v) => m.addStyle(v === 'bottom' ? { top: 'auto', bottom: '0' } : { top: '0', bottom: 'auto' })
        },
        template: () => `<div data-elevare-reading-progress-bar style="height:100%;width:0;background:${BLUE};transition:width .1s linear;"></div>`
    });

    // ---- Tabs -------------------------------------------------------------------
    const tabButton = (i) => `<button type="button" data-elevare-tab="${i + 1}"${i === 0 ? ' aria-selected="true"' : ''} style="padding:10px 16px;border:0;border-bottom:2px solid transparent;background:none;font-weight:600;cursor:pointer;color:${DARK};font-family:inherit;font-size:1rem;">${CT('Sekme', 'Tab')} ${i + 1}</button>`;
    const tabPanel = (i) => `<div data-elevare-tab-panel="${i + 1}" style="padding:16px 4px;"><p>${CT('Sekme ' + (i + 1) + ' içeriği.', 'Tab ' + (i + 1) + ' content.')}</p></div>`;
    smart({
        id: 'elevare-tabs', label: 'Tabs', icon: 'bi-segmented-nav',
        tag: 'div',
        attrs: { class: 'elevare-tabs', 'data-elevare-tabs': '' },
        style: { 'max-width': '900px', margin: '24px auto', padding: '0 20px' },
        traits: [{ type: 'number', name: 'tabCount', label: LOCALE === 'tr' ? 'Sekme sayısı' : 'Tab count', min: 2, max: 8, value: 3, changeProp: 1 }],
        repeats: [
            { trait: 'tabCount', container: '.el-tabs-list', min: 2, max: 8, item: (m, i) => tabButton(i) },
            { trait: 'tabCount', container: '.el-tabs-panels', min: 2, max: 8, item: (m, i) => tabPanel(i) }
        ],
        onLoad: (m) => {
            const list = findModels(m, '.el-tabs-list')[0];
            if (list) m.set('tabCount', list.components().length, { silent: true });
        },
        template: (m) => `
            <div class="el-tabs-list" style="display:flex;gap:4px;flex-wrap:wrap;border-bottom:1px solid ${BORDER};">${rep(m.get('tabCount'), tabButton)}</div>
            <div class="el-tabs-panels">${rep(m.get('tabCount'), tabPanel)}</div>`
    });
    blockCss('.elevare-tabs [data-elevare-tab][aria-selected="true"]{border-bottom-color:' + BLUE + ' !important;color:' + BLUE + ' !important}');

    // ---- Back to top ------------------------------------------------------------
    // Hidden until the visitor has scrolled a screen's worth (runtime); starts in
    // the bottom-left corner so it never sits on a WhatsApp button.
    smart({
        id: 'elevare-back-to-top', label: 'Back to Top', icon: 'bi-arrow-up-circle',
        tag: 'a',
        attrs: { href: '#main-content', 'data-elevare-back-to-top': '', 'aria-label': CT('Sayfanın başına dön', 'Back to top') },
        style: {
            position: 'fixed', bottom: '24px', left: '24px', 'z-index': '9989',
            width: '48px', height: '48px', 'border-radius': '50%', display: 'flex', 'align-items': 'center', 'justify-content': 'center',
            background: DARK, color: WHITE, 'font-size': '1.3rem', 'text-decoration': 'none', 'box-shadow': '0 6px 20px rgba(0,0,0,.25)'
        },
        traits: [
            { type: 'select', name: 'waPosition', label: LOCALE === 'tr' ? 'Konum' : 'Position', value: 'bottom-left', changeProp: 1, options: WA_POSITIONS },
            { type: 'number', name: 'waOffset', label: LOCALE === 'tr' ? 'Kenar Boşluğu (px)' : 'Offset (px)', value: 24, min: 0, max: 200, changeProp: 1 }
        ],
        on: {
            waPosition: (m) => applyWhatsAppPosition(m),
            waOffset: (m) => applyWhatsAppPosition(m)
        },
        template: () => icon('arrow-up')
    });

    // Gallery: "Tıklayınca büyüt" turns every picture in it into a zoomable one,
    // stepped through with the arrow keys (runtime).
    editor.on('component:selected', (m) => {
        if (!m || m.getAttributes()[BLOCK_ATTR] !== 'elevare-gallery') return;
        if (m.getTrait && m.getTrait('galleryZoom')) return;
        m.addTrait({ type: 'checkbox', name: 'galleryZoom', label: LOCALE === 'tr' ? 'Tıklayınca büyüt' : 'Zoom on click', changeProp: 1 });
        m.set('galleryZoom', m.getAttributes()['data-elevare-lightbox-group'] !== undefined, { silent: true });
        m.on('change:galleryZoom', () => {
            if (m.get('galleryZoom')) m.addAttributes({ 'data-elevare-lightbox-group': '' });
            else m.removeAttributes('data-elevare-lightbox-group');
        });
    });

    // ==================================================
    // SCROLL ANIMATIONS
    // ==================================================
    // The site had none at all. This is the whole mechanism: an author picks an
    // effect from the Animasyon trait below, which stamps data-elevare-anim on the
    // component, and elevare-interactions.js (already loaded on every public page —
    // no Site Codes snippet to remember, no per-page script) reveals it as it
    // scrolls into view.
    //
    // The single most important line here is the .elevare-anim-ready guard: the
    // "hidden" starting state applies ONLY once that class is on <html>, and only
    // the script puts it there. So if the script fails, is blocked, or has not run
    // yet, nothing is ever hidden — the page just renders normally without motion.
    // A CMS must never be one JS error away from blank pages, and a naive
    // opacity:0-by-default would be exactly that.
    //
    // It also means the builder canvas (which does not load that script) always
    // shows content in its final state, which is what you want while editing;
    // the animation is seen with Önizle.
    blockCss(
        '@media (prefers-reduced-motion: no-preference){' +
        ' .elevare-anim-ready [data-elevare-anim]{opacity:0;will-change:opacity,transform;}' +
        ' .elevare-anim-ready [data-elevare-anim="fade-up"]{transform:translateY(28px);}' +
        ' .elevare-anim-ready [data-elevare-anim="fade-down"]{transform:translateY(-28px);}' +
        ' .elevare-anim-ready [data-elevare-anim="fade-left"]{transform:translateX(-28px);}' +
        ' .elevare-anim-ready [data-elevare-anim="fade-right"]{transform:translateX(28px);}' +
        ' .elevare-anim-ready [data-elevare-anim="zoom-in"]{transform:scale(.92);}' +
        ' .elevare-anim-ready [data-elevare-anim].elevare-anim-in{opacity:1;transform:none;' +
        '  transition:opacity .6s ease-out var(--elevare-anim-delay,0s),transform .6s ease-out var(--elevare-anim-delay,0s);}' +
        '}'
    );

    // ==================================================
    // GLOBAL TRAIT INJECTIONS (Pop-up & Form logic)
    // ==================================================
    editor.on('component:selected', (model) => {
        const tag = (model.get('tagName') || '').toUpperCase();

        // Animation is offered on everything, before the tag-specific branches
        // below return early — a section, a card, a heading and a button are all
        // things somebody reasonably wants to animate.
        const animTraits = model.get('traits');
        if (!animTraits.where({ name: 'data-elevare-anim' }).length) {
            animTraits.add([
                {
                    type: 'select', label: 'Animasyon', name: 'data-elevare-anim',
                    options: [
                        { id: '', name: 'Yok' },
                        { id: 'fade', name: 'Belirme' },
                        { id: 'fade-up', name: 'Aşağıdan gel' },
                        { id: 'fade-down', name: 'Yukarıdan gel' },
                        { id: 'fade-left', name: 'Soldan gel' },
                        { id: 'fade-right', name: 'Sağdan gel' },
                        { id: 'zoom-in', name: 'Yakınlaş' }
                    ]
                },
                {
                    // Stagger: give three cards 0/100/200ms and they arrive in
                    // sequence instead of as one block.
                    type: 'select', label: 'Animasyon Gecikmesi', name: 'data-elevare-anim-delay',
                    options: [
                        { id: '', name: 'Yok' },
                        { id: '100', name: '0.1 sn' },
                        { id: '200', name: '0.2 sn' },
                        { id: '300', name: '0.3 sn' },
                        { id: '500', name: '0.5 sn' }
                    ]
                }
            ]);
        }

        // The canvas holds every <details> open (holdDetailsOpen); this is the one
        // way to ship one open, and the checkbox is the only place that shows it.
        if (tag === 'DETAILS') {
            const traits = model.get('traits');
            if (!traits.where({ name: 'open' }).length) {
                traits.add({ type: 'checkbox', label: 'Başlangıçta açık', name: 'open', valueTrue: 'open' });
            }
            return;
        }

        if (tag === 'A' || tag === 'BUTTON') {
            const traits = model.get('traits');
            if (!traits.where({ name: 'data-elevare-popup-trigger' }).length) {
                traits.add({ type: 'text', label: 'Pop-up Aç (ID)', name: 'data-elevare-popup-trigger' });
            }
            return;
        }

        if (tag === 'FORM') {
            const parentEl = model.parent && model.parent();
            const isSearchBoxForm = parentEl && (parentEl.getClasses() || []).includes('elevare-search-box');
            if ((model.getClasses() || []).includes('elevare-listing-search') || isSearchBoxForm) return;

            const traits = model.get('traits');
            if (!traits.where({ name: 'data-elevare-managed-form' }).length) {
                traits.add([
                    { type: 'checkbox', label: 'Yanıtları Kaydet', name: 'data-elevare-managed-form' },
                    { type: 'text', label: 'Form Adı', name: 'data-elevare-form-name' },
                    {
                        type: 'select', label: 'Submit Sonrası', name: 'data-elevare-onsubmit',
                        options: [
                            // '' keeps forms saved before this option existed on the
                            // message they always showed.
                            { id: '', name: 'Mesaj Göster' },
                            { id: 'redirect', name: 'Sayfaya Yönlendir' },
                            { id: 'popup', name: 'Pop-up Göster' }
                        ]
                    },
                    { type: 'text', label: 'Yönlendirilecek URL', name: 'data-elevare-redirect-url' },
                    { type: 'text', label: 'Gösterilecek Pop-up ID', name: 'data-elevare-popup-id' }
                ]);
            }
            // Added on their own check so forms that already carry the traits above
            // get these too. Left empty, the site shows its built-in text in the
            // page's language — the placeholder is that text.
            if (!traits.where({ name: 'data-elevare-success-message' }).length) {
                traits.add([
                    {
                        type: 'text', label: 'Başarı Mesajı', name: 'data-elevare-success-message',
                        placeholder: CT('Teşekkürler — gönderiminiz alındı.', 'Thank you — your submission was received.')
                    },
                    {
                        type: 'text', label: 'Hata Mesajı', name: 'data-elevare-error-message',
                        placeholder: CT('Form gönderilemedi. Lütfen tekrar deneyiniz.', 'Form submission failed. Please try again.')
                    }
                ]);
            }
        }
    });
};