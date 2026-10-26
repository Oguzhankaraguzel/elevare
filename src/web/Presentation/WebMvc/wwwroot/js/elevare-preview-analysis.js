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

    // The page as the server wrote it ends here: this script is near the end of
    // the body, and what scripts append afterwards (the cookie banner, the
    // captcha badge and its frames) lands after it. Those are the site's or a
    // third party's widgets, not the page, so they are left out of the copy;
    // the structured data printed after this script stays.
    function pageHtml() {
        var copy = document.documentElement.cloneNode(true);
        var body = document.body, copyBody = copy.querySelector('body');
        if (!body || !copyBody || script.parentNode !== body) return copy.outerHTML;
        var kids = body.children, drop = [], after = false;
        for (var i = 0; i < kids.length; i++) {
            var k = kids[i];
            if (k === script) { after = true; continue; }
            if (after && !(k.tagName === 'SCRIPT' && k.type === 'application/ld+json')) drop.push(copyBody.children[i]);
        }
        drop.forEach(function (n) { if (n) n.remove(); });
        return copy.outerHTML;
    }

    function send() {
        try {
            window.parent.postMessage({
                type: 'elevare-seo-snapshot',
                html: '<!doctype html>' + pageHtml(),
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
