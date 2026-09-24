---
doc-id:    E2E-EXD-HTTP_STOCK_QUOTES
title:     "http-stock-quotes — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# http-stock-quotes — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-10 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/http-stock-quotes`](../../../../../examples/http-stock-quotes/) |
| **Compose service** | `stock-monitor` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/http-stock-quotes:latest` |
| **Transport** | HTTP poll (TWSE `mis.twse.com.tw:443`) |
| **External dependency** | **Outbound internet from the device** (fixture N1) |
| **Host endpoints used** | 2395 (SubNode Web API) |
| **Required deployment overrides** | None |
| **SubNode name** | `StockDemo-0909` |
| **Registered deviceId** | `355914199278288896` |

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
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | `NATS connection verified - RTT: 0.2556ms` |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `Found existing SubNode registration: DeviceName=StockDemo-0909, DeviceId=355914199278288896` |
| **Function** | E2E-EXD-4.4 | Heartbeat reaching the cloud | ✅ | [01:01:37 INF] Reporting health: DeviceId=355914199278288896, IsHealthy=True, CpuUsage=79.… |
| **Function** | E2E-EXD-4.5 | Doing what this example exists to do | ✅ | [01:01:37 INF]   TSMC Current Price (stock_2330_current): 2445 TWD |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.

## 3. Evidence log

Verbatim from `docker logs weda-stock` on the device, tagged with the check each line proves.
Captured 2026-09-10; 85 lines total.

```text
[4.2] [01:01:37 INF] NATS connection verified - RTT: 0.2556ms
[4.2] [01:01:37 INF] NATS service discovered: Name=subnode-mgmt, Version=1.0.0, WedaNodeId=48b02dea8160, Endpoints=registration,configuration,deletion
[4.3] [01:01:37 INF] Found existing SubNode registration: DeviceName=StockDemo-0909, DeviceId=355914199278288896
[4.3] [01:01:37 INF] Configuration uploaded successfully: DeviceId=355914199278288896, Status=applied
[4.4] [01:01:37 INF] Reporting health: DeviceId=355914199278288896, IsHealthy=True, CpuUsage=79.79%, MemoryUsage=1.61%, Topic=eco1j.weda.355914199278288896.health, ReqSeqId=1
[4.5] [01:01:37 INF]   Advantech Open Price (stock_2395_open): 647 TWD
[4.5] [01:01:37 INF]   Advantech High Price (stock_2395_high): 658 TWD
[4.5] [01:01:37 INF]   Advantech Low Price (stock_2395_low): 647 TWD
[4.5] [01:01:37 INF]   Advantech Volume (stock_2395_volume): 100 shares
[4.5] [01:01:37 INF]   TSMC Current Price (stock_2330_current): 2445 TWD
[4.5] [01:01:37 INF]   TSMC Open Price (stock_2330_open): 2445 TWD
[4.5] [01:01:37 INF]   TSMC High Price (stock_2330_high): 2450 TWD
```

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-06** | High | Fixed | hardcoded `.UseMockCloud()`, so it could never reach a WedaNode |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

Live market data reaching the cloud, which is the half D-06 used to break. The values differ from the 2026-09-09 run because the market moved — that is the point: they are real.

## 5. Not verified in this run

Nothing. All nine checks were exercised and observed.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
./scripts/build-push-image.sh http-stock-quotes

# 2. Deploy the revision through container-management and assert 4.1-4.5
./scripts/deploy-verify.sh http-stock-quotes

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 weda-stock'
```

## 7. Change record

| | |
|---|---|
| **This run** | 2026-09-10 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Previous run** | 2026-09-09 — same device; see §4/§5 for what changed |
| **Example fixes** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Report schema + this evidence** | [PR !7396](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7396) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, score 4.1-4.5 separately and capture the full container log |
| **Report schema** | v1.3 — the function check is scored as the heartbeat (4.4) and the example's own job (4.5), from `examples/http-stock-quotes/e2e-expect.json` |
