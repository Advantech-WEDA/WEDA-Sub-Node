---
sidebar_position: 1
sidebar_label: '透過程式碼設定'
hide_title: true
title: '透過程式碼設定 | SubNode SDK'
keywords: ['SubNode', 'Sensor', 'Configuration', 'Code', 'Programmatic']
description: '使用 SubNode SDK API 以程式方式設定裝置和感測器。'
---

# 透過程式碼設定

> 使用 SubNode SDK API 以程式方式設定裝置和感測器。

## Overview

SubNode 除了 JSON 設定檔外，也支援透過程式碼動態定義裝置和感測器。Programmatic 設定適合需要在執行時期動態調整，或感測器定義來自外部系統的場景。本文以 SubNode 範本為例，示範如何透過 `TcpModbusDeviceConfiguration` API 建構設定。

## What You'll Learn

閱讀本文後，你將能夠：

- 使用 `TcpModbusDeviceConfiguration` 建構裝置設定
- 以程式碼新增感測器、Transform 和 DSP Filter
- 理解 programmatic 設定和 JSON 設定的適用場景

## Prerequisites

- 完成[使用範本開始](../02-getting-started/03-start-with-template.md)
- 了解 [SubNode 階層架構](../03-hierarchy/02-aggregation-overview.md)

---

## 建構裝置設定

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
    Port = 5020,
    SlaveId = 1
};
```

### 新增感測器

```csharp
var tempSensor = new ModbusSensorReporturation
{
    Name = "temperature_sensor",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};

// Set reporting interval (milliseconds)
tempSensor.Config.Interval = 5000;

// Add to device configuration
modbusConfig.AddSensor(tempSensor);
```

### 新增多個感測器

```csharp
modbusConfig.AddSensor(new ModbusSensorReporturation
{
    Name = "temp_zone1",
    RegisterAddress = 0,
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
});

modbusConfig.AddSensor(new ModbusSensorReporturation
{
    Name = "temp_zone2",
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
```

---

## 完整範例

搭配 SubNode 範本使用：

```csharp
using Weda.SubNode.Host;
using Weda.SubNode.Host.Context;
using Weda.SubNode.Cloud;
using Weda.SubNode.Core.Protocols.Modbus;
using Weda.SubNode.Abstractions.Telemetry;

using var context = new WedaApplicationContext(
    options => options.CloudService = Cloud.Mock());

await using var subNode = new SubNode(context);
subNode.AddDevice(new MyDevice(context, BuildDeviceConfiguration()));

await subNode.InitializeAsync();
await subNode.StartAsync();

TcpModbusDeviceConfiguration BuildDeviceConfiguration()
{
    var config = new TcpModbusDeviceConfiguration
    {
        DeviceName = "TemperatureMonitor",
        Manufacturer = "YourCompany",
        Model = "TempMon-v1",
        Host = "127.0.0.1",
        Port = 5020,
        SlaveId = 1
    };

    var tempSensor = new ModbusSensorReporturation
    {
        Name = "temperature_sensor",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };
    tempSensor.Config.Interval = 1000;

    config.AddSensor(tempSensor);

    return config;
}
```

> SubNode 範本的 `TcpModbusDeviceConfiguration` 會自動轉換為 `DeviceConfiguration`，不需要手動呼叫 `ToDeviceConfiguration()`。

---

## 新增 Transform 和 DSP Filter

### Calibration Transform

```csharp
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

### 多個 Transform（按順序執行）

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        // Step 1: Calibration
        pipeline.Add(new CalibrationTransform
        {
            Scale = 0.1,
            Offset = -273.15
        });

        // Step 2: Unit conversion
        pipeline.Add(new UnitConversionTransform
        {
            FromUnit = "celsius",
            ToUnit = "fahrenheit"
        });
    });
```

### DSP Filter

```csharp
tempSensor.Config.Report
    .ConfigureTransforms(pipeline =>
    {
        pipeline.Add(new CalibrationTransform { Scale = 0.1 });
    })
    .ConfigureDspFilters(pipeline =>
    {
        pipeline.Add(new MovingAverageFilter { WindowSize = 5 });
    });
```

### Threshold

```csharp
tempSensor.Config.Report.Thresholds = new ThresholdConfig
{
    LowerWarning = 10,
    LowerCritical = 5,
    UpperWarning = 35,
    UpperCritical = 40
};
```

---

## Modbus 資料類型參考

| DataType | 暫存器數 | 說明 |
|----------|----------|------|
| `UInt16` | 1 | 16-bit unsigned integer |
| `Int16` | 1 | 16-bit signed integer |
| `UInt32` | 2 | 32-bit unsigned integer |
| `Int32` | 2 | 32-bit signed integer |
| `Float32` | 2 | 32-bit floating point |
| `Float64` | 4 | 64-bit floating point |

| RegisterType | Modbus Function Code | 說明 |
|--------------|---------------------|------|
| `HoldingRegister` | FC03 | Read/Write registers |
| `InputRegister` | FC04 | Read-only registers |
| `Coil` | FC01 | Read/Write bits |
| `DiscreteInput` | FC02 | Read-only bits |

---

## 自訂 DeviceConfiguration

如果你[自訂了裝置](../09-customization/01-custom-device.md)（例如自訂協定），可以實作 `IDeviceConfiguration` 介面，為你的裝置提供強類型的 programmatic 設定 API。

### IDeviceConfiguration 介面

```csharp
public interface IDeviceConfiguration
{
    /// <summary>
    /// Converts this strongly-typed configuration to the generic DeviceConfiguration
    /// used by the DeviceBase framework.
    /// </summary>
    DeviceConfiguration ToDeviceConfiguration();
}
```

只需實作一個方法：將你的強類型設定轉換為框架通用的 `DeviceConfiguration`。

### 範例：自訂 Serial Device 設定

```csharp
public class SerialDeviceConfiguration : IDeviceConfiguration
{
    public required string DeviceName { get; set; }
    public required string PortName { get; set; }  // e.g. "/dev/ttyUSB0"
    public int BaudRate { get; set; } = 9600;
    public byte SlaveId { get; set; } = 1;
    public List<ModbusSensorReporturation> Sensors { get; set; } = new();

    public SerialDeviceConfiguration AddSensor(ModbusSensorReporturation sensor)
    {
        Sensors.Add(sensor);
        return this;
    }

    public DeviceConfiguration ToDeviceConfiguration()
    {
        return new DeviceConfiguration
        {
            DeviceName = DeviceName,
            DeviceCommunication = new Dictionary<string, object>
            {
                ["PortName"] = PortName,
                ["BaudRate"] = BaudRate,
                ["SlaveId"] = SlaveId
            },
            Sensors = Sensors.Select(s => /* convert to Sensor */ ...).ToList()
        };
    }
}
```

使用方式與 `TcpModbusDeviceConfiguration` 相同 -- 直接傳入 configuration 實例，由 Device 內部呼叫 `ToDeviceConfiguration()`：

```csharp
// Device constructor receives SerialDeviceConfiguration, calls ToDeviceConfiguration() internally
public class MySerialDevice : RtuModbusDevice
{
    public MySerialDevice(IWedaApplicationContext context, SerialDeviceConfiguration config)
        : base(context, config.ToDeviceConfiguration()) { }
}

// Usage - pass the strongly-typed configuration directly
var config = new SerialDeviceConfiguration
{
    DeviceName = "MySerialDevice",
    PortName = "/dev/ttyUSB0",
    BaudRate = 9600
};
config.AddSensor(new ModbusSensorReporturation { ... });

subNode.AddDevice(new MySerialDevice(context, config));
```

> SDK 內建的 `TcpModbusDeviceConfiguration` 就是 `IDeviceConfiguration` 的參考實作，可作為自訂設定類別的範本。呼叫者不需要手動呼叫 `ToDeviceConfiguration()`，這是 Device 的責任。

---

## Programmatic vs JSON 設定

| 面向 | Programmatic | JSON |
|------|-------------|------|
| 適用場景 | 動態設定、外部來源、SubNode 範本 | 靜態設定、SA 操作、WedaBuilder 範本 |
| 編譯時期檢查 | 有（強類型） | 無 |
| 雲端同步 | 支援（透過 `.device-config-cache.json` 持久化）| 支援（透過 `devicecfg.json`）|
| 初始設定來源 | 程式碼（可被 cache 覆蓋） | JSON 檔案（可被 cache 覆蓋）|
| 變更方式 | 需重新編譯 | 修改 JSON 即可 |

> 兩種方式都支援雲端同步。雲端下發的設定變更會被持久化到 `.device-config-cache.json`，確保離線狀態重啟時仍能使用最後一次雲端同步的設定。使用 `--no-cache` 參數可強制忽略 cache。

---

## Summary

- `TcpModbusDeviceConfiguration` 提供強類型 API 建構 Modbus 裝置設定
- `ModbusSensorReporturation` 定義感測器的暫存器位址、資料類型和報告間隔
- Transform 和 DSP Filter 透過 `ConfigureTransforms` / `ConfigureDspFilters` fluent API 新增
- Programmatic 設定適合動態場景，JSON 設定適合靜態部署

## See Also

- [透過 JSON 設定](./02-configuration-via-json.md) - JSON 設定方式
- [設定範例](./03-configuration-examples.md) - 各協定的完整設定範例
- [Data Pipeline](../05-data-pipeline/01-overview.md) - Transform 和 DSP Filter 詳情

---

## Change History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-30 | Rain Hu | Doc created. |
