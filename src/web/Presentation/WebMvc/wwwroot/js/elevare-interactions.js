// Shared runtime for content authored in the CMS page builder — pop-ups, side
// panels, dropdowns, form submit actions, multi-step form navigation, code copy,
// image zoom, tabs, reading progress and "back to top". One small file, loaded once
// by the public site's layout, instead of per-page inline scripts.
(function () {
    'use strict';

    // ---------------------------------------------------------------
    // Scroll animation helpers (used by the sections below, defined here so the
    // dropdown/pop-up/step code can call them regardless of source order)
    // ---------------------------------------------------------------
    var animObserver = null;

    function revealAnimated(el) {
        var delay = el.getAttribute('data-elevare-anim-delay');
        if (delay) el.style.setProperty('--elevare-anim-delay', (parseInt(delay, 10) || 0) / 1000 + 's');
        el.classList.add('elevare-anim-in');
        if (animObserver) animObserver.unobserve(el);
    }

    /// Replays the entrance animation for anything animated inside a container that
    /// has just been shown. Without this, an element in a dropdown or pop-up either
    /// never animates (it was revealed at load, while still hidden) or never appears
    /// at all — both of which read as "the animation does not work".
    function playAnimationsIn(container) {
        if (!container || !document.documentElement.classList.contains('elevare-anim-ready')) return;
        var items = container.querySelectorAll('[data-elevare-anim]');
        if (!items.length) return;

        for (var i = 0; i < items.length; i++) items[i].classList.remove('elevare-anim-in');

        // Two frames, not a forced reflow. The container went from display:none to
        // visible in this very task, so its contents have never been rendered — and a
        // CSS transition needs a "before" style that was actually painted, or it is
        // skipped entirely and the element just appears. void offsetWidth flushes
        // layout but does not give the compositor a painted frame to transition from;
        // measured on the live site, the element went straight to opacity 1 with no
        // animation at all. Waiting for the next frame lets the hidden state paint
        // once; the second frame is the belt-and-braces the idiom is known for.
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                for (var j = 0; j < items.length; j++) revealAnimated(items[j]);
            });
        });
    }

    // ---------------------------------------------------------------
    // Skip link (WCAG 2.4.1 Bypass Blocks)
    // ---------------------------------------------------------------
    // Jumps to <main> itself, which is now genuinely the start of the page's own
    // content: PageRegionSplitter lifts the page's top-level <header>/<footer> out
    // of <main> before the layout renders them, so there is no menu inside it to
    // land on any more.
    //
    // This used to target the first <h1> inside <main> instead, because the menu WAS
    // main's first child and a plain jump landed right back on it. That workaround
    // had a quiet failure of its own: put an <h1> in the menu — a logo as <h1> is a
    // common enough pattern — and the skip link jumped into the header again, with
    // nothing to say so. Fixing the structure removed the need for the trick and the
    // trap with it.
    document.addEventListener('click', function (e) {
        var link = e.target.closest('.skip-link');
        if (!link) return;

        var main = document.getElementById('main-content');
        if (!main) return;

        var target = main;
        e.preventDefault();

        // Anything that isn't natively focusable (an <h1>, or <main> itself) needs
        // tabindex="-1" to accept programmatic focus at all — it stays out of the
        // normal Tab order (browsers don't treat -1 as "add to tab sequence"),
        // this is solely so .focus() below has somewhere valid to land.
        if (!target.hasAttribute('tabindex')) target.setAttribute('tabindex', '-1');
        target.focus({ preventScroll: false });
    });

    // ---------------------------------------------------------------
    // In-page anchors: smooth, and never under a pinned header
    // ---------------------------------------------------------------
    // The header template is usually sticky, and a browser scrolls an anchor's
    // target to the very top of the viewport — straight under it (a table-of-
    // contents link in an article landed its heading 119px deep behind the menu).
    // scroll-padding-top on the root is what every browser consults for that
    // scroll, whatever started it: a #fragment link, a page opened at /page#part,
    // scrollIntoView, focus. So the fix is to keep it equal to whatever is pinned to
    // the top, rather than to take over the click. It is measured, not configured:
    // the header differs per site and per breakpoint, and grows when its mobile
    // menu opens.
    (function initAnchorOffset() {
        var root = document.documentElement;
        var GAP = 16;
        var observed = [];
        var resizeObserver = window.ResizeObserver ? new ResizeObserver(update) : null;

        // A top bar: fixed or sticky, pinned at the viewport's top edge, at least
        // half the viewport wide. The width test is what keeps a sticky sidebar (a
        // table of contents beside an article) from counting as a header. Only
        // candidates a header can plausibly be are inspected — the page's first two
        // levels and any <header> outside the content — not every element.
        function pinnedBars() {
            var body = document.body;
            if (!body) return [];
            var candidates = [];
            for (var i = 0; i < body.children.length; i++) {
                candidates.push(body.children[i]);
                for (var j = 0; j < body.children[i].children.length; j++) candidates.push(body.children[i].children[j]);
            }
            var headers = document.getElementsByTagName('header');
            for (var k = 0; k < headers.length; k++) {
                if (!headers[k].closest('main, article')) candidates.push(headers[k]);
            }
            var bars = [];
            for (var n = 0; n < candidates.length; n++) {
                var el = candidates[n];
                if (bars.indexOf(el) !== -1) continue;
                var cs = getComputedStyle(el);
                if (cs.position !== 'sticky' && cs.position !== 'fixed') continue;
                if (cs.top === 'auto' || parseFloat(cs.top) > 1) continue;
                if (cs.display === 'none' || cs.visibility === 'hidden') continue;
                var rect = el.getBoundingClientRect();
                if (rect.height <= 0 || rect.width < window.innerWidth / 2) continue;
                bars.push(el);
            }
            return bars;
        }

        function update() {
            var bars = pinnedBars();
            var offset = 0;
            for (var i = 0; i < bars.length; i++) {
                offset = Math.max(offset, (parseFloat(getComputedStyle(bars[i]).top) || 0) + bars[i].getBoundingClientRect().height);
            }
            root.style.scrollPaddingTop = Math.ceil(offset + GAP) + 'px';
            if (resizeObserver) {
                for (var j = 0; j < bars.length; j++) {
                    if (observed.indexOf(bars[j]) === -1) { resizeObserver.observe(bars[j]); observed.push(bars[j]); }
                }
            }
        }

        // Instant, regardless of scroll-behavior: used only to correct a jump the
        // browser already made, which should not then glide a second time.
        function alignToHash() {
            if (!location.hash || location.hash.length < 2) return false;
            var target;
            try { target = document.getElementById(decodeURIComponent(location.hash.slice(1))); } catch (e) { return false; }
            if (!target) return false;
            var previous = root.style.scrollBehavior;
            root.style.scrollBehavior = 'auto';
            target.scrollIntoView(true);
            root.style.scrollBehavior = previous;
            return true;
        }

        function isSamePageAnchor(link) {
            var href = link.getAttribute('href') || '';
            if (href.charAt(0) === '#') return href.length > 1;
            return !!link.hash && link.host === location.host &&
                link.pathname.replace(/\/$/, '') === location.pathname.replace(/\/$/, '');
        }

        // A link inside an open mobile menu: close the menu first, and instantly,
        // so the header is back to its closed height before the browser works out
        // where to scroll. Left open, the menu would sit over the section just
        // reached; closed by its own animation, it would shrink mid-scroll and leave
        // the heading a menu's height too low.
        function closeMenuAround(link) {
            var items = link.closest('.navbar-items-c');
            if (items) {
                var container = items.parentElement;
                var burger = container && container.querySelector('.navbar-burger');
                if (burger && getComputedStyle(burger).display !== 'none' && parseInt(items.style.maxHeight, 10) > 0) {
                    var transition = items.style.transition;
                    items.style.transition = 'none';
                    items.style.maxHeight = '0px';
                    void items.offsetHeight;
                    items.style.transition = transition;
                }
            }
            var list = link.closest('.el-nav-links.open');
            if (list) {
                list.classList.remove('open');
                var bar = list.closest('.el-navbar-in');
                var toggle = bar && bar.querySelector('.el-nav-toggle');
                if (toggle) toggle.setAttribute('aria-expanded', 'false');
            }
        }

        // Capture phase: runs before the link's default action, which is the
        // browser's own scroll — so the padding it reads is already right.
        document.addEventListener('click', function (e) {
            var link = e.target.closest && e.target.closest('a[href*="#"]');
            if (!link || !isSamePageAnchor(link)) return;
            closeMenuAround(link);
            update();
        }, true);

        var resizeTimer = 0;
        window.addEventListener('resize', function () {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(update, 100);
        });

        update();
        // A page opened at /page#part was scrolled by the browser before this ran,
        // with no padding yet; put the target where it belongs. Once more on load,
        // since images above it may have taken their height since — but only if
        // the visitor has not scrolled on in the meantime.
        var alignedAt = alignToHash() ? window.scrollY : -1;
        window.addEventListener('load', function () {
            update();
            if (alignedAt !== -1 && window.scrollY === alignedAt) alignToHash();
        });

        // Smooth from here on — set after the correction above so that one is not
        // animated, and not at all for visitors who asked for less motion.
        var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (!reduced) root.style.scrollBehavior = 'smooth';
    })();

    // ---------------------------------------------------------------
    // Pop-ups
    // ---------------------------------------------------------------
    function findPopup(id) {
        return document.querySelector('[data-elevare-popup-id="' + id.replace(/"/g, '\\"') + '"]');
    }

    // Elements a keyboard user can actually land on, in document order — used both
    // to move focus into a freshly-opened popup and to trap Tab inside it.
    function focusableIn(container) {
        var all = container.querySelectorAll(
            'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'
        );
        return Array.prototype.filter.call(all, function (el) { return el.offsetParent !== null; });
    }

    // The element focus returns to when the popup closes — whatever had focus right
    // before it opened (normally its trigger). A plain variable, not a stack: real
    // popup content never opens a second popup from within the first, so there is
    // only ever one to remember.
    var popupReturnFocus = null;

    function openPopup(id) {
        var el = findPopup(id);
        if (!el) return;
        popupReturnFocus = document.activeElement;
        el.style.display = 'flex';
        el.classList.add('is-open');
        // Idempotent, and applied at runtime rather than authored into every block
        // so pop-ups already saved on existing pages pick this up too.
        el.setAttribute('role', 'dialog');
        el.setAttribute('aria-modal', 'true');
        if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex', '-1');
        var focusables = focusableIn(el);
        (focusables[0] || el).focus({ preventScroll: true });
        // Same reason as the dropdown: anything animated inside was hidden at load,
        // so its entrance has to be played when the pop-up actually opens.
        playAnimationsIn(el);
    }

    function closePopup(id) {
        var el = typeof id === 'string' ? findPopup(id) : id;
        if (!el) return;
        el.style.display = 'none';
        el.classList.remove('is-open');
        if (popupReturnFocus && typeof popupReturnFocus.focus === 'function') {
            popupReturnFocus.focus({ preventScroll: true });
        }
        popupReturnFocus = null;
    }

    function closeAllPopups() {
        var open = document.querySelectorAll('.elevare-popup.is-open, [data-elevare-popup-id].is-open');
        for (var i = 0; i < open.length; i++) closePopup(open[i]);
    }

    window.elevarePopup = { open: openPopup, close: closePopup, closeAll: closeAllPopups };

    // Delegated click handling: works for any trigger added anywhere on the
    // page, including content injected later (e.g. after a form submit).
    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-elevare-popup-trigger]');
        if (trigger) {
            e.preventDefault();
            openPopup(trigger.getAttribute('data-elevare-popup-trigger'));
            return;
        }
        var closeBtn = e.target.closest('[data-elevare-popup-close]');
        if (closeBtn) {
            var popup = closeBtn.closest('[data-elevare-popup-id]');
            if (popup) closePopup(popup);
            return;
        }
        // Clicking the semi-transparent overlay (the popup root itself, not its
        // inner box) closes it too.
        var popupRoot = e.target.closest('[data-elevare-popup-id]');
        if (popupRoot && e.target === popupRoot) closePopup(popupRoot);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') { closeAllPopups(); closeAllDropdowns(); closeAllSidebars(); return; }

        // Focus trap: while a popup is open, Tab must cycle within it instead of
        // escaping to the page underneath (which a sighted mouse user can't see is
        // still there, but a keyboard user would otherwise land right back on it).
        if (e.key === 'Tab') {
            var openPopupEl = document.querySelector('.elevare-popup.is-open, [data-elevare-popup-id].is-open');
            if (!openPopupEl) return;
            var focusables = focusableIn(openPopupEl);
            if (!focusables.length) { e.preventDefault(); return; }
            var first = focusables[0], last = focusables[focusables.length - 1];
            if (e.shiftKey && document.activeElement === first) {
                e.preventDefault(); last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault(); first.focus();
            }
        }
    });

    // ---------------------------------------------------------------
    // Dropdowns / Mega menus — generic trigger+panel toggle, reusable by any
    // block (not just the built-in Mega Menü one). A trigger carries
    // data-elevare-dropdown-trigger="ID" and its panel carries
    // data-elevare-dropdown-panel="ID" with the same ID.
    // ---------------------------------------------------------------
    function findDropdownPanel(id) {
        return document.querySelector('[data-elevare-dropdown-panel="' + id.replace(/"/g, '\\"') + '"]');
    }

    function closeAllDropdowns() {
        var open = document.querySelectorAll('[data-elevare-dropdown-panel].is-open');
        for (var i = 0; i < open.length; i++) {
            open[i].style.display = 'none';
            open[i].classList.remove('is-open');
        }
    }

    function toggleDropdown(id) {
        var panel = findDropdownPanel(id);
        if (!panel) return;
        var wasOpen = panel.classList.contains('is-open');
        closeAllDropdowns();
        if (!wasOpen) {
            // Clear the inline "none" set by closeAllDropdowns/the initial script
            // so the panel's own authored display (grid/flex/block) takes over.
            panel.style.display = '';
            panel.classList.add('is-open');
            playAnimationsIn(panel);
        }
    }

    window.elevareDropdown = { toggle: toggleDropdown, closeAll: closeAllDropdowns };

    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-elevare-dropdown-trigger]');
        if (trigger) {
            e.preventDefault();
            toggleDropdown(trigger.getAttribute('data-elevare-dropdown-trigger'));
            return;
        }
        // Clicking anywhere outside an open panel (and outside its trigger) closes it.
        if (!e.target.closest('[data-elevare-dropdown-panel]') && !e.target.closest('[data-elevare-dropdown-trigger]')) {
            closeAllDropdowns();
        }
    });

    // Keyboard activation for triggers that are NOT real buttons. A trigger written
    // as <div role="button" tabindex="0"> is focusable and announced as a button but
    // gets none of a real <button>'s key handling, so without this, tabbing to it and
    // pressing Enter/Space would silently do nothing.
    //
    // A real <button> or <a href> is skipped on purpose: the browser already turns
    // Enter/Space into a click on those, and the click handler above toggles. Running
    // both would toggle twice — open then immediately closed — which reads as the
    // dropdown being broken by keyboard entirely. The Mega Menu block emits a real
    // <button> now, which is exactly what made this guard necessary.
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ' && e.key !== 'Spacebar') return;
        var trigger = e.target.closest('[data-elevare-dropdown-trigger]');
        if (!trigger) return;
        var tag = (trigger.tagName || '').toUpperCase();
        if (tag === 'BUTTON' || (tag === 'A' && trigger.hasAttribute('href'))) return;
        e.preventDefault();
        toggleDropdown(trigger.getAttribute('data-elevare-dropdown-trigger'));
    });

    // ---------------------------------------------------------------
    // Side panels — the Yan Panel block. A trigger carries
    // data-elevare-sidebar-trigger="ID"; the panel data-elevare-sidebar-panel="ID";
    // an optional backdrop data-elevare-sidebar-backdrop="ID". The block's own
    // script hides panel and backdrop at load; this is what shows them.
    // ---------------------------------------------------------------
    function sidebarSelector(kind, id) {
        return '[data-elevare-sidebar-' + kind + '="' + id.replace(/"/g, '\\"') + '"]';
    }

    var sidebarReturnFocus = null;

    // Whether a panel is open is the data-elevare-open attribute, set the moment
    // it opens; the is-open class is only the slide animation, and it lands two
    // frames later. Keeping the two apart matters: a frame callback does not run
    // at all in a background tab, and a panel whose "open" state lived in that
    // class could then never be closed by Escape or its own button.
    function isSidebarOpen(panel) {
        return panel.hasAttribute('data-elevare-open');
    }

    function openSidebar(id) {
        var panel = document.querySelector(sidebarSelector('panel', id));
        if (!panel) return;
        closeAllSidebars(true);
        var backdrop = document.querySelector(sidebarSelector('backdrop', id));
        sidebarReturnFocus = document.activeElement;
        panel.setAttribute('data-elevare-open', '');
        panel.style.display = '';
        if (backdrop) backdrop.style.display = '';
        panel.setAttribute('role', 'dialog');
        panel.setAttribute('aria-modal', 'true');
        if (!panel.hasAttribute('tabindex')) panel.setAttribute('tabindex', '-1');
        // The page underneath must not scroll while a panel that covers it is open.
        document.body.style.overflow = 'hidden';
        // Same two-frame wait as playAnimationsIn: the panel was display:none a
        // moment ago, and the slide transition needs its off-screen state painted
        // once before the class that moves it on screen lands.
        requestAnimationFrame(function () {
            requestAnimationFrame(function () {
                if (!isSidebarOpen(panel)) return;
                panel.classList.add('is-open');
                if (backdrop) backdrop.classList.add('is-open');
            });
        });
        var focusables = focusableIn(panel);
        (focusables[0] || panel).focus({ preventScroll: true });
        playAnimationsIn(panel);
    }

    function closeSidebar(panel, instant) {
        var id = panel.getAttribute('data-elevare-sidebar-panel') || '';
        var backdrop = document.querySelector(sidebarSelector('backdrop', id));
        panel.removeAttribute('data-elevare-open');
        panel.classList.remove('is-open');
        if (backdrop) backdrop.classList.remove('is-open');
        var finish = function () {
            panel.style.display = 'none';
            if (backdrop) backdrop.style.display = 'none';
        };
        // Long enough for the .25s slide in the block's CSS to finish; instant
        // when another panel is about to take its place.
        if (instant) finish(); else setTimeout(finish, 260);
        document.body.style.overflow = '';
        if (sidebarReturnFocus && typeof sidebarReturnFocus.focus === 'function') {
            sidebarReturnFocus.focus({ preventScroll: true });
        }
        sidebarReturnFocus = null;
    }

    function closeAllSidebars(instant) {
        var open = document.querySelectorAll('[data-elevare-sidebar-panel][data-elevare-open]');
        for (var i = 0; i < open.length; i++) closeSidebar(open[i], instant);
    }

    window.elevareSidebar = { open: openSidebar, closeAll: closeAllSidebars };

    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-elevare-sidebar-trigger]');
        if (trigger) {
            e.preventDefault();
            var id = trigger.getAttribute('data-elevare-sidebar-trigger');
            var panel = document.querySelector(sidebarSelector('panel', id));
            if (panel && isSidebarOpen(panel)) closeSidebar(panel); else openSidebar(id);
            return;
        }
        var closeBtn = e.target.closest('[data-elevare-sidebar-close]');
        if (closeBtn) {
            var owner = closeBtn.closest('[data-elevare-sidebar-panel]');
            if (owner) closeSidebar(owner);
            return;
        }
        var backdrop = e.target.closest('[data-elevare-sidebar-backdrop]');
        if (backdrop) {
            var target = document.querySelector(sidebarSelector('panel', backdrop.getAttribute('data-elevare-sidebar-backdrop') || ''));
            if (target) closeSidebar(target);
        }
    });

    // ---------------------------------------------------------------
    // Multi-step form navigation
    // ---------------------------------------------------------------
    function goToStep(form, index) {
        var steps = form.querySelectorAll('[data-elevare-step]');
        for (var i = 0; i < steps.length; i++) {
            steps[i].style.display = i === index ? 'flex' : 'none';
        }
    }

    document.addEventListener('click', function (e) {
        var next = e.target.closest('[data-elevare-step-next]');
        var prev = e.target.closest('[data-elevare-step-prev]');
        var button = next || prev;
        if (!button) return;

        var form = button.closest('form');
        if (!form) return;

        var steps = form.querySelectorAll('[data-elevare-step]');
        var currentIndex = -1;
        for (var i = 0; i < steps.length; i++) {
            if (steps[i].style.display !== 'none') { currentIndex = i; break; }
        }
        if (currentIndex === -1) return;

        goToStep(form, currentIndex + (next ? 1 : -1));
    });

    // ---------------------------------------------------------------
    // Captcha token acquisition — no-op when Integrations.CaptchaProvider is
    // "none" (the default). Supports Google reCAPTCHA v3, hCaptcha, and
    // Cloudflare Turnstile.
    // ---------------------------------------------------------------
    var hcaptchaWidgetId = null;
    var turnstileWidgetId = null;

    function resetCaptchaWidgets() {
        if (hcaptchaWidgetId !== null && window.hcaptcha && typeof window.hcaptcha.reset === 'function') {
            try { window.hcaptcha.reset(hcaptchaWidgetId); } catch (_) { }
        }
        if (turnstileWidgetId !== null && window.turnstile && typeof window.turnstile.reset === 'function') {
            try { window.turnstile.reset(turnstileWidgetId); } catch (_) { }
        }
    }

    function getCaptchaToken() {
        var cfg = window.elevareCaptcha;
        if (!cfg || cfg.provider === 'none' || !cfg.siteKey) return Promise.resolve(null);

        if (cfg.provider === 'recaptcha' && window.grecaptcha) {
            return new Promise(function (resolve) {
                window.grecaptcha.ready(function () {
                    window.grecaptcha.execute(cfg.siteKey, { action: 'submit' })
                        .then(resolve)
                        .catch(function () { resolve(null); });
                });
            });
        }

        if (cfg.provider === 'hcaptcha' && window.hcaptcha) {
            if (hcaptchaWidgetId === null) {
                var container = document.createElement('div');
                container.style.display = 'none';
                document.body.appendChild(container);
                hcaptchaWidgetId = window.hcaptcha.render(container, { sitekey: cfg.siteKey, size: 'invisible' });
            }
            return window.hcaptcha.execute(hcaptchaWidgetId, { async: true })
                .then(function (r) { return r.response; })
                .catch(function () { return null; });
        }

        if (cfg.provider === 'turnstile' && window.turnstile) {
            return new Promise(function (resolve) {
                if (turnstileWidgetId === null) {
                    // Not display:none: 'interaction-only' keeps the widget invisible
                    // on its own whenever Cloudflare can pass the visitor silently, but
                    // it still needs a container it is actually allowed to draw into —
                    // a site key in "Managed" mode can decide to show an interactive
                    // challenge, and a display:none box would swallow that render,
                    // leaving the callback (and the form) hanging forever.
                    var container = document.createElement('div');
                    container.style.margin = '8px 0';
                    form.appendChild(container);
                    turnstileWidgetId = window.turnstile.render(container, {
                        sitekey: cfg.siteKey,
                        action: 'submit',
                        appearance: 'interaction-only',
                        callback: function (token) { resolve(token); },
                        'error-callback': function () { resolve(null); }
                    });
                } else {
                    window.turnstile.reset(turnstileWidgetId);
                    window.turnstile.execute(turnstileWidgetId, {
                        action: 'submit',
                        callback: function (token) { resolve(token); },
                        'error-callback': function () { resolve(null); }
                    });
                }
            });
        }

        return Promise.resolve(null);
    }

    function showFormFeedback(form, isSuccess, message) {
        var existing = form.querySelector('[data-elevare-form-feedback]');
        if (!existing) {
            existing = document.createElement('div');
            existing.setAttribute('data-elevare-form-feedback', '');
            // role="alert" (an assertive live region) so a screen reader announces
            // this the moment it appears — it is injected well after the page's own
            // load, so nothing else would ever tell an AT user the submit failed.
            existing.setAttribute('role', 'alert');
            existing.style.padding = '10px 14px';
            existing.style.borderRadius = '6px';
            existing.style.fontSize = '14px';
            existing.style.marginTop = '12px';
            existing.style.lineHeight = '1.4';
            form.appendChild(existing);
        }
        existing.style.display = 'block';
        if (isSuccess) {
            existing.style.background = '#e6f4ea';
            existing.style.color = '#137333';
            existing.style.border = '1px solid #ceead6';
            existing.textContent = message;
        } else {
            existing.style.background = '#fce8e6';
            existing.style.color = '#c5221f';
            existing.style.border = '1px solid #fad2cf';
            existing.textContent = message;
        }
    }

    // ---------------------------------------------------------------
    // Managed form submission
    // ---------------------------------------------------------------
    document.addEventListener('submit', function (e) {
        var form = e.target.closest('[data-elevare-managed-form]');
        if (!form) return;
        e.preventDefault();

        var lang = document.documentElement.lang || 'tr';
        var errorMessage = lang === 'en'
            ? 'Form submission failed. Please try again.'
            : 'Form gönderilemedi. Lütfen tekrar deneyiniz.';
        var successMessage = lang === 'en'
            ? 'Thank you — your submission was received.'
            : 'Teşekkürler — gönderiminiz alındı.';
        // The texts the author wrote on the form in the CMS, when they wrote any.
        errorMessage = (form.getAttribute('data-elevare-error-message') || '').trim() || errorMessage;
        successMessage = (form.getAttribute('data-elevare-success-message') || '').trim() || successMessage;

        // Reasons the server can give back for a refusal, in the page's language.
        // Everything else (a CAPTCHA that did not verify, a server error) gets the
        // generic message — there is nothing the visitor could change about it.
        var reasonMessages = lang === 'en'
            ? {
                too_large: 'A file is larger than 10 MB. Please choose a smaller one.',
                too_many: 'At most 3 files can be attached.',
                type: 'Only PDF, JPG, PNG, WebP and DOCX files are accepted.'
            }
            : {
                too_large: "Bir dosya 10 MB'tan büyük. Lütfen daha küçük bir dosya seçin.",
                too_many: 'En fazla 3 dosya eklenebilir.',
                type: 'Yalnızca PDF, JPG, PNG, WebP ve DOCX dosyaları kabul edilir.'
            };

        var data = new FormData(form);
        var hasFiles = false;
        var fields = {};
        data.forEach(function (value, key) {
            if (typeof value === 'string') { fields[key] = value; return; }
            // A file input left empty still yields a File with no name; that is
            // not an attachment.
            if (value && value.name) hasFiles = true; else data.delete(key);
        });

        var submitBtn = form.querySelector('[type="submit"]');
        if (submitBtn) submitBtn.disabled = true;

        getCaptchaToken().then(function (captchaToken) {
            var pageId = window.elevarePageId || 0;
            var formName = form.getAttribute('data-elevare-form-name') || null;
            if (hasFiles) {
                // With files the same fields go as multipart; the server reads the
                // three control values from it just like the JSON body.
                data.set('pageId', String(pageId));
                if (formName) data.set('formName', formName);
                if (captchaToken) data.set('captchaToken', captchaToken);
                return fetch('/api/forms/submit', { method: 'POST', body: data });
            }
            return fetch('/api/forms/submit', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ pageId: pageId, formName: formName, fields: fields, captchaToken: captchaToken })
            });
        })
            .then(function (res) {
                if (res.ok) return { ok: true };
                return res.json().then(function (body) { return { ok: false, reason: body && body.reason }; }, function () { return { ok: false }; });
            })
            .then(function (outcome) {
                if (submitBtn) submitBtn.disabled = false;
                if (!outcome.ok) {
                    resetCaptchaWidgets();
                    showFormFeedback(form, false, reasonMessages[outcome.reason] || errorMessage);
                    return;
                }

                var existing = form.querySelector('[data-elevare-form-feedback]');
                if (existing) existing.style.display = 'none';

                var onSubmit = form.getAttribute('data-elevare-onsubmit');
                if (onSubmit === 'redirect') {
                    var url = form.getAttribute('data-elevare-redirect-url');
                    if (url) window.location.href = url;
                } else if (onSubmit === 'popup') {
                    var popupId = form.getAttribute('data-elevare-popup-id');
                    if (popupId) openPopup(popupId);
                } else {
                    // Only when nothing else confirms the submission to the visitor —
                    // a redirect or popup already IS the confirmation, a second banner
                    // right before navigating away would just flash and be gone.
                    showFormFeedback(form, true, successMessage);
                }

                form.reset();
                if (form.querySelector('[data-elevare-step]')) goToStep(form, 0);
            })
            .catch(function () {
                if (submitBtn) submitBtn.disabled = false;
                resetCaptchaWidgets();
                showFormFeedback(form, false, errorMessage);
            });
    });

    // ---------------------------------------------------------------
    // Click tracking — any element marked data-track-click="Label" beacons the
    // label to the CMS's per-page stats (see PageClickHit). Fire-and-forget,
    // never blocks the click's own default behavior (e.g. a link navigating).
    // ---------------------------------------------------------------
    // Whether the visitor allowed analytics in the cookie banner. Read from the
    // banner's own cookie rather than its API, which may not have loaded yet when
    // this runs. The banner is vanilla-cookieconsent, set up under Site Codes with
    // an "analytics" category; with no banner there is no consent to read, and the
    // server then keeps no visitor cookie. elevare-analytics.js has the same check.
    function hasAnalyticsConsent() {
        var m = document.cookie.match(/(?:^|;\s*)cc_cookie=([^;]*)/);
        if (!m) return false;
        try { return (JSON.parse(decodeURIComponent(m[1])).categories || []).indexOf('analytics') >= 0; }
        catch (_) { return false; }
    }

    document.addEventListener('click', function (e) {
        var el = e.target.closest('[data-track-click]');
        if (!el) return;

        var payload = JSON.stringify({
            path: location.pathname,
            elementLabel: el.getAttribute('data-track-click'),
            consent: hasAnalyticsConsent()
        });

        if (navigator.sendBeacon) {
            navigator.sendBeacon('/api/track/click', new Blob([payload], { type: 'application/json' }));
        } else {
            fetch('/api/track/click', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: payload,
                keepalive: true
            }).catch(function () { });
        }
    });

    // ---------------------------------------------------------------
    // Client-side error reporting — uncaught exceptions and unhandled promise
    // rejections are reported to the CMS's log viewer (see AppLog). Capped per
    // page load so a broken loop can't flood the log table.
    // ---------------------------------------------------------------
    var errorReportsSent = 0;
    var MAX_ERROR_REPORTS = 5;

    function reportClientError(message, stack) {
        if (errorReportsSent >= MAX_ERROR_REPORTS) return;
        errorReportsSent++;

        fetch('/api/log/client', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                message: String(message).slice(0, 1000),
                stack: stack ? String(stack).slice(0, 4000) : null,
                path: location.pathname
            }),
            keepalive: true
        }).catch(function () { });
    }

    window.addEventListener('error', function (e) {
        reportClientError(e.message, e.error && e.error.stack);
    });

    window.addEventListener('unhandledrejection', function (e) {
        var reason = e.reason;
        var message = reason && reason.message ? reason.message : String(reason);
        var stack = reason && reason.stack ? reason.stack : null;
        reportClientError('Unhandled promise rejection: ' + message, stack);
    });

    // ---------------------------------------------------------------
    // Scroll animations
    // ---------------------------------------------------------------
    // Reveals anything the page builder marked with data-elevare-anim as it scrolls
    // into view. The CSS lives with the page's own styles (see elevare-blocks.js);
    // all this does is decide when.
    //
    // The .elevare-anim-ready class on <html> is what ARMS that CSS, and it is set
    // here rather than in the markup on purpose: until this line runs nothing is
    // hidden, so a blocked, failed or still-loading script leaves a perfectly
    // readable page instead of a blank one. Same reason the observer falls back to
    // simply revealing everything when IntersectionObserver is missing.
    (function initScrollAnimations() {
        var animated = document.querySelectorAll('[data-elevare-anim]');
        if (!animated.length) return;

        var reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (reduced || !('IntersectionObserver' in window)) return;

        document.documentElement.classList.add('elevare-anim-ready');

        animObserver = new IntersectionObserver(function (entries) {
            for (var i = 0; i < entries.length; i++) {
                if (!entries[i].isIntersecting) continue;
                // One-shot: re-animating on every scroll past is a distraction, not
                // an effect, and it keeps the observer from growing without bound.
                // (revealAnimated unobserves.)
                revealAnimated(entries[i].target);
            }
        // No negative rootMargin, and a threshold barely above zero. A "-10% bottom"
        // margin reads like a nicety — hold the reveal until the element is properly
        // on screen — but it carves a dead band across the bottom tenth of the
        // viewport, and anything that comes to rest inside it never intersects at
        // all. The last element on a page that cannot scroll any further does
        // exactly that, and stays invisible for good. Measured it happening; one
        // visible pixel is the honest trigger.
        }, { rootMargin: '0px', threshold: 0.01 });

        for (var i = 0; i < animated.length; i++) {
            // An element inside something hidden at load — a dropdown panel, a
            // pop-up, a later form step — can never intersect, so observing it would
            // hold it at opacity 0 forever and the container would open EMPTY.
            // Reveal it now instead (visible, just not animated yet); the animation
            // is played by playAnimationsIn when its container is actually opened.
            //
            // Not hypothetical: a language switcher placed inside a Mega Menu panel
            // did exactly this. It escaped being invisible only because the script
            // that hides the panel happened to run after the observer — a race, not
            // a design, and it also meant the animation had already been spent by
            // the time anyone opened the menu.
            if (animated[i].offsetParent === null && animated[i].getClientRects().length === 0) {
                revealAnimated(animated[i]);
                continue;
            }
            animObserver.observe(animated[i]);
        }

        // Anything at or above the fold gets revealed on the next frame rather than
        // waiting for a scroll that may never come on a short page.
        //
        // The test is "top < viewport height", NOT "and bottom > 0" — deliberately,
        // because it has to catch elements that are already ABOVE the viewport too.
        // Landing on /sayfa#bolum scrolls the browser before this script runs, and
        // anything scrolled past by then never intersects again: the observer would
        // never fire for it and it would stay at opacity 0 forever. Measured that
        // exact failure while testing, which is why the condition is this shape.
        requestAnimationFrame(function () {
            for (var j = 0; j < animated.length; j++) {
                if (animated[j].getBoundingClientRect().top < window.innerHeight) revealAnimated(animated[j]);
            }
        });
    })();

    // ---------------------------------------------------------------
    // Runtime styles for the pieces below — only what they need to work (a
    // hidden state, a dialog); how they look is the block's own, from the builder.
    // ---------------------------------------------------------------
    function ensureStyle(id, css) {
        if (document.getElementById(id)) return;
        var style = document.createElement('style');
        style.id = id;
        style.textContent = css;
        document.head.appendChild(style);
    }

    function prefersReducedMotion() {
        return !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    }

    // ---------------------------------------------------------------
    // Code blocks — "Kopyala" copies the code as written (the server coloured it;
    // line numbers are CSS, so they are not in the text).
    // ---------------------------------------------------------------
    document.addEventListener('click', function (e) {
        var btn = e.target && e.target.closest ? e.target.closest('[data-elevare-code-copy]') : null;
        if (!btn) return;
        var block = btn.closest('[data-elevare-code]');
        var code = block && (block.querySelector('pre code') || block.querySelector('code'));
        if (!code || !navigator.clipboard) return;
        navigator.clipboard.writeText(code.textContent).then(function () {
            if (!btn.hasAttribute('data-label')) btn.setAttribute('data-label', btn.textContent);
            btn.textContent = btn.getAttribute('data-copied-text') || 'OK';
            clearTimeout(btn._elevareCopyTimer);
            btn._elevareCopyTimer = setTimeout(function () { btn.textContent = btn.getAttribute('data-label'); }, 2000);
        });
    });

    // ---------------------------------------------------------------
    // Image zoom (lightbox) — an image marked data-elevare-lightbox, or any image
    // inside a data-elevare-lightbox-group (a gallery), opens full size over the
    // page. Arrow keys move through the group, Esc closes, and focus comes back to
    // where it was.
    // ---------------------------------------------------------------
    (function initLightbox() {
        var SELECTOR = 'img[data-elevare-lightbox], [data-elevare-lightbox] img, [data-elevare-lightbox-group] img';
        if (!document.querySelector(SELECTOR)) return;
        ensureStyle('elevare-lightbox-css',
            SELECTOR.split(', ').join(',') + '{cursor:zoom-in}' +
            '.elevare-lightbox{position:fixed;inset:0;z-index:10000;background:rgba(0,0,0,.88);display:flex;align-items:center;justify-content:center;padding:48px 56px}' +
            '.elevare-lightbox figure{margin:0;max-width:100%;max-height:100%;display:flex;flex-direction:column;align-items:center;gap:10px}' +
            '.elevare-lightbox img{max-width:100%;max-height:calc(100vh - 140px);object-fit:contain;border-radius:6px;cursor:default}' +
            '.elevare-lightbox figcaption{color:#e5e7eb;font-size:14px;text-align:center;max-width:70ch}' +
            '.elevare-lightbox button{position:absolute;background:rgba(255,255,255,.12);color:#fff;border:0;border-radius:50%;width:44px;height:44px;font-size:22px;line-height:1;cursor:pointer}' +
            '.elevare-lightbox button:hover,.elevare-lightbox button:focus-visible{background:rgba(255,255,255,.28)}' +
            '.elevare-lightbox .el-lb-close{top:14px;right:14px}.elevare-lightbox .el-lb-prev{left:14px;top:50%;transform:translateY(-50%)}.elevare-lightbox .el-lb-next{right:14px;top:50%;transform:translateY(-50%)}');

        var tr = (document.documentElement.lang || '').toLowerCase().indexOf('tr') === 0;
        var overlay = null, group = [], index = 0, returnFocus = null;

        function caption(img) {
            var fig = img.closest('figure');
            var cap = fig && fig.querySelector('figcaption');
            return (cap && cap.textContent.trim()) || '';
        }

        function show() {
            var img = group[index];
            var big = overlay.querySelector('img');
            big.src = img.getAttribute('data-full') || img.getAttribute('src') || img.currentSrc;
            big.alt = img.alt || '';
            var cap = overlay.querySelector('figcaption');
            cap.textContent = caption(img);
            cap.hidden = !cap.textContent;
            overlay.querySelector('.el-lb-prev').hidden = group.length < 2;
            overlay.querySelector('.el-lb-next').hidden = group.length < 2;
        }

        function close() {
            if (!overlay) return;
            overlay.remove();
            overlay = null;
            document.removeEventListener('keydown', onKey, true);
            document.documentElement.style.overflow = '';
            if (returnFocus && returnFocus.focus) returnFocus.focus();
        }

        function move(step) {
            index = (index + step + group.length) % group.length;
            show();
        }

        function onKey(e) {
            if (e.key === 'Escape') { e.preventDefault(); close(); }
            else if (e.key === 'ArrowRight' && group.length > 1) { e.preventDefault(); move(1); }
            else if (e.key === 'ArrowLeft' && group.length > 1) { e.preventDefault(); move(-1); }
        }

        function open(img) {
            var host = img.closest('[data-elevare-lightbox-group]');
            group = host ? Array.prototype.slice.call(host.querySelectorAll('img')) : [img];
            index = Math.max(0, group.indexOf(img));
            returnFocus = document.activeElement;
            overlay = document.createElement('div');
            overlay.className = 'elevare-lightbox';
            overlay.setAttribute('role', 'dialog');
            overlay.setAttribute('aria-modal', 'true');
            overlay.setAttribute('aria-label', tr ? 'Görsel' : 'Image');
            overlay.innerHTML =
                '<figure><img alt=""><figcaption></figcaption></figure>' +
                '<button type="button" class="el-lb-close" aria-label="' + (tr ? 'Kapat' : 'Close') + '">×</button>' +
                '<button type="button" class="el-lb-prev" aria-label="' + (tr ? 'Önceki görsel' : 'Previous image') + '">‹</button>' +
                '<button type="button" class="el-lb-next" aria-label="' + (tr ? 'Sonraki görsel' : 'Next image') + '">›</button>';
            overlay.addEventListener('click', function (e) {
                if (e.target === overlay || e.target.classList.contains('el-lb-close')) close();
                else if (e.target.classList.contains('el-lb-prev')) move(-1);
                else if (e.target.classList.contains('el-lb-next')) move(1);
            });
            document.body.appendChild(overlay);
            document.documentElement.style.overflow = 'hidden';
            document.addEventListener('keydown', onKey, true);
            show();
            overlay.querySelector('.el-lb-close').focus();
        }

        document.addEventListener('click', function (e) {
            var img = e.target && e.target.closest ? e.target.closest('img') : null;
            if (!img || overlay || !img.matches(SELECTOR)) return;
            // An image that is also a link goes where the link says.
            if (img.closest('a[href]')) return;
            e.preventDefault();
            open(img);
        });
    })();

    // ---------------------------------------------------------------
    // Tabs — the "Sekmeler" block. Without this script every panel simply shows,
    // one under the other; with it, one at a time, with the ARIA tabs pattern
    // (arrow keys, Home/End) so a keyboard or screen-reader user gets real tabs.
    // ---------------------------------------------------------------
    (function initTabs() {
        var sets = document.querySelectorAll('[data-elevare-tabs]');
        Array.prototype.forEach.call(sets, function (set, n) {
            var tabs = Array.prototype.slice.call(set.querySelectorAll('[data-elevare-tab]'));
            if (!tabs.length) return;
            function panelFor(tab) { return set.querySelector('[data-elevare-tab-panel="' + tab.getAttribute('data-elevare-tab') + '"]'); }
            function select(tab, focus) {
                tabs.forEach(function (t) {
                    var on = t === tab;
                    t.setAttribute('aria-selected', on ? 'true' : 'false');
                    t.setAttribute('tabindex', on ? '0' : '-1');
                    t.classList.toggle('is-active', on);
                    var panel = panelFor(t);
                    if (panel) panel.hidden = !on;
                });
                if (focus) tab.focus();
            }
            var list = tabs[0].parentElement;
            if (list) list.setAttribute('role', 'tablist');
            tabs.forEach(function (tab, i) {
                var id = 'el-tab-' + n + '-' + i;
                var panel = panelFor(tab);
                tab.setAttribute('role', 'tab');
                tab.id = tab.id || id;
                if (panel) {
                    panel.setAttribute('role', 'tabpanel');
                    panel.setAttribute('aria-labelledby', tab.id);
                    panel.id = panel.id || id + '-panel';
                    tab.setAttribute('aria-controls', panel.id);
                }
                tab.addEventListener('click', function () { select(tab, false); });
                tab.addEventListener('keydown', function (e) {
                    var k = e.key, next = null;
                    if (k === 'ArrowRight' || k === 'ArrowDown') next = tabs[(i + 1) % tabs.length];
                    else if (k === 'ArrowLeft' || k === 'ArrowUp') next = tabs[(i - 1 + tabs.length) % tabs.length];
                    else if (k === 'Home') next = tabs[0];
                    else if (k === 'End') next = tabs[tabs.length - 1];
                    if (next) { e.preventDefault(); select(next, true); }
                });
            });
            select(tabs.filter(function (t) { return t.getAttribute('aria-selected') === 'true'; })[0] || tabs[0], false);
        });
    })();

    // ---------------------------------------------------------------
    // Reading progress and "back to top" — both follow the scroll, so they share
    // one listener, throttled to a frame.
    // ---------------------------------------------------------------
    (function initScrollHelpers() {
        var bars = document.querySelectorAll('[data-elevare-reading-progress]');
        var tops = document.querySelectorAll('[data-elevare-back-to-top]');
        if (!bars.length && !tops.length) return;
        ensureStyle('elevare-back-to-top-css',
            '[data-elevare-back-to-top]{opacity:0;visibility:hidden;transition:opacity .2s,visibility .2s}' +
            '[data-elevare-back-to-top][data-visible]{opacity:1;visibility:visible}');

        // Progress through the article when there is one, else the page.
        var article = document.querySelector('[data-elevare-article]');
        var pending = false;

        function update() {
            pending = false;
            var y = window.scrollY || window.pageYOffset;
            var total, done;
            if (article) {
                var rect = article.getBoundingClientRect();
                total = Math.max(1, rect.height - window.innerHeight);
                done = -rect.top;
            } else {
                total = Math.max(1, document.documentElement.scrollHeight - window.innerHeight);
                done = y;
            }
            var pct = Math.max(0, Math.min(100, (done / total) * 100));
            Array.prototype.forEach.call(bars, function (bar) {
                var fill = bar.querySelector('[data-elevare-reading-progress-bar]') || bar;
                fill.style.width = pct + '%';
            });
            var visible = y > window.innerHeight * 0.8;
            Array.prototype.forEach.call(tops, function (btn) { btn.toggleAttribute('data-visible', visible); });
        }

        window.addEventListener('scroll', function () {
            if (pending) return;
            pending = true;
            requestAnimationFrame(update);
        }, { passive: true });
        window.addEventListener('resize', update);
        update();

        Array.prototype.forEach.call(tops, function (btn) {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                window.scrollTo({ top: 0, behavior: prefersReducedMotion() ? 'auto' : 'smooth' });
                // Back where the page starts, for a keyboard user too.
                var target = document.getElementById('main-content') || document.body;
                if (target.focus) target.focus({ preventScroll: true });
            });
        });
    })();
})();
