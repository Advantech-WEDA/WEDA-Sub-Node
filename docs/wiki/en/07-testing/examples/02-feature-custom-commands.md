---
doc-id:    E2E-EXD-FEATURE_CUSTOM_COMMANDS
title:     "feature-custom-commands — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# feature-custom-commands — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-10 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/feature-custom-commands`](../../../../../examples/feature-custom-commands/) |
| **Compose service** | `command-handler` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/feature-custom-commands:latest` |
| **Transport** | Modbus TCP (embedded simulator) |
| **External dependency** | None |
| **Host endpoints used** | 2395 (SubNode Web API) |
| **Required deployment overrides** | None |
| **SubNode name** | `CmdHandlerDemo-09090214` |
| **Registered deviceId** | `355899692216745984` |

## 2. Gate results

Four gates, eight checks. **A container reported `running` satisfies check 4.1 and nothing else** —
it is not evidence for 4.2, 4.3 or 4.4. Every row stands on its own observation, quoted from the
device's own log in §3.

| Gate | Step | Check | Result | Evidence |
|---|---|---|:-:|---|
| **Build** | E2E-EXD-2.2 | Builds for `linux/arm64` from the repository root | ✅ | rebuilt for `linux/amd64,linux/arm64` |
| **Push** | E2E-EXD-2.2 | Multi-arch manifest present in Harbor | ✅ | multi-arch manifest re-published to Harbor |
| **Push** | E2E-EXD-2.3 | Image runs with nothing mounted | ✅ | config baked into `/app`; no bind mounts required |
| **Deploy** | E2E-EXD-3.2 | Revision accepted and applied by the device | ✅ | `deviceActions: add` |
| **Function** | E2E-EXD-4.1 | Container up and not restart-looping | ✅ | `status=running restarts=0` |
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | `NATS connection verified - RTT: 0.3174ms` |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `Found existing SubNode registration: DeviceName=CmdHandlerDemo-09090214, DeviceId=355899692216745984` |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ✅ | sensor values + `Reporting health` — see §3 |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.

## 3. Evidence log

Verbatim from `docker logs weda-command-handler` on the device, tagged with the check each line proves.
Captured 2026-09-10; 101 lines total.

```text
[4.1] [00:59:18 INF] Modbus TCP Simulator started successfully
[4.2] [00:59:18 INF] NATS connection verified - RTT: 0.3174ms
[4.2] [00:59:18 INF] NATS service discovered: Name=subnode-mgmt, Version=1.0.0, WedaNodeId=48b02dea8160, Endpoints=registration,configuration,deletion
[4.3] [00:59:18 INF] Found existing SubNode registration: DeviceName=CmdHandlerDemo-09090214, DeviceId=355899692216745984
[4.3] [00:59:18 INF] Configuration uploaded successfully: DeviceId=355899692216745984, Status=applied
[4.4] [00:59:19 INF] Reporting health: DeviceId=355899692216745984, IsHealthy=True, CpuUsage=70.36%, MemoryUsage=1.61%, Topic=eco1j.weda.355899692216745984.health, ReqSeqId=1
[4.4] [00:59:19 INF] temperature_sensor: 25
[4.4] [00:59:19 INF] temperature_sensor: 25.095226
[4.4] [00:59:23 INF] Demo command 'sensor.read' succeeded: {"Status":0,"Message":"Sensor value read successfully","ResultData":{"deviceName":"MyFirstDevice","sensorName":"tempe
[4.4] [00:59:23 INF] Demo command 'tag.set' succeeded: {"Status":0,"Message":"Sensor tags applied successfully","ResultData":{"deviceName":"MyFirstDevice","sensorName":"tempera
[4.4] [00:59:23 INF] Demo command 'props.get' succeeded: {"Status":0,"Message":"Device properties retrieved successfully","ResultData":{"MyFirstDevice":{"deviceName":"MyFirstDe
[4.4] [00:59:23 INF] temperature_sensor: 25.00975
```

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-01** | High | Fixed | baked the developer-machine WedaNode URL `172.22.160.192:4224` |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

Its custom commands **are** the SubNode function under test. The previous report could not show a registered deviceId; this run captures one.

## 5. Not verified in this run

Nothing. All eight checks were exercised and observed.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
./scripts/build-push-image.sh feature-custom-commands

# 2. Deploy the revision through container-management and assert 4.1-4.4
./scripts/deploy-verify.sh feature-custom-commands <compose-file> weda-command-handler

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 weda-command-handler'
```

## 7. Change record

| | |
|---|---|
| **This run** | 2026-09-10 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Previous run** | 2026-09-09 — same device; see §4/§5 for what changed |
| **Example fixes** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Report schema + this evidence** | [PR !7396](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7396) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, score 4.1-4.4 separately and capture the full container log |
| **Report schema** | v1.2 — see [the schema note](README.md#report-schema) |
