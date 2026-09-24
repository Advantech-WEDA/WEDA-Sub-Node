using System.Text.Json;

using Shouldly;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;

using Xunit;

namespace Weda.SubNode.Core.Tests.Configuration;

/// <summary>
/// Pins the wire name of the devicecfg apply-outcome section to lowercase "message".
/// It is written on two paths — the DTO property and the raw-JSON writer in
/// SetRawDeviceCfgWithMessage — which previously disagreed on casing, so a report built
/// from raw JSON carried both "Message" and a stale "message" copied from the desired state.
/// </summary>
public class DeviceCfgMessageKeyTests
{
    private static ConfigUpdateMessageDto SampleMessage() => new()
    {
        Status = ConfigUpdateStatus.Invalid,
        ErrorMessage = "sensor 'cpu' refused",
        LastUpdateTime = DateTimeOffset.Parse("2026-09-21T09:31:45Z")
    };

    private static SubNodeInfo SampleSubNodeInfo() => new()
    {
        Name = "TestSubNode",
        DeviceId = "test-subnode-001",
        Manufacturer = "Advantech",
        Model = "TestModel",
        SwVersion = "1.0.0",
        SubNodeType = SubNodeType.SystemMonitor
    };

    [Fact]
    public void DeviceCfgDto_SerializesTheMessageSection_AsLowercase()
    {
        var dto = new SubNodeDeviceCfgDto { Message = SampleMessage() };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto));

        doc.RootElement.TryGetProperty("message", out var section).ShouldBeTrue(
            "The apply-outcome section must be serialized as lowercase \"message\"");
        doc.RootElement.TryGetProperty("Message", out _).ShouldBeFalse(
            "The capitalized spelling must not be emitted");
        section.GetProperty("status").GetString().ShouldBe(ConfigUpdateStatus.Invalid);
    }

    [Fact]
    public void SetRawDeviceCfgWithMessage_WritesTheMessageSection_AsLowercase()
    {
        var raw = JsonDocument.Parse("""{"deviceConfigs":{"dev":{"enabled":true}}}""").RootElement;
        var reported = new SubNodeReportedConfig();

        reported.SetRawDeviceCfgWithMessage(raw, SampleMessage(), SampleSubNodeInfo());

        var devicecfg = ReportedDeviceCfg(reported);
        devicecfg.TryGetProperty("message", out var section).ShouldBeTrue();
        devicecfg.TryGetProperty("Message", out _).ShouldBeFalse();
        section.GetProperty("status").GetString().ShouldBe(ConfigUpdateStatus.Invalid);
        section.GetProperty("errorMessage").GetString().ShouldBe("sensor 'cpu' refused");
    }

    [Fact]
    public void SetRawDeviceCfgWithMessage_ReplacesAnIncomingMessageSection_RatherThanDuplicatingIt()
    {
        // The raw JSON is a copy of the last desired payload, which carries the cloud's own
        // message stamp. Copying it through would leave two sections differing only in case.
        var raw = JsonDocument.Parse(
            """{"message":{"lastUpdateTime":"2026-09-21T08:57:05+00:00"},"deviceConfigs":{}}""").RootElement;
        var reported = new SubNodeReportedConfig();

        reported.SetRawDeviceCfgWithMessage(raw, SampleMessage(), SampleSubNodeInfo());

        var devicecfg = ReportedDeviceCfg(reported);
        var messageKeys = devicecfg.EnumerateObject()
            .Where(p => string.Equals(p.Name, "message", StringComparison.OrdinalIgnoreCase))
            .ToList();

        messageKeys.Count.ShouldBe(1, "Exactly one message section must survive");
        messageKeys[0].Name.ShouldBe("message");
        messageKeys[0].Value.GetProperty("status").GetString().ShouldBe(ConfigUpdateStatus.Invalid,
            "The device's own apply outcome must win, not the stale desired stamp");
    }

    [Fact]
    public void ReportedDeviceCfg_SerializesEveryKey_InCamelCase()
    {
        // The reported devicecfg reaches the cloud two ways: copied from the raw desired
        // JSON, which is camelCase already, or rebuilt from these DTOs when no raw JSON is
        // available. The second path used to emit PascalCase, so the same section arrived
        // in one of two shapes depending on which path produced it.
        var dto = new SubNodeDeviceCfgDto
        {
            Message = SampleMessage(),
            SubNode = new SubNodeInfoDto { Name = "AdvantechWedaMainboardExtension" },
            DeviceConfigs = new Dictionary<string, SubNodeDeviceConfigDto>
            {
                ["systemAgentDeviceConfig"] = new()
                {
                    Enabled = true,
                    Periods = new SubNodePeriodsDto { ReportHealth = 60000, ReportConfiguration = 300000 },
                    Sensors =
                    [
                        new SubNodeSensorReportDto
                        {
                            Name = "cpu_usage",
                            SensorGroup = "SYS",
                            Report = new SubNodeSensorRuntimeConfigDto { Enabled = true, Interval = 5000 }
                        }
                    ]
                }
            }
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto));

        var pascal = PascalCaseKeys(doc.RootElement, "devicecfg").ToList();
        pascal.ShouldBeEmpty(
            $"Every reported devicecfg key must be camelCase; found: {string.Join(", ", pascal)}");
    }

    /// <summary>
    /// Walks the payload and yields the path of every object key starting with an
    /// uppercase letter. Dictionary keys that are device names are not wire names, so
    /// the deviceConfigs map's own keys are skipped.
    /// </summary>
    private static IEnumerable<string> PascalCaseKeys(JsonElement element, string path)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var isDeviceNameKey = path.EndsWith("deviceConfigs", StringComparison.Ordinal);

                    if (!isDeviceNameKey && prop.Name.Length > 0 && char.IsUpper(prop.Name[0]))
                        yield return $"{path}.{prop.Name}";

                    foreach (var found in PascalCaseKeys(prop.Value, $"{path}.{prop.Name}"))
                        yield return found;
                }
                break;

            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var found in PascalCaseKeys(item, $"{path}[{i}]"))
                        yield return found;
                    i++;
                }
                break;
        }
    }

    private static JsonElement ReportedDeviceCfg(SubNodeReportedConfig reported)
    {
        var json = JsonSerializer.Serialize(reported);
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("devicecfg");
    }
}
