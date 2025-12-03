# 自訂 Transform 與 DSP Filter 開發指南

本指南說明如何為 SubNode 遙測管線建立自訂的 Transform 與 DSP Filter。透過自我註冊模式，您可以新增 Transform 和 Filter 而無需修改任何 Factory 程式碼。

## 架構概述

遙測處理管線由兩個階段組成：

```
原始資料 → [Transform 管線] → [DSP Filter 管線] → 處理後資料
```

- **Transform**：首先執行，用於資料校正、單位轉換等
- **DSP Filter**：在 Transform 之後執行，用於訊號處理如平滑、濾波等

## 建立自訂 Transform

### 步驟 1：實作介面

建立一個同時實作 `ITelemetryTransform` 和 `IConfigurableTransform<T>` 的類別：

```csharp
using ErrorOr;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace MyProject.Transforms;

/// <summary>
/// 自訂 Transform，依可設定的係數縮放數值。
/// </summary>
public class ScaleTransform : ITelemetryTransform, IConfigurableTransform<ScaleTransform>
{
    private double _factor;

    // === 靜態抽象實作（自我註冊） ===

    /// <summary>
    /// 組態檔中使用的類型名稱。
    /// 與 TransformConfig.Type 進行不區分大小寫的比對。
    /// </summary>
    public static string TypeName => "scale";

    /// <summary>
    /// 從組態建立實例時，由 TransformFactory 呼叫的工廠方法。
    /// </summary>
    public static ScaleTransform Create(Dictionary<string, object> parameters)
    {
        var factor = 1.0;
        if (parameters.TryGetValue("Factor", out var value))
            factor = Convert.ToDouble(value);

        return new ScaleTransform(factor);
    }

    // === 實例成員 ===

    public string Name => nameof(ScaleTransform);

    public bool Enabled { get; set; } = true;

    public ScaleTransform(double factor = 1.0)
    {
        _factor = factor;
    }

    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Factor", out var value))
        {
            var factor = Convert.ToDouble(value);
            if (factor == 0)
                return Error.Validation("ScaleTransform.Factor", "Factor 不可為 0");
        }
        return Result.Success;
    }

    public void UpdateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Factor", out var value))
            _factor = Convert.ToDouble(value);
    }

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return Task.FromResult(measures);

        var transformed = measures.Select(measure =>
        {
            if (measure.Value is not double and not int and not float)
                return measure;

            var value = Convert.ToDouble(measure.Value);
            return measure with { Value = value * _factor };
        }).ToList();

        return Task.FromResult(transformed);
    }
}
```

### 步驟 2：在組態中使用

實作完成後，Transform 會自動被發現。在 `appsettings.json` 中使用：

```json
{
  "Sensors": [
    {
      "Name": "temperature.sensor",
      "Config": {
        "TransformPipeline": [
          {
            "Type": "scale",
            "Enabled": true,
            "Parameters": {
              "Factor": 1.5
            }
          }
        ]
      }
    }
  ]
}
```

### 步驟 3：程式化使用

```csharp
sensor.Config.ConfigureTransforms(transforms =>
{
    transforms.Add(new ScaleTransform(factor: 1.5));
});
```

## 建立自訂 DSP Filter

### 步驟 1：實作介面

建立一個實作 `IDspFilter` 和 `IConfigurableDspFilter<T>` 的類別：

```csharp
using System.Runtime.CompilerServices;
using ErrorOr;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace MyProject.Dsp;

/// <summary>
/// 低通濾波器，移除超過閾值的數值。
/// </summary>
public class LowPassFilter : IDspFilter, IConfigurableDspFilter<LowPassFilter>
{
    private double _threshold;

    // === 靜態抽象實作（自我註冊） ===

    public static string TypeName => "lowpass";

    public static LowPassFilter Create(Dictionary<string, object> parameters)
    {
        var threshold = double.MaxValue;
        if (parameters.TryGetValue("Threshold", out var value))
            threshold = Convert.ToDouble(value);

        return new LowPassFilter(threshold);
    }

    // === 實例成員 ===

    public bool Enabled { get; set; } = true;

    public LowPassFilter(double threshold = double.MaxValue)
    {
        _threshold = threshold;
    }

    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
    {
        // 閾值不需要驗證
        return Result.Success;
    }

    public void UpdateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Threshold", out var value))
            _threshold = Convert.ToDouble(value);
    }

    public async IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var measure in input.WithCancellation(cancellationToken))
        {
            if (!Enabled)
            {
                yield return measure;
                continue;
            }

            if (measure.Value is not double and not int and not float)
            {
                yield return measure;
                continue;
            }

            var value = Convert.ToDouble(measure.Value);

            // 只讓低於閾值的數值通過
            if (value <= _threshold)
            {
                yield return measure;
            }
            // 超過閾值的數值會被過濾掉
        }
    }
}
```

### 步驟 2：在組態中使用

```json
{
  "Sensors": [
    {
      "Name": "temperature.sensor",
      "Config": {
        "DspPipeline": [
          {
            "Type": "lowpass",
            "Enabled": true,
            "Parameters": {
              "Threshold": 100.0
            }
          }
        ]
      }
    }
  ]
}
```

### 步驟 3：程式化使用

```csharp
sensor.Config.ConfigureDspFilters(filters =>
{
    filters.Add(new LowPassFilter(threshold: 100.0));
});
```

## 關鍵介面方法

### IConfigurableTransform / IConfigurableDspFilter

| 成員 | 說明 |
|------|------|
| `static string TypeName` | 組態中使用的類型名稱（不區分大小寫比對） |
| `static T Create(Dictionary<string, object> parameters)` | 從組態參數建立實例的工廠方法 |

### ITelemetryTransform

| 成員 | 說明 |
|------|------|
| `string Name` | 用於記錄/除錯的可讀名稱 |
| `bool Enabled` | 在執行時期啟用/停用 Transform |
| `ValidateParameters()` | 在套用參數前進行驗證 |
| `UpdateParameters()` | 在執行時期更新參數（熱重載） |
| `TransformAsync()` | 對遙測量測值套用轉換 |

### IDspFilter

| 成員 | 說明 |
|------|------|
| `bool Enabled` | 在執行時期啟用/停用濾波器 |
| `ValidateParameters()` | 在套用參數前進行驗證 |
| `UpdateParameters()` | 在執行時期更新參數（熱重載） |
| `ApplyAsync()` | 對非同步量測值串流套用濾波 |

## 註冊外部組件

如果您的自訂 Transform/Filter 在獨立的組件中，請在啟動時註冊：

```csharp
// 在 Program.cs 或啟動程式碼中
TransformFactory.RegisterAssemblies(typeof(MyCustomTransform).Assembly);
DspFilterFactory.RegisterAssemblies(typeof(MyCustomFilter).Assembly);
```

## 內建 Transform

| 類型名稱 | 類別 | 說明 |
|----------|------|------|
| `calibration` | `CalibrationTransform` | 線性（縮放/偏移）或曲線校正 |
| `unitconversion` | `UnitConversionTransform` | 溫度單位轉換（C/F/K） |

## 內建 DSP Filter

| 類型名稱 | 類別 | 說明 |
|----------|------|------|
| `kalman` | `KalmanFilter` | 用於降噪的一維卡爾曼濾波器 |
| `movingaverage` | `MovingAverageFilter` | O(1) 複雜度的移動平均 |
| `relu` | `ReluFilter` | ReLU 激活函數（將負值設為 0） |

## 除錯

在執行時期檢查已註冊的類型：

```csharp
// 列出所有已註冊的 Transform 類型
Console.WriteLine($"Transforms: {string.Join(", ", TransformFactory.RegisteredTypes)}");

// 列出所有已註冊的 Filter 類型
Console.WriteLine($"Filters: {string.Join(", ", DspFilterFactory.RegisteredTypes)}");
```

## 最佳實踐

1. **命名**：使用小寫、單字的類型名稱（例如：`scale`、`lowpass`）
2. **驗證**：務必實作 `ValidateParameters()` 以提早捕捉組態錯誤
3. **狀態管理**：將每個感測器的狀態存放在以 `ResourceId` 為鍵的字典中
4. **執行緒安全**：DSP Filter 處理非同步串流 - 確保狀態存取的執行緒安全
5. **預設值**：在 `Create()` 方法中總是提供合理的預設值
6. **文件**：加入 XML 文件說明每個參數的用途