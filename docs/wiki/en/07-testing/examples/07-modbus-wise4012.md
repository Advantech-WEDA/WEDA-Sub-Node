---
doc-id:    E2E-EXD-MODBUS_WISE4012
title:     "modbus-wise4012 — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.1
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# modbus-wise4012 — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **✅ PASS** | 2026-09-09 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/modbus-wise4012`](../../../../../examples/modbus-wise4012/) |
| **Compose service** | `wise-4012` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/modbus-wise4012:latest` |
| **Transport** | Modbus TCP |
| **External dependency** | **Real WISE-4012 at `172.16.8.122:502`** |
| **Host endpoints used** | 2395 (SubNode Web API) |
| **Required deployment overrides** | None |
| **SubNode name** | `ModbusDemo-<date>` |
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
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ✅ | reached the **device's own** dmagent, not a remote one — the point of the D-01 re-verification |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ✅ | *harness* — `deploy-verify.sh` assertion, no verbatim line captured |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ✅ | *harness* — `deploy-verify.sh` assertion, no verbatim line captured |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.
> *harness* means the check passed the `deploy-verify.sh` assertion but no verbatim log line was captured for this page. Those are listed in §5 where they matter.

## 3. Evidence log

Verbatim from `docker logs <container>` on the device, each line tagged with the check it proves:

```text
[4.2–4.4] nats + registration + telemetry against the device's own dmagent
```

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-01** | High | Fixed | baked the developer-machine WedaNode URL `172.22.160.197:4224` |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

**Before D-01 was fixed this appeared to pass** — but it was registering against a *remote* WedaNode, not the device it ran on. The re-verification after the fix is what makes 4.2 meaningful here: the check is not “NATS connected”, it is “connected to **this** device”.

## 5. Not verified in this run

| Check left unproven | Why | What would unblock it |
|---|---|---|
| Verbatim evidence for 4.3–4.4 | recorded only as the summary “nats + registration + telemetry” | re-run with `deploy-verify.sh --capture` and paste the matched lines |

> **A gap listed here is not a pass.** It is recorded so the next run closes it rather than rediscovering it.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh modbus-wise4012

# 2. Deploy the revision through container-management, then assert the four Act-4 checks
./scripts/deploy-verify.sh modbus-wise4012

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 wise-4012'
```

## 7. Change record

| | |
|---|---|
| **Test run** | 2026-09-09 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Fixes landed in** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, assert 4.1–4.4 |
| **Report schema** | v1.1 — see [the schema note](README.md#report-schema) |
