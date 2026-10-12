using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Telemetry;

using Xunit;

namespace Weda.SubNode.Core.Tests.Telemetry;

/// <summary>
/// Covers how the SubNode heartbeat follows configuration over the life of the process: it
/// stops while its sensor or the owning device is disabled, and starts again — without a
/// restart — as soon as configuration enables it.
/// </summary>
/// <remarks>
/// Each test drives <see cref="HeartbeatLoop.RunOnceAsync"/> by hand against a manual clock,
/// the way the hosted service drives it on the wall clock, so no test waits on a timer.
/// </remarks>
public class HeartbeatLoopTests
{
    private const string SubNodeDeviceId = "test-subnode-001";
    private const string HeartbeatResourceId = "21af0dc4-9254-5389-a7dd-df64d7cf782c";
    private const int Interval = 60_000;

    private readonly IWedaCloudService _cloudService = Substitute.For<IWedaCloudService>();
    private readonly IDeviceRegistry _registry = Substitute.For<IDeviceRegistry>();
    private readonly IWedaApplicationContext _context = Substitute.For<IWedaApplicationContext>();
    private readonly SubNodeInfo _subNodeInfo = new() { Name = "TestSubNode", DeviceId = SubNodeDeviceId };
    private readonly ManualClock _clock = new();
    private readonly CapturingLogger _logger = new();
    private readonly List<string> _beats = [];
    private readonly IDevice _idevice = Substitute.For<IDevice>();
    private DeviceConfiguration _device;

    public HeartbeatLoopTests()
    {
        _device = MakeDevice(MakeHeartbeatSensor());
        // Read at call time, so a test can swap the whole configuration.
        _idevice.Configuration.Returns(_ => _device);
        _registry.GetAllDevices().Returns(new[] { _idevice });
        _context.CloudService.Returns(_cloudService);
        _context.SubNodeInfo.Returns(_subNodeInfo);
        _cloudService.SendTelemetryAsync(Arg.Any<string>(), Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _beats.Add(call.Arg<TelemetryData>().Measures[0].ResourceId!);
                return true;
            });
    }

    private static Sensor MakeHeartbeatSensor(
        string resourceId = HeartbeatResourceId, bool enabled = true, int interval = Interval) => new()
        {
            Name = Heartbeat.SensorName,
            ResourceId = resourceId,
            Dtmi = Heartbeat.Dtmi,
            SensorGroup = SensorGroup.SYS,
            SensorInfo = new SensorInfo { Schema = Heartbeat.Schema, DisplayName = "Heartbeat" },
            Report = new SensorReport { Enabled = enabled, Interval = interval },
            Record = new SensorRecordingConfig { Enabled = false }
        };

    private static Sensor MakeOrdinarySensor() => new()
    {
        Name = "cpu_usage",
        ResourceId = "5c30a6f2-0000-5000-8000-000000000001",
        Report = new SensorReport { Enabled = true, Interval = 1_000 }
    };

    private static DeviceConfiguration MakeDevice(params Sensor[] sensors) => new()
    {
        DeviceName = "SystemAgentDeviceConfig",
        Sensors = [.. sensors]
    };

    private HeartbeatLoop MakeLoop() => new(
        _context, _registry, _logger, NullLogger<HeartbeatPublisher>.Instance, _clock);

    /// <summary>Applies a device's Enabled flag the way <c>DeviceBase</c> does on a devicecfg update.</summary>
    private void SetDeviceEnabled(bool enabled)
    {
        _device.Enabled = enabled;
        foreach (var sensor in _device.Sensors)
            sensor.DeviceEnabled = enabled;
    }

    private Sensor HeartbeatSensor => _device.Sensors.Single(Heartbeat.IsHeartbeat);

    [Fact]
    public async Task BeatsAtOnce_ThenOncePerInterval()
    {
        var loop = MakeLoop();

        var wait = await loop.RunOnceAsync(CancellationToken.None);
        Assert.Single(_beats);
        Assert.Equal(TimeSpan.FromMilliseconds(Interval), wait);

        _clock.Advance(TimeSpan.FromMilliseconds(Interval - 1));
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Single(_beats);

        _clock.Advance(TimeSpan.FromMilliseconds(1));
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Equal(2, _beats.Count);
    }

    [Fact]
    public async Task StopsWhileTheOwningDeviceIsDisabled()
    {
        var loop = MakeLoop();
        await loop.RunOnceAsync(CancellationToken.None);

        SetDeviceEnabled(false);
        _clock.Advance(TimeSpan.FromMilliseconds(Interval));
        var wait = await loop.RunOnceAsync(CancellationToken.None);

        Assert.Single(_beats);
        Assert.Equal(HeartbeatLoop.ConfigurationPollInterval, wait);
    }

    [Fact]
    public async Task ResumesAtOnce_WhenTheOwningDeviceIsReEnabled()
    {
        // The field report: off then on through telemetry-configs left the SubNode with no
        // heartbeat until system-agent was restarted.
        var loop = MakeLoop();
        await loop.RunOnceAsync(CancellationToken.None);
        SetDeviceEnabled(false);
        _clock.Advance(TimeSpan.FromMilliseconds(Interval));
        await loop.RunOnceAsync(CancellationToken.None);

        SetDeviceEnabled(true);
        _clock.Advance(HeartbeatLoop.ConfigurationPollInterval);
        var wait = await loop.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, _beats.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(Interval), wait);
    }

    [Fact]
    public async Task StopsAndResumes_WithTheSensorsOwnReportFlag()
    {
        var loop = MakeLoop();
        await loop.RunOnceAsync(CancellationToken.None);

        HeartbeatSensor.Report.Enabled = false;
        _clock.Advance(TimeSpan.FromMilliseconds(Interval));
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Single(_beats);

        HeartbeatSensor.Report.Enabled = true;
        _clock.Advance(HeartbeatLoop.ConfigurationPollInterval);
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Equal(2, _beats.Count);
    }

    [Fact]
    public async Task Starts_WhenADeviceDisabledAtStartupIsEnabled()
    {
        SetDeviceEnabled(false);
        var loop = MakeLoop();

        var wait = await loop.RunOnceAsync(CancellationToken.None);
        Assert.Empty(_beats);
        Assert.Equal(HeartbeatLoop.ConfigurationPollInterval, wait);

        SetDeviceEnabled(true);
        _clock.Advance(wait);
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Single(_beats);
    }

    [Fact]
    public async Task Starts_WhenConfigurationDeclaresTheSensorLater()
    {
        _device = MakeDevice(MakeOrdinarySensor());
        var loop = MakeLoop();

        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Empty(_beats);

        _device.Sensors.Add(MakeHeartbeatSensor());
        _clock.Advance(HeartbeatLoop.ConfigurationPollInterval);
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Single(_beats);
    }

    [Fact]
    public async Task FollowsASensorReplacedByAConfigurationUpdate()
    {
        // A cloud update can rebuild the sensor list, so the sensor read at startup is not
        // the one configuration holds later. The beat must follow the current one.
        var loop = MakeLoop();
        await loop.RunOnceAsync(CancellationToken.None);

        const string newResourceId = "9b1e2f3a-0000-5000-8000-0000000000aa";
        _device.Sensors = [MakeHeartbeatSensor(resourceId: newResourceId, interval: 30_000)];
        _clock.Advance(TimeSpan.FromMilliseconds(Interval));
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Equal(newResourceId, _beats[^1]);

        _clock.Advance(TimeSpan.FromMilliseconds(30_000));
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Equal(3, _beats.Count);
    }

    [Fact]
    public async Task ShortensTheWait_WhenTheIntervalIsReduced()
    {
        var loop = MakeLoop();
        await loop.RunOnceAsync(CancellationToken.None);

        _clock.Advance(TimeSpan.FromMilliseconds(Interval));
        await loop.RunOnceAsync(CancellationToken.None);
        HeartbeatSensor.Report.Interval = 10_000;
        _clock.Advance(TimeSpan.FromMilliseconds(10_000));
        await loop.RunOnceAsync(CancellationToken.None);

        Assert.Equal(3, _beats.Count);
    }

    [Fact]
    public async Task UsesOneSensor_WhenSeveralDevicesDeclareOne()
    {
        // Liveness belongs to the SubNode: a second declaration is a configuration mistake,
        // reported once, not a reason to beat twice.
        var second = Substitute.For<IDevice>();
        second.Configuration.Returns(MakeDevice(MakeHeartbeatSensor(resourceId: "9b1e2f3a-0000-5000-8000-0000000000bb")));
        _registry.GetAllDevices().Returns(new[] { _idevice, second });
        var loop = MakeLoop();

        for (var i = 0; i < 3; i++)
        {
            await loop.RunOnceAsync(CancellationToken.None);
            _clock.Advance(TimeSpan.FromMilliseconds(Interval));
        }

        Assert.Equal(3, _beats.Count);
        Assert.All(_beats, id => Assert.Equal(HeartbeatResourceId, id));
        Assert.Single(_logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("devices declare a heartbeat sensor"));
    }

    [Fact]
    public async Task ClampsAnIntervalBelowTheFloor_AndSaysSoOnce()
    {
        HeartbeatSensor.Report.Interval = 200;
        var loop = MakeLoop();

        var wait = await loop.RunOnceAsync(CancellationToken.None);
        Assert.Equal(TimeSpan.FromMilliseconds(Heartbeat.MinIntervalMilliseconds), wait);
        for (var i = 0; i < 3; i++)
        {
            _clock.Advance(wait);
            await loop.RunOnceAsync(CancellationToken.None);
        }

        Assert.Equal(4, _beats.Count);
        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("floor"));
    }

    [Fact]
    public async Task WaitsForRegistration_BeforeTheFirstBeat()
    {
        _subNodeInfo.DeviceId = null;
        var loop = MakeLoop();

        var wait = await loop.RunOnceAsync(CancellationToken.None);
        Assert.Empty(_beats);
        Assert.Equal(HeartbeatLoop.ConfigurationPollInterval, wait);

        _subNodeInfo.DeviceId = SubNodeDeviceId;
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Single(_beats);
    }

    [Fact]
    public async Task WaitsForTheSensorsResourceId_BeforeTheFirstBeat()
    {
        HeartbeatSensor.ResourceId = string.Empty;
        var loop = MakeLoop();

        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Empty(_beats);

        HeartbeatSensor.ResourceId = HeartbeatResourceId;
        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Single(_beats);
    }

    [Fact]
    public async Task KeepsRunning_WhenConfigurationCannotBeRead()
    {
        // The sensor list can change while it is being scanned. Throwing here would end the
        // heartbeat for the rest of the process, so a failed read is reported and retried.
        _registry.GetAllDevices().Returns(
            _ => throw new InvalidOperationException("Collection was modified"),
            _ => new[] { _idevice });
        var loop = MakeLoop();

        var wait = await loop.RunOnceAsync(CancellationToken.None);
        Assert.Empty(_beats);
        Assert.Equal(HeartbeatLoop.ConfigurationPollInterval, wait);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);

        await loop.RunOnceAsync(CancellationToken.None);
        Assert.Single(_beats);
    }

    [Fact]
    public async Task PropagatesCancellation()
    {
        var loop = MakeLoop();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => loop.RunOnceAsync(cts.Token));
    }

    [Fact]
    public async Task ReportsEachStateChangeOnce()
    {
        // The loop polls every second while it is not beating; the log must say what
        // changed, not repeat itself every poll.
        var loop = MakeLoop();
        await loop.RunOnceAsync(CancellationToken.None);

        SetDeviceEnabled(false);
        for (var i = 0; i < 5; i++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(Interval));
            await loop.RunOnceAsync(CancellationToken.None);
        }
        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("disabled"));

        SetDeviceEnabled(true);
        for (var i = 0; i < 3; i++)
        {
            _clock.Advance(TimeSpan.FromMilliseconds(Interval));
            await loop.RunOnceAsync(CancellationToken.None);
        }
        Assert.Single(_logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("resumed"));
    }

    [Fact]
    public async Task SaysWhetherTheSensorIsMissingOrDisabled()
    {
        // "Declare a sensor named hb" is the wrong advice when one is declared but disabled.
        SetDeviceEnabled(false);
        await MakeLoop().RunOnceAsync(CancellationToken.None);
        var disabled = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("disabled by configuration", disabled.Message);

        _logger.Entries.Clear();
        _device = MakeDevice(MakeOrdinarySensor());
        await MakeLoop().RunOnceAsync(CancellationToken.None);
        var missing = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("Declare a sensor named", missing.Message);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
