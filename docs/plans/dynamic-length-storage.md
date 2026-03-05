# Design: Dynamic Record Storage (Index + Data File)

## Document Control

| Version | Date       | Author | Description of Changes |
|---------|------------|--------|------------------------|
| v2.0    | 2026-02-05 | Kevin  | Major revision — file headers, mandatory data frames, IndexCRC, SchemaType, SeqNum, crash recovery, report.data integration, MIME storage clarification, capacity planning, concurrency control |
| v1.0    | 2026-01-20 | Rain   | Initial design |

---

## 1. Overview

### 1.1 Problem Statement

Telemetry data varies significantly in size:
- **Primitive Data**: Small, fixed or predictable size (e.g., temperature double, boolean).
- **MIME Data**: Large, variable size (e.g., JSON blobs, 500KB JPEG images, 5MB binary dumps).

A single fixed-record-size file is inefficient for this variance, and a pure log structure requires expensive linear scanning to find records.

### 1.2 Selected Solution: Two-File Architecture

The system employs a **sidecar indexing pattern** to manage variable-sized telemetry data on disk. For every sensor, the system maintains two distinct files:

1. **Index File (`.idx`)**: A fixed-size metadata "directory" enabling $O(\log n)$ binary search and $O(1)$ positional access.
2. **Data File (`.dat`)**: An append-only binary log of framed, variable-sized payloads. Its meaning is derived from the Index file.

### 1.3 Related Specifications

| Specification | Relationship |
|---------------|-------------|
| [Report-Data-Command.md](../Requirements/Functional-Requirements/Report-Data-Command.md) | Defines `report.data` command that queries this storage |
| [Large-Size-MIME.md](../Requirements/Functional-Requirements/Large-Size-MIME.md) | Chunking pipeline — storage holds complete blobs, chunking is applied at transport |
| [Report-Data-Retransmission.md](../Requirements/Functional-Requirements/Report-Data-Retransmission.md) | Retransmission uses CRC32 from index to verify integrity before re-sending |
| [SubNode-MIME-Sensor-Interface.md](../Requirements/Functional-Requirements/SubNode-MIME-Sensor-Interface.md) | Producer-side SDK that writes data into this storage |

---

## 2. File Naming Convention

Each sensor gets its own file pair, time-bucketed by day and versioned:

```
{dataDir}/{sensorShortResourceId}_{YYYY-MM-DD}_v{N}.idx
{dataDir}/{sensorShortResourceId}_{YYYY-MM-DD}_v{N}.dat
```

**Examples:**
```
/data/telemetry/f782c_2026-02-05_v1.idx
/data/telemetry/f782c_2026-02-05_v1.dat
/data/telemetry/a3f9b_2026-02-05_v1.idx
/data/telemetry/a3f9b_2026-02-05_v1.dat
```

**Rationale:**
- **Per-sensor** files avoid the need for sensor identification in the index record (all records in a file belong to one sensor).
- **Per-day** bucketing enables simple time-based retention: delete old file pairs when the retention window expires.
- **Version in filepath** enables instant format detection without parsing headers, and allows multiple format versions to coexist during migrations.
- **`sensorShortResourceId`** matches the query key used by `report.data`, enabling direct file resolution without a secondary lookup.

---

## 3. File Pair Validation

On open, the system **SHALL** validate:
1. Version suffix in filepath is supported (current: `v1`)
2. Version suffix matches between `.idx` and `.dat` filenames
3. File pair exists and both files are accessible

If validation fails, the file pair is marked as **corrupted** and the crash recovery procedure (§6) is invoked.

---

## 4. Index File Structure (`.idx`)

The index file is a sequence of fixed-length **32-byte** records with no header.

**Record Position Formula:**
$$Position = RecordIndex \times 32$$

**Record Count:**
$$Count = \frac{FileSize}{32}$$

### 4.1 Index Record Layout (32 Bytes)

| Offset | Field | Type | Size | Description |
|:---|:---|:---|:---|:---|
| 0 | **Timestamp** | `int64` | 8 | Primary sort key (Unix ms). Enables $O(\log n)$ binary search. |
| 8 | **Offset** | `int64` | 8 | Byte address in the `.dat` file where the data frame starts. |
| 16 | **Size** | `int32` | 4 | Length of the payload in bytes (excluding frame header). Max ~2 GB. |
| 20 | **PayloadCRC** | `uint32` | 4 | CRC32 of the raw payload bytes. Used for corruption detection before processing. |
| 24 | **Flags** | `uint8` | 1 | Bitmask (see §4.2). |
| 25 | **SchemaType** | `uint8` | 1 | Data type enum (see §4.3). Enables type-based filtering without reading payload. |
| 26 | **SeqNum** | `uint16` | 2 | Monotonic counter within the same timestamp. Disambiguates multiple records at the same millisecond. |
| 28 | **IndexCRC** | `uint32` | 4 | CRC32 of bytes [0..27]. Self-integrity check for the index entry itself. |

### 4.2 Flags Bitmask

| Bit | Mask | Name | Description |
|:----|:-----|:-----|:------------|
| 0 | `0x01` | MIME | Payload is a MIME-type binary (image, blob). |
| 1 | `0x02` | Compressed | Payload is compressed (algorithm defined by implementation). |
| 2 | `0x04` | Encrypted | Payload is encrypted (key management defined by deployment). |
| 3 | `0x08` | Deleted | Soft-deleted. Excluded from queries; space reclaimed during compaction. |
| 4–7 | — | Reserved | Must be zero. |

### 4.3 SchemaType Enum

| Value | Schema | Category |
|:------|:-------|:---------|
| `0x00` | `double` | Primitive |
| `0x01` | `integer` | Primitive |
| `0x02` | `string` | Primitive |
| `0x03` | `boolean` | Primitive |
| `0x10` | `image/jpeg` | MIME |
| `0x11` | `image/png` | MIME |
| `0x20` | `application/json` | MIME |
| `0x21` | `application/octet-stream` | MIME |

**Note:** When `SchemaType >= 0x10`, the MIME flag (`0x01`) in Flags **MUST** also be set.

---

## 5. Data File Structure (`.dat`)

The data file is an append-only binary stream with no header. Each record is wrapped in a **mandatory frame**.

### 5.1 Data Record Frame

| Field | Type | Size | Description |
|:---|:---|:---|:---|
| **FrameMagic** | `uint16` | 2 | `0xDA7A` — frame start marker for recovery scanning. |
| **FrameSize** | `uint32` | 4 | Payload length in bytes. Must match the `Size` field in the corresponding index entry. |
| **Payload** | `bytes` | variable | Raw content: serialized primitive value, JSON string, or binary MIME data. |

**Total frame overhead:** 6 bytes per record.

**Example Layout:**
```
[Frame0: 0xDA7A | Size | Payload][Frame1: 0xDA7A | Size | Payload]...
```

### 5.2 Why Mandatory Frames

The frame magic + size enables **index-from-data disaster recovery**: if the `.idx` file is lost or corrupted, the system can reconstruct it by sequentially scanning the `.dat` file for `0xDA7A` markers and extracting sizes. This upgrades recovery from "recommended" to a guaranteed capability.

---

## 6. Operational Procedures

### 6.1 Write (Append)

Optimized for flash storage lifespan and crash safety.

```
1. Serialize     → Convert telemetry content into a byte buffer.
2. Compute CRC   → Calculate CRC32 of the payload buffer.
3. Locate        → Read `.dat` file size to determine the write Offset.
4. Write Frame   → Append [0xDA7A | Size | Payload] to `.dat`.
5. Fsync Data    → Flush `.dat` to disk.
6. Build Index   → Construct the 32-byte index record:
                    - Timestamp, Offset, Size, PayloadCRC, Flags, SchemaType
                    - SeqNum = increment if Timestamp == previous record's Timestamp, else 0
                    - IndexCRC = CRC32(bytes[0..27])
7. Write Index   → Append the 32-byte record to `.idx`.
8. Fsync Index   → Flush `.idx` to disk.
```

**Critical invariant:** Data is flushed **before** the index. This ensures that a valid index entry never points to missing or incomplete data. If power is lost between steps 5 and 8, the orphan data in `.dat` is harmless and cleaned up during recovery (§7).

### 6.2 Time-Range Query ($O(\log n)$ Search)

When a `report.historical` command is received:

1. **Identify Files**: Determine which daily file pairs overlap the requested time range.
2. **Binary Search**: Open the `.idx` file. Perform a binary search (jumping by 32-byte increments from offset 0) to find the `Start Index` corresponding to `startTime`.
3. **Sequential Scan**: Read index records sequentially from `Start Index` until `Timestamp > endTime`.
4. **Filter**: Skip records where `Flags & 0x08` (Deleted) is set.
5. **Direct Access**: For each matching index record:
   - **Validate Index**: Verify `IndexCRC` matches CRC32(bytes[0..27]).
   - **Seek**: Jump to `Offset` in the `.dat` file.
   - **Read**: Read `FrameSize` from the frame header, then read `Size` bytes of payload.
   - **Verify Payload**: Calculate CRC32 of read bytes and compare with `PayloadCRC`.
6. **Return**: Assemble and return the list of data objects.

### 6.3 Point Query (`report.data` Command)

When a `report.data` command is received with `(sensorShortResourceId, resourceTimestamp)`:

```
┌──────────────────────────────────────────────────────────────────────┐
│  report.data(sensorShortResourceId="f782c", resourceTimestamp=T)    │
│                                                                      │
│  1. Resolve file pair:                                               │
│     f782c_{date_of_T}_v1.idx / f782c_{date_of_T}_v1.dat             │
│                                                                      │
│  2. Binary search .idx for Timestamp == T                            │
│     ├─ If multiple SeqNums at T → return first non-deleted           │
│     └─ If not found → return status 3 (NO_DATA_AVAILABLE)           │
│                                                                      │
│  3. Validate IndexCRC                                                │
│     └─ If invalid → return status 9 (DATA_CORRUPTED)                │
│                                                                      │
│  4. Read payload from .dat at Offset                                 │
│     └─ Verify PayloadCRC                                             │
│         └─ If invalid → return status 9 (DATA_CORRUPTED)            │
│                                                                      │
│  5. Response routing based on SchemaType:                            │
│     ├─ Primitive → publish single message to *.dm.dt.rl.enr         │
│     ├─ JSON     → publish to *.app.json.data.enr                    │
│     └─ MIME binary:                                                  │
│         ├─ Size ≤ ChunkSize → publish single message                │
│         └─ Size > ChunkSize → chunk on-the-fly, publish N chunks    │
│                                                                      │
│  Status codes: see Report-Data-Command.md §6.1                      │
└──────────────────────────────────────────────────────────────────────┘
```

**Key principle:** Storage holds the **complete original payload**. Chunking is a transport concern applied at retrieval time per the [Large-Size-MIME](../Requirements/Functional-Requirements/Large-Size-MIME.md) specification.

### 6.4 Deletion & Retention

**Time-based Rotation:**
- Daily file pairs are created automatically (`f782c_2026-02-05_v1`, `f782c_2026-02-06_v1`, ...).
- When a file pair exceeds the configured retention window (default: 30 days), both `.idx` and `.dat` files are deleted.

**Soft Delete (Compaction):**
- To delete specific records (e.g., GDPR compliance), set bit 3 (`0x08` Deleted) in the `Flags` field of the index entry.
- Soft-deleted records are excluded from all queries.
- Physical space is reclaimed during a **compaction** maintenance window: both files are rewritten, omitting deleted records and recalculating offsets.

**Compaction Procedure:**
1. Create new `.idx.tmp` and `.dat.tmp` files with headers.
2. Iterate through the original `.idx`, skipping entries with `Flags & 0x08`.
3. For each non-deleted entry, copy the payload from original `.dat` to `.dat.tmp` with a new frame.
4. Write the updated index entry (new offset) to `.idx.tmp`.
5. Fsync both temp files.
6. Atomically rename `.tmp` files to replace originals.

---

## 7. Crash Recovery

### 7.1 Write Ordering Guarantee

The fsync ordering in §6.1 (data before index) guarantees:
- A valid index entry **always** points to complete data.
- An interrupted write can only produce: (a) orphan data bytes in `.dat` with no index entry, or (b) a partial/missing index entry.

### 7.2 Recovery Procedure (On Startup)

```
┌─────────────────────────────────────────────────────────────────┐
│                    Startup Recovery                              │
│                                                                 │
│  1. Validate filepath version suffix                            │
│     └─ If unsupported → mark pair as corrupted                 │
│                                                                 │
│  2. Compute expected .dat size from last valid index entry:     │
│     ExpectedDatEnd = LastEntry.Offset + 6 + LastEntry.Size     │
│                                                                 │
│  3. Compare actual .dat size vs ExpectedDatEnd:                 │
│     ├─ Equal     → consistent, no action needed                │
│     ├─ dat > idx → orphan data; truncate .dat to ExpectedEnd   │
│     └─ dat < idx → last index entry is invalid; truncate .idx  │
│                    to remove the incomplete entry               │
│                                                                 │
│  4. Validate last index entry's IndexCRC:                      │
│     └─ If invalid → truncate .idx to remove corrupted entry    │
│                                                                 │
│  5. Log recovery action and continue normal operation.         │
└─────────────────────────────────────────────────────────────────┘
```

### 7.3 Full Index Rebuild (Disaster Recovery)

If the `.idx` file is missing or irrecoverably corrupted but `.dat` is intact:

1. Open `.dat` file.
2. Starting at byte 0, scan for `0xDA7A` frame markers.
3. For each valid frame:
   - Read `FrameSize`, then read `FrameSize` bytes of payload.
   - Compute `PayloadCRC = CRC32(payload)`.
   - **Note:** `Timestamp`, `Flags`, `SchemaType`, and `SeqNum` cannot be recovered from the data file alone. These fields are set to zero and flagged for manual review.
4. Write reconstructed index entries to a new `.idx` file.
5. Log a warning that metadata fields require operator verification.

**Limitation:** Full index rebuild recovers data positions and integrity checksums, but loses temporal ordering metadata. This is a last-resort procedure.

---

## 8. MIME Data Storage

### 8.1 Storage vs Transport Boundary

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│  SubNode receives sensor data                                   │
│       │                                                         │
│       ▼                                                         │
│  ┌──────────────┐                                               │
│  │   Storage    │  Stores COMPLETE original blob (up to 10MB)  │
│  │  .idx + .dat │  One record per data point, no chunking.     │
│  └──────┬───────┘                                               │
│         │                                                       │
│         │ report.data or report.historical query                │
│         ▼                                                       │
│  ┌──────────────┐                                               │
│  │  Transport   │  Chunks if payload > ChunkSize (750KB)       │
│  │   Layer      │  Per Large-Size-MIME.md specification.        │
│  └──────────────┘                                               │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### 8.2 Inbound (Write Path)

When the SubNode receives MIME data from a sensor:
- The **complete binary payload** (decoded from Base64) is stored as a single record in `.dat`.
- The index entry records the MIME flag, `SchemaType`, `Size`, and `PayloadCRC`.
- If the data arrived chunked from the sensor SDK, the SubNode reassembles first, then stores the complete blob.

### 8.3 Outbound (Read Path)

When responding to a `report.data` or `report.historical` query:
- The complete blob is read from `.dat`.
- `PayloadCRC` is verified.
- If `Size > ChunkSize` → the blob is Base64-encoded and chunked on-the-fly per [Large-Size-MIME.md](../Requirements/Functional-Requirements/Large-Size-MIME.md).
- If `Size ≤ ChunkSize` → published as a single NATS message.

### 8.4 Retransmission Support

When a `report.datapack` retransmission request is received (see [Report-Data-Retransmission.md](../Requirements/Functional-Requirements/Report-Data-Retransmission.md)):
1. The original record is re-read from storage using the same point-query path (§6.3).
2. `PayloadCRC` is verified. If verification fails → respond with status `9 (DATA_CORRUPTED)`.
3. The blob is re-chunked and only the requested missing chunks are retransmitted.

---

## 9. Concurrency Control

### 9.1 Model: Single-Writer, Multiple-Reader

| Actor | Lock Type | Behavior |
|:------|:----------|:---------|
| Writer (telemetry ingest) | Exclusive mutex | One writer per file pair. Holds lock during the append+fsync cycle. |
| Reader (query handler) | None (lock-free) | Reads up to a captured snapshot of the `.idx` file size. |

### 9.2 Lock-Free Read Protocol

Readers do **not** acquire locks. Instead, they use a file-size snapshot approach:

```
1. Read idxFileSize = stat(.idx).size
2. recordCount = idxFileSize / 32
3. Perform binary search / sequential scan within [0, recordCount)
4. Read corresponding .dat payload
```

Since the writer only appends and fsyncs data-before-index, the reader can never see an index entry pointing to missing data. The worst case is the reader misses the most recent record (which was written after the snapshot), which is acceptable for telemetry queries.

### 9.3 Write Mutex

The writer acquires a per-file-pair mutex (or OS-level `flock`) before the append cycle and releases it after both fsyncs complete. This prevents data corruption from concurrent writers.

---

## 10. Capacity Planning

### 10.1 Size Projections

| Scenario | Records/Day | Daily `.idx` Size | Daily `.dat` Size (avg) |
|:---------|:------------|:------------------|:------------------------|
| Primitive sensor (1 Hz) | 86,400 | 2.7 MB | ~0.7 MB (8-byte doubles) |
| Primitive sensor (10 Hz) | 864,000 | 27 MB | ~6.9 MB |
| Camera snapshot (1/min) | 1,440 | 46 KB | ~720 MB (500KB avg JPEG) |
| Camera snapshot (1/5s) | 17,280 | 553 KB | ~8.6 GB (500KB avg) |
| Binary blob (hourly) | 24 | <1 KB | up to 240 MB (10MB max) |

### 10.2 Limits

| Limit | Value | Constraint |
|:------|:------|:-----------|
| Max single record payload | 10 MB | `MaxBinarySize` from [Large-Size-MIME.md](../Requirements/Functional-Requirements/Large-Size-MIME.md) |
| Max payload addressable | ~2 GB | `int32` Size field |
| Max `.dat` file offset | ~8 EB | `int64` Offset field |
| Max records per file | ~67M | Practical limit based on int32 scan time |
| SeqNum range per timestamp | 0–65,535 | `uint16` — supports up to 65,536 records/ms |

### 10.3 Retention Guidance

| Sensor Type | Recommended Retention | Rationale |
|:------------|:---------------------|:----------|
| Primitive telemetry | 30 days | Low storage cost; useful for gap-filling |
| Camera images | 7 days | High storage cost; operational review window |
| Binary blobs | 14 days | Medium storage; compliance minimum |

Retention is enforced by deleting expired daily file pairs. No compaction required for time-based expiry.

---

## 11. IIoT Design Rationale

### 11.1 Reliability & Safety (SIL2 Considerations)

- **Fail-Safe Writes**: The data-before-index fsync ordering ensures no valid index entry ever points to incomplete data. Power loss at any point results in a recoverable state.
- **Double CRC**: `PayloadCRC` validates data integrity; `IndexCRC` validates the index entry itself. Both are verified before processing, preventing corrupted data from propagating to downstream systems.
- **Disaster Recovery**: The mandatory `0xDA7A` frame markers in `.dat` enable index reconstruction from the data file alone.

### 11.2 Resource Efficiency

- **Memory Efficiency**: Only 32-byte index records are loaded for queries. A full-day scan of a 1 Hz sensor loads ~2.7 MB into memory.
- **Predictable Performance**: No background indexing, vacuuming, or compaction during normal operation. Every write has constant, predictable cost.
- **Lock-Free Reads**: Query handlers never block telemetry ingestion.

### 11.3 Storage Optimization

- **Flash-Friendly**: Append-only writes minimize wear on industrial SD cards and eMMC. No random-access writes during normal operation.
- **Flexibility**: The design handles 8-byte primitive doubles and 10 MB high-res images with equal architectural ease.
- **Minimal Overhead**: 6 bytes of frame overhead per data record; 32 bytes of index per record. For a 500 KB JPEG, overhead is 0.008%.

---

## 12. Implementation Notes

| Concern | Recommendation |
|:--------|:---------------|
| **Endianness** | Little-endian for all integer fields. |
| **Buffering** | Use buffered I/O for high-frequency primitive writes (e.g., 10 Hz). Flush buffer on configurable interval or record count. |
| **CRC Algorithm** | CRC-32/ISO-HDLC (polynomial 0x04C11DB7). Same as used in Ethernet, PNG, and zlib. |
| **File Creation** | Create file pairs lazily on first write for a given sensor+day. |
| **Error Logging** | All CRC failures, recovery actions, and file corruption events must be logged at WARN level or above with sensor context. |
| **Language** | Go implementation: use `encoding/binary` for struct serialization, `hash/crc32` for checksums, `os.File.Sync()` for fsync. |
