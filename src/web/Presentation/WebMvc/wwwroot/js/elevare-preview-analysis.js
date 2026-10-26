// Sends this preview, as rendered, to the CMS page editor that opened it for SEO
// analysis (see PreviewAnalysis in SharedKernel). Included only on a signed
// preview whose token names that editor's origin, and posts only to that origin:
// the page itself is never exposed to anyone else, and the response headers let
// no one else frame it.
//
// The document goes over once the page has loaded and its own scripts have had a
// moment to run, so what the analyzer reads is what a visitor gets — resolved
// links, filled-in image attributes, the linked templates' current content.
(function () {
    'use strict';
    var script = document.currentScript;
    var origin = script && script.getAttribute('data-origin');
    if (!origin || window.parent === window) return;

    var SETTLE_MS = 400;

    function send() {
        try {
            window.parent.postMessage({
                type: 'elevare-seo-snapshot',
                html: '<!doctype html>' + document.documentElement.outerHTML,
                url: location.origin + '/',
                theme: document.documentElement.getAttribute('data-theme') || ''
            }, origin);
        } catch (e) {
            // The editor falls back to analysing its own canvas when nothing arrives.
        }
    }

    function later() { setTimeout(send, SETTLE_MS); }

    if (document.readyState === 'complete') later();
    else window.addEventListener('load', later, { once: true });
})();
