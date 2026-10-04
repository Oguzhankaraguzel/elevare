// STANDALONE RICH-TEXT EDITOR
// ---------------------------------------------------------------------------
// Turns a plain <textarea> into a CKEditor instance, for the places outside the
// page builder that compose HTML — e-mail replies and reply templates.
//
// Deliberately reuses the CKEditor build the page builder already loads instead of
// adding a second editor library: same toolbar, same Turkish translation, and a
// visitor who has opened the page editor once has it cached already. Loaded lazily
// on first use, so a session that never composes an e-mail never downloads it.
//
// Unlike the page builder's editor this one is CLASSIC, not inline: it owns a box
// of its own with a docked toolbar. None of the iframe/selection problems that
// forced the builder's toolbar to float apply here — the editable and the toolbar
// live in the same document.
window.elevareHtmlEditor = (function () {
    const CKEDITOR_URL = 'https://cdn.ckeditor.com/4.22.1/full-all/ckeditor.js';
    let loading = null;

    function loadCkeditor() {
        if (window.CKEDITOR) return Promise.resolve(true);
        if (loading) return loading;

        loading = new Promise((resolve) => {
            const existing = document.querySelector(`script[data-elevare-cke], script[data-gjs="${CKEDITOR_URL}"]`);
            if (existing) {
                existing.addEventListener('load', () => resolve(!!window.CKEDITOR));
                existing.addEventListener('error', () => resolve(false));
                return;
            }
            const s = document.createElement('script');
            s.src = CKEDITOR_URL;
            s.async = false;
            s.setAttribute('data-elevare-cke', '1');
            s.onload = () => resolve(!!window.CKEDITOR);
            s.onerror = () => { console.warn('CKEditor failed to load'); resolve(false); };
            document.head.appendChild(s);
        });
        return loading;
    }

    // A composer for one e-mail, not a page: no page-structure tools (Format/Div/
    // Table are fine, but Source stays so a developer can paste prepared markup),
    // and none of the buttons excluded in grapes-editor.js for the same reasons —
    // Image would bypass the Media Library, Smiley pulls an <img> off a third-party
    // CDN into somebody's inbox, and the paid services must not see the content.
    function config(language) {
        return {
            language: language || 'tr',
            versionCheck: false,
            removePlugins: 'exportpdf,scayt,wsc,elementspath',
            resize_enabled: true,
            height: 260,
            // The reply is an e-mail: mail clients ignore <style> blocks and classes,
            // so formatting has to survive as inline attributes on the elements.
            allowedContent: true,
            extraAllowedContent: '*(*);*{*}',
            format_tags: 'p;h1;h2;h3;h4',
            toolbar: [
                { name: 'styles', items: ['Format', 'Font', 'FontSize'] },
                { name: 'basicstyles', items: ['Bold', 'Italic', 'Underline', 'Strike', 'RemoveFormat'] },
                { name: 'colors', items: ['TextColor', 'BGColor'] },
                { name: 'paragraph', items: ['NumberedList', 'BulletedList', 'Outdent', 'Indent', 'Blockquote'] },
                { name: 'align', items: ['JustifyLeft', 'JustifyCenter', 'JustifyRight'] },
                { name: 'links', items: ['Link', 'Unlink'] },
                { name: 'insert', items: ['Table', 'HorizontalRule', 'SpecialChar'] },
                { name: 'clipboard', items: ['PasteText', 'PasteFromWord'] },
                { name: 'document', items: ['Source'] },
                { name: 'undo', items: ['Undo', 'Redo'] }
            ]
        };
    }

    function instance(id) {
        return window.CKEDITOR && window.CKEDITOR.instances
            ? window.CKEDITOR.instances[id]
            : null;
    }

    return {
        /// Replaces the textarea with id `elementId`. `dotNetRef` receives
        /// OnHtmlChanged(html) as the user types — debounced by CKEditor's own change
        /// event, so Blazor is not re-rendered per keystroke.
        attach: async function (elementId, initialHtml, dotNetRef) {
            const ok = await loadCkeditor();
            if (!ok) return false;

            const el = document.getElementById(elementId);
            if (!el) return false;

            // Re-opening the same modal reuses the id; the previous instance has to go
            // or CKEditor refuses to replace the element.
            const prev = instance(elementId);
            if (prev) { try { prev.destroy(true); } catch (e) { } }

            const lang = (document.documentElement.lang || 'tr').split('-')[0];
            const ed = window.CKEDITOR.replace(elementId, config(lang));

            ed.on('instanceReady', () => {
                if (typeof initialHtml === 'string' && initialHtml.length) ed.setData(initialHtml);
            });

            if (dotNetRef) {
                const push = () => {
                    try { dotNetRef.invokeMethodAsync('OnHtmlChanged', ed.getData()); }
                    catch (e) { /* the component may already be gone */ }
                };
                ed.on('change', push);
                // 'change' does not fire for source-mode edits or paste-from-source.
                ed.on('mode', push);
                ed.on('blur', push);
            }
            return true;
        },

        /// Authoritative read used right before saving — 'change' can lag the very
        /// last keystroke, and a reply that loses its final sentence is worse than a
        /// slightly chattier interop.
        getData: function (elementId) {
            const ed = instance(elementId);
            return ed ? ed.getData() : null;
        },

        setData: function (elementId, html) {
            const ed = instance(elementId);
            if (ed) ed.setData(html || '');
            return !!ed;
        },

        destroy: function (elementId) {
            const ed = instance(elementId);
            if (!ed) return false;
            try { ed.destroy(true); } catch (e) { }
            return true;
        }
    };
})();
