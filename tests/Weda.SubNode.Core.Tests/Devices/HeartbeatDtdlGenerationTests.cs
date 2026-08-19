using Shouldly;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace Weda.SubNode.Core.Tests.Devices;

/// <summary>
/// Covers auto-generation of the reserved heartbeat's DTDL.
/// </summary>
/// <remarks>
/// The point of these tests is that <c>devicecfg.json</c> declares a name and an interval
/// and nothing else: the dtmi and the <c>boolean</c> schema are supplied by the SDK and
/// travel into the generated Interface like any other sensor's would. An author who has to
/// hand-write a platform dtmi can delete it, and deleting it used to disable liveness
/// silently.
/// </remarks>
public class HeartbeatDtdlGenerationTests
{
    private static DeviceConfiguration CreateConfig(params Sensor[] sensors)
    {
        var config = new DeviceConfiguration
        {
            DeviceName = "TestDevice",
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                AutoGenEnabled = true,
                SubNodeType = SubNodeType.CustomDevice
            }
        };

        foreach (var sensor in sensors)
            config.Sensors.Add(sensor);

        return config;
    }

    /// <summary>A heartbeat as an author would actually write it: no dtmi, no schema.</summary>
    private static Sensor BareHeartbeat() => new()
    {
        Name = Heartbeat.SensorName,
        SensorGroup = SensorGroup.SYS,
        Report = new SensorReport { Enabled = true, Interval = Heartbeat.DefaultIntervalMilliseconds }
    };

    private static Sensor Cpu() => new()
    {
        Name = "cpu_usage",
        SensorGroup = SensorGroup.SYS,
        SensorInfo = new SensorInfo { Schema = "double", DisplayName = "CPU Usage" },
        Report = new SensorReport { Enabled = true, Interval = 5000 }
    };

    // DeviceConfiguration.DtdlInterface is typed as object so the manual-file path can
    // assign a loaded model; autogen always puts a DtdlInterface there.
    private static DtdlContent? HeartbeatContent(DeviceConfiguration config) =>
        (config.DtdlInterface as DtdlInterface)?.Contents
            .FirstOrDefault(c => string.Equals(c.Id, Heartbeat.Dtmi, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void HeartbeatAppearsInTheGeneratedInterface_WithoutAnyDtmiInConfiguration()
    {
        var config = CreateConfig(BareHeartbeat(), Cpu());

        config.InitializeDtdl();

        HeartbeatContent(config).ShouldNotBeNull();
    }

    [Fact]
    public void HeartbeatTelemetryIsDeclaredAsBoolean()
    {
        // The SDK sends a constant true, so this is the one schema that can be correct.
        var config = CreateConfig(BareHeartbeat());

        config.InitializeDtdl();

        HeartbeatContent(config)!.Schema.ShouldBe("boolean");
    }

    [Fact]
    public void HeartbeatTelemetryIsEmittedAsOrdinaryTelemetry()
    {
        var config = CreateConfig(BareHeartbeat());

        config.InitializeDtdl();

        var content = HeartbeatContent(config)!;
        content.Type.ShouldBe("Telemetry");
        content.Name.ShouldBe(Heartbeat.SensorName);
    }

    [Fact]
    public void TheSensorCarriesTheReservedDtmiAfterInitialization()
    {
        // The capability upload sends Sensor.Dtmi, and it must resolve against the
        // Telemetry @id emitted above.
        var config = CreateConfig(BareHeartbeat());

        config.InitializeDtdl();

        config.Sensors.Single(s => s.Name == Heartbeat.SensorName).Dtmi.ShouldBe(Heartbeat.Dtmi);
    }

    [Fact]
    public void OrdinarySensorsStillGetDeviceScopedAutogenDtmis()
    {
        // The heartbeat is the one sensor with a fixed platform dtmi; nothing else changes.
        var config = CreateConfig(BareHeartbeat(), Cpu());

        config.InitializeDtdl();

        var cpu = config.Sensors.Single(s => s.Name == "cpu_usage");
        cpu.Dtmi.ShouldStartWith("dtmi:sub:TestDevice");
        cpu.Dtmi.ShouldNotBe(Heartbeat.Dtmi);
    }

    [Fact]
    public void AConfiguredSchemaDoesNotSurviveOntoTheHeartbeat()
    {
        var heartbeat = BareHeartbeat();
        heartbeat.SensorInfo = new SensorInfo { Schema = "double" };
        var config = CreateConfig(heartbeat);

        config.InitializeDtdl();

        HeartbeatContent(config)!.Schema.ShouldBe("boolean");
    }

    [Fact]
    public void ADeviceWithNoHeartbeatGeneratesNoHeartbeatTelemetry()
    {
        // Declaring the sensor is the opt-in; nothing is emitted without it.
        var config = CreateConfig(Cpu());

        config.InitializeDtdl();

        HeartbeatContent(config).ShouldBeNull();
    }
}
