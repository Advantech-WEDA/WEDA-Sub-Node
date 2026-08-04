using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Protocols.Cfx;
using Weda.SubNode.TestBase;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

/// <summary>
/// Pins the exactly-once reporting contract for CFX.
/// </summary>
/// <remarks>
/// CFX messages are discrete events. Under the default cache-and-sample model they were re-reported
/// on every <c>Report.Interval</c> tick for as long as they sat in the cache — measured at roughly 5x
/// amplification against a live WedaNode, and unbounded over time. These tests assert that one
/// arriving message produces exactly one report.
/// </remarks>
public class CfxEventDrivenTelemetryTests : IDisposable
{
    private const string MessageName = "CFX.ResourcePerformance.StationStateChanged";

    /// <summary>Report interval short enough that a sampling loop would fire many times.</summary>
    private const int ReportIntervalMs = 100;

    private readonly MockApplicationContext _context = new();
    private readonly FakePubSub _transport = new();

    [Fact]
    public async Task OneArrivingMessage_IsReportedExactlyOnce()
    {
        // Arrange
        using var device = await StartedDeviceAsync();

        var batches = new List<List<TelemetryMeasure>>();
        device.DataReceived += (_, e) => batches.Add([.. e.Data]);

        // Act — deliver once, then wait well past several report intervals.
        _transport.Deliver(TopicFor(MessageName), CfxFixtures.GzipPayload(MessageName));
        await Task.Delay(ReportIntervalMs * 8);

        // Assert — a sampling loop would have re-reported the cached event on every tick.
        var batch = Assert.Single(batches);
        var measure = Assert.Single(batch);

        // InitializeAsync enriches sensors with generated ResourceIds, so resolve it from the
        // device rather than assuming the configured placeholder survived.
        Assert.Equal(ResourceIdOf(device), measure.ResourceId);
    }

    [Fact]
    public async Task RepeatedArrivals_AreReportedOncePerArrival()
    {
        // Arrange
        using var device = await StartedDeviceAsync();

        var count = 0;
        device.DataReceived += (_, _) => Interlocked.Increment(ref count);

        // Act
        const int arrivals = 3;
        for (var i = 0; i < arrivals; i++)
        {
            _transport.Deliver(TopicFor(MessageName), CfxFixtures.GzipPayload(MessageName));
            await Task.Delay(20);
        }

        await Task.Delay(ReportIntervalMs * 5);

        // Assert
        Assert.Equal(arrivals, Volatile.Read(ref count));
    }

    [Fact]
    public async Task TwoArrivalsInsideOneInterval_AreBothReported()
    {
        // Arrange — under cache-and-sample the second message would overwrite the first before the
        // sampler ever read it, silently losing an event.
        using var device = await StartedDeviceAsync();

        var count = 0;
        device.DataReceived += (_, _) => Interlocked.Increment(ref count);

        // Act — both delivered well inside a single report interval.
        _transport.Deliver(TopicFor(MessageName), CfxFixtures.GzipPayload(MessageName));
        _transport.Deliver(TopicFor(MessageName), CfxFixtures.GzipPayload(MessageName));

        await Task.Delay(ReportIntervalMs * 5);

        // Assert
        Assert.Equal(2, Volatile.Read(ref count));
    }

    [Fact]
    public async Task NoMessages_ReportsNothing()
    {
        // Arrange — the sampling loop reported an empty read as nothing, and so must this path.
        using var device = await StartedDeviceAsync();

        var raised = false;
        device.DataReceived += (_, _) => raised = true;

        // Act
        await Task.Delay(ReportIntervalMs * 5);

        // Assert
        Assert.False(raised);
    }

    [Fact]
    public async Task CachedValueRemainsReadable_SoAccessorsStillWork()
    {
        // Arrange — event-driven mode must still populate the cache; only the sampling loop is gone.
        using var device = await StartedDeviceAsync();

        // Act
        _transport.Deliver(TopicFor(MessageName), CfxFixtures.GzipPayload(MessageName));
        await Task.Delay(200);

        var cached = await device.ReadSensorTelemetryAsync(ResourceIdOf(device));

        // Assert
        var measure = Assert.Single(cached);
        Assert.Contains("NewState", Assert.IsType<string>(measure.Value));
    }

    private async Task<CfxDevice> StartedDeviceAsync()
    {
        var device = new CfxDevice(_context, BuildConfiguration(), _transport)
        {
            // RaiseDataReceived is a no-op unless tracking is enabled.
            EnableDataReceivedTracking = true,
        };

        Assert.True(await device.InitializeAsync());
        Assert.True(await device.StartAsync());

        // Let the subscription task attach before messages are delivered.
        await Task.Delay(100);

        return device;
    }

    /// <summary>
    /// The sole sensor's ResourceId as it stands after initialization enriched it.
    /// </summary>
    private static string ResourceIdOf(CfxDevice device) =>
        device.Configuration.Sensors.Single().ResourceId;

    private static string TopicFor(string messageName) =>
        $"SUNJSONG/SLD880A/0001/{CfxTopic.DefaultRoot}/{CfxMessageCatalog.ToRelativeTopicPath(messageName)}";

    private static DeviceConfiguration BuildConfiguration() => new()
    {
        DeviceId = "test-cfx-device",
        DeviceName = "SUNJSONG SLD880A",
        SubNodeInfo = new SubNodeInfo
        {
            Name = "Test",
            Manufacturer = "SUNJSONG",
            Model = "SLD880A",
            SwVersion = "1.0",
            SubNodeType = SubNodeType.CustomDevice,
        },
        Dtdl = new DtdlConfig
        {
            AutoGenEnabled = false,
            DtdlPath = "tests/Weda.SubNode.TestBase/Fixtures/test-device.dtdl.json",
        },
        DeviceCommunication = new Dictionary<string, object>
        {
            [CfxPubSubParser.CfxHandleKey] = "SUNJSONG.SLD880A.0001",
        },
        Sensors =
        [
            new Sensor
            {
                ResourceId = "station-state-resource",
                Name = "cfx_station_state_changed",
                Dtmi = "dtmi:advantech:app:json",
                DeviceResourceId = "test-cfx-device",
                SensorGroup = SensorGroup.SYS,
                Parameters = new Dictionary<string, object>
                {
                    [CfxSensorParameters.MessageNameKey] = MessageName,
                },
                Report = new SensorReport { Enabled = true, Interval = ReportIntervalMs },
                SensorInfo = new SensorInfo { Schema = "application/json" },
            }
        ],
    };

    public void Dispose()
    {
        _transport.Dispose();
        _context.Dispose();
    }
}
