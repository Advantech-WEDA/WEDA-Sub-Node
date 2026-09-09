---
doc-id:    E2E-EXD-CFX_ENDPOINT
title:     "cfx-endpoint — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.0
status:    Review
date:      2026-09-09
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# cfx-endpoint — End-to-End Deployment Test Report

> **Verdict: ✅ PASS**
> Part of [E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md), which defines the
> gates, the step format and the shared fixtures. This page records only what this example did.

## 0. At a Glance

| | |
|---|---|
| **Example** | [`examples/cfx-endpoint`](../../../../../examples/cfx-endpoint/) |
| **Compose service** | `cfx-subnode` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/cfx-endpoint:latest` |
| **Transport** | IPC-CFX over MQTT (RabbitMQ AMQP→MQTT bridge) |
| **External dependency** | CFX bridge — **bundled** (RabbitMQ + `rabbitmq_mqtt` + `rabbitmq_amqp1_0`) |
| **SubNode name used** | `CfxDemo-<date>` |
| **Registered deviceId** | 355914649897533440 |
| **Device** | EPC-R7300A1 `48b02dea8160` — ARM64, Ubuntu 20.04 |
| **Verdict** | **✅ PASS** |

## 1. Gate results

| Gate | Step | Result |
|---|---|---|
| Build | E2E-EXD-2.2 | ✅ builds for `linux/arm64` |
| Push | E2E-EXD-2.2 | ✅ multi-arch manifest in Harbor |
| Standalone start | E2E-EXD-2.3 | ✅ starts with no bind mounts |
| Deploy | E2E-EXD-3.2 | ✅ `deviceActions: add` |
| Function | E2E-EXD-4.1–4.4 | ✅ PASS |

## 2. Evidence

Observed on the device (`docker logs`):

- `Configuration uploaded successfully: Status=applied`
- `Subscribed to CFX topic filter +/+/+/CFX/# for 17 sensor(s) across 17 message type(s)`
- `Health report sent successfully`

## 3. Defects this example exposed

- D-01 WedaNode URL `172.22.160.197:4224`
- D-04 empty NATS password (by design)
- D-14 host/bridge network mixing
- D-15 `rabbitmq.conf` append aborts the broker

Defect numbering is shared across the suite — see
[§10 of the parent report](../01-e2e-test-scenario-example-images.md).

## 4. Notes

**The hardest case — four attempts.** Three failures were test-harness or authoring errors on my part, not the example: two misdiagnoses (assumed pull timeout, then stale network state) and a `rabbitmq.conf` edit that crash-looped the broker. Only D-14 is a real platform finding.

## 5. Re-running this test

```bash
# 1. Build and publish (repository root is the build context — the script enforces it)
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh cfx-endpoint

# 2. Deploy the stack to the device through container-management, then confirm on the device
ssh <device> 'docker logs --tail 40 <container>'
```

A container reported `running` is **not** sufficient evidence — see §6 of the parent report.
