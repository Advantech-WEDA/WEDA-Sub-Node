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
| **Test run** | 2026-09-09 |
| **Report schema** | v1.1 — see [Report schema](#report-schema) |

## Results

Gates: **B**uild · **P**ush · **S**tandalone start (E2E-EXD-2.3) · **D**eploy · then the four
function checks. `—` = not applicable, `⏭️` = not executed.

| # | Example | Verdict | B | P | S | D | 4.1 | 4.2 | 4.3 | 4.4 | External dependency |
|---|---|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|---|
| 01 | [`feature-transform-pipeline`](01-feature-transform-pipeline.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | none — embeds its simulator |
| 02 | [`feature-custom-commands`](02-feature-custom-commands.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | none — embeds its simulator |
| 03 | [`http-air-quality`](03-http-air-quality.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | internet (MOENV API) |
| 04 | [`http-stock-quotes`](04-http-stock-quotes.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | internet (TWSE) |
| 05 | [`feature-aggregation`](05-feature-aggregation.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | none — embeds two simulators |
| 06 | [`opcua-basic`](06-opcua-basic.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | none — embeds its simulator |
| 07 | [`modbus-wise4012`](07-modbus-wise4012.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | real WISE-4012 |
| 08 | [`modbus-wise4012-builder`](08-modbus-wise4012-builder.md) | ⚠️ PARTIAL | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ | real WISE-4012 (unreachable at test time) |
| 09 | [`mqtt-image-chunked`](09-mqtt-image-chunked.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | MQTT broker (bundled) |
| 10 | [`mqtt-isensing-wise4012`](10-mqtt-isensing-wise4012.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | MQTT broker (bundled) |
| 11 | [`vision-object-detection`](11-vision-object-detection.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | MQTT broker (bundled) |
| 12 | [`daq-proxy`](12-daq-proxy.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | pairs with `daq-collector` |
| 13 | [`cfx-endpoint`](13-cfx-endpoint.md) | ✅ PASS | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | CFX bridge (bundled RabbitMQ) |
| 14 | [`daq-collector`](14-daq-collector.md) | ❌ BLOCKED | ✅ | ✅ | — | ✅ | ✅ | ❌ | ❌ | ❌ | **real DAQ modules — absent** |
| 15 | [`system-agent`](15-system-agent.md) | ⏭️ SKIP | ✅ | ✅ | — | ⏭️ | ⏭️ | ⏭️ | ⏭️ | ⏭️ | **a device of its own** |

**13 PASS · 1 PARTIAL · 1 BLOCKED · 1 SKIP** — and a SKIP is not a pass.

> `modbus-wise4012-builder` is counted separately from the 13: its SubNode side is healthy
> (4.1–4.3 all green, 0 errors) and only its Modbus target was unreachable during the run.

## How to read a verdict

| Verdict | Meaning | In terms of the checks |
|---|---|---|
| **PASS** | Container running with 0 restarts, NATS connected, registration accepted, telemetry flowing | B·P·S·D and 4.1–4.4 all ✅ |
| **PARTIAL** | SubNode reached the cloud, but its field device or upstream was unavailable | 4.1–4.3 ✅, 4.4 ⚠️ |
| **BLOCKED** | Deploys and runs; cannot function without hardware this device lacks | 4.1 ✅, later checks not reached |
| **SKIP** | Not executed, with the reason recorded. **Never counts as a pass** | Function checks ⏭️ |

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
| 2 | **Gate results** | The eight checks, one row each: gate · step id · check · result · **the evidence for that specific check** |
| 3 | **Evidence log** | Verbatim device log lines, each tagged with the check it proves |
| 4 | **Defects** | Table of id · severity · status · what the defect did *on this example*, then an **Analysis** paragraph |
| 5 | **Not verified in this run** | Every unproven check, its reason, and what would unblock it |
| 6 | **Re-running this test** | Build → deploy-verify → confirm on device |
| 7 | **Change record** | Run, PR, harness, schema version |

Rules the schema enforces:

1. **No collapsed function gate.** 4.1, 4.2, 4.3 and 4.4 each get their own row and their own
   evidence. A single "Function ✅" row hides exactly the failure mode this suite exists to catch.
2. **Evidence is keyed to a check.** A log line that proves nothing in particular does not belong in
   §3.
3. **§5 always exists.** "Nothing — all eight checks were exercised" is a statement; an absent
   section is not.
4. **Severity and status travel with the defect**, so a reader knows D-08 is still open without
   opening the parent report.

### Known evidence gap in this run

Check **4.2** (SubNode reached *this device's* WedaNode) has verbatim log evidence on only
`modbus-wise4012-builder` and `daq-proxy`, which recorded `nats = 2` counters. Elsewhere it rests on
the `deploy-verify.sh` assertion, marked *harness* in §2 of those pages. The assertion is real, but
the page cannot show it. **Next run: capture the `NATS connection verified` / `NATS service
discovered … WedaNodeId=<deviceId>` lines per example** — this is the one check where D-01 proved a
green result can still be wrong (connected, but to a developer's machine).

## CI status during this work

`Weda.SubNode.Integration.Tests` reported 2/10 failing throughout. That is **D-16** — two tests
hardcoded `nats://localhost:<port>` while Testcontainers publishes on the Docker host, and the CI
agent is itself a container sharing the socket, so `localhost` reached nothing. It predates this
branch by a week and is fixed separately in
[PR #7363](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7363).
**It is unrelated to every example result on these pages.**

Example fixes landed in
[PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361).
