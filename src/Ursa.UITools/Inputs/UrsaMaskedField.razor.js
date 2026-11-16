// Input mask for UrsaMaskedField. The whole editing logic runs in the browser: Blazor only hears about the
// value when it is committed (blur, Enter, or as soon as the mask is complete), never on each key stroke.
//
// Mask syntax: 0 = digit, a = letter, * = any character; every other character is a literal.

const isSlot = c => c === '0' || c === 'a' || c === '*';

export function slotsOf(mask) {
    return [...mask].filter(isSlot);
}

export function accepts(slot, ch) {
    if (ch.length !== 1) return false;
    switch (slot) {
        case '0': return ch >= '0' && ch <= '9';
        case 'a': return /\p{L}/u.test(ch);
        default: return ch > ' ' || ch === ' ';
    }
}

// Splits a text into the characters typed in the slots, ignoring the literals of the mask.
// Returns the slot characters and how many of them sit before the caret.
export function extract(mask, text, caret, upper) {
    const raw = [];
    let before = 0;
    let p = 0;
    for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        let literal = false;
        while (p < mask.length && !isSlot(mask[p])) {
            if (mask[p++] === ch) { literal = true; break; }
        }
        if (literal) continue;
        if (p >= mask.length) break;
        if (accepts(mask[p], ch)) {
            raw.push(upper ? ch.toUpperCase() : ch);
            p++;
            if (i < caret) before++;
        }
    }
    return { raw, before };
}

// Builds the displayed text. Literals are only added when a slot follows them with a character, so that there is
// never a dangling "/" at the end. ends[k] is the index just after the k-th slot character.
export function format(mask, raw) {
    let text = '';
    let r = 0;
    const ends = [];
    for (const c of mask) {
        if (isSlot(c)) {
            if (r >= raw.length) break;
            text += raw[r++];
            ends.push(text.length);
        } else if (r < raw.length) {
            text += c;
        } else {
            break;
        }
    }
    return { text, ends };
}

// Caret index to use after `count` slot characters, moved past the literals when more characters follow.
export function caretFor(layout, raw, count) {
    if (count <= 0) return 0;
    if (count < raw.length) return layout.ends[count] - 1;
    return layout.ends[count - 1];
}

const states = new WeakMap();

function slotCount(state) {
    return slotsOf(state.options.mask).length;
}

function render(state, raw, caretCount) {
    const { input, options } = state;
    const layout = format(options.mask, raw);
    if (input.value !== layout.text) input.value = layout.text;
    const caret = caretFor(layout, raw, Math.min(caretCount, raw.length));
    input.setSelectionRange(caret, caret);
    refreshMode(state);
    return layout.text;
}

function notify(state, text, force) {
    if (!state.dotNetRef) return;
    if (!force && text === state.lastNotified) return;
    state.lastNotified = text;
    state.dotNetRef.invokeMethodAsync('OnMaskCommit', text);
}

function onBeforeInput(state, e) {
    if (e.isComposing) return;
    const { input, options } = state;
    const mask = options.mask;
    const slots = slotsOf(mask);
    const start = input.selectionStart ?? 0;
    const end = input.selectionEnd ?? start;
    const { raw } = extract(mask, input.value, start, options.upper);
    const b = extract(mask, input.value, start, options.upper).before;
    const bEnd = extract(mask, input.value, end, options.upper).before;
    const collapsed = start === end;
    const type = e.inputType;

    if (type === 'insertText' && e.data) {
        e.preventDefault();
        let chars = raw.slice();
        let pos = b;
        // A selection that reaches the end is removed first; any other selection is typed over from its start.
        if (!collapsed && bEnd >= chars.length) chars = chars.slice(0, b);
        for (const ch of e.data) {
            if (pos >= slots.length || !accepts(slots[pos], ch)) continue;
            const value = options.upper ? ch.toUpperCase() : ch;
            if (pos < chars.length) chars[pos] = value; else chars.push(value);
            pos++;
        }
        finish(state, chars, pos);
        return;
    }

    if (type === 'deleteContentBackward' || type === 'deleteContentForward') {
        e.preventDefault();
        const backward = type === 'deleteContentBackward';
        if (!collapsed) {
            if (bEnd >= raw.length) { finish(state, raw.slice(0, b), b); return; }
            render(state, raw, b); // only collapse the selection: deleting here would shift the following characters
            return;
        }
        if (b >= raw.length) {
            if (backward && b > 0) finish(state, raw.slice(0, b - 1), b - 1); // at the end: delete as usual
            return;
        }
        // In the middle: only move over the character so that the next one typed replaces it (no gaps, no shifting).
        render(state, raw, backward ? Math.max(b - 1, 0) : b + 1);
    }
}

function finish(state, raw, caretCount) {
    const text = render(state, raw, caretCount);
    if (state.options.commitOnComplete && raw.length === slotCount(state)) notify(state, text, false);
}

// Paste, cut, drag and drop, autofill...: let the browser edit, then normalize.
function onInput(state) {
    const { input, options } = state;
    const { raw, before } = extract(options.mask, input.value, input.selectionStart ?? input.value.length, options.upper);
    finish(state, raw, before);
}

function onBlur(state) {
    notify(state, state.input.value, false);
}

function onKeyDown(state, e) {
    if (e.key === 'Enter') notify(state, state.input.value, false);
}

// The element is either the input itself or a container holding it; states are keyed by the input.
function resolve(element) {
    if (!element) return null;
    return element instanceof HTMLInputElement ? element : element.querySelector('input');
}

export function attach(element, dotNetRef, options) {
    const input = resolve(element);
    if (!input) return;
    detach(input);
    const state = { input, dotNetRef, options, lastNotified: input.value };
    state.handlers = {
        beforeinput: e => onBeforeInput(state, e),
        input: () => onInput(state),
        blur: () => onBlur(state),
        keydown: e => onKeyDown(state, e),
        keyup: () => refreshMode(state),
        click: () => refreshMode(state),
        focus: () => refreshMode(state),
    };
    for (const [name, handler] of Object.entries(state.handlers)) input.addEventListener(name, handler);
    states.set(input, state);
    update(input, options);
}

// Same as attach, for an input that is only known by its id (the inner input of a MudBlazor picker).
export function attachById(id, dotNetRef, options) {
    attach(document.getElementById(id), dotNetRef, options);
}

// Applies new options or a new value pushed from the server (only the formatting; the caret is left alone).
export function update(element, options) {
    const input = resolve(element);
    const state = input && states.get(input);
    if (!state) return;
    state.options = options;
    const { raw } = extract(options.mask, input.value, 0, options.upper);
    const text = format(options.mask, raw).text;
    if (input.value !== text) input.value = text;
    state.lastNotified = input.value;
}

export function updateById(id, options) {
    update(document.getElementById(id), options);
}

export function detach(element) {
    const input = resolve(element);
    const state = input && states.get(input);
    if (!state) return;
    for (const [name, handler] of Object.entries(state.handlers)) input.removeEventListener(name, handler);
    input.classList.remove('ursa-overwrite');
    input.style.caretShape = '';
    input.style.caretColor = '';
    states.delete(input);
}

export function detachById(id) {
    detach(document.getElementById(id));
}

// Overwrite mode is on while the caret sits before a character: typing replaces it. The caret becomes a block
// (browsers that support caret-shape) in the primary color of the MudBlazor theme, and the input gets the class
// ursa-overwrite.
function refreshMode(state) {
    const { input, options } = state;
    const start = input.selectionStart ?? 0;
    const { raw, before } = extract(options.mask, input.value, start, options.upper);
    const overwrite = input.selectionStart === input.selectionEnd && before < raw.length;
    input.classList.toggle('ursa-overwrite', overwrite);
    input.style.caretShape = overwrite ? 'bar' : '';
    input.style.caretColor = overwrite ? 'var(--mud-palette-primary)' : '';
}
