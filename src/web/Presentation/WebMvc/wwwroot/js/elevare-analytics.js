// Anonymous page-view telemetry (view + time-on-page beacon). Skipped for signed
// preview links — see the conditional around this script's <script> tag in
// _Layout.cshtml — so a CMS editor testing Draft/Archived content never pollutes
// real analytics.
(function () {
    // Time on page counts only while the tab is in view. It is reported each time
    // the visitor leaves (switches tab, minimises, closes); the server keeps the
    // largest value, so a visitor who comes back and reads on is not cut short at
    // the first time they looked away, as they were when it was reported only once.
    var visibleMs = 0, visibleSince = document.visibilityState === 'hidden' ? 0 : Date.now();
    var hitId = 0, reportedMs = -1;

    // Whether the visitor allowed analytics in the cookie banner. Read from the
    // banner's own cookie rather than its API, which may not have loaded yet when
    // this runs. The banner is vanilla-cookieconsent, set up under Site Codes with
    // an "analytics" category; with no banner there is no consent to read, and the
    // server then keeps no visitor cookie. elevare-interactions.js has the same check.
    function hasAnalyticsConsent() {
        var m = document.cookie.match(/(?:^|;\s*)cc_cookie=([^;]*)/);
        if (!m) return false;
        try { return (JSON.parse(decodeURIComponent(m[1])).categories || []).indexOf('analytics') >= 0; }
        catch (_) { return false; }
    }

    fetch('/api/track/view', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ path: location.pathname, title: document.title, consent: hasAnalyticsConsent() }),
        keepalive: true
    }).then(function (r) { return r.ok ? r.json() : null; })
      .then(function (d) { if (d && d.id) hitId = d.id; })
      .catch(function () { /* telemetry must never break the page */ });

    function pause() {
        if (visibleSince) { visibleMs += Date.now() - visibleSince; visibleSince = 0; }
    }

    function reportDuration() {
        pause();
        if (!hitId || visibleMs === reportedMs) return;
        reportedMs = visibleMs;
        var payload = JSON.stringify({ id: hitId, seconds: Math.round(visibleMs / 1000) });
        if (navigator.sendBeacon) {
            navigator.sendBeacon('/api/track/duration', new Blob([payload], { type: 'application/json' }));
        } else {
            fetch('/api/track/duration', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: payload, keepalive: true })
                .catch(function () { });
        }
    }
    addEventListener('pagehide', reportDuration);
    addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'hidden') reportDuration();
        else if (!visibleSince) visibleSince = Date.now();
    });
})();
