using Shouldly;
using System.Diagnostics;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Transforms;
using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// Unit tests for TelemetryTransformPipeline
/// Tests pipeline execution order, CalibrationTransform, UnitConversionTransform, etc.
/// </summary>
public class TelemetryTransformPipelineTests
{
    private readonly TelemetryTransformContext _testContext;

    public TelemetryTransformPipelineTests()
    {
        _testContext = new TelemetryTransformContext
        {
            DeviceId = "test-device-001",
            Timestamp = DateTimeOffset.UtcNow,
            Metadata = new Dictionary<string, object>
            {
                ["SubNodeType"] = "modbus-tcp"
            }
        };
    }

    #region Pipeline Basic Tests

    [Fact]
    public void Create_Should_ReturnNewPipelineInstance()
    {
        // Act
        var pipeline = TelemetryTransformPipeline.Create();

        // Assert
        pipeline.ShouldNotBeNull();
    }

    [Fact]
    public void Add_Should_ThrowException_When_TransformIsNull()
    {
        // Arrange
        var pipeline = TelemetryTransformPipeline.Create();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
        {
            pipeline.Add(null!);
        });
    }

    [Fact]
    public async Task ExecuteAsync_Should_ReturnOriginalMeasures_When_PipelineIsEmpty()
    {
        // Arrange
        var pipeline = TelemetryTransformPipeline.Create();
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 25.0 }
        };

        // Act
        var result = await pipeline.ExecuteAsync(measures, _testContext);

        // Assert
        result.ShouldBe(measures);
        result.Count.ShouldBe(1);
        ((double)result[0].Value).ShouldBe(25.0);
    }

    #endregion

    #region Transform Execution Order Tests

    [Fact]
    public async Task ExecuteAsync_Should_ApplyTransformsInOrder()
    {
        // Arrange
        var pipeline = TelemetryTransformPipeline.Create()
            .Add(new CalibrationTransform(scale: 2.0, offset: 10.0))  // (25 * 2) + 10 = 60
            .Add(new UnitConversionTransform("C", "F")); // 60 * 9/5 + 32 = 140

        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 25.0 }
        };

        // Act
        var result = await pipeline.ExecuteAsync(measures, _testContext);

        // Assert
        result.Count.ShouldBe(1);
        var expected = (25.0 * 2.0 + 10.0) * 9.0 / 5.0 + 32.0; // Should be 140
        ((double)result[0].Value).ShouldBe(expected, 0.001);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ApplyMultipleTransforms_InSequence()
    {
        // Arrange
        var pipeline = TelemetryTransformPipeline.Create()
            .Add(new CalibrationTransform(scale: 1.1, offset: -5.0))
            .Add(new CalibrationTransform(scale: 1.0, offset: 3.0))
            .Add(new CalibrationTransform(scale: 2.0, offset: 0.0));

        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 10.0 }
        };

        // Act
        var result = await pipeline.ExecuteAsync(measures, _testContext);

        // Assert
        // Step 1: (10 * 1.1) - 5 = 6
        // Step 2: (6 * 1.0) + 3 = 9
        // Step 3: (9 * 2.0) + 0 = 18
        ((double)result[0].Value).ShouldBe(18.0, 0.001);
    }

    #endregion

    #region CalibrationTransform Tests

    [Fact]
    public async Task CalibrationTransform_Should_ApplyLinearCalibration()
    {
        // Arrange
        var transform = new CalibrationTransform(scale: 1.1, offset: -5.0);
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 25.0 }
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert
        var expected = (25.0 * 1.1) - 5.0; // 22.5
        ((double)result[0].Value).ShouldBe(expected, 0.001);
    }

    [Fact]
    public async Task CalibrationTransform_Should_ApplyToAllResources()
    {
        // Arrange
        var transform = new CalibrationTransform(scale: 2.0, offset: 10.0);
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 10.0 },
            new() { ResourceId = "humidity-001", Value = 50.0 },
            new() { ResourceId = "pressure-001", Value = 100.0 }
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert
        result.Count.ShouldBe(3);
        ((double)result[0].Value).ShouldBe(30.0); // (10 * 2) + 10
        ((double)result[1].Value).ShouldBe(110.0); // (50 * 2) + 10
        ((double)result[2].Value).ShouldBe(210.0); // (100 * 2) + 10
    }

    [Fact]
    public async Task CalibrationTransform_Should_SkipNonNumericValues()
    {
        // Arrange
        var transform = new CalibrationTransform(scale: 2.0, offset: 10.0);
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "sensor-001", Value = "text value" },
            new() { ResourceId = "sensor-002", Value = true },
            new() { ResourceId = "sensor-003", Value = 25.0 }
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert
        result[0].Value.ShouldBe("text value"); // Unchanged
        result[1].Value.ShouldBe(true); // Unchanged
        ((double)result[2].Value).ShouldBe(60.0); // (25 * 2) + 10
    }

    [Fact]
    public async Task CalibrationTransform_Should_ApplyCurveCalibration_WhenCurveProvided()
    {
        // Arrange
        var calibrationCurve = new List<Weda.SubNode.Core.Transforms.CalibrationPoint>
        {
            new() { RawValue = 0, CalibratedValue = 0 },
            new() { RawValue = 50, CalibratedValue = 60 },
            new() { RawValue = 100, CalibratedValue = 110 }
        };
        var transform = new CalibrationTransform(calibrationCurve);
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 25.0 } // Should interpolate
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert
        // Interpolation between 0->60 at position 25 (halfway)
        // Expected: 0 + (25/50) * (60-0) = 30
        ((double)result[0].Value).ShouldBe(30.0, 0.001);
    }

    #endregion

    #region UnitConversionTransform Tests

    [Fact]
    public async Task UnitConversionTransform_Should_ConvertCelsiusToFahrenheit()
    {
        // Arrange
        var transform = new UnitConversionTransform("C", "F");
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 0.0 },
            new() { ResourceId = "temp-001", Value = 100.0 }
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert
        ((double)result[0].Value).ShouldBe(32.0, 0.001); // 0°C = 32°F
        ((double)result[1].Value).ShouldBe(212.0, 0.001); // 100°C = 212°F
    }

    [Fact]
    public async Task UnitConversionTransform_Should_ConvertFahrenheitToCelsius()
    {
        // Arrange
        var transform = new UnitConversionTransform("F", "C");
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 32.0 },
            new() { ResourceId = "temp-001", Value = 212.0 }
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert
        ((double)result[0].Value).ShouldBe(0.0, 0.001);
        ((double)result[1].Value).ShouldBe(100.0, 0.001);
    }

    [Fact]
    public async Task UnitConversionTransform_Should_ConvertCelsiusToKelvin()
    {
        // Arrange
        var transform = new UnitConversionTransform("C", "K");
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 0.0 },
            new() { ResourceId = "temp-001", Value = 100.0 }
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert
        ((double)result[0].Value).ShouldBe(273.15, 0.001);
        ((double)result[1].Value).ShouldBe(373.15, 0.001);
    }

    [Fact]
    public async Task UnitConversionTransform_Should_ConvertKelvinToCelsius()
    {
        // Arrange
        var transform = new UnitConversionTransform("K", "C");
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 273.15 },
            new() { ResourceId = "temp-001", Value = 373.15 }
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert
        ((double)result[0].Value).ShouldBe(0.0, 0.001);
        ((double)result[1].Value).ShouldBe(100.0, 0.001);
    }

    [Fact]
    public async Task UnitConversionTransform_Should_ConvertAllMeasures()
    {
        // Arrange - Now converts all measures (no TargetResourceId filtering)
        var transform = new UnitConversionTransform("C", "F");
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 0.0 },
            new() { ResourceId = "temp-002", Value = 0.0 }
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert - Both should be converted
        ((double)result[0].Value).ShouldBe(32.0, 0.001); // Converted
        ((double)result[1].Value).ShouldBe(32.0, 0.001); // Converted
    }

    [Fact]
    public async Task UnitConversionTransform_Should_SkipNonNumericValues()
    {
        // Arrange
        var transform = new UnitConversionTransform("C", "F");
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "sensor-001", Value = "text" },
            new() { ResourceId = "sensor-001", Value = 25.0 }
        };

        // Act
        var result = await transform.TransformAsync(measures, _testContext);

        // Assert
        result[0].Value.ShouldBe("text");
        ((double)result[1].Value).ShouldBe(77.0, 0.001); // 25°C to °F
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Pipeline_Should_HandleEmptyMeasureList()
    {
        // Arrange
        var pipeline = TelemetryTransformPipeline.Create()
            .Add(new CalibrationTransform(scale: 2.0, offset: 0.0));
        var measures = new List<TelemetryMeasure>();

        // Act
        var result = await pipeline.ExecuteAsync(measures, _testContext);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task Pipeline_Should_AcceptCancellationToken()
    {
        // Arrange
        var pipeline = TelemetryTransformPipeline.Create()
            .Add(new CalibrationTransform(scale: 2.0, offset: 0.0));
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 25.0 }
        };

        var cts = new CancellationTokenSource();

        // Act - Should complete without throwing
        var result = await pipeline.ExecuteAsync(measures, _testContext, cts.Token);

        // Assert
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task Pipeline_Should_ProcessLargeDataset_UnderPerformanceThreshold()
    {
        // Arrange
        var pipeline = TelemetryTransformPipeline.Create()
            .Add(new CalibrationTransform(scale: 1.1, offset: -5.0))
            .Add(new UnitConversionTransform("C", "F"));

        // Generate 10,000 measures
        var measures = Enumerable.Range(1, 10000)
            .Select(i => new TelemetryMeasure
            {
                ResourceId = $"sensor-{i % 100:000}",
                Value = (double)i
            })
            .ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await pipeline.ExecuteAsync(measures, _testContext);
        stopwatch.Stop();

        // Assert
        result.Count.ShouldBe(10000);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(100); // Must be under 100ms
    }

    [Fact]
    public async Task CalibrationTransform_Should_ProcessLargeDataset_Efficiently()
    {
        // Arrange
        var transform = new CalibrationTransform(scale: 1.5, offset: 10.0);
        var measures = Enumerable.Range(1, 10000)
            .Select(i => new TelemetryMeasure
            {
                ResourceId = $"sensor-{i}",
                Value = (double)i
            })
            .ToList();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = await transform.TransformAsync(measures, _testContext);
        stopwatch.Stop();

        // Assert
        result.Count.ShouldBe(10000);
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(50);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task ComplexPipeline_Should_ProcessMultipleSensors_WithDifferentTransforms()
    {
        // Arrange
        var pipeline = TelemetryTransformPipeline.Create()
            .Add(new CalibrationTransform(scale: 1.05, offset: -2.0))
            .Add(new UnitConversionTransform("C", "F"));

        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "temp-001", Value = 20.0 },
            new() { ResourceId = "pressure-001", Value = 0.0 },
            new() { ResourceId = "humidity-001", Value = 65.0 }
        };

        // Act
        var result = await pipeline.ExecuteAsync(measures, _testContext);

        // Assert
        result.Count.ShouldBe(3);

        // All measures go through both Calibration and UnitConversion:
        // Temperature:
        // Step 1 (Calibration): (20 * 1.05) + (-2) = 21 - 2 = 19
        // Step 2 (UnitConversion): 19 * 9/5 + 32 = 34.2 + 32 = 66.2°F
        ((double)result[0].Value).ShouldBe(66.2, 0.1);

        // Pressure:
        // Step 1 (Calibration): (0 * 1.05) + (-2) = -2.0
        // Step 2 (UnitConversion): -2 * 9/5 + 32 = 28.4°F
        ((double)result[1].Value).ShouldBe(28.4, 0.1);

        // Humidity:
        // Step 1 (Calibration): (65 * 1.05) + (-2) = 68.25 - 2 = 66.25
        // Step 2 (UnitConversion): 66.25 * 9/5 + 32 = 151.25°F
        ((double)result[2].Value).ShouldBe(151.25, 0.1);
    }

    #endregion
}
