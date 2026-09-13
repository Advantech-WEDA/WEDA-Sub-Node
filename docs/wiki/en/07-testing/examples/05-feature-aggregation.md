---
doc-id:    E2E-EXD-FEATURE_AGGREGATION
title:     "feature-aggregation — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# feature-aggregation — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-10 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/feature-aggregation`](../../../../../examples/feature-aggregation/) |
| **Compose service** | `power-aggregation` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/feature-aggregation:latest` |
| **Transport** | Modbus TCP (two embedded simulators, `:5020` and `:5021`) |
| **External dependency** | None |
| **Host endpoints used** | 2395 (SubNode Web API), 5020 + 5021 (embedded Modbus simulators) |
| **Required deployment overrides** | None |
| **SubNode name** | `PowerAggDemo-0909` |
| **Registered deviceId** | `355913980792799232` |

## 2. Gate results

Four gates, nine checks. **A container reported `running` satisfies check 4.1 and nothing else.**
4.4 is the framework heartbeat, which a SubNode emits whether or not the device it reads is
reachable; 4.5 is what this example exists to do. Every row stands on its own observation, quoted
from the device's own log in §3.

| Gate | Step | Check | Result | Evidence |
|---|---|---|:-:|---|
| **Build** | E2E-EXD-2.2 | Builds for `linux/arm64` from the repository root | ✅ | rebuilt for `linux/amd64,linux/arm64` |
| **Push** | E2E-EXD-2.2 | Multi-arch manifest present in Harbor | ✅ | multi-arch manifest re-published to Harbor |
| **Push** | E2E-EXD-2.3 | Image runs with nothing mounted | ✅ | config baked into `/app`; no bind mounts required |
| **Deploy** | E2E-EXD-3.2 | Revision accepted and applied by the device | ✅ | `deviceActions: add` |
| **Function** | E2E-EXD-4.1 | Container up and not restart-looping | ✅ | `status=running restarts=0` |
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | `NATS connection verified - RTT: 0.9497ms` |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `Found existing SubNode registration: DeviceName=PowerAggDemo-0909, DeviceId=355913980792799232` |
| **Function** | E2E-EXD-4.4 | Heartbeat reaching the cloud | ✅ | [01:03:53 INF] Reporting health: DeviceId=355913980792799232, IsHealthy=True, CpuUsage=71.… |
| **Function** | E2E-EXD-4.5 | Doing what this example exists to do | ✅ | [01:03:59 INF] [PowerSensor] DataReceived: ResourceId=0b4c6033-402f-5699-9726-82… |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.

## 3. Evidence log

Verbatim from `docker logs weda-power-agg` on the device, tagged with the check each line proves.
Captured 2026-09-10; 122 lines total.

```text
[4.1] [01:03:51 INF] Modbus TCP Simulator started successfully
[4.2] [01:03:52 INF] NATS connection verified - RTT: 0.9497ms
[4.2] [01:03:52 INF] NATS service discovered: Name=subnode-mgmt, Version=1.0.0, WedaNodeId=48b02dea8160, Endpoints=registration,configuration,deletion
[4.3] [01:03:52 INF] Found existing SubNode registration: DeviceName=PowerAggDemo-0909, DeviceId=355913980792799232
[4.3] [01:03:53 INF] Configuration uploaded successfully: DeviceId=355913980792799232, Status=applied
[4.5] [01:03:53 INF] Message broker subscription established
[4.4] [01:03:53 INF] Reporting health: DeviceId=355913980792799232, IsHealthy=True, CpuUsage=71.20%, MemoryUsage=1.64%, Topic=eco1j.weda.355913980792799232.health, ReqSeqId=1
[4.5] [01:03:53 INF] [CurrentSensor] DataReceived: ResourceId=502ac822-265d-5a88-a3f4-8fd20fa82077, Value=5.00, Timestamp=09/10/2026 01:03:53 +00:00
[4.5] [01:03:53 INF] [VoltageSensor] DataReceived: ResourceId=7c8b5f5f-e4b2-5eb9-aec1-97f7100088fd, Value=230.00, Timestamp=09/10/2026 01:03:53 +00:00
[4.5] [01:03:59 INF] [PowerSensor] DataReceived: ResourceId=0b4c6033-402f-5699-9726-822208743d72, Value=1143.28, Timestamp=09/10/2026 01:03:59 +00:00
```

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-06** | High | Fixed | hardcoded `.UseMockCloud()`, so it could never reach a WedaNode |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

Both source sensors report and the derived sensor follows them. The captured values move between samples, so the derived channel is computing rather than echoing.

## 5. Not verified in this run

Nothing. All nine checks were exercised and observed.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
./scripts/build-push-image.sh feature-aggregation

# 2. Deploy the revision through container-management and assert 4.1-4.5
./scripts/deploy-verify.sh feature-aggregation

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 weda-power-agg'
```

## 7. Change record

| | |
|---|---|
| **This run** | 2026-09-10 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Previous run** | 2026-09-09 — same device; see §4/§5 for what changed |
| **Example fixes** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Report schema + this evidence** | [PR !7396](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7396) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, score 4.1-4.5 separately and capture the full container log |
| **Report schema** | v1.3 — the function check is scored as the heartbeat (4.4) and the example's own job (4.5), from `examples/feature-aggregation/e2e-expect.json` |
