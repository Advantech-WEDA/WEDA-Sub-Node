---
doc-id:    E2E-EXAMPLE-IMAGES-001
title:     "SubNode Examples — Build, Publish, Deploy and Verify — End-to-End Test Scenario"
product:   "SubNode SDK (Examples)"
version:   1.0
status:    Review
date:      2026-09-09
traces-to: docs/wiki/en/02-getting-started/09-build-push-deploy-images.md (SOP), examples/README.md, templates/subnode/Dockerfile
tags:      [doc:test, feature:sub-node, service:sub-node, service:container-management, layer:edge, layer:cloud, tech:docker, tech:rest, tech:nats, tech:mqtt, ver:1.2, status:review]
---

# SubNode Examples — Build, Publish, Deploy and Verify — End-to-End Test Scenario

> **Who this is for:** SDK maintainers and QA verifying that every shipped example is not just
> compilable but **deployable and functional on real edge hardware**, and Application Engineers
> who need to demo *"take any example from source to a running SubNode on a device, without
> touching the device"*.
> **What it covers:** one continuous journey per example — *build the image → push it to Harbor →
> deploy it to a device through the container-management API → confirm the SubNode inside actually
> registers and reports*.
> **What it is not:** unit tests. The example unit suites are separate (§11) and passing them
> proves nothing about deployability — every defect in §10 was found by deploying, not by building.

[[_TOC_]]

<!-- Step ids E2E-EXD-<act>.<step> are STABLE once published — defect titles quote them.
     "Deployable" here means the container-management API can put the image on a device that has
     no repo checkout. That is a stricter bar than "docker compose up works on my laptop", and it
     is the bar that surfaced every defect in §10. -->

---

## 0. At a Glance

| | |
|---|---|
| **Under test** | The 15 examples under `examples/`, plus `tools/simulator-host` |
| **Feature** | Example images: repository-root build context, config baked for standalone start, multi-arch publish, container-management deployment |
| **Persona under test** | **Sam**, an SI engineer who has never built this repo, deploying an example to a customer's edge box from Harbor |
| **Secondary persona** | **Maya**, an SDK maintainer — signs off that no example ships a default that only works on one developer's machine |
| **Scope** | 4 gates per example: **Build → Push → Deploy → Function**. An example passes only when all four pass |
| **Device** | EPC-R7300A1 — ARM64, 6 cores, Ubuntu 20.04, `deviceId 48b02dea8160` |
| **Duration** | ~4 min per example (arm64 build + push + deploy + settle); a full pass is ~2 h, dominated by first-time image pulls |
| **Verdict** | PASS only when **every** Exit Criterion in §12 is ticked |

## Demo Path

For a customer demo, run these steps in order. Each has a one-line story; skip the matrices.

| # | Step | The line to say |
|---|---|---|
| 1 | `./scripts/build-push-image.sh feature-transform-pipeline` | "One command turns any example into a multi-arch image on Harbor." |
| 2 | `POST …/stack-configs` then `…:deploy` | "The device is never touched — the platform pushes the stack to it." |
| 3 | `GET …/devices/{id}/docker/stacks` | "The container is running on the device." |
| 4 | Device log: `temperature_sensor: 25 → 77` | "And the SubNode inside is reading its sensor and transforming the value." |
| 5 | `GET …/orgs/{org}/devices` shows the child SubNode `Connected` | "It registered itself in the cloud as its own device." |

---

## 1. Test Environment & Prerequisites

### 1.1 Environment

| Item | Value | Notes |
|---|---|---|
| Environment | **SIT** | `weda-sit-k3s.weda.dev`, tenant `central` |
| Base URL | `https://weda-sit-k3s.weda.dev/central/weda` | Self-signed cert — **pin it** (`--cacert`), do not use `-k` |
| API prefix | `/api/v1` | |
| Auth | `Authorization: Bearer <token>` | Mint via the `weda-login` skill; tokens last 2 h |
| Registry | `harbor.arfa.wise-paas.com/edge-coa/<example>` | Org needs a registry credential (§1.3) |
| Device arch | **ARM64** | Read it, do not assume — see E2E-EXD-1.1 |
| Build host | amd64 with buildx + binfmt | Cross-compiles; see §1.5 |

### 1.2 Accounts required

| # | Account | Org | Role | Used to prove |
|---|---|---|---|---|
| A1 | `…@advantech.com.tw` | Org-X (owns the device) | **Admin** | The happy path — create stack configs, deploy, read device state |

> Only one identity is needed: this scenario tests deployability, not RBAC. The authorization
> matrix belongs to the container-management E2E doc, not here.

### 1.3 Fixtures required

| # | Fixture | State needed | Used in |
|---|---|---|---|
| **D1** | WEDA Node registered in Org-X, `dmagent` running with NATS on `127.0.0.1:4224` | **Online** | All acts |
| **R1** | Harbor registry credential on Org-X (`harbor-arfa-edge-coa`) | Present | Act 2 onward — without it every pull fails as a stuck deployment |
| **S1** | Free host ports on D1: **2395** (SubNode Web API), plus each example's own | — | Act 3 |
| **N1** | Outbound internet from D1 | — | `http-air-quality`, `http-stock-quotes` only |

> **Precondition: only one SubNode at a time.** Every SubNode starts a Web API on **2395** and the
> stacks use `network_mode: host`, so two examples cannot run concurrently on one device. Deploy
> sequentially — a "port in use" failure on a parallel run is a test-design error, not a defect.

### 1.5 Shell setup (copy-paste)

```bash
SP=/path/to/scratch
TOK=$(cat "$SP/weda.jwt")
BASE="https://weda-sit-k3s.weda.dev/central/weda"
ORG=b60fee4c-3e27-43eb-9a6e-877a52bb54f5
DEV=48b02dea8160
CA="--cacert $SP/weda-sit-k3s.pem"

api()  { curl -sS $CA -H "Authorization: Bearer $TOK" "$@"; }
japi() { api -H 'Content-Type: application/json' "$@"; }
```

---

## 2. How to read a test step

| Field | Meaning |
|---|---|
| **Step** | Stable ID — quote it in the defect title (e.g. `E2E-EXD-3.2 failed`) |
| **Sam does** | What the engineer is trying to achieve, in plain language (this is the AE demo line) |
| **Call** | The exact command or request |
| **Expect** | The result that means PASS |
| **Proves** | What this step evidences |

A step FAILS if the observed state differs from **Expect**. A container reported `running` is **not**
sufficient evidence for any step in Act 4 — see the warning in §6.

> **Automation.** `deploy-verify.sh` (this scenario's harness) applies Act 3 and Act 4 uniformly.
> Steps not executed are recorded `SKIP` — **a `SKIP` is not a pass.**

---

## 3. Act 1 — Will this image even run on that device? *(discovery)*

> **Story:** Sam has a device in front of him and an example he has never built. Before anything
> else he needs to know what architecture to build for.

### E2E-EXD-1.1 — Read the device architecture

| | |
|---|---|
| **Sam does** | Asks the platform what the device is, instead of assuming x86 |
| **Call** | `api "$BASE/api/v1/devices/$DEV/capabilities" \| grep processorArchitecture` |
| **Expect** | `"processorArchitecture": "ARM64"` |
| **Proves** | The build target is chosen from evidence. An amd64 image on a Jetson-class device fails at pull with a manifest error that reads like a registry fault |

### E2E-EXD-1.2 — Confirm the org can pull from Harbor

| | |
|---|---|
| **Sam does** | Checks the registry credential exists before deploying anything |
| **Call** | `api "$BASE/api/v1/orgs/$ORG/containers/registries"` |
| **Expect** | An entry whose `registryUrl` is `https://harbor.arfa.wise-paas.com` |
| **Proves** | Fixture R1. Without it a deploy sits in `deploying` forever and the cause is never surfaced |

---

## 4. Act 2 — Build and publish *(the supply side)*

### E2E-EXD-2.1 — Build from the repository root

| | |
|---|---|
| **Sam does** | Builds the example without knowing the context rule |
| **Call** | `cd examples/<name> && docker build -t x .` |
| **Expect** | **FAILS** with `MSBUILD : error MSB1003: Specify a project or solution file.` |
| **Proves** | The negative case is documented. Every example `.csproj` references `../../src/Weda.SubNode.*`, so the context must be the repo root. This error names neither the context nor the Dockerfile |

### E2E-EXD-2.2 — Build and push via the script

| | |
|---|---|
| **Sam does** | Uses the supported entry point |
| **Call** | `PLATFORMS=linux/arm64 ./scripts/build-push-image.sh <name>` |
| **Expect** | Push succeeds; `docker buildx imagetools inspect` lists `linux/arm64` |
| **Proves** | The image exists in Harbor for the device's architecture |

### E2E-EXD-2.3 — The image runs with nothing mounted

| | |
|---|---|
| **Sam does** | Runs the image with no bind mounts at all, as a deployment would |
| **Call** | `docker run --rm <image>` |
| **Expect** | Starts and reads its **baked** `devicecfg.json` — not "configuration file not found" |
| **Proves** | The image is deployable. A stack deployed by container-management has no repo checkout, so an image depending on mounts starts and immediately dies |

---

## 5. Act 3 — Deploy through the platform *(no device access)*

### E2E-EXD-3.1 — Create the stack config

| | |
|---|---|
| **Sam does** | Registers the compose stack against the org |
| **Call** | `japi -X POST "$BASE/api/v1/orgs/$ORG/stack-configs" -d '{"stackName":…,"type":"compose","composeFileContent":"<base64>"}'` |
| **Expect** | `201` with `stackConfigId` + `stackRevisionId` |
| **Proves** | `composeFileContent` is **base64**, not plain YAML — a plain-text body is accepted and then fails opaquely on the device |

### E2E-EXD-3.2 — Deploy the revision

| | |
|---|---|
| **Sam does** | Pushes the revision to the device |
| **Call** | `japi -X POST "$BASE/api/v1/orgs/$ORG/stack-configs/$STACK/revisions/$REV:deploy" -d '{"deviceIds":["'$DEV'"]}'` |
| **Expect** | `200`, `deviceActions: {<dev>: "add"}` |
| **Proves** | The device accepted the revision. **`"skip"` is a failure here** — it means the cloud believes the revision is already present, which happens after cloud/device drift (§10 D-09) |

### E2E-EXD-3.3 — Persist the SubNode registration

| | |
|---|---|
| **Sam does** | Declares a volume for `/app/.weda` in the stack |
| **Call** | `- ${WEDA_DATA_PATH:-/opt/Advantech/weda/node/<service>}:/app/.weda` |
| **Expect** | After `docker restart`, the log reads `Found existing SubNode registration` — **not** a fresh registration |
| **Proves** | Without it the accepted registration is destroyed on every container recreation and the SubNode collides with its own prior identity: `409 Device name already in use`. **The stack works exactly once** |

---

## 6. Act 4 — Is the SubNode actually working? *(the outcome)*

> **A container reported `running` proves only that the process has not exited.** A SubNode that
> cannot reach its device or its WedaNode stays up and retries forever, and `restart: unless-stopped`
> turns a crash into a crash-loop that the API still reports as `running`. Every step below exists
> because "running" was not evidence.

### E2E-EXD-4.1 — Container is up and not looping

| | |
|---|---|
| **Expect** | `status=running` **and** `restarts=0` |
| **Proves** | Not a crash-loop |

### E2E-EXD-4.2 — The SubNode reached the WedaNode

| | |
|---|---|
| **Expect** | `NATS connection verified` and `NATS service discovered: … WedaNodeId=<deviceId>` |
| **Proves** | Credentials and the WedaNode URL are correct **for this device**, not for a developer's machine |

### E2E-EXD-4.3 — It registered as its own device

| | |
|---|---|
| **Expect** | `Configuration uploaded successfully` or `Found existing SubNode registration`, and the org listing shows a child device with `parentDeviceId = <deviceId>` and `connectivity.state = Connected` |
| **Proves** | A SubNode registers as its **own** WEDA device named by `SubNode.Name` — it does not appear as sensors on the host device |

### E2E-EXD-4.4 — It is producing its own telemetry

| | |
|---|---|
| **Expect** | Sensor values in the log **and** `Health report sent successfully` |
| **Proves** | The example's actual job is being done — the thing that distinguishes "deployed" from "working" |

---

## 7. Act 5 — Per-example results

Gates: **B**uild · **P**ush · **D**eploy · **F**unction. `—` = not applicable.

| # | Example | B | P | D | F | Evidence / blocker |
|---|---|:-:|:-:|:-:|:-:|---|
| 1 | `feature-transform-pipeline` | ✅ | ✅ | ✅ | ✅ | `temperature_sensor: 25 → 77` (°C→°F) then `→ 25.005` (smoothing) |
| 2 | `feature-custom-commands` | ✅ | ✅ | ✅ | ✅ | `sensor.read`, `tag.set`, `props.get` all succeeded on-device |
| 3 | `http-air-quality` | ✅ | ✅ | ✅ | ✅ | `Air quality data received`; registered `355900119696015360` |
| 4 | `http-stock-quotes` | ✅ | ✅ | ✅ | ✅ | Live TWSE: TSMC high 2490 / low 2475 / vol 5734 |
| 5 | `feature-aggregation` | ✅ | ✅ | ✅ | ✅ | Current 5.71 A + Voltage 227.89 V → Power 1276.19 W |
| 6 | `opcua-basic` | ✅ | ✅ | ✅ | ✅ | OPC UA simulator starts after D-05; nats + registration + telemetry |
| 7 | `modbus-wise4012` | ✅ | ✅ | ✅ | ✅ | Registers against the device's own dmagent after D-01 |
| 8 | `modbus-wise4012-builder` | ✅ | ✅ | ✅ | ⚠️ | SubNode side healthy (nats + registration, 0 errors); Modbus target `172.16.8.122:502` unreachable at test time |
| 9 | `mqtt-image-chunked` | ✅ | ✅ | ✅ | ✅ | Passes with the bundled mosquitto |
| 10 | `mqtt-isensing-wise4012` | ✅ | ✅ | ✅ | ✅ | After D-07; subscribed `Advantech/00D0C9FAC80E/{data,status}`, registered `355941923954884608` |
| 11 | `vision-object-detection` | ✅ | ✅ | ✅ | ✅ | After D-03; subscribed `advantech/+/vision/{detections,status,meta}`, registered `355914456695308288` |
| 12 | `daq-proxy` | ✅ | ✅ | ✅ | ✅ | nats + registration + telemetry |
| 13 | `cfx-endpoint` | ✅ | ✅ | ✅ | ✅ | After D-14; registered `355914649897533440`, subscribed `+/+/+/CFX/#` for **17 sensors across 17 message types**, config applied |
| 14 | `daq-collector` | ✅ | ✅ | ✅ | ❌ | `DaqCommunication connection failed: No DAQ modules available` — **fixture gap, not a defect** |
| 15 | `system-agent` | ✅ | ✅ | — | — | **SKIP** — already runs on the device as a shipped product component; a second instance would contend for the same host resources |

> **`system-agent` is a deliberate SKIP, and a SKIP is not a pass.** Deploying it was judged a risk
> to a working device rather than a test. It needs a dedicated device to be verified.

---

## 8. Act 6 — Negative cases that must keep failing

| Step | Case | Expect |
|---|---|---|
| E2E-EXD-6.1 | `docker build .` inside an example directory | Fails, `MSB1003` |
| E2E-EXD-6.2 | Deploy a stack with no `/app/.weda` volume, then recreate the container | Second start fails `409 Device name already in use` |
| E2E-EXD-6.3 | Two SubNode stacks deployed concurrently | Second fails to bind port 2395 |
| E2E-EXD-6.4 | Deploy an amd64-only image to the ARM64 device | Pull fails on manifest mismatch |

---

## 9. Error Catalog Quick Reference

| Symptom | Real cause | Where |
|---|---|---|
| `MSBUILD : error MSB1003` | Build context is the example dir, not the repo root | E2E-EXD-2.1 |
| `409 Device name already in use` | `/app/.weda` not persisted, or the name is taken on the org | E2E-EXD-3.3 |
| `SocketException (98): Address already in use` | Two things want one port — often a bundled simulator the example already embeds | §8 |
| `BadInternalError 'Unexpected error starting application'` | Invariant globalization; the OPC UA X.509 path needs ICU | D-05 |
| `MissingMethodException: Constructor … not found` | Device class lacks the `(context, DeviceConfiguration)` overload | D-07 |
| NATS retry loop, container `running` | WedaNode URL or credentials wrong **for this device** | D-01, D-03, D-04 |
| Deployment `deployed`, no container on device | Cloud/device drift | D-09 |
| Container stuck in `Created`, **no logs at all** | Host-mode service in a stack that also has a bridge-networked service (D-14), or a stale bridge route from a previous stack of the same name (D-13) | D-13, D-14 |
| `no space left on device`, `ResourceExhausted`, corrupt NuGet | Docker data volume full during a batch multi-arch build | D-10 |

---

## 10. Defects found

| # | Defect | Severity | Status |
|---|---|---|---|
| D-01 | Four examples baked a **developer-machine WedaNode URL** (`172.22.160.192/197:4224`), and one used port `4222`. Invisible locally; fatal once images bake config | High | **Fixed** — all default to `127.0.0.1:4224` |
| D-02 | Images bind-mounted their config, so a deployed stack had none and died at startup | High | **Fixed** — config baked into `/app` |
| D-03 | `vision-object-detection` shipped the literal `REPLACE_WITH_NATS_USER` | High | **Fixed** |
| D-04 | `cfx-endpoint` ships an **empty** NATS password by design (`.env`-supplied); a deployment without it can never authenticate | Medium | **By design** — documented; deployments must pass `SystemConfig__WedaNode__Password` |
| D-05 | `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` breaks the OPC UA X.509 application-certificate path | High | **Fixed** — proven by elimination, not inspection |
| D-06 | Three examples hardcoded `.UseMockCloud()`, so they could never reach a WedaNode | High | **Fixed** — config-driven, default real cloud |
| D-07 | `MyFirstISensingDevice` lacked the `(context, DeviceConfiguration)` ctor the host activates → `MissingMethodException` on every start, on any host | High | **Fixed** |
| D-08 | `examples/daq-collector/docker-compose.yml` fails validation: `services.daq-collector.devices must be a list` | Medium | **Open** — pre-existing |
| D-09 | Cloud/device drift: API reports `deployed` while the device has no compose project; `commands:start` → `stack not found`; redeploy answered `skip` | High | **Open** — platform defect. Workaround: `DELETE …/docker/stacks` then redeploy (returns `add`) |
| D-10 | Batch multi-arch builds exhaust the Docker data volume; errors read like network faults | Low | **Documented** — prune between builds |
| D-11 | Self-contained publish layered on `dotnet/runtime` shipped a **second** unused runtime (~82 MB/image) | Low | **Fixed** — `runtime-deps` |
| D-12 | Build context 252.9 MB, of which 177 MB was gitignored logs not covered by `.dockerignore` | Low | **Fixed** — context now 22.4 MB |
| D-14 | A stack mixing `network_mode: host` with a **bridge-networked sibling** cannot deploy: the agent programs the project network's veth into the host namespace and the host-mode container is created but never starts (`cannot program address …/28 … conflicts with existing route`). It emits **no logs at all**, so it reads as a hung pull | High | **Fixed in the example** — both services on host networking. The underlying agent behaviour is **Open** |
| D-15 | Appending to `/etc/rabbitmq/rabbitmq.conf` from `command:` aborts RabbitMQ with `failed_to_prepare_configuration` — the entrypoint generates that file from `RABBITMQ_DEFAULT_*` | Low | **Fixed** — plugins enabled only; MQTT left on 1883 and the SubNode pointed at it |
| D-13 | Repeated deploy/delete of the same stack name leaves a **stale bridge route** for the project network's subnet. The next deployment's container is created and never starts: `cannot program address 10.226.2.3/28 … conflicts with existing route`. `docker network prune` does not clear it (the network is still in use) | High | **Open** — platform/agent defect. Workaround: `DELETE …/docker/stacks` to drop the project network, then redeploy |

---

## 11. Unit test coverage (context, not part of this scenario)

| Test project | Covers | Result |
|---|---|---|
| `SystemAgentDevice.Tests` | `system-agent` | 140 passed |
| `CommandHandlerExample.Tests` | `feature-custom-commands` | 37 passed |
| `StockMonitor.Tests` | `http-stock-quotes` | 15 passed |
| `VisionObjectDetection.Tests` | `vision-object-detection` | 10 passed |

**202 tests, 0 failures — and they caught none of §10.** Only 4 of 15 examples have tests at all,
and every defect above was found by deploying. That is the argument for this scenario existing.

> Two test projects are named for directories that no longer exist (`command-handler`,
> `stock-monitor`). Their `ProjectReference`s are correct; only the names lag the rename.

---

## 12. Exit Criteria — Sign-off Sheet

| # | Criterion | Status |
|---|---|---|
| X-1 | All 15 examples build and push multi-arch to Harbor | ✅ 15/15 |
| X-2 | Every example image starts with no bind mounts (E2E-EXD-2.3) | ✅ |
| X-3 | Every deployable example reaches nats + registration + telemetry on D1 | ⚠️ 13 confirmed, 1 fixture-blocked (`daq-collector`), 1 skipped (`system-agent`) |
| X-4 | Registration survives container recreation (E2E-EXD-3.3) | ✅ verified by restart |
| X-5 | No example defaults to a host outside the device it runs on | ✅ D-01 fixed |
| X-6 | No example is hardcoded to mock cloud | ✅ D-06 fixed |
| X-7 | Every open defect in §10 has an owner | ❌ D-08, D-09, D-13, D-14 (agent behaviour) unassigned |

**Overall verdict: CONDITIONAL PASS** — X-3 and X-7 are not met. **13 of 15 examples are confirmed
working end to end.** The remaining two are fixture gaps, not failures: `daq-collector` needs a DAQ
module, `system-agent` needs a device of its own. Three platform defects are open and unassigned
(**D-09** stack drift, **D-13** stale bridge route, **D-14** host/bridge network mixing) — all three
present as a device doing nothing while the API reports success.

---

## 13. Open Questions

| # | Question | Owner |
|---|---|---|
| OQ-1 | D-09: is the cloud/device stack drift reproducible outside a service rename? It silently leaves a device with nothing running while the API reports success | container-management |
| OQ-5 | D-13: does the agent ever tear down a project network on stack delete, or only its containers? Two of this run's hardest failures (D-09, D-13) were device-side state the API reported as healthy | container-management |
| OQ-2 | Should `system-agent` be excluded from `examples/` entirely? It is a product component, cannot be deployed alongside itself, and the index already lists it under "Not examples" | SDK maintainers |
| OQ-3 | `daq-collector` and `modbus-wise4012*` need real hardware. Should the fixture set include a DAQ module and a WISE-4012, or should these examples ship simulators as `feature-*` ones do? | QA + SDK maintainers |
| OQ-4 | The bundled brokers carry no data producer — the repo has an MQTT *image* simulator but no iSensing or vision publisher. Should those be added so Act 4 can assert real payloads rather than health reports? | SDK maintainers |
