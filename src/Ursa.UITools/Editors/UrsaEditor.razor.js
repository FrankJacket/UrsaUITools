// Quill glue for UrsaEditor. Quill runs without its own theme or toolbar: the toolbar is made of MudBlazor buttons
// (data-ursa-cmd) and the frame is styled with the MudBlazor CSS variables, so the editor follows the app theme.
// The server only hears about the content after a pause in the typing or on blur.

const BASE = new URL('../lib/quill/', import.meta.url).href;
const editors = new WeakMap();
let loading;

function loadQuill() {
    loading ??= new Promise((resolve, reject) => {
        if (!document.querySelector('link[data-ursa-quill]')) {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = BASE + 'quill.core.css';
            link.dataset.ursaQuill = '';
            document.head.append(link);
        }
        if (window.Quill) return resolve(window.Quill);
        const script = document.createElement('script');
        script.src = BASE + 'quill.js';
        script.onload = () => resolve(window.Quill);
        script.onerror = () => reject(new Error('Quill could not be loaded'));
        document.head.append(script);
    });
    return loading;
}

const COMMANDS = {
    bold: q => toggle(q, 'bold'),
    italic: q => toggle(q, 'italic'),
    underline: q => toggle(q, 'underline'),
    strike: q => toggle(q, 'strike'),
    h1: q => q.format('header', q.getFormat().header === 1 ? false : 1),
    h2: q => q.format('header', q.getFormat().header === 2 ? false : 2),
    h3: q => q.format('header', q.getFormat().header === 3 ? false : 3),
    bullet: q => q.format('list', q.getFormat().list === 'bullet' ? false : 'bullet'),
    ordered: q => q.format('list', q.getFormat().list === 'ordered' ? false : 'ordered'),
    blockquote: q => toggle(q, 'blockquote'),
    code: q => toggle(q, 'code-block'),
    clean: q => { const r = q.getSelection(); if (r) q.removeFormat(r.index, r.length); },
    undo: q => q.history.undo(),
    redo: q => q.history.redo(),
    link: (q, state) => requestTool(q, state, 'link'),
    image: (q, state) => requestTool(q, state, 'image'),
};

const ACTIVE = {
    bold: f => !!f.bold,
    italic: f => !!f.italic,
    underline: f => !!f.underline,
    strike: f => !!f.strike,
    h1: f => f.header === 1,
    h2: f => f.header === 2,
    h3: f => f.header === 3,
    bullet: f => f.list === 'bullet',
    ordered: f => f.list === 'ordered',
    blockquote: f => !!f.blockquote,
    code: f => !!f['code-block'],
    link: f => !!f.link,
};

function toggle(q, name) {
    q.format(name, !q.getFormat()[name]);
}

function html(quill) {
    // An empty document is '', not '<p></p>'.
    return quill.getText().trim() === '' && quill.getContents().ops.every(op => typeof op.insert === 'string')
        ? ''
        : quill.getSemanticHTML().replace(/(?<!&nbsp;)&nbsp;(?!&nbsp;)/g, ' '); // lone non-breaking spaces become plain ones
}

// Blazor Server refuses messages from the browser above 32 KB by default (the circuit is closed), and content with an
// embedded image easily goes beyond that. The content is therefore sent in pieces of 8000 characters (at most 24 KB in
// UTF-8) that the component puts back together. The sends are chained so that the pieces stay in order.
const CHUNK = 8000;

function commit(state) {
    clearTimeout(state.timer);
    const value = html(state.quill);
    if (value === state.lastCommitted) return;
    state.lastCommitted = value;
    state.sending = (state.sending ?? Promise.resolve()).then(() => send(state, value)).catch(() => { /* disconnected */ });
}

async function send(state, value) {
    if (value.length <= CHUNK) {
        await state.dotNetRef.invokeMethodAsync('OnEditorCommit', value);
        return;
    }
    const id = String(state.chunkId = (state.chunkId ?? 0) + 1);
    const parts = [];
    for (let start = 0; start < value.length;) {
        let end = Math.min(start + CHUNK, value.length);
        const last = value.charCodeAt(end - 1);
        if (end < value.length && last >= 0xD800 && last <= 0xDBFF) end++; // do not split a surrogate pair
        parts.push(value.slice(start, end));
        start = end;
    }
    for (let i = 0; i < parts.length; i++) {
        await state.dotNetRef.invokeMethodAsync('OnEditorChunk', id, i, parts.length, parts[i]);
    }
}

function refreshToolbar(state) {
    const format = state.quill.getSelection() ? state.quill.getFormat() : {};
    for (const button of state.root.querySelectorAll('[data-ursa-cmd]')) {
        const test = ACTIVE[button.dataset.ursaCmd];
        button.classList.toggle('ursa-editor-tool-active', !!test && test(format));
    }
}

export async function create(root, surface, dotNetRef, options) {
    const Quill = await loadQuill();
    destroy(root);
    const quill = new Quill(surface, {
        theme: undefined,
        modules: { toolbar: false, history: { delay: 800, maxStack: 200, userOnly: true } },
        placeholder: options.placeholder ?? '',
        readOnly: options.readOnly,
    });
    const state = { quill, root, dotNetRef, options, lastCommitted: html(quill), timer: 0 };

    quill.on('text-change', (_delta, _old, source) => {
        placeOverlay(state);
        refreshToolbar(state);
        if (source !== 'user') return;
        clearTimeout(state.timer);
        state.timer = setTimeout(() => commit(state), state.options.delay);
    });
    quill.on('selection-change', range => {
        root.classList.toggle('ursa-editor-focused', !!range);
        refreshToolbar(state);
        if (!range) commit(state); // blur
    });

    // The buttons must not steal the focus (and the selection) from the editor.
    state.onMouseDown = e => { if (e.target.closest('[data-ursa-cmd]')) e.preventDefault(); };
    state.onClick = e => {
        const button = e.target.closest('[data-ursa-cmd]');
        if (!button || state.options.readOnly) return;
        quill.focus();
        COMMANDS[button.dataset.ursaCmd]?.(quill, state);
        refreshToolbar(state);
    };
    root.addEventListener('mousedown', state.onMouseDown);
    root.addEventListener('click', state.onClick);
    state.onSurfaceClick = e => {
        const img = e.target instanceof HTMLImageElement && quill.root.contains(e.target) ? e.target : null;
        if (img) selectImage(state, img); else deselectImage(state);
    };
    state.onSurfaceDoubleClick = e => {
        if (e.target instanceof HTMLImageElement && quill.root.contains(e.target)) {
            selectImage(state, e.target);
            requestTool(quill, state, 'image');
        }
    };
    quill.root.addEventListener('click', state.onSurfaceClick);
    quill.root.addEventListener('dblclick', state.onSurfaceDoubleClick);
    editors.set(root, state);
}

export function configure(root, options) {
    const state = editors.get(root);
    if (!state) return;
    state.options = options;
    state.quill.enable(!options.readOnly);
    state.quill.root.dataset.placeholder = options.placeholder ?? '';
}

// Loads content pushed by the application. Quill drops what it does not support, which also cleans the HTML.
export function setHtml(root, value) {
    const state = editors.get(root);
    if (!state) return;
    const delta = state.quill.clipboard.convert({ html: value ?? '', text: '' });
    state.quill.setContents(delta, 'api');
    state.quill.history.clear();
    state.lastCommitted = html(state.quill);
}

export function destroy(root) {
    const state = editors.get(root);
    if (!state) return;
    clearTimeout(state.timer);
    root.removeEventListener('mousedown', state.onMouseDown);
    root.removeEventListener('click', state.onClick);
    state.quill.root.removeEventListener('click', state.onSurfaceClick);
    state.quill.root.removeEventListener('dblclick', state.onSurfaceDoubleClick);
    deselectImage(state);
    editors.delete(root);
}

// Link and image need an address: remember the selection, then let the component show its panel.

// Applies (or removes, when the address is empty) a link on the remembered selection. Without a selection, the
// address itself is inserted as the link text. Quill only keeps safe protocols (http, https, mailto, tel).
export function applyLink(root, url) {
    const state = editors.get(root);
    if (!state) return;
    const { quill } = state;
    const range = state.savedRange ?? { index: quill.getLength() - 1, length: 0 };
    quill.focus();
    if (!url) {
        quill.formatText(range.index, range.length || 1, 'link', false, 'user');
    } else if (range.length > 0) {
        quill.formatText(range.index, range.length, 'link', url, 'user');
    } else {
        quill.insertText(range.index, url, 'link', url, 'user');
    }
    commit(state);
}

// Inserts an image at the remembered caret. Quill only keeps http, https and data addresses.

function imageAt(quill, range) {
    if (!range || range.length !== 1) return null;
    const [leaf] = quill.getLeaf(range.index);
    return leaf?.domNode instanceof HTMLImageElement ? leaf.domNode : null;
}

function sizeOf(img) {
    const width = parseInt(img.getAttribute('width') ?? '', 10);
    return Number.isFinite(width) ? width : Math.round(img.getBoundingClientRect().width) || null;
}

// Link and image need an address: remember the selection, then let the component show its dialog.
// An image that is selected (clicked, or selected with the keyboard) is edited instead of inserting a new one.
function requestTool(quill, state, tool) {
    state.savedRange = quill.getSelection(true);
    if (tool === 'link') {
        const current = quill.getFormat(state.savedRange).link ?? '';
        state.dotNetRef.invokeMethodAsync('OnToolRequested', 'link', current, null, null);
        return;
    }
    const img = state.selectedImg ?? imageAt(quill, state.savedRange);
    if (img) {
        selectImage(state, img);
        state.dotNetRef.invokeMethodAsync('OnToolRequested', 'image-edit', null, img.getAttribute('alt'), sizeOf(img));
    } else {
        state.dotNetRef.invokeMethodAsync('OnToolRequested', 'image', null, null, null);
    }
}

function imageIndex(state, img) {
    const blot = window.Quill.find(img);
    return blot ? state.quill.getIndex(blot) : -1;
}

function applyImageFormat(state, index, alt, width) {
    const { quill } = state;
    quill.formatText(index, 1, 'alt', alt || false, 'user');
    quill.formatText(index, 1, 'height', false, 'user'); // the height follows the width
    quill.formatText(index, 1, 'width', width ? String(width) : false, 'user');
}

// Inserts an image at the remembered caret. Quill only keeps http, https and data addresses.
export function insertImage(root, src, alt, width) {
    const state = editors.get(root);
    if (!state) return;
    const { quill } = state;
    const index = state.savedRange?.index ?? quill.getLength() - 1;
    quill.focus();
    quill.insertEmbed(index, 'image', src, 'user');
    applyImageFormat(state, index, alt, width);
    quill.setSelection(index + 1, 0, 'silent');
    commit(state);
}

// Changes the description and the width of the selected image.
export function updateImage(root, alt, width) {
    const state = editors.get(root);
    const img = state?.selectedImg;
    if (!img) return;
    const index = imageIndex(state, img);
    if (index < 0) return;
    state.quill.focus();
    applyImageFormat(state, index, alt, width);
    commit(state);
    requestAnimationFrame(() => selectImage(state, state.quill.root.querySelectorAll('img')[imageOrdinal(state, index)] ?? null));
}

function imageOrdinal(state, index) {
    return state.quill.getContents(0, index).ops.filter(op => op.insert?.image).length;
}

// The selected image gets an outline and a handle in its bottom-right corner that resizes it with the mouse.
function selectImage(state, img) {
    deselectImage(state);
    if (!img || state.options.readOnly) return;
    state.selectedImg = img;
    const overlay = document.createElement('div');
    overlay.style.cssText = 'position:absolute;pointer-events:none;outline:2px solid var(--mud-palette-primary);z-index:1';
    const handle = document.createElement('div');
    handle.title = 'Drag to resize';
    handle.style.cssText = 'position:absolute;right:-6px;bottom:-6px;width:12px;height:12px;border-radius:2px;pointer-events:auto;' +
        'cursor:nwse-resize;touch-action:none;background:var(--mud-palette-primary);border:2px solid var(--mud-palette-surface)';
    overlay.append(handle);
    state.quill.container.append(overlay);
    state.overlay = overlay;
    placeOverlay(state);

    handle.addEventListener('pointerdown', e => {
        e.preventDefault();
        try { handle.setPointerCapture(e.pointerId); } catch { /* synthetic pointer */ }
        const startX = e.clientX;
        const startWidth = img.getBoundingClientRect().width;
        const max = state.quill.root.clientWidth - 28;
        let width = startWidth;
        const move = ev => {
            width = Math.round(Math.min(Math.max(startWidth + ev.clientX - startX, 16), max));
            img.style.width = width + 'px';
            img.style.height = 'auto';
            placeOverlay(state);
        };
        const up = () => {
            handle.removeEventListener('pointermove', move);
            handle.removeEventListener('pointerup', up);
            img.style.width = '';
            img.style.height = '';
            const index = imageIndex(state, img);
            if (index >= 0) {
                applyImageFormat(state, index, img.getAttribute('alt'), width);
                commit(state);
            }
            placeOverlay(state);
        };
        handle.addEventListener('pointermove', move);
        handle.addEventListener('pointerup', up);
    });
}

function placeOverlay(state) {
    const { overlay, selectedImg: img } = state;
    if (!overlay || !img || !img.isConnected) return deselectImage(state);
    const box = img.getBoundingClientRect();
    const host = state.quill.container.getBoundingClientRect();
    overlay.style.left = box.left - host.left + 'px';
    overlay.style.top = box.top - host.top + 'px';
    overlay.style.width = box.width + 'px';
    overlay.style.height = box.height + 'px';
}

function deselectImage(state) {
    state.overlay?.remove();
    state.overlay = null;
    state.selectedImg = null;
}
