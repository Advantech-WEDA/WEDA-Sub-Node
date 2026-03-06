---
sidebar_position: 1
sidebar_label: 'Pipeline 概述'
hide_title: true
title: 'Data Pipeline 概述'
keywords: ['SubNode', 'Pipeline', 'Transform', 'DSP', 'Data Processing']
description: '了解 SubNode 資料處理 Pipeline 架構'
---

# Pipeline 概述

> 了解 SubNode 資料處理 Pipeline 架構。

## 什麼是 Data Pipeline？

Data Pipeline 是遙測資料在傳送到雲端或本機儲存之前流經的處理階段序列。它可實現：

- **資料轉換** - 校正、單位轉換
- **訊號處理** - 濾波、平滑、降噪
- **警示產生** - 閾值監控

## Pipeline 階段

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                              Data Pipeline                                   │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│   ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐   │
│   │   Raw       │    │  Transform  │    │    DSP      │    │  Threshold  │   │
│   │   Value     │───>│  Pipeline   │───>│  Pipeline   │───>│   Check     │   │
│   │             │    │             │    │             │    │             │   │
│   │ From device │    │ Calibration │    │ Moving Avg  │    │ Alert if    │   │
│   │             │    │ Unit Conv   │    │ Kalman      │    │ out of range│   │
│   └─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘   │
│                                                                              │
│                                         │                                    │
│                                         v                                    │
│                              ┌─────────────────────┐                         │
│                              │   Final Value       │                         │
│                              │   (Cloud / Storage) │                         │
│                              └─────────────────────┘                         │
│                                                                              │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 階段 1：Raw Value

透過 Protocol Parser 從裝置讀取的原始值。這是未處理的感測器讀數。

### 階段 2：Transform Pipeline

Transform 依序修改原始值。常見的 Transform：

| Transform | 用途 | 範例 |
|-----------|------|------|
| Calibration | 套用縮放和偏移 | `value * 0.1 + 10` |
| Unit Conversion | 轉換單位 | 攝氏轉華氏 |
| Chunking | 批次資料點 | 將 10 個樣本分組 |

### 階段 3：DSP Pipeline

數位訊號處理濾波器，用於訊號品質：

| 濾波器 | 用途 | 範例 |
|--------|------|------|
| Moving Average | 平滑雜訊 | 5 樣本平均 |
| Kalman | 預測和濾波 | 降噪 |
| Low-pass | 移除高頻 | < 10Hz 通過 |
| High-pass | 移除低頻 | > 1Hz 通過 |

### 階段 4：Threshold Check

根據定義的閾值評估處理後的值：

| 等級 | 條件 | 動作 |
|------|------|------|
| Normal | 在範圍內 | 無動作 |
| Warning | 接近限制 | 記錄警告 |
| Critical | 超過限制 | 產生警示 |

## 處理順序

執行順序是固定的：

1. **Transform 優先** - 始終在 DSP 濾波器之前套用
2. **DSP 濾波器第二** - 在所有 Transform 之後套用
3. **Threshold 最後** - 在最終處理值上評估

在每個 Pipeline 內，操作按**陣列順序**執行（索引 0、1、2...）。

## 設定方式

### 透過 JSON

```json
{
  "Report": {
    "Enabled": true,
    "Interval": 3000,
    "TransformPipeline": [
      {
        "Type": "calibration",
        "Enabled": true,
        "Parameters": { "Scale": 0.1, "Offset": -10 }
      }
    ],
    "DspPipeline": [
      {
        "Type": "movingAverage",
        "Enabled": true,
        "Parameters": { "WindowSize": 5 }
      }
    ],
    "Thresholds": {
      "UpperWarning": 35,
      "UpperCritical": 40
    }
  }
}
```

### 透過程式碼

```csharp
sensor.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform { Scale = 0.1, Offset = -10 });
    })
    .ConfigureDspFilters(pipeline =>
    {
        pipeline.Add(new MovingAverageFilter { WindowSize = 5 });
    });

sensor.Report.Thresholds = new ThresholdConfig
{
    UpperWarning = 35,
    UpperCritical = 40
};
```

## 啟用/停用 Pipeline 步驟

每個步驟可以個別啟用或停用：

```json
{
  "TransformPipeline": [
    {
      "Type": "calibration",
      "Enabled": false,      // <-- 停用，將被跳過
      "Parameters": { "Scale": 0.1 }
    },
    {
      "Type": "unitconversion",
      "Enabled": true,       // <-- 啟用
      "Parameters": { "FromUnit": "celsius", "ToUnit": "fahrenheit" }
    }
  ]
}
```

## Pipeline 事件

訂閱 Pipeline 事件以進行監控：

```csharp
// 來自裝置的原始資料
device.EnableDataReceivedTracking = true;
device.DataReceived += (sender, e) =>
{
    Console.WriteLine($"Raw: {e.Data[0].Value}");
};

// Pipeline 處理後
device.EnableDataProcessedTracking = true;
device.DataProcessed += (sender, e) =>
{
    Console.WriteLine($"Processed: {e.Data[0].Value}");
};
```

## 最佳實務

1. **順序很重要** - 將校正放在單位轉換之前
2. **選擇性啟用** - 僅啟用您需要的 Pipeline 步驟
3. **測試 Transform** - 使用已知值驗證 Transform 參數
4. **監控效能** - 複雜的 DSP 濾波器會增加延遲
5. **明智使用閾值** - 太多警示會降低其有效性

## 另請參閱

- [Transformation](./transformation.md) - Transform 詳情
- [DSP Filters](./dsp-filters.md) - DSP 濾波器詳情
- [感測器設定](../04-sensor-configuration/configuration-reference.md) - 設定參考

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />
