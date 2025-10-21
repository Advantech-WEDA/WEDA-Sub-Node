# How to Add Transformation

Data transformation allows you to process, convert, and enrich telemetry data before it's sent to the cloud. This guide shows you how to add and configure transformations in your device.

## Overview

Transformations in Weda SubNode SDK:
- Process data in a pipeline (chain multiple transforms)
- Convert units (e.g., Celsius to Fahrenheit)
- Apply calibration and scaling
- Enrich data with context
- Filter or aggregate values

## Built-in Transformations

The SDK includes several ready-to-use transformations:

### 1. Unit Conversion Transform

Converts between different units of measurement.

```csharp
using Weda.SubNode.Core.Transforms;

var transform = new UnitConversionTransform(
    fromUnit: "celsius",
    toUnit: "fahrenheit"
);
```

**Supported Units:**
- Temperature: `celsius`, `fahrenheit`, `kelvin`
- Pressure: `pa`, `kpa`, `bar`, `psi`
- Length: `meter`, `cm`, `mm`, `inch`, `foot`
- (Add more as needed)

### 2. Calibration Transform

Applies linear calibration (y = mx + b):

```csharp
using Weda.SubNode.Core.Transforms;

var transform = new CalibrationTransform(
    scale: 1.05,    // m (slope)
    offset: -2.5    // b (intercept)
);

// Example: raw value = 100
// calibrated value = 100 * 1.05 - 2.5 = 102.5
```

**Use Cases:**
- Sensor calibration adjustments
- Offset correction
- Scaling raw ADC values to physical units

## Adding Transformations to Your Device

### Method 1: Configuration-Based (Recommended)

Define transformations in `appsettings.json`:

```json
{
  "DeviceConfigs": {
    "MyDevice": {
      "Sensors": [
        {
          "ResourceId": "temp",
          "Name": "Temperature",
          "RegisterAddress": 0,
          "DataType": "Float32",
          "Config": {
            "Enabled": true,
            "Interval": 1000,
            "TransformPipeline": [
              {
                "Type": "Calibration",
                "Enabled": true,
                "Order": 0,
                "Parameters": {
                  "Scale": 1.0,
                  "Offset": -5.0
                }
              },
              {
                "Type": "UnitConversion",
                "Enabled": true,
                "Order": 1,
                "Parameters": {
                  "FromUnit": "C",
                  "ToUnit": "F"
                }
              }
            ]
          }
        }
      ]
    }
  }
}
```

The SDK automatically loads and applies these transforms in order based on the `Order` property.

### Method 2: Programmatic Configuration

Add transforms in your device code:

```csharp
using Weda.SubNode.Core.Transforms;
using Weda.SubNode.Abstractions.Telemetry;

public class MyFirstDevice : ModbusDevice
{
    public MyFirstDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        ICommunication communication)
        : base(context, configuration, communication)
    {
        // Add transformations programmatically
        ConfigureTransformations();
    }

    private void ConfigureTransformations()
    {
        // Create transform pipeline
        var pipeline = TelemetryTransformPipeline.Create()
            .Add(new CalibrationTransform(scale: 1.05, offset: -2.5))
            .Add(new UnitConversionTransform("temp", "C", "F"));

        // Store pipeline for later use in SendTelemetryAsync
        // (Pipeline will be applied when sending telemetry)
    }
}
```

### Method 3: Curve-Based Calibration

For non-linear sensors, use calibration curves:

```csharp
using Weda.SubNode.Core.Transforms;

private void ConfigureCalibrationCurve()
{
    // Define calibration points (measured raw value -> known calibrated value)
    var calibrationCurve = new List<CalibrationPoint>
    {
        new() { RawValue = 0, CalibratedValue = 0 },
        new() { RawValue = 50, CalibratedValue = 60 },
        new() { RawValue = 100, CalibratedValue = 110 },
        new() { RawValue = 200, CalibratedValue = 215 }
    };

    // Create curve-based calibration transform
    var pipeline = TelemetryTransformPipeline.Create()
        .Add(new CalibrationTransform(calibrationCurve));

    // Values between points are linearly interpolated
    // Example: RawValue=25 -> CalibratedValue=30 (interpolated between 0 and 50)
}
```

## Creating Custom Transformations

### Step 1: Implement ITelemetryTransform

```csharp
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Abstractions.Telemetry;

public class MovingAverageTransform : ITelemetryTransform
{
    private readonly int _windowSize;
    private readonly Dictionary<string, Queue<double>> _windows = new();

    public MovingAverageTransform(int windowSize)
    {
        _windowSize = windowSize;
    }

    public async Task<IReadOnlyList<TelemetryMeasure>> TransformAsync(
        IReadOnlyList<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TelemetryMeasure>();

        foreach (var measure in measures)
        {
            if (measure.Value is not double value)
            {
                results.Add(measure);
                continue;
            }

            // Get or create window for this resource
            if (!_windows.ContainsKey(measure.ResourceId))
                _windows[measure.ResourceId] = new Queue<double>();

            var window = _windows[measure.ResourceId];

            // Add new value to window
            window.Enqueue(value);

            // Remove old values if window is full
            while (window.Count > _windowSize)
                window.Dequeue();

            // Calculate average
            var average = window.Average();

            // Create new measure with averaged value
            results.Add(new TelemetryMeasure
            {
                ResourceId = measure.ResourceId,
                Value = average,
                Timestamp = measure.Timestamp
            });
        }

        return await Task.FromResult(results);
    }
}
```

### Step 2: Use Your Custom Transform

```csharp
// Add to pipeline
var pipeline = TelemetryTransformPipeline.Create()
    .Add(new MovingAverageTransform(windowSize: 5));
```

## Advanced Transformation Scenarios

### Scenario 1: Multi-Sensor Aggregation

Combine data from multiple sensors to create derived values:

```csharp
public class HeatIndexTransform : ITelemetryTransform
{
    private readonly Dictionary<string, double> _cache = new();

    public async Task<IReadOnlyList<TelemetryMeasure>> TransformAsync(
        IReadOnlyList<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TelemetryMeasure>(measures);

        // Update cache with latest values
        foreach (var measure in measures)
        {
            if (measure.Value is double value)
                _cache[measure.ResourceId] = value;
        }

        // Calculate heat index if we have both temp and humidity
        if (_cache.ContainsKey("temp") && _cache.ContainsKey("humidity"))
        {
            var temp = _cache["temp"];
            var humidity = _cache["humidity"];
            var heatIndex = CalculateHeatIndex(temp, humidity);

            results.Add(new TelemetryMeasure
            {
                ResourceId = "heat_index",
                Value = heatIndex,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        return await Task.FromResult(results);
    }

    private double CalculateHeatIndex(double tempC, double humidity)
    {
        // Convert to Fahrenheit for calculation
        var tempF = tempC * 9.0 / 5.0 + 32.0;

        // Simplified heat index formula (Rothfusz regression)
        var hi = -42.379
            + 2.04901523 * tempF
            + 10.14333127 * humidity
            - 0.22475541 * tempF * humidity;

        // Convert back to Celsius
        return (hi - 32.0) * 5.0 / 9.0;
    }
}
```

### Scenario 2: Context-Aware Transformation

Use device context and metadata in transformations:

```csharp
public class LocationEnrichmentTransform : ITelemetryTransform
{
    public async Task<IReadOnlyList<TelemetryMeasure>> TransformAsync(
        IReadOnlyList<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        // Context contains DeviceId, Timestamp, and custom Metadata
        var deviceLocation = context.Metadata.GetValueOrDefault("Location")?.ToString();

        var results = measures.Select(m => new TelemetryMeasure
        {
            ResourceId = m.ResourceId,
            Value = m.Value,
            Timestamp = m.Timestamp,
            // Add metadata (implementation-specific)
            // Properties = new Dictionary<string, object>
            // {
            //     ["DeviceId"] = context.DeviceId,
            //     ["Location"] = deviceLocation
            // }
        }).ToList();

        return await Task.FromResult(results);
    }
}
```

## Transformation Pipeline Configuration

### Chaining Multiple Transforms

Transforms are applied in order:

```csharp
var pipeline = TelemetryTransformPipeline.Create()
    .Add(new CalibrationTransform(1.05, -2.5))           // First: calibrate raw sensor
    .Add(new UnitConversionTransform("temp", "C", "F"))  // Second: convert units
    .Add(new MovingAverageTransform(5));                 // Third: smooth with moving average
```

### Executing the Pipeline

```csharp
var measures = new List<TelemetryMeasure>
{
    new() { ResourceId = "temp", Value = 25.0 }
};

var context = new TelemetryTransformContext
{
    DeviceId = "my-device",
    Timestamp = DateTimeOffset.UtcNow
};

// Execute all transforms in order
var transformed = await pipeline.ExecuteAsync(measures, context);
```

## Best Practices

### 1. Order Matters

Apply transforms in logical order:
```csharp
// *> Good: Calibrate raw sensor data, then convert units
var pipeline = TelemetryTransformPipeline.Create()
    .Add(new CalibrationTransform(1.05, -2.5))
    .Add(new UnitConversionTransform("temp", "C", "F"));

// X Bad: Convert units first, calibration factors may be wrong
var pipeline = TelemetryTransformPipeline.Create()
    .Add(new UnitConversionTransform("temp", "C", "F"))
    .Add(new CalibrationTransform(1.05, -2.5));
```

### 2. Keep Transforms Stateless When Possible

```csharp
// *> Good: Stateless transform
public class ScaleTransform : ITelemetryTransform
{
    private readonly double _factor;

    public async Task<IReadOnlyList<TelemetryMeasure>> TransformAsync(
        IReadOnlyList<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        var results = measures.Select(m => new TelemetryMeasure
        {
            ResourceId = m.ResourceId,
            Value = (double)m.Value * _factor,
            Timestamp = m.Timestamp
        }).ToList();

        return await Task.FromResult(results);
    }
}

// ! Use with caution: Stateful transform
public class MovingAverageTransform : ITelemetryTransform
{
    private readonly Dictionary<string, Queue<double>> _windows = new();
    // Maintains state - ensure thread safety if needed
}
```

### 3. Handle Invalid Data

```csharp
public async Task<IReadOnlyList<TelemetryMeasure>> TransformAsync(
    IReadOnlyList<TelemetryMeasure> measures,
    TelemetryTransformContext context,
    CancellationToken cancellationToken = default)
{
    var results = new List<TelemetryMeasure>();

    foreach (var measure in measures)
    {
        // Validate input
        if (measure.Value is not double value)
        {
            _logger.LogWarning("Cannot transform non-numeric value for {ResourceId}",
                measure.ResourceId);
            results.Add(measure); // Pass through unchanged
            continue;
        }

        // Perform transformation
        results.Add(new TelemetryMeasure
        {
            ResourceId = measure.ResourceId,
            Value = ProcessValue(value),
            Timestamp = measure.Timestamp
        });
    }

    return await Task.FromResult(results);
}
```

### 4. Test Transformations

```csharp
[Fact]
public async Task CalibrationTransform_Should_Apply_Correctly()
{
    // Arrange
    var transform = new CalibrationTransform(scale: 2.0, offset: 10.0);
    var measures = new List<TelemetryMeasure>
    {
        new() { ResourceId = "sensor", Value = 5.0 }
    };
    var context = new TelemetryTransformContext
    {
        DeviceId = "test",
        Timestamp = DateTimeOffset.UtcNow
    };

    // Act
    var output = await transform.TransformAsync(measures, context);

    // Assert
    output.Count.ShouldBe(1);
    ((double)output[0].Value).ShouldBe(20.0); // 5 * 2 + 10 = 20
}
```

## Performance Considerations

### 1. Avoid Heavy Computations

If transformation is CPU-intensive, consider:
- Pre-computing lookup tables for calibration curves
- Using approximate algorithms for complex calculations
- Caching results when appropriate

### 2. Memory Management

For stateful transforms:
- Limit buffer sizes for moving windows
- Clean up old data periodically
- Use `ConcurrentDictionary` for thread safety

### 3. Async Operations

All transforms use async patterns, making it easy to integrate I/O-bound operations:
```csharp
public async Task<IReadOnlyList<TelemetryMeasure>> TransformAsync(
    IReadOnlyList<TelemetryMeasure> measures,
    TelemetryTransformContext context,
    CancellationToken cancellationToken = default)
{
    // Can safely await I/O operations
    var enrichmentData = await _database.GetEnrichmentDataAsync(cancellationToken);

    // Process measures with enriched data
    return ProcessWithEnrichment(measures, enrichmentData);
}
```

## Next Steps

- [Adding DSP Filters](04_add_dsp_filters.md) - Apply signal processing filters after transforms
- [Using Hooks](05_using_hooks.md) - Intercept device lifecycle events

## Summary

Transformations in Weda SubNode SDK provide:
- **Built-in transforms**: CalibrationTransform (linear and curve-based), UnitConversionTransform
- **Custom transforms**: Implement `ITelemetryTransform` interface
- **Pipeline architecture**: Chain transforms using `TelemetryTransformPipeline`
- **Configuration-based setup**: Define transforms in `appsettings.json`
- **Async operations**: All transforms support async/await patterns

**Key Concepts:**
- Transforms process telemetry data before DSP filters
- CalibrationTransform applies linear calibration (scale/offset) or curve-based calibration
- UnitConversionTransform converts between temperature, pressure, and other units
- Transforms are applied in order based on `Order` property in configuration
- All transforms receive `TelemetryTransformContext` with device metadata

**Transform Order:**
1. **Calibration**: Apply scale/offset or calibration curve
2. **Unit Conversion**: Convert to desired units
3. **Custom Logic**: Moving average, aggregation, enrichment
4. **DSP Filters**: Applied after all transforms (see next guide)
