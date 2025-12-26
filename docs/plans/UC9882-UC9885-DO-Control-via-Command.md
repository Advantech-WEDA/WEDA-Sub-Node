# UC9882-UC9885: DO Control via Command

## 目標

支援透過 Cloud Command 控制 Modbus 和 ISensing 設備的 Digital Output (DO)。

## 需求摘要

| 項目 | 說明 |
|------|------|
| 功能 | 透過 Command 控制設備 DO 狀態 (ON/OFF) |
| 協定 | Modbus TCP (FC 05) 和 ISensing (MQTT) |
| 安全檢查 | Sensor name 不存在時拒絕執行並報錯 |
| 配置方式 | DO 定義在 Sensor 配置中，非直接指定 register address |

---

## Command 格式

### Cloud 下發的 DeviceCommand

```json
{
  "DeviceCmd": "SetDO",
  "Timeout": 5000,
  "RespTopic": "v1/group1/device123/dm/cmd/response",
  "Parameters": {
    "name": "do0",
    "state": true
  }
}
```

| 參數 | 類型 | 說明 |
|------|------|------|
| `name` | string | Sensor 名稱 (必須存在於 Device Configuration 中) |
| `state` | boolean | DO 狀態: `true` = ON, `false` = OFF |

### 替代參數名稱 (相容性)

為了相容 ISensing 既有格式，也支援：
- `do` 作為 `name` 的別名
- `outputName` 作為 `name` 的別名

---

## Sensor 配置格式

### Modbus DO 配置

```json
{
  "Sensors": [
    {
      "Name": "do0",
      "SensorGroup": "DO",
      "Dtmi": "dtmi:advantech:EdgeSync:DigitalOutput;1",
      "Parameters": {
        "RegisterType": "Coil",
        "RegisterAddress": 0
      }
    },
    {
      "Name": "do1",
      "SensorGroup": "DO",
      "Dtmi": "dtmi:advantech:EdgeSync:DigitalOutput;1",
      "Parameters": {
        "RegisterType": "Coil",
        "RegisterAddress": 1
      }
    }
  ]
}
```

### ISensing DO 配置

```json
{
  "Sensors": [
    {
      "Name": "do0",
      "SensorGroup": "DO",
      "Dtmi": "dtmi:advantech:EdgeSync:DigitalOutput;1",
      "Parameters": {}
    }
  ]
}
```

---

## 架構設計

### Command 執行流程

```
Cloud
  ↓ (NATS publish)
WedaCloudService.SubscribeCommandsAsync()
  ↓
DeviceConnectionManager.CommandReceived event
  ↓
DeviceBase (pre-hook → execute → post-hook)
  ↓
Device.ExecuteCommandAsync()
  ↓
┌─────────────────────────────────────────────────────┐
│ 1. 解析 command.Parameters 取得 name 和 state       │
│ 2. 查找 Sensor (by name) → 不存在則拒絕並報錯        │
│ 3. 驗證 SensorGroup == DO                          │
│ 4. 委派給 Parser 執行寫入                           │
└─────────────────────────────────────────────────────┘
  ↓
Parser.ExecuteCommandAsync() / PublishCommandAsync()
  ↓
  ├─ ModbusRequestResponseParser: FC 05 Write Single Coil
  └─ ISensingPubSubParser: MQTT publish to cmd topic
```

### Modbus FC 05 Request/Response

**Request (12 bytes)**:
```
[Transaction ID: 2 bytes]
[Protocol ID: 2 bytes (0x0000)]
[Length: 2 bytes (0x0006)]
[Unit ID: 1 byte]
[Function Code: 1 byte (0x05)]
[Coil Address: 2 bytes]
[Value: 2 bytes (0xFF00=ON, 0x0000=OFF)]
```

**Response (12 bytes)**: Echo of request (成功時)

---

## 實作項目

### Phase 1: Modbus DO Control

#### 1.1 ModbusRequestResponseParser - 實作 ExecuteCommandAsync

**檔案**: `src/Weda.SubNode.Core/Protocols/Modbus/ModbusRequestResponseParser.cs`

```csharp
public async Task<ErrorOr<object>> ExecuteCommandAsync(
    DeviceCommand command,
    CancellationToken cancellationToken = default)
{
    _logger.LogInformation("Executing Modbus command {CommandName}", command.DeviceCmd);

    try
    {
        return command.DeviceCmd switch
        {
            "SetDO" or "SetDigitalOutput" => await ExecuteSetDOAsync(command, cancellationToken),
            // Future: "SetAO", "SetRegister", etc.
            _ => Error.Validation("Command.NotSupported", $"Command '{command.DeviceCmd}' is not supported")
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error executing command {CommandName}", command.DeviceCmd);
        return Error.Failure("Command.ExecutionFailed", ex.Message);
    }
}

private async Task<ErrorOr<object>> ExecuteSetDOAsync(
    DeviceCommand command,
    CancellationToken cancellationToken)
{
    // 1. Extract parameters
    var name = ExtractOutputName(command.Parameters);
    if (string.IsNullOrEmpty(name))
        return Error.Validation("SetDO.MissingName", "Missing 'name', 'do', or 'outputName' parameter");

    if (!command.Parameters.TryGetValue("state", out var stateObj))
        return Error.Validation("SetDO.MissingState", "Missing 'state' parameter");

    var state = Convert.ToBoolean(stateObj);

    // 2. Find sensor in metadata
    if (!_sensorMetadata.TryGetValue(name, out var sensor))
        return Error.NotFound("SetDO.SensorNotFound", $"Sensor '{name}' not found in device configuration");

    // 3. Validate sensor type
    if (sensor.RegisterType != ModbusRegisterType.Coil)
        return Error.Validation("SetDO.InvalidRegisterType",
            $"Sensor '{name}' is not a Coil (RegisterType: {sensor.RegisterType})");

    // 4. Build and send Modbus request
    var request = BuildWriteSingleCoilRequest(sensor.RegisterAddress, state);
    var response = await _communication.RequestAsync(request, cancellationToken);

    // 5. Validate response
    ValidateWriteCoilResponse(response, sensor.RegisterAddress, state);

    _logger.LogInformation("SetDO succeeded: {Name} = {State}", name, state);
    return new { success = true, name, state };
}

private static string? ExtractOutputName(Dictionary<string, object> parameters)
{
    if (parameters.TryGetValue("name", out var name))
        return name?.ToString();
    if (parameters.TryGetValue("do", out var doName))
        return doName?.ToString();
    if (parameters.TryGetValue("outputName", out var outputName))
        return outputName?.ToString();
    return null;
}

private byte[] BuildWriteSingleCoilRequest(ushort coilAddress, bool state)
{
    var transactionId = ++_transactionId;
    var value = state ? (ushort)0xFF00 : (ushort)0x0000;

    return new byte[]
    {
        (byte)(transactionId >> 8), (byte)(transactionId & 0xFF),  // Transaction ID
        0x00, 0x00,                                                 // Protocol ID
        0x00, 0x06,                                                 // Length
        _slaveId,                                                   // Unit ID
        0x05,                                                       // Function Code (Write Single Coil)
        (byte)(coilAddress >> 8), (byte)(coilAddress & 0xFF),      // Coil Address
        (byte)(value >> 8), (byte)(value & 0xFF)                   // Value
    };
}

private void ValidateWriteCoilResponse(byte[] response, ushort expectedAddress, bool expectedState)
{
    if (response.Length < 12)
        throw new InvalidOperationException($"Invalid response length: {response.Length}");

    // Check function code (error if high bit set)
    if ((response[7] & 0x80) != 0)
    {
        var errorCode = response[8];
        throw new InvalidOperationException($"Modbus error: {GetModbusErrorDescription(errorCode)}");
    }

    // Verify echo
    var actualAddress = (ushort)((response[8] << 8) | response[9]);
    var actualValue = (ushort)((response[10] << 8) | response[11]);
    var expectedValue = expectedState ? 0xFF00 : 0x0000;

    if (actualAddress != expectedAddress || actualValue != expectedValue)
        throw new InvalidOperationException(
            $"Response mismatch: expected ({expectedAddress}, {expectedValue}), got ({actualAddress}, {actualValue})");
}

private static string GetModbusErrorDescription(byte errorCode) => errorCode switch
{
    0x01 => "Illegal Function",
    0x02 => "Illegal Data Address",
    0x03 => "Illegal Data Value",
    0x04 => "Slave Device Failure",
    _ => $"Unknown Error (0x{errorCode:X2})"
};
```

### Phase 2: ISensing DO Control

#### 2.1 ISensingDevice - 使用 ISensingPubSubParser

**選項 A (推薦)**: 修改 ISensingDevice 使用 ISensingPubSubParser

**檔案**: `src/Weda.SubNode.Core/Devices/ISensingDevice.cs`

```csharp
public class ISensingDevice : DeviceBase, ISensorControl
{
    private readonly IMessageBroker _messageBroker;
    private readonly ISensingPubSubParser _parser;  // 改用 PubSub Parser
    // ... existing fields ...

    public ISensingDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IMessageBroker messageBroker)
        : base(context, configuration, messageBroker)
    {
        _messageBroker = messageBroker;

        var macAddress = configuration.Communication.GetValueOrDefault("MacAddress")?.ToString()
            ?? throw new InvalidOperationException("MacAddress not found");
        var manufacturer = configuration.Communication.GetValueOrDefault("Manufacturer")?.ToString()
            ?? "Advantech";

        // 使用 ISensingPubSubParser
        _parser = new ISensingPubSubParser(
            messageBroker,
            macAddress,
            context.LoggerFactory.CreateLogger<ISensingPubSubParser>(),
            manufacturer);

        _dataTopic = $"{manufacturer}/{macAddress}/data";
        _statusTopic = $"{manufacturer}/{macAddress}/status";
    }

    public override async Task<bool> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandName} on ISensing device {DeviceId}",
            command.DeviceCmd, DeviceId);

        try
        {
            // Validate sensor exists for DO commands
            if (command.DeviceCmd is "SetDO" or "SetDigitalOutput")
            {
                var name = ExtractOutputName(command.Parameters);
                if (string.IsNullOrEmpty(name))
                {
                    _logger.LogError("Missing output name in SetDO command");
                    return false;
                }

                var sensor = Configuration.Sensors.FirstOrDefault(s =>
                    s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (sensor == null)
                {
                    _logger.LogError("Sensor '{Name}' not found in device configuration", name);
                    return false;
                }

                if (sensor.SensorGroup != SensorGroup.DO)
                {
                    _logger.LogError("Sensor '{Name}' is not a DO (SensorGroup: {Group})",
                        name, sensor.SensorGroup);
                    return false;
                }
            }

            // Delegate to parser
            await _parser.PublishCommandAsync(command, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command {CommandName}", command.DeviceCmd);
            return false;
        }
    }

    private static string? ExtractOutputName(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("name", out var name))
            return name?.ToString();
        if (parameters.TryGetValue("do", out var doName))
            return doName?.ToString();
        if (parameters.TryGetValue("outputName", out var outputName))
            return outputName?.ToString();
        return null;
    }

    // ... rest of existing code ...
}
```

### Phase 3: 共用工具與測試

#### 3.1 CommandParameterHelper (共用參數解析)

**檔案**: `src/Weda.SubNode.Core/Commands/CommandParameterHelper.cs` (新增)

```csharp
namespace Weda.SubNode.Core.Commands;

/// <summary>
/// Helper methods for extracting command parameters
/// </summary>
public static class CommandParameterHelper
{
    /// <summary>
    /// Extract output name from command parameters (supports multiple aliases)
    /// </summary>
    public static string? ExtractOutputName(Dictionary<string, object> parameters)
    {
        string[] aliases = ["name", "do", "outputName", "ao"];

        foreach (var alias in aliases)
        {
            if (parameters.TryGetValue(alias, out var value) && value != null)
                return value.ToString();
        }

        return null;
    }

    /// <summary>
    /// Extract boolean state from command parameters
    /// </summary>
    public static bool? ExtractState(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("state", out var value))
        {
            return value switch
            {
                bool b => b,
                int i => i != 0,
                string s => bool.TryParse(s, out var result) && result,
                _ => Convert.ToBoolean(value)
            };
        }
        return null;
    }
}
```

#### 3.2 單元測試

**檔案**: `tests/Weda.SubNode.Core.Tests/Commands/SetDOCommandTests.cs` (新增)

```csharp
public class SetDOCommandTests
{
    [Fact]
    public async Task ModbusSetDO_ValidSensor_ShouldSucceed()
    {
        // Arrange: Create ModbusDevice with DO sensor configured
        // Act: Execute SetDO command
        // Assert: Verify FC 05 request was sent correctly
    }

    [Fact]
    public async Task ModbusSetDO_SensorNotFound_ShouldReturnError()
    {
        // Arrange: Create ModbusDevice without the target sensor
        // Act: Execute SetDO command with non-existent sensor name
        // Assert: Verify error returned with "SensorNotFound" code
    }

    [Fact]
    public async Task ModbusSetDO_InvalidRegisterType_ShouldReturnError()
    {
        // Arrange: Create ModbusDevice with HoldingRegister sensor
        // Act: Execute SetDO command targeting that sensor
        // Assert: Verify error returned with "InvalidRegisterType" code
    }

    [Fact]
    public async Task ISensingSetDO_ValidSensor_ShouldPublishToMQTT()
    {
        // Arrange: Create ISensingDevice with DO sensor configured
        // Act: Execute SetDO command
        // Assert: Verify MQTT message published to cmd topic
    }

    [Fact]
    public async Task ISensingSetDO_SensorNotFound_ShouldReturnFalse()
    {
        // Arrange: Create ISensingDevice without the target sensor
        // Act: Execute SetDO command
        // Assert: Verify returns false and logs error
    }
}
```

---

## 修改檔案清單

| 檔案 | 動作 | 說明 |
|------|------|------|
| `Core/Protocols/Modbus/ModbusRequestResponseParser.cs` | 修改 | 實作 ExecuteCommandAsync (FC 05) |
| `Core/Devices/ISensingDevice.cs` | 修改 | 使用 ISensingPubSubParser，加入 sensor 驗證 |
| `Core/Commands/CommandParameterHelper.cs` | 新增 | 共用參數解析工具 |
| `Core.Tests/Commands/SetDOCommandTests.cs` | 新增 | 單元測試 |

---

## 錯誤處理

| 錯誤碼 | 說明 | HTTP Status |
|--------|------|-------------|
| `SetDO.MissingName` | 缺少 name/do/outputName 參數 | 400 |
| `SetDO.MissingState` | 缺少 state 參數 | 400 |
| `SetDO.SensorNotFound` | Sensor 不存在於配置中 | 404 |
| `SetDO.InvalidRegisterType` | Sensor 非 Coil 類型 (Modbus) | 400 |
| `SetDO.InvalidSensorGroup` | Sensor 非 DO 群組 (ISensing) | 400 |
| `Command.ExecutionFailed` | 執行失敗 (通訊錯誤等) | 500 |

---

## 測試案例

| # | 案例 | 輸入 | 預期結果 |
|---|------|------|----------|
| 1 | Modbus SetDO 成功 | name=do0, state=true | Coil 0 設為 ON |
| 2 | Modbus SetDO Sensor 不存在 | name=do99 | Error: SensorNotFound |
| 3 | Modbus SetDO 非 Coil 類型 | name=temp (HoldingRegister) | Error: InvalidRegisterType |
| 4 | ISensing SetDO 成功 | name=do0, state=false | MQTT publish 成功 |
| 5 | ISensing SetDO Sensor 不存在 | name=do99 | return false, log error |
| 6 | 參數別名測試 | do=do0 (而非 name) | 正常執行 |
| 7 | state 類型轉換 | state=1 (int) | 轉換為 true |

---

## 未來擴展

1. **SetAO (Analog Output)**: Modbus FC 06 (Write Single Register)
2. **批次寫入**: Modbus FC 15 (Write Multiple Coils) / FC 16 (Write Multiple Registers)
3. **Command Response**: 將執行結果回報給 Cloud (透過 RespTopic)
4. **Command 權限控制**: 限制哪些 DO 可被遠端控制
