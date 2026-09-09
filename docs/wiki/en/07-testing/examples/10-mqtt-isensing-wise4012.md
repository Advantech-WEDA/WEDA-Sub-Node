---
doc-id:    E2E-EXD-MQTT_ISENSING_WISE4012
title:     "mqtt-isensing-wise4012 — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.0
status:    Review
date:      2026-09-09
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# mqtt-isensing-wise4012 — End-to-End Deployment Test Report

> **Verdict: ✅ PASS**
> Part of [E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md), which defines the
> gates, the step format and the shared fixtures. This page records only what this example did.

## 0. At a Glance

| | |
|---|---|
| **Example** | [`examples/mqtt-isensing-wise4012`](../../../../../examples/mqtt-isensing-wise4012/) |
| **Compose service** | `wise-4012-isensing` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/mqtt-isensing-wise4012:latest` |
| **Transport** | MQTT (Advantech iSensing JSON) |
| **External dependency** | MQTT broker — **bundled**; a real WISE-4012 publishes the payloads in production |
| **SubNode name used** | `ISensingDemo-<date>` |
| **Registered deviceId** | 355941923954884608 |
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

- `Subscribed to ISensing topics: Advantech/00D0C9FAC80E/data, .../status`
- `Message broker subscription established`
- `Health report sent successfully`

## 3. Defects this example exposed

- D-07 missing `(context, DeviceConfiguration)` constructor

Defect numbering is shared across the suite — see
[§10 of the parent report](../01-e2e-test-scenario-example-images.md).

## 4. Notes

D-07 meant this example **could not start on any host**, container or not. It was masked: without a broker it died on the MQTT connect first, so the constructor failure never surfaced until a broker was bundled.

## 5. Re-running this test

```bash
# 1. Build and publish (repository root is the build context — the script enforces it)
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh mqtt-isensing-wise4012

# 2. Deploy the stack to the device through container-management, then confirm on the device
ssh <device> 'docker logs --tail 40 <container>'
```

A container reported `running` is **not** sufficient evidence — see §6 of the parent report.
