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
/// Tests Transform → Filter → Send pipeline with events and statistics.
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
    public async Task ProcessAsync_WithEmptyMeasures_ShouldReturnSuccess()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>();

        // Act
        var result = await _pipeline.ProcessAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockCloudService.DidNotReceive().SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithoutTransformsOrFilters_ShouldSendDirectly()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temperature", Value = 25.5 }
        };

        _mockCloudService.SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _pipeline.ProcessAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockCloudService.Received(1).SendTelemetryAsync(
            TestDeviceId,
            Arg.Is<TelemetryData>(td => td.Measures.Count == 1),
            Arg.Any<CancellationToken>());

        var stats = _pipeline.GetStatistics();
        stats.TotalProcessed.ShouldBe(1);
        stats.SuccessfullySent.ShouldBe(1);
        stats.FailedToSend.ShouldBe(0);
    }

    [Fact]
    public async Task ProcessAsync_WithTransform_ShouldApplyTransformation()
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

        _mockCloudService.SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        _pipeline.AddTransform(transform);

        // Act
        var result = await _pipeline.ProcessAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockCloudService.Received(1).SendTelemetryAsync(
            TestDeviceId,
            Arg.Is<TelemetryData>(td =>
                td.Measures.Count == 1 &&
                Convert.ToDouble(td.Measures[0].Value) == 50), // 25 * 2
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithFilter_ShouldApplySmoothing()
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

        _mockCloudService.SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        _pipeline.AddFilter(filter);

        // Act
        var result = await _pipeline.ProcessAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockCloudService.Received(1).SendTelemetryAsync(
            TestDeviceId,
            Arg.Is<TelemetryData>(td =>
                td.Measures.Count == 3 && // All measures preserved
                Convert.ToDouble(td.Measures[0].Value) == 9 && // 10 * 0.9
                Convert.ToDouble(td.Measures[1].Value) == 27 && // 30 * 0.9
                Convert.ToDouble(td.Measures[2].Value) == 45), // 50 * 0.9
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_WithEmptyMeasuresAfterValidation_ShouldReturnSuccessWithoutSend()
    {
        // Arrange - Empty measures list (all filtered out by validation, not DSP filter)
        var measures = new List<TelemetryMeasure>();

        // Act
        var result = await _pipeline.ProcessAsync(measures);

        // Assert
        result.IsError.ShouldBeFalse();
        await _mockCloudService.DidNotReceive().SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessAsync_CloudServiceFails_ShouldReturnError()
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
        var result = await _pipeline.ProcessAsync(measures);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldContain("SendFailed");

        var stats = _pipeline.GetStatistics();
        stats.FailedToSend.ShouldBe(1);
    }

    [Fact]
    public async Task ProcessAsync_TransformThrows_ShouldReturnError()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp", Value = 25 }
        };

        var transform = Substitute.For<ITelemetryTransform>();
        transform.Name.Returns("FailingTransform");
        transform.TransformAsync(
            Arg.Any<List<TelemetryMeasure>>(),
            Arg.Any<TelemetryTransformContext>(),
            Arg.Any<CancellationToken>())
            .Returns<Task<List<TelemetryMeasure>>>(_ => throw new InvalidOperationException("Transform failed"));

        _pipeline.AddTransform(transform);

        // Act
        var result = await _pipeline.ProcessAsync(measures);

        // Assert
        result.IsError.ShouldBeTrue();
        result.FirstError.Code.ShouldContain("TransformFailed");

        var stats = _pipeline.GetStatistics();
        stats.FailedToSend.ShouldBe(1);
    }

    #endregion

    #region Event Tests

    [Fact]
    public async Task ProcessAsync_ShouldEmitStageEvents()
    {
        // Arrange
        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp", Value = 25 }
        };

        var events = new List<TelemetryPipelineStageEvent>();
        _pipeline.StageExecuting += (s, e) => events.Add(e);

        _mockCloudService.SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _pipeline.ProcessAsync(measures);

        // Assert
        // Should have Before and After events for all 3 stages: Transform, Filter, Send
        events.Count.ShouldBe(6); // 3 stages × 2 events (Before/After) = 6

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

        // Send stage
        events[4].Stage.ShouldBe(PipelineStage.Send);
        events[4].Phase.ShouldBe(StagePhase.Before);
        events[5].Stage.ShouldBe(PipelineStage.Send);
        events[5].Phase.ShouldBe(StagePhase.After);
        events[5].Duration.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessAsync_WithAllStages_ShouldEmitAllEvents()
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
            .Returns(callInfo => Task.FromResult(callInfo.ArgAt<List<TelemetryMeasure>>(0)));

        var filter = Substitute.For<IDspFilter>();
        filter.ApplyAsync(
            Arg.Any<IAsyncEnumerable<TelemetryMeasure>>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo => callInfo.ArgAt<IAsyncEnumerable<TelemetryMeasure>>(0));

        _pipeline.AddTransform(transform);
        _pipeline.AddFilter(filter);

        var events = new List<TelemetryPipelineStageEvent>();
        _pipeline.StageExecuting += (s, e) => events.Add(e);

        _mockCloudService.SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _pipeline.ProcessAsync(measures);

        // Assert
        // Should have 6 events: Transform(Before+After), Filter(Before+After), Send(Before+After)
        events.Count.ShouldBe(6);
        events.Count(e => e.Stage == PipelineStage.Transform).ShouldBe(2);
        events.Count(e => e.Stage == PipelineStage.Filter).ShouldBe(2);
        events.Count(e => e.Stage == PipelineStage.Send).ShouldBe(2);
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
        _mockCloudService.SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        var measures = new List<TelemetryMeasure>
        {
            new() { ResourceId = "Temp", Value = 25 }
        };

        // Act
        await _pipeline.ProcessAsync(measures);
        await _pipeline.ProcessAsync(measures);

        // Assert
        var stats = _pipeline.GetStatistics();
        stats.TotalProcessed.ShouldBe(2);
        stats.SuccessfullySent.ShouldBe(2);
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

        _mockCloudService.SendTelemetryAsync(
            Arg.Any<string>(),
            Arg.Any<TelemetryData>(),
            Arg.Any<CancellationToken>())
            .Returns(true);

        await _pipeline.ProcessAsync(measures);

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

}
