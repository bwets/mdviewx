const ASM = 'MarkdownBlaze';

function isDark() {
    const el = document.documentElement;
    if (el.classList.contains('theme-dark')) return true;
    if (el.classList.contains('theme-light')) return false;
    return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}

// Run syntax highlighting + mermaid over the freshly rendered markdown.
window.mdInit = function () {
    try { if (window.hljs) window.hljs.highlightAll(); } catch (e) { console.error('hljs', e); }
    try {
        if (window.mermaid) {
            window.mermaid.initialize({ startOnLoad: false, theme: isDark() ? 'dark' : 'default' });
            window.mermaid.run();
        }
    } catch (e) { console.error('mermaid', e); }
};

window.mdSetTheme = function (mode) {
    const el = document.documentElement;
    el.classList.remove('theme-dark', 'theme-light', 'theme-system');
    el.classList.add('theme-' + (mode || 'system').toLowerCase());
};

window.mdScrollTo = function (id) {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
};

window.mdPrint = function () { try { window.print(); } catch (e) { } };

window.mdCopy = function (text) {
    try { navigator.clipboard.writeText(text); } catch (e) { }
};

// Intercept link clicks inside the rendered markdown.
document.addEventListener('click', function (e) {
    const a = e.target.closest ? e.target.closest('a') : null;
    if (!a) return;
    const nav = a.getAttribute('data-nav');
    if (nav) { e.preventDefault(); DotNet.invokeMethodAsync(ASM, 'OnLink', 'nav', nav); return; }
    const href = a.getAttribute('href') || '';
    if (href === '' || href.charAt(0) === '#') return; // in-page anchor: let the browser scroll

    // Protocol-relative (//host/…) → treat as https and open externally.
    if (href.slice(0, 2) === '//') {
        e.preventDefault();
        DotNet.invokeMethodAsync(ASM, 'OnLink', 'ext', 'https:' + href);
        return;
    }
    // Anything with a URL scheme (http:, https:, mailto:, tel:, ftp:, …) opens in the OS default
    // handler — browser, mail client, etc. — instead of navigating the WebView.
    if (/^[a-z][a-z0-9+.\-]*:/i.test(href)) {
        e.preventDefault();
        DotNet.invokeMethodAsync(ASM, 'OnLink', 'ext', href);
        return;
    }
    // Any remaining link is an unresolved relative/root path that isn't in-app navigation; don't let
    // the WebView navigate away from the app shell.
    e.preventDefault();
});

// Keyboard shortcuts -> .NET.
window.addEventListener('keydown', function (e) {
    let combo = '';
    if (e.altKey && e.key === 'ArrowLeft') combo = 'alt+left';
    else if (e.altKey && e.key === 'ArrowRight') combo = 'alt+right';
    else if (e.key === 'F5') combo = 'f5';
    else if (e.ctrlKey && (e.key === 'r' || e.key === 'R')) combo = 'ctrl+r';
    else if (e.ctrlKey && (e.key === 'p' || e.key === 'P')) combo = 'ctrl+p';
    else if (e.ctrlKey && (e.key === 'b' || e.key === 'B')) combo = 'ctrl+b';
    if (combo) { e.preventDefault(); DotNet.invokeMethodAsync(ASM, 'OnKey', combo); }
});

// Mouse side buttons (button 3 = Back, button 4 = Forward) -> history navigation.
// Act on mouseup, but suppress the WebView's own default on mousedown/auxclick so it doesn't
// try to navigate on its own.
window.addEventListener('mousedown', function (e) {
    if (e.button === 3 || e.button === 4) e.preventDefault();
});
window.addEventListener('auxclick', function (e) {
    if (e.button === 3 || e.button === 4) e.preventDefault();
});
window.addEventListener('mouseup', function (e) {
    if (e.button === 3) { e.preventDefault(); DotNet.invokeMethodAsync(ASM, 'OnKey', 'alt+left'); }
    else if (e.button === 4) { e.preventDefault(); DotNet.invokeMethodAsync(ASM, 'OnKey', 'alt+right'); }
});

// ---- File drag & drop -----------------------------------------------------------------------
// WebView2 does not expose the real path of a dropped file, so we recover a path only where the
// engine offers one (text/uri-list / file:// — WebKitGTK on Linux does; a File.path from
// Electron-style hosts) and otherwise fall back to reading the file's text and rendering that.
function mdDropPath(dt, file) {
    if (file && file.path) return file.path;
    let uri = '';
    try { uri = dt.getData('text/uri-list') || dt.getData('text/plain') || ''; } catch (_) { }
    uri = (uri.split('\n').find(l => l && l.charAt(0) !== '#') || '').trim();
    if (uri.slice(0, 7) === 'file://') {
        try { return decodeURIComponent(new URL(uri).pathname).replace(/^\/([A-Za-z]:)/, '$1'); }
        catch (_) { }
    }
    return '';
}

function mdHasFiles(e) {
    return !!(e.dataTransfer && Array.from(e.dataTransfer.types || []).indexOf('Files') !== -1);
}

['dragenter', 'dragover'].forEach(function (ev) {
    window.addEventListener(ev, function (e) {
        if (!mdHasFiles(e)) return;
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
        document.body.classList.add('drag-over');
    });
});
['dragleave', 'dragend'].forEach(function (ev) {
    window.addEventListener(ev, function (e) {
        if (e.relatedTarget === null) document.body.classList.remove('drag-over');
    });
});
window.addEventListener('drop', function (e) {
    if (!mdHasFiles(e)) return;
    e.preventDefault();
    document.body.classList.remove('drag-over');
    const dt = e.dataTransfer;
    const files = Array.from(dt.files || []);
    const file = files.find(function (f) { return /\.(md|markdown|mdx|txt)$/i.test(f.name); });
    if (!file) return; // only .md / .markdown / .mdx / .txt are accepted

    const path = mdDropPath(dt, file);
    if (path) { DotNet.invokeMethodAsync(ASM, 'OnFileDrop', 'path', path, ''); return; }

    const reader = new FileReader();
    reader.onload = function () {
        DotNet.invokeMethodAsync(ASM, 'OnFileDrop', 'text', file.name, String(reader.result || ''));
    };
    reader.readAsText(file);
});

// Sidebar resizer (drag updates --sidebar-w; the final width is reported to .NET on release).
window.mdInitResizer = function (splitterId) {
    const splitter = document.getElementById(splitterId);
    if (!splitter || splitter._wired) return;
    splitter._wired = true;
    const root = document.documentElement;
    let resizing = false, startX = 0, startW = 0;
    const cur = () => parseInt(getComputedStyle(root).getPropertyValue('--sidebar-w')) || 280;
    splitter.addEventListener('pointerdown', e => {
        resizing = true; startX = e.clientX; startW = cur();
        splitter.classList.add('dragging');
        try { splitter.setPointerCapture(e.pointerId); } catch (_) { }
    });
    splitter.addEventListener('pointermove', e => {
        if (!resizing) return;
        const w = Math.min(700, Math.max(150, startW + (e.clientX - startX)));
        root.style.setProperty('--sidebar-w', w + 'px');
    });
    const end = e => {
        if (!resizing) return;
        resizing = false;
        splitter.classList.remove('dragging');
        try { splitter.releasePointerCapture(e.pointerId); } catch (_) { }
        DotNet.invokeMethodAsync(ASM, 'OnSidebarResized', cur());
    };
    splitter.addEventListener('pointerup', end);
    splitter.addEventListener('pointercancel', end);
};

window.mdSetSidebarWidth = function (w) {
    document.documentElement.style.setProperty('--sidebar-w', w + 'px');
};
