---
sidebar_position: 3
sidebar_label: '設定參考'
hide_title: true
title: '感測器設定參考'
keywords: ['SubNode', 'Sensor', 'Configuration', 'Reference', 'API']
description: 'SubNode 感測器設定選項的完整參考'
---

# 設定參考

> SubNode 感測器設定選項的完整參考。

## 感測器屬性

### 核心屬性

| 屬性 | 類型 | 必要 | 預設值 | 說明 |
|------|------|------|--------|------|
| `Name` | string | 是 | - | 裝置內的唯一感測器識別碼 |
| `ResourceId` | string | 否 | 自動產生 | 依循 Device Capability Guideline 的 UUID |
| `SensorGroup` | enum | 是 | - | 感測器類別 |
| `Dtmi` | string | 否 | 自動產生 | Digital Twin Model Identifier |

### SensorGroup 值

| 值 | 說明 | 典型資料類型 |
|----|------|--------------|
| `AI` | Analog Input | double |
| `AO` | Analog Output | double |
| `DI` | Digital Input | boolean |
| `DO` | Digital Output | boolean |
| `TEMP` | Temperature | double |
| `PWR` | Power/Energy | double |
| `SYS` | System metrics | 各種 |

## Parameters（協定特定）

### Modbus Parameters

| 參數 | 類型 | 必要 | 值 | 說明 |
|------|------|------|-----|------|
| `RegisterType` | string | 是 | HoldingRegister、InputRegister、Coil、DiscreteInput | Modbus 功能碼 |
| `RegisterAddress` | int | 是 | 0-65535 | 起始暫存器位址 |
| `RegisterCount` | int | 是 | 1-125 | 要讀取的暫存器數量 |
| `DataType` | string | 是 | 見下表 | 資料解譯方式 |

#### Modbus 資料類型

| DataType | 暫存器數 | 範圍/精度 |
|----------|----------|-----------|
| `UInt16` | 1 | 0 至 65,535 |
| `Int16` | 1 | -32,768 至 32,767 |
| `UInt32` | 2 | 0 至 4,294,967,295 |
| `Int32` | 2 | -2,147,483,648 至 2,147,483,647 |
| `Float32` | 2 | IEEE 754 單精度 |
| `Float64` | 4 | IEEE 754 雙精度 |
| `String16` | N | 16 位元字元 |

### MQTT Parameters

| 參數 | 類型 | 必要 | 預設值 | 說明 |
|------|------|------|--------|------|
| `Topic` | string | 是 | - | 要訂閱的 MQTT 主題 |
| `Qos` | int | 否 | 0 | Quality of Service（0、1、2）|
| `JsonPath` | string | 否 | - | 用於值提取的 JSONPath 表達式 |
| `PayloadType` | string | 否 | "json" | 負載格式：json、binary、text |

### HTTP Parameters

| 參數 | 類型 | 必要 | 預設值 | 說明 |
|------|------|------|--------|------|
| `Endpoint` | string | 是 | - | HTTP 端點 URL |
| `Method` | string | 否 | "GET" | HTTP 方法 |
| `JsonPath` | string | 否 | - | 用於回應解析的 JSONPath |
| `Headers` | object | 否 | - | 自訂 HTTP 標頭 |

## SensorInfo

用於 DTDL 產生和顯示的中繼資料：

| 屬性 | 類型 | 必要 | 預設值 | 說明 |
|------|------|------|--------|------|
| `Schema` | string | 否 | 從 SensorGroup 推斷 | DTDL schema 類型 |
| `DisplayName` | string | 否 | 與 Name 相同 | 人類可讀名稱 |
| `Description` | string | 否 | - | 感測器說明 |

### Schema 類型

| Schema | 說明 | C# 類型 |
|--------|------|---------|
| `boolean` | True/false 值 | bool |
| `integer` | 整數 | int/long |
| `double` | 浮點數 | double |
| `string` | 文字值 | string |
| `dateTime` | ISO 8601 時間戳記 | DateTime |

## Report 設定

### 基本 Report 設定

| 屬性 | 類型 | 必要 | 預設值 | 說明 |
|------|------|------|--------|------|
| `Enabled` | bool | 否 | true | 啟用感測器報告 |
| `Interval` | double | 否 | 1000 | 取樣間隔（毫秒）|
| `Unit` | string | 否 | - | 量測單位 |

### TransformPipeline

按順序執行的 Transform 設定陣列：

```json
{
  "TransformPipeline": [
    {
      "Type": "transform_type",
      "Enabled": true,
      "Parameters": { ... }
    }
  ]
}
```

#### 內建 Transform

| Type | Parameters | 說明 |
|------|------------|------|
| `calibration` | Scale、Offset | 線性校正：`value * Scale + Offset` |
| `unitconversion` | FromUnit、ToUnit | 單位轉換 |
| `chunking` | ChunkSize | 批次資料點 |

##### Calibration Parameters

| 參數 | 類型 | 預設值 | 說明 |
|------|------|--------|------|
| `Scale` | double | 1.0 | 乘法因子 |
| `Offset` | double | 0.0 | 加法偏移 |

##### Unit Conversion Parameters

| 參數 | 類型 | 說明 |
|------|------|------|
| `FromUnit` | string | 來源單位 |
| `ToUnit` | string | 目標單位 |

支援的單位轉換：
- 溫度：celsius、fahrenheit、kelvin
- 壓力：pascal、bar、psi、atm
- 長度：meter、foot、inch

### DspPipeline

DSP 濾波器設定陣列：

```json
{
  "DspPipeline": [
    {
      "Type": "filter_type",
      "Enabled": true,
      "Parameters": { ... }
    }
  ]
}
```

#### 內建 DSP 濾波器

| Type | Parameters | 說明 |
|------|------------|------|
| `movingAverage` | WindowSize | 簡單移動平均 |
| `kalman` | ProcessNoise、MeasurementNoise | Kalman 濾波器 |
| `lowpass` | CutoffFrequency、SampleRate | 低通濾波器 |
| `highpass` | CutoffFrequency、SampleRate | 高通濾波器 |

##### Moving Average Parameters

| 參數 | 類型 | 預設值 | 說明 |
|------|------|--------|------|
| `WindowSize` | int | 5 | 要平均的樣本數 |

##### Kalman Filter Parameters

| 參數 | 類型 | 預設值 | 說明 |
|------|------|--------|------|
| `ProcessNoise` | double | 0.01 | 過程噪聲協方差 |
| `MeasurementNoise` | double | 0.1 | 量測噪聲協方差 |

### Thresholds

警示閾值設定：

| 屬性 | 類型 | 說明 |
|------|------|------|
| `LowerWarning` | double? | 當值 <= 閾值時警告 |
| `LowerCritical` | double? | 當值 <= 閾值時嚴重 |
| `UpperWarning` | double? | 當值 >= 閾值時警告 |
| `UpperCritical` | double? | 當值 >= 閾值時嚴重 |

閾值評估順序：Critical 閾值在 Warning 閾值之前檢查。

## Record 設定

本機記錄設定：

| 屬性 | 類型 | 必要 | 預設值 | 說明 |
|------|------|------|--------|------|
| `Enabled` | bool | 否 | false | 啟用本機記錄 |
| `Path` | string | 否 | - | 記錄檔路徑 |

## DeviceCommunication

裝置通訊的連線設定：

### TCP/Modbus TCP

| 屬性 | 類型 | 必要 | 預設值 | 說明 |
|------|------|------|--------|------|
| `Host` | string | 是 | - | IP 位址或主機名稱 |
| `Port` | int | 是 | - | TCP 連接埠 |
| `Timeout` | int | 否 | 5000 | 連線逾時（毫秒）|
| `RetryCount` | int | 否 | 3 | 重試次數 |

### Serial/Modbus RTU

| 屬性 | 類型 | 必要 | 預設值 | 說明 |
|------|------|------|--------|------|
| `PortName` | string | 是 | - | COM 連接埠（例如 "COM1"、"/dev/ttyUSB0"）|
| `BaudRate` | int | 否 | 9600 | 鮑率 |
| `DataBits` | int | 否 | 8 | 資料位元 |
| `Parity` | string | 否 | "None" | 同位：None、Odd、Even |
| `StopBits` | string | 否 | "One" | 停止位元：One、OnePointFive、Two |

### MQTT

| 屬性 | 類型 | 必要 | 預設值 | 說明 |
|------|------|------|--------|------|
| `BrokerUrl` | string | 是 | - | MQTT Broker URL |
| `ClientId` | string | 否 | 自動產生 | MQTT Client ID |
| `Username` | string | 否 | - | 認證使用者名稱 |
| `Password` | string | 否 | - | 認證密碼 |
| `UseTls` | bool | 否 | false | 啟用 TLS |

## Periods

時間設定：

| 屬性 | 類型 | 預設值 | 說明 |
|------|------|--------|------|
| `ReadTelemetryInterval` | int | 1000 | 遙測讀取間隔（毫秒）|
| `HealthReportInterval` | int | 30000 | 健康報告間隔（毫秒）|
| `TelemetrySendInterval` | int | 5000 | 雲端傳送間隔（毫秒）|

## Properties

協定特定屬性：

### Modbus Properties

| 屬性 | 類型 | 預設值 | 說明 |
|------|------|--------|------|
| `SlaveId` | int | 1 | Modbus slave/unit ID |
| `ByteOrder` | string | "BigEndian" | 位元組順序：BigEndian、LittleEndian |
| `WordOrder` | string | "BigEndian" | 32 位元值的字組順序 |

## 設定驗證

SDK 在啟動時驗證設定：

| 驗證 | 錯誤訊息 |
|------|----------|
| 缺少 Name | "Sensor name is required" |
| 無效 RegisterType | "Invalid register type: {value}" |
| RegisterAddress 超出範圍 | "Register address must be 0-65535" |
| 無效 DataType | "Unsupported data type: {value}" |
| Interval <= 0 | "Interval must be greater than 0" |

## 另請參閱

- [透過程式碼設定](./configuration-via-code.md) - 程式化設定
- [透過 JSON 設定](./configuration-via-json.md) - JSON 設定
- [Data Pipeline](../05-data-pipeline/pipeline-overview.md) - Pipeline 詳情

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />
