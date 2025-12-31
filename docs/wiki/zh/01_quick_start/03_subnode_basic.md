---
title: "使用 subnode 模板建立自訂裝置"
description: "使用 console 風格的模板建立第一個 SubNode 應用程式"
author: "Rain Hu"
date: "2025-11-12"
lang: "zh"
parent: "README"
prev: "02_wedabuilder_basic"
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
- **wedabuilder** ↔ **Web API**：production 時，用於 multi device 一次建立

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

### 步驟 1: 建立專案

```bash
# 確保您在 solution 根目錄下
# 切換到 devices 目錄(或其它自定目錄下)
mkdir -p devices
cd devices

# 建立專案
dotnet new subnode -n MyFirstSubnode

# 回到 solution 根目錄
cd ..

# 加入到 solution
dotnet sln add devices/MyFirstSubnode/MyFirstSubnode.csproj

# 進入專案目錄
cd devices/MyFirstSubnode

# 還原 NuGet 套件
dotnet restore
```

**重要提示**：
- 由於 template 使用 project reference，專案**必須建立在 `devices/` 目錄或其它相同深度的目錄下**
- 這樣可以確保 project reference 正確解析到上層的 SDK 專案
- 必須先 `cd devices` 再執行 `dotnet new`，因為 `-n` 參數不能包含路徑分隔符

### 步驟 2: 查看專案結構

```
devices/MyFirstSubnode/
├── appsettings.json        # 配置檔案（Serilog 日誌設定）
├── MyFirstSubnode.csproj   # 專案檔（包含 project reference）
├── MyFirstDevice.cs        # 裝置類別（繼承自 TcpModbusDevice）
└── Program.cs              # 應用程式進入點（手動配置裝置與 Simulator）
```

**重點說明**：
- 與 wedabuilder 不同，subnode 使用 **programmatic 配置**（在 `Program.cs` 中）
- `appsettings.json` 僅包含 Serilog 日誌設定
- 適合需要完全控制裝置生命週期的開發場景
- `MyFirstSubnode.csproj` 使用 project reference 參考 SDK 專案

### 步驟 3: 執行專案

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
        SlaveId = 1,
        // DTDL 路徑 (相對於 solution root，由 LoadDtdl 自動解析)
        DtdlPath = "assets/dtdl/dtmi/advantech/edgesync/sample-1.json"
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

    // 轉換為 DeviceConfiguration
    var deviceConfig = modbusDeviceConfig.ToDeviceConfiguration();

    // 載入 DTDL metadata (雲端註冊時必須)
    deviceConfig.LoadDtdl();

    return deviceConfig;
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

### DTDL 的重要性

**為什麼需要 LoadDtdl()？**

`deviceConfig.LoadDtdl()` 是**必須的**，原因：
1. **雲端註冊要求**：上傳 device configuration 到雲端時，必須包含 DTDL metadata
2. **Schema Validation**：確保 sensor 定義符合 DTDL 標準
3. **類型安全**：提供完整的 telemetry 類型資訊給雲端平台

**DTDL 路徑說明**：
- 路徑是**相對於 solution root**（包含 .sln 或 .git 的目錄）
- `LoadDtdl()` 會自動尋找 solution root 目錄，無需手動計算層級
- SDK 提供標準 DTDL 定義在 `assets/dtdl/dtmi/advantech/edgesync/`
- 只需使用簡單的相對路徑：`assets/dtdl/...`，SDK 會自動解析
- **Dev Container 兼容**：自動檢測 `/workspace` 掛載點，在容器內也能正常工作

**常見錯誤**：
- ❌ 忘記呼叫 `LoadDtdl()` → 雲端註冊失敗
- ❌ DTDL 路徑錯誤 → FileNotFoundException
- ✅ 正確配置 DtdlPath 並呼叫 LoadDtdl()

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

### 步驟 3: 連接真實雲端

**方法 1: 移除 Mock，並用 programmatic 的方式配置連線**

```csharp
using var context = new WedaApplicationContext(options =>
{
    // 移除 Mock,SDK 會自動連接 NATS
    // options.CloudService = WedaFactory.Cloud.Mock;

    // 配置 NATS 連線
    options.NatsConnectionSettings = new NatsConnectionSettings
    {
        Url = "nats://your-nats-server:4222",
        CredFile = "/path/to/nats.creds"
    };
});
```

**方法 2: 移除 Mock，並使用 appsettings.json 配置連線**

```csharp
using var context = new WedaApplicationContext(options =>
{
    // SDK 會自動從 appsettings.json 讀取 NATS 設定
});
```

在 `appsettings.json` 加入:

```json
{
  "WedaNode": {
    "Url": "nats://your-nats-server:4222",
    "CredFile": "/path/to/nats.creds"
  }
}
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
