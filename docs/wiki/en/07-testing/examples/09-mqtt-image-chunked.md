---
doc-id:    E2E-EXD-MQTT_IMAGE_CHUNKED
title:     "mqtt-image-chunked — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.0
status:    Review
date:      2026-09-09
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# mqtt-image-chunked — End-to-End Deployment Test Report

> **Verdict: ✅ PASS**
> Part of [E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md), which defines the
> gates, the step format and the shared fixtures. This page records only what this example did.

## 0. At a Glance

| | |
|---|---|
| **Example** | [`examples/mqtt-image-chunked`](../../../../../examples/mqtt-image-chunked/) |
| **Compose service** | `image-sensor` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/mqtt-image-chunked:latest` |
| **Transport** | MQTT (chunked binary payloads) |
| **External dependency** | MQTT broker — **bundled** (mosquitto) |
| **SubNode name used** | `ImageDemo-<date>` |
| **Registered deviceId** | — |
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

- `nats + registration + telemetry with the bundled broker`

## 3. Defects this example exposed

- None specific to this example.

Defect numbering is shared across the suite — see
[§10 of the parent report](../01-e2e-test-scenario-example-images.md).

## 4. Notes

First attempt failed on my own test stack, not the example: overriding only `command:` on `eclipse-mosquitto` is swallowed by the image entrypoint, so the broker never started. Fixed by overriding `entrypoint` too.

## 5. Re-running this test

```bash
# 1. Build and publish (repository root is the build context — the script enforces it)
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh mqtt-image-chunked

# 2. Deploy the stack to the device through container-management, then confirm on the device
ssh <device> 'docker logs --tail 40 <container>'
```

A container reported `running` is **not** sufficient evidence — see §6 of the parent report.

## 6. Change record

| | |
|---|---|
| Test run | 2026-09-09, EPC-R7300A1 `48b02dea8160`, SIT |
| Fixes landed in | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| Harness | `deploy-verify.sh` — deploy revision, wait for the container, assert the four Act-4 checks |

> CI note: `Weda.SubNode.Integration.Tests` reported 2/10 failing throughout this work. That is
> D-16 — hardcoded `localhost` against a Testcontainers-published port — which predates this
> branch by a week and is fixed separately in
> [PR #7363](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7363).
> It is unrelated to any example result on this page.
