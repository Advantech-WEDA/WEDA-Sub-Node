---
title: "數據轉換與濾波器 Pipeline (Transform & DSP)"
description: "如何使用和自訂 Transform 與 DSP Filter 來處理感測器數據"
author: "Rain Hu"
date: "2025-11-17"
lang: "zh"
translations:
  - lang: "en"
    path: "../../en/02_use_cases/02_transformation.md"
---

# 數據轉換 Pipeline (Transform & DSP)

本文檔說明如何使用和自訂 Transform（轉換）與 DSP Filter（數位訊號處理濾波器）來處理感測器數據。

---

## 目錄

- [概覽](#概覽)
- [Transform vs DSP Filter](#transform-vs-dsp-filter)
- [方式 1: 程式化方式 (Programmatic)](#方式-1-程式化方式-programmatic)
  - [使用內建 Transform](#使用內建-transform)
  - [使用內建 DSP Filter](#使用內建-dsp-filter)
  - [自訂 Transform](#自訂-transform)
  - [自訂 DSP Filter](#自訂-dsp-filter)
- [方式 2: 配置檔方式 (appsettings.json)](#方式-2-配置檔方式-appsettingsjson)
  - [配置 Transform Pipeline](#配置-transform-pipeline)
  - [配置 DSP Pipeline](#配置-dsp-pipeline)
- [Real-time 參數調整](#real-time-參數調整)
  - [操作類型](#操作類型)
  - [參數驗證](#參數驗證)
  - [透過 Cloud 更新](#透過-cloud-更新)
- [效能監控 (Performance Monitoring)](#效能監控-performance-monitoring)
  - [Pipeline 統計資料](#pipeline-統計資料)
  - [事件監聽](#事件監聽)
- [完整範例](#完整範例)
- [最佳實踐](#最佳實踐)

---

## 概覽

Weda SubNode SDK 提供數據處理的 pipeline，允許您在感測器數據上雲前進行:

1. **Transform (轉換)**: 校正、單位轉換、數學運算
2. **DSP Filter (濾波)**: 降噪、平滑化、訊號處理

### 執行順序

```
Raw Data → Transform Pipeline → DSP Pipeline → Cloud
```

每個感測器可以有自己的 Transform 和 DSP pipeline，也可以使用裝置層級的共用 pipeline。

---

## Transform vs DSP Filter

| 特性 | Transform | DSP Filter |
|------|-----------|------------|
| **用途** | 確定性數據轉換 | 統計性訊號處理 |
| **範例** | 校正、單位轉換 | 降噪、移動平均、卡爾曼濾波 |
| **執行時機** | 先執行 | 後執行 |
| **適用場景** | 需要精確轉換數值 | 需要平滑或濾除噪音 |

---

## 方式 1: 程式化方式 (Programmatic)

程式化方式適合:
- 每種感測器需要不同的轉換和參數
- 轉換邏輯複雜，需要動態調整
- 需要型別安全和 IntelliSense 支援

### 使用內建 Transform

#### 1. Calibration Transform (校正轉換)

用於線性校正感測器數值。

**公式**: `output = (input × Scale) + Offset`

```csharp
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Core.Transforms;

var config = new TcpModbusDeviceConfiguration
{
    DeviceName = "TemperatureSensor",
    Host = "192.168.1.100",
    Port = 502
};

// 添加溫度感測器
var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 0,
    DataType = ModbusDataType.UInt16
};

// 添加校正轉換: 將原始值 (0-1000) 轉換為實際溫度 (-50 ~ 150°C)
// output = (input × 0.2) - 50
tempSensor.Config.AddTransform(new CalibrationTransform(
    scale: 0.2,    // 縮放係數
    offset: -50.0  // 偏移量
));

config.AddSensor(tempSensor);
```

#### 2. Unit Conversion Transform (單位轉換)

用於在不同單位之間轉換。

```csharp
using Weda.SubNode.Core.Transforms;

var pressureSensor = new ModbusSensorReporturation
{
    Name = "pressure",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 1,
    DataType = ModbusDataType.Float
};

// 將壓力從 pascal 轉換為 bar
pressureSensor.Config.AddTransform(new UnitConversionTransform(
    targetResourceId: "*",    // "*" 表示套用到所有感測器
    fromUnit: "pascal",
    toUnit: "bar"
));

config.AddSensor(pressureSensor);
```

**支援的單位轉換**:
- 溫度: `celsius` ↔ `fahrenheit` ↔ `kelvin`
- 壓力: `pascal` ↔ `bar` ↔ `psi`
- 長度: `meter` ↔ `feet` ↔ `inch`

### 使用內建 DSP Filter

#### 1. Moving Average Filter (移動平均濾波)

用於平滑化數據，減少短期波動。

```csharp
using Weda.SubNode.Core.Dsp;

var noisySensor = new ModbusSensorReporturation
{
    Name = "vibration",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 2,
    DataType = ModbusDataType.UInt16
};

// 添加 5 點移動平均濾波
noisySensor.Config.AddDspFilter(new MovingAverageFilter(windowSize: 5));

config.AddSensor(noisySensor);
```

#### 2. Kalman Filter (卡爾曼濾波)

用於高精度感測器數據處理，同時考慮過程噪音和測量噪音。

```csharp
using Weda.SubNode.Core.Dsp;

var precisionSensor = new ModbusSensorReporturation
{
    Name = "position",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 3,
    DataType = ModbusDataType.Float
};

// 添加卡爾曼濾波
precisionSensor.Config.AddDspFilter(new KalmanFilter(
    processNoise: 0.01,       // 過程噪音（越小越信任模型）
    measurementNoise: 0.1,    // 測量噪音（越小越信任測量值）
    initialEstimate: 0.0,     // 初始估計值
    initialError: 1.0         // 初始誤差
));

config.AddSensor(precisionSensor);
```

### 組合多個 Transform 和 Filter

可以將多個 Transform 和 Filter 組合成 pipeline，按照添加順序執行:

```csharp
var complexSensor = new ModbusSensorReporturation
{
    Name = "advanced_temp",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 4,
    DataType = ModbusDataType.UInt16
};

// Pipeline 執行順序:
// 1. 校正: (input × 0.1) - 10
complexSensor.Config.AddTransform(new CalibrationTransform(0.1, -10.0));

// 2. 單位轉換: celsius → fahrenheit
complexSensor.Config.AddTransform(new UnitConversionTransform("*", "celsius", "fahrenheit"));

// 3. 移動平均濾波: 5 點平滑
complexSensor.Config.AddDspFilter(new MovingAverageFilter(5));

// 4. 卡爾曼濾波: 進一步降噪
complexSensor.Config.AddDspFilter(new KalmanFilter(0.01, 0.1));

config.AddSensor(complexSensor);
```

### 自訂 Transform

如果內建的 Transform 不符合需求，可以實作 `ITelemetryTransform` 介面。

#### 步驟 1: 實作 ITelemetryTransform

```csharp
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

/// <summary>
/// 自訂轉換: 將數值限制在指定範圍內
/// </summary>
public class ClampTransform : ITelemetryTransform
{
    private readonly double _min;
    private readonly double _max;

    public ClampTransform(double min, double max)
    {
        _min = min;
        _max = max;
    }

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TransformContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var measure in measures)
        {
            if (measure.Value is double value)
            {
                // 限制數值在 min 和 max 之間
                var clampedValue = Math.Max(_min, Math.Min(_max, value));

                // 創建新的 TelemetryMeasure 以保持不可變性
                measure.Value = clampedValue;
            }
        }

        return Task.FromResult(measures);
    }
}
```

#### 步驟 2: 使用自訂 Transform

```csharp
var sensor = new ModbusSensorReporturation
{
    Name = "limited_sensor",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 5,
    DataType = ModbusDataType.Float
};

// 使用自訂 Transform: 限制數值在 0-100 之間
sensor.Config.AddTransform(new ClampTransform(min: 0.0, max: 100.0));

config.AddSensor(sensor);
```

### 自訂 DSP Filter

實作 `IDspFilter` 介面來創建自訂 DSP Filter。

#### 步驟 1: 實作 IDspFilter

```csharp
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// 自訂 DSP Filter: 指數加權移動平均 (EWMA)
/// </summary>
public class ExponentialMovingAverageFilter : IDspFilter
{
    private readonly double _alpha; // 平滑係數 (0-1)
    private readonly Dictionary<string, double> _previousValues = new();

    public ExponentialMovingAverageFilter(double alpha)
    {
        if (alpha <= 0 || alpha > 1)
            throw new ArgumentException("Alpha must be between 0 and 1", nameof(alpha));

        _alpha = alpha;
    }

    public Task<List<TelemetryMeasure>> FilterAsync(
        List<TelemetryMeasure> measures,
        DspContext context,
        CancellationToken cancellationToken = default)
    {
        var filtered = new List<TelemetryMeasure>();

        foreach (var measure in measures)
        {
            if (measure.Value is double currentValue)
            {
                double filteredValue;

                if (_previousValues.TryGetValue(measure.ResourceId, out var previousValue))
                {
                    // EWMA 公式: value = α × current + (1 - α) × previous
                    filteredValue = _alpha * currentValue + (1 - _alpha) * previousValue;
                }
                else
                {
                    // 第一次測量，直接使用原始值
                    filteredValue = currentValue;
                }

                _previousValues[measure.ResourceId] = filteredValue;
                measure.Value = filteredValue;
            }

            filtered.Add(measure);
        }

        return Task.FromResult(filtered);
    }
}
```

#### 步驟 2: 使用自訂 DSP Filter

```csharp
var sensor = new ModbusSensorReporturation
{
    Name = "smooth_sensor",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 6,
    DataType = ModbusDataType.Float
};

// 使用自訂 EWMA Filter: alpha = 0.3 (較保守的平滑)
sensor.Config.AddDspFilter(new ExponentialMovingAverageFilter(alpha: 0.3));

config.AddSensor(sensor);
```

---

## 方式 2: 配置檔方式 (appsettings.json)

配置檔方式適合:
- 需要在不重新編譯的情況下調整參數
- 多個感測器使用相同的轉換配置
- 支援雲端動態更新配置

### 配置 Transform Pipeline

在 `appsettings.json` 中的 `Sensors[].Report.TransformPipeline` 設定:

```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "DeviceName": "WISE-4012",
      "SubNodeType": "adamEthernet",
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502,
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "DataType": "UInt16"
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "TransformPipeline": [
              {
                "Type": "Calibration",
                "Enabled": true,
                "Order": 0,
                "Parameters": {
                  "Scale": 0.1,
                  "Offset": -50.0
                }
              },
              {
                "Type": "UnitConversion",
                "Enabled": true,
                "Order": 1,
                "Parameters": {
                  "TargetResourceId": "*",
                  "FromUnit": "celsius",
                  "ToUnit": "fahrenheit"
                }
              }
            ]
          }
        }
      ]
    }
  }
}
```

### 配置 DSP Pipeline

在 `appsettings.json` 中的 `Sensors[].Report.DspPipeline` 設定:

```json
{
  "Sensors": [
    {
      "Name": "vibration",
      "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
      "SensorGroup": "AI",
      "Parameters": {
        "RegisterType": "HoldingRegister",
        "RegisterAddress": 1,
        "DataType": "UInt16"
      },
      "Config": {
        "Enabled": true,
        "Interval": 1000,
        "DspPipeline": [
          {
            "Type": "movingAverage",
            "Enabled": true,
            "Order": 0,
            "Parameters": {
              "WindowSize": 5
            }
          },
          {
            "Type": "kalman",
            "Enabled": true,
            "Order": 1,
            "Parameters": {
              "ProcessNoise": 0.01,
              "MeasurementNoise": 0.1,
              "InitialEstimate": 0.0,
              "InitialError": 1.0
            }
          }
        ]
      }
    }
  ]
}
```

### 內建 Transform 類型

| Type | 說明 | 必要參數 |
|------|------|----------|
| `Calibration` | 線性校正 | `Scale`, `Offset` |
| `UnitConversion` | 單位轉換 | `FromUnit`, `ToUnit`, `TargetResourceId` (選填) |

### 內建 DSP Filter 類型

| Type | 說明 | 必要參數 |
|------|------|----------|
| `movingAverage` | 移動平均濾波 | `WindowSize` |
| `kalman` | 卡爾曼濾波 | `ProcessNoise`, `MeasurementNoise`, `InitialEstimate` (選填), `InitialError` (選填) |
| `relu` | ReLU 濾波 | `Threshold` (選填) |

---

## Real-time 參數調整

SubNode SDK 支援在運行時動態調整 DSP Filter 和 Transform 的參數，無需重啟裝置。這對於需要根據實際情況微調參數的場景非常有用。

### 操作類型

| 操作 | 重建實體 | Warm-up | 說明 |
|------|---------|---------|------|
| **新增** | ✅ | ✅ | 建立新實例，需累積歷史資料 |
| **移除** | ✅ | - | 移除實例，釋放資源 |
| **更新參數** | ❌ | ❌ | 原地修改，內部狀態保留 |
| **暫停/恢復** | ❌ | ❌ | `Enabled=false` → pass-through |

### 參數驗證

每個 Filter 和 Transform 都有參數驗證規則，確保參數在合理範圍內：

| Filter/Transform | 參數 | 驗證規則 |
|------------------|------|----------|
| MovingAverageFilter | Window | > 0 |
| KalmanFilter | ProcessNoise | >= 0 |
| KalmanFilter | MeasurementNoise | > 0 |
| CalibrationTransform | Scale | != 0 |
| UnitConversionTransform | FromUnit, ToUnit | 不為空 |

#### 程式化驗證與更新

```csharp
using ErrorOr;
using Weda.SubNode.Core.Dsp;

var filter = new MovingAverageFilter(windowSize: 5);

// 驗證參數
var parameters = new Dictionary<string, object>
{
    ["Window"] = 10
};

var validationResult = filter.ValidateParameters(parameters);
if (validationResult.IsError)
{
    Console.WriteLine($"驗證失敗: {validationResult.FirstError.Description}");
    return;
}

// 更新參數 (內部狀態會被重置)
filter.UpdateParameters(parameters);

// 暫停 Filter (資料直接 pass-through)
filter.Enabled = false;

// 恢復 Filter
filter.Enabled = true;
```

#### Transform 參數更新

```csharp
using Weda.SubNode.Core.Transforms;

var calibration = new CalibrationTransform(scale: 1.0, offset: 0.0);

// 驗證並更新參數
var parameters = new Dictionary<string, object>
{
    ["Scale"] = 2.0,
    ["Offset"] = 10.0
};

var result = calibration.ValidateParameters(parameters);
if (!result.IsError)
{
    calibration.UpdateParameters(parameters);
    // 新參數立即生效，無 warm-up
}
```

### 透過 Cloud 更新

Cloud 可以發送配置更新訊息來動態調整 DSP Pipeline：

```json
{
  "cmd": "updateCmd",
  "data": {
    "cfg": {
      "desired": {
        "subNodeDeviceConfig": {
          "deviceConfigs": {
            "MyDevice": {
              "sensors": [{
                "name": "temperature",
                "config": {
                  "transformPipeline": [
                    {
                      "type": "Calibration",
                      "enabled": true,
                      "parameters": { "Scale": 1.1, "Offset": 0.5 }
                    }
                  ],
                  "dspPipeline": [
                    {
                      "type": "movingAverage",
                      "enabled": true,
                      "parameters": { "Window": 10 }
                    },
                    {
                      "type": "kalman",
                      "enabled": false,
                      "parameters": { "ProcessNoise": 0.05 }
                    }
                  ]
                }
              }]
            }
          }
        }
      }
    }
  }
}
```

#### 驗證失敗回報

當 Cloud 發送的參數驗證失敗時，SubNode 會回報錯誤：

```json
{
  "cmd": "updateCmdResponse",
  "data": {
    "cfg": {
      "reported": {
        "status": "invalid",
        "errorMessage": "Sensor 'temperature' DSP filter 'movingAverage': Window must be greater than 0"
      }
    }
  }
}
```

---

## 效能監控 (Performance Monitoring)

SubNode SDK 提供 Pipeline 效能監控功能，可追蹤處理時間和成功/失敗統計。

### Pipeline 統計資料

`TelemetryPipeline` 提供 `GetStatistics()` 方法來獲取統計資料：

```csharp
using Weda.SubNode.Core.Telemetry;

// 獲取 Pipeline 統計
var stats = pipeline.GetStatistics();

Console.WriteLine($"Device: {stats.DeviceId}");
Console.WriteLine($"Total Processed: {stats.TotalProcessed}");
Console.WriteLine($"Successfully Sent: {stats.SuccessfullySent}");
Console.WriteLine($"Failed to Send: {stats.FailedToSend}");
Console.WriteLine($"Filtered Out: {stats.FilteredOut}");
Console.WriteLine($"Last Processed: {stats.LastProcessedAt}");

// 平均處理時間
Console.WriteLine($"Avg Transform Duration: {stats.AverageTransformDuration.TotalMilliseconds:F2}ms");
Console.WriteLine($"Avg Filter Duration: {stats.AverageFilterDuration.TotalMilliseconds:F2}ms");
Console.WriteLine($"Avg Send Duration: {stats.AverageSendDuration.TotalMilliseconds:F2}ms");
Console.WriteLine($"Avg Total Duration: {stats.AverageTotalDuration.TotalMilliseconds:F2}ms");
```

#### PipelineStatistics 屬性

| 屬性 | 類型 | 說明 |
|------|------|------|
| `DeviceId` | string | 裝置 ID |
| `TotalProcessed` | int | 總處理次數 |
| `SuccessfullySent` | int | 成功發送次數 |
| `FailedToSend` | int | 發送失敗次數 |
| `FilteredOut` | int | 被濾除的數據數量 |
| `AverageTransformDuration` | TimeSpan | Transform 階段平均耗時 |
| `AverageFilterDuration` | TimeSpan | Filter 階段平均耗時 |
| `AverageSendDuration` | TimeSpan | 發送階段平均耗時 |
| `AverageTotalDuration` | TimeSpan | 總平均耗時 |
| `LastProcessedAt` | DateTimeOffset? | 最後處理時間 |

### 事件監聽

可訂閱 `StageExecuting` 事件來監控每個階段的執行：

```csharp
using Weda.SubNode.Abstractions.Events;

pipeline.StageExecuting += (sender, e) =>
{
    if (e.Phase == StagePhase.Before)
    {
        Console.WriteLine($"[{e.Timestamp:HH:mm:ss.fff}] {e.Stage} 開始: {e.InputCount} measures");
    }
    else // StagePhase.After
    {
        Console.WriteLine($"[{e.Timestamp:HH:mm:ss.fff}] {e.Stage} 完成: " +
            $"{e.OutputCount} measures, 耗時 {e.Duration?.TotalMilliseconds:F2}ms");

        if (e.Error != null)
        {
            Console.WriteLine($"  錯誤: {e.Error}");
        }
    }
};
```

#### TelemetryPipelineStageEvent 屬性

| 屬性 | 類型 | 說明 |
|------|------|------|
| `DeviceId` | string | 裝置 ID |
| `Stage` | PipelineStage | 階段 (Transform, Filter, Send) |
| `StageName` | string | 階段名稱 |
| `InputCount` | int | 輸入數據數量 |
| `OutputCount` | int? | 輸出數據數量 (After 階段) |
| `Phase` | StagePhase | 執行階段 (Before, After) |
| `Duration` | TimeSpan? | 執行時間 (After 階段) |
| `Error` | string? | 錯誤訊息 |
| `Timestamp` | DateTimeOffset | 事件時間戳 |

#### Pipeline 階段

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│  Transform  │ ──▶ │   Filter    │ ──▶ │    Send     │
│   Stage     │     │   Stage     │     │   Stage     │
└─────────────┘     └─────────────┘     └─────────────┘
      │                   │                   │
      ▼                   ▼                   ▼
   Before              Before              Before
   After               After               After
```

### 效能監控範例

```csharp
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Events;

public class PipelineMonitor
{
    private readonly ILogger _logger;
    private readonly TelemetryPipeline _pipeline;
    private int _slowCount = 0;

    public PipelineMonitor(TelemetryPipeline pipeline, ILogger logger)
    {
        _pipeline = pipeline;
        _logger = logger;

        // 監控慢速處理
        _pipeline.StageExecuting += OnStageExecuting;
    }

    private void OnStageExecuting(object? sender, TelemetryPipelineStageEvent e)
    {
        if (e.Phase != StagePhase.After) return;

        // 警告: Transform 超過 10ms
        if (e.Stage == PipelineStage.Transform && e.Duration > TimeSpan.FromMilliseconds(10))
        {
            _logger.LogWarning("Transform 執行緩慢: {Duration}ms", e.Duration?.TotalMilliseconds);
        }

        // 警告: 發送超過 1000ms
        if (e.Stage == PipelineStage.Send && e.Duration > TimeSpan.FromSeconds(1))
        {
            _slowCount++;
            _logger.LogWarning("Cloud 發送緩慢: {Duration}ms (累計 {Count} 次)",
                e.Duration?.TotalMilliseconds, _slowCount);
        }
    }

    public void PrintReport()
    {
        var stats = _pipeline.GetStatistics();

        var successRate = stats.TotalProcessed > 0
            ? (double)stats.SuccessfullySent / stats.TotalProcessed * 100
            : 0;

        _logger.LogInformation("""
            ══════════════════════════════════════
            Pipeline 效能報告 - {DeviceId}
            ══════════════════════════════════════
            處理統計:
              總處理: {TotalProcessed}
              成功: {SuccessfullySent} ({SuccessRate:F1}%)
              失敗: {FailedToSend}
              濾除: {FilteredOut}

            平均耗時:
              Transform: {TransformMs:F2}ms
              Filter: {FilterMs:F2}ms
              Send: {SendMs:F2}ms
              Total: {TotalMs:F2}ms
            ══════════════════════════════════════
            """,
            stats.DeviceId,
            stats.TotalProcessed,
            stats.SuccessfullySent,
            successRate,
            stats.FailedToSend,
            stats.FilteredOut,
            stats.AverageTransformDuration.TotalMilliseconds,
            stats.AverageFilterDuration.TotalMilliseconds,
            stats.AverageSendDuration.TotalMilliseconds,
            stats.AverageTotalDuration.TotalMilliseconds);
    }
}
```

---

## 完整範例

### 範例 1: 程式化方式 - 多感測器不同處理

```csharp
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Core.Transforms;
using Weda.SubNode.Core.Dsp;
using Weda.SubNode.Host;

var builder = WedaApplication.CreateBuilder(args);

var config = new TcpModbusDeviceConfiguration
{
    DeviceName = "MultiSensorDevice",
    Host = "192.168.1.100",
    Port = 502,
    SlaveId = 1
};

// 感測器 1: 溫度 - 需要校正 + 單位轉換
var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 0,
    DataType = ModbusDataType.UInt16
};
tempSensor.Config
    .AddTransform(new CalibrationTransform(0.1, -50.0))
    .AddTransform(new UnitConversionTransform("*", "celsius", "fahrenheit"))
    .AddDspFilter(new MovingAverageFilter(3));

// 感測器 2: 壓力 - 需要單位轉換 + 卡爾曼濾波
var pressureSensor = new ModbusSensorReporturation
{
    Name = "pressure",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 1,
    DataType = ModbusDataType.Float
};
pressureSensor.Config
    .AddTransform(new UnitConversionTransform("*", "pascal", "bar"))
    .AddDspFilter(new KalmanFilter(0.01, 0.5));

// 感測器 3: 振動 - 只需要降噪
var vibrationSensor = new ModbusSensorReporturation
{
    Name = "vibration",
    Dtmi = "dtmi:advantech:EdgeSync:AI;1",
    SensorGroup = SensorGroup.AI,
    RegisterAddress = 2,
    DataType = ModbusDataType.UInt16
};
vibrationSensor.Config
    .AddDspFilter(new MovingAverageFilter(10))  // 更大的視窗以獲得更平滑的結果
    .AddDspFilter(new KalmanFilter(0.001, 1.0));

config.AddSensors(tempSensor, pressureSensor, vibrationSensor);

builder.AddDevice(context => new TcpModbusDevice(context, config.ToDeviceConfiguration()));

var app = builder.Build();
await app.RunAsync();
```

### 範例 2: 配置檔方式 - appsettings.json

```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "Enabled": true,
      "DeviceName": "WISE-4012",
      "SubNodeType": "adamEthernet",
      "DtdlPath": "assets/dtdl/dtmi/advantech/edgesync/wise-4012.json",
      "DeviceCapabilities": {
        "Manufacturer": "Advantech",
        "Model": "WISE-4012"
      },
      "Communication": {
        "Host": "192.168.1.100",
        "Port": 502,
        "SlaveId": 1
      },
      "Sensors": [
        {
          "Name": "temperature",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 0,
            "DataType": "UInt16"
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "TransformPipeline": [
              {
                "Type": "Calibration",
                "Enabled": true,
                "Order": 0,
                "Parameters": {
                  "Scale": 0.1,
                  "Offset": -50.0
                }
              },
              {
                "Type": "UnitConversion",
                "Enabled": true,
                "Order": 1,
                "Parameters": {
                  "FromUnit": "celsius",
                  "ToUnit": "fahrenheit"
                }
              }
            ],
            "DspPipeline": [
              {
                "Type": "movingAverage",
                "Enabled": true,
                "Order": 0,
                "Parameters": {
                  "WindowSize": 3
                }
              }
            ]
          }
        },
        {
          "Name": "pressure",
          "Dtmi": "dtmi:advantech:EdgeSync:AI;1",
          "SensorGroup": "AI",
          "Parameters": {
            "RegisterType": "HoldingRegister",
            "RegisterAddress": 1,
            "DataType": "Float"
          },
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "TransformPipeline": [
              {
                "Type": "UnitConversion",
                "Enabled": true,
                "Order": 0,
                "Parameters": {
                  "FromUnit": "pascal",
                  "ToUnit": "bar"
                }
              }
            ],
            "DspPipeline": [
              {
                "Type": "kalman",
                "Enabled": true,
                "Order": 0,
                "Parameters": {
                  "ProcessNoise": 0.01,
                  "MeasurementNoise": 0.5
                }
              }
            ]
          }
        }
      ]
    }
  }
}
```

對應的 `Program.cs`:

```csharp
using Weda.SubNode.Host;

var app = WedaApplication.CreateDefaultBuilder(args)
    .Build();

await app.RunAsync();
```

---

## 最佳實踐

### 1. 選擇適合的方式

| 情境 | 建議方式 |
|------|----------|
| 每個感測器需要不同的處理邏輯 | 程式化方式 |
| 多個感測器使用相同配置 | 配置檔方式 |
| 需要動態調整參數 | 配置檔方式 |
| 需要型別安全和 IntelliSense | 程式化方式 |
| 複雜的自訂轉換邏輯 | 程式化方式（自訂 Transform/Filter） |

### 2. Transform 和 DSP 的執行順序

- ✅ **正確**: Transform → DSP
  ```csharp
  sensor.Config
      .AddTransform(new CalibrationTransform(0.1, 0))  // 先校正
      .AddDspFilter(new MovingAverageFilter(5));       // 再平滑
  ```

- ❌ **錯誤**: DSP → Transform
  - 先濾波再校正可能導致校正係數不準確

### 3. Order 參數的重要性

在配置檔方式中，`Order` 決定執行順序:

```json
{
  "TransformPipeline": [
    { "Type": "Calibration", "Order": 0 },      // 第一個執行
    { "Type": "UnitConversion", "Order": 1 }    // 第二個執行
  ]
}
```

### 4. 效能考量

- ✅ 只添加必要的 Transform 和 Filter
- ✅ MovingAverage 的 WindowSize 不要太大（建議 ≤ 10）
- ✅ Kalman Filter 適合高頻採樣，低頻採樣用 MovingAverage 即可

### 5. 混合使用兩種方式

可以同時使用程式化和配置檔方式:

```csharp
// 程式化方式: 添加自訂 Transform
var sensor = new ModbusSensorReporturation
{
    Name = "custom_sensor",
    // ...
};
sensor.Config.AddTransform(new MyCustomTransform());

// 配置檔方式: appsettings.json 會自動加載 TransformPipeline 和 DspPipeline
// Runtime transforms (程式化) 優先於 Config transforms (配置檔)
```

**執行順序**:
1. Runtime Transforms (程式化添加)
2. Config Transforms (配置檔)
3. Runtime DSP Filters (程式化添加)
4. Config DSP Filters (配置檔)

---

## 相關文檔

- [appsettings.json 完整配置指南](../03_advanced/appsettings_configuration.md)
- [DSP 濾波器使用案例](03_dsp_filters.md)
- [連接真實裝置](../01_quick_start/05_connect_real_device.md)

---

**版本**: 1.1.0
**最後更新**: 2025-11-27
**維護者**: Rain Hu

### 更新記錄

- **1.1.0** (2025-11-27): 新增 Real-time 參數調整和效能監控章節
- **1.0.0** (2025-11-17): 初始版本
