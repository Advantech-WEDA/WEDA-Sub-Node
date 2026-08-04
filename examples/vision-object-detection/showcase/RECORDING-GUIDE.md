# Recording guide — real chat screen → 80s journey video

Screen-record your **actual Claude chat window**, then drop in the caption track
(`journey-captions.srt`) and (optionally) append the payoff dashboard. Result: an
~80-second video that shows the *real* messages, not a reconstruction.

---

## 1. Set up the screen (before recording)

- **Window:** make the chat a clean 16:9 window. Hide the sidebar / other panels.
- **Theme:** dark theme looks best and matches the caption/outro styling.
- **Zoom:** browser zoom so ~1–2 message exchanges fill the height (roughly 110–125%).
- **Scrollbar/cursor:** keep the scrollbar visible; move the cursor deliberately, slowly.
- **Recorder:** macOS `⇧⌘5` (record selected window) · Windows `Win+Alt+R` · or **OBS** (cleanest, lets you crop to 16:9 and set 1920×1080 / 30 fps).

## 2. Scroll choreography (the shot list)

Start at the top of the conversation. Hold on each exchange, then scroll smoothly to the
next. Timecodes match `journey-captions.srt` — aim to land on each message at its cue.

| Time | Scroll to / hold on | On-screen (your real chat) | Caption (from .srt) |
|------|---------------------|----------------------------|---------------------|
| 0:00 | Top of chat (or a title slide) | conversation title | *Building a WEDA SubNode with Claude* |
| 0:05 | Msg 1 | **"load the skills in the project"** → skill loaded | It starts with one prompt |
| 0:13 | Msg 2 | **"check the api …"** → MQTT contract parsed | Read the OD container's telemetry contract |
| 0:22 | Msg 3 | **"create a container sub node…"** → build + tests | SubNode built · 10/10 tests green |
| 0:33 | Msg 4–5 | **"buid a container image"** / **arm64 push** | Multi-arch image → Harbor |
| 0:42 | Msg 6 | **/weda-login** → JWT minted | Authenticated to WEDA Core |
| 0:50 | Msg 7 | **/weda-container-mgmt deploy…** → running | Deployed to Jetson · running |
| 1:00 | Msg 8 | **"show me the telemetry data of the detection"** → live bottles | Live detections → WEDA telemetry |
| 1:10 | Outro | payoff dashboard (see §4) | Edge-AI OD → WEDA Core. One session. |

> ⚠️ **Before you record:** scroll to your `/weda-login` message and **blur or crop out the
> password** (it was typed in that prompt). Everything else is safe to show.

## 3. Add the captions

Import `journey-captions.srt` as a subtitle track — works in **CapCut, DaVinci Resolve,
Premiere, Final Cut**, or just upload the video + .srt to **YouTube** (auto-syncs). Style
them as lower-thirds; tweak the cue times if your scrolling runs faster/slower.

## 4. Outro (optional but recommended)

Append a live-dashboard finale so the video ends on the result, not mid-scroll:

- Open **`journey-to-payoff.html`** (or the artifact) and screen-record just its **payoff
  act** (the last ~25s — pipeline + ticking sensor tiles), OR
- Use **`od-weda-showcase.html`** and record its final dashboard scene.

Cut it in at ~1:10 for the last 10–12 s.

## 5. Music / polish

- Minimal ambient-tech bed; cut scroll transitions on the beat.
- Keep transitions quick (crossfade ≤ 0.3 s). Export **1920×1080, 30 fps, H.264**.

---

### Files in this folder
| File | Use |
|------|-----|
| `RECORDING-GUIDE.md` | this guide |
| `journey-captions.srt` | caption track to import |
| `journey-to-payoff.html` | reconstruction video + payoff outro source |
| `od-weda-showcase.html` | product demo (alt outro source) |
| `journey-session-replay.html` | chat-replay reconstruction |
