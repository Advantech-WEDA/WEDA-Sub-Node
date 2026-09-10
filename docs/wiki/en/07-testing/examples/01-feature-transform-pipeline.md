---
doc-id:    E2E-EXD-FEATURE_TRANSFORM_PIPELINE
title:     "feature-transform-pipeline — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.1
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# feature-transform-pipeline — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-09 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/feature-transform-pipeline`](../../../../../examples/feature-transform-pipeline/) |
| **Compose service** | `testdevice` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/feature-transform-pipeline:latest` |
| **Transport** | Modbus TCP (embedded simulator on `127.0.0.1:5020`) |
| **External dependency** | None — the example starts its own simulator |
| **Host endpoints used** | 2395 (SubNode Web API), 5020 (embedded Modbus simulator) |
| **Required deployment overrides** | None |
| **SubNode name** | `TransformDemo-<deviceId>` |
| **Registered deviceId** | `355886333169762304` |

## 2. Gate results

Four gates, eight checks. **A container reported `running` satisfies check 4.1 and nothing else** —
it is not evidence for 4.2, 4.3 or 4.4. Every row must stand on its own observation.

| Gate | Step | Check | Result | Evidence |
|---|---|---|:-:|---|
| **Build** | E2E-EXD-2.2 | Builds for `linux/arm64` from the repository root | ✅ | image produced |
| **Push** | E2E-EXD-2.2 | Multi-arch manifest present in Harbor | ✅ | manifest published |
| **Push** | E2E-EXD-2.3 | Image runs with nothing mounted | ✅ | starts with no bind mounts — config baked into `/app` |
| **Deploy** | E2E-EXD-3.2 | Revision accepted and applied by the device | ✅ | `deviceActions: add` |
| **Function** | E2E-EXD-4.1 | Container up and not restart-looping | ✅ | container `running`, log stream advancing |
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | *harness* — `deploy-verify.sh` assertion, no verbatim line captured |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | org listing shows child device `355886333169762304` |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ✅ | `temperature_sensor: 25.057` + `Health report sent successfully` |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.
> *harness* means the check passed the `deploy-verify.sh` assertion but no verbatim log line was captured for this page. Those are listed in §5 where they matter.

## 3. Evidence log

Verbatim from `docker logs <container>` on the device, each line tagged with the check it proves:

```text
[4.4] temperature_sensor: 25.057
[4.4] [0] 25.057 -> 77.103          # °C→°F transform
[4.4] [0] 25.057 -> 25.028          # smoothing filter
[4.4] Health report sent successfully
```

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-02** | High | Fixed | config not baked into the image, so a deployed stack started with none |
| **D-11** | Low | Fixed | self-contained publish on `dotnet/runtime` shipped a second unused runtime |
| **D-12** | Low | Fixed | 252.9 MB build context, 177 MB of it gitignored logs |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

The reference case: the first example proven end to end, and the one that exposed D-02 and the `/app/.weda` persistence requirement that every later example inherited.

## 5. Not verified in this run

Nothing. All eight checks were exercised and observed.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh feature-transform-pipeline

# 2. Deploy the revision through container-management, then assert the four Act-4 checks
./scripts/deploy-verify.sh feature-transform-pipeline

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 testdevice'
```

## 7. Change record

| | |
|---|---|
| **Test run** | 2026-09-09 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Fixes landed in** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, assert 4.1–4.4 |
| **Report schema** | v1.1 — see [the schema note](README.md#report-schema) |
