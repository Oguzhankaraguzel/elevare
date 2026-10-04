// SITE CODES EDITOR — Monaco (the editor VS Code itself is built on), vendored
// locally under wwwroot/lib/monaco-editor rather than pulled from a CDN, same as
// every other library this app ships with.
// ---------------------------------------------------------------------------
// Replaces the plain <textarea> the Site Codes screen used to edit HTML/CSS/JS
// snippets in — real syntax highlighting, bracket matching, and (for CSS, for
// free, built into Monaco's own language service) an inline colour swatch next
// to every hex/rgb value that opens the browser's native colour picker when
// clicked. No custom colour-picker UI was written for this — Monaco already
// does it for CSS.
window.elevareCodeEditor = (function () {
    const BASE = '/lib/monaco-editor/vs';
    let loading = null;
    const editors = {};
    const escHandlers = {};

    function loadMonaco() {
        if (window.monaco) return Promise.resolve(true);
        if (loading) return loading;

        loading = new Promise((resolve) => {
            const existing = document.querySelector('script[data-elevare-monaco-loader]');
            const onLoaderReady = () => {
                try {
                    window.require.config({ paths: { vs: BASE } });
                    window.require(['vs/editor/editor.main'], () => {
                        // These fields hold pasted vendor snippets (analytics tags, consent
                        // scripts, custom CSS), not authored application code — and Site
                        // Codes requires the content to be complete, wrapper tags included
                        // (see SiteCodeValidator). The JS/CSS language services don't know
                        // that: they see a bare <script>/<style> tag as a syntax error and
                        // underline it in red, even though the save is correct. Turning off
                        // validation removes that false alarm, and as a side effect stops
                        // linting vendor code nobody asked to have checked (undeclared
                        // globals like `dataLayer`, etc.).
                        try {
                            window.monaco.languages.typescript.javascriptDefaults.setDiagnosticsOptions({
                                noSemanticValidation: true,
                                noSyntaxValidation: true,
                            });
                            window.monaco.languages.css.cssDefaults.setDiagnosticsOptions({ validate: false });
                        } catch (e) { /* language service not present in this Monaco build */ }
                        resolve(!!window.monaco);
                    });
                } catch (e) {
                    console.warn('Monaco failed to initialise', e);
                    resolve(false);
                }
            };
            if (existing) {
                if (window.require) onLoaderReady();
                else existing.addEventListener('load', onLoaderReady);
                return;
            }
            const s = document.createElement('script');
            s.src = `${BASE}/loader.js`;
            s.setAttribute('data-elevare-monaco-loader', '1');
            s.onload = onLoaderReady;
            s.onerror = () => { console.warn('Monaco loader.js failed to load'); resolve(false); };
            document.head.appendChild(s);
        });
        return loading;
    }

    // SiteCodeKind (Script/Style/MetaTag/RawHtml) -> Monaco language id.
    // MetaTag and RawHtml are both just markup fragments, so both get 'html'.
    function languageFor(kind) {
        switch (kind) {
            case 'Style': return 'css';
            case 'Script': return 'javascript';
            default: return 'html';
        }
    }

    return {
        /// Mounts an editor into the empty <div id="elementId">. `dotNetRef` receives
        /// OnContentChanged(value) on every edit, debounced by Monaco's own model
        /// change event coalescing — not per-keystroke on the wire.
        attach: async function (elementId, initialValue, kind, dotNetRef) {
            const ok = await loadMonaco();
            if (!ok) return false;

            const el = document.getElementById(elementId);
            if (!el) return false;

            // Re-opening the same panel reuses the id; dispose the previous instance
            // first or its DOM nodes leak inside the new one.
            if (editors[elementId]) {
                try { editors[elementId].dispose(); } catch (e) { /* already gone */ }
                delete editors[elementId];
            }

            const isDark = (document.documentElement.getAttribute('data-theme') || '').includes('dark')
                || document.body.classList.contains('theme-dark');

            const ed = window.monaco.editor.create(el, {
                value: typeof initialValue === 'string' ? initialValue : '',
                language: languageFor(kind),
                theme: isDark ? 'vs-dark' : 'vs',
                automaticLayout: true,
                minimap: { enabled: false },
                fontSize: 13,
                scrollBeyondLastLine: false,
                wordWrap: 'on',
                tabSize: 2,
            });

            if (dotNetRef) {
                ed.onDidChangeModelContent(() => {
                    try { dotNetRef.invokeMethodAsync('OnContentChanged', ed.getValue()); }
                    catch (e) { /* the component may already be gone */ }
                });
            }

            editors[elementId] = ed;

            // Only swap the visible control once Monaco has actually mounted — until
            // now the plain textarea was the real (and only) editor.
            const fallback = document.getElementById(`${elementId}-fallback`);
            if (fallback) fallback.style.display = 'none';
            el.style.display = 'block';

            return true;
        },

        /// Authoritative read used right before saving.
        getValue: function (elementId) {
            const ed = editors[elementId];
            return ed ? ed.getValue() : null;
        },

        destroy: function (elementId) {
            const ed = editors[elementId];
            if (!ed) return;
            try { ed.dispose(); } catch (e) { /* already gone */ }
            delete editors[elementId];
            this.exitFullscreen(elementId);
        },

        /// Relayouts the editor — needed after its container changes size outside
        /// Monaco's own ResizeObserver tick (a CSS class toggle applied the same
        /// frame, for instance), so the canvas doesn't wait for the next resize to catch up.
        relayout: function (elementId) {
            const ed = editors[elementId];
            if (ed) ed.layout();
        },

        /// Escape closes fullscreen — wired here rather than left to bubble because
        /// Monaco owns keydown while focused and only forwards Escape to the page
        /// when it has nothing of its own open (a suggestion popup, for instance)
        /// to close first with it.
        enterFullscreen: function (elementId, dotNetRef) {
            this.exitFullscreen(elementId);
            const onKey = (e) => {
                if (e.key === 'Escape') {
                    e.preventDefault();
                    dotNetRef.invokeMethodAsync('OnFullscreenEscape').catch(() => { /* component gone */ });
                }
            };
            escHandlers[elementId] = onKey;
            document.addEventListener('keydown', onKey);
        },

        exitFullscreen: function (elementId) {
            const onKey = escHandlers[elementId];
            if (onKey) {
                document.removeEventListener('keydown', onKey);
                delete escHandlers[elementId];
            }
        }
    };
})();
