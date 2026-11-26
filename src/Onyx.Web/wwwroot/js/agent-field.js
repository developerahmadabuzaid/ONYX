// Onyx — living agent field behind every public page.
// A decorative canvas: drifting agents that link up when near each other, pass glowing "messages"
// along those links (each arrival ripples and is often forwarded to a neighbour — agents
// collaborating), and lean gently toward the visitor's pointer. Purely progressive enhancement:
// without this script the static SVG mesh in _PublicLayout stays. Reduced motion draws one still
// frame; a hidden tab or a printed page costs nothing. No dependencies (CSP: script-src 'self').
(() => {
    "use strict";

    const canvas = document.querySelector("[data-agent-field]");
    if (!canvas || !canvas.getContext) return;
    const ctx = canvas.getContext("2d", { alpha: true });
    if (!ctx) return;

    const host = canvas.closest(".page-bg");
    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");

    // Graphics-only palette steps from tokens.css (teal, sky, violet, coral, sun, green). They never carry text.
    const PALETTE = [
        [15, 181, 168],
        [58, 160, 255],
        [124, 77, 255],
        [255, 111, 97],
        [255, 197, 49],
        [31, 157, 107],
    ];
    const rgba = (c, a) => `rgba(${c[0]},${c[1]},${c[2]},${a})`;

    let width = 0, height = 0, dpr = 1, linkDist = 150;
    let nodes = [], pulses = [], ripples = [];
    let running = false, frame = 0, last = 0, nextPulse = 0, lastScroll = window.scrollY;
    const pointer = { x: -9999, y: -9999, active: false, glow: 0 };

    const rand = (a, b) => a + Math.random() * (b - a);

    function resize() {
        dpr = Math.min(window.devicePixelRatio || 1, 1.5);
        width = window.innerWidth;
        height = window.innerHeight;
        canvas.width = Math.round(width * dpr);
        canvas.height = Math.round(height * dpr);
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

        const small = width < 768;
        linkDist = small ? 120 : Math.min(180, 110 + width / 30);
        const target = Math.round(Math.min(small ? 34 : 90, (width * height) / (small ? 13000 : 16000)));
        while (nodes.length < target) nodes.push(makeNode());
        if (nodes.length > target) nodes.length = target;
        for (const n of nodes) { n.x = Math.min(n.x, width); n.y = Math.min(n.y, height); }
    }

    function makeNode() {
        const hub = Math.random() < 0.12;
        return {
            x: rand(0, width), y: rand(0, height),
            vx: rand(-0.12, 0.12), vy: rand(-0.12, 0.12),
            r: hub ? rand(3.6, 5.2) : rand(1.6, 2.8),
            hub,
            color: PALETTE[Math.floor(Math.random() * PALETTE.length)],
            phase: rand(0, Math.PI * 2),
            flash: 0,
        };
    }

    // Neighbours within link distance, nearest first — the routes a message can take.
    function neighbours(i) {
        const a = nodes[i], out = [];
        for (let j = 0; j < nodes.length; j++) {
            if (j === i) continue;
            const b = nodes[j], d = Math.hypot(a.x - b.x, a.y - b.y);
            if (d < linkDist) out.push([j, d]);
        }
        return out.sort((p, q) => p[1] - q[1]).map((p) => p[0]);
    }

    function sendPulse(from, hops) {
        const options = neighbours(from);
        if (!options.length) return;
        const to = options[Math.floor(Math.random() * Math.min(3, options.length))];
        pulses.push({ from, to, t: 0, speed: rand(0.010, 0.018), hops, color: nodes[from].color });
    }

    function emitRipple(x, y, color, size) {
        ripples.push({ x, y, r: 2, max: size, color, life: 1 });
    }

    function step(dt) {
        const t = performance.now() / 1000;
        const pull = pointer.active ? 1 : 0;
        pointer.glow += ((pointer.active ? 1 : 0) - pointer.glow) * 0.06;

        for (const n of nodes) {
            // Soft lean toward the pointer, strongest at mid range, never snapping onto it.
            if (pull) {
                const dx = pointer.x - n.x, dy = pointer.y - n.y, d = Math.hypot(dx, dy);
                if (d < 220 && d > 24) {
                    const f = (1 - d / 220) * 0.012;
                    n.vx += (dx / d) * f; n.vy += (dy / d) * f;
                }
            }
            // A gentle wander keeps the field alive without ever settling.
            n.vx += Math.cos(t * 0.3 + n.phase) * 0.0025;
            n.vy += Math.sin(t * 0.27 + n.phase) * 0.0025;
            n.vx *= 0.985; n.vy *= 0.985;
            const sp = Math.hypot(n.vx, n.vy);
            if (sp > 0.6) { n.vx *= 0.6 / sp; n.vy *= 0.6 / sp; }
            n.x += n.vx * dt; n.y += n.vy * dt;
            if (n.x < -20) n.x = width + 20; else if (n.x > width + 20) n.x = -20;
            if (n.y < -20) n.y = height + 20; else if (n.y > height + 20) n.y = -20;
            n.flash *= 0.94;
        }

        for (let i = pulses.length - 1; i >= 0; i--) {
            const p = pulses[i];
            p.t += p.speed * dt;
            if (p.t >= 1) {
                const target = nodes[p.to];
                pulses.splice(i, 1);
                if (!target) continue;
                target.flash = 1;
                emitRipple(target.x, target.y, p.color, target.hub ? 34 : 22);
                // Collaboration: most messages are handed on, so work visibly travels across the network.
                if (p.hops > 0 && Math.random() < 0.8) sendPulse(p.to, p.hops - 1);
            }
        }

        for (let i = ripples.length - 1; i >= 0; i--) {
            const r = ripples[i];
            r.r += (r.max - r.r) * 0.06 * dt;
            r.life -= 0.016 * dt;
            if (r.life <= 0) ripples.splice(i, 1);
        }

        if (performance.now() > nextPulse && pulses.length < 9 && nodes.length) {
            const hubs = nodes.map((n, i) => (n.hub ? i : -1)).filter((i) => i >= 0);
            const from = hubs.length && Math.random() < 0.6 ? hubs[Math.floor(Math.random() * hubs.length)] : Math.floor(Math.random() * nodes.length);
            nodes[from].flash = 1;
            sendPulse(from, 3 + Math.floor(Math.random() * 4));
            nextPulse = performance.now() + rand(500, 1300);
        }
    }

    function draw() {
        const t = performance.now() / 1000;
        ctx.clearRect(0, 0, width, height);

        // Links: hairlines that fade with distance and blend the two agents' colours.
        ctx.lineWidth = 1;
        for (let i = 0; i < nodes.length; i++) {
            const a = nodes[i];
            for (let j = i + 1; j < nodes.length; j++) {
                const b = nodes[j];
                const dx = a.x - b.x, dy = a.y - b.y;
                if (Math.abs(dx) > linkDist || Math.abs(dy) > linkDist) continue;
                const d = Math.hypot(dx, dy);
                if (d >= linkDist) continue;
                const k = 1 - d / linkDist;
                const g = ctx.createLinearGradient(a.x, a.y, b.x, b.y);
                g.addColorStop(0, rgba(a.color, k * 0.42));
                g.addColorStop(1, rgba(b.color, k * 0.42));
                ctx.strokeStyle = g;
                ctx.beginPath(); ctx.moveTo(a.x, a.y); ctx.lineTo(b.x, b.y); ctx.stroke();
            }
        }

        // The visitor is part of the network: nearby agents reach out to the pointer.
        if (pointer.glow > 0.02) {
            const px = pointer.x, py = pointer.y;
            const halo = ctx.createRadialGradient(px, py, 0, px, py, 160);
            halo.addColorStop(0, `rgba(58,160,255,${0.10 * pointer.glow})`);
            halo.addColorStop(1, "rgba(58,160,255,0)");
            ctx.fillStyle = halo;
            ctx.beginPath(); ctx.arc(px, py, 160, 0, Math.PI * 2); ctx.fill();
            for (const n of nodes) {
                const d = Math.hypot(n.x - px, n.y - py);
                if (d > 190) continue;
                ctx.strokeStyle = rgba(n.color, (1 - d / 190) * 0.5 * pointer.glow);
                ctx.beginPath(); ctx.moveTo(n.x, n.y); ctx.lineTo(px, py); ctx.stroke();
            }
        }

        for (const r of ripples) {
            ctx.strokeStyle = rgba(r.color, Math.max(0, r.life) * 0.55);
            ctx.lineWidth = 1.2;
            ctx.beginPath(); ctx.arc(r.x, r.y, r.r, 0, Math.PI * 2); ctx.stroke();
        }

        // Messages in flight: a bright head with a short comet tail along the link.
        for (const p of pulses) {
            const a = nodes[p.from], b = nodes[p.to];
            if (!a || !b) continue;
            const tail = Math.max(0, p.t - 0.22);
            const hx = a.x + (b.x - a.x) * p.t, hy = a.y + (b.y - a.y) * p.t;
            const sx = a.x + (b.x - a.x) * tail, sy = a.y + (b.y - a.y) * tail;
            const g = ctx.createLinearGradient(sx, sy, hx, hy);
            g.addColorStop(0, rgba(p.color, 0));
            g.addColorStop(1, rgba(p.color, 0.9));
            ctx.strokeStyle = g; ctx.lineWidth = 2; ctx.lineCap = "round";
            ctx.beginPath(); ctx.moveTo(sx, sy); ctx.lineTo(hx, hy); ctx.stroke();
            ctx.fillStyle = rgba(p.color, 0.22);
            ctx.beginPath(); ctx.arc(hx, hy, 6, 0, Math.PI * 2); ctx.fill();
            ctx.fillStyle = rgba(p.color, 1);
            ctx.beginPath(); ctx.arc(hx, hy, 2.2, 0, Math.PI * 2); ctx.fill();
        }

        // Agents breathe; a node that just received a message brightens for a moment.
        for (const n of nodes) {
            const breathe = 1 + Math.sin(t * 1.6 + n.phase) * 0.18;
            const r = n.r * breathe + n.flash * 2.5;
            if (n.hub || n.flash > 0.05) {
                ctx.fillStyle = rgba(n.color, 0.12 + n.flash * 0.18);
                ctx.beginPath(); ctx.arc(n.x, n.y, r * 3, 0, Math.PI * 2); ctx.fill();
            }
            ctx.fillStyle = "rgba(255,255,255,0.95)";
            ctx.strokeStyle = rgba(n.color, 0.75 + n.flash * 0.25);
            ctx.lineWidth = n.hub ? 1.6 : 1.2;
            ctx.beginPath(); ctx.arc(n.x, n.y, r, 0, Math.PI * 2); ctx.fill(); ctx.stroke();
            if (n.hub) {
                ctx.fillStyle = rgba(n.color, 0.85);
                ctx.beginPath(); ctx.arc(n.x, n.y, r * 0.42, 0, Math.PI * 2); ctx.fill();
            }
        }
    }

    function loop(now) {
        if (!running) return;
        const dt = Math.min(3, (now - (last || now)) / 16.67);
        last = now;
        step(dt);
        draw();
        frame = requestAnimationFrame(loop);
    }

    function start() {
        if (running || reduceMotion.matches || document.hidden) return;
        running = true; last = 0;
        frame = requestAnimationFrame(loop);
    }
    function stop() { running = false; cancelAnimationFrame(frame); }

    function still() {
        // One composed frame for reduced motion: the network is shown, nothing moves.
        stop();
        pulses = []; ripples = [];
        draw();
    }

    resize();
    host?.classList.add("is-live");

    window.addEventListener("resize", () => { resize(); if (!running) draw(); }, { passive: true });
    window.addEventListener("scroll", () => {
        // Light parallax: agents drift at a fraction of the scroll; edge wrapping keeps the field filled.
        const delta = window.scrollY - lastScroll;
        lastScroll = window.scrollY;
        for (const n of nodes) n.y -= delta * 0.12;
        for (const r of ripples) r.y -= delta * 0.12;
        if (!running) draw();
    }, { passive: true });
    document.addEventListener("visibilitychange", () => (document.hidden ? stop() : start()));

    if (finePointer.matches) {
        window.addEventListener("pointermove", (e) => {
            pointer.x = e.clientX; pointer.y = e.clientY; pointer.active = true;
        }, { passive: true });
        document.addEventListener("pointerleave", () => { pointer.active = false; });
        window.addEventListener("blur", () => { pointer.active = false; });
        // A click anywhere wakes the nearest agent, which starts a fresh chain of messages.
        window.addEventListener("pointerdown", (e) => {
            if (!running || !nodes.length) return;
            let best = 0, bestD = Infinity;
            nodes.forEach((n, i) => { const d = Math.hypot(n.x - e.clientX, n.y - e.clientY); if (d < bestD) { bestD = d; best = i; } });
            emitRipple(e.clientX, e.clientY, nodes[best].color, 46);
            nodes[best].flash = 1;
            sendPulse(best, 5);
        }, { passive: true });
    }

    const onMotionPref = () => (reduceMotion.matches ? still() : start());
    reduceMotion.addEventListener?.("change", onMotionPref);
    onMotionPref();
})();
