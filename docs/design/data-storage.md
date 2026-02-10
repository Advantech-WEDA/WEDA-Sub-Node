# Data Storage Design

## Overview

SubNode 提供兩種資料儲存機制，針對不同資料特性最佳化：

| Storage Type | Use Case | Data Types | File Structure |
|--------------|----------|------------|----------------|
| **SlotRecordStorage** | Fixed-size primitives | Boolean, Integer, Long, Double | Single `.bin` file |
| **DynamicRecordStorage** | Variable-size payloads | String, JSON, Images, Blob | `.idx` + `.dat` pair |

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      IRecordStorage                             │
├─────────────────────────────┬───────────────────────────────────┤
│    SlotRecordStorage        │      DynamicRecordStorage         │
│    (Fixed-Size)             │      (Variable-Size)              │
├─────────────────────────────┼───────────────────────────────────┤
│ • Boolean (1 byte)          │ • String                          │
│ • Integer (4 bytes)         │ • JSON                            │
│ • Long (8 bytes)            │ • image/jpeg, image/png           │
│ • Double (8 bytes)          │ • application/octet-stream        │
├─────────────────────────────┼───────────────────────────────────┤
│ Single .bin file per day    │ .idx + .dat pair per day          │
│ O(1) slot access            │ O(log n) binary search            │
└─────────────────────────────┴───────────────────────────────────┘
```

---

## 1. SlotRecordStorage (Fixed-Size)

### Design Rationale

- **Time-slot based**: 每日依 interval 切分為固定數量的 slots
- **O(1) access**: 位置 = `HeaderSize + SlotIndex × SlotSize`
- **Pre-allocated**: 檔案建立時即配置完整空間，以 NaN/sentinel 填充
- **No index file**: Header 包含所有必要 metadata

### File Format

```
┌─────────────────────────────────────────────────────────────────┐
│                    SlotFileHeader (24 bytes)                    │
├──────────┬──────────┬──────────┬──────────┬─────────────────────┤
│  Offset  │  Field   │   Type   │   Size   │    Description      │
├──────────┼──────────┼──────────┼──────────┼─────────────────────┤
│    0     │ Magic    │  uint32  │    4     │ 0x534C4F54 ("SLOT") │
│    4     │ Version  │  uint16  │    2     │ Format version (1)  │
│    6     │ Flags    │  uint8   │    1     │ Reserved            │
│    7     │ DataType │  uint8   │    1     │ SchemaType enum     │
│    8     │ SlotSize │  uint16  │    2     │ Bytes per slot      │
│   10     │ Reserved │  byte[]  │    2     │ Future use          │
│   12     │ Interval │  uint32  │    4     │ Interval (ms)       │
│   16     │ StartTime│  int64   │    8     │ Start of day (ms)   │
├──────────┴──────────┴──────────┴──────────┴─────────────────────┤
│                      Slot Data (N × SlotSize)                   │
├─────────────────────────────────────────────────────────────────┤
│ [Slot 0][Slot 1][Slot 2]...[Slot N-1]                           │
│  └─ N = 86400000 / Interval                                     │
└─────────────────────────────────────────────────────────────────┘
```

### SlotSize by DataType

| SchemaType | SlotSize | Sentinel Value |
|------------|----------|----------------|
| Boolean    | 1 byte   | 0xFF           |
| Integer    | 4 bytes  | Int32.MinValue |
| Long       | 8 bytes  | Int64.MinValue |
| Double     | 8 bytes  | NaN            |

### File Naming

```
{storageDir}/{sensorId}/{yyyy-MM-dd}_{interval}.bin
```

Example: `recordings/sensor-001/2024-01-15_1000.bin`

### Operations

| Operation | Complexity | Description |
|-----------|------------|-------------|
| Write     | O(1)       | Seek to slot position, write value |
| Read      | O(1)       | Seek to slot position, read value |
| ReadRange | O(k)       | Sequential read of k slots |

---

## 2. DynamicRecordStorage (Variable-Size)

### Design Rationale

- **Append-only**: 資料依序寫入，支援任意大小的 payload
- **Crash-safe**: Write ordering: data → fsync → index → fsync
- **Binary search**: O(log n) 透過 index file 查詢
- **CRC validation**: 每筆資料有 CRC32 校驗

### File Structure

```
┌─────────────────────────────────────────────────────────────────┐
│                    Index File (.idx)                            │
├─────────────────────────────────────────────────────────────────┤
│ [IndexRecord 0][IndexRecord 1][IndexRecord 2]...                │
│  └─ Each record: 32 bytes, sorted by timestamp                  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    Data File (.dat)                             │
├─────────────────────────────────────────────────────────────────┤
│ [DataFrame 0][DataFrame 1][DataFrame 2]...                      │
│  └─ Each frame: 6-byte header + variable payload                │
└─────────────────────────────────────────────────────────────────┘
```

### IndexRecord (32 bytes)

```
┌──────────┬────────────┬──────────┬──────────┬───────────────────┐
│  Offset  │   Field    │   Type   │   Size   │   Description     │
├──────────┼────────────┼──────────┼──────────┼───────────────────┤
│    0     │ Timestamp  │  int64   │    8     │ Unix timestamp ms │
│    8     │ Offset     │  int64   │    8     │ Position in .dat  │
│   16     │ PayloadSize│  int32   │    4     │ Payload bytes     │
│   20     │ PayloadCrc │  uint32  │    4     │ CRC32 of payload  │
│   24     │ Flags      │  uint8   │    1     │ IndexFlags        │
│   25     │ SchemaType │  uint8   │    1     │ Data type         │
│   26     │ SeqNum     │  uint16  │    2     │ Sequence number   │
│   28     │ IndexCrc   │  uint32  │    4     │ CRC32 of record   │
└──────────┴────────────┴──────────┴──────────┴───────────────────┘
```

### DataFrame (6 + N bytes)

```
┌──────────┬──────────┬──────────┬──────────┬───────────────────┐
│  Offset  │  Field   │   Type   │   Size   │   Description     │
├──────────┼──────────┼──────────┼──────────┼───────────────────┤
│    0     │ Magic    │  uint16  │    2     │ 0xDA7A            │
│    2     │ Size     │  uint32  │    4     │ Payload size      │
│    6     │ Payload  │  byte[]  │    N     │ Variable data     │
└──────────┴──────────┴──────────┴──────────┴───────────────────┘
```

### File Naming

```
{storageDir}/{sensorId}_{yyyy-MM-dd}_v{version}.idx
{storageDir}/{sensorId}_{yyyy-MM-dd}_v{version}.dat
```

Example:
- `dynamic/sensor-001_2024-01-15_v1.idx`
- `dynamic/sensor-001_2024-01-15_v1.dat`

### Operations

| Operation | Complexity | Description |
|-----------|------------|-------------|
| Append    | O(1)       | Append to .dat, append to .idx |
| Read      | O(log n)   | Binary search in .idx |
| ReadRange | O(log n + k) | Binary search + sequential read |

---

## 3. SchemaType Enum

```csharp
public enum SchemaType : byte
{
    // Fixed-size primitives (SlotRecordStorage)
    Boolean = 1,
    Integer = 2,
    Long = 3,
    Double = 4,

    // Variable-size data (DynamicRecordStorage)
    String = 10,
    Json = 11,

    // MIME types (DynamicRecordStorage)
    ImageJpeg = 20,
    ImagePng = 21,
    OctetStream = 30
}
```

### Routing Logic

```csharp
public static bool IsFixedSize(SchemaType type) => type switch
{
    SchemaType.Boolean or SchemaType.Integer or
    SchemaType.Long or SchemaType.Double => true,
    _ => false
};
```

---

## 4. Retention Policy

Both storage types support cleanup based on file date:

```csharp
// SlotRecordStorage
Task CleanupAsync(DateTimeOffset before, CancellationToken ct);

// DynamicRecordStorage
Task<int> CleanupAsync(DateTimeOffset cutoffDate, CancellationToken ct);
```

- Files older than cutoff date are deleted
- Date is extracted from filename pattern
- Ring-buffer behavior for disk space management

---

## 5. Comparison Summary

| Aspect | SlotRecordStorage | DynamicRecordStorage |
|--------|-------------------|----------------------|
| Data Size | Fixed (1/4/8 bytes) | Variable (any size) |
| File Count | 1 per day | 2 per day (.idx + .dat) |
| Write | O(1) random access | O(1) append |
| Read | O(1) slot lookup | O(log n) binary search |
| Space | Pre-allocated | Grows with data |
| Crash Safety | Atomic slot write | fsync ordering |
| Use Case | Sensor telemetry | Images, logs, events |

---

## 6. Implementation Files

```
src/Weda.SubNode.Abstractions/Storage/Recordings/
├── SchemaType.cs           # Data type enum
├── IndexFlags.cs           # Index record flags
├── IndexRecord.cs          # Index entry for dynamic storage
├── DataFrame.cs            # Data frame header constants
├── StorageRecord.cs        # Read result DTO
├── SlotFileHeader.cs       # Header for slot-based storage (NEW)
└── IDynamicRecordStorage.cs

src/Weda.SubNode.Core/Storage/
├── SlotRecordStorage.cs    # Fixed-size implementation (RENAME)
├── DynamicRecordStorage.cs # Variable-size implementation
└── DynamicStoragePathHelper.cs
```

## Hot spots
### Issues: historical query might miss due to differecne between recording and reporting interval.
+ Solution: persist for `tp`, resampling after `tp`, rotate at `tr`

+ solution 1: Hot zones and cold zones
```
  v   v v     v     v  v    -> not found
0,2,4,6,8,10,12,14,16,18,20    
0,  4,    10,   14,      20

                                    tp                                tr
                                  168hrs                            336hrs
                                    |                                  |
record interval = report interval       resampling                 rotation

0,2,4,6,8,10,12,14,16,18,20          0,2,4,6,8,10,12,14,16,18,20          deleted
0,2,4,6,8,10,12,14,16,18,20          0, ,4, ,8,  ,12,  ,16,  ,20          deleted
```

+ solution 2:
type: `exact`, `approximate`

┌────┬────┬────┬────┬────┬────┬────┬────┬────┬────┬────┬────┬────┬─...
│  5 │ 10 │ 15 │ 20 │ 25 │ 30 │ 35 │ 40 │ 45 │ 50 │ 55 │ 60 │ 65 │ 
├────┴────┴──┬─┴────┴────┴┬───┴────┴───┬┴────┴────┴─┬──┴────┴────┼─...
│     13     │     26     │     39     │     52     │     65     │
└────────────┴────────────┴────────────┴────────────┴────────────┴─...