---
title: Telemetry 轉換管道
category: transform
order: 1
parent: null
related:
  - path: architecture_overview.md
    title: 架構概述
  - path: modbus_scanner.md
    title: Modbus 掃描器
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: TelemetryTransformContext 使用範例 - 在資料轉換管道中使用上下文資訊
tags: [Telemetry, Transform, Context, Data Processing]
---

# TelemetryTransformContext 使用範例

## 為什麼需要 Context？

`TelemetryTransformContext` 提供了 Transform 執行時所需的**上下文資訊**,讓 Transform 可以根據不同的情境做出不同的處理決策。

### Context 的三個核心欄位

1. **DeviceId** - 裝置識別碼
2. **Timestamp** - 轉換時間戳記
3. **Metadata** - 額外的上下文資料(動態鍵值對)

---

## 實際使用場景

### 場景 1: 基於裝置類型的動態校準

不同型號的裝置可能需要不同的校準參數。

```csharp
/// <summary>
/// Device-specific calibration transform
/// Uses DeviceId from context to determine calibration parameters
/// </summary>
public class DeviceSpecificCalibrationTransform : ITelemetryTransform
{
    private readonly Dictionary<string, CalibrationConfig> _deviceCalibrations;

    public string Name => "DeviceSpecificCalibration";

    public DeviceSpecificCalibrationTransform()
    {
        // 預定義不同裝置的校準參數
        _deviceCalibrations = new()
        {
            ["ADAM-6052-001"] = new CalibrationConfig { ScaleFactor = 0.1, Offset = -5.0 },
            ["ADAM-6052-002"] = new CalibrationConfig { ScaleFactor = 0.12, Offset = -3.5 },
            ["ADAM-6017-001"] = new CalibrationConfig { ScaleFactor = 0.08, Offset = -2.0 },
        };
    }

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // 根據 DeviceId 選擇對應的校準配置
        if (!_deviceCalibrations.TryGetValue(context.DeviceId, out var config))
        {
            // 沒有特定配置,使用預設校準
            config = new CalibrationConfig { ScaleFactor = 1.0, Offset = 0.0 };
        }

        var transformed = measures.Select(m =>
        {
            if (m.ValueObject is not double and not int and not float)
                return m;

            var rawValue = Convert.ToDouble(m.ValueObject);
            var calibrated = config.Apply(rawValue);

            return m with { ValueObject = calibrated };
        }).ToList();

        return Task.FromResult(transformed);
    }
}

// 使用範例
var pipeline = TelemetryTransformPipeline.Create()
    .Add(new DeviceSpecificCalibrationTransform());

var context = new TelemetryTransformContext
{
    DeviceId = "ADAM-6052-001" // 根據不同裝置套用不同校準
};

var result = await pipeline.ExecuteAsync(measures, context);
```

---

### 場景 2: 時間相關的資料處理

某些轉換需要知道當前時間,例如:白天/夜間不同的處理邏輯、時區轉換等。

```csharp
/// <summary>
/// Time-aware temperature adjustment
/// Applies different corrections based on time of day
/// </summary>
public class TimeAwareTemperatureTransform : ITelemetryTransform
{
    public string Name => "TimeAwareTemperature";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // 使用 Context 的 Timestamp 判斷是白天還是夜晚
        var localTime = context.Timestamp.ToLocalTime();
        var isDaytime = localTime.Hour >= 6 && localTime.Hour < 18;

        // 白天和夜晚套用不同的溫度修正
        var adjustment = isDaytime ? -2.0 : -1.0; // 白天感測器較熱,需要較大修正

        var transformed = measures.Select(m =>
        {
            if (!m.ResourceId.Contains("temperature"))
                return m;

            if (m.ValueObject is not double and not int and not float)
                return m;

            var temperature = Convert.ToDouble(m.ValueObject);
            var adjusted = temperature + adjustment;

            return m with { ValueObject = adjusted };
        }).ToList();

        return Task.FromResult(transformed);
    }
}

// 使用範例
var context = new TelemetryTransformContext
{
    DeviceId = "device-001",
    Timestamp = DateTimeOffset.Now // 根據當前時間決定如何處理
};

var result = await pipeline.ExecuteAsync(measures, context);
```

---

### 場景 3: 使用 Metadata 傳遞運行時資訊

Metadata 可以傳遞任意的運行時資訊,例如:環境溫度、裝置位置、操作模式等。

```csharp
/// <summary>
/// Environment-compensated pressure transform
/// Adjusts pressure reading based on ambient temperature
/// </summary>
public class PressureCompensationTransform : ITelemetryTransform
{
    public string Name => "PressureCompensation";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // 從 Metadata 取得環境溫度
        var ambientTemp = context.Metadata.TryGetValue("AmbientTemperature", out var temp)
            ? Convert.ToDouble(temp)
            : 25.0; // 預設 25°C

        // 從 Metadata 取得裝置海拔高度
        var altitude = context.Metadata.TryGetValue("Altitude", out var alt)
            ? Convert.ToDouble(alt)
            : 0.0; // 預設海平面

        var transformed = measures.Select(m =>
        {
            if (!m.ResourceId.Contains("pressure"))
                return m;

            if (m.ValueObject is not double and not int and not float)
                return m;

            var pressure = Convert.ToDouble(m.ValueObject);

            // 溫度補償: 每°C 影響 0.37%
            var tempCompensation = 1.0 + ((ambientTemp - 25.0) * 0.0037);

            // 高度補償: 每 100m 減少約 12 hPa
            var altitudeCompensation = altitude * 0.12;

            var compensated = (pressure * tempCompensation) - altitudeCompensation;

            return m with { ValueObject = compensated };
        }).ToList();

        return Task.FromResult(transformed);
    }
}

// 使用範例
var context = new TelemetryTransformContext
{
    DeviceId = "pressure-sensor-001",
    Timestamp = DateTimeOffset.UtcNow,
    Metadata = new Dictionary<string, object>
    {
        ["AmbientTemperature"] = 28.5,  // 環境溫度 28.5°C
        ["Altitude"] = 150.0,           // 海拔 150 公尺
        ["Location"] = "Taipei",        // 位置資訊
        ["OperatingMode"] = "Normal"    // 操作模式
    }
};

var result = await pipeline.ExecuteAsync(measures, context);
```

---

### 場景 4: 跨 Sensor 的資料關聯

使用 Metadata 在 Transform 之間傳遞計算結果。

```csharp
/// <summary>
/// Power calculation transform
/// Calculates power from voltage and current measurements
/// </summary>
public class PowerCalculationTransform : ITelemetryTransform
{
    public string Name => "PowerCalculation";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // 從 measures 中找出電壓和電流
        var voltage = measures
            .FirstOrDefault(m => m.ResourceId.Contains("voltage"))
            ?.ValueObject as double?;

        var current = measures
            .FirstOrDefault(m => m.ResourceId.Contains("current"))
            ?.ValueObject as double?;

        if (voltage.HasValue && current.HasValue)
        {
            // 計算功率: P = V × I
            var power = voltage.Value * current.Value;

            // 將計算結果存入 Metadata,供後續 Transform 使用
            context.Metadata["CalculatedPower"] = power;
            context.Metadata["PowerTimestamp"] = context.Timestamp;

            // 新增一個功率 measure
            var powerMeasure = new TelemetryMeasure
            {
                ResourceId = $"{context.DeviceId}-power",
                ValueObject = power,
                Timestamp = context.Timestamp
            };

            return Task.FromResult(measures.Append(powerMeasure).ToList());
        }

        return Task.FromResult(measures);
    }
}

/// <summary>
/// Energy accumulation transform
/// Uses calculated power from context to accumulate energy
/// </summary>
public class EnergyAccumulationTransform : ITelemetryTransform
{
    private double _accumulatedEnergy = 0.0;
    private DateTimeOffset _lastTimestamp = DateTimeOffset.MinValue;

    public string Name => "EnergyAccumulation";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // 從 Metadata 取得前一個 Transform 計算的功率
        if (context.Metadata.TryGetValue("CalculatedPower", out var powerObj))
        {
            var power = Convert.ToDouble(powerObj);
            var timeDiff = (context.Timestamp - _lastTimestamp).TotalHours;

            if (_lastTimestamp != DateTimeOffset.MinValue && timeDiff > 0)
            {
                // 累積能量: E = P × t (Wh)
                _accumulatedEnergy += power * timeDiff;

                // 新增能量累積 measure
                var energyMeasure = new TelemetryMeasure
                {
                    ResourceId = $"{context.DeviceId}-energy",
                    ValueObject = _accumulatedEnergy,
                    Timestamp = context.Timestamp
                };

                measures = measures.Append(energyMeasure).ToList();
            }

            _lastTimestamp = context.Timestamp;
        }

        return Task.FromResult(measures);
    }
}

// 使用範例
var pipeline = TelemetryTransformPipeline.Create()
    .Add(new PowerCalculationTransform())       // 計算功率並存入 Context
    .Add(new EnergyAccumulationTransform());    // 從 Context 讀取功率並累積能量

var measures = new List<TelemetryMeasure>
{
    new() { ResourceId = "voltage-001", ValueObject = 220.0 },
    new() { ResourceId = "current-001", ValueObject = 5.0 }
};

var context = new TelemetryTransformContext
{
    DeviceId = "power-meter-001",
    Timestamp = DateTimeOffset.UtcNow
};

var result = await pipeline.ExecuteAsync(measures, context);
// result 將包含: voltage, current, power (220 × 5 = 1100W), energy (累積值)
```

---

### 場景 5: 條件式轉換(基於 DeviceId 和 Metadata)

```csharp
/// <summary>
/// Conditional smoothing transform
/// Applies smoothing only for specific devices or conditions
/// </summary>
public class ConditionalSmoothingTransform : ITelemetryTransform
{
    private readonly Dictionary<string, Queue<double>> _history = new();
    private const int WindowSize = 5;

    public string Name => "ConditionalSmoothing";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // 條件 1: 只對特定裝置套用平滑
        var enabledDevices = new[] { "noisy-sensor-001", "noisy-sensor-002" };
        if (!enabledDevices.Contains(context.DeviceId))
        {
            return Task.FromResult(measures);
        }

        // 條件 2: 只在高雜訊模式下套用平滑
        var isHighNoiseMode = context.Metadata.TryGetValue("NoiseLevel", out var noise)
            && noise.ToString() == "High";

        if (!isHighNoiseMode)
        {
            return Task.FromResult(measures);
        }

        // 套用移動平均平滑
        var transformed = measures.Select(m =>
        {
            if (m.ValueObject is not double and not int and not float)
                return m;

            var value = Convert.ToDouble(m.ValueObject);
            var key = m.ResourceId;

            if (!_history.ContainsKey(key))
            {
                _history[key] = new Queue<double>();
            }

            var queue = _history[key];
            queue.Enqueue(value);

            if (queue.Count > WindowSize)
            {
                queue.Dequeue();
            }

            var smoothed = queue.Average();
            return m with { ValueObject = smoothed };
        }).ToList();

        return Task.FromResult(transformed);
    }
}

// 使用範例
var context = new TelemetryTransformContext
{
    DeviceId = "noisy-sensor-001",
    Timestamp = DateTimeOffset.UtcNow,
    Metadata = new Dictionary<string, object>
    {
        ["NoiseLevel"] = "High",    // 觸發條件式平滑
        ["Environment"] = "Factory"
    }
};

var result = await pipeline.ExecuteAsync(measures, context);
```

---

## 完整實際應用範例

### 情境:工廠環境監測系統

```csharp
public class FactoryMonitoringService
{
    private readonly TelemetryTransformPipeline _pipeline;
    private readonly IWeatherService _weatherService;
    private readonly IDeviceLocationService _locationService;

    public FactoryMonitoringService(
        IWeatherService weatherService,
        IDeviceLocationService locationService)
    {
        _weatherService = weatherService;
        _locationService = locationService;

        // 建立 Transform Pipeline
        _pipeline = TelemetryTransformPipeline.Create()
            .Add(new DeviceSpecificCalibrationTransform())
            .Add(new PressureCompensationTransform())
            .Add(new PowerCalculationTransform())
            .Add(new EnergyAccumulationTransform())
            .Add(new ConditionalSmoothingTransform());
    }

    public async Task<List<TelemetryMeasure>> ProcessTelemetryAsync(
        string deviceId,
        List<TelemetryMeasure> rawMeasures,
        CancellationToken cancellationToken = default)
    {
        // 取得裝置位置資訊
        var location = await _locationService.GetLocationAsync(deviceId);

        // 取得當前天氣資訊
        var weather = await _weatherService.GetWeatherAsync(location);

        // 建立豐富的 Context
        var context = new TelemetryTransformContext
        {
            DeviceId = deviceId,
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                // 環境資訊
                ["AmbientTemperature"] = weather.Temperature,
                ["Humidity"] = weather.Humidity,
                ["Pressure"] = weather.Pressure,

                // 位置資訊
                ["Altitude"] = location.Altitude,
                ["Location"] = location.Name,
                ["Latitude"] = location.Latitude,
                ["Longitude"] = location.Longitude,

                // 操作資訊
                ["OperatingMode"] = "Production",
                ["ShiftNumber"] = 1,
                ["NoiseLevel"] = "High",

                // 其他上下文
                ["ProcessedBy"] = "FactoryMonitoringService",
                ["Version"] = "1.0.0"
            }
        };

        // 執行 Transform Pipeline
        var processedMeasures = await _pipeline.ExecuteAsync(
            rawMeasures,
            context,
            cancellationToken);

        return processedMeasures;
    }
}
```

---

## 總結

### Context 的核心價值

1. **DeviceId**
   - 識別資料來源
   - 套用裝置特定的處理邏輯
   - 支援多租戶場景

2. **Timestamp**
   - 時間相關的處理決策
   - 資料時序分析
   - 時區處理

3. **Metadata(最靈活)**
   - 傳遞任意運行時資訊
   - Transform 之間共享計算結果
   - 條件式處理的依據
   - 環境上下文資訊

### 設計原則

- **不可變性**: Transform 不應修改 Context(僅讀取或添加 Metadata)
- **可選性**: Transform 應該能在缺少 Context 資訊時使用合理的預設值
- **擴展性**: Metadata 使用 `Dictionary<string, object>` 提供無限擴展能力
- **傳遞性**: Context 在整個 Pipeline 中傳遞,允許 Transform 之間協作

### 何時使用 Context

**應該使用**:
- 需要知道資料來源(DeviceId)
- 需要時間資訊(Timestamp)
- 需要環境或運行時資訊(Metadata)
- 需要在 Transform 之間共享資料

**不需要使用**:
- 簡單的數學運算(如線性校準)
- 無狀態的資料轉換
- 不依賴外部資訊的處理

Context 提供了強大的靈活性,讓 Transform 可以根據實際情境做出智慧決策!
