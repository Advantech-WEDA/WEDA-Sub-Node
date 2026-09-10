---
doc-id:    E2E-EXD-CFX_ENDPOINT
title:     "cfx-endpoint — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# cfx-endpoint — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-10 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/cfx-endpoint`](../../../../../examples/cfx-endpoint/) |
| **Compose service** | `cfx-subnode` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/cfx-endpoint:latest` |
| **Transport** | IPC-CFX over MQTT (RabbitMQ AMQP→MQTT bridge) |
| **External dependency** | CFX bridge — **bundled** (RabbitMQ + `rabbitmq_mqtt` + `rabbitmq_amqp1_0`) |
| **Host endpoints used** | 2395 (SubNode Web API), 1883 (RabbitMQ MQTT), 5672 / 15672 (AMQP, management) |
| **Required deployment overrides** | **`SystemConfig__WedaNode__Password` is mandatory** — the example ships an empty NATS password by design (D-04); a deployment without it can never authenticate |
| **SubNode name** | `CfxDemo-0909d` |
| **Registered deviceId** | `355914649897533440` |

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
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | `NATS connection verified - RTT: 0.2888ms` |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `Found existing SubNode registration: DeviceName=CfxDemo-0909d, DeviceId=355914649897533440` |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ✅ | sensor values + `Reporting health` — see §3 |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.

## 3. Evidence log

Verbatim from `docker logs weda-cfx` on the device, tagged with the check each line proves.
Captured 2026-09-10; 68 lines total.

```text
[4.2] [01:22:54.387 INF] NATS connection verified - RTT: 0.2888ms
[4.2] [01:22:54.433 INF] NATS service discovered: Name=subnode-mgmt, Version=1.0.0, WedaNodeId=48b02dea8160, Endpoints=registration,configuration,deletion
[4.3] [01:22:54.447 INF] Found existing SubNode registration: DeviceName=CfxDemo-0909d, DeviceId=355914649897533440
[4.3] [01:22:54.699 INF] Configuration uploaded successfully: DeviceId=355914649897533440, Status=null
[4.3] [01:22:54.742 INF] Device registered: DeviceId=355914649897533440, RegistrationStatus=accepted, RspSeqId=f94232c8-6c91-4e7f-8671-eef60b75c680
[4.4] [01:22:54.917 INF] Reporting health: DeviceId=355914649897533440, IsHealthy=True, CpuUsage=58.29%, MemoryUsage=1.71%, Topic=eco1j.weda.355914649897533440.health, ReqSeqId
[4.4] [01:22:54.937 INF] Subscribed to MQTT topic: +/+/+/CFX/#
[4.4] [01:22:54.938 INF] Subscribed to CFX topic filter +/+/+/CFX/# for 17 sensor(s) across 17 message type(s)
[4.4] [01:22:54.939 INF] Message broker subscription established
```

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-01** | High | Fixed | baked the developer-machine WedaNode URL `172.22.160.197:4224` |
| **D-04** | Medium | By design | ships an **empty** NATS password by design — deployments must supply `SystemConfig__WedaNode__Password` |
| **D-14** | High | Fixed in the example; agent behaviour **Open** | a stack mixing `network_mode: host` with a bridge-networked sibling never starts and emits no logs at all |
| **D-15** | Low | Fixed | appending to `/etc/rabbitmq/rabbitmq.conf` from `command:` aborts the broker |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

**Clean on the first attempt this time**, against four attempts in the 2026-09-09 run. All 17 sensors across 17 CFX message types subscribed. The D-14 fix (both services on host networking) is holding.

## 5. Not verified in this run

| Check left unproven | Why | What would unblock it |
|---|---|---|
| CFX messages from a real production line | the bundled RabbitMQ bridge stands in for the shop-floor endpoints | point the bridge at a real CFX endpoint and assert decoded message types |

> **A gap listed here is not a pass.** It is recorded so the next run closes it rather than rediscovering it.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
./scripts/build-push-image.sh cfx-endpoint

# 2. Deploy the revision through container-management and assert 4.1-4.4
./scripts/deploy-verify.sh cfx-endpoint <compose-file> weda-cfx

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 weda-cfx'
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
