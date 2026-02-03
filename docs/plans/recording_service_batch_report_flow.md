# RecordingService 與 BatchReportCommand 流程

## 1. RecordingService — 資料錄製流程

```mermaid
flowchart TD
    A[裝置送出遙測資料] --> B[RecordAsync\nsensorId, interval, timestamp, value]
    B --> C{ShouldRecord?}
    C -->|計算 time-slot\n比對 _lastRecordedSlot| D{是否重複?}
    D -->|重複| E[跳過，不錄製]
    D -->|不重複| F[更新 _lastRecordedSlot]
    F --> G{BatchEnabled?}
    G -->|No| H[直接寫入\nBinaryRecordStorage]
    G -->|Yes| I[放入 _batchBuffers\nkey = sensorId:interval]
    I --> J{buffer.Count\n>= BatchMaxSamples?}
    J -->|Yes| K[立即 WriteBatchAsync\n寫入 Storage]
    J -->|No| L[等待定時 Flush]

    subgraph 定時器 FlushTimer
        M[每 FlushIntervalSeconds 觸發] --> N[ExecutePeriodicFlush]
        N --> O[遍歷所有 _batchBuffers]
        O --> P[逐一 WriteBatchAsync]
        P --> Q[清空 buffer]
    end

    L -.-> M
```

### 儲存格式

```
.weda/data/recording/
└── {sensorId}/
    ├── 2026-02-01_5000.bin      ← interval=5s 的資料
    ├── 2026-02-01_60000.bin     ← interval=60s 的資料
    └── 2026-02-02_5000.bin
```

---

## 2. RecordingService — 資料查詢流程

```mermaid
flowchart TD
    A[GetSensorIdsAsync] --> B[掃描 recording 目錄]
    B --> C[回傳 sensorId 列表]

    D[GetRecordingsAsync\nsensorId, start, end] --> E[計算日期範圍]
    E --> F[逐日讀取 BinaryRecordStorage]
    F --> G[解析 .bin 檔案]
    G --> H[組裝 RecordingResult]
    H --> I[回傳 Measures 列表]

    subgraph RecordingResult
        J[SensorId: string]
        K["Measures: List&lt;RecordingMeasureResult&gt;"]
        K --> K1[Interval: int ms]
        K --> K2[StartTimeStamp: long]
        K --> K3["Values: IReadOnlyList&lt;double&gt;"]
    end
```

---

## 3. BatchReportCommand — 指令結構

```mermaid
flowchart LR
    subgraph "BatchReportCommand [DeviceCmd: report.historical]"
        A[timeRange\nStartTime / EndTime\n預設: 最近10分鐘]
        B[sensorFilter\nInclude / Exclude]
        C[maxBatchesPerMessage\n1~1000, 預設1]
        D[maxBatchSize\n1~100000, 預設10000]
        E[transmissionRateLimit\n0~10000 msg/sec]
        F[timeout\n1~3600s, 預設300]
        G[respTopic\n回應主題]
    end
```

---

## 4. BatchReportCommandHandler — 完整執行流程

```mermaid
flowchart TD
    A[雲端下發\nBatchReportCommand] --> B[CommandDispatcher\n設定 SeqId / ReqSeqId]
    B --> C[BatchReportCommandValidator\n驗證參數]
    C --> D{驗證通過?}
    D -->|No| E[回傳錯誤\nInvalidTimeRange]
    D -->|Yes| F[取得 RecordingService\nfrom IWedaApplicationContext]

    F --> G[recordingService\n.GetSensorIdsAsync]
    G --> H[套用 SensorFilter\nInclude / Exclude]
    H --> I[計算預估批次數與樣本數]
    I --> J["送出 ACK 回應\n(estimatedBatches, estimatedSamples)"]

    J --> K[開始逐一處理 Sensor]
    K --> L[recordingService\n.GetRecordingsAsync\nsensorId, start, end]

    L --> M{measure.Values.Count\n> maxBatchSize?}
    M -->|Yes| N["垂直切割\n拆成多個子批次\n每批 ≤ maxBatchSize"]
    M -->|No| O[保持原樣]

    N --> P[放入 batchBuffer]
    O --> P

    P --> Q{batchBuffer.Count\n>= maxBatchesPerMessage?}
    Q -->|Yes| R[組裝 BatchTelemetrySendMessage\n送出到雲端]
    Q -->|No| S[繼續累積]

    R --> T{transmissionRateLimit > 0?}
    T -->|Yes| U[Task.Delay 限速]
    T -->|No| V[繼續]
    U --> V

    V --> W[送出 Progress 更新]
    W --> X{還有更多 Sensor?}
    X -->|Yes| K
    X -->|No| Y[送出剩餘 batchBuffer]

    Y --> Z["回傳 BatchReportResult\n(batchesSent, totalSamples, dataGaps)"]
```

---

## 5. 回應生命週期

```mermaid
sequenceDiagram
    participant Cloud as 雲端
    participant Handler as BatchReportCommandHandler
    participant RS as RecordingService
    participant Storage as BinaryRecordStorage

    Cloud->>Handler: BatchReportCommand
    Handler->>Handler: 驗證參數
    Handler->>RS: GetSensorIdsAsync()
    RS->>Storage: 掃描目錄
    Storage-->>RS: sensorId 列表
    RS-->>Handler: [sensor1, sensor2, ...]
    Handler->>Handler: 套用 SensorFilter
    Handler-->>Cloud: ACK (預估批次/樣本數)

    loop 每個 Sensor
        Handler->>RS: GetRecordingsAsync(sensorId, start, end)
        RS->>Storage: 讀取 .bin 檔案
        Storage-->>RS: 二進位資料
        RS-->>Handler: RecordingResult (Measures[])
        Handler->>Handler: 垂直切割 + 水平分組
        Handler-->>Cloud: BatchTelemetrySendMessage
        Handler-->>Cloud: Progress 更新
    end

    Handler-->>Cloud: Result (最終統計)
```

---

## 6. 切割策略說明

### 垂直切割 (Vertical Split)

單一 measure 的 samples 超過 `maxBatchSize` 時，拆成多個子批次：

```
原始 measure: Values = [v0, v1, ..., v24999]  (25000 筆)
maxBatchSize = 10000

切割後:
├── Batch 1: startTs=T0,           Values=[v0 ~ v9999]
├── Batch 2: startTs=T0+10000*interval, Values=[v10000 ~ v19999]
└── Batch 3: startTs=T0+20000*interval, Values=[v20000 ~ v24999]
```

### 水平分組 (Horizontal Split)

多個 measure 累積超過 `maxBatchesPerMessage` 時，分成多則訊息：

```
batchBuffer 中有 5 個 measures，maxBatchesPerMessage = 2

送出:
├── Message 1: measures = [measure1, measure2]
├── Message 2: measures = [measure3, measure4]
└── Message 3: measures = [measure5]
```

---

## 7. 錯誤狀態碼

| Code | 名稱 | 說明 |
|------|------|------|
| 0 | Success | 全部完成 |
| 1 | PartialSuccess | 部分 sensor 有資料缺口 |
| 2 | InvalidTimeRange | 時間範圍無效 |
| 3 | NoDataAvailable | 查無資料 |
| 4 | StorageError | 儲存層讀取錯誤 |
| 5 | Timeout | 超時 (預設 300s) |
| 6 | PermissionDenied | 權限不足 |
| 7 | ResourceExhausted | 資源耗盡 |
