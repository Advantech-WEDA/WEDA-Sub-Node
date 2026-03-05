# Large Binary Data Handling

## Overview

SubNode handles large data (such as images and binary blobs) uploaded to the Cloud while respecting **NATS max_payload = 1MB** limitation. This document describes the three-layer protection strategy that ensures large data can be safely and reliably transmitted via NATS.

### Three-Layer Strategy Overview

```
Large Data Upload Strategy:

+---------------------------------------------------------------------+
|                                                                     |
|  Layer 1: Active Rejection                                          |
|  +- Timing: Validation stage (Pipeline Stage 0)                     |
|  +- Condition: Base64 data > MaxBinarySize (10MB)                   |
|  +- Behavior: Reject processing, Log Warning, Skip Transform/Filter |
|                                                                     |
|  Layer 2: Auto-Chunking (at 750KB)                                  |
|  +- Timing: DTO conversion stage (TelemetryMeasureDto.From)         |
|  +- Condition: Base64 string > ChunkSize (750KB)                    |
|  +- Behavior: Auto-split, each chunk <= 750KB, with chunk metadata  |
|                                                                     |
|  Layer 3: Custom Chunking (ChunkingTransform)                       |
|  +- Timing: Transform stage (Pipeline Stage 1)                      |
|  +- Condition: User-configured chunkSize (1KB ~ 750KB)              |
|  +- Behavior: Split by custom size, for bandwidth-limited scenarios |
|                                                                     |
+---------------------------------------------------------------------+
```

---

## Architecture: Data Flow Through Pipeline

```
+-----------------------------------------------------------------------------+
|                        TelemetryPipeline.ProcessAsync()                     |
+-----------------------------------------------------------------------------+
|                                                                             |
|  TelemetryMeasure[]                                                         |
|       |                                                                     |
|       v                                                                     |
|  +---------------------------------------------+                            |
|  | Stage 0: Validate (SchemaBasedValidator)    | <-- Layer 1: Rejection     |
|  |                                             |                            |
|  |  schema contains '/' -> ValidateBase64()    |                            |
|  |  +- size > 10MB -> REJECT (Error)           |                            |
|  |  +- invalid Base64 -> REJECT (Error)        |                            |
|  |  +- valid -> PASS                           |                            |
|  +---------------------+-----------------------+                            |
|       valid            |  invalid                                           |
|       measures         |  measures                                          |
|                        |                                                    |
|       +----------------+-------------------+                                |
|       v                                    v                                |
|  +--------------------------+   +-----------------------+                   |
|  | Stage 1: Transform       |   | Skip Transform/Filter | <-- Rejected      |
|  |                          |   | Go directly to Send   |     measures      |
|  | if ChunkingTransform     |   +-----------+-----------+                   |
|  |   configured:            |               |                               |
|  |   +- size > chunkSize    | <-- Layer 3   |                               |
|  |   |  -> split into chunks|               |                               |
|  |   +- size <= chunkSize   |               |                               |
|  |      -> pass through     |               |                               |
|  +-------------+------------+               |                               |
|                |                            |                               |
|                v                            |                               |
|  +--------------------------+               |                               |
|  | Stage 2: Filter (DSP)    |               |                               |
|  +-------------+------------+               |                               |
|                |                            |                               |
|                v                            |                               |
|  +--------------------------+               |                               |
|  | Stage 3: Send            |<--------------+                               |
|  | WedaCloudService         |                                               |
|  |   .SendTelemetryAsync()  |                                               |
|  +-------------+------------+                                               |
|                |                                                            |
|                v                                                            |
|  +------------------------------------------+                               |
|  | TelemetryClient.SendTelemetryAsync()     |                               |
|  |                                          |                               |
|  | TelemetryMeasureDto.From(measures)       | <-- Layer 2: Auto-Chunking    |
|  |   +- Base64 > 750KB -> auto chunk        |     (Final safety net)        |
|  |   +- JSON string -> JsonElement          |                               |
|  |   +- other -> pass through               |                               |
|  |                                          |                               |
|  | if chunked:                              |                               |
|  |   -> SendChunkedTelemetryAsync()         |                               |
|  |   -> Publish each chunk separately       |                               |
|  | else:                                    |                               |
|  |   -> SendNormalTelemetryAsync()          |                               |
|  |                                          |                               |
|  | NATS.PublishAsync(message)               |                               |
|  +------------------------------------------+                               |
|                                                                             |
+-----------------------------------------------------------------------------+
```

---

## Layer 1: Active Rejection

### Purpose

Intercept oversized data at the earliest stage of the Pipeline to avoid wasting compute resources on Transform/Filter stages.

### Trigger Conditions

- Sensor schema contains `/` (MIME type, e.g., `image/jpeg`, `application/octet-stream`)
- Base64 encoded string length > `MaxBinarySize` (default: 10MB)

### Implementation Location

**`SchemaBasedValidator.cs`** -> `ValidateBase64()`

```csharp
private static ErrorOr<Success> ValidateBase64(object value, int maxSize)
{
    if (value is not string s)
        return Error.Failure($"Expected Base64 string, got {value.GetType().Name}");

    if (string.IsNullOrEmpty(s))
        return Error.Failure("Base64 string cannot be empty");

    if (s.Length > maxSize)          // <-- Active rejection
        return Error.Failure(
            $"Base64 data exceeds maximum size of {maxSize / 1024 / 1024}MB");

    Convert.FromBase64String(s);     // <-- Format validation
    return Result.Success;
}
```

### Validation Flow

```
TelemetryMeasure
       |
       v
+------------------+
| Schema contains  |
| '/' (MIME type)? |
+--------+---------+
    Yes  |  No
         |   +---> Other validation (numeric/boolean/string)
         v
+------------------+
| application/json?|
+--------+---------+
    Yes  |  No
         |   +---> ValidateJson
         v
+------------------+
| ValidateBase64   |
+--------+---------+
         |
         v
+------------------+     +------------------+
| value is string? |--No-| X REJECT         |
+--------+---------+     | Expected Base64  |
    Yes  |               +------------------+
         v
+------------------+     +------------------+
| isEmpty?         |--Yes| X REJECT         |
+--------+---------+     | Cannot be empty  |
    No   |               +------------------+
         v
+------------------+     +------------------+
| length > 10MB?   |--Yes| X REJECT         |
+--------+---------+     | Exceeds max size |
    No   |               +------------------+
         v
+------------------+     +------------------+
| Valid Base64     |--No-| X REJECT         |
| format?          |     | Invalid format   |
+--------+---------+     +------------------+
    Yes  |
         v
   +----------+
   | OK PASS  |
   +----------+
```

### Configuration

```csharp
// TelemetryOptions.cs
public class TelemetryOptions
{
    /// <summary>
    /// Maximum allowed for binary/image data in bytes.
    /// Data exceeding this size will be rejected.
    /// Default is 10MB.
    /// </summary>
    public int MaxBinarySize { get; set; } = 10 * 1024 * 1024;
}
```

---

## Layer 2: Auto-Chunking (at 750KB)

### Purpose

Acts as the **final safety net** before data is sent via NATS. Automatically splits Base64 data exceeding 750KB to ensure each NATS message stays under the 1MB payload limit.

### Trigger Conditions

- Value is a string and NOT JSON
- String length > `ChunkSize` (default: 750KB = 768,000 bytes)

### Implementation Location

**`TelemetrySendMessage.cs`** -> `TelemetryMeasureDto.From()`

```csharp
public static TelemetryDataDto From(List<TelemetryMeasure> measures, TelemetryOptions options)
{
    foreach (var m in measures)
    {
        var adapted = AdaptValue(m.Value);

        if (adapted is string base64
            && base64.Length > options.ChunkSize   // <-- Exceeds 750KB
            && !IsJsonString(base64))               // <-- Not JSON
        {
            result.AddRange(CreateChunks(m, base64, options.ChunkSize));
        }
        else
        {
            result.Add(/* normal DTO */);
        }
    }
}
```

### Chunk Structure

Each chunk contains the following metadata:

```json
{
  "sensorId": "f782c",
  "value": "<base64_chunk_data>",
  "timestamp": 1706500000000,
  "metadata": {
    "transferId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "chunkIndex": 0,
    "totalChunks": 3,
    "crc32Checksum": 1234567890
  }
}
```

| Metadata Field | Type | Description |
|----------------|------|-------------|
| `transferId` | string (GUID) | Unique identifier for the same image/data, used for reassembly |
| `chunkIndex` | int | Chunk number (0-based) |
| `totalChunks` | int | Total number of chunks |
| `crc32Checksum` | uint | CRC32 checksum of original Base64 string for integrity verification |

### Per-Chunk Publishing

When chunked data is detected (via `transferId` in metadata), `TelemetryClient` publishes each chunk as a separate NATS message to ensure each message stays under 1MB:

```csharp
// TelemetryClient.cs
if (dto.Measures.Any(m => m.Metadata?.ContainsKey("transferId") == true))
{
    return await SendChunkedTelemetryAsync(deviceId, dto, topicAssignments, ct);
}
```

### Chunking Example

```
Original data: 2MB Base64 string (image/jpeg)
ChunkSize: 750KB (768,000 chars)

Chunking result:
+------------------------------------------------------------------+
| Chunk 0                                                          |
| +- transferId: "a1b2c3d4..."                                     |
| +- chunkIndex: 0                                                 |
| +- totalChunks: 3                                                |
| +- crc32Checksum: 1234567890                                     |
| +- value: base64[0..768000]                                      |
+------------------------------------------------------------------+
| Chunk 1                                                          |
| +- transferId: "a1b2c3d4..." (same GUID)                         |
| +- chunkIndex: 1                                                 |
| +- totalChunks: 3                                                |
| +- value: base64[768000..1536000]                                |
+------------------------------------------------------------------+
| Chunk 2                                                          |
| +- transferId: "a1b2c3d4..." (same GUID)                         |
| +- chunkIndex: 2                                                 |
| +- totalChunks: 3                                                |
| +- value: base64[1536000..2097152]                               |
+------------------------------------------------------------------+

Each chunk is published as a separate NATS message
Receiver (Transceiver) reassembles by transferId
CRC32 checksum verifies data integrity after reassembly
```

### Configuration

```csharp
// TelemetryOptions.cs
public class TelemetryOptions
{
    /// <summary>
    /// Chunk size threshold for automatic chunking in bytes.
    /// Base64 strings larger than this will be automatically split.
    /// Default is 750KB to stay under NATS 1MB payload limit.
    /// </summary>
    public int ChunkSize { get; set; } = 750 * 1024;
}
```

---

## Layer 3: Custom Chunking (ChunkingTransform)

### Purpose

Provides a **configurable chunking Transform** that allows users to split data with smaller chunk sizes (e.g., 256KB) in bandwidth-limited scenarios.

### Comparison with Layer 2

| Aspect | Layer 2 (Auto-Chunking) | Layer 3 (ChunkingTransform) |
|--------|-------------------------|------------------------------|
| Location | DTO conversion (final safety net) | Pipeline Transform stage |
| Default enabled | **Always enabled** | **Manual configuration required** |
| Chunk Size | Fixed 750KB | Configurable 1KB ~ 750KB |
| Default size | 750KB | 256KB |
| Use case | All MIME type sensors | Bandwidth-limited scenarios |
| JSON handling | No chunking for JSON | No chunking for JSON |

### Implementation Location

**`ChunkingTransform.cs`**

```csharp
public class ChunkingTransform : ITelemetryTransform, IConfigurableTransform<ChunkingTransform>
{
    private int _chunkSize = 256 * 1024; // Default 256KB

    public static string TypeName => "chunking";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken ct = default)
    {
        foreach (var measure in measures)
        {
            if (measure.Value is string base64
                && base64.Length > _chunkSize
                && !IsJsonString(base64))
            {
                result.AddRange(CreateChunks(measure, base64));
            }
            else
            {
                result.Add(measure);
            }
        }
        return Task.FromResult(result);
    }
}
```

### Parameter Validation

```csharp
public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
{
    if (parameters.TryGetValue("chunkSize", out var value))
    {
        var size = Convert.ToInt32(value);
        if (size < 1024)        // Minimum 1KB
            return Error.Failure("chunkSize must be at least 1KB");
        if (size > 750 * 1024)  // Maximum 750KB (must fit in NATS)
            return Error.Failure("chunkSize cannot exceed 750KB");
    }
    return Result.Success;
}
```

### Configuration (appsettings.json / cloud config)

```json
{
  "Sensors": [
    {
      "Name": "camera.snapshot",
      "Schema": "image/jpeg",
      "Report": {
        "Enabled": true,
        "Interval": 5000,
        "TransformPipeline": [
          {
            "Type": "chunking",
            "Enabled": true,
            "Parameters": {
              "chunkSize": 262144
            }
          }
        ]
      }
    }
  ]
}
```

---

## Processing Paths by Data Size

```
+--------------------------------------------------------------------+
| Data Size vs Processing Path                                       |
+--------------------------------------------------------------------+
|                                                                    |
|  Size        Layer 1         Layer 3              Layer 2          |
|  (Base64)    Rejection       ChunkingTransform    Auto-Chunking    |
|              (10MB)          (256KB, optional)    (750KB)          |
|                                                                    |
|  100KB  ---- PASS ---------- pass through ------- pass through     |
|              (< 10MB)        (< 256KB)            (< 750KB)        |
|              Result: 1 message, 100KB                              |
|                                                                    |
|  500KB  ---- PASS ---------- 2 chunks x 256KB --- pass through     |
|              (< 10MB)        (> 256KB)            (< 750KB each)   |
|              Result: 2 messages, ~250KB each                       |
|                                                                    |
|  2MB    ---- PASS ---------- 8 chunks x 256KB --- pass through     |
|              (< 10MB)        (> 256KB)            (< 750KB each)   |
|              Result: 8 messages, ~256KB each                       |
|                                                                    |
|  2MB    ---- PASS ---------- (not configured) --- 3 chunks         |
|  (no Tx)     (< 10MB)                             x 750KB          |
|              Result: 3 messages, ~700KB each                       |
|                                                                    |
|  15MB   ---- REJECT -------- (not reached) ------ (not reached)    |
|              (> 10MB)                                              |
|              Result: X Log warning, skip                           |
|                                                                    |
+--------------------------------------------------------------------+
```

---

## JSON Handling (Special Rule)

JSON strings are **never chunked**, regardless of size:

```csharp
private static bool IsJsonString(string str)
{
    if (string.IsNullOrWhiteSpace(str))
        return false;
    var trimmed = str.TrimStart();
    return trimmed.StartsWith('{') || trimmed.StartsWith('[');
}
```

**Reason**: JSON cannot maintain semantic integrity after chunking. JSON data is converted to `JsonElement` during DTO conversion, preserving the original structure.

```
+---------------------+     +---------------+
| Large JSON string   |     | IsJsonString? |
| (e.g., 2MB)         |---->|               |
+---------------------+     +-------+-------+
                                Yes |  No
                                    |   +---> Treat as Base64
                                    |         -> chunk if > threshold
                                    v
                            +---------------+
                            | Convert to    |
                            | JsonElement   |
                            | (AdaptValue)  |
                            +-------+-------+
                                    |
                                    v
                            +---------------+
                            | Serialize as  |
                            | JSON object   |
                            | in NATS msg   |
                            +---------------+
```

---

## MIME Type Support

Currently supported MIME types (validated by `SensorsValidator`):

| MIME Type | Description | Chunking Behavior |
|-----------|-------------|-------------------|
| `image/jpeg` | JPEG image | Base64 -> chunk if large |
| `image/png` | PNG image | Base64 -> chunk if large |
| `application/octet-stream` | Binary blob | Base64 -> chunk if large |
| `application/json` | JSON data | **Never chunk** -> JsonElement |

---

## End-to-End Flow Example

### 2MB image/jpeg with ChunkingTransform (256KB)

```
Device/Sensor         SubNode                            NATS          Transceiver
     |                   |                                |                |
     | image (2MB Base64)|                                |                |
     |------------------>|                                |                |
     |                   |                                |                |
     |            +------+------+                         |                |
     |            | Stage 0:    |                         |                |
     |            | Validate    |                         |                |
     |            | 2MB < 10MB  |                         |                |
     |            | PASS        |                         |                |
     |            +------+------+                         |                |
     |                   |                                |                |
     |            +------+------+                         |                |
     |            | Stage 1:    |                         |                |
     |            | Transform   |                         |                |
     |            | Chunking    |                         |                |
     |            | 256KB each  |                         |                |
     |            | -> 8 chunks |                         |                |
     |            +------+------+                         |                |
     |                   |                                |                |
     |            +------+------+                         |                |
     |            | Stage 2:    |                         |                |
     |            | Filter      |                         |                |
     |            | (no-op)     |                         |                |
     |            +------+------+                         |                |
     |                   |                                |                |
     |            +------+------+                         |                |
     |            | Stage 3:    |                         |                |
     |            | Send        |                         |                |
     |            +------+------+                         |                |
     |                   |                                |                |
     |            +------+------+                         |                |
     |            | DTO.From()  |                         |                |
     |            | Each 256KB  |                         |                |
     |            | < 750KB     |                         |                |
     |            | No further  |                         |                |
     |            | chunking    |                         |                |
     |            +------+------+                         |                |
     |                   |                                |                |
     |                   | chunk_0 {transferId, 0/8}      |                |
     |                   |------------------------------->|                |
     |                   | chunk_1 {transferId, 1/8}      |                |
     |                   |------------------------------->|                |
     |                   | ...                            |                |
     |                   | chunk_7 {transferId, 7/8}      |                |
     |                   |------------------------------->|                |
     |                   |                                |                |
     |                   |                                | chunk_0        |
     |                   |                                |--------------->|
     |                   |                                | chunk_1        |
     |                   |                                |--------------->|
     |                   |                                | ...            |
     |                   |                                | chunk_7        |
     |                   |                                |--------------->|
     |                   |                                |                |
     |                   |                                |   +------------+
     |                   |                                |   | Reassemble |
     |                   |                                |   | by         |
     |                   |                                |   | transferId |
     |                   |                                |   | Verify     |
     |                   |                                |   | CRC32      |
     |                   |                                |   +------------+
```

---

## Key Implementation Files

| File | Role | Layer |
|------|------|-------|
| `SchemaBasedValidator.cs` | Validation & rejection | Layer 1 |
| `TelemetryOptions.cs` | MaxBinarySize & ChunkSize config | Layer 1 & 2 |
| `TelemetryPipeline.cs` | Pipeline orchestration | All layers |
| `TelemetrySendMessage.cs` | Auto-chunking at DTO level | Layer 2 |
| `TelemetryClient.cs` | Per-chunk NATS publishing | Layer 2 |
| `ChunkingTransform.cs` | Custom chunking transform | Layer 3 |
| `SensorsValidator.cs` | MIME type whitelist | Config |

## Test Coverage

| Test File | Scenarios |
|-----------|-----------|
| `ImageChunkingTests.cs` | Validation, auto-chunking, ChunkingTransform, JSON exclusion, CRC32 checksum |
| `SchemaBasedValidatorTests.cs` | MIME type validation, Base64 format, size limits |
| `TelemetryMeasureDtoTests.cs` | DTO conversion, JSON adaptation |

---

## Summary

| Layer | Location | Default Threshold | Behavior | Configurable |
|-------|----------|-------------------|----------|--------------|
| **1. Active Rejection** | SchemaBasedValidator (Stage 0) | 10MB | Reject + Log Warning | `TelemetryOptions.MaxBinarySize` |
| **2. Auto-Chunking** | TelemetryMeasureDto.From (Send) | 750KB | Auto-split | `TelemetryOptions.ChunkSize` |
| **3. Custom Chunking** | ChunkingTransform (Stage 1) | 256KB | Split by config | `TransformPipeline[].Parameters.chunkSize` |

```
Data In -> [Layer 1: > 10MB? Reject] -> [Layer 3: > custom size? Chunk] -> [Layer 2: > 750KB? Chunk] -> NATS
```