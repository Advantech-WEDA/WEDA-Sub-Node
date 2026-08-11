using System.Buffers;
using System.Text;

using Shouldly;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Cloud.Serialization;

using Xunit;

namespace Weda.SubNode.Integration.Tests.Cloud;

/// <summary>
/// Regression tests for the registry SUBSCRIPTIONS actually use. These must go
/// through <see cref="WedaNatsSerializerRegistry.Default"/>.GetDeserializer (the
/// live NATS path), NOT <see cref="WedaNatsSerializerRegistry.DefaultOptions"/>
/// directly: a static-field-initialization-order bug once left the Default
/// instance holding null options, so the live path deserialized case-SENSITIVELY
/// while every options-based test kept passing — cloud config updates bound
/// DeviceConfigs = null and devices skipped every update while reporting success.
/// </summary>
public class WedaNatsSerializerRegistryTests
{
    private static SubNodeConfigUpdateMessage DeserializeViaRegistry(string json)
    {
        var deserializer = WedaNatsSerializerRegistry.Default
            .GetDeserializer<SubNodeConfigUpdateMessage>();
        var buffer = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(json));
        return deserializer.Deserialize(buffer)!;
    }

    private const string PascalDelta = """
        {"seqId":1,"data":{"cfg":{"desired":{"devicecfg":{"DeviceConfigs":{"MyFirstDeviceConfig":{"Enabled":true,"Sensors":[{"Name":"temperature_sensor","SensorGroup":"TEMP","Report":{"Enabled":true,"Interval":2000}}]}}}}}}}
        """;

    private const string CamelDelta = """
        {"seqId":2,"data":{"cfg":{"desired":{"devicecfg":{"deviceConfigs":{"MyFirstDeviceConfig":{"enabled":true,"sensors":[{"name":"temperature_sensor","sensorGroup":"TEMP","report":{"enabled":true,"interval":1000}}]}}}}}}}
        """;

    [Fact]
    public void Registry_Default_DeserializesPascalCaseDelta()
    {
        // Local payloads (devicecfg.json-shaped test messages) are PascalCase.
        var msg = DeserializeViaRegistry(PascalDelta);

        var dc = msg.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
        dc.ShouldNotBeNull();
        dc!.TryGetValue("MyFirstDeviceConfig", out var cfg).ShouldBeTrue();
        cfg!.Sensors.ShouldNotBeNull();
        cfg.Sensors![0].Report.ShouldNotBeNull();
        cfg.Sensors[0].Report!.Interval.ShouldBe(2000);
    }

    [Fact]
    public void Registry_Default_DeserializesCamelCaseDelta()
    {
        // The schemaVersion=2 cloud wire format is camelCase.
        var msg = DeserializeViaRegistry(CamelDelta);

        var dc = msg.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs;
        dc.ShouldNotBeNull();
        dc!.TryGetValue("MyFirstDeviceConfig", out var cfg).ShouldBeTrue();
        cfg!.Sensors![0].Report!.Interval.ShouldBe(1000);
    }
}
