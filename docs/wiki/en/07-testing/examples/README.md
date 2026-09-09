# Per-Example End-to-End Deployment Test Reports

One report per SubNode example, recording what it actually did when built, published to Harbor and
deployed to a real device through the container-management API.

The shared method — gates, step format, fixtures, defect register, exit criteria — lives in
**[E2E-EXAMPLE-IMAGES-001](../01-e2e-test-scenario-example-images.md)**. These pages record only
per-example results, so read that first.

**Device under test:** EPC-R7300A1 `48b02dea8160` — ARM64, Ubuntu 20.04, SIT.

**Change record:** example fixes in [PR #7361](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7361); the unrelated CI integration-test failure (D-16) in [PR #7363](https://dev.azure.com/Advantech-EBO/IoT%20Platform/_git/edge_subnode/pullrequest/7363).

## Results

| # | Example | Verdict | External dependency |
|---|---|---|---|
| 01 | [`feature-transform-pipeline`](01-feature-transform-pipeline.md) | ✅ PASS | none — embeds its simulator |
| 02 | [`feature-custom-commands`](02-feature-custom-commands.md) | ✅ PASS | none — embeds its simulator |
| 03 | [`http-air-quality`](03-http-air-quality.md) | ✅ PASS | internet (MOENV API) |
| 04 | [`http-stock-quotes`](04-http-stock-quotes.md) | ✅ PASS | internet (TWSE) |
| 05 | [`feature-aggregation`](05-feature-aggregation.md) | ✅ PASS | none — embeds two simulators |
| 06 | [`opcua-basic`](06-opcua-basic.md) | ✅ PASS | none — embeds its simulator |
| 07 | [`modbus-wise4012`](07-modbus-wise4012.md) | ✅ PASS | real WISE-4012 |
| 08 | [`modbus-wise4012-builder`](08-modbus-wise4012-builder.md) | ⚠️ PARTIAL | real WISE-4012 (unreachable at test time) |
| 09 | [`mqtt-image-chunked`](09-mqtt-image-chunked.md) | ✅ PASS | MQTT broker (bundled) |
| 10 | [`mqtt-isensing-wise4012`](10-mqtt-isensing-wise4012.md) | ✅ PASS | MQTT broker (bundled) |
| 11 | [`vision-object-detection`](11-vision-object-detection.md) | ✅ PASS | MQTT broker (bundled) |
| 12 | [`daq-proxy`](12-daq-proxy.md) | ✅ PASS | pairs with `daq-collector` |
| 13 | [`cfx-endpoint`](13-cfx-endpoint.md) | ✅ PASS | CFX bridge (bundled RabbitMQ) |
| 14 | [`daq-collector`](14-daq-collector.md) | ❌ BLOCKED | **real DAQ modules — absent** |
| 15 | [`system-agent`](15-system-agent.md) | ⏭️ SKIP | **a device of its own** |

**13 PASS · 1 PARTIAL · 1 BLOCKED · 1 SKIP** — and a SKIP is not a pass.

> `modbus-wise4012-builder` is counted separately from the 13: its SubNode side is healthy
> (registered, 0 errors) and only its Modbus target was unreachable during the run.

## How to read a verdict

| Verdict | Meaning |
|---|---|
| **PASS** | Container running with 0 restarts, NATS connected, registration accepted, telemetry flowing |
| **PARTIAL** | SubNode reached the cloud, but its field device or upstream was unavailable |
| **BLOCKED** | Deploys and runs; cannot function without hardware this device lacks |
| **SKIP** | Not executed, with the reason recorded. **Never counts as a pass** |

A container reported `running` proves only that the process has not exited. Three of this suite's
defects produced a `running` container that was doing nothing at all.
