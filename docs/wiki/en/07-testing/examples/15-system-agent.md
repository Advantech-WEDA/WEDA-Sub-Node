---
doc-id:    E2E-EXD-SYSTEM_AGENT
title:     "system-agent — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.1
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# system-agent — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **⏭️ SKIP** | 2026-09-09 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

> Method, gate definitions, fixtures, negative cases and the shared defect register live in
> **[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. This page records only
> what **this** example did. Section numbering is identical across all 15 reports.

## 1. Subject under test

| Field | Value |
|---|---|
| **Example** | [`examples/system-agent`](../../../../../examples/system-agent/) |
| **Compose service** | `system-agent` |
| **Image** | `harbor.arfa.wise-paas.com/edge-coa/system-agent:latest` |
| **Transport** | Host metrics (a shipped product component, not a sample) |
| **External dependency** | **A device of its own** |
| **Host endpoints used** | 2395 (SubNode Web API) — would collide with the running instance |
| **Required deployment overrides** | None |
| **SubNode name** | — not deployed |
| **Registered deviceId** | — not deployed |

## 2. Gate results

Four gates, eight checks. **A container reported `running` satisfies check 4.1 and nothing else** —
it is not evidence for 4.2, 4.3 or 4.4. Every row must stand on its own observation.

| Gate | Step | Check | Result | Evidence |
|---|---|---|:-:|---|
| **Build** | E2E-EXD-2.2 | Builds for `linux/arm64` from the repository root | ✅ | image produced |
| **Push** | E2E-EXD-2.2 | Multi-arch manifest present in Harbor | ✅ | manifest published |
| **Push** | E2E-EXD-2.3 | Image runs with nothing mounted | — | not exercised |
| **Deploy** | E2E-EXD-3.2 | Revision accepted and applied by the device | ⏭️ | **not attempted** — see the note below |
| **Function** | E2E-EXD-4.1 | Container up and not restart-looping | ⏭️ | not executed |
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ⏭️ | not executed |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ⏭️ | not executed |
| **Function** | E2E-EXD-4.4 | Producing its own telemetry | ⏭️ | not executed |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.
> *harness* means the check passed the `deploy-verify.sh` assertion but no verbatim log line was captured for this page. Those are listed in §5 where they matter.

## 3. Evidence log

No evidence — this example was not executed. See §5.

## 4. Defects

None. This example exposed no defect of its own; it inherited the suite-wide fixes (baked config, repository-root build context).

> Defect ids are shared across the suite; the register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

**Deliberately not deployed.** Deploying it was judged a risk to a working device rather than a test. **A SKIP is not a pass** — this example's deployability is unproven, and the suite total says so. Excluded from the base-image change (`gpgv` CVE pin, and it needs root for SUSI driver access).

## 5. Not verified in this run

| Check left unproven | Why | What would unblock it |
|---|---|---|
| E2E-EXD-2.3 and everything in Acts 3–4 | the EPC-R7300 already runs `system-agent` as a shipped product component; a second instance would contend for host resources and port 2395 | a dedicated device with no `system-agent` already installed |

> **A gap listed here is not a pass.** It is recorded so the next run closes it rather than rediscovering it.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
PLATFORMS=linux/arm64 ./scripts/build-push-image.sh system-agent

# 2. Deploy the revision through container-management, then assert the four Act-4 checks
./scripts/deploy-verify.sh system-agent

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 system-agent'
```

## 7. Change record

| | |
|---|---|
| **Test run** | 2026-09-09 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Fixes landed in** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, assert 4.1–4.4 |
| **Report schema** | v1.1 — see [the schema note](README.md#report-schema) |
