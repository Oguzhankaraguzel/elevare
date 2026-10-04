// ==========================================
// ELEVARE SEO ANALYZER v2.2
//
// Updates:
// - Unicode supported Regex word boundary (\b) for keyword density.
// - Fixed form element contrast checking (inputs with value/placeholder).
// - Caught 'javascript:void(0)' as empty hrefs.
// - Filtered out <script> and <style> from word count to prevent hidden text pollution.
// - Simplified Blazor notify with optional chaining.
// - Made mobile device test wait dynamic (waits for resize instead of fixed ms).
// - Replaced the naive "every image but the first should be lazy" heuristic with a
//   device-aware check: switches through desktop/tablet/mobile and flags images that
//   are actually above/below the fold on each, instead of guessing from DOM order.
// - Added a check for multiple loading="eager" images (only the first is preloaded
//   as the page's LCP candidate; extras just compete for bandwidth).
// - Narrowed the old lazy-loading check to iframes only, now that images have their
//   own device-aware checks.
// ==========================================
window.elevareSeoAnalyzer = (function () {
    'use strict';

    const GENERIC_LINK_TEXTS = {
        en: ['click here', 'here', 'read more', 'more', 'link', 'this', 'learn more', 'details', 'click', 'go', 'page'],
        tr: ['tıkla', 'tıklayın', 'buraya tıklayın', 'buraya tıkla', 'devamı', 'devamını oku', 'daha fazla', 'daha fazlası', 'detay', 'detaylar', 'incele', 'link', 'burada', 'burası', 'git']
    };

    const SCAN_CAP = 1500;   // max DOM elements visited per pass
    const LIST_CAP = 6;      // max components kept per issue row (for "locate")
    const STYLE_ID = 'elevare-seo-style-v2';

    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const esc = (s) => String(s == null ? '' : s)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    const fmt = (tpl, map) => String(tpl).replace(/\{(\w+)\}/g, (m, k) => (map && map[k] != null ? map[k] : m));
    const escapeRegExp = (s) => String(s).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

    // ------------------------------------------------------------
    // COLOR / CONTRAST UTILITIES (WCAG 2.x)
    // ------------------------------------------------------------
    function parseColor(str) {
        if (!str) return null;
        const m = String(str).match(/rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*(?:,\s*([\d.]+)\s*)?\)/i);
        if (!m) return null;
        return { r: +m[1], g: +m[2], b: +m[3], a: m[4] == null ? 1 : +m[4] };
    }
    function luminance(c) {
        const f = (v) => {
            v /= 255;
            return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
        };
        return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b);
    }
    function contrastRatio(a, b) {
        const l1 = luminance(a), l2 = luminance(b);
        const hi = Math.max(l1, l2), lo = Math.min(l1, l2);
        return (hi + 0.05) / (lo + 0.05);
    }
    function blend(top, under) {
        const a = top.a + under.a * (1 - top.a);
        if (a <= 0) return { r: 255, g: 255, b: 255, a: 0 };
        return {
            r: (top.r * top.a + under.r * under.a * (1 - top.a)) / a,
            g: (top.g * top.a + under.g * under.a * (1 - top.a)) / a,
            b: (top.b * top.a + under.b * under.a * (1 - top.a)) / a,
            a
        };
    }
    function effectiveBg(el) {
        let node = el, acc = null;
        while (node && node.nodeType === 1) {
            const win = node.ownerDocument.defaultView;
            const cs = win.getComputedStyle(node);
            if (cs.backgroundImage && cs.backgroundImage !== 'none') return null;
            const c = parseColor(cs.backgroundColor);
            if (c && c.a > 0) {
                acc = acc ? blend(acc, c) : c;
                if (acc.a >= 0.999) return acc;
            }
            node = node.parentElement;
        }
        const base = { r: 255, g: 255, b: 255, a: 1 };
        return acc ? blend(acc, base) : base;
    }

    const CSS = `
.gjs-mdl-dialog{background:var(--cms-card,#ffffff);color:var(--cms-text,#1f2937);}
.gjs-mdl-header{background:var(--cms-card,#ffffff);border-bottom:1px solid var(--cms-border,#e5e7eb);}
.gjs-mdl-title{color:var(--cms-text,#1f2937);}
.gjs-mdl-btn-close{color:var(--cms-text-muted,#6b7280);}
.gjs-mdl-content{background:var(--cms-card,#ffffff);color:var(--cms-text,#1f2937);}
.gjs-mdl-overlay{background:rgba(0,0,0,.5);}
.elevare-seo-report{--seo-bg:var(--cms-card,#ffffff);--seo-fg:var(--cms-text,#1f2937);--seo-muted:var(--cms-text-muted,#6b7280);--seo-border:var(--cms-border,#e5e7eb);--seo-accent:var(--cms-accent,#2563eb);--seo-pass:#16a34a;--seo-warn:#d97706;--seo-fail:#dc2626;background:var(--seo-bg);color:var(--seo-fg);font-size:13px;max-height:70vh;overflow:auto;padding:4px 2px;}
.elevare-seo-head{display:flex;align-items:center;gap:16px;margin-bottom:12px;}
.elevare-seo-score{width:72px;height:72px;border-radius:50%;display:flex;flex-direction:column;align-items:center;justify-content:center;font-weight:800;font-size:22px;color:#fff;flex-shrink:0;line-height:1;}
.elevare-seo-score small{font-size:10px;font-weight:600;opacity:.85;margin-top:2px;}
.elevare-seo-score.good{background:var(--seo-pass);}
.elevare-seo-score.mid{background:var(--seo-warn);}
.elevare-seo-score.bad{background:var(--seo-fail);}
.elevare-seo-headtxt{flex:1;}
.elevare-seo-headtxt h3{margin:0 0 4px;font-size:15px;color:var(--seo-fg);}
.elevare-seo-headtxt p{margin:0;color:var(--seo-muted);}
.elevare-seo-reanalyze{background:var(--seo-accent);color:#fff;border:0;border-radius:6px;padding:8px 14px;font-weight:600;cursor:pointer;}
.elevare-seo-cats{display:flex;flex-wrap:wrap;gap:8px;margin:0 0 14px;}
.elevare-seo-cat-chip{display:inline-flex;align-items:center;gap:6px;padding:4px 10px;border-radius:999px;border:1px solid var(--seo-border);font-weight:600;font-size:12px;background:var(--seo-bg);}
.elevare-seo-cat-chip .dot{width:8px;height:8px;border-radius:50%;}
.elevare-seo-cat-chip.pass .dot{background:var(--seo-pass);}
.elevare-seo-cat-chip.warn .dot{background:var(--seo-warn);}
.elevare-seo-cat-chip.fail .dot{background:var(--seo-fail);}
.elevare-seo-row{display:flex;align-items:flex-start;gap:10px;padding:9px 10px;border:1px solid var(--seo-border);border-radius:8px;margin-bottom:8px;background:var(--seo-bg);}
.elevare-seo-row .ic{font-weight:800;flex-shrink:0;width:16px;text-align:center;}
.elevare-seo-row.pass .ic{color:var(--seo-pass);}
.elevare-seo-row.warn .ic{color:var(--seo-warn);}
.elevare-seo-row.fail .ic{color:var(--seo-fail);}
.elevare-seo-row .bd{flex:1;min-width:0;}
.elevare-seo-row .bd p{margin:0;font-weight:600;color:var(--seo-fg);}
.elevare-seo-row .bd small{color:var(--seo-muted);display:block;margin-top:2px;line-height:1.45;}
.elevare-seo-row-cat{display:inline-block;font-size:9px;font-weight:700;letter-spacing:.4px;text-transform:uppercase;color:var(--seo-muted);border:1px solid var(--seo-border);border-radius:4px;padding:1px 5px;margin-right:6px;vertical-align:1px;}
.elevare-seo-tplnote{display:block;margin-top:5px;padding:5px 8px;border-left:3px solid var(--seo-accent);background:var(--cms-accent-light,rgba(217,119,6,.08));border-radius:0 4px 4px 0;font-size:11.5px;line-height:1.45;color:var(--cms-text-muted,#6b7280);}
.elevare-seo-legend{margin:0 0 12px;padding:8px 12px;background:var(--cms-accent-light,rgba(217,119,6,.07));border:1px solid var(--seo-border);border-radius:8px;font-size:12px;line-height:1.5;color:var(--cms-text-muted,#6b7280);}
.elevare-seo-locate{background:none;border:1px solid var(--seo-border);color:var(--seo-accent);border-radius:6px;padding:4px 10px;font-size:12px;cursor:pointer;flex-shrink:0;font-weight:600;}
.elevare-seo-locate:hover{border-color:var(--seo-accent);}
.elevare-seo-loading{display:flex;align-items:center;justify-content:center;gap:12px;padding:48px 12px;color:var(--cms-text-muted,#6b7280);font-size:14px;}
.elevare-seo-spinner{width:26px;height:26px;border:3px solid var(--cms-border,#e5e7eb);border-top-color:var(--cms-accent,#2563eb);border-radius:50%;animation:elevareSpin .8s linear infinite;}
@keyframes elevareSpin{to{transform:rotate(360deg);}}
.elevare-seo-btn-label{font-weight:700;font-size:11px;padding:2px 6px;border-radius:4px;background:#6b7280;color:#fff;}
.elevare-seo-btn-label.good{background:#16a34a;}
.elevare-seo-btn-label.mid{background:#d97706;}
.elevare-seo-btn-label.bad{background:#dc2626;}
`;

    function injectCss() {
        if (document.getElementById(STYLE_ID)) return;
        const s = document.createElement('style');
        s.id = STYLE_ID;
        s.textContent = CSS;
        document.head.appendChild(s);
    }

    // ============================================================
    // ATTACH
    // ============================================================
    function attach(editor, lang, seoMeta, seoResources, dotNetRef) {
        const R = seoResources || {};
        const C = R.checks || {};
        const tr = (k, fb) => (R[k] != null && R[k] !== '' ? R[k] : fb);
        const tc = (k, fb) => (C[k] != null && C[k] !== '' ? C[k] : fb);
        const L = (lang || 'en').split('-')[0];
        const genericTexts = (GENERIC_LINK_TEXTS[L] || []).concat(GENERIC_LINK_TEXTS.en);

        const state = {
            meta: normalizeMeta(seoMeta),
            lastScore: null,
            lastMobile: null,
            lastViewportImages: null,
            analyzing: false,
            debounce: null
        };

        function normalizeMeta(m) {
            m = m || {};
            return {
                focusKeyword: (m.focusKeyword ?? m.FocusKeyword ?? '').toString(),
                metaDescription: (m.metaDescription ?? m.MetaDescription ?? '').toString(),
                pageTitle: (m.pageTitle ?? m.PageTitle ?? '').toString(),
                // The site-wide suffix the public layout appends after " - ", so the
                // title check can judge the REAL <title> ("Ana Sayfa - Oğuzhan …")
                // rather than just the page-title field.
                titleSuffix: (m.titleSuffix ?? m.TitleSuffix ?? '').toString(),
                structuredData: (m.structuredData ?? m.StructuredData ?? '').toString(),
                social: normalizeSocial(m.social ?? m.Social),
                // Where the page is served — what og:url and the structured data's
                // own addresses are checked against. Empty: not known, not checked.
                pageUrl: (m.pageUrl ?? m.PageUrl ?? '').toString().trim()
            };
        }

        // The addresses the structured data gives THIS page: a page node's @id and
        // url, an article's own @id ("…#article") and mainEntityOfPage, and the last
        // step of the breadcrumb trail. Not the site's, the publisher's or the
        // parents' — those are other things' addresses.
        function structuredDataAddresses(raw) {
            let graph;
            try {
                const parsed = JSON.parse(raw || '');
                graph = Array.isArray(parsed) ? parsed : (parsed && parsed['@graph']) || (parsed ? [parsed] : []);
            } catch (e) { return []; }
            const urls = [];
            const types = (n) => [].concat(n['@type'] || []).map(String);
            const idOf = (v) => (typeof v === 'string' ? v : v && typeof v === 'object' ? v['@id'] : null);
            (Array.isArray(graph) ? graph : []).forEach((n) => {
                if (!n || typeof n !== 'object') return;
                const t = types(n);
                if (t.some((x) => /Page$/.test(x))) {
                    if (typeof n['@id'] === 'string') urls.push(n['@id']);
                    if (typeof n.url === 'string') urls.push(n.url);
                }
                if (t.some((x) => /Article$|^BlogPosting$/.test(x)) && typeof n['@id'] === 'string' && n['@id'].includes('#')) urls.push(n['@id']);
                const main = idOf(n.mainEntityOfPage);
                if (main) urls.push(main);
                if (t.includes('BreadcrumbList') && Array.isArray(n.itemListElement) && n.itemListElement.length) {
                    const last = n.itemListElement[n.itemListElement.length - 1];
                    const item = last && (idOf(last.item) || last.url);
                    if (item) urls.push(item);
                }
            });
            return urls.filter((u) => typeof u === 'string' && /^https?:\/\//i.test(u));
        }

        // The page's share tags as its fields say them (the Social Sharing panel).
        function normalizeSocial(s) {
            s = s || {};
            const str = (a, b) => (s[a] ?? s[b] ?? '').toString().trim();
            const num = (a, b) => parseInt(s[a] ?? s[b], 10) || 0;
            return {
                title: str('title', 'Title'),
                description: str('description', 'Description'),
                image: str('image', 'Image'),
                imageWidth: num('imageWidth', 'ImageWidth'),
                imageHeight: num('imageHeight', 'ImageHeight'),
                imageAlt: str('imageAlt', 'ImageAlt'),
                url: str('url', 'Url')
            };
        }

        injectCss();

        const BTN_ID = 'elevare-seo-btn';
        editor.Commands.add('elevare-seo-report', { run: () => { openModal(); } });
        if (!editor.Panels.getButton('options', BTN_ID)) {
            editor.Panels.addButton('options', {
                id: BTN_ID,
                className: 'elevare-seo-btn-cls',
                label: '<span class="elevare-seo-btn-label">SEO</span>',
                command: 'elevare-seo-report',
                attributes: { title: tr('title', 'SEO Analysis') }
            });
        }
        function updateButton(score) {
            const b = editor.Panels.getButton('options', BTN_ID);
            if (!b) return;
            const cls = score >= 80 ? 'good' : score >= 50 ? 'mid' : 'bad';
            b.set('label', `<span class="elevare-seo-btn-label ${cls}">SEO ${score}</span>`);
        }
        function notify(score) {
            // Optional chaining to prevent unhandled rejections cleanly
            dotNetRef?.invokeMethodAsync('OnSeoScoreUpdated', score).catch(() => { });
        }

        function collect() {
            const wrapper = editor.getWrapper();
            const compByEl = new Map();
            (function walk(c) {
                const el = c.getEl && c.getEl();
                if (el && el.nodeType === 1) compByEl.set(el, c);
                c.components().forEach(walk);
            })(wrapper);
            return {
                compByEl,
                doc: editor.Canvas.getDocument(),
                body: editor.Canvas.getBody()
            };
        }
        function findComp(el, compByEl) {
            let n = el;
            while (n && n.nodeType === 1) {
                const c = compByEl.get(n);
                if (c) return c;
                n = n.parentElement;
            }
            return null;
        }
        function mapComps(els, compByEl) {
            const out = [], seen = new Set();
            for (const el of els) {
                const c = findComp(el, compByEl);
                if (c && !seen.has(c.cid)) { seen.add(c.cid); out.push(c); }
                if (out.length >= LIST_CAP) break;
            }
            return out;
        }
        const visible = (el) => {
            const r = el.getBoundingClientRect();
            if (r.width <= 0 && r.height <= 0) return false;
            const cs = el.ownerDocument.defaultView.getComputedStyle(el);
            return cs.display !== 'none' && cs.visibility !== 'hidden';
        };
        const ownText = (el) => {
            // Check form elements that have visual text via attributes
            const tag = (el.tagName || '').toLowerCase();
            if (['input', 'textarea', 'select'].includes(tag)) {
                return !!(el.value || el.placeholder || '').trim();
            }
            // Normal text nodes
            for (const n of el.childNodes) {
                if (n.nodeType === 3 && n.textContent.trim().length > 0) return true;
            }
            return false;
        };

        // Helper to safely extract body text without hidden <script> or <style> tags
        const getCleanText = (body) => {
            if (body.innerText !== undefined) return body.innerText.trim();
            const clone = body.cloneNode(true);
            clone.querySelectorAll('script, style, noscript').forEach(el => el.remove());
            return (clone.textContent || '').trim();
        };

        // ------------------------------------------------------------
        // DEVICE-BASED MOBILE OVERFLOW TEST
        // ------------------------------------------------------------
        async function measureMobileOverflow(compByEl) {
            const dm = editor.Devices || editor.DeviceManager;
            if (!dm || !dm.getAll) return null;
            let name = null;
            dm.getAll().forEach((d) => {
                const n = (d.get && d.get('name')) || '';
                if (n === 'Mobile portrait') name = n;
            });
            if (!name) {
                dm.getAll().forEach((d) => {
                    const n = (d.get && d.get('name')) || '';
                    if (!name && /mobile/i.test(n)) name = n;
                });
            }
            if (!name) return null;

            const prev = editor.getDevice();
            try {
                editor.setDevice(name);
                const doc = editor.Canvas.getDocument();
                const body = editor.Canvas.getBody();

                // Dynamic wait for iframe resize to settle (max ~400ms)
                let initialWidth = doc.documentElement.clientWidth;
                for (let j = 0; j < 8; j++) {
                    await wait(50);
                    const currentWidth = doc.documentElement.clientWidth;
                    if (currentWidth !== initialWidth) break;
                }

                const cw = doc.documentElement.clientWidth;
                const overflow = body.scrollWidth > cw + 4;
                let comps = [];
                if (overflow) {
                    const offenders = [];
                    const els = body.querySelectorAll('*');
                    for (let i = 0; i < els.length && i < SCAN_CAP; i++) {
                        const el = els[i];
                        const r = el.getBoundingClientRect();
                        if (r.width > 0 && (r.right > cw + 4 || r.left < -4)) offenders.push(el);
                        if (offenders.length >= 12) break;
                    }
                    comps = mapComps(offenders, compByEl);
                }
                return { overflow, comps, width: cw };
            } catch (e) {
                return null;
            } finally {
                try { editor.setDevice(prev); } catch (e) { }
                await wait(150); // slight buffer before returning to normal view
            }
        }

        // ------------------------------------------------------------
        // DEVICE-BASED ABOVE-THE-FOLD IMAGE LOADING TEST
        //
        // Switches through every configured device (desktop/tablet/mobile — see
        // grapes-editor.js's deviceManager) and, for each, checks which <img>s sit
        // within the initial viewport (no scrolling needed) versus below it. An
        // image's loading attribute only matters relative to where it actually
        // renders, and that differs per device — a two-column hero that is fully
        // visible on desktop can fall below the fold on mobile.
        // ------------------------------------------------------------
        const DEVICE_LABELS = { desktop: /desktop/i, tablet: /tablet/i, mobile: /mobile/i };

        async function measureViewportImageLoading() {
            const dm = editor.Devices || editor.DeviceManager;
            if (!dm || !dm.getAll) return null;

            const names = {};
            dm.getAll().forEach((d) => {
                const n = (d.get && d.get('name')) || '';
                Object.keys(DEVICE_LABELS).forEach((key) => {
                    if (!names[key] && DEVICE_LABELS[key].test(n)) names[key] = n;
                });
            });
            if (!Object.keys(names).length) return null;

            const prev = editor.getDevice();
            const perDevice = {};
            try {
                for (const key of Object.keys(names)) {
                    const name = names[key];
                    try {
                        editor.setDevice(name);
                        const doc = editor.Canvas.getDocument();
                        const body = editor.Canvas.getBody();

                        let initialHeight = doc.documentElement.clientHeight;
                        for (let j = 0; j < 8; j++) {
                            await wait(50);
                            const currentHeight = doc.documentElement.clientHeight;
                            if (currentHeight !== initialHeight) break;
                        }

                        // A device frame too wide for the canvas is drawn scaled down
                        // and made taller by the same factor so it still fills the
                        // canvas (installDeviceFit); the fold is what is visible, so
                        // undo that stretch.
                        const zoom = (editor.Canvas.getZoom ? editor.Canvas.getZoom() : 100) / 100 || 1;
                        const vh = doc.documentElement.clientHeight * zoom;
                        const vw = doc.documentElement.clientWidth;
                        const win = doc.defaultView;
                        const imgs = Array.prototype.slice.call(body.querySelectorAll('img'));
                        // An image is "above the fold" only when it is actually painted
                        // in the initial viewport: inside it BOTH vertically and
                        // horizontally, and hidden by neither itself nor an ancestor. A
                        // carousel lays its slides out in a row and shows one at a time —
                        // the others are shifted out and clipped, or faded to opacity:0.
                        // The old vertical-only test counted every slide as visible, so
                        // every slide but the first was flagged "above-fold lazy"; making
                        // them all eager then tripped the multiple-eager warning, leaving
                        // no setting that passed. Now only the shown slide counts.
                        const notHidden = (start) => {
                            for (let n = start; n && n !== doc.documentElement; n = n.parentElement) {
                                const cs = win.getComputedStyle(n);
                                if (cs.display === 'none' || cs.visibility === 'hidden' || parseFloat(cs.opacity) === 0) return false;
                            }
                            return true;
                        };
                        const renderedInViewport = (img) => {
                            const slidesRow = img.closest && img.closest('.el-slides');
                            if (slidesRow) {
                                // A carousel shows only its first slide when the published
                                // page loads — whatever slide the editor happens to sit on
                                // now. So only the first slide's image can be above the
                                // fold, and it is judged by the SLIDER's box, not its own:
                                // the editor may have slid the track sideways, putting the
                                // first slide's image off-screen (x < 0) even though on load
                                // it sits at x=0. That makes the advice deterministic and
                                // match the real LCP — first slide eager, the rest lazy.
                                let slide = img;
                                while (slide && slide.parentElement !== slidesRow) slide = slide.parentElement;
                                if (slide && slide !== slidesRow.firstElementChild) return false;
                                const sr = slidesRow.getBoundingClientRect();
                                return sr.height > 1 && sr.bottom > 0 && sr.top < vh && notHidden(slidesRow);
                            }
                            const r = img.getBoundingClientRect();
                            if (r.width <= 1 || r.height <= 1) return false;
                            if (!(r.bottom > 0 && r.top < vh)) return false;   // vertical
                            if (!(r.right > 0 && r.left < vw)) return false;   // horizontal
                            return notHidden(img);
                        };
                        const aboveFold = [], belowFold = [];
                        imgs.forEach((img) => (renderedInViewport(img) ? aboveFold : belowFold).push(img));
                        perDevice[key] = {
                            label: name,
                            lazyAboveFold: aboveFold.filter((i) => i.getAttribute('loading') === 'lazy'),
                            // Same fix as the multipleEager check below: a below-fold
                            // <img> with no loading attribute renders eager by browser
                            // default, same as one that says so explicitly — checking
                            // only the literal 'eager' string let the common case (no
                            // attribute set) through uncaught. A carousel's off-screen
                            // slides land here too, which is correct: they SHOULD be
                            // lazy, so an eager one among them is worth flagging.
                            eagerBelowFold: belowFold.filter((i) => i.getAttribute('loading') !== 'lazy')
                        };
                    } catch (e) {
                        perDevice[key] = null;
                    }
                }
            } finally {
                try { editor.setDevice(prev); } catch (e) { }
                await wait(150); // slight buffer before returning to normal view
            }
            return perDevice;
        }

        async function analyzeSeo(runDeviceTest) {
            const { compByEl, body } = collect();
            const checks = [];
            let score = 100;
            // Warnings are guidance, not failures. A "!" nudges a good page toward
            // perfect; it must not sink the score, or an author who has fixed every
            // real problem still sees 55 and stops trusting the number. So a fail
            // ("✕": missing title/H1, broken structured data, poor contrast) keeps its
            // full weight, while each warning costs only a couple of points and all
            // warnings together are capped — a page whose only issues are warnings
            // never drops below ~80, which is where "good, with room to polish" should
            // land. (The empty-page short-circuit below still scores 0 on its own.)
            const WARN_MAX_EACH = 3;
            const WARN_TOTAL_CAP = 20;
            let warnDeducted = 0;
            const add = (id, cat, status, title, detail, comps, deduct) => {
                if (status === 'fail' && deduct) {
                    score -= deduct;
                } else if (status === 'warn' && deduct) {
                    const soft = Math.min(WARN_MAX_EACH, Math.ceil(deduct / 3));
                    const applied = Math.min(soft, WARN_TOTAL_CAP - warnDeducted);
                    warnDeducted += applied;
                    score -= applied;
                }
                const row = { id, cat, status, title, detail: detail || '', comps: comps || [] };
                checks.push(row);
                return row;
            };
            const q = (sel) => Array.prototype.slice.call(body.querySelectorAll(sel));

            // ================= CONTENT =================
            const text = getCleanText(body);
            const words = text ? text.split(/\s+/).filter(Boolean) : [];

            // An empty canvas is not a half-decent page, but that is what the score
            // used to say: every check below is written to judge a page that HAS
            // content, so on an empty one most of them never fire, and only the
            // handful of fixed deductions that do apply (no H1, no meta description,
            // no structured data, thin content) land — adding up to roughly half.
            // A blank page scored ~50/100, which reads as "half done" for something
            // with nothing on it at all.
            //
            // There is nothing to analyse here, and saying so is more useful than a
            // number. Score 0 rather than a partial tally, and a single row that
            // says why instead of a list of failures the author cannot act on yet.
            const hasAnyContent = words.length > 0
                || body.querySelector('img, iframe, video, audio, table, form, h1, h2, h3, h4, h5, h6');
            if (!hasAnyContent) {
                add('emptyPage', 'content', 'fail',
                    tc('emptyPageTitle', 'The page is empty'),
                    tc('emptyPageDetail', 'There is nothing on the canvas to analyse yet. Add your content, then run the analysis again.'),
                    [], 100);
                return { score: 0, checks };
            }

            const h1s = q('h1');
            if (h1s.length === 0) {
                add('h1', 'content', 'fail', tc('h1MissingTitle', 'No H1 heading'), tc('h1MissingDetail', 'Every page needs exactly one H1 that states the main topic.'), [], 10);
            } else if (h1s.length > 1) {
                add('h1', 'content', 'warn', tc('h1MultipleTitle', 'Multiple H1 headings'), fmt(tc('h1MultipleDetail', '{n} H1 tags found; keep a single H1 per page.'), { n: h1s.length }), mapComps(h1s.slice(1), compByEl), 5);
            } else {
                add('h1', 'content', 'pass', tc('h1OkTitle', 'H1 heading present'), '', mapComps(h1s, compByEl), 0);
            }

            const hs = q('h1,h2,h3,h4,h5,h6');
            let skips = 0; let last = 0; const skipEls = [];
            hs.forEach((h) => {
                const lv = +h.tagName[1];
                if (last && lv > last + 1) { skips++; skipEls.push(h); }
                last = lv;
            });
            if (skips > 0) {
                add('headingSkip', 'content', 'warn', tc('headingSkipTitle', 'Heading levels skipped'), fmt(tc('headingSkipDetail', '{n} heading(s) skip a level (e.g. H2 → H4). Keep the hierarchy sequential.'), { n: skips }), mapComps(skipEls, compByEl), 4);
            }

            if (words.length < 300) {
                add('thinContent', 'content', 'warn', tc('thinContentTitle', 'Thin content'), fmt(tc('thinContentDetail', 'Only {n} words. Pages under ~300 words rarely rank; add substantive copy.'), { n: words.length }), [], 8);
            } else {
                add('thinContent', 'content', 'pass', fmt(tc('contentOkTitle', 'Content length OK ({n} words)'), { n: words.length }), '', [], 0);
            }

            const kw = (state.meta.focusKeyword || '').trim();
            if (!kw) {
                add('keyword', 'content', 'warn', tc('keywordNotSetTitle', 'No focus keyword set'), tc('keywordNotSetDetail', 'Set a focus keyword in the SEO panel to enable keyword checks.'), [], 3);
            } else {
                let occ = 0;
                try {
                    // Unicode-aware RegExp for proper word boundaries (supports Turkish chars)
                    const regex = new RegExp(`(?<=^|[^\\p{L}\\p{N}])${escapeRegExp(kw)}(?=[^\\p{L}\\p{N}]|$)`, 'giu');
                    const matches = text.match(regex);
                    occ = matches ? matches.length : 0;
                } catch (e) {
                    // Fallback for older browsers without Unicode RegExp support
                    const regex = new RegExp(`\\b${escapeRegExp(kw)}\\b`, 'gi');
                    const matches = text.match(regex);
                    occ = matches ? matches.length : 0;
                }

                if (occ === 0) {
                    add('keyword', 'content', 'fail', tc('keywordMissingTitle', 'Focus keyword not found'), fmt(tc('keywordMissingDetail', '"{kw}" does not appear in the page content.'), { kw: kw }), [], 8);
                } else {
                    add('keyword', 'content', 'pass', fmt(tc('keywordOkTitle', 'Focus keyword found ({n}×)'), { n: occ }), '', [], 0);
                    const kwWords = kw.split(/\s+/).length;
                    const density = words.length ? (occ * kwWords) / words.length * 100 : 0;
                    if (occ > 8 && density > 4) {
                        add('keywordStuffing', 'content', 'warn', tc('keywordStuffingTitle', 'Possible keyword stuffing'), fmt(tc('keywordStuffingDetail', '"{kw}" appears {n} times ({d}% density). Over-repetition can trigger spam filters.'), { kw: kw, n: occ, d: density.toFixed(1) }), [], 5);
                    }
                }
            }

            // ---- PAGE TITLE ----
            // The published <title> is not the page-title field alone: the public
            // layout renders "{page title} - {suffix}", where the suffix is the SEO
            // settings' title suffix or the site name (see EffectiveTitleSuffix in
            // PageEditor and _Layout.cshtml). So "Ana Sayfa" really appears as
            // "Ana Sayfa - Oğuzhan KARAGÜZEL" in a search result — and the length check
            // judges THAT composed title, not the 9-character field, or it nags about a
            // title that is actually a reasonable length once the site name is on it.
            //
            // The page-title field is a box in the toolbar, not a canvas element, so
            // these findings carry a focus target instead of a component: "Locate"
            // scrolls to the Başlık field and puts the cursor in it (see buildReport).
            const TITLE_FOCUS = '#pe-page-title-input';
            const pt = (state.meta.pageTitle || '').trim();
            const suffix = (state.meta.titleSuffix || '').trim();
            const fullTitle = pt && suffix ? pt + ' - ' + suffix : (pt || suffix);
            const titleLen = fullTitle.length;
            const titleVars = { n: titleLen, t: pt, full: fullTitle, suffix: suffix };
            let titleRow;
            if (!pt) {
                titleRow = add('title', 'content', 'fail', tc('titleMissingTitle', 'Page title missing'), fmt(tc('titleMissingDetail', 'The title is what a search result shows first and what a browser tab is named. Give the page one in the Title field at the top of the editor; the site suffix “{suffix}” is added automatically when it is published.'), titleVars), [], 10);
            } else if (titleLen < 30) {
                titleRow = add('title', 'content', 'warn', tc('titleShortTitle', 'Page title too short'), fmt(tc('titleShortDetail', 'The published title “{full}” is only {n} characters. Your page title is “{t}”; the site suffix “{suffix}” is added after a dash. Aim for 30–60 so the result line is descriptive without being cut off.'), titleVars), [], 4);
            } else if (titleLen > 60) {
                titleRow = add('title', 'content', 'warn', tc('titleLongTitle', 'Page title too long'), fmt(tc('titleLongDetail', 'The published title “{full}” is {n} characters; Google truncates around 60. (Your page title “{t}” plus the site suffix “{suffix}”.) Put the important words first.'), titleVars), [], 4);
            } else {
                titleRow = add('title', 'content', 'pass', tc('titleOkTitle', 'Page title OK'), fmt(tc('titleOkDetail', 'The published title is “{full}” ({n} characters).'), titleVars), [], 0);
            }
            if (titleRow && titleRow.status !== 'pass') titleRow.focusSel = TITLE_FOCUS;

            // Substring, not a word-boundary match like the body-text check below:
            // a title legitimately inflects the keyword ("web tasarımı" for the
            // keyword "web tasarım"), and flagging that as absent would be noise.
            if (pt && kw && pt.toLocaleLowerCase(L).indexOf(kw.toLocaleLowerCase(L)) === -1) {
                add('titleKeyword', 'content', 'warn', tc('titleNoKeywordTitle', 'Focus keyword not in the title'), fmt(tc('titleNoKeywordDetail', '"{kw}" does not appear in the page title, which carries more weight than anywhere else on the page.'), { kw: kw }), [], 5);
            }

            const md = (state.meta.metaDescription || '').trim();
            if (!md) {
                add('metaDesc', 'content', 'fail', tc('metaDescMissingTitle', 'Meta description missing'), tc('metaDescMissingDetail', 'Write a 70–160 character meta description in the SEO panel.'), [], 8);
            } else if (md.length < 70) {
                add('metaDesc', 'content', 'warn', tc('metaDescShortTitle', 'Meta description too short'), fmt(tc('metaDescShortDetail', '{n} characters. Aim for 70–160.'), { n: md.length }), [], 4);
            } else if (md.length > 165) {
                add('metaDesc', 'content', 'warn', tc('metaDescLongTitle', 'Meta description too long'), fmt(tc('metaDescLongDetail', '{n} characters; Google truncates around 160.'), { n: md.length }), [], 4);
            } else {
                add('metaDesc', 'content', 'pass', tc('metaOkTitle', 'Meta description OK'), '', [], 0);
            }

            // ================= TECHNICAL =================
            const sdRaw = (state.meta.structuredData || '').trim();
            if (!sdRaw) {
                add('structuredData', 'technical', 'warn', tc('sdMissingTitle', 'No structured data'), tc('sdMissingDetail', 'Open the Structured Data panel and click "Build draft" to describe this page to search engines.'), [], 6);
            } else {
                let sdNodes = 0;
                try {
                    const parsed = JSON.parse(sdRaw);
                    const graph = Array.isArray(parsed) ? parsed : (parsed && parsed['@graph']) || (parsed ? [parsed] : []);
                    sdNodes = Array.isArray(graph) ? graph.length : 0;
                } catch (e) {
                    sdNodes = -1;
                }

                if (sdNodes < 0) {
                    add('structuredData', 'technical', 'fail', tc('sdBrokenTitle', 'Structured data is not valid JSON'), tc('sdBrokenDetail', 'Search engines will skip it entirely. Open the Structured Data panel to fix it.'), [], 10);
                } else if (sdNodes === 0) {
                    add('structuredData', 'technical', 'warn', tc('sdEmptyTitle', 'Structured data is empty'), tc('sdEmptyDetail', 'The graph has no nodes, so nothing is described.'), [], 6);
                } else {
                    add('structuredData', 'technical', 'pass', tc('sdOkTitle', 'Structured data present'), '', [], 0);
                }
            }

            // ---- SHARE TAGS ----
            // What a link to this page looks like on Facebook, LinkedIn, WhatsApp and
            // X. Nothing is filled in behind the author's back, so an empty field is a
            // missing tag — and a bare link where a card should be.
            const so = state.meta.social;
            const socialMissing = [];
            if (!so.title) socialMissing.push('og:title');
            if (!so.description) socialMissing.push('og:description');
            if (!so.image) socialMissing.push('og:image');
            if (socialMissing.length) {
                add('social', 'technical', 'warn', tc('socialMissingTitle', 'Share tags incomplete'),
                    fmt(tc('socialMissingDetail', 'Empty: {fields}. Page Settings › Social Sharing › "Auto-fill" fills them from the page.'), { fields: socialMissing.join(', ') }),
                    [], 3 + socialMissing.length * 2);
            } else if (!so.imageWidth || !so.imageHeight) {
                add('social', 'technical', 'warn', tc('socialNoSizeTitle', 'Share image has no size'),
                    tc('socialNoSizeDetail', 'Without og:image:width/height the first share can go out without the picture. "Fill from the image" in the Social Sharing panel writes them.'), [], 3);
            } else if (so.imageWidth < 600 || so.imageHeight < 315) {
                add('social', 'technical', 'warn', tc('socialSmallTitle', 'Share image is small'),
                    fmt(tc('socialSmallDetail', '{w}×{h}. Large cards need at least 600×315; 1200×630 is safe everywhere.'), { w: so.imageWidth, h: so.imageHeight }), [], 3);
            } else if (!so.imageAlt) {
                add('social', 'technical', 'warn', tc('socialNoAltTitle', 'Share image has no description'),
                    tc('socialNoAltDetail', 'og:image:alt tells screen-reader users on social networks what the picture shows.'), [], 2);
            } else {
                add('social', 'technical', 'pass', tc('socialOkTitle', 'Share tags complete'), '', [], 0);
            }

            // ---- THE PAGE'S OWN ADDRESS ----
            // og:url and the structured data name this page by its address, and
            // neither is rewritten when the page moves: a new slug or parent leaves
            // them pointing at the old one — a share card and a search result for an
            // address that now redirects, or 404s.
            const pageUrl = state.meta.pageUrl;
            if (pageUrl) {
                const norm = (u) => String(u || '').trim().split('#')[0].replace(/\/+$/, '').toLowerCase();
                const here = norm(pageUrl);
                let addressIssues = 0;
                if (!so.url) {
                    addressIssues++;
                    add('pageUrlSocial', 'technical', 'warn', tc('socialUrlMissingTitle', 'No share address'),
                        fmt(tc('socialUrlMissingDetail', 'og:url is empty. "Auto-fill" in the Social Sharing panel writes the page\'s address: {page}'), { page: pageUrl }), [], 3);
                } else if (norm(so.url) !== here) {
                    addressIssues++;
                    add('pageUrlSocial', 'technical', 'fail', tc('socialUrlWrongTitle', 'Share address is not this page\'s'),
                        fmt(tc('socialUrlWrongDetail', 'og:url is {url}, the page is at {page}. "Auto-fill" in the Social Sharing panel updates it.'), { url: so.url, page: pageUrl }), [], 6);
                }
                const stale = structuredDataAddresses(sdRaw).filter((u) => norm(u) !== here);
                if (stale.length) {
                    addressIssues++;
                    add('pageUrlSd', 'technical', 'fail', tc('sdUrlWrongTitle', 'Structured data names another address'),
                        fmt(tc('sdUrlWrongDetail', 'It says {urls}, the page is at {page}. Structured Data panel: rebuild the draft, or refresh the generated nodes.'),
                            { urls: [...new Set(stale.map((u) => u.split('#')[0]))].join(', '), page: pageUrl }), [], 6);
                }
                if (!addressIssues) add('pageUrls', 'technical', 'pass', tc('pageUrlsOkTitle', 'Share and structured data addresses match the page'), '', [], 0);
            }

            const imgs = q('img');
            // alt="" is not missing alt text: it is how a decorative image says "skip
            // me" — the flags beside the language switcher's "tr"/"en", for one.
            // Only an image with no alt attribute at all leaves a screen reader
            // guessing (it falls back to reading the file name).
            const noAlt = imgs.filter((i) => !i.hasAttribute('alt'));
            if (noAlt.length) {
                add('imgAlt', 'technical', 'fail', tc('imgAltTitle', 'Images without alt text'), fmt(tc('imgAltDetail', '{n} image(s) have no alt attribute. Alt text is required for SEO and screen readers.'), { n: noAlt.length }), mapComps(noAlt, compByEl), Math.min(12, 4 + noAlt.length * 2));
            } else if (imgs.length) {
                add('imgAlt', 'technical', 'pass', tc('imgAltOkTitle', 'All images have alt text'), '', [], 0);
            }

            const noDims = imgs.filter((i) => !i.getAttribute('width') || !i.getAttribute('height'));
            if (noDims.length) {
                add('imgDims', 'technical', 'warn', tc('imgNoDimsTitle', 'Images without width/height'), fmt(tc('imgNoDimsDetail', '{n} image(s) lack width/height attributes, causing layout shift (CLS).'), { n: noDims.length }), mapComps(noDims, compByEl), Math.min(8, noDims.length * 2));
            }

            // Displayed at a different shape than the file — the browser stretches
            // or squashes the pixels to fit. Judged on the rendered box, the way
            // Lighthouse judges it (its 2 % tolerance too), so a size forced by CSS
            // counts as much as width/height attributes. object-fit other than fill
            // does not distort (it crops or letterboxes), and an SVG has no fixed
            // pixels to distort, so both are left out.
            const distorted = imgs.filter((img) => {
                if (!img.complete || !img.naturalWidth || !img.naturalHeight) return false;
                if (/\.svg(\?|#|$)|^data:image\/svg/i.test(img.currentSrc || img.getAttribute('src') || '')) return false;
                const r = img.getBoundingClientRect();
                if (r.width < 8 || r.height < 8) return false;
                const fit = img.ownerDocument.defaultView.getComputedStyle(img).objectFit;
                if (fit && fit !== 'fill') return false;
                const natural = img.naturalWidth / img.naturalHeight;
                return Math.abs((r.width / r.height) - natural) / natural > 0.02;
            });
            if (distorted.length) {
                add('imgAspect', 'technical', 'warn', tc('imgAspectTitle', 'Images shown at the wrong proportions'), fmt(tc('imgAspectDetail', '{n} image(s) are displayed at a different aspect ratio than the file, so they look stretched or squashed. Select the image and use "Fit to ratio" in the size field, or set only one of width/height.'), { n: distorted.length }), mapComps(distorted, compByEl), Math.min(8, distorted.length * 2));
            }

            const lazyMissing = q('iframe').filter((f) => f.getAttribute('loading') !== 'lazy');
            if (lazyMissing.length) {
                add('lazy', 'technical', 'warn', tc('iframeNoLazyTitle', 'Iframes without lazy loading'), fmt(tc('iframeNoLazyDetail', '{n} iframe(s) load eagerly. Add loading="lazy" unless the iframe is above the fold.'), { n: lazyMissing.length }), mapComps(lazyMissing, compByEl), Math.min(6, lazyMissing.length * 2));
            }

            // Not '=== eager': an <img> with no loading attribute at all — most of
            // the block library's own templates, since only some blocks set it —
            // is eager by browser default just as surely as one that spells it out.
            // Checking the literal string missed the common case entirely, so a page
            // with five unmarked images (all genuinely competing for eager load,
            // exactly what this check exists to catch) reported nothing.
            const eagerImgs = imgs.filter((i) => i.getAttribute('loading') !== 'lazy');
            // The public site fetches every eager image at high priority. A logo and
            // an opening image is what that is for; past a few, they only slow one
            // another down — the advice says to keep it to what is at the top.
            const EAGER_LIMIT = 3;
            if (eagerImgs.length > EAGER_LIMIT) {
                add('multipleEager', 'technical', 'warn', tc('multipleEagerTitle', 'Too many images set to eager loading'), fmt(tc('multipleEagerDetail', '{n} images load eagerly (loading="eager", or no loading attribute at all, which means the same thing), and each is fetched at high priority. Keep it to what is visible at the top of the page — the logo, the opening image — and set the rest to loading="lazy".'), { n: eagerImgs.length }), mapComps(eagerImgs.slice(EAGER_LIMIT), compByEl), Math.min(6, (eagerImgs.length - EAGER_LIMIT) * 2));
            }

            let viewportImages = state.lastViewportImages;
            if (runDeviceTest) {
                viewportImages = await measureViewportImageLoading();
                state.lastViewportImages = viewportImages;
            }
            if (viewportImages) {
                const lazyAboveFold = new Set(), eagerBelowFold = new Set();
                const lazyDevices = [], eagerDevices = [];
                Object.keys(viewportImages).forEach((key) => {
                    const d = viewportImages[key];
                    if (!d) return;
                    if (d.lazyAboveFold.length) { d.lazyAboveFold.forEach((el) => lazyAboveFold.add(el)); lazyDevices.push(d.label); }
                    if (d.eagerBelowFold.length) { d.eagerBelowFold.forEach((el) => eagerBelowFold.add(el)); eagerDevices.push(d.label); }
                });

                // Only claim this passes when there was something to check. With no
                // images on the page the old code still added a green "Above-the-fold
                // images load eagerly" row, which inflated the category counters and
                // told the author a check had succeeded that never ran.
                const hasImagesToJudge = imgs.length > 0;
                if (lazyAboveFold.size) {
                    // "Set eager", not "or remove the attribute" — see multipleEager
                    // above: only an explicit loading="eager" gets preloaded.
                    add('aboveFoldLazy', 'technical', 'warn', tc('aboveFoldLazyTitle', 'Above-the-fold image set to lazy'), fmt(tc('aboveFoldLazyDetail', '{n} image(s) are visible without scrolling on {devices} but use loading="lazy", which delays paint. Set loading="eager" on them — spell it out rather than removing the attribute, because the published page only preloads an image that says "eager" explicitly.'), { n: lazyAboveFold.size, devices: lazyDevices.join(', ') }), mapComps(Array.from(lazyAboveFold), compByEl), Math.min(8, lazyAboveFold.size * 3));
                } else if (hasImagesToJudge) {
                    add('aboveFoldLazy', 'technical', 'pass', tc('aboveFoldLazyOkTitle', 'Above-the-fold images load eagerly'), '', [], 0);
                }

                if (eagerBelowFold.size) {
                    add('belowFoldEager', 'technical', 'warn', tc('belowFoldEagerTitle', 'Below-the-fold image not lazy-loaded'), fmt(tc('belowFoldEagerDetail', '{n} image(s) only appear after scrolling on {devices} but load eagerly. Add loading="lazy" so they don\'t compete with the initial paint.'), { n: eagerBelowFold.size, devices: eagerDevices.join(', ') }), mapComps(Array.from(eagerBelowFold), compByEl), Math.min(6, eagerBelowFold.size * 2));
                }
            }

            const links = q('a');
            const noText = links.filter((a) => !(a.textContent || '').trim() && !(a.getAttribute('aria-label') || '').trim() && !a.querySelector('img[alt]'));
            if (noText.length) {
                add('linkNoText', 'technical', 'fail', tc('linkNoTextTitle', 'Links without accessible text'), fmt(tc('linkNoTextDetail', '{n} link(s) have no text or aria-label; crawlers and screen readers see nothing.'), { n: noText.length }), mapComps(noText, compByEl), Math.min(8, noText.length * 3));
            }

            const generic = links.filter((a) => genericTexts.includes((a.textContent || '').trim().toLocaleLowerCase(L)));
            if (generic.length) {
                add('genericLink', 'technical', 'warn', tc('genericLinkTitle', 'Generic link text'), fmt(tc('genericLinkDetail', '{n} link(s) use text like "click here". Use descriptive anchor text.'), { n: generic.length }), mapComps(generic, compByEl), Math.min(6, generic.length * 2));
            }

            const emptyHref = links.filter((a) => {
                const h = a.getAttribute('href');
                if (h == null || h === '' || h === '#') return true;
                const hl = h.trim().toLowerCase();
                return hl.startsWith('javascript:void') || hl === 'javascript:;';
            });
            if (emptyHref.length) {
                add('emptyHref', 'technical', 'warn', tc('linkEmptyHrefTitle', 'Links without a destination'), fmt(tc('linkEmptyHrefDetail', '{n} link(s) have an empty, "#", or script href.'), { n: emptyHref.length }), mapComps(emptyHref, compByEl), Math.min(6, emptyHref.length * 2));
            }

            const badBlank = links.filter((a) => a.getAttribute('target') === '_blank' && !/noopener/.test(a.getAttribute('rel') || ''));
            if (badBlank.length) {
                add('blankRel', 'technical', 'warn', tc('blankRelTitle', 'target="_blank" without rel="noopener"'), fmt(tc('blankRelDetail', '{n} link(s) open a new tab without rel="noopener noreferrer" (security + SEO hygiene).'), { n: badBlank.length }), mapComps(badBlank, compByEl), Math.min(5, badBlank.length * 2));
            }

            const badTables = q('table').filter((t) => !t.querySelector('th'));
            if (badTables.length) {
                add('tableTh', 'technical', 'warn', tc('tableNoThTitle', 'Tables without header cells'), fmt(tc('tableNoThDetail', '{n} table(s) have no <th> cells; add scoped headers.'), { n: badTables.length }), mapComps(badTables, compByEl), Math.min(6, badTables.length * 3));
            }

            if (!body.querySelector('section,article,header,nav,main,footer,aside')) {
                add('landmarks', 'technical', 'warn', tc('semanticMissingTitle', 'No semantic landmarks'), tc('semanticMissingDetail', 'The page uses only generic containers. Use section/article/header/nav/footer so crawlers and assistive tech understand the structure.'), [], 4);
            }

            // ================= ACCESSIBILITY =================
            const badIframes = q('iframe').filter((f) => !(f.getAttribute('title') || '').trim());
            if (badIframes.length) {
                add('iframeTitle', 'a11y', 'warn', tc('iframeTitleTitle', 'Iframes without a title'), fmt(tc('iframeTitleDetail', '{n} iframe(s) have no title attribute.'), { n: badIframes.length }), mapComps(badIframes, compByEl), Math.min(6, badIframes.length * 2));
            }

            const navs = q('nav');
            if (navs.length > 1) {
                const unlabeled = navs.filter((n) => !(n.getAttribute('aria-label') || n.getAttribute('aria-labelledby')));
                if (unlabeled.length) {
                    add('navAria', 'a11y', 'warn', tc('navNoAriaTitle', 'Multiple navs without aria-label'), fmt(tc('navNoAriaDetail', '{n} of {t} nav elements are unlabeled; label each nav so they can be told apart.'), { n: unlabeled.length, t: navs.length }), mapComps(unlabeled, compByEl), 3);
                }
            }

            const fields = q('input,textarea,select').filter((f) => {
                const type = (f.getAttribute('type') || '').toLowerCase();
                return !['hidden', 'submit', 'button', 'reset', 'image'].includes(type);
            });
            const unlabeledFields = fields.filter((f) => {
                if ((f.getAttribute('aria-label') || '').trim()) return false;
                if (f.getAttribute('aria-labelledby')) return false;
                const id = f.getAttribute('id');
                if (id && body.querySelector('label[for="' + id.replace(/"/g, '\\"') + '"]')) return false;
                if (f.closest && f.closest('label')) return false;
                return true;
            });
            if (unlabeledFields.length) {
                add('inputLabel', 'a11y', 'warn', tc('inputNoLabelTitle', 'Form fields without labels'), fmt(tc('inputNoLabelDetail', '{n} field(s) have no <label> or aria-label. Placeholders disappear on input and are not labels.'), { n: unlabeledFields.length }), mapComps(unlabeledFields, compByEl), Math.min(9, unlabeledFields.length * 3));
            }

            const contrastFails = [];
            const smallFonts = [];
            let contrastChecked = 0;
            const allEls = body.querySelectorAll('*');
            for (let i = 0; i < allEls.length && i < SCAN_CAP; i++) {
                const el = allEls[i];
                if (!ownText(el) || !visible(el)) continue;
                const cs = el.ownerDocument.defaultView.getComputedStyle(el);
                const fs = parseFloat(cs.fontSize) || 16;
                if (fs < 12) smallFonts.push(el);
                const fg = parseColor(cs.color);
                if (!fg) continue;
                const bg = effectiveBg(el);
                if (!bg) continue;
                contrastChecked++;
                const weight = parseInt(cs.fontWeight, 10) || 400;
                const large = fs >= 24 || (fs >= 18.66 && weight >= 700);
                const needed = large ? 3 : 4.5;
                const ratio = contrastRatio(fg.a < 1 ? blend(fg, bg) : fg, bg);
                if (ratio < needed) contrastFails.push({ el, ratio, needed });
            }
            if (contrastFails.length) {
                contrastFails.sort((a, b) => a.ratio - b.ratio);
                const worst = contrastFails[0];
                add('contrast', 'a11y', 'fail', tc('contrastFailTitle', 'Insufficient color contrast'), fmt(tc('contrastFailDetail', '{n} element(s) fall below WCAG AA contrast (worst: {r}:1, required {req}:1).'), { n: contrastFails.length, r: worst.ratio.toFixed(2), req: worst.needed }), mapComps(contrastFails.map((f) => f.el), compByEl), Math.min(16, 4 + contrastFails.length * 3));
            } else if (contrastChecked > 0) {
                add('contrast', 'a11y', 'pass', tc('contrastOkTitle', 'Color contrast meets WCAG AA'), '', [], 0);
            }
            if (smallFonts.length) {
                add('smallFont', 'a11y', 'warn', tc('smallFontTitle', 'Text smaller than 12px'), fmt(tc('smallFontDetail', '{n} element(s) use fonts under 12px, which are hard to read on mobile.'), { n: smallFonts.length }), mapComps(smallFonts, compByEl), Math.min(6, smallFonts.length * 2));
            }

            const tiny = [];
            q('a,button').forEach((el) => {
                if (!visible(el)) return;
                const cs = el.ownerDocument.defaultView.getComputedStyle(el);
                if (cs.display === 'inline') return;
                if (!(el.textContent || '').trim() && !el.querySelector('*')) return;
                const r = el.getBoundingClientRect();
                if ((r.width > 0 && r.width < 24) || (r.height > 0 && r.height < 24)) tiny.push(el);
            });
            if (tiny.length) {
                add('tapTarget', 'a11y', 'warn', tc('tapTargetTitle', 'Tap targets too small'), fmt(tc('tapTargetDetail', '{n} interactive element(s) are smaller than 24×24px (WCAG 2.5.8 minimum).'), { n: tiny.length }), mapComps(tiny, compByEl), Math.min(6, tiny.length * 2));
            }

            // ================= RESPONSIVE =================
            const fixedWide = [];
            for (let i = 0; i < allEls.length && i < SCAN_CAP; i++) {
                const el = allEls[i];
                const w = el.style && el.style.width;
                if (w && /px$/.test(w) && parseFloat(w) >= 500) {
                    const cs = el.ownerDocument.defaultView.getComputedStyle(el);
                    if (cs.maxWidth === 'none') fixedWide.push(el);
                }
            }
            if (fixedWide.length) {
                add('fixedWidth', 'responsive', 'warn', tc('fixedWidthTitle', 'Fixed-width elements (500px+)'), fmt(tc('fixedWidthDetail', '{n} element(s) have a fixed pixel width of 500px+ with no max-width; they will overflow on phones.'), { n: fixedWide.length }), mapComps(fixedWide, compByEl), Math.min(8, fixedWide.length * 2));
            }

            let css = '';
            try { css = editor.getCss() || ''; } catch (e) { }
            if (css && css.indexOf('@media') === -1) {
                add('mediaQuery', 'responsive', 'warn', tc('noMediaQueryTitle', 'No @media rules in CSS'), tc('noMediaQueryDetail', 'The stylesheet has no media queries. Flex/grid can still adapt, but verify the mobile layout.'), [], 2);
            }

            let mobile = state.lastMobile;
            if (runDeviceTest) {
                mobile = await measureMobileOverflow(compByEl);
                state.lastMobile = mobile;
            }
            if (mobile) {
                if (mobile.overflow) {
                    add('mobileOverflow', 'responsive', 'fail', tc('mobileOverflowTitle', 'Horizontal overflow on mobile'), fmt(tc('mobileOverflowDetail', 'The page scrolls sideways at {w}px viewport width. Fix the highlighted elements.'), { w: mobile.width }), mobile.comps || [], 12);
                } else {
                    add('mobileOverflow', 'responsive', 'pass', fmt(tc('mobileOkTitle', 'No overflow at {w}px viewport'), { w: mobile.width }), '', [], 0);
                }
            }

            score = Math.max(0, Math.round(score));
            const rank = { fail: 0, warn: 1, pass: 2 };
            checks.sort((a, b) => rank[a.status] - rank[b.status]);
            return { score, checks };
        }

        // ------------------------------------------------------------
        // REPORT UI
        // ------------------------------------------------------------
        const CATS = [
            { id: 'content', label: () => tr('catContent', 'Content'), short: 'C' },
            { id: 'technical', label: () => tr('catTechnical', 'Technical'), short: 'T' },
            { id: 'a11y', label: () => tr('catAccessibility', 'Accessibility'), short: 'A' },
            { id: 'responsive', label: () => tr('catResponsive', 'Responsive'), short: 'R' }
        ];

        function buildReport(result) {
            const root = document.createElement('div');
            root.className = 'elevare-seo-report';
            const scoreCls = result.score >= 80 ? 'good' : result.score >= 50 ? 'mid' : 'bad';

            const chips = CATS.map((cat) => {
                const rows = result.checks.filter((c) => c.cat === cat.id);
                if (!rows.length) return '';
                const fails = rows.filter((c) => c.status === 'fail').length;
                const warns = rows.filter((c) => c.status === 'warn').length;
                const pass = rows.filter((c) => c.status === 'pass').length;
                const st = fails ? 'fail' : warns ? 'warn' : 'pass';
                return `<span class="elevare-seo-cat-chip ${st}"><span class="dot"></span>${esc(cat.label())} ${pass}/${rows.length}</span>`;
            }).join('');

            // A flagged element can live inside a linked template's content (the header
            // or footer shared across pages), which is locked on the page — "Locate"
            // jumps there but the author can't edit it in place and is left confused.
            // When that is the case, say so, so the fix ("edit the template") is clear.
            const inLinkedTemplate = (comps) => (comps || []).some((comp) => {
                try { const el = comp && comp.getEl && comp.getEl(); return !!(el && el.closest && el.closest('.elevare-tpl-ref')); }
                catch (e) { return false; }
            });

            const rowsHtml = result.checks.map((c, i) => {
                const cat = CATS.find((x) => x.id === c.cat);
                const ic = c.status === 'pass' ? '✓' : c.status === 'warn' ? '!' : '✕';
                const locate = (c.comps.length || c.focusSel)
                    ? `<button type="button" class="elevare-seo-locate" data-row="${i}">${esc(tr('locateBtn', 'Locate'))}</button>`
                    : '';
                const tplNote = (c.status !== 'pass' && inLinkedTemplate(c.comps))
                    ? `<small class="elevare-seo-tplnote">${esc(tr('linkedTemplateNote', 'Note: this content is inside a linked template (the shared header or footer) and cannot be edited from the page — open that template to fix it.'))}</small>`
                    : '';
                return `<div class="elevare-seo-row ${c.status}">
                    <span class="ic">${ic}</span>
                    <div class="bd">
                        <p><span class="elevare-seo-row-cat" title="${esc(cat ? cat.label() : '')}">${cat ? cat.short : ''}</span>${esc(c.title)}</p>
                        ${c.detail ? `<small>${esc(c.detail)}</small>` : ''}
                        ${tplNote}
                    </div>
                    ${locate}
                </div>`;
            }).join('');

            root.innerHTML = `
                <div class="elevare-seo-head">
                    <div class="elevare-seo-score ${scoreCls}">${result.score}<small>/100</small></div>
                    <div class="elevare-seo-headtxt">
                        <h3>${esc(tr('title', 'SEO Analysis'))}</h3>
                        <p>${esc(tr('subtitle', 'Content, technical SEO, accessibility and responsive checks.'))}</p>
                    </div>
                    <button type="button" class="elevare-seo-reanalyze">${esc(tr('reanalyzeBtn', 'Re-analyze'))}</button>
                </div>
                <div class="elevare-seo-cats">${chips}</div>
                <p class="elevare-seo-legend">${esc(tr('severityNote', 'The score is here to nudge the page toward perfect. A ✕ (fail) is what matters — fix these first. A ! (warning) is a small improvement; you do not have to clear every one.'))}</p>
                ${rowsHtml}`;

            root.querySelector('.elevare-seo-reanalyze').addEventListener('click', () => openModal(true));
            root.querySelectorAll('.elevare-seo-locate').forEach((btn) => {
                btn.addEventListener('click', () => {
                    const row = result.checks[+btn.getAttribute('data-row')];
                    if (!row) return;
                    // A meta finding (the page title) points at a field in the toolbar,
                    // not a canvas element: close the report, scroll the Başlık box into
                    // view and put the cursor in it, ready to edit.
                    if (row.focusSel) {
                        try {
                            editor.Modal.close();
                            const input = document.querySelector(row.focusSel);
                            if (input) {
                                if (input.scrollIntoView) input.scrollIntoView({ behavior: 'smooth', block: 'center' });
                                input.focus();
                                if (input.select) input.select();
                            }
                        } catch (e) { }
                        return;
                    }
                    const comp = row.comps[0];
                    if (!comp) return;
                    try {
                        editor.Modal.close();
                        editor.select(comp);
                        const el = comp.getEl();
                        if (el && el.scrollIntoView) el.scrollIntoView({ behavior: 'smooth', block: 'center' });
                    } catch (e) { }
                });
            });
            return root;
        }

        async function openModal(force) {
            if (state.analyzing) return;
            injectCss();
            const modal = editor.Modal;
            // Reopening the report from the toolbar button shows the LAST full result
            // as-is — no second device sweep. That sweep flips the canvas through
            // desktop/tablet/mobile and back, which is the slow, flickery part; running
            // it every time the author peeks at the panel is what made the button feel
            // heavy. A fresh analysis happens only on the first open (nothing measured
            // yet) or when the author asks for it with "Re-analyze" inside the window.
            if (state.lastResult && !force) {
                try {
                    modal.setTitle(esc(tr('title', 'SEO Analysis')));
                    modal.setContent(buildReport(state.lastResult));
                    modal.open();
                } catch (e) { console.warn('SEO report render failed', e); }
                return;
            }
            state.analyzing = true;
            try {
                modal.setTitle(esc(tr('title', 'SEO Analysis')));
                modal.setContent(`<div class="elevare-seo-loading"><div class="elevare-seo-spinner"></div><span>${esc(tr('loading', 'Analyzing page…'))}</span></div>`);
                modal.open();
                await wait(40);
                const result = await analyzeSeo(true);
                state.lastResult = result;
                state.lastScore = result.score;
                updateButton(result.score);
                notify(result.score);
                modal.setContent(buildReport(result));
            } catch (e) {
                console.warn('SEO analysis failed', e);
                try { modal.close(); } catch (e2) { }
            } finally {
                state.analyzing = false;
            }
        }

        async function silentAnalyze() {
            if (state.analyzing) return;
            state.analyzing = true;
            try {
                const result = await analyzeSeo(false);
                state.lastScore = result.score;
                updateButton(result.score);
                notify(result.score);
            } catch (e) { } finally {
                state.analyzing = false;
            }
        }

        let booted = false;
        const boot = () => { if (booted) return; booted = true; setTimeout(silentAnalyze, 900); };
        editor.on('load', boot);
        setTimeout(boot, 1500);

        return {
            updateMeta(meta) {
                const nm = meta || {};
                const pick = (a, b) => (nm[a] !== undefined ? nm[a] : nm[b]);
                const fk = pick('focusKeyword', 'FocusKeyword');
                const mdv = pick('metaDescription', 'MetaDescription');
                const pt = pick('pageTitle', 'PageTitle');
                const ts = pick('titleSuffix', 'TitleSuffix');
                const sd = pick('structuredData', 'StructuredData');
                const so = pick('social', 'Social');
                const pu = pick('pageUrl', 'PageUrl');
                if (pu !== undefined) state.meta.pageUrl = String(pu == null ? '' : pu).trim();
                if (fk !== undefined) state.meta.focusKeyword = String(fk == null ? '' : fk);
                if (mdv !== undefined) state.meta.metaDescription = String(mdv == null ? '' : mdv);
                if (pt !== undefined) state.meta.pageTitle = String(pt == null ? '' : pt);
                if (ts !== undefined) state.meta.titleSuffix = String(ts == null ? '' : ts);
                if (sd !== undefined) state.meta.structuredData = String(sd == null ? '' : sd);
                if (so !== undefined) state.meta.social = normalizeSocial(so);
                clearTimeout(state.debounce);
                state.debounce = setTimeout(silentAnalyze, 500);
            },
            reanalyze() { openModal(true); }
        };
    }

    return { attach };
})();