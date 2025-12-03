# Configuration Update Validation

When cloud sends configuration updates to SubNode devices, the SDK performs validation before applying changes. This document explains how to customize validation behavior for your devices.

## Overview

The configuration update validation system provides:
- **Update modes** - Replace (complete payload, default) or Patch (partial updates)
- **Default validation** - Standard checks for periods, sensors, and thresholds
- **ConfigUpdateOptions** - Fine-grained control over which validations are enabled
- **Custom validation** - Override the validation method for device-specific logic

## Update Modes

SDK supports two update modes:

| Mode | Description | Use Case |
|------|-------------|----------|
| **Replace** (default) | Replace entire configuration with provided payload | Shadow systems, complete state synchronization |
| **Patch** | Only update provided fields, retain others | Partial update scenarios |

```csharp
public enum ConfigUpdateMode
{
    Replace,  // Complete replacement - requires full payload (default)
    Patch     // Partial update - only update provided fields
}
```

## ConfigUpdateOptions

`ConfigUpdateOptions` is a record that controls validation behavior:

```csharp
public record ConfigUpdateOptions
{
    // Default: Replace mode, requires complete payload (Shadow-compatible)
    public static ConfigUpdateOptions Default => new();

    // Relaxed: Patch mode, allows partial updates
    public static ConfigUpdateOptions Relaxed => new()
    {
        UpdateMode = ConfigUpdateMode.Patch,
        RejectUnknownSensors = false,
        RequireAllSensors = false
    };

    // Update mode (default: Replace)
    public ConfigUpdateMode UpdateMode { get; init; } = ConfigUpdateMode.Replace;

    // Validate DeviceName matches (default: true)
    public bool ValidateDeviceName { get; init; } = true;

    // Validate period values are non-negative (default: true)
    public bool ValidatePeriods { get; init; } = true;

    // Validate sensor configurations (default: true)
    public bool ValidateSensors { get; init; } = true;

    // Validate threshold consistency (default: true)
    public bool ValidateThresholds { get; init; } = true;

    // Reject unknown sensors in update (default: true)
    public bool RejectUnknownSensors { get; init; } = true;

    // Require all existing sensors in update (default: true)
    public bool RequireAllSensors { get; init; } = true;
}
```

### Option Details

| Option | Default | Description |
|--------|---------|-------------|
| `UpdateMode` | `Replace` | Update mode: Replace (complete) or Patch (partial) |
| `ValidateDeviceName` | `true` | Ensures DeviceName in update matches the device |
| `ValidatePeriods` | `true` | Checks ReadTelemetry, SendTelemetry, ReportHealth >= 0 |
| `ValidateSensors` | `true` | Validates sensor name not empty, interval >= 0 |
| `ValidateThresholds` | `true` | Validates UpperCritical >= UpperWarning >= LowerWarning >= LowerCritical |
| `RejectUnknownSensors` | `true` | Rejects sensors not in current config |
| `RequireAllSensors` | `true` | All existing sensors must be in update |

### Default Mode vs Relaxed Mode

```
┌─────────────────────────────────────────────────────────────────┐
│                    ConfigUpdateOptions.Default                   │
│                        (Replace Mode)                            │
├─────────────────────────────────────────────────────────────────┤
│  • Requires complete payload                                     │
│  • Unknown sensors cause validation failure                      │
│  • All existing sensors must be present                          │
│  • Best for Shadow systems, complete state sync                  │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    ConfigUpdateOptions.Relaxed                   │
│                         (Patch Mode)                             │
├─────────────────────────────────────────────────────────────────┤
│  • Allows partial updates                                        │
│  • Unknown sensors are ignored                                   │
│  • Can update only some sensors                                  │
│  • Best for flexible partial update scenarios                    │
└─────────────────────────────────────────────────────────────────┘
```

## Usage Patterns

### Method 1: Use Preset Options

```csharp
// Replace mode (default) - requires complete payload (Shadow-compatible)
protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default;

// Relaxed mode - allows partial updates
protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Relaxed;
```

### Method 2: Custom Options

```csharp
public class MyCustomDevice : TcpModbusDevice
{
    public MyCustomDevice(IWedaApplicationContext context)
        : base(context) { }

    // Custom options: Use Patch mode but keep other validations
    protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default with
    {
        UpdateMode = ConfigUpdateMode.Patch,
        RequireAllSensors = false
    };
}
```

### Method 3: Override ValidateConfigurationUpdate Method

For complex validation logic, override the validation method:

```csharp
public class MyCustomDevice : TcpModbusDevice
{
    public MyCustomDevice(IWedaApplicationContext context)
        : base(context) { }

    protected override ConfigurationValidationResult ValidateConfigurationUpdate(
        SubNodeConfigurationUpdateMessage message)
    {
        // Call base validation first
        var baseResult = base.ValidateConfigurationUpdate(message);
        if (!baseResult.IsValid)
            return baseResult;

        // Add custom validation
        var desiredConfig = message.Data?.Cfg?.Desired?
            .SubNodeDeviceConfig?.DeviceConfigs?.Values.FirstOrDefault();

        // Example: Require specific communication field
        if (desiredConfig?.Communication?.ContainsKey("SlaveId") != true)
            return ConfigurationValidationResult.Failure("SlaveId is required in Communication");

        // Example: Validate custom business rules
        if (desiredConfig?.Periods?.ReadTelemetry > 60000)
            return ConfigurationValidationResult.Failure("ReadTelemetry must be <= 60000ms");

        return ConfigurationValidationResult.Success;
    }
}
```

### Method 4: Skip Base Validation Entirely

For complete control, implement validation from scratch:

```csharp
public class MyCustomDevice : TcpModbusDevice
{
    protected override ConfigurationValidationResult ValidateConfigurationUpdate(
        SubNodeConfigurationUpdateMessage message)
    {
        // Skip base validation, implement custom logic only
        if (message?.Data?.Cfg?.Desired == null)
            return ConfigurationValidationResult.Failure("Invalid message structure");

        // Your custom validation logic here
        // ...

        return ConfigurationValidationResult.Success;
    }
}
```

## ConfigurationValidationResult

The validation result is a simple record:

```csharp
public record ConfigurationValidationResult(
    bool IsValid,
    string? ErrorMessage = null)
{
    public static ConfigurationValidationResult Success => new(true);
    public static ConfigurationValidationResult Failure(string errorMessage) => new(false, errorMessage);
}
```

## Validation Flow

When a configuration update is received:

1. **Pre-hook** - `OnBeforeConfigUpdateAsync()` is called
2. **Validation** - `ValidateConfigurationUpdate()` is called
3. **If invalid** - Send "invalid" status to cloud, stop processing
4. **If valid** - Send "updating" status, apply changes, send "success" status
5. **Post-hook** - `OnAfterConfigUpdateAsync()` is called

```
Cloud Update → Pre-hook → Validate → [Invalid?] → Send "invalid" → Stop
                                   ↓
                              [Valid?] → Send "updating" → Apply → Send "success" → Post-hook
```

## Examples

### Shadow Compatible Mode (Default)

```csharp
public class ShadowCompatibleDevice : TcpModbusDevice
{
    // Use Default: requires complete payload
    protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default;
}
```

### Partial Update Mode

```csharp
public class FlexibleDevice : TcpModbusDevice
{
    // Use Relaxed: allows partial updates
    protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Relaxed;
}
```

### Development Relaxed Validation

```csharp
protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Relaxed with
{
    ValidateThresholds = false,   // Skip threshold checks
    ValidatePeriods = false       // Skip period checks
};
```

### Industrial Device with Business Rules

```csharp
public class IndustrialSensorDevice : TcpModbusDevice
{
    // Use default Replace mode + custom validation
    protected override ConfigUpdateOptions ConfigUpdateOptions => ConfigUpdateOptions.Default;

    protected override ConfigurationValidationResult ValidateConfigurationUpdate(
        SubNodeConfigurationUpdateMessage message)
    {
        var baseResult = base.ValidateConfigurationUpdate(message);
        if (!baseResult.IsValid)
            return baseResult;

        var config = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs?.Values.FirstOrDefault();

        // Industrial safety: minimum polling interval
        if (config?.Periods?.ReadTelemetry < 100)
            return ConfigurationValidationResult.Failure(
                "ReadTelemetry must be >= 100ms for industrial safety");

        // All sensors must have thresholds defined
        if (config?.Sensors?.Any(s => s.Config?.Thresholds == null) == true)
            return ConfigurationValidationResult.Failure(
                "All sensors must have thresholds defined");

        return ConfigurationValidationResult.Success;
    }
}
```
