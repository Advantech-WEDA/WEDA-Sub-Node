---
doc-id:    E2E-EXD-FEATURE_TRANSFORM_PIPELINE
title:     "feature-transform-pipeline — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.0
status:    Review
date:      2026-09-09
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# feature-transform-pipeline — End-to-End Deployment Test Report

> **Verdict: ✅ PASS**
> Part of [E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md), which defines the
> gates, the step format and the shared fixtures. This page records only what this example did.

## 0. At a Glance

| | |
|---|---|
| **Example** | [`examples/feature-transform-pipeline`](../../../../../examples/feature-transform-pipeline/) |
| **Compose service** | `testdevice` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/feature-transform-pipeline:latest` |
| **Transport** | Modbus TCP (embedded simulator on 127.0.0.1:5020) |
| **External dependency** | None — the example starts its own simulator |
| **SubNode name used** | `TransformDemo-<deviceId>` |
| **Registered deviceId** | 355886333169762304 |
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

- `temperature_sensor: 25.057`
- `[0] 25.057 -> 77.103` (°C→°F transform)
- `[0] 25.057 -> 25.028` (smoothing filter)
- `Health report sent successfully`

## 3. Defects this example exposed

- D-02 config not baked
- D-11 duplicate runtime
- D-12 build context

Defect numbering is shared across the suite — see
[§10 of the parent report](../01-e2e-test-scenario-example-images.md).

## 4. Notes

The reference case: first example proven end to end, and the one that exposed D-02 (images had no baked config) and the `/app/.weda` persistence requirement.

## 5. Re-running this test

```bash
# 1. Build and publish (repository root is the build context — the script enforces it)
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh feature-transform-pipeline

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
