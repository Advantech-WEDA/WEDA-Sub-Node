using System.Collections.Concurrent;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Telemetry;
using Weda.SubNode.Host;

using Xunit;

namespace Weda.SubNode.Core.Tests.Telemetry;

/// <summary>
/// Runs the real heartbeat hosted service on the wall clock, at the 1 s interval floor, the
/// way a devicecfg update drives it on a device: <c>DeviceBase</c> applies a device's
/// <c>Enabled</c> flag by setting <see cref="Sensor.DeviceEnabled"/> on every sensor it owns.
/// </summary>
/// <remarks>
/// Field report (EPC-R7300, v1.2.0): turning the system-agent device off through
/// telemetry-configs and back on left the SubNode with no heartbeat — and the platform
/// reading it as Disconnected — until system-agent was restarted.
/// </remarks>
public class HeartbeatHostedServiceTests
{
    private const string HeartbeatResourceId = "21af0dc4-9254-5389-a7dd-df64d7cf782c";
    private static readonly TimeSpan BeatWait = TimeSpan.FromSeconds(5);

    private readonly IWedaCloudService _cloudService = Substitute.For<IWedaCloudService>();
    private readonly ConcurrentQueue<DateTimeOffset> _beats = new();

    public HeartbeatHostedServiceTests()
    {
        _cloudService.SendTelemetryAsync(Arg.Any<string>(), Arg.Any<TelemetryData>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _beats.Enqueue(DateTimeOffset.UtcNow);
                return true;
            });
    }

    private static Sensor MakeHeartbeatSensor(bool deviceEnabled = true) => new()
    {
        Name = Heartbeat.SensorName,
        ResourceId = HeartbeatResourceId,
        Dtmi = Heartbeat.Dtmi,
        SensorGroup = SensorGroup.SYS,
        SensorInfo = new SensorInfo { Schema = Heartbeat.Schema, DisplayName = "Heartbeat" },
        Report = new SensorReport { Enabled = true, Interval = Heartbeat.MinIntervalMilliseconds },
        Record = new SensorRecordingConfig { Enabled = false },
        DeviceEnabled = deviceEnabled
    };

    private HeartbeatHostedService MakeService(DeviceConfiguration configuration)
    {
        var device = Substitute.For<IDevice>();
        device.Configuration.Returns(configuration);

        var registry = Substitute.For<IDeviceRegistry>();
        registry.GetAllDevices().Returns(_ => new[] { device });

        var context = Substitute.For<IWedaApplicationContext>();
        context.CloudService.Returns(_cloudService);
        context.SubNodeInfo.Returns(new SubNodeInfo { Name = "TestSubNode", DeviceId = "test-subnode-001" });

        return new HeartbeatHostedService(
            context,
            registry,
            NullLogger<HeartbeatHostedService>.Instance,
            NullLogger<HeartbeatPublisher>.Instance);
    }

    private static DeviceConfiguration MakeDevice(Sensor heartbeat) => new()
    {
        DeviceName = "SystemAgentDeviceConfig",
        Enabled = heartbeat.DeviceEnabled,
        Sensors = [heartbeat]
    };

    /// <summary>Waits until more than <paramref name="count"/> beats were sent, or the wait runs out.</summary>
    private async Task<bool> BeatsExceedAsync(int count, TimeSpan wait)
    {
        var deadline = DateTimeOffset.UtcNow + wait;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_beats.Count > count)
                return true;
            await Task.Delay(50);
        }
        return _beats.Count > count;
    }

    [Fact]
    public async Task Heartbeat_ResumesWhenTheOwningDeviceIsReEnabled()
    {
        var heartbeat = MakeHeartbeatSensor();
        var device = MakeDevice(heartbeat);
        using var service = MakeService(device);

        await service.StartAsync(CancellationToken.None);
        try
        {
            Assert.True(await BeatsExceedAsync(0, BeatWait), "the heartbeat never started");

            // Turn the device off, as a devicecfg update does, and let the service notice.
            device.Enabled = false;
            heartbeat.DeviceEnabled = false;
            await Task.Delay(TimeSpan.FromMilliseconds(2_500));
            var beatsWhileDisabled = _beats.Count;
            Assert.False(await BeatsExceedAsync(beatsWhileDisabled, TimeSpan.FromMilliseconds(1_500)),
                "a disabled device must not beat");

            // Turn it back on.
            device.Enabled = true;
            heartbeat.DeviceEnabled = true;
            Assert.True(await BeatsExceedAsync(beatsWhileDisabled, BeatWait),
                "the heartbeat must resume once the device is enabled again, without a restart");
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Heartbeat_StartsWhenADeviceDisabledAtStartupIsEnabled()
    {
        var heartbeat = MakeHeartbeatSensor(deviceEnabled: false);
        var device = MakeDevice(heartbeat);
        using var service = MakeService(device);

        await service.StartAsync(CancellationToken.None);
        try
        {
            Assert.False(await BeatsExceedAsync(0, TimeSpan.FromMilliseconds(1_500)),
                "a disabled device must not beat");

            device.Enabled = true;
            heartbeat.DeviceEnabled = true;
            Assert.True(await BeatsExceedAsync(0, BeatWait),
                "the heartbeat must start once the device is enabled, without a restart");
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }
}
