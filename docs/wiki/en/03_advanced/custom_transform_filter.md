# Custom Transform and DSP Filter Development

This guide explains how to create custom transforms and DSP filters for the SubNode telemetry pipeline. With the self-registration pattern, you can add new transforms and filters without modifying any factory code.

## Architecture Overview

The telemetry processing pipeline consists of two stages:

```
Raw Data → [Transform Pipeline] → [DSP Filter Pipeline] → Processed Data
```

- **Transforms**: Applied first, used for data calibration, unit conversion, etc.
- **DSP Filters**: Applied after transforms, used for signal processing like smoothing, filtering, etc.

## Creating a Custom Transform

### Step 1: Implement the Interface

Create a new class that implements both `ITelemetryTransform` and `IConfigurableTransform<T>`:

```csharp
using ErrorOr;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;

namespace MyProject.Transforms;

/// <summary>
/// Custom transform that scales values by a configurable factor.
/// </summary>
public class ScaleTransform : ITelemetryTransform, IConfigurableTransform<ScaleTransform>
{
    private double _factor;

    // === Static Abstract Implementation (Self-Registration) ===

    /// <summary>
    /// The type name used in configuration files.
    /// This is matched case-insensitively against TransformConfig.Type.
    /// </summary>
    public static string TypeName => "scale";

    /// <summary>
    /// Factory method called by TransformFactory when creating from configuration.
    /// </summary>
    public static ScaleTransform Create(Dictionary<string, object> parameters)
    {
        var factor = 1.0;
        if (parameters.TryGetValue("Factor", out var value))
            factor = Convert.ToDouble(value);

        return new ScaleTransform(factor);
    }

    // === Instance Members ===

    public string Name => nameof(ScaleTransform);

    public bool Enabled { get; set; } = true;

    public ScaleTransform(double factor = 1.0)
    {
        _factor = factor;
    }

    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Factor", out var value))
        {
            var factor = Convert.ToDouble(value);
            if (factor == 0)
                return Error.Validation("ScaleTransform.Factor", "Factor cannot be 0");
        }
        return Result.Success;
    }

    public void UpdateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Factor", out var value))
            _factor = Convert.ToDouble(value);
    }

    public Task<List<TelemetryMeasure>> TransformAsync(
        List<TelemetryMeasure> measures,
        TelemetryTransformContext context,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled)
            return Task.FromResult(measures);

        var transformed = measures.Select(measure =>
        {
            if (measure.Value is not double and not int and not float)
                return measure;

            var value = Convert.ToDouble(measure.Value);
            return measure with { Value = value * _factor };
        }).ToList();

        return Task.FromResult(transformed);
    }
}
```

### Step 2: Use in Configuration

Once implemented, the transform is automatically discovered. Use it in `appsettings.json`:

```json
{
  "Sensors": [
    {
      "Name": "temperature.sensor",
      "Config": {
        "TransformPipeline": [
          {
            "Type": "scale",
            "Enabled": true,
            "Parameters": {
              "Factor": 1.5
            }
          }
        ]
      }
    }
  ]
}
```

### Step 3: Use Programmatically

```csharp
sensor.Config.ConfigureTransforms(transforms =>
{
    transforms.Add(new ScaleTransform(factor: 1.5));
});
```

## Creating a Custom DSP Filter

### Step 1: Implement the Interface

Create a class that implements `IDspFilter` and `IConfigurableDspFilter<T>`:

```csharp
using System.Runtime.CompilerServices;
using ErrorOr;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Telemetry;

namespace MyProject.Dsp;

/// <summary>
/// Low-pass filter that removes values above a threshold.
/// </summary>
public class LowPassFilter : IDspFilter, IConfigurableDspFilter<LowPassFilter>
{
    private double _threshold;

    // === Static Abstract Implementation (Self-Registration) ===

    public static string TypeName => "lowpass";

    public static LowPassFilter Create(Dictionary<string, object> parameters)
    {
        var threshold = double.MaxValue;
        if (parameters.TryGetValue("Threshold", out var value))
            threshold = Convert.ToDouble(value);

        return new LowPassFilter(threshold);
    }

    // === Instance Members ===

    public bool Enabled { get; set; } = true;

    public LowPassFilter(double threshold = double.MaxValue)
    {
        _threshold = threshold;
    }

    public ErrorOr<Success> ValidateParameters(Dictionary<string, object> parameters)
    {
        // No validation needed for threshold
        return Result.Success;
    }

    public void UpdateParameters(Dictionary<string, object> parameters)
    {
        if (parameters.TryGetValue("Threshold", out var value))
            _threshold = Convert.ToDouble(value);
    }

    public async IAsyncEnumerable<TelemetryMeasure> ApplyAsync(
        IAsyncEnumerable<TelemetryMeasure> input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var measure in input.WithCancellation(cancellationToken))
        {
            if (!Enabled)
            {
                yield return measure;
                continue;
            }

            if (measure.Value is not double and not int and not float)
            {
                yield return measure;
                continue;
            }

            var value = Convert.ToDouble(measure.Value);

            // Only pass through values below threshold
            if (value <= _threshold)
            {
                yield return measure;
            }
            // Values above threshold are filtered out
        }
    }
}
```

### Step 2: Use in Configuration

```json
{
  "Sensors": [
    {
      "Name": "temperature.sensor",
      "Config": {
        "DspPipeline": [
          {
            "Type": "lowpass",
            "Enabled": true,
            "Parameters": {
              "Threshold": 100.0
            }
          }
        ]
      }
    }
  ]
}
```

### Step 3: Use Programmatically

```csharp
sensor.Config.ConfigureDspFilters(filters =>
{
    filters.Add(new LowPassFilter(threshold: 100.0));
});
```

## Key Interface Methods

### IConfigurableTransform / IConfigurableDspFilter

| Member | Description |
|--------|-------------|
| `static string TypeName` | The type name used in configuration (case-insensitive match) |
| `static T Create(Dictionary<string, object> parameters)` | Factory method to create instance from config parameters |

### ITelemetryTransform

| Member | Description |
|--------|-------------|
| `string Name` | Human-readable name for logging/debugging |
| `bool Enabled` | Enable/disable the transform at runtime |
| `ValidateParameters()` | Validate parameters before applying them |
| `UpdateParameters()` | Update parameters at runtime (hot reload) |
| `TransformAsync()` | Apply transformation to telemetry measures |

### IDspFilter

| Member | Description |
|--------|-------------|
| `bool Enabled` | Enable/disable the filter at runtime |
| `ValidateParameters()` | Validate parameters before applying them |
| `UpdateParameters()` | Update parameters at runtime (hot reload) |
| `ApplyAsync()` | Apply filter to async stream of measures |

## Registering External Assemblies

If your custom transforms/filters are in a separate assembly, register it at startup:

```csharp
// In Program.cs or startup code
TransformFactory.RegisterAssemblies(typeof(MyCustomTransform).Assembly);
DspFilterFactory.RegisterAssemblies(typeof(MyCustomFilter).Assembly);
```

## Built-in Transforms

| Type Name | Class | Description |
|-----------|-------|-------------|
| `calibration` | `CalibrationTransform` | Linear (scale/offset) or curve-based calibration |
| `unitconversion` | `UnitConversionTransform` | Temperature unit conversion (C/F/K) |

## Built-in DSP Filters

| Type Name | Class | Description |
|-----------|-------|-------------|
| `kalman` | `KalmanFilter` | 1D Kalman filter for noise reduction |
| `movingaverage` | `MovingAverageFilter` | Moving average with O(1) complexity |
| `relu` | `ReluFilter` | ReLU activation (sets negative to 0) |

## Debugging

Check registered types at runtime:

```csharp
// List all registered transform types
Console.WriteLine($"Transforms: {string.Join(", ", TransformFactory.RegisteredTypes)}");

// List all registered filter types
Console.WriteLine($"Filters: {string.Join(", ", DspFilterFactory.RegisteredTypes)}");
```

## Best Practices

1. **Naming**: Use lowercase, single-word type names (e.g., `scale`, `lowpass`)
2. **Validation**: Always implement `ValidateParameters()` to catch configuration errors early
3. **State Management**: Keep per-sensor state in a dictionary keyed by `ResourceId`
4. **Thread Safety**: DSP filters process async streams - ensure thread-safe state access
5. **Default Values**: Always provide sensible defaults in `Create()` method
6. **Documentation**: Add XML docs explaining what each parameter does
