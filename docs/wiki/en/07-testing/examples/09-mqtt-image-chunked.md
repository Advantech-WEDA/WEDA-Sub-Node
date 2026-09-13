---
doc-id:    E2E-EXD-MQTT_IMAGE_CHUNKED
title:     "mqtt-image-chunked — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# mqtt-image-chunked — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **⚠️ PARTIAL** | 2026-09-10 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/mqtt-image-chunked`](../../../../../examples/mqtt-image-chunked/) |
| **Compose service** | `image-sensor` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/mqtt-image-chunked:latest` |
| **Transport** | MQTT (chunked binary payloads) |
| **External dependency** | MQTT broker — **bundled** (mosquitto) |
| **Host endpoints used** | 2395 (SubNode Web API), 1883 (bundled mosquitto) |
| **Required deployment overrides** | None |
| **SubNode name** | `ImageDemo-0909` |
| **Registered deviceId** | `355915016601337856` |

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
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | `NATS connection verified - RTT: 0.2591ms` |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `Found existing SubNode registration: DeviceName=ImageDemo-0909, DeviceId=355915016601337856` |
| **Function** | E2E-EXD-4.4 | Heartbeat reaching the cloud | ✅ | [01:15:20 INF] Reporting health: DeviceId=355915016601337856, IsHealthy=True, CpuUsage=56.… |
| **Function** | E2E-EXD-4.5 | Doing what this example exists to do | ❌ | The bundled broker has no publisher, so the SubNode subscribes but no frame arrives. Unblock by attaching an image publisher, then assert the reassembled frame size. |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.

## 3. Evidence log

Verbatim from `docker logs weda-image-sensor` on the device, tagged with the check each line proves.
Captured 2026-09-10; 52 lines total.

```text
[4.2] [01:15:19 INF] NATS connection verified - RTT: 0.2591ms
[4.2] [01:15:19 INF] NATS service discovered: Name=subnode-mgmt, Version=1.0.0, WedaNodeId=48b02dea8160, Endpoints=registration,configuration,deletion
[4.3] [01:15:19 INF] Found existing SubNode registration: DeviceName=ImageDemo-0909, DeviceId=355915016601337856
[4.3] [01:15:20 INF] Configuration uploaded successfully: DeviceId=355915016601337856, Status=applied
[4.4] [01:15:20 INF] Reporting health: DeviceId=355915016601337856, IsHealthy=True, CpuUsage=56.48%, MemoryUsage=1.56%, Topic=eco1j.weda.355915016601337856.health, ReqSeqId=1
[4.5] [01:15:20 INF] Subscribed to MQTT topic: sensor/image/mnist
[4.5] [01:15:20 INF] Subscribed to image topic: sensor/image/mnist
[4.5] [01:15:20 INF] Message broker subscription established
```

## 4. Defects

None. This example exposed no defect of its own; it inherited the suite-wide fixes (baked config, repository-root build context).

> Defect ids are shared across the suite; the register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

Subscribes and registers cleanly. What it cannot show without a publisher is the chunking itself — the feature the example exists to demonstrate.

## 5. Not verified in this run

| Check left unproven | Why | What would unblock it |
|---|---|---|
| Chunk reassembly of a real image | the bundled broker had no publisher attached, so the SubNode subscribed but received no frames | attach an image publisher and assert reassembled frame size |

> **A gap listed here is not a pass.** It is recorded so the next run closes it rather than rediscovering it.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
./scripts/build-push-image.sh mqtt-image-chunked

# 2. Deploy the revision through container-management and assert 4.1-4.5
./scripts/deploy-verify.sh mqtt-image-chunked

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 weda-image-sensor'
```

## 7. Change record

| | |
|---|---|
| **This run** | 2026-09-10 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Previous run** | 2026-09-09 — same device; see §4/§5 for what changed |
| **Example fixes** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Report schema + this evidence** | [PR !7396](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7396) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, score 4.1-4.5 separately and capture the full container log |
| **Report schema** | v1.3 — the function check is scored as the heartbeat (4.4) and the example's own job (4.5), from `examples/mqtt-image-chunked/e2e-expect.json` |
