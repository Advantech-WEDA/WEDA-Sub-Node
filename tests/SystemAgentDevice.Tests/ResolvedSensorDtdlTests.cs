using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using SystemAgentExample.Communication;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Telemetry;
using Weda.SubNode.TestBase;

using Xunit;

namespace SystemAgentDevice.Tests;

/// <summary>
/// The DTDL uploaded with the capability must describe the per-resource sensors that
/// system-agent actually publishes, not only the templates written in devicecfg.json.
///
/// The host builds the DTDL while loading devicecfg.json, before the device is constructed;
/// <see cref="SystemAgentDeviceBase"/> then expands each template into one sensor per
/// discovered interface, pin or onboard source, and the base constructor's second
/// InitializeDtdl call is a no-op. The cloud therefore received Telemetry for
/// <c>voltage</c> but none for <c>voltage_12V</c>, and its data-stream service created those
/// streams as <c>string</c> without a display name, so their latest values never appeared.
/// After a cloud configuration update, re-resolution left the DTDL on the templates the same way.
///
/// These tests share the global TypedSensorDispatch.Resolve hook, so they run in one collection.
/// </summary>
[Collection("TypedSensorDispatch")]
public class ResolvedSensorDtdlTests
{
    private const string DeviceName = "SystemAgentDeviceConfig";

    private readonly ILogger _logger = Substitute.For<ILogger>();

    static ResolvedSensorDtdlTests()
    {
        // Materialize the registry's lazy init, which writes TypedSensorDispatch.Resolve once,
        // before any test here swaps that hook.
        _ = SensorTypeRegistry.HasAny;
    }

    private static Sensor NetworkTemplate() => new()
    {
        Name = "network_bytes_sent",
        SensorGroup = SensorGroup.SYS,
        Parameters = new Dictionary<string, object>
        {
            ["MetricType"] = "network",
            ["MetricName"] = "bytes_sent",
            ["Interfaces"] = Array.Empty<string>()
        },
        SensorInfo = new SensorInfo { Schema = "long", DisplayName = "Bytes Sent" },
        Report = new SensorReport { Enabled = true, Interval = 60000 },
    };

    private static Sensor VoltageTemplate() => new()
    {
        Name = "voltage",
        SensorGroup = SensorGroup.PWR,
        Parameters = new Dictionary<string, object> { ["MetricType"] = "voltage", ["MetricName"] = "voltage" },
        SensorInfo = new SensorInfo { Schema = "double", DisplayName = "Voltage", Description = "Voltage sensor reading" },
        Report = new SensorReport { Enabled = true, Interval = 30000 },
    };

    private static Sensor CpuUsage() => new()
    {
        Name = "cpu_usage",
        SensorGroup = SensorGroup.SYS,
        Parameters = new Dictionary<string, object> { ["MetricType"] = "cpu", ["MetricName"] = "usage" },
        SensorInfo = new SensorInfo { Schema = "double", DisplayName = "CPU Usage" },
        Report = new SensorReport { Enabled = true, Interval = 10000 },
    };

    /// <summary>The configuration as the host hands it over: DTDL already built from the templates.</summary>
    private static DeviceConfiguration HostLoadedConfig(bool typed, params Sensor[] sensors)
    {
        var configuration = new DeviceConfiguration
        {
            DeviceName = DeviceName,
            DeviceId = "c400ad54f9e0",
            DeviceTypeName = typed ? SystemAgentExample.Sensors.SystemMonitor.DeviceTypeName : null,
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            Sensors = [.. sensors]
        };
        configuration.InitializeDtdl();
        return configuration;
    }

    private static List<DtdlContent> TelemetryOf(DeviceConfiguration configuration) =>
        configuration.DtdlInterface.ShouldBeOfType<DtdlInterface>().Contents;

    /// <summary>Every sensor has exactly one Telemetry entry, and no two entries share an @id.</summary>
    private static void ShouldDescribeEverySensor(DeviceConfiguration configuration)
    {
        var telemetry = TelemetryOf(configuration);
        telemetry.Select(c => c.Name).ShouldBe(configuration.Sensors.Select(s => s.Name));
        telemetry.Select(c => c.Id).Distinct().Count().ShouldBe(telemetry.Count);
    }

    /// <summary>Stub typed dispatch: one sensor type dtmi per MetricType, shared by its sensors.</summary>
    private static IDisposable UseTypeDtmiPerMetricType()
    {
        var previous = TypedSensorDispatch.Resolve;
        TypedSensorDispatch.Resolve = (_, sensor) =>
            $"dtmi:test:sensor:{sensor.Parameters!["MetricType"]};1";
        return new Restore(() => TypedSensorDispatch.Resolve = previous);
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    // --- Startup: the device constructor expands the templates ---

    [Fact]
    public void Constructor_RebuildsTheDtdlForTheExpandedSensors()
    {
        var configuration = HostLoadedConfig(typed: false, NetworkTemplate());

        using var device = new SystemAgentDeviceBase(
            new MockApplicationContext(), configuration, new LocalSystemCommunication());

        // At least the loopback interface is discovered on any Linux host.
        configuration.Sensors.Count.ShouldBeGreaterThan(1);
        ShouldDescribeEverySensor(configuration);
    }

    [Fact]
    public void Constructor_TypedDevice_KeepsOneTelemetryIdPerSensor()
    {
        using var _ = UseTypeDtmiPerMetricType();
        var configuration = HostLoadedConfig(typed: true, CpuUsage(), NetworkTemplate());

        using var device = new SystemAgentDeviceBase(
            new MockApplicationContext(), configuration, new LocalSystemCommunication());

        configuration.Sensors.Count.ShouldBeGreaterThan(2);
        ShouldDescribeEverySensor(configuration);

        // The capability keeps reporting each sensor's type dtmi.
        configuration.Sensors.Where(s => s.Name.StartsWith("network_"))
            .ShouldAllBe(s => s.Dtmi == "dtmi:test:sensor:network;1");
    }

    // --- Runtime: re-resolution after a cloud configuration update ---

    [Fact]
    public void ReResolve_RebuildsTheDtdlForTheReResolvedSensors()
    {
        var configuration = HostLoadedConfig(typed: false, VoltageTemplate());
        var resources = new DiscoveredResources([], [], [], VoltageSources: ["Vcore", "5V", "12V", "CMOS Battery"]);

        SensorReResolver.ReResolve(configuration, resources, _logger).ShouldBeTrue();

        ShouldDescribeEverySensor(configuration);
        var rail = TelemetryOf(configuration).Single(c => c.Name == "voltage_12V");
        rail.Schema.ShouldBe("double");
        rail.DisplayName.ShouldBe("12V Voltage");
    }

    [Fact]
    public void ReResolve_TypedDevice_KeepsOneTelemetryIdPerSensor()
    {
        using var _ = UseTypeDtmiPerMetricType();
        var configuration = HostLoadedConfig(typed: true, CpuUsage(), VoltageTemplate());
        var resources = new DiscoveredResources([], [], [], VoltageSources: ["Vcore", "5V", "12V"]);

        SensorReResolver.ReResolve(configuration, resources, _logger).ShouldBeTrue();

        ShouldDescribeEverySensor(configuration);
        configuration.Sensors.Where(s => s.Name.StartsWith("voltage"))
            .ShouldAllBe(s => s.Dtmi == "dtmi:test:sensor:voltage;1");
    }

    [Fact]
    public void ReResolve_NothingToResolve_LeavesTheDtdlAlone()
    {
        var configuration = HostLoadedConfig(typed: false, CpuUsage());
        var before = configuration.DtdlInterface;

        SensorReResolver.ReResolve(configuration, new DiscoveredResources([], [], []), _logger).ShouldBeFalse();

        configuration.DtdlInterface.ShouldBeSameAs(before);
    }
}
