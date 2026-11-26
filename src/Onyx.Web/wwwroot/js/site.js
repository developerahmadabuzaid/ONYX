// Onyx public site — progressive enhancement only. Every control works without this file
// (menus are native <details> disclosures). No dependencies, no inline scripts (CSP: script-src 'self').
(() => {
    "use strict";

    const root = document.documentElement;
    root.classList.add("js");
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");

    const ready = (fn) => (document.readyState === "loading" ? document.addEventListener("DOMContentLoaded", fn) : fn());

    ready(() => {
        const header = document.querySelector("[data-header]");
        const mega = document.querySelector("[data-mega]");
        const sheet = document.querySelector("[data-sheet]");

        // --- Header state: condensed after the first scroll step ------------------------
        if (header) {
            let ticking = false;
            const update = () => {
                header.classList.toggle("is-condensed", window.scrollY > 24);
                ticking = false;
            };
            window.addEventListener("scroll", () => {
                if (!ticking) {
                    ticking = true;
                    window.requestAnimationFrame(update);
                }
            }, { passive: true });
            update();
        }

        // --- Mega menu ------------------------------------------------------------------
        if (mega) {
            const trigger = mega.querySelector("summary");
            const items = () => Array.from(mega.querySelectorAll(".mega__panel a"));
            let openTimer = 0;
            let closeTimer = 0;

            const setOpen = (open, focusTrigger) => {
                window.clearTimeout(openTimer);
                window.clearTimeout(closeTimer);
                if (mega.open === open) {
                    return;
                }
                mega.open = open;
                if (!open && focusTrigger) {
                    trigger.focus();
                }
            };

            mega.addEventListener("toggle", () => root.classList.toggle("mega-open", mega.open));

            // Hover intent on fine pointers only; click/Enter always works through the native summary.
            mega.addEventListener("pointerenter", (event) => {
                if (!finePointer.matches || event.pointerType !== "mouse") return;
                window.clearTimeout(closeTimer);
                openTimer = window.setTimeout(() => setOpen(true), 150);
            });
            mega.addEventListener("pointerleave", (event) => {
                if (!finePointer.matches || event.pointerType !== "mouse") return;
                window.clearTimeout(openTimer);
                closeTimer = window.setTimeout(() => setOpen(false), 300);
            });

            document.addEventListener("pointerdown", (event) => {
                if (mega.open && !mega.contains(event.target)) setOpen(false);
            });

            mega.addEventListener("keydown", (event) => {
                if (event.key === "Escape" && mega.open) {
                    event.preventDefault();
                    setOpen(false, true);
                    return;
                }
                if (!mega.open || !["ArrowDown", "ArrowUp", "Home", "End"].includes(event.key)) return;
                const list = items();
                if (list.length === 0) return;
                event.preventDefault();
                const index = list.indexOf(document.activeElement);
                const next = event.key === "Home" ? 0
                    : event.key === "End" ? list.length - 1
                    : event.key === "ArrowDown" ? (index + 1) % list.length
                    : (index <= 0 ? list.length - 1 : index - 1);
                list[next].focus();
            });

            // Following a link inside the panel (e.g. an in-page anchor) closes it.
            mega.addEventListener("click", (event) => {
                if (event.target.closest(".mega__panel a")) setOpen(false);
            });
        }

        // --- Mobile sheet -----------------------------------------------------------------
        if (sheet) {
            const toggle = sheet.querySelector(":scope > summary");
            sheet.addEventListener("toggle", () => {
                // The panel starts below the header bar wherever it sits (the language notice can push it down).
                const bar = sheet.closest("[data-header]");
                if (sheet.open && bar) root.style.setProperty("--sheet-top", `${Math.max(0, bar.getBoundingClientRect().bottom)}px`);
                root.classList.toggle("sheet-open", sheet.open);
            });
            document.addEventListener("keydown", (event) => {
                if (event.key === "Escape" && sheet.open) {
                    sheet.open = false;
                    toggle.focus();
                }
            });
            sheet.addEventListener("click", (event) => {
                if (event.target.closest(".sheet__panel a")) sheet.open = false;
            });
            window.matchMedia("(min-width: 64rem)").addEventListener("change", (event) => {
                if (event.matches) sheet.open = false;
            });
        }

        // --- Reveal on scroll (below-the-fold content only; skipped for reduced motion) ------
        const reveals = document.querySelectorAll("[data-reveal]");
        if (reveals.length && "IntersectionObserver" in window && !reduceMotion.matches) {
            root.classList.add("reveal-ready");
            const observer = new IntersectionObserver((entries) => {
                for (const entry of entries) {
                    if (entry.isIntersecting) {
                        entry.target.classList.add("is-visible");
                        observer.unobserve(entry.target);
                    }
                }
            }, { rootMargin: "0px 0px -8% 0px", threshold: 0.12 });
            reveals.forEach((element, index) => {
                element.style.setProperty("--reveal-delay", `${Math.min(index % 4, 3) * 70}ms`);
                observer.observe(element);
            });
        }
    });
})();
