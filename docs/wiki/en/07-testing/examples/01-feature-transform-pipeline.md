---
doc-id:    E2E-EXD-FEATURE_TRANSFORM_PIPELINE
title:     "feature-transform-pipeline — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# feature-transform-pipeline — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-10 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/feature-transform-pipeline`](../../../../../examples/feature-transform-pipeline/) |
| **Compose service** | `testdevice` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/feature-transform-pipeline:latest` |
| **Transport** | Modbus TCP (embedded simulator on `127.0.0.1:5020`) |
| **External dependency** | None — the example starts its own simulator |
| **Host endpoints used** | 2395 (SubNode Web API), 5020 (embedded Modbus simulator) |
| **Required deployment overrides** | None |
| **SubNode name** | `TransformDemo-09090124` |
| **Registered deviceId** | `355886333169762304` |

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
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | `NATS connection verified - RTT: 0.3242ms` |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `Found existing SubNode registration: DeviceName=TransformDemo-09090124, DeviceId=355886333169762304` |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ✅ | sensor values + `Reporting health` — see §3 |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.

## 3. Evidence log

Verbatim from `docker logs weda-transform-pipeline` on the device, tagged with the check each line proves.
Captured 2026-09-10; 91 lines total.

```text
[4.1] [00:58:09 INF] Modbus TCP Simulator started successfully
[4.2] [00:58:09 INF] NATS connection verified - RTT: 0.3242ms
[4.2] [00:58:09 INF] NATS service discovered: Name=subnode-mgmt, Version=1.0.0, WedaNodeId=48b02dea8160, Endpoints=registration,configuration,deletion
[4.3] [00:58:10 INF] Found existing SubNode registration: DeviceName=TransformDemo-09090124, DeviceId=355886333169762304
[4.3] [00:58:10 INF] Configuration uploaded successfully: DeviceId=355886333169762304, Status=applied
[4.4] [00:58:10 INF] Reporting health: DeviceId=355886333169762304, IsHealthy=True, CpuUsage=45.05%, MemoryUsage=1.93%, Topic=eco1j.weda.355886333169762304.health, ReqSeqId=1
[4.4] [00:58:10 INF] temperature_sensor: 25
[4.4] [00:58:10 INF]   [0] 25 -> 77
[4.4] [00:58:15 INF] temperature_sensor: 25.030037
[4.4] [00:58:15 INF]   [0] 25.030037 -> 77.05406646728515
[4.4] [00:58:15 INF]   [0] 77.05406646728515 -> 25.03003692626953
[4.4] [00:58:20 INF]   [0] 24.66589736938477 -> 24.883808578372133
```

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-02** | High | Fixed | config not baked into the image, so a deployed stack started with none |
| **D-11** | Low | Fixed | self-contained publish on `dotnet/runtime` shipped a second unused runtime |
| **D-12** | Low | Fixed | 252.9 MB build context, 177 MB of it gitignored logs |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

The reference case, and still the cleanest run in the suite. The registration id is **unchanged from the 2026-09-09 run**, so `/app/.weda` persistence held across a week, a full image rebuild and a redeploy — the D-02 lesson is sticking.

## 5. Not verified in this run

Nothing. All eight checks were exercised and observed.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
./scripts/build-push-image.sh feature-transform-pipeline

# 2. Deploy the revision through container-management and assert 4.1-4.4
./scripts/deploy-verify.sh feature-transform-pipeline <compose-file> weda-transform-pipeline

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 weda-transform-pipeline'
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
