# Per-Example End-to-End Deployment Test Reports

One report per SubNode example, recording what it actually did when built, published to Harbor and
deployed to a real device through the container-management API.

The shared method — gates, step format, fixtures, negative cases, defect register and exit criteria
— lives in **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. These pages
record only per-example results, so read that first.

| | |
|---|---|
| **Device under test** | EPC-R7300A1 `48b02dea8160` — ARM64, Ubuntu 20.04 |
| **Environment** | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **This run** | **2026-09-10** — full rebuild, re-push, redeploy, with the device log captured per example |
| **Previous run** | 2026-09-09 |
| **Report schema** | v1.3 — see [Report schema](#report-schema) |

## Results

Gates: **B**uild · **P**ush · **S**tandalone start (E2E-EXD-2.3) · **D**eploy · then the four
function checks. `—` = not applicable, `⏭️` = not executed.

| # | Example | Verdict | B | P | S | D | 4.1 | 4.2 | 4.3 | 4.4 | 4.5 | External dependency |
|---|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|---|
| 01 | [`feature-transform-pipeline`](01-feature-transform-pipeline.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | none — embeds its simulator |
| 02 | [`feature-custom-commands`](02-feature-custom-commands.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | none — embeds its simulator |
| 03 | [`http-air-quality`](03-http-air-quality.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | internet (MOENV API) |
| 04 | [`http-stock-quotes`](04-http-stock-quotes.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | internet (TWSE) |
| 05 | [`feature-aggregation`](05-feature-aggregation.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | none — embeds two simulators |
| 06 | [`opcua-basic`](06-opcua-basic.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | none — embeds its simulator |
| 07 | [`modbus-wise4012`](07-modbus-wise4012.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ | real WISE-4012 — **refusing connections** |
| 08 | [`modbus-wise4012-builder`](08-modbus-wise4012-builder.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ | real WISE-4012 — **refusing connections** |
| 09 | [`mqtt-image-chunked`](09-mqtt-image-chunked.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | MQTT broker (bundled) |
| 10 | [`mqtt-isensing-wise4012`](10-mqtt-isensing-wise4012.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | MQTT broker (bundled) |
| 11 | [`vision-object-detection`](11-vision-object-detection.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | MQTT broker (bundled) |
| 12 | [`daq-proxy`](12-daq-proxy.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | pairs with `daq-collector` |
| 13 | [`cfx-endpoint`](13-cfx-endpoint.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ | CFX bridge (bundled RabbitMQ) |
| 14 | [`daq-collector`](14-daq-collector.md) | ❌ BLOCKED | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | ❌ | **real DAQ modules — absent** |
| 15 | [`system-agent`](15-system-agent.md) | ⏭️ SKIP | ✅ | ✅ | — | ⏭️ | ⏭️ | ⏭️ | ⏭️ | ⏭️ | ⏭️ | **a device of its own** |

**6 PASS · 7 PARTIAL · 1 BLOCKED · 1 SKIP = 15** — and a SKIP is not a pass.

> **Count correction.** The previous edition of this page claimed *13 PASS · 1 PARTIAL · 1 BLOCKED ·
> 1 SKIP*, which totals 16 for a 15-example suite; the table underneath it showed 12 PASS. The
> verdict column is authoritative and the totals above are counted from it.

## What changed with the 4.4 / 4.5 split

Scoring the framework heartbeat apart from the example's own output moved five examples out of
PASS. Each of them reached the platform and registered; none was ever seen doing its job.

| Example | Was | Now | What 4.4 was standing in for |
|---|---|---|---|
| `mqtt-image-chunked` | ✅ PASS | ⚠️ PARTIAL | subscribed to the image topic; no frame ever arrived |
| `mqtt-isensing-wise4012` | ✅ PASS | ⚠️ PARTIAL | subscribed to the iSensing topics; nothing published to them |
| `vision-object-detection` | ✅ PASS | ⚠️ PARTIAL | subscribed to the vision topics; no CV container running |
| `daq-proxy` | ✅ PASS | ⚠️ PARTIAL | the heartbeat alone; its upstream was blocked |
| `cfx-endpoint` | ✅ PASS | ⚠️ PARTIAL | subscribed for 17 sensors; 17 is the subscription, not receipt |

`modbus-wise4012`, `modbus-wise4012-builder` and `daq-collector` also lose their 4.4 mark: the
excerpt kept for those reports carries no heartbeat line, which is not the same as the heartbeat
being absent. Their §5 says so and names what would settle it.

## What changed since 2026-09-09

| Example | Was | Now | Why |
|---|---|---|---|
| `modbus-wise4012` | ✅ PASS | ⚠️ PARTIAL | **Fixture, not code.** The WISE-4012 now answers `Connection refused` on `:502`. SubNode side still proves 4.1–4.3 |
| `daq-collector` | ❌ BLOCKED, 4.2/4.3 "not reached" | ❌ BLOCKED, 4.2/4.3 ✅ | **The old report was wrong.** It reaches NATS and registers, *then* fails the hardware probe |
| `daq-collector`, `system-agent` | Build ✅ | Build ✅ *(after a fix)* | **D-17** broke both builds outright — see below |
| every example | 4.2 on a harness assertion | 4.2 on a verbatim log line | The evidence gap the v1.1 schema exposed is closed |

### D-17 — an exact `apt` pin on a security package rots

`daq-collector` and `system-agent` both pinned `gpgv=2.4.4-2ubuntu17.4` for a CVE fix. Ubuntu has
since shipped `2.4.4-2ubuntu17.6` and **dropped 17.4 from the archive**, so both builds failed:

```
E: Version '2.4.4-2ubuntu17.4' for 'gpgv' was not found
```

Nothing in the repo changed — the archive did. An exact `=` pin on a security-updated package is
self-destructing. It fails *closed*, which is the one mercy: the build dies rather than silently
installing something older. The fix takes the archive candidate and asserts the floor, because
`apt-get install` has no `>=` operator:

```dockerfile
apt-get install -y --no-install-recommends gpgv && \
dpkg --compare-versions "$(dpkg-query -W -f='${Version}' gpgv)" ge 2.4.4-2ubuntu17.4
```

That is strictly stronger than the original pin: it always installs the current security build, and
it fails the build if that build is somehow below the CVE floor.

### Observation — the heartbeat warning overstates its consequence

11 of 14 deployed examples log:

> `SubNode heartbeat is DISABLED: no enabled sensor named 'hb' was found. This SubNode publishes no
> liveness signal and the platform will read it as Disconnected.`

The org listing does **not** bear that out: `opcua-basic` is in the disabled set and reported
`Connected` while running, whereas `modbus-wise4012` — which *has* an `hb` sensor — reported
`Disconnected` once it was torn down. Connectivity tracks whether the SubNode is currently up, not
the presence of `hb`. Recorded as a follow-up on the warning's wording, **not** filed as a defect.

## How to read a verdict

| Verdict | Meaning | In terms of the checks |
|---|---|---|
| **PASS** | Container running with 0 restarts, NATS connected, registration accepted, telemetry flowing | B·P·S·D and 4.1–4.4 all ✅ |
| **PARTIAL** | SubNode reached the cloud, but its field device or upstream was unavailable **at test time** | 4.1–4.3 ✅, 4.4 ❌ |
| **BLOCKED** | Deploys, runs and registers; cannot function without hardware this device lacks | 4.1–4.3 ✅, 4.4 ❌ — same shape as PARTIAL, but the gap is not transient |
| **SKIP** | Not executed, with the reason recorded. **Never counts as a pass** | Function checks ⏭️ |

PARTIAL and BLOCKED share a check pattern and differ in prognosis: a PARTIAL clears when someone
powers a device back on; a BLOCKED needs different hardware.

A container reported `running` proves only that the process has not exited. Three of this suite's
defects produced a `running` container that was doing nothing at all — which is why **4.1 is a
separate check from 4.2–4.4** in every report, rather than one "Function" row.

## Report schema

All 15 pages use the same sections in the same order, so any two are diffable and a reader knows
where to look without re-reading the page:

| § | Section | Contains |
|---|---|---|
| — | Verdict banner | Verdict · test run date · device · environment |
| 1 | **Subject under test** | Example, compose service, image, transport, external dependency, host endpoints, **required deployment overrides**, SubNode name, registered deviceId |
| 2 | **Gate results** | The nine checks, one row each: gate · step id · check · result · **the evidence for that specific check**. 4.4 is the framework heartbeat, 4.5 is the example's own job |
| 3 | **Evidence log** | Verbatim device log lines, each tagged with the check it proves |
| 4 | **Defects** | Table of id · severity · status · what the defect did *on this example*, then an **Analysis** paragraph |
| 5 | **Not verified in this run** | Every unproven check, its reason, and what would unblock it |
| 6 | **Re-running this test** | Build → deploy-verify → confirm on device |
| 7 | **Change record** | This run, previous run, PRs, harness, schema version |

Rules the schema enforces:

1. **No collapsed function gate.** 4.1, 4.2, 4.3 and 4.4 each get their own row and their own
   evidence. A single "Function ✅" row hides exactly the failure mode this suite exists to catch —
   and this run proved the point twice, on `modbus-wise4012` and `daq-collector`.
2. **Evidence is keyed to a check.** A log line that proves nothing in particular does not belong in
   §3.
3. **§5 always exists.** "Nothing — all nine checks were exercised" is a statement; an absent
   section is not.
4. **Severity and status travel with the defect**, so a reader knows D-08 is still open without
   opening the parent report.
5. **Totals are counted from the verdict column**, never asserted separately.

## CI status

`Weda.SubNode.Integration.Tests` reported 2/10 failing throughout the previous run. That is
**D-16** — two tests hardcoded `nats://localhost:<port>` while Testcontainers publishes on the
Docker host, and the CI agent is itself a container sharing the socket, so `localhost` reached
nothing. Fixed separately in
[PR #7363](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7363).
**It is unrelated to every example result on these pages.**

Example fixes landed in
[PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361);
the report schema, this run's evidence and the D-17 fix in
[PR !7396](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7396).
