---
doc-id:    E2E-EXD-DAQ_PROXY
title:     "daq-proxy — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.1
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# daq-proxy — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-09 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/daq-proxy`](../../../../../examples/daq-proxy/) |
| **Compose service** | `daq-proxy` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/daq-proxy:latest` |
| **Transport** | SubNode → SubNode (consumes another SubNode's telemetry) |
| **External dependency** | Pairs with `daq-collector` in production; **not required** for this run |
| **Host endpoints used** | 2395 (SubNode Web API) |
| **Required deployment overrides** | None |
| **SubNode name** | `DaqPDemo-<date>` |
| **Registered deviceId** | — not captured in this run |

## 2. Gate results

Four gates, eight checks. **A container reported `running` satisfies check 4.1 and nothing else** —
it is not evidence for 4.2, 4.3 or 4.4. Every row must stand on its own observation.

| Gate | Step | Check | Result | Evidence |
|---|---|---|:-:|---|
| **Build** | E2E-EXD-2.2 | Builds for `linux/arm64` from the repository root | ✅ | image produced |
| **Push** | E2E-EXD-2.2 | Multi-arch manifest present in Harbor | ✅ | manifest published |
| **Push** | E2E-EXD-2.3 | Image runs with nothing mounted | ✅ | starts with no bind mounts |
| **Deploy** | E2E-EXD-3.2 | Revision accepted and applied by the device | ✅ | `deviceActions: add` |
| **Function** | E2E-EXD-4.1 | Container up and not restart-looping | ✅ | container `running`, log stream advancing |
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | `nats = 2` |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | `registration = 2` |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ✅ | `telemetry = 3` |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.
> *harness* means the check passed the `deploy-verify.sh` assertion but no verbatim log line was captured for this page. Those are listed in §5 where they matter.

## 3. Evidence log

Verbatim from `docker logs <container>` on the device, each line tagged with the check it proves:

```text
[4.2–4.4] nats = 2, registration = 2, telemetry = 3
```

## 4. Defects

None. This example exposed no defect of its own; it inherited the suite-wide fixes (baked config, repository-root build context).

> Defect ids are shared across the suite; the register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

Passed unmodified, and — importantly — passes **without** its upstream `daq-collector` present. The proxy degrades cleanly rather than crash-looping when its source is absent, which is why it is a PASS while its partner is BLOCKED.

## 5. Not verified in this run

| Check left unproven | Why | What would unblock it |
|---|---|---|
| Proxying real upstream data | `daq-collector` was BLOCKED on this device (no DAQ modules), so there was no upstream | run the pair on a device with DAQ modules fitted |

> **A gap listed here is not a pass.** It is recorded so the next run closes it rather than rediscovering it.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh daq-proxy

# 2. Deploy the revision through container-management, then assert the four Act-4 checks
./scripts/deploy-verify.sh daq-proxy

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 daq-proxy'
```

## 7. Change record

| | |
|---|---|
| **Test run** | 2026-09-09 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Fixes landed in** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, assert 4.1–4.4 |
| **Report schema** | v1.1 — see [the schema note](README.md#report-schema) |
