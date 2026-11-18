// Input mask for UrsaMaskedField. The whole editing logic runs in the browser: Blazor only hears about the
// value when it is committed (blur, Enter, or as soon as the mask is complete), never on each key stroke.
//
// Two mutually exclusive modes, chosen per field by options.mode:
// - 'mask' (the default): fixed-width mask, 0 = digit, a = letter, * = any character; every other character is
//   a literal.
// - 'regex': a prefix-matching regular expression constrains typing when the value has no fixed width (for
//   example an IPv4 address, whose octets are 1 to 3 digits each), ported from MudBlazor's RegexMask
//   (MudBlazor.Utilities.MaskAlgorithms.RegexMask, MIT licensed).

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

// --- 'regex' mode: a prefix-matching regular expression, with delimiters auto-inserted. ---

// Splits a text into its significant characters (everything that isn't one of the delimiters), ignoring position,
// and how many of them sit before the caret. Delimiters are only ever inserted by alignRegex below, so a value
// never has one in a position alignRegex itself would not have put it.
export function extractRegex(delimiters, text, caret) {
    const raw = [];
    let before = 0;
    for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        if (delimiters.includes(ch)) continue;
        raw.push(ch);
        if (i < caret) before++;
    }
    return { raw, before };
}

// Rebuilds a valid value from a stream of significant characters (digits, letters...), the same way MudBlazor's
// RegexMask aligns text against its mask: each character is appended only if the text so far plus that character
// still matches `regex`; failing that, the first delimiter that makes it match again is inserted before it;
// failing that too, the character is dropped. Returns the rebuilt text, the index in `text` right after each
// surviving character (ends[k]), and which original indices of `raw` survived (acceptedIdx).
export function alignRegex(regex, delimiters, raw) {
    let text = '';
    const ends = [];
    const acceptedIdx = [];
    for (let i = 0; i < raw.length; i++) {
        const ch = raw[i];
        if (regex.test(text + ch)) {
            text += ch;
        } else {
            let inserted = false;
            for (const d of delimiters) {
                if (regex.test(text + d + ch)) {
                    text += d + ch;
                    inserted = true;
                    break;
                }
            }
            if (!inserted) continue;
        }
        acceptedIdx.push(i);
        ends.push(text.length);
    }
    return { text, ends, acceptedIdx };
}

// Caret index to use after `count` significant characters (of the original raw stream passed to alignRegex) have
// been accepted at or before the edit point.
function caretForRegex(aligned, count) {
    if (count <= 0) return 0;
    let n = 0;
    for (let k = 0; k < aligned.acceptedIdx.length; k++) {
        if (aligned.acceptedIdx[k] >= count) break;
        n = k + 1;
    }
    return n > 0 ? aligned.ends[n - 1] : 0;
}

function renderRegex(state, raw, caretRawCount) {
    const { input, options } = state;
    const regex = options.compiled ??= new RegExp(options.regex, 'u');
    const aligned = alignRegex(regex, options.delimiters, raw);
    if (input.value !== aligned.text) input.value = aligned.text;
    const caret = caretForRegex(aligned, caretRawCount);
    input.setSelectionRange(caret, caret);
    return aligned.text;
}

function onBeforeInputRegex(state, e) {
    if (e.isComposing) return;
    const { input, options } = state;
    const start = input.selectionStart ?? 0;
    const end = input.selectionEnd ?? start;
    const collapsed = start === end;
    const type = e.inputType;
    const { raw, before: b } = extractRegex(options.delimiters, input.value, start);
    const { before: bEnd } = extractRegex(options.delimiters, input.value, end);

    if (type === 'insertText' && e.data) {
        e.preventDefault();
        const chars = raw.slice(0, b).concat(raw.slice(bEnd));
        let pos = b;
        for (const ch of e.data) {
            chars.splice(pos, 0, options.upper ? ch.toUpperCase() : ch);
            pos++;
        }
        renderRegex(state, chars, pos);
        return;
    }

    if (type === 'deleteContentBackward' || type === 'deleteContentForward') {
        e.preventDefault();
        if (!collapsed) {
            renderRegex(state, raw.slice(0, b).concat(raw.slice(bEnd)), b);
            return;
        }
        if (type === 'deleteContentBackward') {
            if (b > 0) renderRegex(state, raw.slice(0, b - 1).concat(raw.slice(b)), b - 1);
        } else if (b < raw.length) {
            renderRegex(state, raw.slice(0, b).concat(raw.slice(b + 1)), b);
        }
    }
}

// Paste, cut, drag and drop, autofill...: let the browser edit, then realign.
function onInputRegex(state) {
    const { input, options } = state;
    const { raw, before } = extractRegex(options.delimiters, input.value, input.selectionStart ?? input.value.length);
    renderRegex(state, raw, before);
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
        beforeinput: e => (state.options.mode === 'regex' ? onBeforeInputRegex : onBeforeInput)(state, e),
        input: () => (state.options.mode === 'regex' ? onInputRegex : onInput)(state),
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
    let text;
    if (options.mode === 'regex') {
        const regex = options.compiled ??= new RegExp(options.regex, 'u');
        const { raw } = extractRegex(options.delimiters, input.value, 0);
        text = alignRegex(regex, options.delimiters, raw).text;
    } else {
        const { raw } = extract(options.mask, input.value, 0, options.upper);
        text = format(options.mask, raw).text;
    }
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
    // 'regex' mode always inserts (there is no fixed slot to overwrite).
    const overwrite = options.mode !== 'regex' && (() => {
        const start = input.selectionStart ?? 0;
        const { raw, before } = extract(options.mask, input.value, start, options.upper);
        return input.selectionStart === input.selectionEnd && before < raw.length;
    })();
    input.classList.toggle('ursa-overwrite', overwrite);
    input.style.caretShape = overwrite ? 'bar' : '';
    input.style.caretColor = overwrite ? 'var(--mud-palette-primary)' : '';
}
