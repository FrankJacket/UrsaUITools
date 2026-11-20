// Drag and resize behavior for UrsaFloatingDialog. Pointer events are delegated on the dialog root.

const instances = new Map(); // id -> { root, options, cleanup }
const stack = []; // dialogs from bottom to top; used to bring the focused one to front

const interactive = "button, a, input, select, textarea, [data-ursa-no-drag]";

const clamp = (value, min, max) => Math.min(Math.max(value, min), Math.max(min, max));

// The usable area: the viewport minus the optional insets the host declares with CSS variables
// (--ursa-dialog-inset-top/-bottom/-left/-right, in px), for example to leave an app bar and a taskbar visible.
function area() {
    const style = getComputedStyle(document.documentElement);
    const inset = (name) => parseFloat(style.getPropertyValue(`--ursa-dialog-inset-${name}`)) || 0;
    const left = inset("left");
    const top = inset("top");
    const right = document.documentElement.clientWidth - inset("right");
    const bottom = window.innerHeight - inset("bottom");
    return { left, top, right, bottom, width: right - left, height: bottom - top };
}

const isHidden = (root) => root.classList.contains("ursa-minimized");
const isMaximized = (root) => root.classList.contains("ursa-maximized");

function applyStacking() {
    stack.forEach((el, index) => {
        el.style.zIndex = `calc(var(--mud-zindex-appbar) + ${index + 1})`;
    });
}

function bringToFrontRoot(root) {
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

// Keeps the dialog entirely inside the usable area. Does nothing while the dialog is hidden or maximized
// (its size is then not its own).
function fitToViewport(root, options) {
    if (isHidden(root) || isMaximized(root)) {
        return;
    }
    const a = area();
    const rect = currentRect(root);
    const width = clamp(rect.width, Math.min(options.minWidth, a.width), a.width);
    if (width !== rect.width) {
        root.style.width = `${width}px`;
    }
    if (root.style.height) {
        const height = clamp(rect.height, Math.min(options.minHeight, a.height), a.height);
        root.style.height = `${height}px`;
    }
    const fitted = currentRect(root);
    setPosition(
        root,
        clamp(fitted.left, a.left, a.right - fitted.width),
        clamp(fitted.top, a.top, a.bottom - fitted.height));
}

export function initialize(root, id, dotNetRef, options) {
    dispose(id);

    const a = area();
    root.style.width = `${Math.min(options.width, a.width)}px`;
    if (options.height != null) {
        root.style.height = `${Math.min(options.height, a.height)}px`;
    }
    const size = currentRect(root);
    setPosition(
        root,
        clamp(options.x ?? a.left + (a.width - size.width) / 2, a.left, a.right - size.width),
        clamp(options.y ?? a.top + (a.height - size.height) / 2, a.top, a.bottom - size.height));

    stack.push(root);
    applyStacking();
    root.setAttribute("data-ursa-ready", "");

    let gesture = null;

    const onPointerDown = (event) => {
        bringToFrontRoot(root);
        if (event.button !== 0 || isMaximized(root)) {
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
        const bounds = area();

        if (gesture.mode === "move") {
            setPosition(
                root,
                clamp(left + dx, bounds.left, bounds.right - width),
                clamp(top + dy, bounds.top, bounds.bottom - height));
            return;
        }

        const mode = gesture.mode;
        let newLeft = left;
        let newTop = top;
        let newWidth = width;
        let newHeight = height;

        if (mode.includes("e")) {
            newWidth = clamp(width + dx, Math.min(options.minWidth, bounds.right - left), bounds.right - left);
        }
        if (mode.includes("w")) {
            newWidth = clamp(width - dx, Math.min(options.minWidth, right - bounds.left), right - bounds.left);
            newLeft = right - newWidth;
        }
        if (mode.includes("s")) {
            newHeight = clamp(height + dy, Math.min(options.minHeight, bounds.bottom - top), bounds.bottom - top);
        }
        if (mode.includes("n")) {
            newHeight = clamp(height - dy, Math.min(options.minHeight, bottom - bounds.top), bottom - bounds.top);
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

    instances.set(id, {
        root,
        options,
        cleanup: () => {
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
        },
    });
}

// Puts the dialog above the other floating dialogs.
export function bringToFront(id) {
    const instance = instances.get(id);
    if (instance) {
        bringToFrontRoot(instance.root);
    }
}

// Refits the dialog after it comes back from minimized or maximized (the viewport may have changed meanwhile).
export function refit(id) {
    const instance = instances.get(id);
    if (instance) {
        fitToViewport(instance.root, instance.options);
    }
}

export function dispose(id) {
    const instance = instances.get(id);
    if (instance) {
        instance.cleanup();
        instances.delete(id);
    }
}
