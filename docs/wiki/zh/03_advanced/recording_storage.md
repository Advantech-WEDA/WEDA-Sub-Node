# Recording Storage 設計文件

## 概述

Recording Storage 是 SubNode 的本地離線資料記錄功能，將 telemetry 資料以二進位格式儲存於本地磁碟，支援 historical query 與離線資料回補。

### 術語定義

| 術語 | 說明 |
|------|------|
| **Recording** | 本地儲存（Local Storage）- 資料寫入本地磁碟 |
| **Reporting** | 雲端回報（Cloud Upload）- 資料上傳至雲端 |

這兩個動作是獨立的：Recording 確保資料不遺失，Reporting 負責雲端同步。

---

## 架構設計

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

### Clean Architecture 分層

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

## 設定階層

### Level 1: SubNode (systemcfg.json)

`RecordingOptions` - 全域儲存設定

```json
{
  "Record": {
    "MinFreeDiskSpaceMb": 128,
    "RetentionDays": 7
  }
}
```

| 屬性 | 型別 | 預設值 | 範圍 | 可設定 | 說明 |
|------|------|--------|------|--------|------|
| `StorageDirectory` | string | `"./data/recording"` | 固定值 | No | 儲存目錄路徑 (固定值) |
| `MinFreeDiskSpaceMb` | int | `128` | 10-10000 | Yes | 最小可用磁碟空間 (MB) |
| `RetentionDays` | int | `7` | 0-180 | Yes | 資料保留天數 (0=無限) |

### Level 2: Sensor (devicecfg.json)

`SensorRecordingConfig` - 感測器層級設定

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

| 屬性 | 型別 | 預設值 | 範圍 | 說明 |
|------|------|--------|------|------|
| `Enabled` | bool | `true` | - | 是否啟用該 Sensor 的 Recording |
| `Interval` | int | `0` | 0, 或 ≥ Report.Interval | 記錄間隔 (毫秒)。0 代表等同於 Report.Interval |

---

## 儲存設計

### 目錄結構

```
{StorageDirectory}/
└── {SensorId}/
    ├── 2026-01-15.bin    ← 每日一個檔案
    ├── 2026-01-16.bin
    └── 2026-01-17.bin
```

> **Note**: `SensorId` 由 `SubNodeId + Device.Name + Sensor.Name` 組成，已具唯一性。

### 二進位檔案格式

#### Header (24 bytes)

```
┌───────────────────────────────────────────────────────────┐
│ Offset │ Size │ Type    │ Field                           │
├────────┼──────┼─────────┼─────────────────────────────────┤
│ 0      │ 4    │ uint32  │ Prefix (0x57454441 "WEDA")      │
│ 4      │ 2    │ uint16  │ Version (1)                     │
│ 6      │ 2    │ uint16  │ Interval (ms)                   │
│ 8      │ 8    │ int64   │ StartTimestamp (Unix ms)        │
│ 16     │ 4    │ int32   │ SlotCount                       │
│ 20     │ 4    │ int32   │ CurrentSlot                     │
└───────────────────────────────────────────────────────────┘
Total: 24 bytes
```

#### Data Section (Slot-based Storage)

**Slot 概念**：將一天 (86,400,000 ms) 切成固定大小的時間格子。

```
Interval = 1000ms (1秒) 時，一天 = 86,400 個 slots

時間軸:
00:00:00.000 ──────────────────────────────────────> 23:59:59.999
   │           │           │           │                │
   Slot 0      Slot 1      Slot 2      ...          Slot 86399
```

**檔案結構**：

```
┌───────────────────────────────────────────────────────────┐
│ Slot 0  │ double (8 bytes) │ Value or NaN                 │
│ Slot 1  │ double (8 bytes) │ Value or NaN                 │
│ ...     │ ...              │ ...                          │
│ Slot N  │ double (8 bytes) │ Value or NaN                 │
└───────────────────────────────────────────────────────────┘
```

**Slot 計算**：

| 項目 | 公式 | 範例 (Interval=1000ms) |
|------|------|------------------------|
| SlotCount | `86400000 / interval` | 86,400 slots |
| SlotIndex | `(timestamp - startTimestamp) / interval` | - |
| 檔案大小 | `HeaderSize + SlotCount × 8` | 24 + 691,200 = 691,224 bytes (~675 KB) |

**NaN 填充**：
- 無資料的 slot 填入 `double.NaN`
- 讀取時可識別為「該時間點無資料」
- 確保檔案大小固定，支援 random access

### Ring Buffer FIFO

當磁碟空間不足時：
1. 計算需要釋放的空間
2. 依照日期順序刪除最舊的檔案
3. 刪除直到空間足夠或無檔案可刪

---

## 介面設計

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

## API 設計

### NATS Topic 架構

```json
{
  "natsTopicAssignments": {
    "telemetryTopic": "{protoVer}.{deviceId}.telemetry",
    "batchTelemetryTopic": "{protoVer}.{deviceId}.telemetry-batch"
  }
}
```

| Topic | 用途 | 方向 |
|-------|------|------|
| `telemetryTopic` | 即時 telemetry 上報 | SubNode → Cloud |
| `batchTelemetryTopic` | 歷史資料批次上報 (補遺) | SubNode → Cloud |

### Telemetry Topic (即時上報)

使用 `TelemetrySendMessage` 格式：

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

| 欄位 | 型別 | 說明 |
|------|------|------|
| `seqId` | int | 自增序列號 (per message type) |
| `timestamp` | long | 訊息時間戳記 (Unix ms) |
| `measures[].sensorId` | string | Sensor Short ID (ResourceId 後 5 碼) |
| `measures[].value` | object | 感測值 |
| `measures[].timestamp` | long | 資料時間戳記 (Unix ms) |

> 參考：[TelemetrySendMessage.cs](../../src/Weda.SubNode.Abstractions/Cloud/Clients/Telemetry/Contracts/TelemetrySendMessage.cs)

### Batch Telemetry Topic (歷史補遺)

批次歷史資料上報，用於離線期間資料補遺：

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

| 欄位 | 型別 | 說明 |
|------|------|------|
| `measures[].id` | string | Sensor 識別碼 |
| `measures[].interval` | int | 資料間隔 (秒) |
| `measures[].startTimeStamp` | long | 起始時間 (Unix 秒) |
| `measures[].values` | double[] | 數值陣列（順序對應時間，`null` 表示無資料） |

### 資料流程

```
┌──────────────────────────────────────────────────────────────────────────┐
│                              SubNode                                     │
├──────────────────────────────────────────────────────────────────────────┤
│                                                                          │
│  Sensor Data ──┬──> TelemetryPipeline ──> telemetryTopic ──> Cloud       │
│                │                                                         │
│                └──> RecordingStorage ──> .bin files (local)              │
│                          ▲                                               │
│                          │ ReadAsync()                                   │
│                          │                                               │
│  Cloud ──> cmd.req ──> CommandHandler ──> batchTelemetryTopic ──> Cloud  │
│       (REPORT command)       │                                           │
│                              └──> cmd.rsp (Completed)                    │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```

---

## Acceptance Criteria 對應

| AC | 實作方式 |
|----|----------|
| 當 `storageRetentionDays` 更新時，調整 cleanup 邏輯 | `RecordingCleanupService` 讀取 `RecordingOptions.RetentionDays` |
| 本地儲存滿時，覆寫最舊資料 (Ring Buffer FIFO) | `BinaryRecordingStorage.EnsureDiskSpace()` 依日期刪除檔案 |
| Recording 開關 | `SensorRecordingConfig.Enabled` 控制 Sensor 層級開關 |
| 每日 rotation | 檔案命名 `{date}.bin`，每日自動建立新檔 |

---

## Future Design

### 儲存後端擴展

目前使用 Binary 檔案格式，未來可透過 `IRecordingStorage` 介面擴展其他儲存後端：

```
IRecordingStorage (介面)
    ├── BinaryRecordingStorage   ← 目前實作
    ├── SqliteRecordingStorage   ← 未來可加
    └── ...
```

若需支援多種儲存後端，可在 `RecordingOptions` 加入 `StorageType`：

```csharp
public class RecordingOptions
{
    public RecordingStorageType StorageType { get; set; } = RecordingStorageType.Binary;
    // ...
}

public enum RecordingStorageType
{
    Binary,   // .bin files (預設)
    Sqlite    // SQLite database
}
```

DI 註冊時根據設定選擇實作：

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

> **Note**: 目前保持 KISS，僅實作 `BinaryRecordingStorage`。介面已抽象，未來擴展容易。

### 儲存後端選擇指南

#### Binary File vs SQLite 比較

| 面向 | Binary File | SQLite |
|------|-------------|--------|
| 寫入速度 | 最快 (direct I/O) | 中等 (WAL + transaction) |
| Rotation | `File.Delete()` 即完成 | 慢 (需 VACUUM 回收空間) |
| 檔案大小 | 固定 (header + slots) | 動態成長 + overhead |
| Random Access | O(1) slot seek | O(log n) index lookup |
| Query 彈性 | 低 (需自己實作) | 高 (SQL 語法) |
| 跨 Sensor 查詢 | 需讀多檔案 | 單一 SQL |
| Corruption Recovery | 單檔損壞不影響其他 | 整個 DB 可能損壞 |
| 依賴 | 無 | 需 SQLite library |

#### 情境選擇建議

| 部署情境 | 推薦 | 原因 |
|----------|------|------|
| **嵌入式裝置** | Binary File | 快寫、快 rotation、無依賴 |
| **強大設備 + Admin API** | SQLite | 複雜查詢、聚合運算、工具生態 |
| **需遠端/多用戶存取** | PostgreSQL | 並發、權限、網路存取 |

#### SQLite 適用場景

當設備夠強大且需提供 Admin API 時，SQLite 的優勢：

```sql
-- 複雜條件查詢
SELECT * FROM recordings
WHERE sensor_id = 'xxx'
  AND timestamp BETWEEN @start AND @end
  AND value > 100

-- 聚合統計
SELECT AVG(value), MAX(value), MIN(value)
FROM recordings
WHERE sensor_id = 'xxx'

-- 跨 Sensor 比對
SELECT a.value, b.value
FROM recordings a
JOIN recordings b ON a.timestamp = b.timestamp
WHERE a.sensor_id = 'temp' AND b.sensor_id = 'humidity'
```

> **目前選擇 Binary File**：因為主要需求是「快寫 + 快 rotation」，且 Query 很少使用。

---

## 相關文件

- [appsettings_configuration.md](appsettings_configuration.md) - 應用程式設定
- [telemetry_event_hooks.md](telemetry_event_hooks.md) - Telemetry 事件鉤子
