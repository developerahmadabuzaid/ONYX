# Media credits

Provenance for every non-original media asset shipped from `src/Onyx.Web/wwwroot`. Fonts are credited
alongside their files in `wwwroot/fonts/LICENSE-*.txt`; this file covers any footage while it ships.

**Why this file exists:** an asset may only be published with a recorded source and licence. It also
records what each asset may *not* be used for, so the site never implies something untrue
(`CLAUDE.md` §2.1). Keep it current: add a row before shipping a new asset.

## Hero reel — `wwwroot/videos/hero/`

Four short clips play in sequence in the homepage hero frame (6.5 s each, 26 s for the full loop) and
are decorative: the hero stage is `aria-hidden`, they carry no caption, and **they are not Onyx people,
clients, offices or work**. Do not caption them as such, and do not pair them with a claim
(`CLAUDE.md` §2.1 — no invented team, clients or proof).

Source: [Mixkit](https://mixkit.co/) — **Mixkit Stock Video Free License**. What was verified on
2026-10-14: each clip page below carries that licence label, and Mixkit's own site states that items
under the Free License may be used in commercial and non-commercial projects. Mixkit's full licence
text is rendered in a modal that could not be read by the tooling used here, so the *attribution*
clause was **not** independently confirmed — every clip is credited in this file regardless, which
satisfies the stricter reading too. Re-check the licence page before any re-use outside this site.

Each clip's own page is the authority on its licence. Mixkit also publishes a *Restricted* licence
(personal use only) for some items, so **check the label on the item page before shipping anything new
from this source** — two otherwise suitable clips were rejected for exactly that reason (below).

| Shipped file | Mixkit item | Source page (licence) | Original file | Original length | Used segment |
|---|---|---|---|---|---|
| `01-ai.mp4` | A woman wearing futuristic VR glasses controlling an unseen user interface (#51214) | [page](https://mixkit.co/free-stock-video/a-woman-wearign-a-futuristic-vr-glasses-concentrates-on-the-51214/) | `assets.mixkit.co/videos/51214/51214-720.mp4` | 18.1 s | 3.0 s – 9.5 s |
| `02-code.mp4` | Programmer working with codes on a computer (#41637) | [page](https://mixkit.co/free-stock-video/programmer-working-with-codes-on-a-computer-41637/) | `assets.mixkit.co/videos/41637/41637-720.mp4` | 10.3 s | 1.2 s – 7.7 s |
| `03-business.mp4` | A young business man working on a laptop on the table takes a sip of coffee (#99898) | [page](https://mixkit.co/free-stock-video/a-young-business-man-working-on-a-laptop-on-the-99898/) | `assets.mixkit.co/active_storage/video_items/99898/1717706002/99898-video-720.mp4` | 10.6 s | 1.5 s – 8.0 s |
| `04-accounts.mp4` | Woman doing accounts on a calculator (#4533) | [page](https://mixkit.co/free-stock-video/woman-doing-accounts-on-a-calculator-4533/) | `assets.mixkit.co/videos/4533/4533-720.mp4` | 21.0 s | 5.0 s – 11.5 s |

**Processing applied** (ffmpeg, so the shipped files are deterministic):

```
ffmpeg -ss <start> -t 6.5 -i <original>.mp4 -an -vf "fps=24,scale=1280:720:flags=lanczos" \
       -c:v libx264 -preset slow -crf 26 -profile:v high -pix_fmt yuv420p \
       -g 48 -keyint_min 24 -sc_threshold 0 -movflags +faststart <shipped>.mp4
```

1280×720 H.264 High, 24 fps, silent (audio track stripped — the hero is muted by design), keyframe
every 2 s, `+faststart` for progressive playback. Shipped sizes: 638 KB, 770 KB, 693 KB, 676 KB —
2.78 MB for the whole 26 s reel, and no clip exceeds the 30 s ceiling.

`wwwroot/images/home-hero-poster.webp` (1280×720, 33 KB) is a still frame from `01-ai.mp4` at 3.6 s,
used as the hero poster: it is what reduced-motion visitors, small screens and pre-playback see.

## Rejected candidates (recorded so they are not re-used by mistake)

| Item | Reason |
|---|---|
| Mixkit #46680 "Brainstorming over the meeting table" | Restricted License (personal use only) |
| Mixkit #22961 "A man is presenting a business report" | Restricted License (personal use only) |

## Retired

`wwwroot/videos/onyx-home-hero.mp4` (the earlier single generated brand film, 1280×720, 4.96 s) was
replaced by the reel and deleted from the site.

`wwwroot/videos/hero/` (four Mixkit clips: `01-ai.mp4`, `02-code.mp4`, `03-business.mp4`,
`04-accounts.mp4`), `wwwroot/images/home-hero-poster.webp` and `wwwroot/js/hero-reel.js` were removed
together on owner decision of 2026-10-14. The hero is now a centred copy block on the decorative colour
background; the licence notes for the four clips are kept in this file's history for reference.
