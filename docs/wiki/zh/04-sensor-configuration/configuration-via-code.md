---
sidebar_position: 1
sidebar_label: '透過程式碼設定'
hide_title: true
title: '透過程式碼設定感測器'
keywords: ['SubNode', 'Sensor', 'Configuration', 'Code', 'Programmatic']
description: '使用 SubNode SDK API 以程式方式設定感測器'
---

# 透過程式碼設定

> 使用 SubNode SDK API 以程式方式設定感測器。

## 概述

程式化設定適用於以下情況：
- 需要根據執行時期條件動態設定感測器
- 感測器定義來自外部來源
- 偏好編譯時期驗證設定

## 基本感測器設定

### 使用 TcpModbusDeviceConfiguration

```csharp
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Abstractions.Telemetry;

var modbusConfig = new TcpModbusDeviceConfiguration
{
    DeviceName = "MyDevice",
    Manufacturer = "YourCompany",
    Model = "Device-v1",
    Host = "127.0.0.1",
    Port = 502,
    SlaveId = 1
};
```

### 新增感測器

```csharp
// 建立溫度感測器
var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature.sensor",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};

// 設定報告間隔（毫秒）
tempSensor.Config.Interval = 5000;

// 新增至裝置設定
modbusConfig.AddSensor(tempSensor);
```

### 完整範例

```csharp
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Abstractions.Telemetry;

// 建立應用程式 context
using var context = new WedaApplicationContext(
    options => options.CloudService = WedaFactory.Cloud.Mock);

// 建構裝置設定
var deviceConfig = BuildDeviceConfiguration();

// 建立包含裝置的 SubNode
await using var subNode = new SubNode(context);
subNode.AddDevice(new MyDevice(context, deviceConfig));

await subNode.InitializeAsync();
await subNode.StartAsync();

DeviceConfiguration BuildDeviceConfiguration()
{
    var modbusConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "TemperatureMonitor",
        Manufacturer = "YourCompany",
        Model = "TempMon-v1",
        Host = "192.168.1.100",
        Port = 502,
        SlaveId = 1
    };

    // 新增多個感測器
    modbusConfig.AddSensor(new ModbusSensorReporturation
    {
        Name = "temp.zone1",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    });

    modbusConfig.AddSensor(new ModbusSensorReporturation
    {
        Name = "temp.zone2",
        RegisterAddress = 2,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    });

    modbusConfig.AddSensor(new ModbusSensorReporturation
    {
        Name = "humidity",
        RegisterAddress = 4,
        RegisterCount = 1,
        DataType = ModbusDataType.UInt16,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.AI
    });

    // 轉換為 DeviceConfiguration
    var config = modbusConfig.ToDeviceConfiguration();

    // 初始化 DTDL 中繼資料（雲端註冊所需）
    config.InitializeDtdl();

    return config;
}
```

## 透過程式碼新增 Transform

### Calibration Transform

```csharp
using Weda.SubNode.Core.Transforms;

var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};

// 新增校正轉換
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform
        {
            Scale = 0.1,
            Offset = -10.0
        });
    });
```

### 多個 Transform

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        // 第一步：套用校正
        pipeline.Add(new CalibrationTransform
        {
            Scale = 0.1,
            Offset = -273.15  // 轉換為攝氏
        });

        // 第二步：單位轉換
        pipeline.Add(new UnitConversionTransform
        {
            FromUnit = "celsius",
            ToUnit = "fahrenheit"
        });
    });
```

### 新增 DSP Filter

```csharp
using Weda.SubNode.Core.Dsp;

tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform { Scale = 0.1 });
    })
    .ConfigureDspFilters(pipeline =>
    {
        // 新增移動平均濾波器
        pipeline.Add(new MovingAverageFilter { WindowSize = 5 });

        // 新增 Kalman 濾波器以降噪
        pipeline.Add(new KalmanFilter
        {
            ProcessNoise = 0.01,
            MeasurementNoise = 0.1
        });
    });
```

## 直接操作感測器

### 直接建立感測器

```csharp
using Weda.SubNode.Abstractions.Telemetry;

var sensor = new Sensor
{
    Name = "pressure.sensor",
    SensorGroup = SensorGroup.AI,
    Parameters = new Dictionary<string, object>
    {
        ["RegisterType"] = "HoldingRegister",
        ["RegisterAddress"] = 10,
        ["RegisterCount"] = 2,
        ["DataType"] = "Float32"
    },
    Report = new SensorReport
    {
        Enabled = true,
        Interval = 3000,
        Unit = "pascal"
    },
    SensorInfo = new SensorInfo
    {
        Schema = "double",
        DisplayName = "Pressure Sensor",
        Description = "Measures atmospheric pressure"
    }
};
```

### 新增 Transform 到現有感測器

```csharp
// 使用 AddTransform 方法
sensor.Report.AddTransform(new CalibrationTransform
{
    Scale = 0.001,
    Offset = 101325  // 標準大氣壓
});

// 使用 ConfigureTransforms builder
sensor.Report.ConfigureTransforms(pipeline =>
{
    pipeline
        .Clear()  // 移除現有 transform
        .Add(new CalibrationTransform { Scale = 0.001 });
});
```

## 設定閾值

```csharp
sensor.Report.Thresholds = new ThresholdConfig
{
    LowerWarning = 95000,     // 低壓警告
    LowerCritical = 90000,    // 低壓嚴重
    UpperWarning = 107000,    // 高壓警告
    UpperCritical = 110000    // 高壓嚴重
};
```

## Modbus 資料類型

Modbus 感測器可用的資料類型：

| DataType | 暫存器數 | 說明 |
|----------|----------|------|
| `UInt16` | 1 | 16 位元無符號整數 |
| `Int16` | 1 | 16 位元有符號整數 |
| `UInt32` | 2 | 32 位元無符號整數 |
| `Int32` | 2 | 32 位元有符號整數 |
| `Float32` | 2 | 32 位元浮點數 |
| `Float64` | 4 | 64 位元浮點數 |
| `String16` | N | 16 位元字元字串 |

## Modbus 暫存器類型

| RegisterType | Modbus 功能碼 | 說明 |
|--------------|---------------|------|
| `HoldingRegister` | FC03 | 可讀寫暫存器 |
| `InputRegister` | FC04 | 唯讀暫存器 |
| `Coil` | FC01 | 可讀寫位元 |
| `DiscreteInput` | FC02 | 唯讀位元 |

## 最佳實務

1. **使用強類型設定** - 優先使用 `TcpModbusDeviceConfiguration` 而非原始字典
2. **初始化 DTDL** - 始終呼叫 `InitializeDtdl()` 以進行雲端註冊
3. **設定有意義的名稱** - 使用描述性感測器名稱如 `temp.zone1` 而非 `sensor1`
4. **適當設定間隔** - 在資料即時性與系統負載之間取得平衡
5. **按順序套用 Transform** - 先校正再單位轉換

## 另請參閱

- [透過 JSON 設定](./configuration-via-json.md) - JSON 設定方式
- [設定參考](./configuration-reference.md) - 完整參考
- [Data Pipeline](../05-data-pipeline/pipeline-overview.md) - Transform 詳情

import Revision from '@site/src/components/Revision';

<Revision date="Mar-06, 2026" version="v1.0.0" />
