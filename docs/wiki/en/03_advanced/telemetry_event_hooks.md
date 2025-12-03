# Telemetry Event Hooks

This document describes the event hooks available in the telemetry data flow pipeline, allowing you to monitor and debug data transformations at each stage.

## Data Flow Overview

```mermaid
flowchart TB
    subgraph Device["Device Hardware"]
        Sensor["Device Sensor"]
    end

    subgraph Read["ReadTelemetryAsync()"]
        ReadDesc["Raw sensor data from hardware"]
    end

    DataReceivedEvent["DataReceived Event<br/>───────────────────<br/>EnableDataReceivedTracking = true<br/>Contains: Raw telemetry measures"]

    subgraph Pipeline["Telemetry Pipeline"]
        subgraph TransformStage["Stage 1: Transform Pipeline"]
            T1["Transform 1<br/>(Calibration)"]
            T2["Transform 2<br/>(UnitConvert)"]
            TN["Transform N"]
            T1 --> T2 --> TN
        end

        TransformEvent["ValueChanged Event<br/>Stage = Transform<br/>───────────────────<br/>EnableValueChangeTracking = false<br/>Contains: InputValues, OutputValues"]

        subgraph FilterStage["Stage 2: DSP Filter Pipeline"]
            F1["Filter 1<br/>(MovingAvg)"]
            F2["Filter 2<br/>(Kalman)"]
            FN["Filter N"]
            F1 --> F2 --> FN
        end

        FilterEvent["ValueChanged Event<br/>Stage = Filter<br/>───────────────────<br/>EnableValueChangeTracking = false<br/>Contains: InputValues, OutputValues"]

        subgraph SendStage["Stage 3: Cloud Send"]
            SendDesc["Send to WEDA Cloud"]
        end
    end

    TelemetrySentEvent["TelemetrySent Event<br/>───────────────────<br/>EnableTelemetrySentTracking = true<br/>Contains: MeasureCount, Success"]

    Sensor --> Read
    Read --> DataReceivedEvent
    DataReceivedEvent --> TransformStage
    TransformStage --> TransformEvent
    TransformEvent --> FilterStage
    FilterStage --> FilterEvent
    FilterEvent --> SendStage
    SendStage --> TelemetrySentEvent

    style DataReceivedEvent fill:#e1f5fe,stroke:#0288d1
    style TransformEvent fill:#fff3e0,stroke:#ff9800
    style FilterEvent fill:#fff3e0,stroke:#ff9800
    style TelemetrySentEvent fill:#e8f5e9,stroke:#4caf50
```

## Other Device Events

```mermaid
flowchart LR
    subgraph Connection["Connection Events"]
        CSC["ConnectionStateChanged<br/>─────────────────<br/>EnableConnectionStateTracking = true<br/>Contains: PreviousState, CurrentState"]
    end

    subgraph Status["Status Events"]
        DSC["DeviceStatusChanged<br/>─────────────────<br/>EnableDeviceStatusTracking = true<br/>Contains: FromStatus, ToStatus"]
    end

    subgraph Cloud["Cloud Events"]
        CUR["ConfigurationUpdateReceived<br/>─────────────────<br/>EnableConfigurationUpdateTracking = true<br/>Contains: Config payload"]
        CR["CommandReceived<br/>─────────────────<br/>EnableCommandReceivedTracking = true<br/>Contains: Command details"]
    end

    style CSC fill:#e3f2fd,stroke:#1976d2
    style DSC fill:#fce4ec,stroke:#c2185b
    style CUR fill:#f3e5f5,stroke:#7b1fa2
    style CR fill:#f3e5f5,stroke:#7b1fa2
```

## Event Summary Table

| Event | Enable Flag | Default | Description |
|-------|-------------|---------|-------------|
| `DataReceived` | `EnableDataReceivedTracking` | `false` | Raw telemetry data from device |
| `ValueChanged` | `EnableValueChangeTracking` | `false` | Per-stage transform/filter values |
| `TelemetrySent` | `EnableTelemetrySentTracking` | `false` | Cloud send result |
| `ConnectionStateChanged` | `EnableConnectionStateTracking` | `false` | Device connection changes |
| `DeviceStatusChanged` | `EnableDeviceStatusTracking` | `false` | Device lifecycle status |
| `ConfigurationUpdateReceived` | `EnableConfigurationUpdateTracking` | `false` | Cloud config updates |
| `CommandReceived` | `EnableCommandReceivedTracking` | `false` | Cloud command execution |

> **Note:** All tracking flags default to `false` for better performance. Enable only the events you need.

## Usage Example

```csharp
public class MyDevice : TcpModbusDevice
{
    public MyDevice(IWedaApplicationContext context, DeviceConfiguration config)
        : base(context, config)
    {
        // Enable and subscribe to events you need
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;

        EnableValueChangeTracking = true;
        ValueChanged += OnValueChanged;

        EnableTelemetrySentTracking = true;
        TelemetrySent += OnTelemetrySent;

        // Optionally disable events you don't need
        // EnableConnectionStateTracking = false;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogInformation("Raw data: {Count} measures", e.Data.Count);
        foreach (var m in e.Data)
        {
            _logger.LogDebug("  {ResourceId}: {Value}", m.ResourceId, m.Value);
        }
    }

    private void OnValueChanged(object? sender, TelemetryValueChangedEvent e)
    {
        var stageType = e.Stage == ValueChangeStage.Transform ? "Transform" : "Filter";

        _logger.LogDebug(
            "[{StageType}] {StageName} (#{Index}): {In} -> {Out} values ({Duration:F2}ms)",
            stageType, e.StageName, e.StageIndex,
            e.InputValues.Count, e.OutputValues.Count,
            e.Duration?.TotalMilliseconds ?? 0);

        // Log individual value changes
        if (e.ValuesChanged)
        {
            for (int i = 0; i < Math.Max(e.InputValues.Count, e.OutputValues.Count); i++)
            {
                var input = i < e.InputValues.Count ? e.InputValues[i].Value?.ToString() : "(none)";
                var output = i < e.OutputValues.Count ? e.OutputValues[i].Value?.ToString() : "(filtered)";

                if (input != output)
                    _logger.LogDebug("  [{Index}] {Input} -> {Output}", i, input, output);
            }
        }
    }

    private void OnTelemetrySent(object? sender, TelemetrySentEvent e)
    {
        _logger.LogInformation("Sent {Count} measures, Success={Success}",
            e.MeasureCount, e.Success);
    }
}
```

## TelemetryValueChangedEvent Properties

| Property | Type | Description |
|----------|------|-------------|
| `DeviceId` | `string` | Device identifier |
| `ResourceId` | `string` | Sensor/resource identifier |
| `StageName` | `string` | Name of transform or filter |
| `Stage` | `ValueChangeStage` | `Transform` or `Filter` |
| `StageIndex` | `int` | Index in pipeline (0-based) |
| `InputValues` | `IReadOnlyList<TelemetryMeasure>` | Values before processing |
| `OutputValues` | `IReadOnlyList<TelemetryMeasure>` | Values after processing |
| `Duration` | `TimeSpan?` | Processing duration |
| `ValuesChanged` | `bool` | Whether values were modified |
| `CountDelta` | `int` | Output count - Input count |
| `Timestamp` | `DateTimeOffset` | Event timestamp |

## Performance Considerations

- **EnableValueChangeTracking** is `false` by default because it creates copies of telemetry data at each pipeline stage
- Enable only during debugging or when detailed monitoring is required
- In production, consider disabling events you don't need to reduce overhead