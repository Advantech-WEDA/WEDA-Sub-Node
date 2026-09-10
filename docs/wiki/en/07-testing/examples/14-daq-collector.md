---
doc-id:    E2E-EXD-DAQ_COLLECTOR
title:     "daq-collector — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# daq-collector — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **❌ BLOCKED** | 2026-09-10 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/daq-collector`](../../../../../examples/daq-collector/) |
| **Compose service** | `daq-collector` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/daq-collector:latest` |
| **Transport** | DAQ streaming with windowed feature extraction |
| **External dependency** | **Real DAQ modules on the device — absent on `48b02dea8160`** |
| **Host endpoints used** | 2395 (SubNode Web API) |
| **Required deployment overrides** | None |
| **SubNode name** | `DaqCDemo-0909` |
| **Registered deviceId** | `355905847374643200` |

## 2. Gate results

Four gates, eight checks. **A container reported `running` satisfies check 4.1 and nothing else** —
it is not evidence for 4.2, 4.3 or 4.4. Every row stands on its own observation, quoted from the
device's own log in §3.

| Gate | Step | Check | Result | Evidence |
|---|---|---|:-:|---|
| **Build** | E2E-EXD-2.2 | Builds for `linux/arm64` from the repository root | ✅ | rebuilt for `linux/amd64,linux/arm64` after the D-17 fix |
| **Push** | E2E-EXD-2.2 | Multi-arch manifest present in Harbor | ✅ | multi-arch manifest re-published to Harbor |
| **Push** | E2E-EXD-2.3 | Image runs with nothing mounted | ✅ | config baked into `/app`; no bind mounts required |
| **Deploy** | E2E-EXD-3.2 | Revision accepted and applied by the device | ✅ | `deviceActions: add` |
| **Function** | E2E-EXD-4.1 | Container up and not restart-looping | ✅ | `status=running restarts=0` |
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | `NATS connection verified - RTT: 0.391ms` |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `Found existing SubNode registration: DeviceName=DaqCDemo-0909, DeviceId=355905847374643200` |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ❌ | `DaqCommunication connection failed: No DAQ modules available.` |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.

## 3. Evidence log

Verbatim from `docker logs weda-daqc` on the device, tagged with the check each line proves.
Captured 2026-09-10; 142 lines total.

```text
[4.2] [01:23:55 INF] NATS connection verified - RTT: 0.391ms
[4.2] [01:23:55 INF] NATS service discovered: Name=subnode-mgmt, Version=1.0.0, WedaNodeId=48b02dea8160, Endpoints=registration,configuration,deletion
[4.3] [01:23:55 INF] Found existing SubNode registration: DeviceName=DaqCDemo-0909, DeviceId=355905847374643200
[4.4-fail] [01:23:55 ERR] DaqCommunication connection failed: No DAQ modules available.
```

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-08** | Medium | **Open** | `docker-compose.yml` fails validation: `services.daq-collector.devices must be a list` — pre-existing, still open |
| **D-17** | High | **Fixed** in this run | the build failed outright until this run's fix: the image pinned `gpgv=2.4.4-2ubuntu17.4`, which Ubuntu has dropped from the archive |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

**The previous report was wrong about this example and this run corrects it.** It claimed 4.2 and 4.3 were “not reached — startup halts at the DAQ probe”. They are reached: the SubNode verifies NATS, registers, and only then fails the hardware probe. The corrected row is ✅✅✅❌ — the same shape as a PARTIAL, but the verdict stays BLOCKED because the missing hardware is not a transient fixture outage, it is absent from this class of device.

## 5. Not verified in this run

| Check left unproven | Why | What would unblock it |
|---|---|---|
| E2E-EXD-4.4 — telemetry from the DAQ modules | the test device has no DAQ modules fitted | re-run on a device with DAQ hardware; no code change expected |

> **A gap listed here is not a pass.** It is recorded so the next run closes it rather than rediscovering it.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
./scripts/build-push-image.sh daq-collector

# 2. Deploy the revision through container-management and assert 4.1-4.4
./scripts/deploy-verify.sh daq-collector <compose-file> weda-daqc

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 weda-daqc'
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
