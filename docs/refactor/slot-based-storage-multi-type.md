# Refactor Plan: Slot-Based Storage Multi-Type Support

## Overview

將 `BinaryRecordStorage` 從固定 `double` (8 bytes) slot 改為根據 `SchemaType` 決定 slot size，支援多種 primitive types。

**向後相容**：Reader 支援 V1 和 V2 格式，Writer 只產生 V2。

---

## File Formats

### Version 1 (現有，只讀)
```
Header: 24 bytes
┌────────┬──────┬─────────┬─────────────────────────────────┐
│ Offset │ Size │ Type    │ Field                           │
├────────┼──────┼─────────┼─────────────────────────────────┤
│ 0      │ 4    │ uint32  │ Prefix (0x57454441 "WEDA")      │
│ 4      │ 2    │ uint16  │ Version (= 1)                   │
│ 6      │ 1    │ byte    │ Flags                           │
│ 7      │ 1    │ byte    │ CheckSumType                    │
│ 8      │ 4    │ uint32  │ Interval (ms)                   │
│ 12     │ 8    │ ulong   │ StartTimestamp (Unix ms)        │
│ 20     │ 4    │ uint32  │ SlotCount                       │
└────────┴──────┴─────────┴─────────────────────────────────┘
Data: SlotCount × 8 bytes (double only)
```

### Version 2 (新格式，讀寫)
```
Header: 32 bytes (aligned)
┌────────┬──────┬─────────┬─────────────────────────────────┐
│ Offset │ Size │ Type    │ Field                           │
├────────┼──────┼─────────┼─────────────────────────────────┤
│ 0      │ 4    │ uint32  │ Prefix (0x57454441 "WEDA")      │
│ 4      │ 2    │ uint16  │ Version (= 2)                   │
│ 6      │ 1    │ byte    │ Flags (bit 0: Endianness)       │
│ 7      │ 1    │ byte    │ SchemaType                      │
│ 8      │ 4    │ uint32  │ Interval (ms)                   │
│ 12     │ 4    │ uint32  │ SlotSize (bytes)                │
│ 16     │ 8    │ ulong   │ StartTimestamp (Unix ms)        │
│ 24     │ 4    │ uint32  │ SlotCount                       │
│ 28     │ 4    │ byte[4] │ Reserved                        │
└────────┴──────┴─────────┴─────────────────────────────────┘
Data: SlotCount × SlotSize bytes
```

### SchemaType → SlotSize Mapping
```
┌─────────────┬───────────┬───────────────────┬─────────────┐
│ SchemaType  │ SlotSize  │ C# Type           │ Empty Value │
├─────────────┼───────────┼───────────────────┼─────────────┤
│ Double      │ 8 bytes   │ double            │ double.NaN  │
│ Long        │ 8 bytes   │ long              │ long.MinValue│
│ Integer     │ 4 bytes   │ int               │ int.MinValue │
│ Boolean     │ 1 byte    │ byte (0/1/0xFF)   │ 0xFF        │
└─────────────┴───────────┴───────────────────┴─────────────┘
```

### Storage 分類
```
┌─────────────────────┬────────────────────┐
│ SchemaType          │ Storage            │
├─────────────────────┼────────────────────┤
│ Double (0x00)       │ BinaryRecordStorage│
│ Integer (0x01)      │ BinaryRecordStorage│
│ Long (0x02)         │ BinaryRecordStorage│
│ Boolean (0x03)      │ BinaryRecordStorage│
├─────────────────────┼────────────────────┤
│ String (0x10)       │ DynamicRecordStorage│
│ ImageJpeg (0x20+)   │ DynamicRecordStorage│
│ ApplicationJson...  │ DynamicRecordStorage│
└─────────────────────┴────────────────────┘
```

---

## Backward Compatibility Strategy

### Reader 邏輯
```csharp
var header = ReadHeader(filePath);
return header.Version switch
{
    1 => ReadV1(filePath, header),  // 舊格式：固定 double, 8 bytes
    2 => ReadV2(filePath, header),  // 新格式：根據 SchemaType
    _ => throw new NotSupportedException($"Version {header.Version} not supported")
};
```

### V1 讀取時的型別轉換
```csharp
// V1 檔案只有 double，讀取後統一回傳 RecordingDataPoint
// SchemaType 預設為 Double
private RecordingDataPoint ReadV1Slot(byte[] bytes)
{
    var value = BitConverter.ToDouble(bytes);
    return new RecordingDataPoint(timestamp, value, SchemaType.Double);
}
```

### 檔名規則（區分 V1/V2）
```
V1: {date}_{interval}.bin           例如: 2024-01-15_1000.bin
V2: {date}_{interval}_{schema}.bin  例如: 2024-01-15_1000_double.bin
                                         2024-01-15_1000_boolean.bin
```

或者：**不改檔名**，由 Header Version 決定讀取方式。

---

## Pros and Cons

### Pros
| Benefit | Impact |
|---------|--------|
| Boolean: 8→1 byte (87.5% 節省) | 空間效率 |
| Integer: 8→4 bytes (50% 節省) | 空間效率 |
| 無需 `double.TryParse` | 型別安全、效能 |
| `long` 無精度損失 | 避免 >2^53 問題 |
| `bool` 直接存 0/1 | 語義清晰 |
| V1 檔案仍可讀取 | 向後相容 |

### Cons
| Drawback | Mitigation |
|----------|------------|
| Reader 複雜度增加 | Version switch 封裝 |
| 介面變更 | 一次性重構 |
| Write/Read 複雜度 | Helper methods |

---

## Files to Modify

| File | Change |
|------|--------|
| `RecordingFileHeader.cs` | 新增 V2 欄位，保留 V1 常數 |
| `RecordingDataPoint.cs` | `double Value` → `object Value` + `SchemaType` |
| `IRecordStorage.cs` | 新增 SchemaType 參數 |
| `BinaryRecordStorage.cs` | V2 寫入，V1/V2 讀取 |
| `RecordingBinFileReader.cs` | V1/V2 雙版本解析 |
| `RecordingService.cs` | 傳遞 SchemaType |
| `DeviceBase.cs` | 移除 double.TryParse |

---

## Implementation Steps

### Step 1: Data Structures
```csharp
// RecordingDataPoint.cs
public readonly record struct RecordingDataPoint(
    long Timestamp,
    object Value,
    SchemaType SchemaType = SchemaType.Double);  // 預設 Double 相容 V1

// RecordingFileHeader.cs
public class RecordingFileHeader
{
    public const uint Prefix = 0x57454441;
    public const int HeaderSizeV1 = 24;
    public const int HeaderSizeV2 = 32;

    public ushort Version { get; set; }
    public byte Flags { get; set; }
    public SchemaType SchemaType { get; set; } = SchemaType.Double;  // V1 預設
    public uint Interval { get; set; }
    public uint SlotSize { get; set; } = 8;  // V1 預設
    public ulong StartTimestamp { get; set; }
    public uint SlotCount { get; set; }

    public int HeaderSize => Version == 1 ? HeaderSizeV1 : HeaderSizeV2;
}
```

### Step 2: Helper Methods
```csharp
// SchemaTypeExtensions.cs - 新增方法
public static int GetSlotSize(this SchemaType schemaType) => schemaType switch
{
    SchemaType.Double => 8,
    SchemaType.Long => 8,
    SchemaType.Integer => 4,
    SchemaType.Boolean => 1,
    _ => throw new NotSupportedException($"SchemaType {schemaType} not supported")
};

public static byte[] GetEmptySlotBytes(this SchemaType schemaType) => schemaType switch
{
    SchemaType.Double => BitConverter.GetBytes(double.NaN),
    SchemaType.Long => BitConverter.GetBytes(long.MinValue),
    SchemaType.Integer => BitConverter.GetBytes(int.MinValue),
    SchemaType.Boolean => [0xFF],
    _ => throw new NotSupportedException()
};

public static byte[] ToBytes(this SchemaType schemaType, object value) => schemaType switch
{
    SchemaType.Double => BitConverter.GetBytes(Convert.ToDouble(value)),
    SchemaType.Long => BitConverter.GetBytes(Convert.ToInt64(value)),
    SchemaType.Integer => BitConverter.GetBytes(Convert.ToInt32(value)),
    SchemaType.Boolean => [(byte)(Convert.ToBoolean(value) ? 1 : 0)],
    _ => throw new NotSupportedException()
};

public static object FromBytes(this SchemaType schemaType, ReadOnlySpan<byte> bytes) => schemaType switch
{
    SchemaType.Double => BitConverter.ToDouble(bytes),
    SchemaType.Long => BitConverter.ToInt64(bytes),
    SchemaType.Integer => BitConverter.ToInt32(bytes),
    SchemaType.Boolean => bytes[0] == 1,
    _ => throw new NotSupportedException()
};

public static bool IsEmptySlot(this SchemaType schemaType, ReadOnlySpan<byte> bytes) => schemaType switch
{
    SchemaType.Double => double.IsNaN(BitConverter.ToDouble(bytes)),
    SchemaType.Long => BitConverter.ToInt64(bytes) == long.MinValue,
    SchemaType.Integer => BitConverter.ToInt32(bytes) == int.MinValue,
    SchemaType.Boolean => bytes[0] == 0xFF,
    _ => false
};
```

### Step 3: RecordingBinFileReader (V1/V2 支援)
```csharp
public static RecordingFileHeader? ParseHeader(byte[] headerBytes)
{
    var prefix = BitConverter.ToUInt32(headerBytes, 0);
    if (prefix != RecordingFileHeader.Prefix)
        return null;

    var version = BitConverter.ToUInt16(headerBytes, 4);
    return version switch
    {
        1 => ParseHeaderV1(headerBytes),
        2 => ParseHeaderV2(headerBytes),
        _ => null
    };
}

private static RecordingFileHeader ParseHeaderV1(byte[] bytes) => new()
{
    Version = 1,
    Flags = bytes[6],
    SchemaType = SchemaType.Double,  // V1 固定 double
    Interval = BitConverter.ToUInt32(bytes, 8),
    SlotSize = 8,  // V1 固定 8 bytes
    StartTimestamp = BitConverter.ToUInt64(bytes, 12),
    SlotCount = BitConverter.ToUInt32(bytes, 20)
};

private static RecordingFileHeader ParseHeaderV2(byte[] bytes) => new()
{
    Version = 2,
    Flags = bytes[6],
    SchemaType = (SchemaType)bytes[7],
    Interval = BitConverter.ToUInt32(bytes, 8),
    SlotSize = BitConverter.ToUInt32(bytes, 12),
    StartTimestamp = BitConverter.ToUInt64(bytes, 16),
    SlotCount = BitConverter.ToUInt32(bytes, 24)
};
```

### Step 4: Interface Update
```csharp
// IRecordStorage.cs
public interface IRecordStorage
{
    Task WriteAsync(string sensorId, int interval, SchemaType schemaType,
                    RecordingDataPoint dataPoint, CancellationToken ct = default);

    Task WriteBatchAsync(string sensorId, int interval, SchemaType schemaType,
                         IEnumerable<RecordingDataPoint> dataPoints, CancellationToken ct = default);

    // Read 不需要 SchemaType，從 Header 讀取
    Task<IReadOnlyList<RecordingDataPoint>> ReadAsync(string sensorId, int interval,
                         DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    // ...
}
```

### Step 5: BinaryRecordStorage
- `CreateFileWithHeader`: 產生 V2 格式
- `WriteToSlotAsync`: 根據 SchemaType 寫入
- `ReadFromFileAsync`: 根據 Header.Version 選擇 V1 或 V2 讀取邏輯

### Step 6: DeviceBase Simplification
```csharp
// BEFORE
if (double.TryParse(stringValue, out var doubleValue))
{
    RaiseTelemetryRecording(new TelemetryRecordingEvent(..., Value: doubleValue));
}

// AFTER
RaiseTelemetryRecording(new TelemetryRecordingEvent(
    Sensor: sensor,
    Interval: interval,
    Timestamp: processedMeasure.Timestamp,
    Value: processedMeasure.Value,
    SchemaType: schemaType));
```

---

## Estimated Effort

| Task | Time |
|------|------|
| Data structures | 1 hour |
| Helper methods (SchemaTypeExtensions) | 1 hour |
| RecordingBinFileReader (V1/V2) | 1.5 hours |
| BinaryRecordStorage | 2 hours |
| RecordingService | 1 hour |
| DeviceBase | 0.5 hour |
| Testing (V1/V2 讀取) | 2 hours |
| **Total** | **~9 hours** |
