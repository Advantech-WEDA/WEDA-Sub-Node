---
sidebar_position: 2
sidebar_label: 'Transformations'
hide_title: true
title: 'Transformations | SubNode SDK'
keywords: ['SubNode', 'Transform', 'Calibration', 'UnitConversion', 'Chunking']
description: '了解 SubNode 內建的 Transform 類型和使用方式。'
---

# Transformations

> 了解 SubNode 內建的 Transform 類型和使用方式。

## Overview

Transform 是 Data Pipeline 的第一個處理階段，負責對 Sensor 的原始值進行數值轉換。SubNode 內建三種 Transform：線性/曲線校正、單位轉換、大型資料分片。每種都可透過 JSON 或程式碼設定，且支援從雲端動態更新參數。

## What You'll Learn

閱讀本文後，你將能夠：

- 使用 Calibration Transform 進行線性和曲線校正
- 使用 Unit Conversion Transform 進行溫度單位轉換
- 使用 Chunking Transform 分片傳輸大型資料
- 理解 `IConfigurableTransform` 介面如何支援雲端參數更新

## Prerequisites

- 完成 [Pipeline 概述](./01-overview.md)

---

## 內建 Transform 總覽

| Type Name | 類別 | 用途 | 狀態性 |
|-----------|------|------|--------|
| `calibration` | `CalibrationTransform` | 線性校正或曲線校正 | 無狀態 |
| `unitconversion` | `UnitConversionTransform` | 溫度單位轉換 | 無狀態 |
| `chunking` | `ChunkingTransform` | 大型資料分片傳輸 | 無狀態 |

---

## Calibration Transform

### 線性校正

公式：`calibrated_value = (raw_value * Scale) + Offset`

**JSON：**

```json
{
  "TransformPipeline": [
    {
      "Type": "calibration",
      "Enabled": true,
      "Parameters": {
        "Scale": 0.1,
        "Offset": -40.0
      }
    }
  ]
}
```

**程式碼：**

```csharp
tempSensor.Config.Report.ConfigureTransforms(pipeline =>
{
    pipeline.Add(new CalibrationTransform(scale: 0.1, offset: -40.0));
});
```

| Parameter | 類型 | 預設值 | 說明 |
|-----------|------|--------|------|
| `Scale` | double | 1.0 | 乘法因子（不可為 0）|
| `Offset` | double | 0.0 | 加法偏移 |

### 曲線校正

對於非線性感測器，可提供校正曲線點。框架會在相鄰點之間進行線性內插：

**JSON：**

```json
{
  "TransformPipeline": [
    {
      "Type": "calibration",
      "Enabled": true,
      "Parameters": {
        "CalibrationCurve": [
          { "RawValue": 0,    "CalibratedValue": -20.0 },
          { "RawValue": 1000, "CalibratedValue": 0.0 },
          { "RawValue": 2000, "CalibratedValue": 25.0 },
          { "RawValue": 3000, "CalibratedValue": 50.0 },
          { "RawValue": 4095, "CalibratedValue": 80.0 }
        ]
      }
    }
  ]
}
```

**程式碼：**

```csharp
pipeline.Add(new CalibrationTransform(new List<CalibrationPoint>
{
    new() { RawValue = 0,    CalibratedValue = -20.0 },
    new() { RawValue = 1000, CalibratedValue = 0.0 },
    new() { RawValue = 2000, CalibratedValue = 25.0 },
    new() { RawValue = 3000, CalibratedValue = 50.0 },
    new() { RawValue = 4095, CalibratedValue = 80.0 }
}));
```

> 當 `CalibrationCurve` 存在時，`Scale` 和 `Offset` 會被忽略。值超出曲線範圍時，使用邊界點的值。

---

## Unit Conversion Transform

目前支援溫度單位之間的轉換。

**JSON：**

```json
{
  "TransformPipeline": [
    {
      "Type": "unitconversion",
      "Enabled": true,
      "Parameters": {
        "FromUnit": "celsius",
        "ToUnit": "fahrenheit"
      }
    }
  ]
}
```

**程式碼：**

```csharp
pipeline.Add(new UnitConversionTransform("celsius", "fahrenheit"));
```

| Parameter | 類型 | 說明 |
|-----------|------|------|
| `FromUnit` | string | 來源單位（required）|
| `ToUnit` | string | 目標單位（required）|

### 支援的單位轉換

| From | To | 公式 |
|------|----|------|
| Celsius (C) | Fahrenheit (F) | `F = C * 9/5 + 32` |
| Fahrenheit (F) | Celsius (C) | `C = (F - 32) * 5/9` |
| Celsius (C) | Kelvin (K) | `K = C + 273.15` |
| Kelvin (K) | Celsius (C) | `C = K - 273.15` |
| Fahrenheit (F) | Kelvin (K) | `K = (F - 32) * 5/9 + 273.15` |
| Kelvin (K) | Fahrenheit (F) | `F = (K - 273.15) * 9/5 + 32` |

> 單位名稱不區分大小寫。支援縮寫（`C`、`F`、`K`）和全名（`celsius`、`fahrenheit`、`kelvin`）。

---

## Chunking Transform

將大型資料（如 Base64 編碼的影像）分片傳輸。每個分片包含 transfer metadata，接收端可用來重組。

**JSON：**

```json
{
  "TransformPipeline": [
    {
      "Type": "chunking",
      "Enabled": true,
      "Parameters": {
        "chunkSize": 131072
      }
    }
  ]
}
```

**程式碼：**

```csharp
pipeline.Add(new ChunkingTransform { ChunkSize = 128 * 1024 });
```

| Parameter | 類型 | 預設值 | 範圍 | 說明 |
|-----------|------|--------|------|------|
| `chunkSize` | int | 256KB | 1KB - 750KB | 每個分片的大小（bytes）|

### 分片 Metadata

每個分片的 `TelemetryMeasure.Metadata` 會包含：

| Key | 類型 | 說明 |
|-----|------|------|
| `transferId` | string | 本次傳輸的唯一 ID（GUID）|
| `chunkIndex` | int | 分片索引（從 0 開始）|
| `totalChunks` | int | 總分片數 |
| `crc32Checksum` | uint | 原始資料的 CRC32 校驗碼 |

> Chunking 只處理 Base64 字串且超過 `chunkSize` 的資料。JSON 字串和小型資料會直接通過。

---

## 串接多個 Transform

Transform 按陣列順序依序執行。常見的串接模式：

```json
{
  "TransformPipeline": [
    { "Type": "calibration", "Parameters": { "Scale": 0.1, "Offset": -40 } },
    { "Type": "unitconversion", "Parameters": { "FromUnit": "celsius", "ToUnit": "fahrenheit" } }
  ]
}
```

執行流程：`Raw(4000)` -> Calibration(`4000 * 0.1 - 40 = 360`) -> UnitConversion(`360°C -> 680°F`)

> 順序很重要。校正應在單位轉換之前。

---

## 雲端參數更新

所有內建 Transform 都實作 `IConfigurableTransform` 介面，支援從雲端動態更新參數而不需重啟：

```csharp
public interface IConfigurableTransform<TSelf>
{
    static abstract string TypeName { get; }
    static abstract TSelf Create(Dictionary<string, object> parameters);
    ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters);
    void UpdateParameters(Dictionary<string, object> parameters);
}
```

當雲端下發 Config Update 時，框架會：

1. 呼叫 `ValidateParameters` 驗證新參數
2. 驗證通過後呼叫 `UpdateParameters` 套用新參數
3. 驗證失敗則拒絕更新，保持原參數

---

## Summary

- **Calibration**：線性校正（`Scale` + `Offset`）或曲線校正（`CalibrationCurve` + 線性內插）
- **Unit Conversion**：溫度單位轉換（Celsius / Fahrenheit / Kelvin）
- **Chunking**：大型資料分片，每片包含 `transferId`、`chunkIndex`、`totalChunks`、`crc32Checksum`
- 所有 Transform 支援雲端動態參數更新（`IConfigurableTransform`）

## See Also

- [Pipeline 概述](./01-overview.md) - Pipeline 架構和執行順序
- [DSP Filters](./03-dsp-filters.md) - DSP Filter 詳細說明
- [Calibration Transform 詳解](./02-01-linear-transformation.md) - 校正的進階用法

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-31 | Rain Hu | Doc created. |
