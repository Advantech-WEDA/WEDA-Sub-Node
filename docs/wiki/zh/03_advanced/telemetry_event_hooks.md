# Telemetry 事件 Hooks

本文件說明 telemetry 資料流 pipeline 中可用的事件 hooks，讓您可以監控和除錯每個階段的資料轉換。

## 資料流程總覽

```mermaid
flowchart TB
    subgraph Device["裝置硬體"]
        Sensor["Device Sensor"]
    end

    subgraph Read["ReadTelemetryAsync()"]
        ReadDesc["從硬體讀取原始感測器資料"]
    end

    DataReceivedEvent["DataReceived 事件<br/>───────────────────<br/>EnableDataReceivedTracking = true<br/>內容: 原始 telemetry 量測值"]

    subgraph Pipeline["Telemetry Pipeline"]
        subgraph TransformStage["階段 1: Transform Pipeline (資料轉換)"]
            T1["Transform 1<br/>(校正轉換)"]
            T2["Transform 2<br/>(單位轉換)"]
            TN["Transform N"]
            T1 --> T2 --> TN
        end

        TransformEvent["ValueChanged 事件<br/>Stage = Transform<br/>───────────────────<br/>EnableValueChangeTracking = false<br/>內容: InputValues, OutputValues"]

        subgraph FilterStage["階段 2: DSP Filter Pipeline (濾波)"]
            F1["Filter 1<br/>(移動平均)"]
            F2["Filter 2<br/>(卡爾曼濾波)"]
            FN["Filter N"]
            F1 --> F2 --> FN
        end

        FilterEvent["ValueChanged 事件<br/>Stage = Filter<br/>───────────────────<br/>EnableValueChangeTracking = false<br/>內容: InputValues, OutputValues"]

        subgraph SendStage["階段 3: Cloud Send (雲端發送)"]
            SendDesc["發送至 WEDA Cloud"]
        end
    end

    TelemetrySentEvent["TelemetrySent 事件<br/>───────────────────<br/>EnableTelemetrySentTracking = true<br/>內容: MeasureCount, Success"]

    Sensor --> Read
    Read --> DataReceivedEvent
    DataReceivedEvent --> TransformStage
    TransformStage --> TransformEvent
    TransformEvent --> FilterStage
    FilterStage --> FilterEvent
    FilterEvent --> SendStage
    SendStage --> TelemetrySentEvent

    style DataReceivedEvent fill:#e1f5fe,stroke:#0288d1
    style TransformEvent fill:#fff3e0,stroke:#ff9800
    style FilterEvent fill:#fff3e0,stroke:#ff9800
    style TelemetrySentEvent fill:#e8f5e9,stroke:#4caf50
```

## 其他裝置事件

```mermaid
flowchart LR
    subgraph Connection["連線事件"]
        CSC["ConnectionStateChanged<br/>─────────────────<br/>EnableConnectionStateTracking = true<br/>內容: PreviousState, CurrentState"]
    end

    subgraph Status["狀態事件"]
        DSC["DeviceStatusChanged<br/>─────────────────<br/>EnableDeviceStatusTracking = true<br/>內容: FromStatus, ToStatus"]
    end

    subgraph Cloud["雲端事件"]
        CUR["ConfigurationUpdateReceived<br/>─────────────────<br/>EnableConfigurationUpdateTracking = true<br/>內容: 組態 payload"]
        CR["CommandReceived<br/>─────────────────<br/>EnableCommandReceivedTracking = true<br/>內容: 命令詳情"]
    end

    style CSC fill:#e3f2fd,stroke:#1976d2
    style DSC fill:#fce4ec,stroke:#c2185b
    style CUR fill:#f3e5f5,stroke:#7b1fa2
    style CR fill:#f3e5f5,stroke:#7b1fa2
```

## 事件總覽表

| 事件 | 啟用旗標 | 預設值 | 說明 |
|------|----------|--------|------|
| `DataReceived` | `EnableDataReceivedTracking` | `false` | 來自裝置的原始 telemetry 資料 |
| `ValueChanged` | `EnableValueChangeTracking` | `false` | 每個 transform/filter 階段的值變化 |
| `TelemetrySent` | `EnableTelemetrySentTracking` | `false` | 雲端發送結果 |
| `ConnectionStateChanged` | `EnableConnectionStateTracking` | `false` | 裝置連線狀態變化 |
| `DeviceStatusChanged` | `EnableDeviceStatusTracking` | `false` | 裝置生命週期狀態 |
| `ConfigurationUpdateReceived` | `EnableConfigurationUpdateTracking` | `false` | 雲端組態更新 |
| `CommandReceived` | `EnableCommandReceivedTracking` | `false` | 雲端命令執行 |

> **注意:** 所有追蹤旗標預設為 `false` 以提升效能。只啟用您需要的事件。

## 使用範例

```csharp
public class MyDevice : TcpModbusDevice
{
    public MyDevice(IWedaApplicationContext context, DeviceConfiguration config)
        : base(context, config)
    {
        // 啟用並訂閱需要的事件
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;

        EnableValueChangeTracking = true;
        ValueChanged += OnValueChanged;

        EnableTelemetrySentTracking = true;
        TelemetrySent += OnTelemetrySent;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogInformation("原始資料: {Count} 個量測值", e.Data.Count);
        foreach (var m in e.Data)
        {
            _logger.LogDebug("  {ResourceId}: {Value}", m.ResourceId, m.Value);
        }
    }

    private void OnValueChanged(object? sender, TelemetryValueChangedEvent e)
    {
        var stageType = e.Stage == ValueChangeStage.Transform ? "Transform" : "Filter";

        _logger.LogDebug(
            "[{StageType}] {StageName} (#{Index}): {In} -> {Out} 個值 ({Duration:F2}ms)",
            stageType, e.StageName, e.StageIndex,
            e.InputValues.Count, e.OutputValues.Count,
            e.Duration?.TotalMilliseconds ?? 0);

        // 記錄個別值的變化
        if (e.ValuesChanged)
        {
            for (int i = 0; i < Math.Max(e.InputValues.Count, e.OutputValues.Count); i++)
            {
                var input = i < e.InputValues.Count ? e.InputValues[i].Value?.ToString() : "(無)";
                var output = i < e.OutputValues.Count ? e.OutputValues[i].Value?.ToString() : "(已過濾)";

                if (input != output)
                    _logger.LogDebug("  [{Index}] {Input} -> {Output}", i, input, output);
            }
        }
    }

    private void OnTelemetrySent(object? sender, TelemetrySentEvent e)
    {
        _logger.LogInformation("已發送 {Count} 個量測值, 成功={Success}",
            e.MeasureCount, e.Success);
    }
}
```

## TelemetryValueChangedEvent 屬性

| 屬性 | 型別 | 說明 |
|------|------|------|
| `DeviceId` | `string` | 裝置識別碼 |
| `ResourceId` | `string` | 感測器/資源識別碼 |
| `StageName` | `string` | Transform 或 Filter 的名稱 |
| `Stage` | `ValueChangeStage` | `Transform` 或 `Filter` |
| `StageIndex` | `int` | 在 pipeline 中的索引 (從 0 開始) |
| `InputValues` | `IReadOnlyList<TelemetryMeasure>` | 處理前的值 |
| `OutputValues` | `IReadOnlyList<TelemetryMeasure>` | 處理後的值 |
| `Duration` | `TimeSpan?` | 處理耗時 |
| `ValuesChanged` | `bool` | 值是否有變化 |
| `CountDelta` | `int` | 輸出數量 - 輸入數量 |
| `Timestamp` | `DateTimeOffset` | 事件時間戳 |

## 效能考量

- **EnableValueChangeTracking** 預設為 `false`，因為它會在每個 pipeline 階段建立 telemetry 資料的副本
- 僅在除錯或需要詳細監控時才啟用
- 在正式環境中，可考慮停用不需要的事件以減少額外開銷
