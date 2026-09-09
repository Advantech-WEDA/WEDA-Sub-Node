---
doc-id:    E2E-EXD-DAQ_COLLECTOR
title:     "daq-collector — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.0
status:    Review
date:      2026-09-09
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# daq-collector — End-to-End Deployment Test Report

> **Verdict: ❌ BLOCKED**
> Part of [E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md), which defines the
> gates, the step format and the shared fixtures. This page records only what this example did.

## 0. At a Glance

| | |
|---|---|
| **Example** | [`examples/daq-collector`](../../../../../examples/daq-collector/) |
| **Compose service** | `daq-collector` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/daq-collector:latest` |
| **Transport** | DAQ streaming with windowed feature extraction |
| **External dependency** | **Real DAQ modules on the device** |
| **SubNode name used** | `DaqCDemo-<date>` |
| **Registered deviceId** | — |
| **Device** | EPC-R7300A1 `48b02dea8160` — ARM64, Ubuntu 20.04 |
| **Verdict** | **❌ BLOCKED** |

## 1. Gate results

| Gate | Step | Result |
|---|---|---|
| Build | E2E-EXD-2.2 | ✅ builds for `linux/arm64` |
| Push | E2E-EXD-2.2 | ✅ multi-arch manifest in Harbor |
| Standalone start | E2E-EXD-2.3 | — not exercised |
| Deploy | E2E-EXD-3.2 | ✅ `deviceActions: add` |
| Function | E2E-EXD-4.1–4.4 | ❌ BLOCKED |

## 2. Evidence

Observed on the device (`docker logs`):

- `DaqCommunication connection failed: No DAQ modules available`
- `Connection failed`

## 3. Defects this example exposed

- D-08 `docker-compose.yml` fails validation: `services.daq-collector.devices must be a list`

Defect numbering is shared across the suite — see
[§10 of the parent report](../01-e2e-test-scenario-example-images.md).

## 4. Notes

**Fixture gap, not a defect.** Builds, pushes and deploys; the container runs and fails only where it needs hardware that this device does not have. Excluded from the base-image change (pins a `gpgv` CVE fix). D-08 is pre-existing and still open.

## 5. Re-running this test

```bash
# 1. Build and publish (repository root is the build context — the script enforces it)
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh daq-collector

# 2. Deploy the stack to the device through container-management, then confirm on the device
ssh <device> 'docker logs --tail 40 <container>'
```

A container reported `running` is **not** sufficient evidence — see §6 of the parent report.
