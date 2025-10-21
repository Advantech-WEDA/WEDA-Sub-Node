# Weda.SubNode.Simulators

Weda SubNode SDK 的測試基礎架構，提供真實的裝置模擬器。

## TcpModbusSimulator

完整的 Modbus TCP 伺服器實作，具有真實感測器數值模擬功能。

### 功能特性

- Modbus TCP 協定（功能碼：03, 06, 10）
- 標準 Modbus 位址（保持暫存器為 40001-49999）
- 可透過 appsettings.json 配置
- 真實的感測器模擬，數值漸進變化
- 多種資料型別：UInt16, Int16, Float32, UInt32, Int32, Float64, UInt64, Int64
- 預定義的感測器類型：Temperature, Humidity, Pressure, Voltage, Current
- 每個感測器可獨立配置模擬參數

### 配置範例

```json
{
  "Simulator": {
    "TcpConnection": {
      "IpAddress": "127.0.0.1",
      "Port": 5020
    },
    "ModbusProtocol": {
      "SlaveId": 1,
      "UseModbusAddressing": true,
      "HoldingRegisterBase": 40001
    },
    "Sensors": [
      {
        "Name": "Temperature",
        "Type": "Temperature",
        "StartAddress": 40001,
        "RegisterCount": 2,
        "DataType": "Float32",
        "SimulationParams": {
          "MinValue": 18.0,
          "MaxValue": 32.0,
          "InitialValue": 25.0,
          "ChangeRate": 0.2,
          "UpdateIntervalMs": 2000,
          "NoiseLevel": 0.1
        }
      }
    ]
  }
}
```

### 感測器類型

每種感測器類型都有真實的預設參數：

- **Temperature**：18-32C，緩慢變化（+/-0.2C/次），低雜訊（+/-0.1C）
- **Humidity**：30-80%，中等變化（+/-0.5%/次），中等雜訊（+/-0.2%）
- **Pressure**：980-1030 hPa，非常緩慢變化（+/-0.1hPa/次），非常低雜訊（+/-0.05hPa）
- **Voltage**：220-240V，小幅變化（+/-0.5V/次），中等雜訊（+/-0.3V）
- **Current**：0-10A，快速變化（+/-1.0A/次），中等雜訊（+/-0.2A）
- **Custom**：使用 SimulationParams 自訂行為

### 使用方式

```csharp
using Microsoft.Extensions.Configuration;
using Weda.SubNode.Simulators.Modbus;

// 載入配置
var config = configuration.GetSection("Simulator")
    .Get<TcpModbusSimulatorConfiguration>();

// 建立並啟動模擬器
var simulator = new TcpModbusSimulator(config, logger);
await simulator.StartAsync();

// 模擬器會根據配置自動更新感測器數值

// 停止模擬器
await simulator.StopAsync();
```

### Modbus 位址對應

當 UseModbusAddressing 為 true 時：
- 位址 40001 對應保持暫存器 0
- 位址 40002 對應保持暫存器 1
- 以此類推

當設為 false 時：
- 位址直接作為暫存器編號使用

### 模擬演算法

模擬器使用有界隨機漫步演算法：

1. 從初始值開始（或在最小/最大值範圍內隨機產生）
2. 每次更新間隔：
   - 產生隨機方向：-1 到 +1
   - 計算變化量：direction * changeRate
   - 加入雜訊：+/- noiseLevel
   - 套用變化：newValue = currentValue + change + noise
   - 限制範圍：Math.Clamp(newValue, minValue, maxValue)

這會產生真實的漸進式數值變化，並保持在配置的範圍內。

### 資料型別轉換

Float32 數值以大端位元組順序編碼至 Modbus 暫存器：
- Float32（4 位元組）-> 2 個暫存器
- Float64（8 位元組）-> 4 個暫存器
- UInt32/Int32（4 位元組）-> 2 個暫存器
- UInt64/Int64（8 位元組）-> 4 個暫存器

### 輸出範例

```
Temperature: 25.00 -> 24.96 -> 25.06 -> 24.92 -> 25.17 -> 25.35 -> 25.24 -> 25.14
Humidity:    55.00 -> 55.29 -> 55.06 -> 54.76 -> 54.83 -> 54.35 -> 53.94 -> 53.60
Pressure:    1013.25 -> 1013.13 -> 1013.14 -> 1013.21（非常緩慢）
Current:     5.00 -> 5.75 -> 5.03 -> 6.48 -> 5.90 -> 2.31 -> 0.08（快速變化）
```

## 參考資料

- [ModbusSimulatorExample](../../examples/ModbusSimulatorExample/) - 完整使用範例
- [Weda.SubNode.Core](../Weda.SubNode.Core/) - 核心框架實作
