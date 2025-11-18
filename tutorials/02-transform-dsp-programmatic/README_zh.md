---
title: "Transform & DSP Filter - 程式化方式"
description: "學習如何使用程式碼配置來應用 Transform 和 DSP Filter"
version: "0.0.1"
author: "Rain Hu"
date: "2025-11-18"
lang: "zh"
---

# Transform & DSP Filter 範例 - 程式化方式

本範例展示如何使用 `wedabuilder` 模板透過**程式碼**來應用 **Transform** (UnitConversion) 和 **DSP Filter** (MovingAverage)。

## 本範例展示

- **程式化設定**: 在程式碼中定義 transforms 和 filters
- **Builder Pattern API**: 使用 `ConfigureTransforms()` 和 `ConfigureDspFilters()`
- **CalibrationTransform**: 比例和偏移校準
- **UnitConversionTransform**: 將攝氏轉換為華氏
- **MovingAverageFilter**: 使用視窗大小 5 平滑感測器資料
- **動態配置**: 運行時動態調整 pipeline

## Pipeline 配置

範例對溫度感測器應用以下 pipeline：

```
原始資料 → Calibration → UnitConversion (°C→°F) → MovingAverage(5) → 輸出
```

### Transform Pipeline (首先執行)
1. **CalibrationTransform**: `scale=1.0, offset=0.0`
2. **UnitConversionTransform**: `celsius` → `fahrenheit`

### DSP Pipeline (在 transforms 之後執行)
1. **MovingAverageFilter**: 視窗大小 = 5

## 運作方式

所有配置都在 `MyFirstDevice.cs` 程式碼中：

```csharp
private void ConfigureTransformsAndFilters()
{
    var tempSensor = Configuration.Sensors.FirstOrDefault(s => s.Name == "temperature.sensor");
    if (tempSensor != null)
    {
        // Configure Transform Pipeline (executed first)
        tempSensor.Config.ConfigureTransforms(transforms =>
        {
            transforms.Add(new CalibrationTransform(scale: 1.0, offset: 0.0));
            transforms.Add(new UnitConversionTransform(
                targetResourceId: "*",
                fromUnit: "celsius",
                toUnit: "fahrenheit"));
        });

        // Configure DSP Filter Pipeline (executed after transforms)
        tempSensor.Config.ConfigureDspFilters(filters =>
        {
            filters.Add(new MovingAverageFilter(5));
        });
    }
}
```

## 先決條件

- .NET 9.0 SDK
- 已安裝 Weda SubNode SDK 模板

## 快速開始

### 1. 使用 Mock Simulator 執行

範例包含內建的 Modbus 模擬器，可生成溫度資料：

```bash
cd tutorials/02-transform-dsp-programmatic
dotnet run
```

**預期輸出:**
```
[12:34:56 INF] Configuring Transform and DSP Filter for temperature.sensor
[12:34:56 INF] Applied: Calibration → UnitConversion (°C→°F) → MovingAverage(5)
[12:34:57 INF] temperature.sensor: 77.23°F (after programmatic pipeline)
```

## 程式化方式的優勢

- **類型安全**: 編譯時期檢查
- **動態配置**: 可根據運行時條件調整
- **程式碼自動完成**: IDE 支援
- **版本控制**: 與程式碼一起追蹤變更
- **重構友好**: IDE 重構工具支援

## API 參考

### Transform Pipeline Builder

```csharp
tempSensor.Config.ConfigureTransforms(transforms =>
{
    transforms.Add(new CalibrationTransform(...));
    transforms.Add(new UnitConversionTransform(...));
    transforms.Clear();  // Clear all transforms
});
```

### DSP Filter Pipeline Builder

```csharp
tempSensor.Config.ConfigureDspFilters(filters =>
{
    filters.Add(new MovingAverageFilter(windowSize));
    filters.Add(new KalmanFilter(...));
    filters.Clear();  // Clear all filters
});
```

## 相關範例

- [Transform DSP Config Example](../01-transform-dsp-config/) - 使用配置檔案的相同功能
- [文檔](../../docs/wiki/zh/03_advanced/appsettings_configuration.md) - 完整指南

## 了解更多

- [Transformation Pipeline 指南](../../docs/wiki/zh/03_advanced/appsettings_configuration.md)
- [Builder Pattern API](../../docs/wiki/zh/02_core_concepts/wedaapplication_builder.md)
