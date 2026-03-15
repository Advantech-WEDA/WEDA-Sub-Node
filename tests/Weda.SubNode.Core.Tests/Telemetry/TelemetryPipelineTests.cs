using ErrorOr;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Abstractions.Transforms;
using Weda.SubNode.Core.Telemetry;
using Xunit;

namespace Weda.SubNode.Core.Tests.Telemetry;

/// <summary>
/// Unit tests for TelemetryPipeline.
/// Tests Validate → Transform → Filter pipeline with events and statistics.
/// </summary>
public sealed class TelemetryPipelineTests
{
    private const string TestDeviceId = "test-pipeline-device";
    private readonly IWedaCloudService _mockCloudService;
    private readonly TelemetryPipeline _pipeline;

    public TelemetryPipelineTests()
    {
        _mockCloudService = Substitute.For<IWedaCloudService>();
        _pipeline = new TelemetryPipeline(
            TestDeviceId,
            configuration: null,
            _mockCloudService,
            NullLogger<TelemetryPipeline>.Instance);
    }

    #region Pipeline Execution Tests

    [Fact]
    public async Task TransformAndFilterAsync_WithEmptyMeasures_ShouldReturnEmptyList()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>();

        // Act
        var result = await _pipeline.TransformAndFilterAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task TransformAndFilterAsync_WithoutTransformsOrFilters_ShouldReturnMeasuresUnchanged()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temperature", Value = 25.5 }
        };

        // Act
        var result = await _pipeline.TransformAndFilterAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(1);
        result.Value[0].Value.ShouldBe(25.5);

        var stats = _pipeline.GetStatistics();
        stats.TotalProcessed.ShouldBe(1);
    }

    [Fact]
    public async Task TransformAndFilterAsync_WithTransform_ShouldApplyTransformation()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp", Value = 25 }
        };

        var transform = Substitute.For<ITelemetryTransform>();
        transform.Name.Returns("TestTransform");
        transform.TransformAsync(
            Arg.Any<List<TelemetryMeasure>>(),
            Arg.Any<TelemetryTransformContext>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var input = callInfo.ArgAt<List<TelemetryMeasure>>(0);
                return Task.FromResult(input.Select(m => new TelemetryMeasure
                {
                    ResourceId = m.ResourceId,
                    Value = Convert.ToDouble(m.Value) * 2
                }).ToList());
            });

        _pipeline.AddTransform(transform);

        // Act
        var result = await _pipeline.TransformAndFilterAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(1);
        Convert.ToDouble(result.Value[0].Value).ShouldBe(50); // 25 * 2
    }

    [Fact]
    public async Task TransformAndFilterAsync_WithFilter_ShouldApplySmoothing()
    {
        // Arrange - DSP filters smooth values but don't remove items
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp1", Value = 10 },
            new() { ResourceId = "Temp2", Value = 30 },
            new() { ResourceId = "Temp3", Value = 50 }
        };

        var filter = Substitute.For<IDspFilter>();
        filter.ApplyAsync(
            Arg.Any<IAsyncEnumerable<TelemetryMeasure>>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var input = callInfo.ArgAt<IAsyncEnumerable<TelemetryMeasure>>(0);
                return SmoothAsync(input);

                // DSP filter smooths values (e.g., moving average) but preserves count
                static async IAsyncEnumerable<TelemetryMeasure> SmoothAsync(
                    IAsyncEnumerable<TelemetryMeasure> source)
                {
                    await foreach (var measure in source)
                    {
                        // Simulate smoothing: multiply by 0.9 (like a low-pass filter)
                        yield return new TelemetryMeasure
                        {
                            ResourceId = measure.ResourceId,
                            Value = Convert.ToDouble(measure.Value) * 0.9
                        };
                    }
                }
            });

        _pipeline.AddFilter(filter);

        // Act
        var result = await _pipeline.TransformAndFilterAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(3); // All measures preserved
        Convert.ToDouble(result.Value[0].Value).ShouldBe(9);  // 10 * 0.9
        Convert.ToDouble(result.Value[1].Value).ShouldBe(27); // 30 * 0.9
        Convert.ToDouble(result.Value[2].Value).ShouldBe(45); // 50 * 0.9
    }

    [Fact]
    public async Task TransformAndFilterAsync_TransformThrows_ShouldIsolateSensorAndContinue()
    {
        // Arrange - Two sensors, one will fail
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "GoodSensor", Value = 25 },
            new() { ResourceId = "BadSensor", Value = 100 }
        };

        var transform = Substitute.For<ITelemetryTransform>();
        transform.Name.Returns("FailingTransform");
        transform.TransformAsync(
            Arg.Any<List<TelemetryMeasure>>(),
            Arg.Any<TelemetryTransformContext>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var input = callInfo.ArgAt<List<TelemetryMeasure>>(0);
                // Throw if processing BadSensor
                if (input.Any(m => m.ResourceId == "BadSensor"))
                    throw new InvalidOperationException("Transform failed for BadSensor");
                return Task.FromResult(input);
            });

        _pipeline.AddTransform(transform);

        // Act
        var result = await _pipeline.TransformAndFilterAsync(measures);

        // Assert - GoodSensor should still be processed
        result.IsError.ShouldBeFalse();
        result.Value.Count.ShouldBe(1);
        result.Value[0].ResourceId.ShouldBe("GoodSensor");
    }

    #endregion

    #region Event Tests

    [Fact]
    public async Task TransformAndFilterAsync_ShouldEmitStageEvents()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp", Value = 25 }
        };

        var events = new List<TelemetryPipelineStageEvent>();
        _pipeline.StageExecuting += (s, e) => events.Add(e);

        // Act
        await _pipeline.TransformAndFilterAsync(measures);

        // Assert
        // Should have Before and After events for Transform and Filter stages
        events.Count.ShouldBe(4); // 2 stages × 2 events (Before/After) = 4

        // Transform stage
        events[0].Stage.ShouldBe(PipelineStage.Transform);
        events[0].Phase.ShouldBe(StagePhase.Before);
        events[1].Stage.ShouldBe(PipelineStage.Transform);
        events[1].Phase.ShouldBe(StagePhase.After);
        events[1].Duration.ShouldNotBeNull();

        // Filter stage
        events[2].Stage.ShouldBe(PipelineStage.Filter);
        events[2].Phase.ShouldBe(StagePhase.Before);
        events[3].Stage.ShouldBe(PipelineStage.Filter);
        events[3].Phase.ShouldBe(StagePhase.After);
        events[3].Duration.ShouldNotBeNull();
    }

    #endregion

    #region Statistics Tests

    [Fact]
    public void GetStatistics_InitialState_ShouldReturnZeros()
    {
        // Act
        var stats = _pipeline.GetStatistics();

        // Assert
        stats.DeviceId.ShouldBe(TestDeviceId);
        stats.TotalProcessed.ShouldBe(0);
        stats.SuccessfullySent.ShouldBe(0);
        stats.FailedToSend.ShouldBe(0);
        stats.LastProcessedAt.ShouldBeNull();
    }

    [Fact]
    public async Task GetStatistics_AfterProcessing_ShouldTrackCorrectly()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp", Value = 25 }
        };

        // Act
        await _pipeline.TransformAndFilterAsync(measures);
        await _pipeline.TransformAndFilterAsync(measures);

        // Assert
        var stats = _pipeline.GetStatistics();
        stats.TotalProcessed.ShouldBe(2);
        stats.LastProcessedAt.ShouldNotBeNull();
    }

    #endregion

    #region Pipeline Management Tests

    [Fact]
    public void AddTransform_ShouldAddToTransforms()
    {
        // Arrange
        var transform = Substitute.For<ITelemetryTransform>();
        transform.Name.Returns("TestTransform");

        // Act
        _pipeline.AddTransform(transform);

        // Assert - No direct way to verify, but should not throw
        Should.NotThrow(() => _pipeline.AddTransform(transform));
    }

    [Fact]
    public void AddFilter_ShouldAddToFilters()
    {
        // Arrange
        var filter = Substitute.For<IDspFilter>();

        // Act
        _pipeline.AddFilter(filter);

        // Assert - No direct way to verify, but should not throw
        Should.NotThrow(() => _pipeline.AddFilter(filter));
    }

    [Fact]
    public async Task ClearStages_ShouldRemoveAllTransformsAndFilters()
    {
        // Arrange
        var transform = Substitute.For<ITelemetryTransform>();
        transform.Name.Returns("TestTransform");
        transform.TransformAsync(
            Arg.Any<List<TelemetryMeasure>>(),
            Arg.Any<TelemetryTransformContext>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new List<TelemetryMeasure>()));

        _pipeline.AddTransform(transform);
        _pipeline.AddFilter(Substitute.For<IDspFilter>());

        // Act
        _pipeline.ClearStages();

        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp", Value = 25 }
        };

        await _pipeline.TransformAndFilterAsync(measures);

        // Assert - Transform should not be called
        await transform.DidNotReceive().TransformAsync(
            Arg.Any<List<TelemetryMeasure>>(),
            Arg.Any<TelemetryTransformContext>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AddTransform_WithNull_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _pipeline.AddTransform(null!));
    }

    [Fact]
    public void AddFilter_WithNull_ShouldThrow()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() => _pipeline.AddFilter(null!));
    }

    #endregion

    #region SendAsync Tests

    [Fact]
    public async Task SendAsync_WithMeasures_ShouldCallCloudService()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp", Value = 25 }
        };

        _mockCloudService.SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _pipeline.SendAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockCloudService.Received(1).SendTelemetryAsync(
            TestDeviceId,
            Arg.Is<TelemetryData>(td => td.Measures.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_CloudServiceFails_ShouldReturnError()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp", Value = 25 }
        };

        _mockCloudService.SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _pipeline.SendAsync(measures);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldContain("SendFailed");
    }

    #endregion
}
