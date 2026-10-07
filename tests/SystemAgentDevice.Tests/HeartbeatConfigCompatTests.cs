using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Configuration;

using Shouldly;
using Xunit;

using SystemAgentExample.Communication;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.TestBase;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Regression tests for devicecfg.json files deployed with an incomplete heartbeat, which
/// cannot be corrected on disk.
///
/// <para>
/// The setup wizard template declares <c>hb</c> without <c>SensorInfo.Schema</c>. The SDK stamps
/// the schema onto the in-memory sensor, but two paths read the raw file instead: a cloud config
/// reset re-validates devicecfg.json as written, and SensorsValidator rejected it with
/// "Sensor 'hb': Schema is required"; and the reported devicecfg echoes the raw file, which SIT
/// rejected with 502 "SystemAgentDeviceConfig/hb: sensorInfo.schema is missing".
/// </para>
/// <para>
/// A devicecfg.json that declares no <c>hb</c> at all gets the default heartbeat, so the
/// SubNode never runs without a liveness signal.
/// </para>
/// </summary>
public class HeartbeatConfigCompatTests
{
    private const string DeviceName = "SystemAgentDeviceConfig";

    private static readonly JsonSerializerOptions CaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>The hb block exactly as the setup wizard ships it, beside an ordinary sensor.</summary>
    private const string WizardDeviceCfg = """
        {
          "SubNode": {
            "Name": "AdvantechWedaMainboardExtension",
            "SubNodeType": "SystemMonitor",
            "Manufacturer": "Advantech",
            "Model": "SystemAgent",
            "SwVersion": "1.0.0"
          },
          "DeviceConfigs": {
            "SystemAgentDeviceConfig": {
              "Enabled": true,
              "Dtdl": { "AutoGenEnabled": true },
              "Sensors": [
                {
                  "Name": "hb",
                  "SensorGroup": "SYS",
                  "Report": { "Enabled": true, "Interval": 60000 },
                  "Record": { "Enabled": false },
                  "SensorInfo": {
                    "Description": "Reserved platform liveness ping.",
                    "DisplayName": "Heartbeat"
                  }
                },
                {
                  "Name": "cpu_usage",
                  "SensorGroup": "SYS",
                  "Parameters": { "MetricType": "cpu", "MetricName": "usage" },
                  "Report": { "Enabled": true, "Interval": 10000 },
                  "SensorInfo": { "Schema": "double", "DisplayName": "CPU Usage" }
                }
              ]
            }
          }
        }
        """;

    /// <summary>A devicecfg.json that predates the heartbeat: no hb sensor at all.</summary>
    private const string NoHeartbeatDeviceCfg = """
        {
          "DeviceConfigs": {
            "SystemAgentDeviceConfig": {
              "Enabled": true,
              "Dtdl": { "AutoGenEnabled": true },
              "Sensors": [
                {
                  "Name": "cpu_usage",
                  "SensorGroup": "SYS",
                  "Parameters": { "MetricType": "cpu", "MetricName": "usage" },
                  "Report": { "Enabled": true, "Interval": 10000 },
                  "SensorInfo": { "Schema": "double", "DisplayName": "CPU Usage" }
                }
              ]
            }
          }
        }
        """;

    private static JsonElement ParseElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Builds the message SubNodeManager.HandleDeviceConfigResetAsync synthesises: the base
    /// devicecfg.json deserialised case-insensitively, with the raw file kept for the echo.
    /// </summary>
    private static SubNodeConfigUpdateMessage ResetMessage(string rawDeviceCfg) => new()
    {
        DeviceId = "74fe488d5d54",
        GroupId = "weda",
        Cmd = "updateCmd",
        SeqId = 1,
        Data = new SubNodeConfigUpdateData
        {
            Cfg = new SubNodeConfigState
            {
                Desired = new SubNodeDesiredConfigSections
                {
                    DeviceCfg = JsonSerializer.Deserialize<SubNodeDeviceCfgDto>(rawDeviceCfg, CaseInsensitive),
                    RawDeviceCfg = ParseElement(rawDeviceCfg)
                }
            }
        }
    };

    private static SystemAgentDeviceBase CreateDevice(JsonElement? rawDeviceCfg = null)
    {
        var configuration = new DeviceConfiguration
        {
            DeviceName = DeviceName,
            Dtdl = new DtdlConfig { AutoGenEnabled = true },
            RawDeviceCfgJson = rawDeviceCfg,
            Sensors =
            [
                new Sensor
                {
                    Name = Heartbeat.SensorName,
                    Report = new SensorReport { Enabled = true, Interval = 60000 }
                },
                new Sensor
                {
                    Name = "cpu_usage",
                    Parameters = new Dictionary<string, object>
                    {
                        ["MetricType"] = "cpu",
                        ["MetricName"] = "usage"
                    },
                    SensorInfo = new SensorInfo { Schema = "double" },
                    Report = new SensorReport { Enabled = true, Interval = 10000 }
                }
            ]
        };

        return new SystemAgentDeviceBase(
            new MockApplicationContext(), configuration, new LocalSystemCommunication());
    }

    /// <summary>Loads devicecfg JSON the way the host does: under the "DeviceConfig" section.</summary>
    private static IConfigurationRoot LoadDeviceCfg(
        string deviceCfgJson, IEnumerable<KeyValuePair<string, string?>>? extra = null)
    {
        var json = $$"""{ "DeviceConfig": {{deviceCfgJson}} }""";
        var builder = new ConfigurationBuilder().AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)));
        if (extra != null)
            builder.AddInMemoryCollection(extra);
        return builder.Build();
    }

    private static JsonArray Sensors(JsonElement rawDeviceCfg)
    {
        var root = JsonNode.Parse(rawDeviceCfg.GetRawText())!.AsObject();
        return Child(Child(Child(root, "DeviceConfigs"), DeviceName), "Sensors")!.AsArray();
    }

    /// <summary>Reads the heartbeat entry from raw devicecfg JSON in either key casing.</summary>
    private static JsonNode HeartbeatEntry(JsonElement rawDeviceCfg) =>
        Sensors(rawDeviceCfg).Single(sensor => Child(sensor, "Name")?.GetValue<string>() == Heartbeat.SensorName)!;

    private static string? HeartbeatSchema(JsonElement rawDeviceCfg) =>
        Child(Child(HeartbeatEntry(rawDeviceCfg), "SensorInfo"), "Schema")?.GetValue<string>();

    private static JsonNode? Child(JsonNode? node, string name) =>
        node is JsonObject obj
            ? obj.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value
            : null;

    // ----- Device level: the paths that failed on SIT -----

    [Fact]
    public async Task ConfigReset_WizardHeartbeatWithoutSchema_PassesDeviceValidation()
    {
        // Before the fix: Invalid, "Sensor 'hb': Schema is required" -- the reset was refused.
        using var device = CreateDevice();
        var reset = ResetMessage(WizardDeviceCfg);

        var result = await device.ValidateConfigurationUpdateAsync(reset, CancellationToken.None);

        result.IsValid.ShouldBeTrue(result.ErrorMessage);
        result.IsSkipped.ShouldBeFalse();
    }

    [Fact]
    public async Task ConfigReset_WizardHeartbeatWithoutSchema_EchoesBooleanSchema()
    {
        // SubNodeManager caches this raw JSON as the device's reported devicecfg after the
        // reset applies, so it must carry the schema the cloud envelope requires.
        using var device = CreateDevice();
        var reset = ResetMessage(WizardDeviceCfg);

        await device.ValidateConfigurationUpdateAsync(reset, CancellationToken.None);

        HeartbeatSchema(reset.Data!.Cfg!.Desired!.RawDeviceCfg!.Value).ShouldBe(Heartbeat.Schema);
    }

    [Fact]
    public async Task ConfigReset_DevicecfgWithoutHeartbeat_KeepsTheHeartbeat()
    {
        // REPLACE mode would otherwise remove the running heartbeat sensor on reset.
        using var device = CreateDevice();
        var reset = ResetMessage(NoHeartbeatDeviceCfg);

        var result = await device.ValidateConfigurationUpdateAsync(reset, CancellationToken.None);

        result.IsValid.ShouldBeTrue(result.ErrorMessage);
        reset.Data!.Cfg!.Desired!.DeviceCfg!.DeviceConfigs![DeviceName].Sensors!
            .ShouldContain(s => s.Name == Heartbeat.SensorName);
        HeartbeatSchema(reset.Data.Cfg.Desired.RawDeviceCfg!.Value).ShouldBe(Heartbeat.Schema);
    }

    [Fact]
    public void Startup_WizardRawConfig_ReportsHeartbeatWithBooleanSchema()
    {
        // Before the fix SIT answered GET telemetry-configs with 502 for this device.
        using var device = CreateDevice(ParseElement(WizardDeviceCfg));

        HeartbeatSchema(device.Configuration.RawDeviceCfgJson!.Value).ShouldBe(Heartbeat.Schema);
    }

    [Fact]
    public void Startup_RawConfigWithoutHeartbeat_ReportsTheDefaultHeartbeat()
    {
        using var device = CreateDevice(ParseElement(NoHeartbeatDeviceCfg));

        HeartbeatSchema(device.Configuration.RawDeviceCfgJson!.Value).ShouldBe(Heartbeat.Schema);
    }

    // ----- Startup: devicecfg.json as the host binds it -----

    [Fact]
    public void DefaultSensorSettings_DevicecfgWithoutHeartbeat_BindsTheDefaultHeartbeat()
    {
        var defaults = HeartbeatConfigCompat.DefaultSensorSettings(LoadDeviceCfg(NoHeartbeatDeviceCfg), DeviceName);

        defaults.ShouldNotBeNull();
        var bound = LoadDeviceCfg(NoHeartbeatDeviceCfg, defaults)
            .GetSection($"DeviceConfig:DeviceConfigs:{DeviceName}").Get<DeviceConfiguration>()!;

        bound.Sensors.Select(s => s.Name).ShouldBe(new[] { "cpu_usage", Heartbeat.SensorName });
        var heartbeat = bound.Sensors[1];
        Heartbeat.FindEnabled(bound.Sensors).ShouldBeSameAs(heartbeat);
        heartbeat.Report.Interval.ShouldBe(Heartbeat.DefaultIntervalMilliseconds);
        heartbeat.Record.Enabled.ShouldBeFalse();
        heartbeat.SensorInfo.Schema.ShouldBe(Heartbeat.Schema);
        heartbeat.SensorInfo.DisplayName.ShouldBe("Heartbeat");
    }

    [Fact]
    public void DefaultSensorSettings_HeartbeatDeclared_ReturnsNothing()
    {
        HeartbeatConfigCompat.DefaultSensorSettings(LoadDeviceCfg(WizardDeviceCfg), DeviceName).ShouldBeNull();
    }

    [Fact]
    public void DefaultSensorSettings_HeartbeatDisabledInDevicecfg_LeftDisabled()
    {
        // An explicit hb with Report.Enabled=false is a choice, not an omission.
        var disabled = WizardDeviceCfg.Replace(
            "\"Report\": { \"Enabled\": true, \"Interval\": 60000 }",
            "\"Report\": { \"Enabled\": false, \"Interval\": 60000 }");
        disabled.ShouldNotBe(WizardDeviceCfg);

        HeartbeatConfigCompat.DefaultSensorSettings(LoadDeviceCfg(disabled), DeviceName).ShouldBeNull();
    }

    [Fact]
    public void DefaultSensorSettings_DeviceSectionMissing_ReturnsNothing()
    {
        HeartbeatConfigCompat.DefaultSensorSettings(LoadDeviceCfg(NoHeartbeatDeviceCfg), "OtherDevice").ShouldBeNull();
    }

    // ----- Raw devicecfg JSON (the reported echo) -----

    [Fact]
    public void TryApplyDefaults_WizardRawConfig_KeepsAuthoredFields()
    {
        HeartbeatConfigCompat.TryApplyDefaults(ParseElement(WizardDeviceCfg), DeviceName, out var patched).ShouldBeTrue();

        var heartbeat = HeartbeatEntry(patched);
        heartbeat["SensorInfo"]!["Schema"]!.GetValue<string>().ShouldBe(Heartbeat.Schema);
        heartbeat["SensorInfo"]!["DisplayName"]!.GetValue<string>().ShouldBe("Heartbeat");
        heartbeat["Report"]!["Interval"]!.GetValue<int>().ShouldBe(60000);
        Sensors(patched).Count.ShouldBe(2);
    }

    [Fact]
    public void TryApplyDefaults_NoHeartbeat_AppendsTheDefaultSensor()
    {
        HeartbeatConfigCompat.TryApplyDefaults(ParseElement(NoHeartbeatDeviceCfg), DeviceName, out var patched).ShouldBeTrue();

        var sensors = Sensors(patched);
        sensors.Select(s => s!["Name"]!.GetValue<string>()).ShouldBe(new[] { "cpu_usage", Heartbeat.SensorName });
        var heartbeat = sensors[1]!;
        heartbeat["SensorGroup"]!.GetValue<string>().ShouldBe("SYS");
        heartbeat["Parameters"]!.AsObject().Count.ShouldBe(0);
        heartbeat["Report"]!["Enabled"]!.GetValue<bool>().ShouldBeTrue();
        heartbeat["Report"]!["Interval"]!.GetValue<int>().ShouldBe(Heartbeat.DefaultIntervalMilliseconds);
        heartbeat["Record"]!["Enabled"]!.GetValue<bool>().ShouldBeFalse();
        heartbeat["SensorInfo"]!["Schema"]!.GetValue<string>().ShouldBe(Heartbeat.Schema);
    }

    [Fact]
    public void TryApplyDefaults_NoHeartbeatCamelCase_AppendsCamelCaseSensor()
    {
        // Shape of the cloud echo: camelCase keys throughout.
        var raw = ParseElement("""
            { "deviceConfigs": { "SystemAgentDeviceConfig": { "sensors": [
              { "name": "cpu_usage", "sensorInfo": { "schema": "double" } }
            ] } } }
            """);

        HeartbeatConfigCompat.TryApplyDefaults(raw, DeviceName, out var patched).ShouldBeTrue();

        var heartbeat = Sensors(patched)[1]!.AsObject();
        heartbeat["name"]!.GetValue<string>().ShouldBe(Heartbeat.SensorName);
        heartbeat["sensorInfo"]!["schema"]!.GetValue<string>().ShouldBe(Heartbeat.Schema);
        heartbeat.ContainsKey("Name").ShouldBeFalse();
    }

    [Fact]
    public void TryApplyDefaults_CamelCaseWireKeys_AddsCamelCaseSchema()
    {
        var raw = ParseElement("""
            { "deviceConfigs": { "SystemAgentDeviceConfig": { "sensors": [
              { "name": "hb", "sensorInfo": { "displayName": "Heartbeat" } }
            ] } } }
            """);

        HeartbeatConfigCompat.TryApplyDefaults(raw, DeviceName, out var patched).ShouldBeTrue();

        var sensorInfo = HeartbeatEntry(patched)["sensorInfo"]!.AsObject();
        sensorInfo["schema"]!.GetValue<string>().ShouldBe(Heartbeat.Schema);
        sensorInfo.ContainsKey("Schema").ShouldBeFalse();
    }

    [Fact]
    public void TryApplyDefaults_NoSensorInfo_AddsOne()
    {
        var raw = ParseElement("""
            { "DeviceConfigs": { "SystemAgentDeviceConfig": { "Sensors": [ { "Name": "hb" } ] } } }
            """);

        HeartbeatConfigCompat.TryApplyDefaults(raw, DeviceName, out var patched).ShouldBeTrue();

        HeartbeatSchema(patched).ShouldBe(Heartbeat.Schema);
    }

    [Fact]
    public void TryApplyDefaults_HeartbeatComplete_LeavesJsonUntouched()
    {
        // The current edge_subnode example declares everything; nothing to do.
        var raw = ParseElement("""
            { "DeviceConfigs": { "SystemAgentDeviceConfig": { "Sensors": [
              { "Name": "hb", "SensorInfo": { "Schema": "boolean" } }
            ] } } }
            """);

        HeartbeatConfigCompat.TryApplyDefaults(raw, DeviceName, out var patched).ShouldBeFalse();

        patched.GetRawText().ShouldBe(raw.GetRawText());
    }

    [Fact]
    public void TryApplyDefaults_OtherSensorWithoutSchema_LeftAsWritten()
    {
        // Only the reserved heartbeat has an SDK-owned schema; anything else is the author's mistake to see.
        var raw = ParseElement("""
            { "DeviceConfigs": { "SystemAgentDeviceConfig": { "Sensors": [
              { "Name": "cpu_usage", "SensorInfo": { "DisplayName": "CPU Usage" } }
            ] } } }
            """);

        HeartbeatConfigCompat.TryApplyDefaults(raw, DeviceName, out var patched).ShouldBeTrue();

        Child(Child(Sensors(patched)[0], "SensorInfo"), "Schema").ShouldBeNull();
    }

    [Fact]
    public void TryApplyDefaults_OtherDevice_GetsNoHeartbeat()
    {
        // The heartbeat is SubNode-level; only the system-agent device carries it.
        HeartbeatConfigCompat.TryApplyDefaults(ParseElement(NoHeartbeatDeviceCfg), "OtherDevice", out _).ShouldBeFalse();
    }

    [Fact]
    public void TryApplyDefaults_HeartbeatIdentifiedByDtmi_FilledNotDuplicated()
    {
        // Configurations written against the earlier dtmi-keyed contract name the heartbeat by dtmi.
        var raw = ParseElement($$"""
            { "DeviceConfigs": { "SystemAgentDeviceConfig": { "Sensors": [
              { "Name": "liveness", "Dtmi": "{{Heartbeat.Dtmi}}" }
            ] } } }
            """);

        HeartbeatConfigCompat.TryApplyDefaults(raw, DeviceName, out var patched).ShouldBeTrue();

        var sensors = Sensors(patched);
        sensors.Count.ShouldBe(1);
        sensors[0]!["SensorInfo"]!["Schema"]!.GetValue<string>().ShouldBe(Heartbeat.Schema);
    }

    [Fact]
    public void TryApplyDefaults_NullDeviceCfg_LeftAsResetMarker()
    {
        // A JSON null devicecfg is how the cloud requests a reset; it must survive untouched.
        HeartbeatConfigCompat.TryApplyDefaults(ParseElement("null"), DeviceName, out var patched).ShouldBeFalse();

        patched.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // ----- Desired configuration from the cloud -----

    [Fact]
    public void ApplyDefaults_DesiredDto_StampsHeartbeatOnlyAndIsIdempotent()
    {
        var message = ResetMessage(WizardDeviceCfg);
        var sensors = message.Data!.Cfg!.Desired!.DeviceCfg!.DeviceConfigs![DeviceName].Sensors!;

        HeartbeatConfigCompat.ApplyDefaults(message, DeviceName).ShouldBeTrue();

        sensors.Single(s => s.Name == Heartbeat.SensorName).SensorInfo!.Schema.ShouldBe(Heartbeat.Schema);
        sensors.Single(s => s.Name == "cpu_usage").SensorInfo!.Schema.ShouldBe("double");
        HeartbeatConfigCompat.ApplyDefaults(message, DeviceName).ShouldBeFalse();
    }

    [Fact]
    public void ApplyDefaults_DesiredWithoutHeartbeat_AddsTheDefault()
    {
        var message = ResetMessage(NoHeartbeatDeviceCfg);

        HeartbeatConfigCompat.ApplyDefaults(message, DeviceName).ShouldBeTrue();

        var heartbeat = message.Data!.Cfg!.Desired!.DeviceCfg!.DeviceConfigs![DeviceName].Sensors!
            .Single(s => s.Name == Heartbeat.SensorName);
        heartbeat.Report!.Enabled.ShouldBeTrue();
        heartbeat.Report.Interval.ShouldBe(Heartbeat.DefaultIntervalMilliseconds);
        heartbeat.Record!.Enabled.ShouldBeFalse();
        heartbeat.SensorInfo!.Schema.ShouldBe(Heartbeat.Schema);
        HeartbeatConfigCompat.ApplyDefaults(message, DeviceName).ShouldBeFalse();
    }

    [Fact]
    public void ApplyDefaults_DesiredSensorsOmitted_AddsNothing()
    {
        // No sensor list means "keep the sensors as they are", so there is nothing to complete.
        var message = ResetMessage(NoHeartbeatDeviceCfg);
        message.Data!.Cfg!.Desired!.DeviceCfg!.DeviceConfigs![DeviceName].Sensors = null;
        message.Data.Cfg.Desired.RawDeviceCfg = null;

        HeartbeatConfigCompat.ApplyDefaults(message, DeviceName).ShouldBeFalse();
    }

    [Fact]
    public void ApplyDefaults_NoDesiredSection_ReportsNothingApplied()
    {
        HeartbeatConfigCompat.ApplyDefaults(new SubNodeConfigUpdateMessage(), DeviceName).ShouldBeFalse();
        HeartbeatConfigCompat.ApplyDefaults(null, DeviceName).ShouldBeFalse();
    }
}
