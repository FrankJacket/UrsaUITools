// Drag and resize behavior for UrsaFloatingDialog. Pointer events are delegated on the dialog root.

const instances = new Map();
const stack = []; // dialogs from bottom to top; used to bring the focused one to front

const interactive = "button, a, input, select, textarea, [data-ursa-no-drag]";

const clamp = (value, min, max) => Math.min(Math.max(value, min), Math.max(min, max));
const viewportWidth = () => document.documentElement.clientWidth;
const viewportHeight = () => window.innerHeight;

function applyStacking() {
    stack.forEach((el, index) => {
        el.style.zIndex = `calc(var(--mud-zindex-appbar) + ${index + 1})`;
    });
}

function bringToFront(root) {
    const index = stack.indexOf(root);
    if (index === stack.length - 1) {
        return;
    }
    if (index >= 0) {
        stack.splice(index, 1);
    }
    stack.push(root);
    applyStacking();
}

function setPosition(root, left, top) {
    root.style.left = `${left}px`;
    root.style.top = `${top}px`;
}

function currentRect(root) {
    const rect = root.getBoundingClientRect();
    return { left: rect.left, top: rect.top, width: rect.width, height: rect.height };
}

// Keeps the dialog entirely inside the viewport.
function fitToViewport(root, options) {
    const rect = currentRect(root);
    const maxWidth = viewportWidth();
    const maxHeight = viewportHeight();
    const width = clamp(rect.width, Math.min(options.minWidth, maxWidth), maxWidth);
    if (width !== rect.width) {
        root.style.width = `${width}px`;
    }
    if (root.style.height) {
        const height = clamp(rect.height, Math.min(options.minHeight, maxHeight), maxHeight);
        root.style.height = `${height}px`;
    }
    const fitted = currentRect(root);
    setPosition(
        root,
        clamp(fitted.left, 0, maxWidth - fitted.width),
        clamp(fitted.top, 0, maxHeight - fitted.height));
}

export function initialize(root, id, dotNetRef, options) {
    dispose(id);

    root.style.width = `${Math.min(options.width, viewportWidth())}px`;
    if (options.height != null) {
        root.style.height = `${Math.min(options.height, viewportHeight())}px`;
    }
    const size = currentRect(root);
    setPosition(
        root,
        clamp(options.x ?? (viewportWidth() - size.width) / 2, 0, viewportWidth() - size.width),
        clamp(options.y ?? (viewportHeight() - size.height) / 2, 0, viewportHeight() - size.height));

    stack.push(root);
    applyStacking();
    root.setAttribute("data-ursa-ready", "");

    let gesture = null;

    const onPointerDown = (event) => {
        bringToFront(root);
        if (event.button !== 0) {
            return;
        }

        const handle = event.target.closest(".ursa-resize-handle");
        const header = event.target.closest(".ursa-floating-dialog-header");
        let mode = null;
        if (handle) {
            mode = handle.dataset.dir;
        } else if (header && root.dataset.draggable === "true" && !event.target.closest(interactive)) {
            mode = "move";
        }
        if (!mode) {
            return;
        }

        event.preventDefault();
        const rect = currentRect(root);
        gesture = {
            mode,
            pointerId: event.pointerId,
            captureTarget: event.target,
            startX: event.clientX,
            startY: event.clientY,
            rect,
            moved: false,
        };
        event.target.setPointerCapture(event.pointerId);
    };

    const onPointerMove = (event) => {
        if (!gesture || event.pointerId !== gesture.pointerId) {
            return;
        }
        gesture.moved = true;
        const dx = event.clientX - gesture.startX;
        const dy = event.clientY - gesture.startY;
        const { left, top, width, height } = gesture.rect;
        const right = left + width;
        const bottom = top + height;
        const vw = viewportWidth();
        const vh = viewportHeight();

        if (gesture.mode === "move") {
            setPosition(root, clamp(left + dx, 0, vw - width), clamp(top + dy, 0, vh - height));
            return;
        }

        const mode = gesture.mode;
        let newLeft = left;
        let newTop = top;
        let newWidth = width;
        let newHeight = height;

        if (mode.includes("e")) {
            newWidth = clamp(width + dx, Math.min(options.minWidth, vw - left), vw - left);
        }
        if (mode.includes("w")) {
            newWidth = clamp(width - dx, Math.min(options.minWidth, right), right);
            newLeft = right - newWidth;
        }
        if (mode.includes("s")) {
            newHeight = clamp(height + dy, Math.min(options.minHeight, vh - top), vh - top);
        }
        if (mode.includes("n")) {
            newHeight = clamp(height - dy, Math.min(options.minHeight, bottom), bottom);
            newTop = bottom - newHeight;
        }

        if (mode.includes("e") || mode.includes("w")) {
            root.style.width = `${newWidth}px`;
        }
        if (mode.includes("n") || mode.includes("s")) {
            root.style.height = `${newHeight}px`;
        }
        setPosition(root, newLeft, newTop);
    };

    const onPointerEnd = (event) => {
        if (!gesture || event.pointerId !== gesture.pointerId) {
            return;
        }
        const { moved, captureTarget } = gesture;
        gesture = null;
        if (captureTarget.hasPointerCapture?.(event.pointerId)) {
            captureTarget.releasePointerCapture(event.pointerId);
        }
        if (moved) {
            const rect = currentRect(root);
            dotNetRef.invokeMethodAsync("NotifyBoundsChanged", rect.left, rect.top, rect.width, rect.height);
        }
    };

    const onWindowResize = () => fitToViewport(root, options);

    root.addEventListener("pointerdown", onPointerDown);
    root.addEventListener("pointermove", onPointerMove);
    root.addEventListener("pointerup", onPointerEnd);
    root.addEventListener("pointercancel", onPointerEnd);
    window.addEventListener("resize", onWindowResize);

    instances.set(id, () => {
        root.removeEventListener("pointerdown", onPointerDown);
        root.removeEventListener("pointermove", onPointerMove);
        root.removeEventListener("pointerup", onPointerEnd);
        root.removeEventListener("pointercancel", onPointerEnd);
        window.removeEventListener("resize", onWindowResize);
        const index = stack.indexOf(root);
        if (index >= 0) {
            stack.splice(index, 1);
            applyStacking();
        }
    });
}

export function dispose(id) {
    const cleanup = instances.get(id);
    if (cleanup) {
        cleanup();
        instances.delete(id);
    }
}