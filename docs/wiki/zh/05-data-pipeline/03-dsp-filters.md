---
sidebar_position: 3
sidebar_label: 'DSP Filters'
hide_title: true
title: 'DSP Filters | SubNode SDK'
keywords: ['SubNode', 'DSP', 'Filter', 'MovingAverage', 'Kalman', 'ReLU']
description: '了解 SubNode 內建的 DSP Filter 類型和使用方式。'
---

# DSP Filters

> 了解 SubNode 內建的 DSP Filter 類型和使用方式。

## Overview

DSP (Digital Signal Processing) Filter 是 Data Pipeline 的第二個處理階段，在 Transform 之後執行。與 Transform 不同，DSP Filter 通常是**有狀態的** -- 它們需要記住歷史資料才能計算結果（如移動平均需要前幾次的值）。SubNode 內建三種 DSP Filter，每種都為每個 Sensor 獨立維護狀態。

## What You'll Learn

閱讀本文後，你將能夠：

- 使用 Moving Average Filter 平滑雜訊
- 使用 Kalman Filter 進行預測和降噪
- 使用 ReLU Filter 過濾負值
- 理解 DSP Filter 的有狀態特性和 warm-up 行為

## Prerequisites

- 完成 [Pipeline 概述](./01-overview.md)

---

## 內建 DSP Filter 總覽

| Type Name | 類別 | 用途 | 狀態性 |
|-----------|------|------|--------|
| `movingaverage` | `MovingAverageFilter` | 移動平均（平滑雜訊）| 有狀態（circular buffer）|
| `kalman` | `KalmanFilter` | Kalman 濾波（預測 + 降噪）| 有狀態（estimate + covariance）|
| `relu` | `ReluFilter` | ReLU 濾波（負值歸零）| 無狀態 |

---

## Moving Average Filter

使用 circular buffer 實現 O(1) 時間複雜度的移動平均。對每個 Sensor 獨立維護 buffer。

**JSON：**

```json
{
  "DspPipeline": [
    {
      "Type": "movingAverage",
      "Enabled": true,
      "Parameters": {
        "Window": 5
      }
    }
  ]
}
```

**程式碼：**

```csharp
sensor.Report.ConfigureDspFilters(pipeline =>
{
    pipeline.Add(new MovingAverageFilter(window: 5));
});
```

| Parameter | 類型 | 預設值 | 說明 |
|-----------|------|--------|------|
| `Window` | int | 5 | 移動平均的窗口大小（必須 > 0）|

### 行為說明

- **Warm-up**：buffer 未滿時，使用已有資料計算平均值（不會等到滿了才輸出）
- **Window 變更**：當透過雲端更新 `Window` 參數時，buffer 會重置，需要重新 warm-up
- **Per-Sensor 狀態**：每個 Sensor（by `ResourceId`）獨立維護自己的 buffer

### 範例

Window = 3，輸入序列：`10, 20, 30, 40, 50`

| 輸入 | Buffer 狀態 | 輸出（平均） |
|------|-------------|-------------|
| 10 | [10] | 10.0 |
| 20 | [10, 20] | 15.0 |
| 30 | [10, 20, 30] | 20.0 |
| 40 | [20, 30, 40] | 30.0 |
| 50 | [30, 40, 50] | 40.0 |

---

## Kalman Filter

簡易 1D Kalman Filter，透過預測和量測更新來降噪。適合感測器讀數有噪聲但變化趨勢穩定的場景。

**JSON：**

```json
{
  "DspPipeline": [
    {
      "Type": "kalman",
      "Enabled": true,
      "Parameters": {
        "ProcessNoise": 0.01,
        "MeasurementNoise": 0.1
      }
    }
  ]
}
```

**程式碼：**

```csharp
sensor.Report.ConfigureDspFilters(pipeline =>
{
    pipeline.Add(new KalmanFilter(processNoise: 0.01, measurementNoise: 0.1));
});
```

| Parameter | 類型 | 預設值 | 範圍 | 說明 |
|-----------|------|--------|------|------|
| `ProcessNoise` | double | 0.01 | >= 0 | 過程噪聲協方差（值越大，越信任新量測）|
| `MeasurementNoise` | double | 0.1 | > 0 | 量測噪聲協方差（值越大，越信任預測）|

### 參數調校指南

| 場景 | ProcessNoise | MeasurementNoise | 效果 |
|------|-------------|------------------|------|
| 感測器雜訊大 | 低（0.001） | 高（1.0）| 大幅平滑，反應慢 |
| 感測器精準 | 高（0.1）| 低（0.01）| 幾乎不平滑，反應快 |
| 一般用途 | 0.01 | 0.1 | 適度平滑 |

### 行為說明

- **初始化**：第一個量測值直接作為初始 estimate
- **參數更新**：更新 `ProcessNoise` / `MeasurementNoise` 時，狀態保留（不需重新 warm-up）
- **Per-Sensor 狀態**：每個 Sensor 獨立維護 estimate 和 error covariance

---

## ReLU Filter

ReLU (Rectified Linear Unit) 濾波器，將負值設為 0。適用於物理上不可能出現負值的感測器（如功率、亮度）。

**JSON：**

```json
{
  "DspPipeline": [
    {
      "Type": "relu",
      "Enabled": true,
      "Parameters": {}
    }
  ]
}
```

**程式碼：**

```csharp
sensor.Report.ConfigureDspFilters(pipeline =>
{
    pipeline.Add(new ReluFilter());
});
```

公式：`output = max(0, input)`

| 輸入 | 輸出 |
|------|------|
| 25.3 | 25.3 |
| -1.5 | 0.0 |
| 0.0 | 0.0 |
| 100.0 | 100.0 |

> ReLU Filter 無參數，無狀態。

---

## 串接多個 DSP Filter

DSP Filter 按陣列順序依序執行：

```json
{
  "DspPipeline": [
    { "Type": "kalman", "Parameters": { "ProcessNoise": 0.01, "MeasurementNoise": 0.1 } },
    { "Type": "relu" }
  ]
}
```

執行流程：`Transformed Value` -> Kalman（降噪）-> ReLU（負值歸零）-> `Final Value`

---

## Transform 與 DSP Filter 的串接

完整的 Pipeline 範例（Transform + DSP）：

```json
{
  "Report": {
    "Enabled": true,
    "Interval": 3000,
    "TransformPipeline": [
      { "Type": "calibration", "Parameters": { "Scale": 0.1, "Offset": -40 } }
    ],
    "DspPipeline": [
      { "Type": "movingAverage", "Parameters": { "Window": 5 } },
      { "Type": "relu" }
    ]
  }
}
```

執行流程：`Raw` -> Calibration -> Moving Average -> ReLU -> `Final`

---

## Summary

- **Moving Average**：O(1) circular buffer 實現，`Window` 參數控制平滑程度，變更時需重新 warm-up
- **Kalman**：1D Kalman Filter，`ProcessNoise` 和 `MeasurementNoise` 控制信任程度，參數更新時狀態保留
- **ReLU**：負值歸零，無參數無狀態
- 所有 DSP Filter 為每個 Sensor 獨立維護狀態，支援雲端動態參數更新

## See Also

- [Pipeline 概述](./01-overview.md) - Pipeline 架構和執行順序
- [Transformations](./02-transformations.md) - Transform 詳細說明
- [Kalman Filter 詳解](./03-01-kalman-filter.md) - Kalman Filter 數學原理

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |
