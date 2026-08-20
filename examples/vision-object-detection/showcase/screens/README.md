# Drop your real Claude chat screenshots here

Take one screenshot per exchange below, save it in **this folder** with the exact filename,
then tell me "screenshots are in" — I'll embed them (as data-URIs) into a timed screenshot
video with captions and the live payoff dashboard.

## Capture list (8 shots)

| Filename | Screenshot this message/exchange |
|----------|----------------------------------|
| `01-load-skills.png` | "load the skills in the project" → skill loaded |
| `02-check-api.png` | "check the api …" → MQTT contract parsed |
| `03-create-subnode.png` | "create a container sub node…" → built + 10/10 tests |
| `04-build-image.png` | "buid a container image" → image built |
| `05-arm64-push.png` | "yes, build for arm64 and push…" → multi-arch pushed |
| `06-weda-login.png` | `/weda-login` → JWT minted — **⚠️ blur/crop the password first** |
| `07-deploy.png` | `/weda-container-mgmt deploy…` → status: running |
| `08-telemetry.png` | "show me the telemetry data of the detection" → live bottles |

## Tips for clean shots
- **Dark theme**, one exchange per shot, consistent width (crop each to the same aspect if you can).
- PNG preferred (JPG fine). Any resolution — I'll scale. Landscape reads best in a 16:9 video.
- **`06-weda-login.png`: redact the password** (black box / blur) before saving — it was typed in that prompt.
- Fewer/more shots are fine — just keep the numeric prefix so ordering is clear.

Once the files are here, I'll build `journey-screenshots.html` — each real shot revealed on the
timeline (subtle Ken-Burns pan), captions synced, ending on the WEDA Core dashboard.
