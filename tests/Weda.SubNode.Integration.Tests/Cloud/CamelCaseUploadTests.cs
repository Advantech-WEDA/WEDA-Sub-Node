using System.Text.Json;

using Shouldly;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Cloud.Serialization;

using Xunit;

namespace Weda.SubNode.Integration.Tests.Cloud;

/// <summary>
/// Proves that, regardless of whether a device's devicecfg.json is authored in
/// camelCase OR PascalCase, the configuration payload the SDK uploads/reports is
/// STRICT camelCase on the wire (every object key, recursively). The cloud-side
/// validator for schemaVersion=2 rejects any PascalCase key.
/// </summary>
/// <remarks>
/// <para>
/// Map-entry-key decision (documented + asserted below): a raw <see cref="JsonElement"/>
/// carries no schema, so <see cref="CamelCaseJsonNormalizer"/> cannot generically tell a
/// structural property key (e.g. <c>deviceConfigs</c>) from a data key whose value is a
/// dictionary entry (e.g. a device-config entry name such as <c>SystemAgentDeviceConfig</c>,
/// or an arbitrary <c>Metadata</c>/<c>DeviceInfo</c> key). We deliberately transform ALL
/// object keys. This is the desired behavior: the cloud validator wants camelCase
/// everywhere, downstream device-name lookups are case-insensitive, and
/// <c>JsonNamingPolicy.CamelCase.ConvertName</c> only lowercases the leading upper-case run
/// (<c>SystemAgentDeviceConfig</c> -> <c>systemAgentDeviceConfig</c>), leaving embedded
/// casing and underscores intact. The device-name-key transform is asserted explicitly in
/// <see cref="Normalizer_transforms_map_entry_keys_as_data"/>.
/// </para>
/// </remarks>
public class CamelCaseUploadTests
{
    private const string PascalCasePayload = """
    {
      "DeviceConfigs": {
        "SystemAgentDeviceConfig": {
          "Sensors": [
            {
              "Name": "cpu_usage",
              "Parameters": { "MetricType": "cpu", "MetricName": "usage" },
              "Report": { "Interval": 5000 }
            }
          ]
        }
      },
      "SubNode": { "Name": "x" }
    }
    """;

    /// <summary>
    /// A key is in camelCase iff normalizing it is a no-op, i.e.
    /// <c>ConvertName(key) == key</c>. Used to assert every object key recursively.
    /// </summary>
    private static bool IsCamelCase(string key) =>
        JsonNamingPolicy.CamelCase.ConvertName(key) == key;

    private static void AssertAllKeysCamelCase(JsonElement element, string path = "$")
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    IsCamelCase(prop.Name).ShouldBeTrue(
                        $"Key '{prop.Name}' at {path} is not camelCase");
                    AssertAllKeysCamelCase(prop.Value, $"{path}.{prop.Name}");
                }
                break;

            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in element.EnumerateArray())
                {
                    AssertAllKeysCamelCase(item, $"{path}[{i++}]");
                }
                break;
        }
    }

    [Fact]
    public void Normalizer_converts_all_object_keys_to_camelCase_recursively()
    {
        var result = CamelCaseJsonNormalizer.Normalize(PascalCasePayload);

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        // Every structural key was transformed, at every depth.
        AssertAllKeysCamelCase(root);

        // Spot-check the exact keys the cloud validator expects.
        root.TryGetProperty("deviceConfigs", out var deviceConfigs).ShouldBeTrue();
        root.TryGetProperty("subNode", out var subNode).ShouldBeTrue();
        subNode.GetProperty("name").GetString().ShouldBe("x");

        var device = deviceConfigs.GetProperty("systemAgentDeviceConfig");
        var sensor = device.GetProperty("sensors")[0];
        sensor.GetProperty("name").GetString().ShouldBe("cpu_usage");

        var parameters = sensor.GetProperty("parameters");
        parameters.TryGetProperty("metricType", out var metricType).ShouldBeTrue();
        parameters.TryGetProperty("metricName", out var metricName).ShouldBeTrue();
        metricType.GetString().ShouldBe("cpu");
        metricName.GetString().ShouldBe("usage");

        sensor.GetProperty("report").GetProperty("interval").GetInt32().ShouldBe(5000);
    }

    [Fact]
    public void Normalizer_preserves_scalar_values_and_array_order()
    {
        const string json = """
        { "Items": [ { "V": 3 }, { "V": 1 }, { "V": 2 } ], "Flag": true, "Text": "AsIs" }
        """;

        var result = CamelCaseJsonNormalizer.Normalize(json);
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        // Array element order preserved.
        var items = root.GetProperty("items");
        items[0].GetProperty("v").GetInt32().ShouldBe(3);
        items[1].GetProperty("v").GetInt32().ShouldBe(1);
        items[2].GetProperty("v").GetInt32().ShouldBe(2);

        // Scalar values (including a PascalCase-looking string VALUE) untouched.
        root.GetProperty("flag").GetBoolean().ShouldBeTrue();
        root.GetProperty("text").GetString().ShouldBe("AsIs");
    }

    [Fact]
    public void Normalizer_is_idempotent_on_already_camelCase_payload()
    {
        // First pass produces canonical camelCase JSON.
        var once = CamelCaseJsonNormalizer.Normalize(PascalCasePayload);

        // Feeding an already-camelCase payload yields identical output.
        var twice = CamelCaseJsonNormalizer.Normalize(once);

        twice.ShouldBe(once);
    }

    [Fact]
    public void Normalizer_transforms_map_entry_keys_as_data()
    {
        // Documented trade-off: device-config entry names (map keys) are transformed
        // too, because they are indistinguishable from structural keys on a raw
        // JsonElement. ConvertName only lowercases the leading upper-case run.
        var result = CamelCaseJsonNormalizer.Normalize(PascalCasePayload);
        using var doc = JsonDocument.Parse(result);

        var deviceConfigs = doc.RootElement.GetProperty("deviceConfigs");
        deviceConfigs.TryGetProperty("systemAgentDeviceConfig", out _).ShouldBeTrue();
        // Original PascalCase key must be gone.
        deviceConfigs.TryGetProperty("SystemAgentDeviceConfig", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("pascal")]
    [InlineData("camel")]
    public void Report_upload_chokepoint_produces_camelCase_for_both_input_casings(string casing)
    {
        // Author the raw devicecfg in the requested casing. This is the exact
        // JsonElement that flows through SetRawDeviceCfgWithMessage on the report path.
        var authored = casing == "pascal"
            ? """
              {
                "SubNode": { "Name": "x", "AutoGenEnabled": true },
                "DeviceConfigs": {
                  "SystemAgentDeviceConfig": {
                    "Enabled": true,
                    "Sensors": [
                      {
                        "Name": "cpu_usage",
                        "Parameters": { "MetricType": "cpu", "MetricName": "usage" },
                        "Report": { "Interval": 5000 }
                      }
                    ]
                  }
                }
              }
              """
            : """
              {
                "subNode": { "name": "x", "autoGenEnabled": true },
                "deviceConfigs": {
                  "systemAgentDeviceConfig": {
                    "enabled": true,
                    "sensors": [
                      {
                        "name": "cpu_usage",
                        "parameters": { "metricType": "cpu", "metricName": "usage" },
                        "report": { "interval": 5000 }
                      }
                    ]
                  }
                }
              }
              """;

        using var authoredDoc = JsonDocument.Parse(authored);

        var reported = new SubNodeReportedConfigSections();
        reported.SetRawDeviceCfgWithMessage(
            authoredDoc.RootElement,
            new ConfigUpdateMessageDto
            {
                Status = ConfigUpdateStatus.Success,
                LastUpdateTime = DateTimeOffset.UnixEpoch
            },
            sdkVersion: "0.0.5",
            schemaVersion: 2);

        var report = new SubNodeConfigUpdateMessage
        {
            Cmd = "updateDevConfig",
            DeviceId = "device-001",
            Data = new SubNodeConfigUpdateData
            {
                Cfg = new SubNodeConfigState { Reported = reported }
            }
        };

        // Reproduce the production choke point exactly: serialize with the registry
        // options, then normalize the bytes to camelCase.
        var bytes = CamelCaseJsonNormalizer.SerializeAndNormalize(
            report, WedaNatsSerializerRegistry.DefaultOptions);

        using var wireDoc = JsonDocument.Parse(bytes);
        var wire = wireDoc.RootElement;

        // Every key on the wire is camelCase, recursively — for BOTH input casings.
        AssertAllKeysCamelCase(wire);

        // The raw echo made it through and is camelCase.
        var devicecfg = wire
            .GetProperty("data")
            .GetProperty("cfg")
            .GetProperty("reported")
            .GetProperty("devicecfg");

        var subNode = devicecfg.GetProperty("subNode");
        subNode.GetProperty("name").GetString().ShouldBe("x");

        // The SDK-injected version keys survive and stay camelCase (idempotent).
        subNode.GetProperty("sdkVersion").GetString().ShouldBe("0.0.5");
        subNode.GetProperty("schemaVersion").GetInt32().ShouldBe(2);

        // Dictionary keys under Parameters are camelCase.
        var parameters = devicecfg
            .GetProperty("deviceConfigs")
            .GetProperty("systemAgentDeviceConfig")
            .GetProperty("sensors")[0]
            .GetProperty("parameters");
        parameters.TryGetProperty("metricType", out _).ShouldBeTrue();
        parameters.TryGetProperty("metricName", out _).ShouldBeTrue();

        // The injected Message envelope is camelCase too.
        devicecfg.GetProperty("message").GetProperty("status").GetString()
            .ShouldBe(ConfigUpdateStatus.Success);
    }
}
