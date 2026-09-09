---
doc-id:    E2E-EXD-HTTP_STOCK_QUOTES
title:     "http-stock-quotes — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.0
status:    Review
date:      2026-09-09
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# http-stock-quotes — End-to-End Deployment Test Report

> **Verdict: ✅ PASS**
> Part of [E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md), which defines the
> gates, the step format and the shared fixtures. This page records only what this example did.

## 0. At a Glance

| | |
|---|---|
| **Example** | [`examples/http-stock-quotes`](../../../../../examples/http-stock-quotes/) |
| **Compose service** | `stock-monitor` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/http-stock-quotes:latest` |
| **Transport** | HTTP poll (TWSE `mis.twse.com.tw:443`) |
| **External dependency** | Outbound internet from the device |
| **SubNode name used** | `StockDemo-<date>` |
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

- `TSMC High Price (stock_2330_high): 2490 TWD`
- `TSMC Low Price (stock_2330_low): 2475 TWD`
- `TSMC Volume (stock_2330_volume): 5734 shares`

## 3. Defects this example exposed

- D-06 hardcoded `.UseMockCloud()`

Defect numbering is shared across the suite — see
[§10 of the parent report](../01-e2e-test-scenario-example-images.md).

## 4. Notes

Fetched live market data before the fix too — but could never report it to a WedaNode, because the mock cloud was compiled in.

## 5. Re-running this test

```bash
# 1. Build and publish (repository root is the build context — the script enforces it)
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh http-stock-quotes

# 2. Deploy the stack to the device through container-management, then confirm on the device
ssh <device> 'docker logs --tail 40 <container>'
```

A container reported `running` is **not** sufficient evidence — see §6 of the parent report.
