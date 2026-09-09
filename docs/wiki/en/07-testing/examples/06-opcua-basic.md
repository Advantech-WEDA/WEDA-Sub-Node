---
doc-id:    E2E-EXD-OPCUA_BASIC
title:     "opcua-basic — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.0
status:    Review
date:      2026-09-09
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# opcua-basic — End-to-End Deployment Test Report

> **Verdict: ✅ PASS**
> Part of [E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md), which defines the
> gates, the step format and the shared fixtures. This page records only what this example did.

## 0. At a Glance

| | |
|---|---|
| **Example** | [`examples/opcua-basic`](../../../../../examples/opcua-basic/) |
| **Compose service** | `opcua-device` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/opcua-basic:latest` |
| **Transport** | OPC UA (embedded simulator on localhost:4840) |
| **External dependency** | None |
| **SubNode name used** | `OpcUaDemo-<date>` |
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

- `OPC-UA Simulator started successfully at opc.tcp://localhost:4840/WedaOpcUaSimulator`
- `nats + registration + telemetry`

## 3. Defects this example exposed

- D-05 `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`
- D-06 hardcoded `.UseMockCloud()`

Defect numbering is shared across the suite — see
[§10 of the parent report](../01-e2e-test-scenario-example-images.md).

## 4. Notes

D-05 was established by elimination: works via `dotnet run` on the host, works self-contained on the host, fails in a container on **both** base images, starts cleanly with the flag set to 0. The OPC UA stack builds an X.509 application certificate at startup and its X.500 name handling needs ICU.

## 5. Re-running this test

```bash
# 1. Build and publish (repository root is the build context — the script enforces it)
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh opcua-basic

# 2. Deploy the stack to the device through container-management, then confirm on the device
ssh <device> 'docker logs --tail 40 <container>'
```

A container reported `running` is **not** sufficient evidence — see §6 of the parent report.
