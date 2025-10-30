---
title: Telemetry Transform Pipeline
category: transform
order: 1
parent: null
related:
  - path: architecture_overview.md
    title: Architecture Overview
  - path: device_lifecycle.md
    title: Device Lifecycle
author: Rain Hu
version: 0.0.1
date: 2025-10-15
description: TelemetryTransformContext Usage Examples - Using context information in data transformation pipeline
tags: [Telemetry, Transform, Context, Data Processing]
---

# TelemetryTransformContext Usage Examples

## Why Do We Need Context?

`TelemetryTransformContext` provides **contextual information** required during Transform execution, allowing Transforms to make different processing decisions based on different scenarios.

### Three Core Fields of Context

1. **DeviceId** - Device identifier
2. **Timestamp** - Transformation timestamp
3. **Metadata** - Additional context data (dynamic key-value pairs)

---

## Real-World Usage Scenarios

### Scenario 1: Dynamic Calibration Based on Device Type

Different device models may require different calibration parameters.

```csharp
/// <summary>
/// Device-specific calibration transform
/// Uses DeviceId from context to determine calibration parameters
/// </summary>
public class DeviceSpecificCalibrationTransform : ITelemetryTransform
{
    private readonly Dictionary<string, (double Scale, double Offset)> _deviceCalibrations;

    public DeviceSpecificCalibrationTransform()
    {
        // Pre-defined calibration parameters for different devices
        _deviceCalibrations = new()
        {
            ["ADAM-6052-001"] = (Scale: 0.1, Offset: -5.0),
            ["ADAM-6052-002"] = (Scale: 0.12, Offset: -3.5),
            ["ADAM-6017-001"] = (Scale: 0.08, Offset: -2.0),
        };
    }

    public async Task<IReadOnlyList<TelemetryMeasure>> TransformAsync(
        IReadOnlyList<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Select corresponding calibration configuration based on DeviceId
        var (scale, offset) = _deviceCalibrations.TryGetValue(context.DeviceId, out var config)
            ? config
            : (Scale: 1.0, Offset: 0.0); // Default calibration if not found

        var transformed = measures.Select(m =>
        {
            if (m.Value is not double value)
                return m;

            // Apply linear calibration: y = mx + b
            var calibrated = value * scale + offset;

            return new TelemetryMeasure
            {
                ResourceId = m.ResourceId,
                Value = calibrated,
                Timestamp = m.Timestamp
            };
        }).ToList();

        return await Task.FromResult(transformed);
    }
}

// Usage example
var pipeline = TelemetryTransformPipeline.Create()
    .Add(new DeviceSpecificCalibrationTransform());

var context = new TelemetryTransformContext
{
    DeviceId = "ADAM-6052-001", // Apply different calibration based on device
    Timestamp = DateTimeOffset.UtcNow
};

var result = await pipeline.ExecuteAsync(measures, context);
```

---

### Scenario 2: Time-Related Data Processing

Some transformations need to know the current time, for example: different processing logic for day/night, timezone conversion, etc.

```csharp
/// <summary>
/// Time-aware temperature adjustment
/// Applies different corrections based on time of day
/// </summary>
public class TimeAwareTemperatureTransform : ITelemetryTransform
{
    public string Name => "TimeAwareTemperature";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Use Context's Timestamp to determine if it's day or night
        var localTime = context.Timestamp.ToLocalTime();
        var isDaytime = localTime.Hour >= 6 && localTime.Hour < 18;

        // Apply different temperature corrections for day and night
        var adjustment = isDaytime ? -2.0 : -1.0; // Sensor is hotter during day, needs larger correction

        var transformed = measures.Select(m =>
        {
            if (!m.ResourceId.Contains("temperature"))
                return m;

            if (m.ValueObject is not double and not int and not float)
                return m;

            var temperature = Convert.ToDouble(m.ValueObject);
            var adjusted = temperature + adjustment;

            return m with { ValueObject = adjusted };
        }).ToList();

        return Task.FromResult(transformed);
    }
}

// Usage example
var context = new TelemetryTransformContext
{
    DeviceId = "device-001",
    Timestamp = DateTimeOffset.Now // Determine how to process based on current time
};

var result = await pipeline.ExecuteAsync(measures, context);
```

---

### Scenario 3: Using Metadata to Pass Runtime Information

Metadata can pass arbitrary runtime information, such as: ambient temperature, device location, operation mode, etc.

```csharp
/// <summary>
/// Environment-compensated pressure transform
/// Adjusts pressure reading based on ambient temperature
/// </summary>
public class PressureCompensationTransform : ITelemetryTransform
{
    public string Name => "PressureCompensation";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Get ambient temperature from Metadata
        var ambientTemp = context.Metadata.TryGetValue("AmbientTemperature", out var temp)
            ? Convert.ToDouble(temp)
            : 25.0; // Default 25°C

        // Get device altitude from Metadata
        var altitude = context.Metadata.TryGetValue("Altitude", out var alt)
            ? Convert.ToDouble(alt)
            : 0.0; // Default sea level

        var transformed = measures.Select(m =>
        {
            if (!m.ResourceId.Contains("pressure"))
                return m;

            if (m.ValueObject is not double and not int and not float)
                return m;

            var pressure = Convert.ToDouble(m.ValueObject);

            // Temperature compensation: 0.37% per °C
            var tempCompensation = 1.0 + ((ambientTemp - 25.0) * 0.0037);

            // Altitude compensation: approximately 12 hPa per 100m
            var altitudeCompensation = altitude * 0.12;

            var compensated = (pressure * tempCompensation) - altitudeCompensation;

            return m with { ValueObject = compensated };
        }).ToList();

        return Task.FromResult(transformed);
    }
}

// Usage example
var context = new TelemetryTransformContext
{
    DeviceId = "pressure-sensor-001",
    Timestamp = DateTimeOffset.UtcNow,
    Metadata = new Dictionary<string, object>
    {
        ["AmbientTemperature"] = 28.5,  // Ambient temperature 28.5°C
        ["Altitude"] = 150.0,           // Altitude 150 meters
        ["Location"] = "Taipei",        // Location information
        ["OperatingMode"] = "Normal"    // Operation mode
    }
};

var result = await pipeline.ExecuteAsync(measures, context);
```

---

### Scenario 4: Cross-Sensor Data Correlation

Using Metadata to pass calculation results between Transforms.

```csharp
/// <summary>
/// Power calculation transform
/// Calculates power from voltage and current measurements
/// </summary>
public class PowerCalculationTransform : ITelemetryTransform
{
    public string Name => "PowerCalculation";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Find voltage and current from measures
        var voltage = measures
            .FirstOrDefault(m => m.ResourceId.Contains("voltage"))
            ?.ValueObject as double?;

        var current = measures
            .FirstOrDefault(m => m.ResourceId.Contains("current"))
            ?.ValueObject as double?;

        if (voltage.HasValue && current.HasValue)
        {
            // Calculate power: P = V × I
            var power = voltage.Value * current.Value;

            // Store calculation result in Metadata for subsequent Transforms
            context.Metadata["CalculatedPower"] = power;
            context.Metadata["PowerTimestamp"] = context.Timestamp;

            // Add a power measure
            var powerMeasure = new TelemetryMeasure
            {
                ResourceId = $"{context.DeviceId}-power",
                ValueObject = power,
                Timestamp = context.Timestamp
            };

            return Task.FromResult(measures.Append(powerMeasure).ToList());
        }

        return Task.FromResult(measures);
    }
}

/// <summary>
/// Energy accumulation transform
/// Uses calculated power from context to accumulate energy
/// </summary>
public class EnergyAccumulationTransform : ITelemetryTransform
{
    private double _accumulatedEnergy = 0.0;
    private DateTimeOffset _lastTimestamp = DateTimeOffset.MinValue;

    public string Name => "EnergyAccumulation";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Get power calculated by previous Transform from Metadata
        if (context.Metadata.TryGetValue("CalculatedPower", out var powerObj))
        {
            var power = Convert.ToDouble(powerObj);
            var timeDiff = (context.Timestamp - _lastTimestamp).TotalHours;

            if (_lastTimestamp != DateTimeOffset.MinValue && timeDiff > 0)
            {
                // Accumulate energy: E = P × t (Wh)
                _accumulatedEnergy += power * timeDiff;

                // Add energy accumulation measure
                var energyMeasure = new TelemetryMeasure
                {
                    ResourceId = $"{context.DeviceId}-energy",
                    ValueObject = _accumulatedEnergy,
                    Timestamp = context.Timestamp
                };

                measures = measures.Append(energyMeasure).ToList();
            }

            _lastTimestamp = context.Timestamp;
        }

        return Task.FromResult(measures);
    }
}

// Usage example
var pipeline = TelemetryTransformPipeline.Create()
    .Add(new PowerCalculationTransform())       // Calculate power and store in Context
    .Add(new EnergyAccumulationTransform());    // Read power from Context and accumulate energy

var measures = new List<TelemetryMeasure>
{
    new() { ResourceId = "voltage-001", ValueObject = 220.0 },
    new() { ResourceId = "current-001", ValueObject = 5.0 }
};

var context = new TelemetryTransformContext
{
    DeviceId = "power-meter-001",
    Timestamp = DateTimeOffset.UtcNow
};

var result = await pipeline.ExecuteAsync(measures, context);
// result will contain: voltage, current, power (220 × 5 = 1100W), energy (accumulated value)
```

---

### Scenario 5: Conditional Transformation (Based on DeviceId and Metadata)

```csharp
/// <summary>
/// Conditional smoothing transform
/// Applies smoothing only for specific devices or conditions
/// </summary>
public class ConditionalSmoothingTransform : ITelemetryTransform
{
    private readonly Dictionary<string, Queue<double>> _history = new();
    private const int WindowSize = 5;

    public string Name => "ConditionalSmoothing";

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Condition 1: Apply smoothing only for specific devices
        var enabledDevices = new[] { "noisy-sensor-001", "noisy-sensor-002" };
        if (!enabledDevices.Contains(context.DeviceId))
        {
            return Task.FromResult(measures);
        }

        // Condition 2: Apply smoothing only in high noise mode
        var isHighNoiseMode = context.Metadata.TryGetValue("NoiseLevel", out var noise)
            && noise.ToString() == "High";

        if (!isHighNoiseMode)
        {
            return Task.FromResult(measures);
        }

        // Apply moving average smoothing
        var transformed = measures.Select(m =>
        {
            if (m.ValueObject is not double and not int and not float)
                return m;

            var value = Convert.ToDouble(m.ValueObject);
            var key = m.ResourceId;

            if (!_history.ContainsKey(key))
            {
                _history[key] = new Queue<double>();
            }

            var queue = _history[key];
            queue.Enqueue(value);

            if (queue.Count > WindowSize)
            {
                queue.Dequeue();
            }

            var smoothed = queue.Average();
            return m with { ValueObject = smoothed };
        }).ToList();

        return Task.FromResult(transformed);
    }
}

// Usage example
var context = new TelemetryTransformContext
{
    DeviceId = "noisy-sensor-001",
    Timestamp = DateTimeOffset.UtcNow,
    Metadata = new Dictionary<string, object>
    {
        ["NoiseLevel"] = "High",    // Trigger conditional smoothing
        ["Environment"] = "Factory"
    }
};

var result = await pipeline.ExecuteAsync(measures, context);
```

---

## Complete Real-World Application Example

### Scenario: Factory Environment Monitoring System

```csharp
public class FactoryMonitoringService
{
    private readonly TelemetryTransformPipeline _pipeline;
    private readonly IWeatherService _weatherService;
    private readonly IDeviceLocationService _locationService;

    public FactoryMonitoringService(
        IWeatherService weatherService,
        IDeviceLocationService locationService)
    {
        _weatherService = weatherService;
        _locationService = locationService;

        // Build Transform Pipeline
        _pipeline = TelemetryTransformPipeline.Create()
            .Add(new DeviceSpecificCalibrationTransform())
            .Add(new PressureCompensationTransform())
            .Add(new PowerCalculationTransform())
            .Add(new EnergyAccumulationTransform())
            .Add(new ConditionalSmoothingTransform());
    }

    public async Task<List<TelemetryMeasure>> ProcessTelemetryAsync(
        string deviceId,
        List<TelemetryMeasure> rawMeasures,
        CancellationToken cancellationToken = default)
    {
        // Get device location information
        var location = await _locationService.GetLocationAsync(deviceId);

        // Get current weather information
        var weather = await _weatherService.GetWeatherAsync(location);

        // Build rich Context
        var context = new TelemetryTransformContext
        {
            DeviceId = deviceId,
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                // Environment information
                ["AmbientTemperature"] = weather.Temperature,
                ["Humidity"] = weather.Humidity,
                ["Pressure"] = weather.Pressure,

                // Location information
                ["Altitude"] = location.Altitude,
                ["Location"] = location.Name,
                ["Latitude"] = location.Latitude,
                ["Longitude"] = location.Longitude,

                // Operation information
                ["OperatingMode"] = "Production",
                ["ShiftNumber"] = 1,
                ["NoiseLevel"] = "High",

                // Other context
                ["ProcessedBy"] = "FactoryMonitoringService",
                ["Version"] = "1.0.0"
            }
        };

        // Execute Transform Pipeline
        var processedMeasures = await _pipeline.ExecuteAsync(
            rawMeasures,
            context,
            cancellationToken);

        return processedMeasures;
    }
}
```

---

## Summary

### Core Value of Context

1. **DeviceId**
   - Identify data source
   - Apply device-specific processing logic
   - Support multi-tenant scenarios

2. **Timestamp**
   - Time-related processing decisions
   - Data time-series analysis
   - Timezone handling

3. **Metadata (Most Flexible)**
   - Pass arbitrary runtime information
   - Share calculation results between Transforms
   - Basis for conditional processing
   - Environmental context information

### Design Principles

- **Immutability**: Transforms should not modify Context (only read or add to Metadata)
- **Optionality**: Transforms should be able to use reasonable defaults when Context information is missing
- **Extensibility**: Metadata uses `Dictionary<string, object>` to provide unlimited expansion capability
- **Transitivity**: Context is passed through the entire Pipeline, allowing Transforms to collaborate

### When to Use Context

**Should Use**:
- Need to know data source (DeviceId)
- Need time information (Timestamp)
- Need environment or runtime information (Metadata)
- Need to share data between Transforms

**Don't Need to Use**:
- Simple mathematical operations (like linear calibration)
- Stateless data transformations
- Processing that doesn't depend on external information

Context provides powerful flexibility, allowing Transforms to make intelligent decisions based on actual scenarios!
