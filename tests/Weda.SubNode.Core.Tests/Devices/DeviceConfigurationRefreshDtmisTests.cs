using Shouldly;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace Weda.SubNode.Core.Tests.Devices;

/// <summary>
/// Regression tests for <see cref="DeviceConfiguration.RefreshSensorDtmis"/>.
///
/// A cloud configuration update rebuilds sensors from the cloud's desired state, which carries
/// dtmi=null — the mapping layer then substitutes an autogen dtmi:sub:{device}:{group}:{hash}
/// fallback. DeviceBase called InitializeDtdl to repair those dtmis, but that method is idempotent
/// and silently no-oped, so a strongly-typed device reported autogen dtmis to dmagent
/// (e.g. dtmi:sub:SystemAgentDeviceConfig:sys:76c353c7;1) instead of its catalog sensor type dtmi
/// (dtmi:advantech:weda:sensor:system_monitor_network_metric;1).
///
/// These tests share the global TypedSensorDispatch.Resolve hook, so they run in one collection.
/// </summary>
[Collection("TypedSensorDispatch")]
public class DeviceConfigurationRefreshDtmisTests
{
    private const string DeviceType = "system-monitor";
    private const string CatalogDtmi = "dtmi:advantech:weda:sensor:system_monitor_network_metric;1";

    private static DeviceConfiguration CreateConfig(bool typed, bool autoGen = true)
    {
        var config = new DeviceConfiguration
        {
            DeviceName = "SystemAgentDeviceConfig",
            DeviceTypeName = typed ? DeviceType : null,
            Dtdl = new DtdlConfig { AutoGenEnabled = autoGen },
            SubNodeInfo = new SubNodeInfo
            {
                Name = "TestSubNode",
                AutoGenEnabled = autoGen,
                SubNodeType = SubNodeType.CustomDevice
            }
        };
        config.Sensors.Add(MakeSensor("network_bytes_received"));
        return config;
    }

    private static Sensor MakeSensor(string name, string? dtmi = null) => new()
    {
        Name = name,
        Dtmi = dtmi,
        SensorGroup = SensorGroup.SYS,
        SensorInfo = new SensorInfo { Schema = "long", DisplayName = name },
        Report = new SensorReport { Interval = 5000 }
    };

    /// <summary>Installs a stub typed-dispatch hook and restores the previous one.</summary>
    private static IDisposable UseResolveHook(Func<string, Sensor, string> resolve)
    {
        var previous = TypedSensorDispatch.Resolve;
        TypedSensorDispatch.Resolve = resolve;
        return new Restore(() => TypedSensorDispatch.Resolve = previous);
    }

    private sealed class Restore(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    // --- The reported defect ---

    [Fact]
    public void RefreshSensorDtmis_TypedDevice_AfterSensorsReplaced_ResolvesCatalogDtmis()
    {
        using var _ = UseResolveHook((_, _) => CatalogDtmi);

        var config = CreateConfig(typed: true);
        config.InitializeDtdl();
        config.Sensors[0].Dtmi.ShouldBe(CatalogDtmi);

        // A cloud update replaces the sensor list; the rebuilt sensor carries the autogen
        // fallback dtmi that ConfigurationUpdateHelper.MapToSensor substitutes for dtmi=null.
        config.Sensors =
        [
            MakeSensor("network_bytes_received", "dtmi:sub:SystemAgentDeviceConfig:sys:76c353c7;1")
        ];

        var refreshed = config.RefreshSensorDtmis();

        refreshed.ShouldBeTrue();
        config.Sensors[0].Dtmi.ShouldBe(CatalogDtmi);
    }

    [Fact]
    public void InitializeDtdl_AfterSensorsReplaced_DoesNotRepairDtmis()
    {
        // Documents why RefreshSensorDtmis exists: InitializeDtdl is idempotent and no-ops here.
        using var _ = UseResolveHook((_, _) => CatalogDtmi);

        var config = CreateConfig(typed: true);
        config.InitializeDtdl();

        const string autogen = "dtmi:sub:SystemAgentDeviceConfig:sys:76c353c7;1";
        config.Sensors = [MakeSensor("network_bytes_received", autogen)];

        config.InitializeDtdl();

        config.Sensors[0].Dtmi.ShouldBe(autogen);
    }

    [Fact]
    public void RefreshSensorDtmis_RegeneratesInterfaceForTheNewSensorSet()
    {
        using var _ = UseResolveHook((_, _) => CatalogDtmi);

        var config = CreateConfig(typed: true);
        config.InitializeDtdl();
        var before = config.DtdlInterface;

        config.Sensors = [MakeSensor("network_bytes_received"), MakeSensor("network_packets_sent")];
        config.RefreshSensorDtmis().ShouldBeTrue();

        config.DtdlInterface.ShouldNotBeNull();
        config.DtdlInterface.ShouldNotBeSameAs(before);
    }

    // --- Guards ---

    [Fact]
    public void RefreshSensorDtmis_BeforeInitializeDtdl_ReturnsFalse()
    {
        using var _ = UseResolveHook((_, _) => CatalogDtmi);

        var config = CreateConfig(typed: true);

        // InitializeDtdl owns first-time setup; refresh must not pre-empt it.
        config.RefreshSensorDtmis().ShouldBeFalse();
        config.DtdlInterface.ShouldBeNull();
    }

    [Fact]
    public void RefreshSensorDtmis_ManualDtdlMode_ReturnsFalseAndLeavesDtmisUntouched()
    {
        // Manual mode: dtmis are author-specified and the Interface comes from DtdlPath,
        // so there is nothing to regenerate.
        var config = CreateConfig(typed: false, autoGen: false);
        config.Sensors = [MakeSensor("network_bytes_received", "dtmi:my:hand:written;1")];
        config.DtdlInterface = new object();
        typeof(DeviceConfiguration)
            .GetField("_dtdlInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(config, true);

        config.RefreshSensorDtmis().ShouldBeFalse();
        config.Sensors[0].Dtmi.ShouldBe("dtmi:my:hand:written;1");
    }

    [Fact]
    public void RefreshSensorDtmis_UntypedAutogenDevice_RegeneratesAutogenDtmis()
    {
        var previous = TypedSensorDispatch.Resolve;
        TypedSensorDispatch.Resolve = null;
        try
        {
            var config = CreateConfig(typed: false);
            config.InitializeDtdl();

            config.Sensors = [MakeSensor("network_packets_sent")];
            config.RefreshSensorDtmis().ShouldBeTrue();

            // Autogen path populates a dtmi for the new sensor rather than leaving it null.
            config.Sensors[0].Dtmi.ShouldNotBeNullOrEmpty();
        }
        finally
        {
            TypedSensorDispatch.Resolve = previous;
        }
    }

    // --- Resilience ---

    [Fact]
    public void RefreshSensorDtmis_WhenOneSensorFailsToResolve_OthersStillResolve()
    {
        using var _ = UseResolveHook((_, sensor) =>
            sensor.Name == "broken"
                ? throw new InvalidOperationException("no matching sensor type")
                : CatalogDtmi);

        var config = CreateConfig(typed: true);
        config.InitializeDtdl();

        config.Sensors = [MakeSensor("broken", "autogen-kept"), MakeSensor("network_bytes_received")];

        config.RefreshSensorDtmis().ShouldBeTrue();

        // The failing sensor keeps an addressable autogen dtmi; the healthy one resolves.
        config.Sensors[0].Dtmi.ShouldNotBeNullOrEmpty();
        config.Sensors[1].Dtmi.ShouldBe(CatalogDtmi);
    }

    [Fact]
    public void RefreshSensorDtmis_IsRepeatable()
    {
        using var _ = UseResolveHook((_, _) => CatalogDtmi);

        var config = CreateConfig(typed: true);
        config.InitializeDtdl();

        config.RefreshSensorDtmis().ShouldBeTrue();
        config.RefreshSensorDtmis().ShouldBeTrue();
        config.Sensors[0].Dtmi.ShouldBe(CatalogDtmi);
    }
}
