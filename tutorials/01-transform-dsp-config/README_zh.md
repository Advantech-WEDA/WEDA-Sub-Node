---
title: "Transform & DSP Filter - 配置方式"
description: "學習如何使用 appsettings.json 配置來應用 Transform 和 DSP Filter"
version: "0.0.1"
author: "Rain Hu"
date: "2025-11-18"
lang: "zh"
---

# Transform & DSP Filter 範例 - 配置方式

本範例展示如何使用 `wedabuilder` 模板透過 `appsettings.json` 配置來應用 **Transform** (UnitConversion) 和 **DSP Filter** (MovingAverage)。

## 本範例展示

- **基於配置的設定**: 所有 transforms 和 filters 都在 `appsettings.json` 中定義
- **零程式碼變更**: 不需要程式化配置
- **CalibrationTransform**: 比例和偏移校準
- **UnitConversionTransform**: 將攝氏轉換為華氏
- **MovingAverageFilter**: 使用視窗大小 5 平滑感測器資料

## Pipeline 配置

範例對溫度感測器應用以下 pipeline：

```
原始資料 → Calibration → UnitConversion (°C→°F) → MovingAverage(5) → 輸出
```

### Transform Pipeline (首先執行)
1. **CalibrationTransform** (array index=0): `scale=1.0, offset=0.0`
2. **UnitConversionTransform** (array index=1): `celsius` → `fahrenheit`

### DSP Pipeline (在 transforms 之後執行)
1. **MovingAverageFilter** (array index=0): 視窗大小 = 5

## 運作方式

所有配置都在 `appsettings.json` 中：

```json
{
  "Sensors": [
    {
      "Name": "temperature_sensor",
      "Config": {
        "TransformPipeline": [
          {
            "Type": "Calibration",
            "Enabled": true,
            "Parameters": {
              "Scale": 1.0,
              "Offset": 0.0
            }
          },
          {
            "Type": "UnitConversion",
            "Enabled": true,
            "Parameters": {
              "TargetResourceId": "*",
              "FromUnit": "celsius",
              "ToUnit": "fahrenheit"
            }
          }
        ],
        "DspPipeline": [
          {
            "Type": "MovingAverage",
            "Enabled": true,
            "Parameters": {
              "WindowSize": 5
            }
          }
        ]
      }
    }
  ]
}
```

裝置程式碼 (`MyFirstDevice.cs`) **不需要 pipeline 配置** - 一切都從配置檔案自動載入！

## 先決條件

- .NET 10.0 SDK
- 已安裝 Weda SubNode SDK 模板

## 快速開始

### 1. 使用 Mock Simulator 執行

範例包含內建的 Modbus 模擬器，可生成溫度資料：

```bash
cd tutorials/01-transform-dsp-config
dotnet run
```

**預期輸出:**
```
[12:34:56 INF] Transform and DSP Filter loaded from appsettings.json for temperature_sensor
[12:34:56 INF] TransformPipeline: 2 transforms configured
[12:34:56 INF]   - [Index=0] Calibration (Enabled=True)
[12:34:56 INF]   - [Index=1] UnitConversion (Enabled=True)
[12:34:56 INF] DspPipeline: 1 filters configured
[12:34:56 INF]   - [Index=0] MovingAverage (Enabled=True)
[12:34:56 INF] Pipeline: Calibration → UnitConversion (°C→°F) → MovingAverage(5)
[12:34:57 INF] temperature_sensor: 77.23°F (after config-based pipeline)
```

## 配置方式的優勢

- **無程式碼變更**: 無需重新編譯即可更新 pipelines
- **易於部署**: 根據環境變更配置
- **雲端同步**: 可從 Weda.Core 更新
- **版本控制友好**: 單獨追蹤配置變更
- **非開發人員可修改**: 不需要程式設計知識

## 配置參考

### 可用的 Transform 類型

| Type | Parameters | 說明 |
|------|------------|------|
| `Calibration` | `Scale`, `Offset` | 線性校準: `output = input × Scale + Offset` |
| `UnitConversion` | `FromUnit`, `ToUnit`, `TargetResourceId` | 單位轉換 (溫度、壓力等) |

### 可用的 DSP Filter 類型

| Type | Parameters | 說明 |
|------|------------|------|
| `MovingAverage` | `WindowSize` | 簡單移動平均濾波器 |
| `Kalman` | `ProcessNoise`, `MeasurementNoise`, `EstimationError` | Kalman 濾波器用於最優估計 |

## 相關範例

- [Transform DSP Programmatic Example](../02-transform-dsp-programmatic/) - 使用程式碼的相同功能
- [文檔](../../docs/wiki/zh/03_advanced/appsettings_configuration.md) - 完整指南

## 了解更多

- [Transformation Pipeline 指南](../../docs/wiki/zh/03_advanced/appsettings_configuration.md)
- [appsettings.json 配置](../../docs/wiki/zh/03_advanced/appsettings_configuration.md)
