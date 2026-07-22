using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Telemetry;

using Xunit;

namespace Weda.SubNode.Core.Tests.Telemetry;

/// <summary>
/// Covers the SubNode liveness heartbeat
/// (SD-Heartbeat-Committed-Scope §API Contract — "The heartbeat sensor").
/// </summary>
/// <remarks>
/// The heartbeat asserts one thing only: this SubNode was alive and reporting at the
/// moment the platform received the message. The platform derives connectivity from
/// arrival time, never from the value or the device-reported timestamp, so these tests
/// pin the wire shape rather than any state the value might be thought to carry.
/// </remarks>
public class HeartbeatPublisherTests
{
    private const string SubNodeDeviceId = "test-subnode-001";
    private const string HeartbeatResourceId = "21af0dc4-9254-5389-a7dd-df64d7cf782c";

    private readonly IWedaCloudService _cloudService = Substitute.For<IWedaCloudService>();

    private static SubNodeInfo MakeSubNodeInfo(string? deviceId = SubNodeDeviceId) => new()
    {
        Name = "TestSubNode",
        DeviceId = deviceId
    };

    private static Sensor MakeHeartbeatSensor(
        string resourceId = HeartbeatResourceId,
        bool enabled = true,
        int interval = Heartbeat.DefaultIntervalMilliseconds) => new()
        {
            Name = Heartbeat.SensorName,
            ResourceId = resourceId,
            Dtmi = Heartbeat.Dtmi,
            SensorGroup = SensorGroup.SYS,
            SensorInfo = new SensorInfo { Schema = "boolean", DisplayName = "Heartbeat" },
            Report = new SensorReport { Enabled = enabled, Interval = interval },
            Record = new SensorRecordingConfig { Enabled = false }
        };

    private HeartbeatPublisher MakePublisher(SubNodeInfo? subNodeInfo = null, Sensor? sensor = null) =>
        new(_cloudService,
            subNodeInfo ?? MakeSubNodeInfo(),
            sensor ?? MakeHeartbeatSensor(),
            NullLogger<HeartbeatPublisher>.Instance);

    private void CloudAccepts() =>
        _cloudService.SendTelemetryAsync(Arg.Any<string>(), Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>())
            .Returns(true);

    private TelemetryData? CapturedTelemetry()
    {
        var call = _cloudService.ReceivedCalls()
            .FirstOrDefault(c => c.GetMethodInfo().Name == nameof(IWedaCloudService.SendTelemetryAsync));

        return call?.GetArguments()[1] as TelemetryData;
    }

    [Fact]
    public async Task PublishAsync_SendsSingleMeasure_OnTheSubNodeDeviceId()
    {
        // Sent on the SubNode's id, not the owning device's: one SubNode, one liveness signal.
        CloudAccepts();

        var sent = await MakePublisher().PublishAsync(CancellationToken.None);

        Assert.True(sent);
        await _cloudService.Received(1).SendTelemetryAsync(
            SubNodeDeviceId, Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>());
        Assert.Single(CapturedTelemetry()!.Measures);
    }

    [Fact]
    public async Task PublishAsync_EmitsConstantTrue()
    {
        CloudAccepts();

        await MakePublisher().PublishAsync(CancellationToken.None);

        Assert.Equal(true, CapturedTelemetry()!.Measures[0].Value);
    }

    [Fact]
    public async Task PublishAsync_UsesTheDeclaredSensorsResourceId()
    {
        // Reusing the declared sensor's id is what lets enriched telemetry resolve the beat
        // against the sensor registry instead of falling back to "unknown".
        CloudAccepts();

        await MakePublisher().PublishAsync(CancellationToken.None);

        Assert.Equal(HeartbeatResourceId, CapturedTelemetry()!.Measures[0].ResourceId);
    }

    [Fact]
    public async Task PublishAsync_TagsMeasureWithReservedMarker()
    {
        // The Transceiver detects liveness on the raw, pre-enrichment message. The wire
        // sensorId is a per-SubNode hash and cannot be a well-known literal, so the marker
        // in metadata is what identifies the measure.
        CloudAccepts();

        await MakePublisher().PublishAsync(CancellationToken.None);

        var metadata = CapturedTelemetry()!.Measures[0].Metadata;
        Assert.NotNull(metadata);
        Assert.True(metadata!.TryGetValue(Heartbeat.MarkerKey, out var marker));
        Assert.Equal(true, marker);
    }

    [Fact]
    public async Task PublishAsync_SkipsQuietly_WhenSubNodeNotYetRegistered()
    {
        // Before registration there is no DeviceId and no telemetry topic. Beating into an
        // unconfigured topic would throw on every tick.
        var sent = await MakePublisher(MakeSubNodeInfo(deviceId: null)).PublishAsync(CancellationToken.None);

        Assert.False(sent);
        await _cloudService.DidNotReceive().SendTelemetryAsync(
            Arg.Any<string>(), Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_SkipsQuietly_BeforeResourceIdsAreAssigned()
    {
        // ResourceIds are assigned during device initialization; a beat sent earlier could
        // not be attributed to anything.
        var sensor = MakeHeartbeatSensor(resourceId: string.Empty);

        var sent = await MakePublisher(sensor: sensor).PublishAsync(CancellationToken.None);

        Assert.False(sent);
        await _cloudService.DidNotReceive().SendTelemetryAsync(
            Arg.Any<string>(), Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_DoesNotPropagate_WhenCloudSendThrows()
    {
        // A transport fault must not tear down the heartbeat loop: the next beat is the
        // recovery mechanism, and an unhandled exception here would stop liveness for good.
        _cloudService.SendTelemetryAsync(Arg.Any<string>(), Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("topics not configured"));

        var sent = await MakePublisher().PublishAsync(CancellationToken.None);

        Assert.False(sent);
    }

    [Fact]
    public async Task PublishAsync_PropagatesCancellation()
    {
        // Shutdown must not be swallowed by the fault handling above.
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => MakePublisher().PublishAsync(cts.Token));
    }
}

/// <summary>
/// Covers how a heartbeat sensor is recognised in <c>devicecfg.json</c>. Declaring the
/// sensor is the opt-in, so these rules decide whether a SubNode beats at all.
/// </summary>
public class HeartbeatSensorResolutionTests
{
    private static Sensor MakeSensor(string? dtmi, bool enabled = true, int interval = 60_000) => new()
    {
        Name = Heartbeat.SensorName,
        ResourceId = "21af0dc4-9254-5389-a7dd-df64d7cf782c",
        Dtmi = dtmi,
        Report = new SensorReport { Enabled = enabled, Interval = interval }
    };

    [Fact]
    public void IsHeartbeat_MatchesTheReservedDtmi()
    {
        Assert.True(Heartbeat.IsHeartbeat(MakeSensor(Heartbeat.Dtmi)));
    }

    [Fact]
    public void IsHeartbeat_IgnoresSensorsThatMerelyShareTheName()
    {
        // Identity is the DTMI, not the name — a customer sensor called "hb" must not be
        // mistaken for the platform heartbeat, nor inherit its handling.
        Assert.False(Heartbeat.IsHeartbeat(MakeSensor("dtmi:example:Custom;1")));
        Assert.False(Heartbeat.IsHeartbeat(MakeSensor(dtmi: null)));
        Assert.False(Heartbeat.IsHeartbeat(null));
    }

    [Fact]
    public void FindEnabled_ReturnsNull_WhenNoSensorIsDeclared()
    {
        // The disabled default: a devicecfg with no heartbeat sensor beats never.
        Assert.Null(Heartbeat.FindEnabled([MakeSensor("dtmi:example:Custom;1")]));
        Assert.Null(Heartbeat.FindEnabled([]));
        Assert.Null(Heartbeat.FindEnabled(null));
    }

    [Fact]
    public void FindEnabled_ReturnsNull_WhenTheSensorIsDisabled()
    {
        Assert.Null(Heartbeat.FindEnabled([MakeSensor(Heartbeat.Dtmi, enabled: false)]));
    }

    [Fact]
    public void FindEnabled_ReturnsNull_WhenTheOwningDeviceIsDisabled()
    {
        var sensor = MakeSensor(Heartbeat.Dtmi);
        sensor.DeviceEnabled = false;

        Assert.Null(Heartbeat.FindEnabled([sensor]));
    }

    [Fact]
    public void FindEnabled_FindsTheSensorAmongOthers()
    {
        var heartbeat = MakeSensor(Heartbeat.Dtmi);

        var found = Heartbeat.FindEnabled([MakeSensor("dtmi:example:Custom;1"), heartbeat]);

        Assert.Same(heartbeat, found);
    }

    [Theory]
    [InlineData(60_000, 60_000)]
    [InlineData(30_000, 30_000)]
    [InlineData(Heartbeat.MinIntervalMilliseconds, Heartbeat.MinIntervalMilliseconds)]
    public void ResolveInterval_HonoursAConfiguredInterval(int configured, int expected)
    {
        Assert.Equal(expected, Heartbeat.ResolveInterval(MakeSensor(Heartbeat.Dtmi, interval: configured)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public void ResolveInterval_ClampsBelowFloor_RatherThanThrowing(int configured)
    {
        // This value can arrive from a cloud config update. Refusing to run would take
        // liveness down entirely — the opposite of what the signal is for.
        Assert.Equal(
            Heartbeat.MinIntervalMilliseconds,
            Heartbeat.ResolveInterval(MakeSensor(Heartbeat.Dtmi, interval: configured)));
    }

    [Fact]
    public void ReservedContract_MatchesSolutionDesign()
    {
        Assert.Equal("hb", Heartbeat.SensorName);
        Assert.Equal("hb", Heartbeat.MarkerKey);
        Assert.Equal("dtmi:com:advantech:weda:Heartbeat;1", Heartbeat.Dtmi);
        // T = 60 s per User Stories v2.11 (SD still states 30 s — known divergence).
        Assert.Equal(60_000, Heartbeat.DefaultIntervalMilliseconds);
    }
}
