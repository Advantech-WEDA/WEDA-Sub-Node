# Dynamic Record Storage

## Overview

DynamicRecordStorage provides efficient storage and retrieval for variable-size telemetry data (including MIME types like images). It uses a two-file architecture optimized for IIoT scenarios with crash safety and O(log n) query performance.

## Architecture

```
Two-File Architecture:

+------------------+     +------------------+
|   Index File     |     |   Data File      |
|   (.idx)         |     |   (.dat)         |
+------------------+     +------------------+
| [IndexRecord 0]  |---->| [DataFrame 0]    |
| [IndexRecord 1]  |---->| [DataFrame 1]    |
| [IndexRecord 2]  |---->| [DataFrame 2]    |
| ...              |     | ...              |
+------------------+     +------------------+
  32 bytes fixed          Variable size
  O(log n) search         Append-only
```

### File Naming Convention

```
{dataDir}/{sensorId}_{date}_v1.idx   - Index file
{dataDir}/{sensorId}_{date}_v1.dat   - Data file

Example:
/data/recordings/sensor01_2026-02-10_v1.idx
/data/recordings/sensor01_2026-02-10_v1.dat
```

## Data Structures

### IndexRecord (32 bytes, fixed-size)

```
+--------+-------------+--------+------+
| Offset | Field       | Type   | Size |
+--------+-------------+--------+------+
|    0   | Timestamp   | int64  |   8  |
|    8   | Offset      | int64  |   8  |
|   16   | PayloadSize | int32  |   4  |
|   20   | PayloadCrc  | uint32 |   4  |
|   24   | Flags       | uint8  |   1  |
|   25   | SchemaType  | uint8  |   1  |
|   26   | SeqNum      | uint16 |   2  |
|   28   | IndexCrc    | uint32 |   4  |
+--------+-------------+--------+------+
| Total  |             |        |  32  |
+--------+-------------+--------+------+
```

| Field | Description |
|-------|-------------|
| Timestamp | Unix timestamp in milliseconds (primary sort key) |
| Offset | Byte position in .dat file where DataFrame starts |
| PayloadSize | Size of payload in bytes (max ~2GB) |
| PayloadCrc | CRC32 of payload for corruption detection |
| Flags | Bitmask: Mime=0x01, Compressed=0x02, Encrypted=0x04, Deleted=0x08 |
| SchemaType | Data type enum (Double, Integer, String, ImageJpeg, etc.) |
| SeqNum | Disambiguates multiple records with same timestamp |
| IndexCrc | CRC32 of bytes [0..27] for self-integrity check |

### DataFrame (Variable-size)

```
+--------+-------+--------+------+
| Offset | Field | Type   | Size |
+--------+-------+--------+------+
|    0   | Magic | uint16 |   2  |  <- 0xDA7A
|    2   | Size  | uint32 |   4  |
|    6   | Data  | byte[] |   N  |
+--------+-------+--------+------+
```

The magic number `0xDA7A` enables disaster recovery by scanning for valid frames.

### SchemaType Enum

```csharp
public enum SchemaType : byte
{
    Double = 0x00,
    Integer = 0x01,
    String = 0x02,
    Boolean = 0x03,
    ImageJpeg = 0x10,
    ImagePng = 0x11,
    ApplicationJson = 0x20,
    ApplicationOctetStream = 0x21,
}
```

## Write Path

### Crash-Safe Write Order

```
AppendAsync Flow:

1. Compute PayloadCRC
       |
       v
2. Get current .dat file size (= write offset)
       |
       v
3. Write DataFrame to .dat
   [Magic:2][Size:4][Payload:N]
       |
       v
4. fsync(.dat)  <-- Ensure data is on disk
       |
       v
5. Build IndexRecord with:
   - Timestamp
   - Offset (from step 2)
   - PayloadSize
   - PayloadCRC
   - Flags, SchemaType
   - SeqNum (increment if same timestamp)
   - IndexCRC
       |
       v
6. Write IndexRecord to .idx
       |
       v
7. fsync(.idx)  <-- Ensure index is on disk
       |
       v
   Done (crash-safe)
```

**Why this order matters**:
- Data is written before index
- If crash occurs after step 4 but before step 7, data exists but index doesn't reference it (safe)
- If crash occurs after step 7, both data and index are consistent
- Never have an index pointing to non-existent data

### SeqNum Logic

When multiple records have the same timestamp:

```
Timestamp  SeqNum  Payload
---------  ------  -------
1000       0       "first"
1000       1       "second"
1000       2       "third"
1001       0       "next ms"
```

## Read Path

### Binary Search (O(log n))

```
BinarySearchIndex(timestamp):

  left = 0
  right = recordCount - 1
  result = -1

  while left <= right:
      mid = left + (right - left) / 2
      record = ReadIndexRecordAt(mid)

      if record.Timestamp == timestamp:
          result = mid
          right = mid - 1   # Find first occurrence
      else if record.Timestamp < timestamp:
          left = mid + 1
      else:
          right = mid - 1

  return result
```

### Point Query (ReadAsync)

```
ReadAsync(sensorId, timestamp):

1. Determine file paths from sensorId + timestamp
       |
       v
2. Get record count from .idx file size
       |
       v
3. Binary search for timestamp
       |
       v
4. If not found, return null
       |
       v
5. Read IndexRecord at found position
       |
       v
6. Read payload from .dat at IndexRecord.Offset
       |
       v
7. Return StorageRecord(timestamp, payload, schemaType, flags)
```

### Range Query (ReadRangeAsync)

```
ReadRangeAsync(sensorId, startTime, endTime):

1. Binary search for first record >= startTime
       |
       v
2. Sequential scan from that position
       |
       v
3. For each record where timestamp <= endTime:
   - Read IndexRecord
   - Read payload from .dat
   - Add to results
       |
       v
4. Stop when timestamp > endTime
       |
       v
5. Return results list
```

## Usage Example

```csharp
// Create storage instance
var storage = new DynamicRecordStorage(logger, "/data/recordings");

// Write a record
var payload = Encoding.UTF8.GetBytes("Hello, World!");
await storage.AppendAsync("sensor01", timestamp, payload, SchemaType.String);

// Read a single record
var record = await storage.ReadAsync("sensor01", timestamp);
if (record != null)
{
    Console.WriteLine($"Found: {Encoding.UTF8.GetString(record.Value.Payload.Span)}");
}

// Read a range of records
var records = await storage.ReadRangeAsync("sensor01", startTime, endTime);
foreach (var r in records)
{
    Console.WriteLine($"Timestamp: {r.Timestamp}, Size: {r.Payload.Length}");
}
```

## Performance Characteristics

| Operation | Time Complexity | Notes |
|-----------|-----------------|-------|
| AppendAsync | O(1) | Append-only, constant time |
| ReadAsync | O(log n) | Binary search on index |
| ReadRangeAsync | O(log n + k) | Binary search + k sequential reads |

Where:
- n = number of records in the day's index file
- k = number of records in the query range

## File Size Calculations

For 1 million records per day:
- Index file: 1M x 32 bytes = **32 MB**
- Data file: depends on payload sizes

For 10,000 records per day (typical sensor):
- Index file: 10K x 32 bytes = **320 KB**

## Comparison with Other Approaches

| Approach | Pros | Cons |
|----------|------|------|
| Single file per record | Simple | Inode exhaustion on Linux |
| SQLite | Full SQL support | Overhead for simple append/query |
| **Index + Data (this)** | Fast, crash-safe, efficient | Manual implementation |
| LevelDB/RocksDB | Good for key-value | Overkill for time-series |

## Related Files

| File | Purpose |
|------|---------|
| `IDynamicRecordStorage.cs` | Interface definition |
| `DynamicRecordStorage.cs` | Implementation |
| `IndexRecord.cs` | 32-byte index structure |
| `DataFrame.cs` | Data frame constants |
| `SchemaType.cs` | Data type enum |
| `IndexFlags.cs` | Flag bitmask enum |
| `StorageRecord.cs` | Query result record |
| `DynamicStoragePathHelper.cs` | File naming utilities |

## Test Coverage

| Test | Description |
|------|-------------|
| `AppendAndRead_SingleRecord` | Basic write/read |
| `AppendAndRead_MultipleRecords` | Multiple records lookup |
| `ReadAsync_NonExistentTimestamp` | Not found case |
| `ReadRangeAsync_ShouldReturnRecordsInRange` | Range query |
| `AppendAsync_DuplicateTimestamps` | SeqNum handling |
| `AppendAsync_LargePayload` | 1MB payload test |
| `BinarySearch_ManyRecords` | 1000 records search |
