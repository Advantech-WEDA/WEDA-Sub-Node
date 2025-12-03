# 配置更新驗證

當雲端發送配置更新到 SubNode 設備時，SDK 會在套用變更前執行驗證。本文件說明如何為您的設備自訂驗證行為。

## 概覽

配置更新驗證系統提供：
- **更新模式** - Replace（完整替換，預設）或 Patch（部分更新）
- **預設驗證** - 週期、感測器和閾值的標準檢查
- **ConfigUpdateOptions** - 細粒度控制啟用哪些驗證
- **自訂驗證** - 覆寫驗證方法以實現設備特定邏輯

## 更新模式

SDK 支援兩種更新模式：

| 模式 | 說明 | 適用場景 |
|------|------|----------|
| **Replace** (預設) | 完整替換配置，要求提供完整的 payload | Shadow 系統、需要完整狀態同步的場景 |
| **Patch** | 只更新提供的欄位，未提供的欄位保持原值 | 允許部分更新的場景 |

```csharp
public enum ConfigUpdateMode
{
    Replace,  // 完整替換 - 要求完整 payload（預設）
    Patch     // 部分更新 - 只更新提供的欄位
}
```

## ConfigUpdateOptions

`ConfigUpdateOptions` 是一個控制驗證行為的 record：

```csharp
public record ConfigUpdateOptions
{
    // 預設：Replace 模式，要求完整 payload（Shadow 相容）
    public static ConfigUpdateOptions Default => new();

    // 寬鬆模式：Patch 模式，允許部分更新
    public static ConfigUpdateOptions Relaxed => new()
    {
        UpdateMode = ConfigUpdateMode.Patch,
        RejectUnknownSensors = false,
        RequireAllSensors = false
    };

    // 更新模式（預設：Replace）
    public ConfigUpdateMode UpdateMode { get; init; } = ConfigUpdateMode.Replace;

    // 驗證 DeviceName 是否匹配（預設：true）
    public bool ValidateDeviceName { get; init; } = true;

    // 驗證週期值是否非負（預設：true）
    public bool ValidatePeriods { get; init; } = true;

    // 驗證感測器配置（預設：true）
    public bool ValidateSensors { get; init; } = true;

    // 驗證閾值一致性（預設：true）
    public bool ValidateThresholds { get; init; } = true;

    // 拒絕更新中的未知感測器（預設：true）
    public bool RejectUnknownSensors { get; init; } = true;

    // 要求更新中包含所有現有感測器（預設：true）
    public bool RequireAllSensors { get; init; } = true;
}
```

### 選項詳細說明

| 選項 | 預設值 | 說明 |
|------|--------|------|
| `UpdateMode` | `Replace` | 更新模式：Replace（完整替換）或 Patch（部分更新） |
| `ValidateDeviceName` | `true` | 確保更新中的 DeviceName 與設備匹配 |
| `ValidatePeriods` | `true` | 檢查 ReadTelemetry、SendTelemetry、ReportHealth >= 0 |
| `ValidateSensors` | `true` | 驗證感測器名稱不為空、interval >= 0 |
| `ValidateThresholds` | `true` | 驗證 UpperCritical >= UpperWarning >= LowerWarning >= LowerCritical |
| `RejectUnknownSensors` | `true` | 拒絕不在目前配置中的感測器 |
| `RequireAllSensors` | `true` | 所有現有感測器都必須在更新中 |

### 預設模式 vs 寬鬆模式

```
┌─────────────────────────────────────────────────────────────────┐
│                    ConfigUpdateOptions.Default                   │
│                       (Replace 模式)                             │
├─────────────────────────────────────────────────────────────────┤
│  • 要求完整 payload                                              │
│  • 未知感測器會導致驗證失敗                                       │
│  • 所有現有感測器都必須在更新中                                   │
│  • 適合 Shadow 系統、需要完整狀態同步的場景                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    ConfigUpdateOptions.Relaxed                   │
│                        (Patch 模式)                              │
├─────────────────────────────────────────────────────────────────┤
│  • 允許部分更新                                                   │
│  • 未知感測器會被忽略                                             │
│  • 可以只更新部分感測器                                           │
│  • 適合需要彈性部分更新的場景                                     │
└─────────────────────────────────────────────────────────────────┘
```

## 使用模式

### 方式 1：使用預設選項

```csharp
// Replace 模式（預設）- 要求完整 payload（Shadow 相容）
protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default;

// Relaxed 模式 - 允許部分更新
protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Relaxed;
```

### 方式 2：自訂選項

```csharp
public class MyCustomDevice : TcpModbusDevice
{
    public MyCustomDevice(IWedaApplicationContext context)
        : base(context) { }

    // 自訂驗證選項：使用 Patch 模式但保留其他驗證
    protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default with
    {
        UpdateMode = ConfigUpdateMode.Patch,
        RequireAllSensors = false
    };
}
```

### 方式 3：覆寫 ValidateConfigurationUpdate 方法

對於複雜的驗證邏輯，覆寫驗證方法：

```csharp
public class MyCustomDevice : TcpModbusDevice
{
    public MyCustomDevice(IWedaApplicationContext context)
        : base(context) { }

    protected override ConfigurationValidationResult ValidateConfigurationUpdate(
        SubNodeConfigurationUpdateMessage message)
    {
        // 先呼叫基礎驗證
        var baseResult = base.ValidateConfigurationUpdate(message);
        if (!baseResult.IsValid)
            return baseResult;

        // 加入自訂驗證
        var desiredConfig = message.Data?.Cfg?.Desired?
            .SubNodeDeviceConfig?.DeviceConfigs?.Values.FirstOrDefault();

        // 範例：要求特定的通訊欄位
        if (desiredConfig?.Communication?.ContainsKey("SlaveId") != true)
            return ConfigurationValidationResult.Failure("Communication 中必須包含 SlaveId");

        // 範例：驗證自訂業務規則
        if (desiredConfig?.Periods?.ReadTelemetry > 60000)
            return ConfigurationValidationResult.Failure("ReadTelemetry 必須 <= 60000ms");

        return ConfigurationValidationResult.Success;
    }
}
```

### 方式 4：完全跳過基礎驗證

要完全控制驗證，從頭實作：

```csharp
public class MyCustomDevice : TcpModbusDevice
{
    protected override ConfigurationValidationResult ValidateConfigurationUpdate(
        SubNodeConfigurationUpdateMessage message)
    {
        // 跳過基礎驗證，只實作自訂邏輯
        if (message?.Data?.Cfg?.Desired == null)
            return ConfigurationValidationResult.Failure("無效的訊息結構");

        // 您的自訂驗證邏輯
        // ...

        return ConfigurationValidationResult.Success;
    }
}
```

## ConfigurationValidationResult

驗證結果是一個簡單的 record：

```csharp
public record ConfigurationValidationResult(
    bool IsValid,
    string? ErrorMessage = null)
{
    public static ConfigurationValidationResult Success => new(true);
    public static ConfigurationValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
```

## 驗證流程

當收到配置更新時：

1. **Pre-hook** - 呼叫 `OnBeforeConfigUpdateAsync()`
2. **驗證** - 呼叫 `ValidateConfigurationUpdate()`
3. **如果無效** - 發送「invalid」狀態到雲端，停止處理
4. **如果有效** - 發送「updating」狀態，套用變更，發送「success」狀態
5. **Post-hook** - 呼叫 `OnAfterConfigUpdateAsync()`

```
雲端更新 → Pre-hook → 驗證 → [無效?] → 發送 "invalid" → 停止
                            ↓
                       [有效?] → 發送 "updating" → 套用 → 發送 "success" → Post-hook
```

## 範例

### Shadow 相容模式（預設）

```csharp
public class ShadowCompatibleDevice : TcpModbusDevice
{
    // 使用 Default：要求完整 payload
    protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default;
}
```

### 部分更新模式

```csharp
public class FlexibleDevice : TcpModbusDevice
{
    // 使用 Relaxed：允許部分更新
    protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Relaxed;
}
```

### 開發環境寬鬆驗證

```csharp
protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Relaxed with
{
    ValidateThresholds = false,   // 跳過閾值檢查
    ValidatePeriods = false       // 跳過週期檢查
};
```

### 帶業務規則的工業設備

```csharp
public class IndustrialSensorDevice : TcpModbusDevice
{
    // 使用預設 Replace 模式 + 自訂驗證
    protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default;

    protected override ConfigurationValidationResult ValidateConfigurationUpdate(
        SubNodeConfigurationUpdateMessage message)
    {
        var baseResult = base.ValidateConfigurationUpdate(message);
        if (!baseResult.IsValid)
            return baseResult;

        var config = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs?.Values.FirstOrDefault();

        // 工業安全：最小輪詢間隔
        if (config?.Periods?.ReadTelemetry < 100)
            return ConfigurationValidationResult.Failure(
                "ReadTelemetry 必須 >= 100ms 以確保工業安全");

        // 所有感測器必須定義閾值
        if (config?.Sensors?.Any(s => s.Config?.Thresholds == null) == true)
            return ConfigurationValidationResult.Failure(
                "所有感測器必須定義閾值");

        return ConfigurationValidationResult.Success;
    }
}
```
