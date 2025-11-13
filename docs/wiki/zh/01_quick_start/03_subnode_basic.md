---
title: "使用 subnode 模板建立自訂裝置"
description: "使用 console 風格的模板建立第一個 SubNode 應用程式"
author: "Rain Hu"
date: "2025-11-12"
lang: "zh"
parent: "README"
prev: "02_wedaapi_basic"
next: "04_configuration"
translations:
  - lang: "en"
    path: "../../en/01_quick_start/03_subnode_basic.md"
examples:
  - "examples/basic-modbus"
templates:
  - "subnode"
---

# 使用 subnode 模板建立自訂裝置

學習如何使用 `subnode` 模板建立 IoT 裝置應用程式。`subnode` 是一個 **console 風格的模板**，適合用於單一裝置的開發和調試。

## 模板類比

- **subnode** ↔ **Console App**：方便用於單一 device 的開發、調試
- **wedaapi** ↔ **Web API**：production 時，用於 multi device 一次建立

**所需時間**: 15 分鐘

---

## 學習目標

完成本教學後，您將學會:

1. 使用 `dotnet new subnode` 建立專案並執行
2. 理解如何透過繼承 `TcpModbusDevice` 上傳 telemetry
3. 將 simulator 置換為真實裝置並連接雲端服務

---

## 先決條件

- 已安裝 .NET 9.0 SDK
- 已安裝 Weda SubNode 模板(參考 [安裝模板](01_install_templates.md))

---

## 目標 1: 建立並執行 subnode template

### 建立專案

```bash
mkdir -p devices/MyFirstDevice
cd devices/MyFirstDevice
dotnet new subnode -n MyFirstDevice
```

### 執行專案

```bash
dotnet run
```

**預期輸出**:
```
[15:30:21 INF] Modbus Simulator started on 127.0.0.1:5020
[15:30:22 INF] Device 'MySubNode' initialized successfully
[15:30:22 INF] Telemetry received:
[15:30:22 INF]   - temperature.sensor: 25.3
```

按 `Ctrl+C` 停止應用程式。

---

## 目標 2: 理解如何上傳 Telemetry

### MyFirstDevice.cs

這個檔案定義了您的自訂裝置，繼承自 `TcpModbusDevice`:

```csharp
public class MyFirstDevice : TcpModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration)
    {
        // 訂閱 DataReceived 事件
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // e.Data 包含所有感測器的最新數值
        foreach (var measure in e.Data)
        {
            _logger.LogInformation("  - {Name}: {Value}",
                measure.Name, measure.Value);
        }
    }

    ~MyFirstDevice()
    {
        DataReceived -= OnDataReceived;
    }
}
```

### Program.cs

這個檔案負責啟動應用程式並設定裝置:

```csharp
try
{
    // 1. 建立 ApplicationContext (使用 Mock 雲端服務)
    using var context = new WedaApplicationContext(options =>
    {
        options.CloudService = WedaFactory.Cloud.Mock;
    });

    // 2. 啟動 Modbus Simulator
    var simulator = await ConfigureTcpModbusSimulator(context);

    // 3. 設定並啟動裝置
    var deviceConfig = ConfigureDeviceConfiguration();
    var device = new MyFirstDevice(context, deviceConfig);
    await device.InitializeAsync();
    await device.StartAsync();

    // 4. 等待停止訊號
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
    await Task.Delay(Timeout.Infinite, cts.Token);

    // 5. 優雅關閉
    await device.StopAsync();
    device.Dispose();
    await simulator.StopAsync();
}
catch (OperationCanceledException)
{
    Console.WriteLine("Application stopped");
}

// 定義資料的來源
DeviceConfiguration ConfigureDeviceConfiguration()
{
    var modbusDeviceConfig = new TcpModbusDeviceConfiguration
    {
        DeviceName = "MySubNode",
        Manufacturer = "Advantech",
        Model = "CustomDevice-v1",
        Host = "127.0.0.1",
        Port = 5020,
        SlaveId = 1
    };

    var tempSensor = new ModbusSensorConfiguration
    {
        Name = "temperature.sensor",
        Dtmi = "dtmi:advantech:EdgeSync:Temperature;1",
        RegisterAddress = 0,
        RegisterCount = 2,
        DataType = ModbusDataType.Float32,
        RegisterType = ModbusRegisterType.HoldingRegister,
        SensorGroup = SensorGroup.TEMP
    };

    modbusDeviceConfig.AddSensor(tempSensor);
    return modbusDeviceConfig.ToDeviceConfiguration();
}

// 定義模擬器的參數
async Task<TcpModbusSimulator> ConfigureTcpModbusSimulator(WedaApplicationContext context)
{
    var simulatorConfig = new TcpModbusSimulatorConfiguration
    {
        TcpConnection = new TcpConnectionSettings
        {
            IpAddress = "127.0.0.1",
            Port = 5020
        },
        ModbusProtocol = new ModbusProtocolSettings
        {
            SlaveId = 1,
            UseModbusAddressing = false,
            HoldingRegisterBase = 0
        },
        Simulation = new SimulationSettings
        {
            GlobalUpdateIntervalSeconds = 5,
            EnableValueChanges = true
        },
        Sensors = new List<SimulatedSensor>
        {
            new SimulatedSensor
            {
                Name = "TemperatureSensor",
                Type = SensorType.Temperature,
                StartAddress = 0,
                RegisterCount = 2,
                DataType = SimulatedDataType.Float32,
                SimulationParams = new SensorSimulationParams
                {
                    MinValue = 18.0,
                    MaxValue = 32.0,
                    InitialValue = 25.0,
                    ChangeRate = 0.2,
                    NoiseLevel = 0.1
                }
            }
        }
    };

    var logger = context.LoggerFactory.CreateLogger<TcpModbusSimulator>();
    var simulator = new TcpModbusSimulator(simulatorConfig, logger: logger);
    await simulator.StartAsync();
    return simulator;
}
```

### Telemetry 上傳流程

1. SDK 自動從 Modbus 裝置讀取 sensor 資料
2. SDK 觸發 `DataReceived` 事件
3. 您的 `OnDataReceived` 方法被呼叫，可以處理資料
4. SDK 自動將 telemetry 上傳到雲端服務

---

## 目標 3: 連接真實裝置

### 步驟 1: 移除 Simulator

在 `Program.cs` 中註解掉 simulator:

```csharp
// var simulator = await ConfigureTcpModbusSimulator(context);
// ...
// await simulator.StopAsync();
```

### 步驟 2: 修改裝置設定

修改 `ConfigureDeviceConfiguration()`:

```csharp
var modbusDeviceConfig = new TcpModbusDeviceConfiguration
{
    DeviceName = "MySubNode",
    Manufacturer = "Advantech",
    Model = "CustomDevice-v1",
    Host = "192.168.1.100",  // 改成真實裝置 IP
    Port = 502,              // Modbus 標準 Port
    SlaveId = 1
};

var tempSensor = new ModbusSensorConfiguration
{
    Name = "temperature.sensor",
    Dtmi = "dtmi:advantech:EdgeSync:Temperature;1",
    RegisterAddress = 0,     // 根據裝置手冊設定
    RegisterCount = 2,
    DataType = ModbusDataType.Float32,
    RegisterType = ModbusRegisterType.HoldingRegister,
    SensorGroup = SensorGroup.TEMP
};
```

### 步驟 3: 連接真實雲端 (選擇性)

**方法 1: 移除 Mock**

```csharp
using var context = new WedaApplicationContext(options =>
{
    // 移除 Mock,SDK 會自動連接 NATS
    // options.CloudService = WedaFactory.Cloud.Mock;
});
```

**方法 2: 使用 appsettings.json**

在 `appsettings.json` 加入:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [ { "Name": "Console" } ]
  },
  "Nats": {
    "Url": "nats://your-nats-server:4222",
    "CredFile": "",
    "Name": "default",
    "SerializerType": "json"
  }
}
```

然後移除 Mock:

```csharp
using var context = new WedaApplicationContext(options =>
{
    // SDK 會自動從 appsettings.json 讀取 NATS 設定
});
```

### 執行

```bash
dotnet run
```

---

## 總結

1. **建立並執行**: `dotnet new subnode` 建立專案並執行
2. **上傳 Telemetry**: 繼承 `TcpModbusDevice`,訂閱 `DataReceived` 事件
3. **連接真實裝置**: 修改 Host/Port 連接真實裝置,移除 Mock 連接雲端

---

## 下一步

- [配置方式詳解](04_configuration.md)（選讀）- 深入了解 appsettings.json vs programmatic 配置

---

**版本**: 1.0.0
**最後更新**: 2025-11-12
**維護者**: Rain Hu
