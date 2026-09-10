---
sidebar_position: 4
sidebar_label: 'Historical Report Command'
hide_title: true
title: 'Historical Report Command | SubNode SDK'
keywords: ['SubNode', 'Command', 'report.historical', 'Batch Report', 'Recording', 'Backfill', 'Historical Data']
description: 'Invoke report.historical to replay locally recorded telemetry from a SubNode to WedaCore as batched historical uploads.'
---

# Historical Report Command

> Replay telemetry the SubNode recorded on disk back to WedaCore, batched and rate-limited.

## Overview

`report.historical` is a **generic command** — every SubNode exposes it, regardless of the devices
attached (see [Command Capability Gating](./03-command-capability-gating.md)). It reads the local
recording store for a time range, converts each sensor's samples into batch telemetry messages, and
publishes them on the SubNode's batch telemetry topic while streaming acknowledgement, progress, and
final result messages back on the command response topic.

The typical use is **backfill**: the SubNode kept recording while the uplink was down, and an
operator now asks it to upload what the cloud missed.

The implementation lives in `src/Weda.SubNode.Core/Commands/Handlers/BatchReport/`.

## What You'll Learn

- The full request payload and every parameter's default and valid range
- The three-message response sequence (ack → progress → result) and how to correlate them
- Where the telemetry itself lands, and in what shape
- How `maxBatchSize`, `maxBatchesPerMessage`, and `transmissionRateLimit` interact to determine
  upload bandwidth
- Which status and error codes to expect, and what each one means operationally

## Prerequisites

- Recording enabled on the SubNode — `builder.AddRecording()`. Without it the command returns
  `STORAGE_UNAVAILABLE`.
- The SubNode registered with WedaCore (it needs an assigned batch telemetry topic).
- Familiarity with the command envelope from [Remote Control Configuration](./01-configuration.md).

---

## Request

The command arrives in the standard `CommandMessage` envelope; `data` carries the `CommandData`
fields plus a `report.historical`-specific `parameters` object.

```json
{
  "cmd": "deviceCmd",
  "seqId": 100,
  "reqSeqId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "timestamp": 1757000000000,
  "parentDeviceId": "74fe488d5d54",
  "data": {
    "deviceCmd": "report.historical",
    "timeout": 300,
    "respTopic": "eco1j.weda.74fe488d5d54.subnode.cmd.rsp",
    "parameters": {
      "timeRange": { "startTime": 1757000000000, "endTime": 1757003600000 },
      "sensorFilter": { "include": ["a1b2c"], "exclude": [] },
      "maxBatchesPerMessage": 10,
      "maxBatchSize": 5000,
      "transmissionRateLimit": 5
    }
  }
}
```

### Envelope fields (`data`)

| Field | Type | Default | Meaning |
|---|---|---|---|
| `deviceCmd` | string | — | Always `report.historical` |
| `timeout` | uint (seconds) | `30` | Hard deadline for the **whole** run. Exceeding it aborts mid-transfer with `TIMEOUT`; batches already published are not rolled back. |
| `respTopic` | string | — | Where ack/progress/result go. When empty, the command still runs but emits no responses. |

:::caution
The default `timeout` of 30 s is short for a real backfill. Size it against the expected run
duration (see [Flow control and bandwidth](#flow-control-and-bandwidth)) or the upload will be cut
off partway through.
:::

### Parameters

| Parameter | Type | Required | Default | Range | Meaning |
|---|---|---|---|---|---|
| `timeRange` | object | No | last 10 minutes | — | Query window. Omit for `now − 10 min … now`. |
| `timeRange.startTime` | int64 | Yes¹ | — | not in the future | Window start, **Unix milliseconds** |
| `timeRange.endTime` | int64 | Yes¹ | — | `> startTime` | Window end, **Unix milliseconds** |
| `sensorFilter` | object | No | all sensors | — | Sensor selection |
| `sensorFilter.include` | string[] | No | all | — | Keep only these sensor short IDs. Empty or absent means no include restriction. |
| `sensorFilter.exclude` | string[] | No | none | — | Drop these sensor short IDs. Applied **after** `include`. |
| `maxBatchesPerMessage` | int | No | `1` | 1–1000 | Maximum measure entries in one outbound message |
| `maxBatchSize` | int | No | `10000` | 1–100000 | Maximum samples buffered before a message is flushed. Larger sensor result sets are split across messages. |
| `transmissionRateLimit` | int | No | `0` | 0–10000 | Messages per second. `0` disables limiting. |

¹ Required only when `timeRange` is present.

Sensor IDs in the filter are the **short IDs** used by the recording store, matched
case-insensitively.

### Validation

Two layers reject a malformed request:

`BatchReportCommandValidator` (before dispatch):

- `timeRange.endTime` must be greater than `timeRange.startTime`
- `timeRange.startTime` must not be in the future

`BatchReportCommandHandler` (at execution, on the *effective* range after defaults):

| Check | Error code | Status |
|---|---|---|
| Recording service not configured | `STORAGE_UNAVAILABLE` | `4` HardwareError |
| SubNode not registered | `NOT_REGISTERED` | `5` PermissionDenied |
| `startTime >= endTime` | `INVALID_TIME_RANGE` | `1` ValidationFailed |
| Window wider than `MaxQueryTimeRangeDays` | `TIME_RANGE_TOO_LARGE` | `1` ValidationFailed |

Range attributes on the parameters (`maxBatchSize` 1–100000, and so on) are declared for DTDL and
portal form generation; they are not re-checked inside the handler.

---

## Response sequence

`report.historical` sets `[AutoAck(false)]` — the dispatcher does **not** send the generic
"received" ack. The handler emits its own three message types on `respTopic`, all sharing the
request's `seqId` and `reqSeqId`, distinguished by `data.msgType`.

```
   cloud ──── report.historical ───▶ SubNode
         ◀─── msgType: "ack"      ── (estimates)
         ◀─── msgType: "progress" ── (one per flushed message)
         ◀─── msgType: "progress" ──
                    ...
         ◀─── msgType: "result"   ── (final status, gaps)

         ◀═══ batch telemetry ════ (on the batch topic, not respTopic)
```

### 1. Initial ack

Sent once, before the first read, carrying the up-front estimate:

```json
{
  "cmd": "deviceCmd",
  "seqId": 100,
  "reqSeqId": "a1b2c3d4-...",
  "rspSeqId": "b2c3d4e5-...",
  "timestamp": 1757000000100,
  "deviceId": "74fe488d5d54-ffff",
  "data": {
    "deviceCmd": "report.historical",
    "msgType": "ack",
    "status": 0,
    "message": "Historical data query started",
    "resultData": {
      "estimatedBatches": 121,
      "estimatedSamples": 72020,
      "estimatedDurationSeconds": 25,
      "storageAvailable": true
    }
  }
}
```

Estimates come from each sensor's **configured report interval**, not from the store:

```
samples_i          = ceil(timeRangeMs / intervalMs_i) + 1        (interval falls back to 1000 ms)
estimatedSamples   = Σ samples_i
estimatedBatches   = Σ max(1, ceil(samples_i / maxBatchSize))
estimatedDuration  = ceil(messages / transmissionRateLimit)      when rate-limited
                   = max(1, messages / 10)                       otherwise (assumes ~10 msg/s)
```

They are a planning aid, not a promise — a sensor that reported irregularly, or whose data partly
aged out of retention, yields fewer real samples than estimated.

### 2. Progress

One per flushed message, so the progress rate equals the telemetry message rate:

```json
{
  "data": {
    "deviceCmd": "report.historical",
    "msgType": "progress",
    "status": 0,
    "message": "Progress update",
    "resultData": {
      "batchesSent": 42,
      "totalBatches": 121,
      "samplesSent": 25200,
      "totalSamples": 72020,
      "percentComplete": 34.7
    }
  }
}
```

`percentComplete` tracks **sensors processed**, not samples sent, so it advances in steps as large
as one sensor's share of the work.

### 3. Final result

```json
{
  "data": {
    "deviceCmd": "report.historical",
    "msgType": "result",
    "status": 0,
    "message": "Historical data retrieval complete",
    "resultData": {
      "batchesSent": 121,
      "totalSamples": 72020,
      "timeRange": {
        "startTime": "2026-09-04T10:00:00.000Z",
        "endTime": "2026-09-04T11:00:00.000Z"
      },
      "sensors": ["a1b2c", "d4e5f"],
      "dataGaps": []
    },
    "executedAt": 1757000000000,
    "completedAt": 1757000024200
  }
}
```

| Field | Meaning |
|---|---|
| `batchesSent` | Messages actually published (not measure entries) |
| `totalSamples` | Samples across all published messages |
| `timeRange` | The **effective** range, ISO 8601 — echoes the applied default when `timeRange` was omitted |
| `sensors` | Sensors successfully read, including those that turned out to hold nothing |
| `dataGaps` | Omitted entirely when empty; see below |

On error, `resultData` is replaced by `errorDetails`:

```json
{ "errorCode": "TIME_RANGE_TOO_LARGE", "recommendation": "Adjust time range to available period" }
```

---

## Where the telemetry goes

The samples do **not** travel on `respTopic`. They are published as `BatchTelemetrySendMessage` on
the SubNode's assigned batch telemetry topic (`dm.dt.batch.rl` in the registration response):

```json
{
  "timestamp": 1757000000500,
  "cmd": "telemetryBatch",
  "seqId": 1757000000500,
  "groupId": "74fe488d5d54-ffff",
  "deviceId": "74fe488d5d54-ffff",
  "data": {
    "measures": [
      {
        "id": "a1b2c",
        "interval": 1000,
        "startTimeStamp": 1757000000000,
        "values": [23.4, 23.5, null, 23.7]
      }
    ]
  }
}
```

| Field | Meaning |
|---|---|
| `id` | Sensor short ID (last 5 characters of the ResourceId) |
| `interval` | Spacing between consecutive values |
| `startTimeStamp` | Timestamp of `values[0]` |
| `values` | Time-ordered array; `null` marks a sample that could not be converted to a number |

Values are converted to `double`: non-finite doubles, and any type other than `double`/`int`/`long`/
`bool` (`true` → `1.0`, `false` → `0.0`), become `null`.

:::caution Units
`interval` and `startTimeStamp` are copied through from the recording store, which keeps both in
**milliseconds** (`RecordingDataPoint.Timestamp` and the recording file header are documented as
Unix ms). The `BatchTelemetryMeasureDto` contract comments describe them as seconds. Consumers
should decode what the store emits — milliseconds — and treat the contract comment as stale.
:::

---

## Data gaps

`dataGaps` is present only when at least one sensor failed to read. Each entry spans the whole query
window, because the read failed for the entire range:

```json
{
  "sensorId": "d4e5f",
  "startTime": "2026-09-04T10:00:00.000Z",
  "endTime": "2026-09-04T11:00:00.000Z",
  "reason": "sensorError",
  "missingSamples": 3601
}
```

`missingSamples` is estimated from the sensor's configured report interval, not counted.

A sensor whose recordings aged out of retention between enumeration and read returns `NotFound`
from the store. That is **not** a gap — the sensor is counted as processed and contributes nothing.
Only genuine read failures produce a gap entry.

---

## Status and error codes

| `status` | Name | When | `errorCode` |
|---|---|---|---|
| `0` | Success | At least one message sent, no failures | — |
| `1` | ValidationFailed | Bad or oversized time range | `INVALID_TIME_RANGE`, `TIME_RANGE_TOO_LARGE` |
| `3` | Timeout | Exceeded `timeout`, or externally cancelled | `TIMEOUT`, `CANCELLED` |
| `4` | HardwareError | Recording service absent, or sensor enumeration failed | `STORAGE_UNAVAILABLE`, `STORAGE_ERROR` |
| `5` | PermissionDenied | SubNode not registered | `NOT_REGISTERED` |
| `8` | PartialSuccess | Some sensors or batches failed — see `dataGaps` | — |
| `9` | NotFound | No sensor matched the filter, or no data in range | — |
| `10` | ResourceExhausted | Out of memory loading data | `RESOURCE_EXHAUSTED` |

Error responses carry a `recommendation`:

| Status | Recommendation |
|---|---|
| ValidationFailed | Adjust time range to available period |
| HardwareError | Check device storage health and retry |
| Timeout | Reduce query scope or increase timeout |
| ResourceExhausted | Reduce batch size or add rate limiting |

Note that `NotFound` (`9`) is returned as a *successful* result with zero batches — it means "the
query was valid, there was simply nothing there".

---

## Flow control and bandwidth

The buffer flushes on whichever cap trips first: `maxBatchesPerMessage` measure entries, or
`maxBatchSize` samples. After each flush the handler publishes the message, emits a progress update,
then sleeps `floor(1000 / transmissionRateLimit)` ms.

With `L̄` = mean samples per stored recording block:

```
samples per message  n_msg = min(maxBatchSize, maxBatchesPerMessage · L̄)
entries per message  m_msg = min(maxBatchesPerMessage, ceil(maxBatchSize / L̄))
```

Message size (JSON, uncompressed over NATS):

```
S_msg ≈ 156 + m_msg · 70 + n_msg · b_v      bytes
```

where `156` is the envelope (grows with the device ID), `70` is the per-measure overhead, and `b_v`
is bytes per sample including its separator — about 6 for two-decimal values, up to ~25 for full
round-trip doubles. Each flush also emits a ~300 byte progress message on `respTopic`.

Sustained uplink bitrate, when rate-limited:

```
B ≈ 8 · transmissionRateLimit · (S_msg + 300)     bit/s
T_run ≈ ceil(totalSamples / n_msg) / transmissionRateLimit    seconds
```

:::caution
`transmissionRateLimit` caps the **message rate, not the bandwidth**. Only the product
`rate × message size` bounds the link. Raising `maxBatchesPerMessage` from 10 to 200 at an unchanged
rate limit can raise the bitrate by more than an order of magnitude.
:::

To fit a link budget, size the message first, then solve for the rate:

```
transmissionRateLimit = B_target / (8 · (S_msg + 300))
```

Worked example — 20 sensors at 1 s, one hour, `maxBatchSize` 10000, `maxBatchesPerMessage` 10,
`L̄` = 60, `b_v` = 6, rate limit 5:

| Quantity | Value |
|---|---|
| Samples | 72,020 |
| Samples per message | 600 (`maxBatchesPerMessage` binds; `maxBatchSize` never reached) |
| Message size | ≈ 4.5 kB |
| Messages | 121 |
| Run duration | ≈ 24 s → **set `timeout` well above 30 s** |
| Bitrate | ≈ 190 kbit/s |

Because `floor(1000 / rate)` is integer millisecond division, any `transmissionRateLimit` above 1000
yields no delay at all. With `0`, throughput is bounded only by storage reads and NATS publishes.

---

## Storage configuration

Recording behaviour is configured under `SystemConfig:Record` (`RecordingOptions`) and governs what
`report.historical` can find:

| Option | Default | Effect on this command |
|---|---|---|
| `MaxQueryTimeRangeDays` | `30` | Windows wider than this are rejected with `TIME_RANGE_TOO_LARGE`. `0` disables the check (not recommended — it is the DoS guard). |
| `RetentionDays` | `7` | Data older than this may already be deleted; a query reaching further back silently returns less. |
| `MaxStorageSizeMb` | `1024` | Ring-buffer cap; oldest files are dropped first when exceeded. |
| `MinFreeDiskSpaceMb` | `128` | Oldest files are dropped when free space falls below this. |
| `Enabled` | `true` | When false, nothing new is recorded — existing data stays queryable. |

```csharp
builder.AddRecording(options =>
{
    options.RetentionDays = 30;
    options.MaxQueryTimeRangeDays = 30;
});
```

---

## Examples

**Backfill the default window** — everything recorded in the last 10 minutes:

```json
{ "deviceCmd": "report.historical", "timeout": 120,
  "respTopic": "...", "parameters": {} }
```

**One sensor, one hour, gentle on a cellular uplink:**

```json
{ "deviceCmd": "report.historical", "timeout": 600, "respTopic": "...",
  "parameters": {
    "timeRange": { "startTime": 1757000000000, "endTime": 1757003600000 },
    "sensorFilter": { "include": ["a1b2c"] },
    "maxBatchesPerMessage": 1,
    "maxBatchSize": 500,
    "transmissionRateLimit": 2
  } }
```

**Everything except the noisy vibration channels, as fast as the link allows:**

```json
{ "deviceCmd": "report.historical", "timeout": 900, "respTopic": "...",
  "parameters": {
    "timeRange": { "startTime": 1756990000000, "endTime": 1757003600000 },
    "sensorFilter": { "exclude": ["vib01", "vib02"] },
    "maxBatchesPerMessage": 50,
    "maxBatchSize": 20000,
    "transmissionRateLimit": 0
  } }
```

---

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| `STORAGE_UNAVAILABLE` | `AddRecording()` was never called — no recording service exists |
| `NOT_REGISTERED` | SubNode has no assigned ID yet; registration has not completed |
| `status 9` with `sensors: []` | `sensorFilter` matched nothing — the filter uses **short IDs**, not ResourceIds |
| `status 9` with sensors listed | Sensors exist but hold no data in the window — check `RetentionDays` |
| `TIMEOUT` after partial upload | `timeout` is below the real run duration; batches already sent stay in the cloud |
| `TIME_RANGE_TOO_LARGE` | Window exceeds `MaxQueryTimeRangeDays` (default 30 days) |
| Uplink saturated despite a rate limit | Message size grew — see [Flow control and bandwidth](#flow-control-and-bandwidth) |
| Nothing arrives on `respTopic` | `respTopic` was empty in the request; the run still happened |
| Timestamps land in 1970 | Consumer decoded `startTimeStamp` as seconds — the store emits milliseconds |

## Related Links

- [Command Capability Gating](./03-command-capability-gating.md)
- [Remote Control Configuration](./01-configuration.md)
- [Data Pipeline Overview](../05-data-pipeline/01-overview.md)
