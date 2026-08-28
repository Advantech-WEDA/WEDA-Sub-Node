using System.Text.Json.Nodes;

using Shouldly;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Cloud.Clients.DeviceManagement.Mapping;

using Xunit;

namespace Weda.SubNode.Core.Tests.Cloud;

/// <summary>
/// Pins that the reserved heartbeat's dtmi is <em>defined</em> in the capability upload's
/// <c>refModelsMap.configs</c>, not merely referenced.
/// </summary>
/// <remarks>
/// <para>
/// Appearing as a Telemetry <c>@id</c> inside the SubNode wrapper Interface is not enough:
/// a sensor's dtmi is resolved against the uploaded catalog, and every ordinary sensor gets
/// there via <c>SensorTypeRegistry</c>. The heartbeat is deliberately skipped by typed
/// dispatch, so it never entered that registry and its dtmi resolved to nothing.
/// </para>
/// <para>
/// These assert against <c>refModelsMap</c> rather than <c>refModels</c>: the latter is
/// obsolete and always uploaded empty, so a test written against it would pass while the
/// live payload stayed broken.
/// </para>
/// </remarks>
public class HeartbeatRefModelTests
{
    private const string DeviceId = "74fe488d5d54";

    private static DeviceConfigurations Configs(params Sensor[] sensors)
    {
        var config = new DeviceConfiguration
        {
            DeviceName = "SystemAgentDeviceConfig",
            DeviceId = DeviceId,
            Enabled = true,
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            SubNodeInfo = new SubNodeInfo
            {
                Name = "system-agent",
                DeviceId = DeviceId,
                AutoGenEnabled = true,
                SubNodeType = SubNodeType.CustomDevice
            }
        };

        foreach (var sensor in sensors)
            config.Sensors.Add(sensor);

        config.InitializeDtdl();

        return new DeviceConfigurations { [config.DeviceName] = config };
    }

    private static Sensor Heartbeat_() => new()
    {
        Name = Heartbeat.SensorName,
        ResourceId = "21af0dc4-9254-5389-a7dd-df64d7cf782c",
        SensorGroup = SensorGroup.SYS,
        Report = new SensorReport { Enabled = true, Interval = 60_000 }
    };

    private static Sensor Cpu() => new()
    {
        Name = "cpu_usage",
        ResourceId = "31af0dc4-9254-5389-a7dd-df64d7cf7999",
        SensorGroup = SensorGroup.SYS,
        SensorInfo = new SensorInfo { Schema = "double" },
        Report = new SensorReport { Enabled = true, Interval = 5_000 }
    };

    private static List<string> RefModelIds(DeviceConfigurations configs) =>
        configs.ToConfigurationDto().RefModelsMap.Configs
            .Select(m => m["@id"]?.GetValue<string>() ?? "")
            .ToList();

    [Fact]
    public void TheObsoleteRefModelsFieldStaysEmpty()
    {
        // Guards the reason these tests target refModelsMap: refModels carries no content,
        // so putting the heartbeat there would look right and ship broken.
#pragma warning disable CS0618 // deliberately asserting on the obsolete field
        Configs(Heartbeat_(), Cpu()).ToConfigurationDto().RefModels.ShouldBeEmpty();
#pragma warning restore CS0618
    }

    [Fact]
    public void RefModelsDefineTheHeartbeatDtmi()
    {
        RefModelIds(Configs(Heartbeat_(), Cpu())).ShouldContain(Heartbeat.Dtmi);
    }

    [Fact]
    public void TheSensorsDeclaredDtmiResolvesWithinRefModels()
    {
        // The capability upload sends this dtmi on the SensorDto; a dtmi that no refModel
        // defines is a dangling reference.
        var configs = Configs(Heartbeat_(), Cpu());
        var dto = configs.ToConfigurationDto();

        var heartbeatSensor = dto.DeviceCapabilities.Sensors
            .Single(s => s.Name == Heartbeat.SensorName);

        RefModelIds(configs).ShouldContain(heartbeatSensor.Dtmi);
    }

    [Fact]
    public void TheHeartbeatInterfaceExtendsTheSensorEnvelope_WhichIsAlsoDefined()
    {
        var configs = Configs(Heartbeat_());
        var models = configs.ToConfigurationDto().RefModelsMap.Configs;

        var heartbeat = models.Single(m => m["@id"]?.GetValue<string>() == Heartbeat.Dtmi);
        heartbeat["extends"]!.GetValue<string>().ShouldBe(SensorBase.Dtmi);

        // extends is itself a reference: the base has to travel too.
        RefModelIds(configs).ShouldContain(SensorBase.Dtmi);
    }

    [Fact]
    public void TheHeartbeatInterfaceCarriesNoParameters()
    {
        // The cloud rejects a SensorRef carrying unknown fields, and the heartbeat takes no
        // configuration -- so the Interface stays a bare envelope.
        var heartbeat = Heartbeat.GetInterface();

        heartbeat["contents"].ShouldBeNull();
    }

    [Fact]
    public void NoHeartbeatDeclared_MeansNoHeartbeatRefModel()
    {
        RefModelIds(Configs(Cpu())).ShouldNotContain(Heartbeat.Dtmi);
    }

    [Fact]
    public void TheHeartbeatRefModelIsNotDuplicated()
    {
        var ids = RefModelIds(Configs(Heartbeat_(), Cpu()));

        ids.Count(id => id == Heartbeat.Dtmi).ShouldBe(1);
        ids.Count(id => id == SensorBase.Dtmi).ShouldBe(1);
    }
}
