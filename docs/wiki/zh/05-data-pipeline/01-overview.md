---
sidebar_position: 1
sidebar_label: 'Pipeline 概述'
hide_title: true
title: 'Data Pipeline 概述 | SubNode SDK'
keywords: ['SubNode', 'Pipeline', 'Transform', 'DSP', 'Data Processing', 'Threshold']
description: '了解 SubNode 的資料處理 Pipeline 架構、執行順序和設定方式。'
---

# Data Pipeline 概述

> 了解 SubNode 的資料處理 Pipeline 架構、執行順序和設定方式。

## Overview

SubNode 的每個 Sensor 可以配置一條 Data Pipeline，將從裝置讀取的原始值依序經過 Transform、DSP Filter 和 Threshold 處理後，再傳送到雲端或本機儲存。Pipeline 的設計是可組合的 -- 每個階段都是 optional，且可以串接多個同類型的處理器。

## What You'll Learn

閱讀本文後，你將能夠：

- 理解 Data Pipeline 的三個處理階段和執行順序
- 區分 Transform 和 DSP Filter 的差異
- 透過 JSON 和程式碼兩種方式設定 Pipeline

## Prerequisites

- 完成[透過 JSON 設定](../04-configuration/02-configuration-via-json.md)

---

## Pipeline 架構

```text
┌───────────────────────────────────────────────────────────────────┐
│                      Sensor Data Pipeline                         │
├───────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────┐    ┌──────────────┐    ┌───────────┐                │
│  │   Raw    │    │  Transform   │    │    DSP    │                │
│  │  Value   │───>│  Pipeline    │───>│  Pipeline │───> Final      │
│  │          │    │              │    │           │     Value      │
│  │ (device) │    │ calibration  │    │ moving    │    (Cloud/     │
│  │          │    │ unitconv     │    │  average  │     Storage)   │
│  │          │    │ chunking     │    │ kalman    │                │
│  │          │    │              │    │ relu      │                │
│  └──────────┘    └──────────────┘    └───────────┘                │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

## 執行順序

Pipeline 的三個階段固定按以下順序執行：

| 順序 | 階段 | 類別 | 作用 |
|------|------|------|------|
| 1 | **TransformPipeline** | `ITelemetryTransform` | 數值轉換（校正、單位、分片）|
| 2 | **DspPipeline** | `IDspFilter` | 訊號處理（平滑、濾波、降噪）|

> **Note**: `ThresholdConfig` 已定義在 SDK 中（可設定 `LowerWarning`、`UpperCritical` 等），但目前尚未整合到 Pipeline 自動執行。未來版本將支援 Threshold 觸發警示。

在每個階段內，處理器按**陣列順序**（JSON）或**新增順序**（程式碼）依序執行。

---

## Transform vs DSP Filter

| 面向 | Transform | DSP Filter |
|------|-----------|------------|
| 介面 | `ITelemetryTransform` | `IDspFilter` |
| 作用 | 數值轉換、格式變換 | 訊號品質處理 |
| 是否有狀態 | 通常無狀態 | 通常有狀態（需要歷史資料）|
| 典型用途 | 校正、單位轉換、分片 | 移動平均、Kalman 濾波 |
| 執行順序 | 先於 DSP Filter | 後於 Transform |

---

## 內建 Transform

| Type | 類別 | 說明 |
|------|------|------|
| `calibration` | `CalibrationTransform` | 線性校正或曲線校正 |
| `unitconversion` | `UnitConversionTransform` | 單位轉換 |
| `chunking` | `ChunkingTransform` | 大型資料分片傳輸 |

## 內建 DSP Filter

| Type | 類別 | 說明 |
|------|------|------|
| `movingAverage` | `MovingAverageFilter` | 移動平均（平滑雜訊）|
| `kalman` | `KalmanFilter` | Kalman 濾波（預測 + 降噪）|
| `relu` | `ReluFilter` | ReLU 濾波（負值歸零）|

---

## JSON 設定方式

在 `devicecfg.json` 的 Sensor `Report` 中設定：

```json
{
  "Name": "temperature_sensor",
  "Report": {
    "Enabled": true,
    "Interval": 3000,
    "TransformPipeline": [
      {
        "Type": "calibration",
        "Enabled": true,
        "Parameters": {
          "Scale": 0.1,
          "Offset": -40.0
        }
      },
      {
        "Type": "unitconversion",
        "Enabled": true,
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
        "Parameters": {
          "WindowSize": 5
        }
      }
    ],
    "Thresholds": {
      "LowerWarning": 0,
      "LowerCritical": -10,
      "UpperWarning": 50,
      "UpperCritical": 60
    }
  }
}
```

每個處理器都有 `Enabled` 欄位，設為 `false` 可跳過而不需移除設定。

## Programmatic 設定方式

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform(scale: 0.1, offset: -40.0));
        pipeline.Add(new UnitConversionTransform
        {
            FromUnit = "celsius",
            ToUnit = "fahrenheit"
        });
    })
    .ConfigureDspFilters(pipeline =>
    {
        pipeline.Add(new MovingAverageFilter { WindowSize = 5 });
    });

tempSensor.Config.Report.Thresholds = new ThresholdConfig
{
    LowerWarning = 0,
    LowerCritical = -10,
    UpperWarning = 50,
    UpperCritical = 60
};
```

---

## Threshold（規劃中）

SDK 已定義 `ThresholdConfig` 資料模型，可在設定中預先配置閾值。目前尚未自動整合到 Pipeline 執行，未來版本將支援自動觸發警示。

| Level | 條件 |
|-------|------|
| `Normal` | 在所有閾值範圍內 |
| `LowerWarning` | value <= LowerWarning |
| `LowerCritical` | value <= LowerCritical |
| `UpperWarning` | value >= UpperWarning |
| `UpperCritical` | value >= UpperCritical |

---

## 監控 Pipeline

透過事件監控 Pipeline 的輸入和輸出：

```csharp
// Raw data from device (before pipeline)
device.EnableDataReceivedTracking = true;
device.DataReceived += (sender, e) =>
{
    Console.WriteLine($"Raw: {e.Data[0].Value}");
};

// Processed data (after pipeline)
device.EnableDataProcessedTracking = true;
device.DataProcessed += (sender, e) =>
{
    Console.WriteLine($"Processed: {e.Data[0].Value}");
};
```

> 事件預設為停用以提升效能。僅在開發和除錯時啟用。

---

## Summary

- Data Pipeline 有兩個固定順序的處理階段：Transform -> DSP Filter（Threshold 規劃中）
- Transform 負責數值轉換（無狀態），DSP Filter 負責訊號處理（有狀態）
- 內建 3 種 Transform（calibration、unitconversion、chunking）和 3 種 DSP Filter（movingAverage、kalman、relu）
- 透過 JSON 或程式碼設定，每個處理器可個別啟用/停用
- 使用 `DataReceived` 和 `DataProcessed` 事件監控 Pipeline 的輸入和輸出

## See Also

- [Transformations](./02-transformations.md) - Transform 詳細說明
- [DSP Filters](./03-dsp-filters.md) - DSP Filter 詳細說明
- [透過 JSON 設定](../04-configuration/02-configuration-via-json.md) - Report 設定

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |
