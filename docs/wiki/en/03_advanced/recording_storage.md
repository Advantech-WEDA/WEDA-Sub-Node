# Recording Storage Design Document

## Overview

Recording Storage is SubNode's local offline data recording feature that stores telemetry data in binary format on local disk, supporting historical queries and offline data backfill.

### Terminology

| Term | Description |
|------|-------------|
| **Recording** | Local Storage - Writing data to local disk |
| **Reporting** | Cloud Upload - Uploading data to the cloud |

These two operations are independent: Recording ensures data is not lost, Reporting handles cloud synchronization.

---

## Architecture Design

### Domain Model
```text
┌────────────────────────────────────────────────────────────────────┐
│                           SubNode Scope                            │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│                         ┌────────────────┐*                        │
│                         │    SubNode     │                         │   - RecordingOptions (systemcfg.json)
│                         └───────┬────────┘                         │
│                                 │                                  │
│         ┌───────────────────────┼───────────────────────┐          │
│   ┌─────┴─────┐           ┌─────┴─────┐           ┌─────┴────┐     │
│   │ Device A  │           │ Device B  │           │ Device C │     │   - X
│   └─────┬─────┘           └─────┬─────┘           └─────┬────┘     │
│         │                       │                       │          │
│   ┌─────┴─────┐           ┌─────┴─────┐           ┌─────┴─────┐    │
│   │ Sensor A1 ├┐          │ Sensor B1 ├┐          │ Sensor C1 ├┐   │   - SensorRecordingConfig (devicecfg.json)
│   └┬──────────┘├┐         └┬──────────┘├┐         └┬──────────┘├┐  │
│    └┬──────────┘│          └┬──────────┘│          └┬──────────┘│  │
│     └───────────┘           └───────────┘           └───────────┘  │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

### Clean Architecture Layers

```text
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│                    Weda.SubNode.Core                                                    │
├─────────────────────────────────────────────────────────────────────────────────────────┤
│  ┌────────────────────────────────────────────────────┐  ┌────────────────────────────┐ │
│  │ Application                                        │  │ Infrastructure             │ │
│  │  ┌──────────────────┐  ┌─────────────────────────┐ │  │ ┌────────────────────────┐ │ │
│  │  │ RecordingService │  │ RecordingCleanupService │ │  │ │ BinaryRecordingStorage │ │ │
│  │  └────────┬─────────┘  └────────────┬────────────┘ │  │ └─┬──────────────────────┘ │ │
│  └───────────│─────────────────────────│──────────────┘  └───│────────────────────────┘ │
│              │                         │                     │                          │
└──────────────│─────────────────────────│─────────────────────│──────────────────────────┘
               │                         │                     │
               ▼                         ▼                     ▼
┌────────────────────────────────────────────────────────────────────┐
│                    Weda.SubNode.Abstractions                       │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ Application Contracts                                        │  │
│  │  ┌───────────────────┐  ┌──────────────────────────────────┐ │  │
│  │  │ IRecordingStorage │  │ RecordingDataPoint               │ │  │
│  │  └───────────────────┘  │ RecordingFileHeader              │ │  │
│  │                         │ RecordingStorageStatistics       │ │  │
│  │                         └──────────────────────────────────┘ │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │ Domain (Entities)    refer. to shadow                        │  │
│  │  ┌──────────────────────┐  ┌───────────────────────────────┐ │  │
│  │  │ RecordingOptions     │  │ SensorRecordingConfig         │ │  │
│  │  │ (systemcfg.json)     │  │ (devicecfg.json)              │ │  │
│  │  └──────────────────────┘  └───────────────────────────────┘ │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
```

---

## Configuration Hierarchy

### Level 1: SubNode (systemcfg.json)

`RecordingOptions` - Global storage settings

```json
{
  "Record": {
    "MinFreeDiskSpaceMb": 128,
    "MaxStorageSizeMb": 1024,
    "RetentionDays": 7
  }
}
```

| Property | Type | Default | Range | Configurable | Description |
|----------|------|---------|-------|--------------|-------------|
| `StorageDirectory` | string | `"./data/recording"` | Fixed | No | Storage directory path (fixed value) |
| `MinFreeDiskSpaceMb` | int | `128` | 10-10000 | Yes | Minimum free disk space (MB) |
| `MaxStorageSizeMb` | int | `0` | 0-500000 | Yes | Max total size for recording files (MB, 0=disabled) |
| `RetentionDays` | int | `7` | 0-180 | Yes | Data retention days (0=unlimited) |

### Level 2: Sensor (devicecfg.json)

`SensorRecordingConfig` - Sensor-level settings

```json
{
  "Sensors": [
    {
      "Name": "temp.sensor",
      "Record": {
        "Enabled": true,
        "Interval": 1000
      },
      "Report": {
        "Enabled": true,
        "Interval": 1000
      }
    }
  ]
}
```

| Property | Type | Default | Range | Description |
|----------|------|---------|-------|-------------|
| `Enabled` | bool | `true` | - | Whether to enable Recording for this Sensor |
| `Interval` | int | `0` | 0, or ≥ Report.Interval | Recording interval (milliseconds). 0 = same as Report.Interval |

### Downsampling Design

Recording interval must be **greater than or equal to** Report interval. Recording downsamples from Report data:

```
Constraint: Report.Interval ≤ Recording.Interval

Example:
  Report.Interval = 2000ms (2s)
  Recording.Interval = 5000ms (5s)
```

**Downsampling Behavior**:

```
Report (2s interval):
Timestamp:  0     2     4     6     8     10    12    14    16    18    20
Value:      v0    v1    v2    v3    v4    v5    v6    v7    v8    v9    v10
            ↓              ↓         ↓              ↓         ↓
Recording (5s interval, aligned to slot boundary):
Slot Index: 0              1         2              3         4
Slot Time:  0              5         10             15        20
Value:      v0             v3        v5             v8        v10
```

**Logic Explanation**:
- Slot 0 (0~5ms): ts=0 enters slot 0 → record v0
- Slot 1 (5~10ms): ts=6 first enters slot 1 → record v3
- Slot 2 (10~15ms): ts=10 first enters slot 2 → record v5
- Slot 3 (15~20ms): ts=16 first enters slot 3 → record v8
- Slot 4 (20~25ms): ts=20 first enters slot 4 → record v10

**Key Points**:
- Recording timestamps are **aligned to Recording.Interval boundaries** (0, 5, 10, 15, ...)
- NOT aligned to actual Report timestamps
- When Report timestamp crosses a new slot boundary, that value is recorded
- Enables O(1) random access: `slotIndex = timestamp / recordingInterval`

**Slot Recording Logic**:

```csharp
// When Report data arrives
var slotIndex = (timestamp - startOfDay) / recordingInterval;

// Only record if this is a new slot (not already recorded)
if (slotIndex > lastRecordedSlot)
{
    RecordToSlot(slotIndex, value);
    lastRecordedSlot = slotIndex;
}
```

---

## Storage Design

### Directory Structure

```
{StorageDirectory}/
└── {SensorId}/
    ├── 2026-01-15_1000.bin    ← {date}_{interval}.bin format
    ├── 2026-01-15_5000.bin    ← Different interval on same day
    ├── 2026-01-16_1000.bin
    └── 2026-01-17_1000.bin
```

> **Note**: `SensorId` is composed of `SubNodeId + Device.Name + Sensor.Name`, ensuring uniqueness.

### Dynamic Interval Support

When `Recording.Interval` changes during operation, a new file with different interval is created:

```
Before: Recording.Interval = 1000ms
File: 2026-01-19_1000.bin

↓ Cloud updates Recording.Interval to 5000ms

After: Recording.Interval = 5000ms
File: 2026-01-19_5000.bin (new file created)
```

**Design Decisions**:

| Decision | Choice | Reason |
|----------|--------|--------|
| Index File | **No** | File count per sensor is limited (2-3 interval files per day), directory scan is efficient enough. Avoids index-file sync issues. |
| File Discovery | Directory Scan | Use glob pattern `{date}_*.bin` to find all interval files for a date |
| Read Strategy | Segment Reporting | Return data grouped by interval when querying across multiple interval files |

**Query Flow** (ReadAsync):
```
1. Scan directory for files matching date range
2. For each file, read header to get interval
3. Read data from each file
4. Return combined results ordered by timestamp
```

**Multiple Interval Changes on Same Day Example**:

```
Timeline (2026-01-19):
00:00 ─────────── 08:00 ─────────── 14:00 ─────────── 24:00
│                  │                  │                  │
│  interval=1000ms │  interval=5000ms │  interval=1000ms │
│                  │                  │                  │
└──────────────────┴──────────────────┴──────────────────┘

Generated files:
2026-01-19_1000.bin  ← 00:00~08:00 data + 14:00~24:00 data (appended to same file)
2026-01-19_5000.bin  ← 08:00~14:00 data
```

**Key Points**:
- When switching back to the same interval, data is written to the same file (appended to corresponding slots)
- No duplicate files are created
- Slot-based design ensures data from different time periods doesn't overwrite each other

### Binary File Format

#### Header (24 bytes)

```
┌────────┬──────┬─────────┬─────────────────────────────────┐
│ Offset │ Size │ Type    │ Field                           │
├────────┼──────┼─────────┼─────────────────────────────────┤
│ 0      │ 4    │ uint32  │ Prefix (0x57454441 "WEDA")      │
│ 4      │ 2    │ uint16  │ Version (1)                     │
│ 6      │ 1    │ byte    │ Flags (Preserved)               │
│ 7      │ 1    │ byte    │ ChecksumType                    │
│ 8      │ 4    │ uint32  │ Interval (ms)                   │
│ 12     │ 8    │ ulong   │ StartTimestamp (Unix ms)        │
│ 20     │ 4    │ uint32  │ SlotCount                       │
└────────┴──────┴─────────┴─────────────────────────────────┘
Total: 24 bytes
```

#### Data Section (Slot-based Storage)

**Slot Concept**: Divides one day (86,400,000 ms) into fixed-size time slots.

```
Interval = 1000ms (1 second), one day = 86,400 slots

Timeline:
00:00:00.000 ──────────────────────────────────────> 23:59:59.999
   │           │           │           │                │
   Slot 0      Slot 1      Slot 2      ...          Slot 86399
```

**File Structure**:

```
┌───────────────────────────────────────────────────────────┐
│ Slot 0  │ double (8 bytes) │ Value or NaN                 │
│ Slot 1  │ double (8 bytes) │ Value or NaN                 │
│ ...     │ ...              │ ...                          │
│ Slot N  │ double (8 bytes) │ Value or NaN                 │
└───────────────────────────────────────────────────────────┘
```

**Slot Calculations**:

| Item | Formula | Example (Interval=1000ms) |
|------|---------|---------------------------|
| SlotCount | `86400000 / interval` | 86,400 slots |
| SlotIndex | `(timestamp - startTimestamp) / interval` | - |
| File Size | `HeaderSize + SlotCount × 8` | 24 + 691,200 = 691,224 bytes (~675 KB) |

**NaN Filling**:
- Slots without data are filled with `double.NaN`
- Can be identified as "no data at this time point" when reading
- Ensures fixed file size, supports random access

### Ring Buffer FIFO

When disk space is insufficient:
1. Calculate the space that needs to be freed
2. Delete oldest files in date order
3. Continue deleting until enough space is available or no files remain

---

## Interface Design

### IRecordingStorage

```csharp
public interface IRecordingStorage
{
    Task WriteAsync(
        string sensorId,
        RecordingDataPoint dataPoint,
        CancellationToken cancellationToken = default);

    Task WriteBatchAsync(
        string sensorId,
        IEnumerable<RecordingDataPoint> dataPoints,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RecordingDataPoint>> ReadAsync(
        string sensorId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default);

    Task<RecordingStorageStatistics> GetStatisticsAsync(
        string sensorId,
        CancellationToken cancellationToken = default);

    Task CleanupAsync(
        DateTimeOffset before,
        CancellationToken cancellationToken = default);
}
```

### RecordingDataPoint

```csharp
public readonly record struct RecordingDataPoint(
    long Timestamp,    // Unix timestamp in milliseconds
    double Value       // Sensor value (NaN for missing)
);
```

### RecordingStorageStatistics

```csharp
public record RecordingStorageStatistics(
    long TotalBytes,
    int FileCount,
    DateTimeOffset? OldestData,
    DateTimeOffset? NewestData
);
```

---

## API Design

### NATS Topic Architecture

```json
{
  "natsTopicAssignments": {
    "telemetryTopic": "{protoVer}.{deviceId}.telemetry",
    "batchTelemetryTopic": "{protoVer}.{deviceId}.telemetry-batch"
  }
}
```

| Topic | Purpose | Direction |
|-------|---------|-----------|
| `telemetryTopic` | Real-time telemetry reporting | SubNode → Cloud |
| `batchTelemetryTopic` | Historical data batch reporting (backfill) | SubNode → Cloud |

### Telemetry Topic (Real-time Reporting)

Uses `TelemetrySendMessage` format:

```json
{
  "seqId": 1,
  "timestamp": 1739266815469,
  "data": {
    "measures": [
      { "sensorId": "f782c", "value": 25.3, "timestamp": 1742683150000 }
    ]
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `seqId` | int | Auto-incrementing sequence number (per message type) |
| `timestamp` | long | Message timestamp (Unix ms) |
| `measures[].sensorId` | string | Sensor Short ID (last 5 chars of ResourceId) |
| `measures[].value` | object | Sensor value |
| `measures[].timestamp` | long | Data timestamp (Unix ms) |

> Reference: [TelemetrySendMessage.cs](../../src/Weda.SubNode.Abstractions/Cloud/Clients/Telemetry/Contracts/TelemetrySendMessage.cs)

### Batch Telemetry Topic (Historical Backfill)

Batch historical data reporting for offline period data backfill:

```json
{
  "timestamp": 1739266815469,
  "cmd": "telemetryBatch",
  "seqId": 1739266815469,
  "groupId": "group1",
  "deviceId": "test1234",
  "data": {
    "measures": [
      {
        "id": "5176e",
        "interval": 600,
        "startTimeStamp": 1742683150,
        "values": [25.3, 26.1, 20.9]
      },
      {
        "id": "f30f3",
        "interval": 500,
        "startTimeStamp": 1742683150,
        "values": [101325, 101400, 101100]
      }
    ]
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `measures[].id` | string | Sensor identifier |
| `measures[].interval` | int | Data interval (seconds) |
| `measures[].startTimeStamp` | long | Start time (Unix seconds) |
| `measures[].values` | double[] | Value array (ordered by time, `null` indicates no data) |

### Data Flow

```
┌──────────────────────────────────────────────────────────────────────────┐
│                              SubNode                                      │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                           │
│  Sensor Data ──┬──> TelemetryPipeline ──> telemetryTopic ──> Cloud       │
│                │                                                          │
│                └──> RecordingStorage ──> .bin files (local)               │
│                          ▲                                                │
│                          │ ReadAsync() [Future: Historical Query]         │
│                          │                                                │
│  Cloud ──> cmd.req ──> CommandHandler ──> batchTelemetryTopic ──> Cloud  │
│       (REPORT command)       │                                            │
│                              └──> cmd.rsp (Completed)                     │
│                                                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

> **Note**: Historical Query (REPORT command) is for future implementation. Current Epic 3 only includes Recording Storage.

---

## Acceptance Criteria Mapping

| AC | Implementation |
|----|----------------|
| When `storageRetentionDays` is updated, adjust cleanup logic | `RecordingCleanupService` reads `RecordingOptions.RetentionDays` |
| Overwrite oldest data when local storage is full (Ring Buffer FIFO) | `BinaryRecordingStorage.EnsureDiskSpace()` deletes files by date |
| Recording toggle | `SensorRecordingConfig.Enabled` controls Sensor-level toggle |
| Daily rotation | File naming `{date}.bin`, automatically creates new file daily |

---

## Future Design

### Storage Backend Extension

Currently uses Binary file format. Future extensions via `IRecordingStorage` interface:

```
IRecordingStorage (interface)
    ├── BinaryRecordingStorage   ← Current implementation
    ├── SqliteRecordingStorage   ← Future addition
    └── ...
```

To support multiple storage backends, add `StorageType` to `RecordingOptions`:

```csharp
public class RecordingOptions
{
    public RecordingStorageType StorageType { get; set; } = RecordingStorageType.Binary;
    // ...
}

public enum RecordingStorageType
{
    Binary,   // .bin files (default)
    Sqlite    // SQLite database
}
```

DI registration selects implementation based on configuration:

```csharp
services.AddSingleton<IRecordingStorage>(sp =>
{
    var options = sp.GetRequiredService<IOptions<RecordingOptions>>().Value;
    return options.StorageType switch
    {
        RecordingStorageType.Sqlite => new SqliteRecordingStorage(...),
        _ => new BinaryRecordingStorage(...)
    };
});
```

> **Note**: Currently keeping KISS, only implementing `BinaryRecordingStorage`. Interface is abstracted for easy future extension.

### Storage Backend Selection Guide

#### Binary File vs SQLite Comparison

| Aspect | Binary File | SQLite |
|--------|-------------|--------|
| Write Speed | Fastest (direct I/O) | Medium (WAL + transaction) |
| Rotation | `File.Delete()` completes instantly | Slow (requires VACUUM to reclaim space) |
| File Size | Fixed (header + slots) | Dynamic growth + overhead |
| Random Access | O(1) slot seek | O(log n) index lookup |
| Query Flexibility | Low (requires custom implementation) | High (SQL syntax) |
| Cross-Sensor Query | Requires reading multiple files | Single SQL |
| Corruption Recovery | Single file corruption doesn't affect others | Entire DB may corrupt |
| Dependencies | None | Requires SQLite library |

#### Scenario Selection Recommendations

| Deployment Scenario | Recommended | Reason |
|---------------------|-------------|--------|
| **Embedded Device** | Binary File | Fast write, fast rotation, no dependencies |
| **Powerful Device + Admin API** | SQLite | Complex queries, aggregations, tool ecosystem |
| **Remote/Multi-user Access** | PostgreSQL | Concurrency, permissions, network access |

#### SQLite Use Cases

When device is powerful enough and needs Admin API, SQLite advantages:

```sql
-- Complex conditional queries
SELECT * FROM recordings
WHERE sensor_id = 'xxx'
  AND timestamp BETWEEN @start AND @end
  AND value > 100

-- Aggregate statistics
SELECT AVG(value), MAX(value), MIN(value)
FROM recordings
WHERE sensor_id = 'xxx'

-- Cross-Sensor comparison
SELECT a.value, b.value
FROM recordings a
JOIN recordings b ON a.timestamp = b.timestamp
WHERE a.sensor_id = 'temp' AND b.sensor_id = 'humidity'
```

> **Current Choice: Binary File**: Because main requirements are "fast write + fast rotation", and Query is rarely used.

---

## Related Documents

- [appsettings_configuration.md](appsettings_configuration.md) - Application Settings
- [telemetry_event_hooks.md](telemetry_event_hooks.md) - Telemetry Event Hooks
