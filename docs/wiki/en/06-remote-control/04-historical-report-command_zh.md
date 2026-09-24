---
sidebar_position: 4
sidebar_label: '歷史資料回報指令'
hide_title: true
title: '歷史資料回報指令 | SubNode SDK'
keywords: ['SubNode', 'Command', 'report.historical', 'Batch Report', 'Recording', 'Backfill', 'Historical Data']
description: '透過 report.historical 把 SubNode 本機錄下的 telemetry 以批次歷史上傳的方式重放回 WedaCore。'
---

# 歷史資料回報指令

> 把 SubNode 錄在磁碟上的 telemetry 重放回 WedaCore，分批並可限速。

## 概觀

`report.historical` 是一個**通用指令**——不論掛了哪些裝置，每個 SubNode 都會提供它（見
[指令能力閘控](./03-command-capability-gating.md)）。它會讀取本機錄製儲存區中某個時間範圍的資料，把
各感測器的取樣轉成批次 telemetry 訊息，發佈到 SubNode 的批次 telemetry topic，同時在指令回應 topic 上
串流回傳「確認、進度、最終結果」三種訊息。

典型用途是**補資料**：上行斷線期間 SubNode 仍持續錄製，現在由操作人員要求它把雲端漏掉的部分上傳。

實作位於 `src/Weda.SubNode.Core/Commands/Handlers/BatchReport/`。

## 你會學到什麼

- 完整的請求 payload，以及每個參數的預設值與有效範圍
- 三段式回應序列（ack → progress → result）以及如何關聯它們
- telemetry 本身送到哪裡、長什麼樣子
- `maxBatchSize`、`maxBatchesPerMessage`、`transmissionRateLimit` 如何互相作用決定上傳頻寬
- 會遇到哪些狀態碼與錯誤碼，以及每一個在運維上代表什麼

## 前置條件

- SubNode 已啟用錄製——`builder.AddRecording()`。沒有的話指令會回 `STORAGE_UNAVAILABLE`。
- SubNode 已向 WedaCore 註冊（它需要一個被指派的批次 telemetry topic）。
- 熟悉[遠端控制設定](./01-configuration.md)裡的指令封包格式。

---

## 請求

指令以標準的 `CommandMessage` 封包送達；`data` 帶著 `CommandData` 欄位，加上 `report.historical`
專屬的 `parameters` 物件。

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

### 封包欄位（`data`）

| 欄位 | 型別 | 預設 | 意義 |
|---|---|---|---|
| `deviceCmd` | string | — | 固定是 `report.historical` |
| `timeout` | uint（秒） | `30` | **整趟**執行的硬性期限。超過就在傳輸中途以 `TIMEOUT` 中止；已經發出去的批次不會回滾。 |
| `respTopic` | string | — | ack／progress／result 送到哪裡。留空時指令照樣執行，但不發任何回應。 |

:::caution
預設的 `timeout` 30 秒對真正的補資料而言太短。請依預期執行時間調整（見
[流量控制與頻寬](#流量控制與頻寬)），否則上傳會在中途被切斷。
:::

### 參數

| 參數 | 型別 | 必填 | 預設 | 範圍 | 意義 |
|---|---|---|---|---|---|
| `timeRange` | object | 否 | 最近 10 分鐘 | — | 查詢視窗。省略則為 `now − 10 min … now`。 |
| `timeRange.startTime` | int64 | 是¹ | — | 不可在未來 | 視窗起點，**Unix 毫秒** |
| `timeRange.endTime` | int64 | 是¹ | — | `> startTime` | 視窗終點，**Unix 毫秒** |
| `sensorFilter` | object | 否 | 全部感測器 | — | 感測器篩選 |
| `sensorFilter.include` | string[] | 否 | 全部 | — | 只保留這些感測器短 ID。空或不存在表示不做 include 限制。 |
| `sensorFilter.exclude` | string[] | 否 | 無 | — | 排除這些感測器短 ID。在 `include` **之後**套用。 |
| `maxBatchesPerMessage` | int | 否 | `1` | 1–1000 | 單一外送訊息裡最多幾筆 measure |
| `maxBatchSize` | int | 否 | `10000` | 1–100000 | 送出訊息前緩衝的最大取樣數。單一感測器結果集更大時會被拆成多則訊息。 |
| `transmissionRateLimit` | int | 否 | `0` | 0–10000 | 每秒訊息數。`0` 表示不限速。 |

¹ 只有在有帶 `timeRange` 時才必填。

篩選器裡的感測器 ID 是錄製儲存區使用的**短 ID**，比對時不分大小寫。

### 驗證

有兩層會擋掉格式錯誤的請求：

`BatchReportCommandValidator`（派送前）：

- `timeRange.endTime` 必須大於 `timeRange.startTime`
- `timeRange.startTime` 不可在未來

`BatchReportCommandHandler`（執行時，針對套用預設後的*有效*範圍）：

| 檢查 | 錯誤碼 | 狀態 |
|---|---|---|
| 未設定錄製服務 | `STORAGE_UNAVAILABLE` | `4` HardwareError |
| SubNode 未註冊 | `NOT_REGISTERED` | `5` PermissionDenied |
| `startTime >= endTime` | `INVALID_TIME_RANGE` | `1` ValidationFailed |
| 視窗大於 `MaxQueryTimeRangeDays` | `TIME_RANGE_TOO_LARGE` | `1` ValidationFailed |

參數上的範圍標註（`maxBatchSize` 1–100000 等）是給 DTDL 與入口網站表單產生用的，handler 內部不會
再檢查一次。

---

## 回應序列

`report.historical` 設了 `[AutoAck(false)]`——派送器**不會**送出通用的「已收到」ack。handler 會在
`respTopic` 上自己發三種訊息，三者共用請求的 `seqId` 與 `reqSeqId`，以 `data.msgType` 區分。

```
   雲端 ──── report.historical ───▶ SubNode
        ◀─── msgType: "ack"      ── （估計值）
        ◀─── msgType: "progress" ── （每送出一則訊息一次）
        ◀─── msgType: "progress" ──
                   ...
        ◀─── msgType: "result"   ── （最終狀態、缺口）

        ◀═══ 批次 telemetry ════ （走批次 topic，不是 respTopic）
```

### 1. 初始 ack

在第一次讀取之前送出一次，帶著事前估計：

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

估計值來自各感測器**設定的回報間隔**，不是來自儲存區：

```
samples_i          = ceil(timeRangeMs / intervalMs_i) + 1        （間隔預設回退為 1000 ms）
estimatedSamples   = Σ samples_i
estimatedBatches   = Σ max(1, ceil(samples_i / maxBatchSize))
estimatedDuration  = ceil(messages / transmissionRateLimit)      有限速時
                   = max(1, messages / 10)                       否則（假設約 10 msg/s）
```

它們是規劃用的參考，不是承諾——一個回報不規律、或部分資料已超過保留期而被清掉的感測器，實際取樣會
比估計少。

### 2. 進度

每送出一則訊息就發一次，所以進度訊息的頻率等於 telemetry 訊息的頻率：

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

`percentComplete` 追蹤的是**已處理的感測器數**，不是已送出的取樣數，所以它會以「單一感測器佔的比重」
為單位跳動。

### 3. 最終結果

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

| 欄位 | 意義 |
|---|---|
| `batchesSent` | 實際發佈的訊息數（不是 measure 筆數） |
| `totalSamples` | 所有已發佈訊息裡的取樣總數 |
| `timeRange` | **有效**範圍，ISO 8601——省略 `timeRange` 時會回傳套用後的預設值 |
| `sensors` | 成功讀取的感測器，包含那些結果其實是空的 |
| `dataGaps` | 空的時候整個欄位會省略；見下 |

發生錯誤時，`resultData` 會被 `errorDetails` 取代：

```json
{ "errorCode": "TIME_RANGE_TOO_LARGE", "recommendation": "Adjust time range to available period" }
```

---

## telemetry 送到哪裡

取樣**不會**走 `respTopic`。它們以 `BatchTelemetrySendMessage` 發佈到 SubNode 被指派的批次 telemetry
topic（註冊回應裡的 `dm.dt.batch.rl`）：

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

| 欄位 | 意義 |
|---|---|
| `id` | 感測器短 ID（ResourceId 的最後 5 個字元） |
| `interval` | 相鄰兩個值之間的間隔 |
| `startTimeStamp` | `values[0]` 的時間戳 |
| `values` | 依時間排序的陣列；`null` 代表該取樣無法轉成數字 |

值會被轉成 `double`：非有限的 double，以及 `double`／`int`／`long`／`bool`（`true` → `1.0`、
`false` → `0.0`）以外的任何型別，都會變成 `null`。

:::caution 單位
`interval` 與 `startTimeStamp` 是從錄製儲存區直接帶過來的，而儲存區兩者都以**毫秒**保存
（`RecordingDataPoint.Timestamp` 與錄製檔標頭都記載為 Unix 毫秒）。`BatchTelemetryMeasureDto` 合約的
註解卻寫成秒。消費端應該以儲存區實際送出的為準——毫秒——並把那段合約註解視為過期。
:::

---

## 資料缺口

只有在至少一個感測器讀取失敗時才會出現 `dataGaps`。每一筆都涵蓋整個查詢視窗，因為那次讀取是整段
都失敗：

```json
{
  "sensorId": "d4e5f",
  "startTime": "2026-09-04T10:00:00.000Z",
  "endTime": "2026-09-04T11:00:00.000Z",
  "reason": "sensorError",
  "missingSamples": 3601
}
```

`missingSamples` 是依該感測器設定的回報間隔估算的，不是數出來的。

如果某個感測器的錄製資料在「列舉」與「讀取」之間剛好超過保留期，儲存區會回 `NotFound`。那**不是**
缺口——該感測器算是已處理，只是沒有貢獻任何資料。只有真正的讀取失敗才會產生缺口項目。

---

## 狀態碼與錯誤碼

| `status` | 名稱 | 什麼時候 | `errorCode` |
|---|---|---|---|
| `0` | Success | 至少送出一則訊息且沒有失敗 | — |
| `1` | ValidationFailed | 時間範圍錯誤或過大 | `INVALID_TIME_RANGE`、`TIME_RANGE_TOO_LARGE` |
| `3` | Timeout | 超過 `timeout`，或被外部取消 | `TIMEOUT`、`CANCELLED` |
| `4` | HardwareError | 沒有錄製服務，或感測器列舉失敗 | `STORAGE_UNAVAILABLE`、`STORAGE_ERROR` |
| `5` | PermissionDenied | SubNode 未註冊 | `NOT_REGISTERED` |
| `8` | PartialSuccess | 部分感測器或批次失敗——見 `dataGaps` | — |
| `9` | NotFound | 沒有感測器符合篩選條件，或該範圍內沒有資料 | — |
| `10` | ResourceExhausted | 載入資料時記憶體不足 | `RESOURCE_EXHAUSTED` |

錯誤回應會帶一則 `recommendation`：

| 狀態 | 建議 |
|---|---|
| ValidationFailed | Adjust time range to available period |
| HardwareError | Check device storage health and retry |
| Timeout | Reduce query scope or increase timeout |
| ResourceExhausted | Reduce batch size or add rate limiting |

注意 `NotFound`（`9`）是以*成功*結果、零批次的形式回傳——它的意思是「查詢是有效的，只是那裡什麼都
沒有」。

---

## 流量控制與頻寬

緩衝區會在兩個上限中先觸發的那一個時送出：`maxBatchesPerMessage` 筆 measure，或 `maxBatchSize` 個
取樣。每次送出後 handler 會發佈該訊息、發一則進度更新，然後睡 `floor(1000 / transmissionRateLimit)`
毫秒。

令 `L̄` 為每個已儲存錄製區塊的平均取樣數：

```
每則訊息的取樣數  n_msg = min(maxBatchSize, maxBatchesPerMessage · L̄)
每則訊息的筆數    m_msg = min(maxBatchesPerMessage, ceil(maxBatchSize / L̄))
```

訊息大小（JSON，在 NATS 上未壓縮）：

```
S_msg ≈ 156 + m_msg · 70 + n_msg · b_v      bytes
```

其中 `156` 是封包（會隨裝置 ID 變長），`70` 是每筆 measure 的額外負擔，`b_v` 是每個取樣含分隔符的
位元組數——兩位小數約 6，完整往返精度的 double 最多約 25。每次送出還會在 `respTopic` 上多發一則約
300 位元組的進度訊息。

有限速時的持續上行位元率：

```
B ≈ 8 · transmissionRateLimit · (S_msg + 300)     bit/s
T_run ≈ ceil(totalSamples / n_msg) / transmissionRateLimit    秒
```

:::caution
`transmissionRateLimit` 限制的是**訊息速率，不是頻寬**。真正決定鏈路負載的是
`速率 × 訊息大小` 的乘積。在限速不變的情況下把 `maxBatchesPerMessage` 從 10 拉到 200，位元率可能
提高一個數量級以上。
:::

要符合鏈路預算，先決定訊息大小，再回推速率：

```
transmissionRateLimit = B_target / (8 · (S_msg + 300))
```

實例——20 個感測器、1 秒間隔、一小時、`maxBatchSize` 10000、`maxBatchesPerMessage` 10、`L̄` = 60、
`b_v` = 6、限速 5：

| 量 | 值 |
|---|---|
| 取樣數 | 72,020 |
| 每則訊息取樣數 | 600（由 `maxBatchesPerMessage` 決定；`maxBatchSize` 從未觸及） |
| 訊息大小 | 約 4.5 kB |
| 訊息數 | 121 |
| 執行時間 | 約 24 秒 → **`timeout` 要設得遠高於 30 秒** |
| 位元率 | 約 190 kbit/s |

因為 `floor(1000 / rate)` 是整數毫秒除法，任何大於 1000 的 `transmissionRateLimit` 都等於完全不延遲。
設為 `0` 時，吞吐量只受限於儲存區讀取與 NATS 發佈。

---

## 儲存設定

錄製行為設定在 `SystemConfig:Record`（`RecordingOptions`）底下，決定 `report.historical` 找得到什麼：

| 選項 | 預設 | 對這個指令的影響 |
|---|---|---|
| `MaxQueryTimeRangeDays` | `30` | 超過這個寬度的視窗會被 `TIME_RANGE_TOO_LARGE` 拒絕。`0` 會關閉這個檢查（不建議——它是 DoS 防護）。 |
| `RetentionDays` | `7` | 比這更舊的資料可能已被刪除；查得更久以前只會默默拿到比較少的資料。 |
| `MaxStorageSizeMb` | `1024` | 環狀緩衝上限；超過時最舊的檔案先被丟掉。 |
| `MinFreeDiskSpaceMb` | `128` | 可用空間低於此值時，最舊的檔案會被丟掉。 |
| `Enabled` | `true` | 設為 false 時不再錄新資料——既有資料仍可查詢。 |

```csharp
builder.AddRecording(options =>
{
    options.RetentionDays = 30;
    options.MaxQueryTimeRangeDays = 30;
});
```

---

## 範例

**補預設視窗**——最近 10 分鐘錄到的所有資料：

```json
{ "deviceCmd": "report.historical", "timeout": 120,
  "respTopic": "...", "parameters": {} }
```

**單一感測器、一小時、對行動網路上行溫和一點：**

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

**除了吵雜的振動通道以外全部，並以鏈路允許的最快速度：**

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

## 疑難排解

| 症狀 | 可能原因 |
|---|---|
| `STORAGE_UNAVAILABLE` | 從來沒呼叫過 `AddRecording()`——沒有錄製服務 |
| `NOT_REGISTERED` | SubNode 還沒拿到指派的 ID；註冊尚未完成 |
| `status 9` 且 `sensors: []` | `sensorFilter` 沒有比對到任何東西——篩選器用的是**短 ID**，不是 ResourceId |
| `status 9` 但有列出感測器 | 感測器存在，但該視窗內沒有資料——檢查 `RetentionDays` |
| 部分上傳後 `TIMEOUT` | `timeout` 低於實際執行時間；已經送出去的批次會留在雲端 |
| `TIME_RANGE_TOO_LARGE` | 視窗超過 `MaxQueryTimeRangeDays`（預設 30 天） |
| 有限速但上行仍被塞滿 | 訊息變大了——見[流量控制與頻寬](#流量控制與頻寬) |
| `respTopic` 上什麼都沒收到 | 請求裡的 `respTopic` 是空的；那趟執行還是發生了 |
| 時間戳落在 1970 年 | 消費端把 `startTimeStamp` 當成秒解讀——儲存區送出的是毫秒 |

## 相關連結

- [指令能力閘控](./03-command-capability-gating.md)
- [遠端控制設定](./01-configuration.md)
- [資料管線概觀](../05-data-pipeline/01-overview.md)
