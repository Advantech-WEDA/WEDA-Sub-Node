---
doc-id:    E2E-EXD-DAQ_PROXY
title:     "daq-proxy — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# daq-proxy — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-10 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/daq-proxy`](../../../../../examples/daq-proxy/) |
| **Compose service** | `daq-proxy` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/daq-proxy:latest` |
| **Transport** | SubNode → SubNode (consumes another SubNode's telemetry) |
| **External dependency** | Pairs with `daq-collector` in production; **not required** for this run |
| **Host endpoints used** | 2395 (SubNode Web API) |
| **Required deployment overrides** | None |
| **SubNode name** | `DaqPDemo-0909` |
| **Registered deviceId** | `355906074861109248` |

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
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | `NATS connection verified - RTT: 0.2414ms` |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `Found existing SubNode registration: DeviceName=DaqPDemo-0909, DeviceId=355906074861109248` |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ✅ | sensor values + `Reporting health` — see §3 |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.

## 3. Evidence log

Verbatim from `docker logs weda-daqp` on the device, tagged with the check each line proves.
Captured 2026-09-10; 93 lines total.

```text
[4.2] [01:21:40 INF] NATS connection verified - RTT: 0.2414ms
[4.2] [01:21:40 INF] NATS service discovered: Name=subnode-mgmt, Version=1.0.0, WedaNodeId=48b02dea8160, Endpoints=registration,configuration,deletion
[4.3] [01:21:40 INF] Found existing SubNode registration: DeviceName=DaqPDemo-0909, DeviceId=355906074861109248
[4.3] [01:21:41 INF] Configuration uploaded successfully: DeviceId=355906074861109248, Status=null
[4.3] [01:21:41 INF] Device registered: DeviceId=355906074861109248, RegistrationStatus=accepted, RspSeqId=691949ec-35df-49d0-80c5-0dfb0365e6fa
[4.4] [01:21:41 INF] Reporting health: DeviceId=355906074861109248, IsHealthy=True, CpuUsage=52.02%, MemoryUsage=1.68%, Topic=eco1j.weda.355906074861109248.health, ReqSeqId=1
```

## 4. Defects

None. This example exposed no defect of its own; it inherited the suite-wide fixes (baked config, repository-root build context).

> Defect ids are shared across the suite; the register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

Passes without its upstream present — it degrades cleanly rather than crash-looping, which is why it is a PASS while its partner is BLOCKED.

## 5. Not verified in this run

| Check left unproven | Why | What would unblock it |
|---|---|---|
| Proxying real upstream data | `daq-collector` is BLOCKED on this device (no DAQ modules), so there was no upstream | run the pair on a device with DAQ modules fitted |

> **A gap listed here is not a pass.** It is recorded so the next run closes it rather than rediscovering it.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
./scripts/build-push-image.sh daq-proxy

# 2. Deploy the revision through container-management and assert 4.1-4.4
./scripts/deploy-verify.sh daq-proxy <compose-file> weda-daqp

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 weda-daqp'
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
