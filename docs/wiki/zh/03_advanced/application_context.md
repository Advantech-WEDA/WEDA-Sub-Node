# WedaApplicationContext

`WedaApplicationContext` 是 Weda SubNode SDK 的核心配置和服務容器。它管理框架層級的服務，如雲端連線、日誌記錄和設備註冊表。

## 概覽

`WedaApplicationContext` 提供：
- **Cloud Service** - 連接到 Weda 雲端平台
- **Logger Factory** - 集中式日誌配置
- **Connection Options** - 連線的重試和超時策略
- **Device Options** - 功能開關（遙測、命令、配置更新）
- **Device Registry** - 所有已註冊設備的發現和管理
- **Configuration** - 存取 appsettings.json 中的 `IConfiguration`

## 設備註冊表 (Device Registry)

設備註冊表是實現跨設備通訊的關鍵功能。當您使用 context 創建設備時，設備會自動註冊。

### 取得設備

```csharp
// 依名稱取得設備（找不到時拋出例外）
var device = context.GetDevice("MyDevice");

// 依名稱取得設備並檢查類型（找不到或類型不符時拋出例外）
var modbusDevice = context.GetDevice<TcpModbusDevice>("MyModbusDevice");

// 依名稱尋找設備（找不到時回傳 null）
var device = context.FindDevice("MyDevice");

// 依名稱尋找設備並檢查類型（找不到或類型不符時回傳 null）
var modbusDevice = context.FindDevice<TcpModbusDevice>("MyModbusDevice");
```

### 從設備取得感測器

```csharp
// 依名稱取得感測器（找不到時拋出例外）
var sensor = device.GetSensor("Temperature");

// 依名稱尋找感測器（找不到時回傳 null）
var sensor = device.FindSensor("Temperature");

// 依 ResourceId 取得/尋找感測器
var sensor = device.GetSensorByResourceId("temp-001");
var sensor = device.FindSensorByResourceId("temp-001");
```

## 使用模式

### 預設單例模式（最簡單）

對於簡單的單一設備場景，使用預設單例實例：

```csharp
// 最簡單的用法 - 無需管理 context
var device = new TcpModbusDevice(new WedaApplicationContext(args), config);
await device.StartAsync();
```

`Default` 單例特性：
- 自動從 `appsettings.json` 載入配置
- 執行緒安全的延遲初始化
- 在應用程式中共享
- 不應手動釋放（應用程式結束時自動釋放）

### SubNode 模式（手動創建設備）

在 SubNode 模式中，您手動創建設備。**建議使用 `new WedaApplicationContext(args)` 單例**，確保所有設備共享相同的 context：

```csharp
var context = new WedaApplicationContext(args);

var config = new DeviceConfiguration
{
    DeviceName = "MyDevice",
    // ... 其他配置
};

var device = new MyFirstDevice(context, config);
await device.StartAsync();
```

**重要**：只有使用相同 context 實例創建的設備才能互相發現。如果您使用不同的 context 創建設備，它們將無法互相發現：

```csharp
// 錯誤示範 - 不建議這樣做
using var context1 = new WedaApplicationContext();
using var context2 = new WedaApplicationContext();

var device1 = new MyDevice(context1, config1);
var device2 = new MyDevice(context2, config2);

// device1 無法找到 device2，因為它們有不同的 context
var found = context1.FindDevice("Device2"); // 回傳 null
```

```csharp
// 正確做法 - 使用 Default 單例
var context = new WedaApplicationContext(args);

var device1 = new MyDevice(context, config1);
var device2 = new MyDevice(context, config2);

// device1 可以找到 device2
var found = context.FindDevice("Device2"); // 成功找到
```

### WedaBuilder 模式（自動設備管理）

在 WedaBuilder 模式中，所有設備自動共享單一 context：

```csharp
var builder = WedaApplication.CreateBuilder(args)
    .AddLogging()
    .AddTelemetry()
    .ConfigureNats(options => { /* ... */ });

// 所有設備共享相同的 context
builder.AddDevice<MyFirstDevice>("MyFirstDeviceConfig");
builder.AddDevice<MySecondDevice>("MySecondDeviceConfig");

var app = builder.Build();

// 設備可以互相發現
var device1 = app.Context.GetDevice<MyFirstDevice>("MyFirstDevice");
var device2 = app.Context.GetDevice<MySecondDevice>("MySecondDevice");

await app.RunAsync();
```

## 配置選項

### 連線策略

配置重試和超時行為：

```csharp
builder.ConfigureConnectionPolicy(options =>
{
    options.MaxRetryAttempts = 5;        // 限制重試次數（-1 表示無限）
    options.RetryDelayMs = 2000;          // 初始延遲（指數退避）
    options.MaxRetryDelayMs = 30000;      // 最大延遲上限
    options.ConnectionTimeoutMs = 60000;  // 每次嘗試的超時時間
});
```

### 設備選項

啟用/停用功能：

```csharp
// 啟用遙測發送
builder.AddTelemetry();

// 啟用從雲端接收命令
builder.AddCommands();

// 啟用從雲端接收配置更新
builder.AddConfigUpdates();
```

## 跨設備通訊範例

```csharp
public class ControllerDevice : TcpModbusDevice
{
    public ControllerDevice(IWedaApplicationContext context, DeviceConfiguration config)
        : base(context, config)
    {
        DataReceived += OnDataReceived;
    }

    private async void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // 在相同 context 中尋找其他設備
        var actuator = _context.FindDevice<ActuatorDevice>("Actuator1");
        if (actuator != null)
        {
            // 從執行器取得感測器
            var valve = actuator.FindSensor("ValveControl");

            // 執行跨設備操作
            // ...
        }
    }
}
```

## 最佳實踐

1. **共享 Context**：當設備需要通訊時，確保它們共享相同的 `WedaApplicationContext`。

2. **多設備應用使用 WedaBuilder**：對於有多個設備的應用程式，建議使用 WedaBuilder 模式，因為它自動處理 context 共享。

3. **正確釋放資源**：使用完畢後務必釋放 context。釋放 context 會取消註冊所有設備。

4. **類型安全存取**：當您知道設備類型時，使用泛型方法（`GetDevice<T>`、`FindDevice<T>`）以確保類型安全。

5. **Null 檢查**：當設備/感測器可能不存在時，使用 `FindDevice`/`FindSensor` 搭配 null 檢查，而非捕捉 `GetDevice`/`GetSensor` 的例外。
