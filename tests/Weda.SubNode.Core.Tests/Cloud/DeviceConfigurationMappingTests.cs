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
/// Tests for the flattened wrapper Interface built by
/// DeviceConfigurationMappingExtensions: sensors from every device are merged
/// into a single Interface, so its content names must stay unique and the
/// device-namespaced DTMIs (dtmi:sub:&lt;device-cfg-key&gt;:...) must reveal
/// which device each sensor came from.
/// </summary>
public class DeviceConfigurationMappingTests
{
    private static DeviceConfiguration MakeDevice(string deviceName, params string[] sensorNames)
    {
        var config = new DeviceConfiguration
        {
            DeviceName = deviceName,
            DeviceId = $"{deviceName}-id",
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            SubNodeInfo = new SubNodeInfo { Name = "TestSubNode" },
            Sensors = [.. sensorNames.Select(n => new Sensor
            {
                Name = n,
                SensorGroup = SensorGroup.AI,
                SensorInfo = new SensorInfo { Schema = "double" }
            })]
        };

        config.InitializeDtdl();
        return config;
    }

    [Fact]
    public void ToConfigurationDto_Should_NamespaceTelemetryIdsPerDevice()
    {
        // Arrange
        var configs = new DeviceConfigurations
        {
            ["DeviceA"] = MakeDevice("DeviceA", "channel.0"),
            ["DeviceB"] = MakeDevice("DeviceB", "humidity")
        };

        // Act
        var dto = configs.ToConfigurationDto();

        // Assert
        var contents = dto.Dtdl["contents"].ShouldBeOfType<JsonArray>();
        var ids = contents.Select(c => c!["@id"]!.GetValue<string>()).ToList();

        ids.ShouldContain(id => id.StartsWith("dtmi:sub:DeviceA:ai:"));
        ids.ShouldContain(id => id.StartsWith("dtmi:sub:DeviceB:ai:"));
    }

    [Fact]
    public void ToConfigurationDto_Should_KeepWrapperContentNamesUniqueAcrossDevices()
    {
        // Two devices exposing a same-named sensor: their DTMIs differ (device
        // namespace) so @id dedup no longer collapses them — the wrapper must
        // still not emit duplicate content names (invalid DTDL). First device
        // wins, matching the previous @id-dedup behavior.
        var configs = new DeviceConfigurations
        {
            ["DeviceA"] = MakeDevice("DeviceA", "channel.0", "a_only"),
            ["DeviceB"] = MakeDevice("DeviceB", "channel.0", "b_only")
        };

        // Act
        var dto = configs.ToConfigurationDto();

        // Assert
        var contents = dto.Dtdl["contents"].ShouldBeOfType<JsonArray>();
        var names = contents.Select(c => c!["name"]!.GetValue<string>()).ToList();

        names.Count.ShouldBe(names.Distinct().Count());
        names.ShouldContain("a_only");
        names.ShouldContain("b_only");

        var channel0 = contents.Single(c => c!["name"]!.GetValue<string>() == "channel_0");
        channel0!["@id"]!.GetValue<string>().ShouldStartWith("dtmi:sub:DeviceA:ai:");
    }
}
