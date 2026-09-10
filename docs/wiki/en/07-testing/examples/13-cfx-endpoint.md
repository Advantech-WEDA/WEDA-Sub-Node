---
doc-id:    E2E-EXD-CFX_ENDPOINT
title:     "cfx-endpoint — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.1
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# cfx-endpoint — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-09 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

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
| **SubNode name** | `CfxDemo-<date>` |
| **Registered deviceId** | `355914649897533440` |

## 2. Gate results

Four gates, eight checks. **A container reported `running` satisfies check 4.1 and nothing else** —
it is not evidence for 4.2, 4.3 or 4.4. Every row must stand on its own observation.

| Gate | Step | Check | Result | Evidence |
|---|---|---|:-:|---|
| **Build** | E2E-EXD-2.2 | Builds for `linux/arm64` from the repository root | ✅ | image produced |
| **Push** | E2E-EXD-2.2 | Multi-arch manifest present in Harbor | ✅ | manifest published |
| **Push** | E2E-EXD-2.3 | Image runs with nothing mounted | ✅ | starts with no bind mounts |
| **Deploy** | E2E-EXD-3.2 | Revision accepted and applied by the device | ✅ | `deviceActions: add` — after both services were put on host networking (D-14) |
| **Function** | E2E-EXD-4.1 | Container up and not restart-looping | ✅ | container `running`, log stream advancing |
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | *harness* — `deploy-verify.sh` assertion, no verbatim line captured |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `Configuration uploaded successfully: Status=applied`; child device `355914649897533440` |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ✅ | 17 sensors subscribed across 17 CFX message types + `Health report sent successfully` |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.
> *harness* means the check passed the `deploy-verify.sh` assertion but no verbatim log line was captured for this page. Those are listed in §5 where they matter.

## 3. Evidence log

Verbatim from `docker logs <container>` on the device, each line tagged with the check it proves:

```text
[4.3] Configuration uploaded successfully: Status=applied
[4.4] Subscribed to CFX topic filter +/+/+/CFX/# for 17 sensor(s) across 17 message type(s)
[4.4] Health report sent successfully
```

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-01** | High | Fixed | baked the developer-machine WedaNode URL `172.22.160.197:4224` |
| **D-04** | Medium | By design | ships an **empty** NATS password by design — deployments must supply `SystemConfig__WedaNode__Password` |
| **D-14** | High | Fixed in the example; agent behaviour **Open** | a stack mixing `network_mode: host` with a bridge-networked sibling never starts, and emits **no logs at all**, so it reads as a hung image pull |
| **D-15** | Low | Fixed | appending to `/etc/rabbitmq/rabbitmq.conf` from `command:` aborts the broker with `failed_to_prepare_configuration` |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

**The hardest case — four attempts.** Three of the four failures were test-harness or authoring errors on my part, not the example: two misdiagnoses (assumed a pull timeout, then stale network state) and a `rabbitmq.conf` edit that crash-looped the broker. Only D-14 is a real platform finding. Recorded in full because the misdiagnoses are the expensive part to repeat.

## 5. Not verified in this run

| Check left unproven | Why | What would unblock it |
|---|---|---|
| CFX messages from a real production line | the bundled RabbitMQ bridge stands in for the shop-floor endpoints | point the bridge at a real CFX endpoint and assert decoded message types |

> **A gap listed here is not a pass.** It is recorded so the next run closes it rather than rediscovering it.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh cfx-endpoint

# 2. Deploy the revision through container-management, then assert the four Act-4 checks
./scripts/deploy-verify.sh cfx-endpoint

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 cfx-subnode'
```

## 7. Change record

| | |
|---|---|
| **Test run** | 2026-09-09 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Fixes landed in** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, assert 4.1–4.4 |
| **Report schema** | v1.1 — see [the schema note](README.md#report-schema) |
