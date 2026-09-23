---
doc-id:    E2E-EXD-SYSTEM_AGENT
title:     "system-agent — End-to-End Deployment Test Report"
product:   "SubNode SDK (Examples)"
version:   1.2
status:    Review
date:      2026-09-10
traces-to: E2E-EXAMPLE-IMAGES-001, docs/wiki/en/02-getting-started/09-build-push-deploy-images.md
tags:      [doc:test, feature:sub-node, service:sub-node, layer:edge, ver:1.2, status:review]
---

# system-agent — End-to-End Deployment Test Report

| Verdict | Test run | Device under test | Environment |
|:--|:--|:--|:--|
| **⏭️ SKIP** | 2026-09-10 | EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04) | SIT — `weda-sit-k3s.weda.dev`, tenant `central` |

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

Four gates, nine checks. **A container reported `running` satisfies check 4.1 and nothing else.**
4.4 is the framework heartbeat, which a SubNode emits whether or not the device it reads is
reachable; 4.5 is what this example exists to do. Every row stands on its own observation, quoted
from the device's own log in §3.

| Gate | Step | Check | Result | Evidence |
|---|---|---|:-:|---|
| **Build** | E2E-EXD-2.2 | Builds for `linux/arm64` from the repository root | ✅ | rebuilt for `linux/amd64,linux/arm64` after the D-17 fix |
| **Push** | E2E-EXD-2.2 | Multi-arch manifest present in Harbor | ✅ | manifest re-published |
| **Push** | E2E-EXD-2.3 | Image runs with nothing mounted | — | not exercised |
| **Deploy** | E2E-EXD-3.2 | Revision accepted and applied by the device | ⏭️ | **not attempted** — see §5 |
| **Function** | E2E-EXD-4.1 | Container up and not restart-looping | ⏭️ | not executed |
| **Function** | E2E-EXD-4.2 | SubNode reached **this device's** WedaNode | ⏭️ | not executed |
| **Function** | E2E-EXD-4.3 | Registered as its own child device | ⏭️ | not executed |
| **Function** | E2E-EXD-4.4 | Heartbeat reaching the cloud | ⏭️ | — |
| **Function** | E2E-EXD-4.5 | Doing what this example exists to do | ⏭️ | Ships as a product component and already runs on every WEDA Node. Deploying a second instance contends for the same host resources, so it needs a device of its own to be verified. |

> **Legend.** ✅ passed · ⚠️ partial · ❌ failed or not reached · ⏭️ not executed · — not applicable.

## 3. Evidence log

No evidence — this example was not executed in this run. See §5.

## 4. Defects

| # | Severity | Status | What it did **here** |
|---|---|---|---|
| **D-17** | High | **Fixed** in this run | the build failed outright until this run's fix: the image pinned `gpgv=2.4.4-2ubuntu17.4`, which Ubuntu has dropped from the archive |

> Defect ids are shared across the suite; the full register is [§10 of the parent report](../01-e2e-test-scenario-example-images.md).

### Analysis

**Still a deliberate SKIP, and a SKIP is still not a pass.** Its build gate did move though: D-17 broke it outright this run, and it now builds again for both architectures with the CVE floor asserted rather than pinned.

## 5. Not verified in this run

| Check left unproven | Why | What would unblock it |
|---|---|---|
| E2E-EXD-2.3 and everything in Acts 3–4 | the EPC-R7300 already runs `system-agent` as a shipped product component; a second instance would contend for host resources and port 2395 | a dedicated device with no `system-agent` already installed |

> **A gap listed here is not a pass.** It is recorded so the next run closes it rather than rediscovering it.

## 6. Re-running this test

```bash
# 1. Build and publish — the repository root is the build context; the script enforces it
./scripts/build-push-image.sh system-agent

# 2. Deploy the revision through container-management and assert 4.1-4.5
./scripts/deploy-verify.sh system-agent

# 3. Confirm on the device itself
ssh <device> 'docker logs --tail 40 weda-system-agent'
```

## 7. Change record

| | |
|---|---|
| **This run** | 2026-09-10 — EPC-R7300A1 `48b02dea8160` (ARM64, Ubuntu 20.04), SIT — `weda-sit-k3s.weda.dev`, tenant `central` |
| **Previous run** | 2026-09-09 — same device; see §4/§5 for what changed |
| **Example fixes** | [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361) |
| **Report schema + this evidence** | [PR !7396](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7396) |
| **Harness** | `deploy-verify.sh` — deploy the revision, wait for the container, score 4.1-4.5 separately and capture the full container log |
| **Report schema** | v1.3 — the function check is scored as the heartbeat (4.4) and the example's own job (4.5), from `examples/system-agent/e2e-expect.json` |
