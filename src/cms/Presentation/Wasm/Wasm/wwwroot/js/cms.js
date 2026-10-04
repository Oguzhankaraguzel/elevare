// Elevare CMS - JavaScript Helpers

window.cmsJs = {
    // ── Theme ─────────────────────────────────────────────────────────────
    setTheme: function (theme) {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('cms_theme', theme);
    },

    // ── Topbar pin ────────────────────────────────────────────────────────
    setTopbarPinned: function (pinned) {
        localStorage.setItem('cms_topbar_pinned', pinned ? '1' : '0');
    },

    // ── Rightbar pin ──────────────────────────────────────────────────────
    setRightbarPinned: function (pinned) {
        localStorage.setItem('cms_rightbar_pinned', pinned ? '1' : '0');
    },
    setRightbarExpanded: function (expanded) {
        localStorage.setItem('cms_rightbar_expanded', expanded ? '1' : '0');
    },

    // ── Sidebar ───────────────────────────────────────────────────────────
    setSidebarCollapsed: function (collapsed) {
        localStorage.setItem('cms_sidebar_collapsed', collapsed ? '1' : '0');
    },
    setSidebarPinned: function (pinned) {
        localStorage.setItem('cms_sidebar_pinned', pinned ? '1' : '0');
    },

    // ── Sidebar nav-group collapse state (accordion) ─────────────────────
    setNavSectionCollapsed: function (key, collapsed) {
        var state = cmsJs.getNavSectionsState();
        state[key] = collapsed;
        localStorage.setItem('cms_nav_sections', JSON.stringify(state));
    },
    getNavSectionsState: function () {
        try { return JSON.parse(localStorage.getItem('cms_nav_sections') || '{}'); }
        catch (e) { return {}; }
    },

    // ── Batch preference read (single JS round-trip for all layout prefs) ─
    getLayoutPrefs: function () {
        return {
            theme:            localStorage.getItem('cms_theme') || 'dark',
            topbarPinned:     localStorage.getItem('cms_topbar_pinned') !== '0',
            sidebarCollapsed: localStorage.getItem('cms_sidebar_collapsed') === '1',
            sidebarPinned:    localStorage.getItem('cms_sidebar_pinned') !== '0',
            rightbarPinned:   localStorage.getItem('cms_rightbar_pinned') !== '0',
            rightbarExpanded: localStorage.getItem('cms_rightbar_expanded') === '1',
            utcOffsetMinutes: -new Date().getTimezoneOffset()
        };
    },

    // ── Clipboard copy, in the click that asked for it ────────────────────
    // Blazor Server routes @onclick to the server over SignalR and calls back
    // into JS afterwards. By then the browser no longer counts the call as a user
    // gesture, so navigator.clipboard rejects and execCommand does nothing — which
    // is why a Blazor-driven copy button silently fails even in a normal browser.
    //
    // This listener runs in the real click instead. Mark up a button with
    //   data-copy-target="#someTextarea"
    //   data-copied-label / data-copyfail-label
    // and the copy happens before the server ever hears about the click.
    installCopyDelegation: function () {
        if (this._copyDelegationInstalled) return;
        this._copyDelegationInstalled = true;

        document.addEventListener('click', function (e) {
            const btn = e.target.closest('[data-copy-target],[data-copy-text]');
            if (!btn) return;

            // Two ways to say what to copy: point at a field the user can see, or
            // carry the value on the button when there is no such field (a media
            // row's URL, a template's export payload).
            const selector = btn.getAttribute('data-copy-target');
            const source = selector
                ? document.querySelector(selector)
                : null;

            if (selector && !source) return;

            const text = source
                ? ('value' in source ? source.value : source.textContent)
                : btn.getAttribute('data-copy-text');

            if (text === null || text === undefined) return;

            let ok = false;

            // Synchronous path first: still inside the gesture, so it is allowed
            // even where the async clipboard API is not.
            try {
                if (source) {
                    source.focus();
                    source.select();
                    ok = document.execCommand('copy');
                } else {
                    // No visible field to select, so borrow one for the duration of
                    // the copy. Off-screen rather than hidden: display:none and
                    // visibility:hidden elements cannot be selected.
                    const scratch = document.createElement('textarea');
                    scratch.value = text;
                    scratch.setAttribute('readonly', '');
                    scratch.style.cssText = 'position:fixed;top:-1000px;left:-1000px;opacity:0';
                    document.body.appendChild(scratch);
                    scratch.select();
                    ok = document.execCommand('copy');
                    document.body.removeChild(scratch);
                }
            } catch (err) { ok = false; }

            // Only buttons that opted in with a label span get their text swapped.
            // An icon-only button has no room for a sentence, so it reports through
            // the state attribute alone and lets CSS colour it.
            const label = btn.querySelector('[data-copy-label]');
            const done = btn.getAttribute('data-copied-label');
            const failed = btn.getAttribute('data-copyfail-label');

            const report = function (success) {
                if (label) label.textContent = success ? done : failed;
                btn.setAttribute('data-copy-state', success ? 'ok' : 'fail');
                setTimeout(function () {
                    if (label) label.textContent = btn.getAttribute('data-copy-idle-label') || '';
                    btn.removeAttribute('data-copy-state');
                }, 2000);
            };

            if (ok) { report(true); return; }

            // Fall back to the async API — it can still succeed on a secure origin
            // when execCommand is disabled by the browser.
            if (navigator.clipboard && window.isSecureContext) {
                navigator.clipboard.writeText(text).then(
                    function () { report(true); },
                    function () { report(false); });
            } else {
                report(false);
            }
        });
    },


    downloadFile: function (fileName, contentType, content) {
        const url = URL.createObjectURL(new Blob([content], { type: contentType }));
        const a = document.createElement('a');
        a.href = url; a.download = fileName;
        document.body.appendChild(a); a.click();
        document.body.removeChild(a); URL.revokeObjectURL(url);
    },

    // ── Pending-tab pattern for "click → await something → open result" flows ─
    // (e.g. Önizle in PageEditor.razor) — window.open() is only allowed inside the
    // synchronous call stack of a real user gesture. Awaiting a save + a query
    // first, THEN calling window.open with the final URL, gets silently blocked by
    // the browser's popup blocker. Opening a blank tab immediately (before any
    // await) and redirecting it later avoids that.
    _pendingWindows: {},
    openPendingWindow: function () {
        const w = window.open('', '_blank');
        const key = 'pw_' + Date.now() + '_' + Math.random().toString(36).slice(2);
        cmsJs._pendingWindows[key] = w;
        return key;
    },
    navigatePendingWindow: function (key, url) {
        const w = cmsJs._pendingWindows[key];
        delete cmsJs._pendingWindows[key];
        if (w && !w.closed) {
            w.location.href = url;
        } else {
            window.open(url, '_blank');
        }
    },

    // ── Window Manager ────────────────────────────────────────────────────
    _windowZIndex: 1100,
    _windowInstances: {},

    initWindow: function (el, windowId, dotNetRef) {
        if (!el) return;
        var inst = { el: el, dotNetRef: dotNetRef, cleanups: [] };
        cmsJs._windowInstances[windowId] = inst;

        // ── Center on screen, and stay on it ──
        // A window is centred when it opens, which is usually before its content
        // has loaded — a list arriving a moment later grew it downwards from a top
        // edge set for an empty window, and its bottom (with the buttons) ended up
        // below the screen. So it is placed again whenever its size or the
        // browser's changes: centred until the user has moved it, and after that
        // only pushed back inside. The CSS caps its height at the screen's; the
        // rest scrolls inside the window.
        var moved = false;
        var place = function () {
            if (el.classList.contains('cms-window-maximized')) return;
            var r = el.getBoundingClientRect();
            var maxLeft = Math.max(20, window.innerWidth - r.width - 20);
            var maxTop = Math.max(20, window.innerHeight - r.height - 20);
            var left = moved ? Math.min(Math.max(20, r.left), maxLeft) : Math.max(20, (window.innerWidth - r.width) / 2);
            var top = moved ? Math.min(Math.max(20, r.top), maxTop) : Math.max(20, (window.innerHeight - r.height) / 2);
            el.style.left = left + 'px';
            el.style.top = top + 'px';
        };
        place();
        if (window.ResizeObserver) {
            var sizeWatch = new ResizeObserver(function () { if (!resizing && !dragging) place(); });
            sizeWatch.observe(el);
            inst.cleanups.push(function () { sizeWatch.disconnect(); });
        }
        window.addEventListener('resize', place);
        inst.cleanups.push(function () { window.removeEventListener('resize', place); });

        // ── State ──
        var dragging = false, dragOx = 0, dragOy = 0;
        var resizing = false, resDir = '', resSx = 0, resSy = 0, resSw = 0, resSh = 0, resSl = 0, resSt = 0;

        // ── Initial z-index ──
        cmsJs._windowZIndex++;
        el.style.zIndex = cmsJs._windowZIndex;

        // ── Window mousedown: bring-to-front + resize via event delegation ──
        var onElMouseDown = function (e) {
            // Bring to front & stop bubble so parent windows don't also jump forward
            cmsJs._windowZIndex++;
            el.style.zIndex = cmsJs._windowZIndex;
            e.stopPropagation();

            // Resize detection (event delegation — works even after Blazor re-renders handles)
            var handle = e.target.closest('.cms-window-resize');
            if (handle && !el.classList.contains('cms-window-maximized') && !el.classList.contains('cms-window-minimized')) {
                e.preventDefault();
                resizing = true;
                moved = true;
                resDir = handle.getAttribute('data-dir') || '';
                resSx = e.clientX; resSy = e.clientY;
                resSw = el.offsetWidth; resSh = el.offsetHeight;
                resSl = el.offsetLeft; resSt = el.offsetTop;
                el.classList.add('cms-window-resizing');
            }
        };
        el.addEventListener('mousedown', onElMouseDown);
        inst.cleanups.push(function () { el.removeEventListener('mousedown', onElMouseDown); });

        // ── Drag via title bar ──
        var titlebar = el.querySelector('.cms-window-titlebar');
        var onTitleDown = function (e) {
            if (e.target.closest('.cms-window-controls')) return;
            if (el.classList.contains('cms-window-maximized')) return;
            dragging = true;
            moved = true;
            dragOx = e.clientX - el.offsetLeft;
            dragOy = e.clientY - el.offsetTop;
            el.classList.add('cms-window-dragging');
            e.preventDefault();
        };
        // Double-click the title bar to maximise. Named and registered for cleanup
        // like its three siblings — as an anonymous listener it was the one handler
        // destroyWindow never removed, and it closes over dotNetRef, so every window
        // that had been opened and closed kept a .NET object reference alive for as
        // long as the element was reachable.
        var onTitleDblClick = function (e) {
            if (e.target.closest('.cms-window-controls')) return;
            dotNetRef.invokeMethodAsync('ToggleMaximizeFromJs');
        };
        if (titlebar) {
            titlebar.addEventListener('mousedown', onTitleDown);
            titlebar.addEventListener('dblclick', onTitleDblClick);
            inst.cleanups.push(function () {
                titlebar.removeEventListener('mousedown', onTitleDown);
                titlebar.removeEventListener('dblclick', onTitleDblClick);
            });
        }

        // ── Shared document move / up ──
        var onDocMove = function (e) {
            if (dragging) {
                var x = Math.max(0, Math.min(e.clientX - dragOx, window.innerWidth - 80));
                var y = Math.max(0, Math.min(e.clientY - dragOy, window.innerHeight - 40));
                el.style.left = x + 'px';
                el.style.top  = y + 'px';
            }
            if (resizing) {
                var dx = e.clientX - resSx, dy = e.clientY - resSy;
                if (resDir.indexOf('e') !== -1) el.style.width  = Math.max(320, resSw + dx) + 'px';
                if (resDir.indexOf('s') !== -1) el.style.height = Math.max(200, resSh + dy) + 'px';
                if (resDir.indexOf('w') !== -1) { var nw = Math.max(320, resSw - dx); el.style.width = nw + 'px'; el.style.left = (resSl + resSw - nw) + 'px'; }
                if (resDir.indexOf('n') !== -1) { var nh = Math.max(200, resSh - dy); el.style.height = nh + 'px'; el.style.top  = (resSt + resSh - nh) + 'px'; }
            }
        };
        var onDocUp = function () {
            if (dragging) { dragging = false; el.classList.remove('cms-window-dragging'); }
            if (resizing) { resizing = false; el.classList.remove('cms-window-resizing'); }
        };
        document.addEventListener('mousemove', onDocMove);
        document.addEventListener('mouseup', onDocUp);
        inst.cleanups.push(function () {
            document.removeEventListener('mousemove', onDocMove);
            document.removeEventListener('mouseup', onDocUp);
        });
    },

    destroyWindow: function (windowId) {
        var inst = cmsJs._windowInstances[windowId];
        if (!inst) return;
        inst.cleanups.forEach(function (fn) { fn(); });
        delete cmsJs._windowInstances[windowId];
    },

    _reminderAudio: null,

    playReminderSound: function () {
        try {
            if (cmsJs._reminderAudio) return; // already playing
            var audio = new Audio('/assets/sounds/reminder.wav');
            audio.volume = 0.6;
            audio.loop = true;
            audio.play().catch(function () { /* autoplay blocked - ignore */ });
            cmsJs._reminderAudio = audio;
        } catch (e) { /* ignore */ }
    },

    stopReminderSound: function () {
        try {
            if (cmsJs._reminderAudio) {
                cmsJs._reminderAudio.pause();
                cmsJs._reminderAudio.currentTime = 0;
                cmsJs._reminderAudio = null;
            }
        } catch (e) { /* ignore */ }
    },

    getBrowserUtcOffsetMinutes: function () {
        return -new Date().getTimezoneOffset(); // JS returns negative of UTC offset
    },

    // Leaving-the-page confirmation for IN-APP navigation. The browser-level exit
    // (tab close / reload / Back) is covered by NavigationLock's own
    // ConfirmExternalNavigation, which cannot carry a custom message; this one can,
    // so the editor is told exactly what is about to be lost.
    confirmMessage: function (message) {
        return window.confirm(message);
    },

    // An image's real size in pixels, read by loading it — for an address the
    // media library knows nothing about. Null when it does not load in time.
    measureImage: function (url) {
        return new Promise(function (resolve) {
            if (!url) { resolve(null); return; }
            const img = new Image();
            const timer = setTimeout(function () { resolve(null); }, 8000);
            img.onload = function () {
                clearTimeout(timer);
                resolve(img.naturalWidth > 0 ? { width: img.naturalWidth, height: img.naturalHeight } : null);
            };
            img.onerror = function () { clearTimeout(timer); resolve(null); };
            img.src = url;
        });
    }
};

// Installed at load: the listener has to already be attached when the user clicks,
// because the click itself is the only moment the clipboard will accept.
window.cmsJs.installCopyDelegation();
