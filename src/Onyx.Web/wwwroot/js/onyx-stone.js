// ONYX signature stone: a translucent-white faceted stone with ink edges, orbit lines and travelling spheres.
// Self-contained 3D renderer on a 2D canvas (crisp, thick, anti-aliased edges; no WebGL or library dependency; CSP: script-src 'self').
// The geometry is an approximation of the faceted stone inside the official ONYX logo (traced from the supplied raster logo;
// no vector source was available): pointed crown, hexagonal silhouette, a broad front table facet and smaller lower facets.
(() => {
    "use strict";

    // ---------------------------------------------------------------- Geometry ---
    // Logo-traced silhouette ring (x right, y up), front facet points and back points. Units: stone radius ≈ 1.
    const POINTS = [
        // Silhouette ring (front view of the logo stone)
        [0.12, 0.98, 0.02], [0.42, 0.6, 0.06], [0.74, 0.2, 0.0], [0.72, -0.64, 0.05], [0.24, -0.98, -0.02],
        [-0.36, -0.88, 0.04], [-0.7, -0.6, -0.02], [-0.77, 0.2, 0.05], [-0.38, 0.63, -0.03],
        // Front: the broad table facet (coplanar) and a crown point under the apex
        [-0.36, 0.24, 0.6], [0.34, 0.14, 0.6], [0.46, -0.36, 0.6], [-0.04, -0.68, 0.6], [-0.52, -0.26, 0.6],
        [0.06, 0.62, 0.42], [0.62, -0.1, 0.36], [-0.62, -0.02, 0.34], [-0.46, -0.62, 0.38],
        // Back
        [-0.2, 0.32, -0.56], [0.42, 0.02, -0.5], [0.12, -0.6, -0.56], [-0.46, -0.32, -0.5], [0.08, 0.68, -0.36],
    ];

    const sub = (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
    const dot = (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
    const cross = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
    const norm = (a) => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; };

    // Incremental convex hull → outward triangles, then coplanar triangles merged into polygon facets.
    function buildStone(points) {
        const eps = 1e-7;
        const face = (a, b, c) => { const n = norm(cross(sub(points[b], points[a]), sub(points[c], points[a]))); return { v: [a, b, c], n, d: dot(n, points[a]) }; };
        const centre = points.slice(0, 4).reduce((s, p) => [s[0] + p[0] / 4, s[1] + p[1] / 4, s[2] + p[2] / 4], [0, 0, 0]);
        let faces = [[0, 1, 2], [0, 1, 3], [0, 2, 3], [1, 2, 3]].map(([a, b, c]) => {
            const f = face(a, b, c);
            return dot(f.n, centre) - f.d > 0 ? face(a, c, b) : f;
        });
        // The first four points must not be coplanar; the ring's z offsets guarantee it.
        for (let i = 4; i < points.length; i++) {
            const visible = faces.filter((f) => dot(f.n, points[i]) - f.d > eps);
            if (visible.length === 0) continue;
            const edges = new Set();
            for (const f of visible) for (let k = 0; k < 3; k++) edges.add(`${f.v[k]},${f.v[(k + 1) % 3]}`);
            faces = faces.filter((f) => !visible.includes(f));
            for (const key of edges) {
                const [a, b] = key.split(",").map(Number);
                if (!edges.has(`${b},${a}`)) faces.push(face(a, b, i));
            }
        }

        // Group coplanar neighbours (union-find), then trace each group's boundary loop.
        const parent = faces.map((_, i) => i);
        const find = (i) => (parent[i] === i ? i : (parent[i] = find(parent[i])));
        const owner = new Map();
        faces.forEach((f, i) => { for (let k = 0; k < 3; k++) owner.set(`${f.v[k]},${f.v[(k + 1) % 3]}`, i); });
        faces.forEach((f, i) => {
            for (let k = 0; k < 3; k++) {
                const j = owner.get(`${f.v[(k + 1) % 3]},${f.v[k]}`);
                if (j !== undefined && dot(f.n, faces[j].n) > 0.9995) parent[find(i)] = find(j);
            }
        });
        const groups = new Map();
        faces.forEach((f, i) => { const r = find(i); if (!groups.has(r)) groups.set(r, []); groups.get(r).push(f); });
        const facets = [];
        for (const group of groups.values()) {
            const inGroup = new Set();
            for (const f of group) for (let k = 0; k < 3; k++) inGroup.add(`${f.v[k]},${f.v[(k + 1) % 3]}`);
            const next = new Map();
            for (const key of inGroup) {
                const [a, b] = key.split(",").map(Number);
                if (!inGroup.has(`${b},${a}`)) next.set(a, b);
            }
            const start = next.keys().next().value;
            const loop = [start];
            for (let v = next.get(start); v !== start && loop.length <= next.size; v = next.get(v)) loop.push(v);
            facets.push({ loop, n: group[0].n });
        }
        // Unique edges with the two facets that share them (for front/back edge styling).
        const edgeMap = new Map();
        facets.forEach((f, fi) => f.loop.forEach((a, k) => {
            const b = f.loop[(k + 1) % f.loop.length];
            const key = a < b ? `${a},${b}` : `${b},${a}`;
            if (!edgeMap.has(key)) edgeMap.set(key, { a: Math.min(a, b), b: Math.max(a, b), faces: [] });
            edgeMap.get(key).faces.push(fi);
        }));
        return { facets, edges: [...edgeMap.values()] };
    }

    const STONE = buildStone(POINTS);
    const LIGHT = norm([-0.55, 0.75, 0.9]);

    // Orbits: tilted circles around the stone; spheres travel along them.
    const ORBITS = [
        { radius: 1.34, tiltX: 1.18, tiltZ: 0.32, speed: 0.05, spheres: [{ phase: 0.4, speed: 0.32, size: 0.055 }] },
        { radius: 1.52, tiltX: 1.36, tiltZ: -0.42, speed: -0.035, spheres: [{ phase: 2.6, speed: -0.22, size: 0.07 }, { phase: 5.1, speed: -0.22, size: 0.045 }] },
        { radius: 1.22, tiltX: 0.48, tiltZ: 1.05, speed: 0.04, spheres: [{ phase: 1.3, speed: 0.27, size: 0.05 }] },
    ];

    // ------------------------------------------------------------------ Render ---
    // Sphere and contact-shadow sprites, drawn once and reused every frame.
    let spriteCache = null;
    function getSprites() {
        if (spriteCache) return spriteCache;
        const make = (size, paint) => {
            const c = document.createElement("canvas");
            c.width = c.height = size;
            paint(c.getContext("2d"), size);
            return c;
        };
        const sphere = make(128, (g, s) => {
            const r = s / 2;
            const grad = g.createRadialGradient(r * 0.65, r * 0.6, r * 0.05, r, r, r);
            grad.addColorStop(0, "#ffffff");
            grad.addColorStop(0.3, "#d4d4d4");
            grad.addColorStop(0.75, "#6b6b6b");
            grad.addColorStop(1, "#2b2b2b");
            g.fillStyle = grad;
            g.beginPath();
            g.arc(r, r, r - 0.5, 0, Math.PI * 2);
            g.fill();
        });
        const shadow = make(256, (g, s) => {
            const r = s / 2;
            const grad = g.createRadialGradient(r, r, 0, r, r, r);
            grad.addColorStop(0, "rgba(17,17,17,0.2)");
            grad.addColorStop(1, "rgba(17,17,17,0)");
            g.fillStyle = grad;
            g.fillRect(0, 0, s, s);
        });
        spriteCache = { sphere, shadow };
        return spriteCache;
    }

    function rotation(yaw, pitch, roll) {
        const cy = Math.cos(yaw), sy = Math.sin(yaw), cp = Math.cos(pitch), sp = Math.sin(pitch), cr = Math.cos(roll), sr = Math.sin(roll);
        // R = Rz(roll) · Rx(pitch) · Ry(yaw)
        return [
            [cr * cy - sr * sp * sy, -sr * cp, cr * sy + sr * sp * cy],
            [sr * cy + cr * sp * sy, cr * cp, sr * sy - cr * sp * cy],
            [-cp * sy, sp, cp * cy],
        ];
    }
    const apply = (m, p) => [m[0][0] * p[0] + m[0][1] * p[1] + m[0][2] * p[2], m[1][0] * p[0] + m[1][1] * p[1] + m[1][2] * p[2], m[2][0] * p[0] + m[2][1] * p[1] + m[2][2] * p[2]];

    /**
     * Draws one frame. state: { yaw, pitch, roll, lift, time } ; view: { width, height, dpr, scale, cx, cy, distance, tone, orbits, segments, maxSpheres }.
     */
    function draw(ctx, state, view) {
        const { width, height, dpr } = view;
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.clearRect(0, 0, width, height);

        const unit = view.scale;               // pixels per stone unit
        const D = view.distance;               // camera distance (perspective strength)
        const cx = view.cx, cy = view.cy - state.lift * unit;
        const project = (p) => { const k = D / (D - p[2]); return [cx + p[0] * k * unit, cy - p[1] * k * unit, k]; };
        const m = rotation(state.yaw, state.pitch, state.roll);
        const verts = POINTS.map((p) => apply(m, p));
        const screen = verts.map(project);
        const graphite = view.tone === "graphite";

        // Soft contact shadow, smaller and lighter as the stone lifts.
        const shadowY = view.cy + unit * 1.38;
        const spread = unit * (0.95 - state.lift * 0.6);
        // Pre-rendered sprite, scaled and faded per frame (no per-frame gradient or state save/restore).
        const sprites = getSprites();
        ctx.globalAlpha = Math.max(0, Math.min(1, ((graphite ? 0.2 : 0.13) - state.lift * 0.25) / 0.2));
        ctx.drawImage(sprites.shadow, view.cx - spread, shadowY - spread * 0.16, spread * 2, spread * 0.32);
        ctx.globalAlpha = 1;

        // Orbit geometry for this frame (split into behind / in front of the stone's centre plane).
        const orbitLayers = { back: [], front: [] };
        const spheres = [];
        if (view.orbits) {
            const precession = state.time;
            ORBITS.forEach((orbit) => {
                const om = rotation(precession * orbit.speed * 6 + state.yaw * 0.35, orbit.tiltX + state.pitch * 0.4, orbit.tiltZ);
                const ring = [];
                for (let i = 0; i <= view.segments; i++) {
                    const a = (i / view.segments) * Math.PI * 2;
                    ring.push(apply(om, [Math.cos(a) * orbit.radius, 0, Math.sin(a) * orbit.radius]));
                }
                for (let i = 0; i < ring.length - 1; i++) {
                    const p = ring[i], q = ring[i + 1];
                    ((p[2] + q[2]) / 2 < 0 ? orbitLayers.back : orbitLayers.front).push([project(p), project(q)]);
                }
                orbit.spheres.slice(0, view.maxSpheres).forEach((s) => {
                    const a = s.phase + state.time * s.speed;
                    const p = apply(om, [Math.cos(a) * orbit.radius, 0, Math.sin(a) * orbit.radius]);
                    spheres.push({ p, s: project(p), size: s.size });
                });
            });
        }

        const strokeOrbit = (segments) => {
            ctx.lineWidth = Math.max(1, dpr * 0.9);
            ctx.strokeStyle = graphite ? "rgba(17,17,17,0.35)" : "rgba(17,17,17,0.26)";
            ctx.beginPath();
            for (const [a, b] of segments) { ctx.moveTo(a[0], a[1]); ctx.lineTo(b[0], b[1]); }
            ctx.stroke();
        };
        const drawSphere = (sp) => {
            const r = sp.size * unit * sp.s[2];
            ctx.drawImage(sprites.sphere, sp.s[0] - r, sp.s[1] - r, r * 2, r * 2);
        };

        if (view.orbits) {
            strokeOrbit(orbitLayers.back);
            spheres.filter((sp) => sp.p[2] < 0).forEach(drawSphere);
        }

        // Facets: visible = facing the camera.
        const camera = [0, 0, D];
        const facets = STONE.facets.map((f) => {
            const n = apply(m, f.n);
            const p0 = verts[f.loop[0]];
            return { f, n, front: dot(n, sub(camera, p0)) > 0 };
        });
        const path = (loop) => {
            ctx.beginPath();
            loop.forEach((vi, k) => (k === 0 ? ctx.moveTo(screen[vi][0], screen[vi][1]) : ctx.lineTo(screen[vi][0], screen[vi][1])));
            ctx.closePath();
        };

        const edgeWidth = Math.max(1.5, unit * (view.edge ?? 0.024));
        ctx.lineJoin = "round";
        ctx.lineCap = "round";

        // Back edges, seen faintly through the translucent stone.
        ctx.lineWidth = edgeWidth * 0.45;
        ctx.strokeStyle = graphite ? "rgba(17,17,17,0.4)" : "rgba(17,17,17,0.34)";
        ctx.beginPath();
        for (const e of STONE.edges) {
            if (e.faces.every((fi) => !facets[fi].front)) {
                ctx.moveTo(screen[e.a][0], screen[e.a][1]);
                ctx.lineTo(screen[e.b][0], screen[e.b][1]);
            }
        }
        ctx.stroke();

        // Front facets: translucent white (or graphite) with a light-dependent sheen gradient.
        for (const item of facets) {
            if (!item.front) continue;
            const diffuse = Math.max(0, dot(item.n, LIGHT));
            const rim = 1 - Math.abs(item.n[2]);
            const loop = item.f.loop;
            let minY = Infinity, maxY = -Infinity, minX = Infinity, maxX = -Infinity;
            for (const vi of loop) { const s = screen[vi]; minY = Math.min(minY, s[1]); maxY = Math.max(maxY, s[1]); minX = Math.min(minX, s[0]); maxX = Math.max(maxX, s[0]); }
            const top = graphite ? 70 + diffuse * 150 : 226 + diffuse * 29 - rim * 18;
            const bottom = graphite ? 40 + diffuse * 110 : 206 + diffuse * 34 - rim * 22;
            const g = ctx.createLinearGradient(minX, minY, maxX * 0.3 + minX * 0.7, maxY);
            const alpha = graphite ? 0.96 : 0.8;
            g.addColorStop(0, `rgba(${top | 0},${top | 0},${top | 0},${alpha})`);
            g.addColorStop(1, `rgba(${bottom | 0},${bottom | 0},${bottom | 0},${alpha})`);
            ctx.fillStyle = g;
            path(loop);
            ctx.fill();
        }

        // Front edges in ink.
        ctx.lineWidth = edgeWidth;
        ctx.strokeStyle = "#111111";
        ctx.beginPath();
        for (const e of STONE.edges) {
            if (e.faces.some((fi) => facets[fi].front)) {
                ctx.moveTo(screen[e.a][0], screen[e.a][1]);
                ctx.lineTo(screen[e.b][0], screen[e.b][1]);
            }
        }
        ctx.stroke();

        if (view.orbits) {
            strokeOrbit(orbitLayers.front);
            spheres.filter((sp) => sp.p[2] >= 0).forEach(drawSphere);
        }
    }

    const instances = [];

    // ------------------------------------------------------------------- Mount ---
    // Cost controls: starts only after the page has loaded and the main thread is idle (the static image covers until then);
    // device-pixel ratio is capped; 30 fps on touch devices and whenever measured frame cost is high; runs only while on screen
    // and the tab is visible; reduced motion draws a single still frame.
    function mount(stage) {
        const canvas = stage.querySelector("canvas");
        if (!canvas || !canvas.getContext) return;
        const ctx = canvas.getContext("2d");
        if (!ctx) return;

        const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
        const coarse = window.matchMedia("(pointer: coarse), (max-width: 48rem)");
        const fine = window.matchMedia("(hover: hover) and (pointer: fine)");
        const view = { width: 0, height: 0, dpr: 1, scale: 1, cx: 0, cy: 0, distance: 5, tone: "light", orbits: true, segments: 96, maxSpheres: 2 };
        const pointer = { x: 0, y: 0, tx: 0, ty: 0 };
        let scroll = 0;
        let running = false;
        let visible = true;
        let started = false;
        let frame = 0;
        let last = 0;
        let lastDraw = 0;
        let time = 0;
        let cost = 0;            // exponential moving average of draw time (ms)
        let dprCap = 0;          // lowered once if the device struggles
        let interval = 0;        // minimum ms between frames (0 = every display frame)
        let pendingStill = 0;
        let frames = 0;          // warm-up frames are excluded from adaptation (sprite creation, JIT)

        const resize = () => {
            const rect = canvas.getBoundingClientRect();
            const low = coarse.matches;
            view.dpr = Math.min(window.devicePixelRatio || 1, dprCap || (low ? 1.25 : 1.5));
            view.width = Math.max(1, Math.round(rect.width * view.dpr));
            view.height = Math.max(1, Math.round(rect.height * view.dpr));
            canvas.width = view.width;
            canvas.height = view.height;
            view.scale = Math.min(view.width, view.height) * 0.3;
            view.cx = view.width / 2;
            view.cy = view.height * 0.47;
            view.segments = low ? 56 : 96;
            view.maxSpheres = low ? 1 : 2;
            interval = low ? 1000 / 30 : interval;
            render();
        };

        const state = () => {
            const drift = reduceMotion.matches ? 0 : time;
            return {
                time: drift,
                yaw: -0.38 + Math.sin(drift * 0.21) * 0.5 + pointer.x * 0.28 + scroll * 0.55,
                pitch: -0.1 + Math.sin(drift * 0.33) * 0.06 - pointer.y * 0.16 - scroll * 0.12,
                roll: 0.05 + Math.sin(drift * 0.27) * 0.035,
                lift: 0.035 + Math.sin(drift * 0.7) * 0.035 + scroll * 0.18,
            };
        };

        function render() {
            const t0 = performance.now();
            draw(ctx, state(), view);
            cost = cost ? cost * 0.9 + (performance.now() - t0) * 0.1 : performance.now() - t0;
        }

        // Adaptive quality: halve the frame rate above ~6 ms per draw, then drop to 1× pixels above ~10 ms.
        function adapt() {
            if (++frames < 90) { if (frames === 60) cost = 0; return; }
            if (cost > 6 && interval === 0) interval = 1000 / 30;
            if (cost > 10 && dprCap !== 1) { dprCap = 1; resize(); }
        }

        function tick(now) {
            frame = 0;
            if (!running) return;
            frame = window.requestAnimationFrame(tick);
            if (interval && now - lastDraw < interval - 1) return;
            const dt = Math.min(0.05, (now - (last || now)) / 1000);
            last = now;
            lastDraw = now;
            time += dt;
            pointer.x += (pointer.tx - pointer.x) * 0.08;
            pointer.y += (pointer.ty - pointer.y) * 0.08;
            render();
            adapt();
        }

        const start = () => {
            if (!started || running || reduceMotion.matches || !visible || document.hidden) return;
            running = true;
            last = 0;
            frame = window.requestAnimationFrame(tick);
        };
        const stop = () => {
            running = false;
            if (frame) window.cancelAnimationFrame(frame);
            frame = 0;
        };
        // One still frame on the next display frame (reduced motion or paused), coalescing bursts of scroll events.
        const still = () => {
            if (running || pendingStill || !started || !visible) return;
            pendingStill = window.requestAnimationFrame(() => { pendingStill = 0; render(); });
        };

        new IntersectionObserver((entries) => {
            visible = entries.some((e) => e.isIntersecting);
            if (visible) start(); else stop();
        }, { threshold: 0 }).observe(stage);
        document.addEventListener("visibilitychange", () => (document.hidden ? stop() : start()));
        reduceMotion.addEventListener("change", () => { stop(); render(); start(); });

        window.addEventListener("pointermove", (event) => {
            if (!fine.matches || reduceMotion.matches || !running) return;
            pointer.tx = (event.clientX / window.innerWidth - 0.5) * 2 * (document.documentElement.dir === "rtl" ? -1 : 1);
            pointer.ty = (event.clientY / window.innerHeight - 0.5) * 2;
        }, { passive: true });
        window.addEventListener("scroll", () => {
            if (!visible) return;
            const rect = stage.getBoundingClientRect();
            scroll = Math.max(0, Math.min(1, -rect.top / Math.max(1, rect.height)));
            still();
        }, { passive: true });

        const begin = () => {
            started = true;
            resize();
            if ("ResizeObserver" in window) new ResizeObserver(resize).observe(canvas); else window.addEventListener("resize", resize);
            stage.classList.add("is-live");
            start();
        };
        instances.push({ stage, stats: () => ({ drawMs: +cost.toFixed(2), fpsCap: interval ? Math.round(1000 / interval) : 60, dpr: view.dpr, running }) });
        const whenIdle = () => ("requestIdleCallback" in window ? window.requestIdleCallback(begin, { timeout: 1500 }) : window.setTimeout(begin, 200));
        if (document.readyState === "complete") whenIdle(); else window.addEventListener("load", whenIdle, { once: true });
    }

    // Exposed for the static-fallback capture (development tooling only); the page mounts automatically.
    window.OnyxStone = {
        draw,
        rotation,
        facetCount: STONE.facets.length,
        // Live cost per mounted stage (measured draw time, frame cap, pixel ratio) for performance checks.
        stats: () => instances.map((instance) => instance.stats()),
    };

    const init = () => document.querySelectorAll("[data-stone]").forEach(mount);
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init); else init();
})();
