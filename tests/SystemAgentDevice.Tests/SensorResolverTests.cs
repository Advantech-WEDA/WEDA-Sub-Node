using System.Text;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using NSubstitute;

using SystemAgentExample.Communication;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

using Xunit;
using Xunit.Abstractions;

namespace SystemAgentDevice.Tests;

public class SensorResolverTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    private static Sensor MakeNetworkSensor(string name = "net_bytes_sent", Dictionary<string, object>? extraParams = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["MetricType"] = "network",
            ["MetricName"] = "bytes_sent"
        };
        if (extraParams != null)
            foreach (var kv in extraParams)
                parameters[kv.Key] = kv.Value;

        return new Sensor
        {
            Name = name,
            Parameters = parameters,
            SensorInfo = new SensorInfo { Schema = "long", DisplayName = "Bytes Sent" },
            Report = new SensorReport { Enabled = true, Interval = 5000 },
        };
    }

    private static Sensor MakeGpioSensor(string name = "gpio_pin", Dictionary<string, object>? extraParams = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["MetricType"] = "gpio",
            ["MetricName"] = "pinState"
        };
        if (extraParams != null)
            foreach (var kv in extraParams)
                parameters[kv.Key] = kv.Value;

        return new Sensor
        {
            Name = name,
            Parameters = parameters,
            SensorInfo = new SensorInfo { Schema = "integer", DisplayName = "GPIO Pin" },
            Report = new SensorReport { Enabled = true, Interval = 6000 },
        };
    }

    private static Sensor MakeTemperatureSensor(string name = "temp_all", Dictionary<string, object>? extraParams = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["MetricType"] = "temperature"
        };
        if (extraParams != null)
            foreach (var kv in extraParams)
                parameters[kv.Key] = kv.Value;

        return new Sensor
        {
            Name = name,
            Parameters = parameters,
            SensorInfo = new SensorInfo { Schema = "double", DisplayName = "Temperature" },
            Report = new SensorReport { Enabled = true, Interval = 1000 },
        };
    }

    // --- Network resolution ---

    [Fact]
    public void Expand_NetworkSensor_WithoutInterface_ExpandsToDiscoveredInterfaces()
    {
        var sensor = MakeNetworkSensor();
        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(3, result.Count);
        Assert.Equal("net_bytes_sent", result[0].Name);
        Assert.Equal("net_bytes_sent_eth0", result[1].Name);
        Assert.Equal("net_bytes_sent_eth1", result[2].Name);
        Assert.Equal("eth0", result[1].Parameters!["Interface"]);
        Assert.Equal("eth1", result[2].Parameters!["Interface"]);
    }

    [Fact]
    public void Expand_KeepsOriginalTemplate_DisabledForCapabilityReporting()
    {
        var sensor = MakeNetworkSensor();
        var resources = new DiscoveredResources(["eth0"], [], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        // The original template stays in the list so its name still reaches
        // deviceCapabilities.sensors[], but disabled so it is never polled.
        var template = result[0];
        Assert.Equal("net_bytes_sent", template.Name);
        Assert.False(template.Report.Enabled);
        Assert.Equal(5000, template.Report.Interval);

        // The per-resource clone carries the configured reporting state.
        Assert.Equal("net_bytes_sent_eth0", result[1].Name);
        Assert.True(result[1].Report.Enabled);
    }

    [Fact]
    public void Expand_TemplateAlreadyExpanded_DoesNotDuplicateExistingClones()
    {
        // Steady state after re-resolution: template and a live clone coexist in the
        // input list. Expanding again must not produce a second net_bytes_sent_eth0.
        var template = MakeNetworkSensor();
        var existingClone = MakeNetworkSensor("net_bytes_sent_eth0", new() { ["Interface"] = "eth0" });
        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        var result = SensorResolver.Resolve([template, existingClone], resources, _logger);

        Assert.Equal(
            ["net_bytes_sent", "net_bytes_sent_eth1", "net_bytes_sent_eth0"],
            result.Select(s => s.Name));
        // The live clone passes through untouched — no identity churn.
        Assert.Same(existingClone, result[2]);
    }

    [Fact]
    public void Expand_NetworkSensor_WithExistingInterface_NoExpansion()
    {
        var sensor = MakeNetworkSensor(extraParams: new() { ["Interface"] = "eth0" });
        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    [Fact]
    public void Expand_NetworkSensor_NoDiscoveredInterfaces_KeepsOriginal()
    {
        var sensor = MakeNetworkSensor();
        var resources = new DiscoveredResources([], [], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    // --- GPIO resolution ---

    [Fact]
    public void Expand_GpioSensor_WithoutPinId_ExpandsToDiscoveredPins()
    {
        var sensor = MakeGpioSensor();
        var resources = new DiscoveredResources([], ["DI_0", "DI_1"], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(3, result.Count);
        Assert.Equal("gpio_pin", result[0].Name);
        Assert.False(result[0].Report.Enabled);
        Assert.Equal("gpio_pin_DI_0", result[1].Name);
        Assert.Equal("gpio_pin_DI_1", result[2].Name);
        Assert.Equal("DI_0", result[1].Parameters!["PinId"]);
    }

    [Fact]
    public void Expand_GpioSensor_WithExplicitPinIds_UsesExplicitList()
    {
        var sensor = MakeGpioSensor(extraParams: new()
        {
            ["PinIds"] = new[] { "0", "1" }
        });
        var resources = new DiscoveredResources([], ["DI_0", "DI_1", "DO_0"], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(3, result.Count);
        Assert.Equal("gpio_pin_0", result[1].Name);
        Assert.Equal("0", result[1].Parameters!["PinId"]);
        Assert.Equal("1", result[2].Parameters!["PinId"]);
        // PinIds array should be removed from resolved sensor
        Assert.False(result[1].Parameters!.ContainsKey("PinIds"));
    }

    [Fact]
    public void Expand_GpioSensor_WithExistingPinId_NoExpansion()
    {
        var sensor = MakeGpioSensor(extraParams: new() { ["PinId"] = "DI_0" });
        var resources = new DiscoveredResources([], ["DI_0", "DI_1"], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    [Fact]
    public void Expand_GpioSensor_MetricNameNotPinState_NoExpansion()
    {
        var sensor = new Sensor
        {
            Name = "gpio_supported",
            Parameters = new Dictionary<string, object>
            {
                ["MetricType"] = "gpio",
                ["MetricName"] = "isSupported"
            },
            SensorInfo = new SensorInfo { Schema = "boolean" },
            Report = new SensorReport { Enabled = true, Interval = 6000 },
        };
        var resources = new DiscoveredResources([], ["DI_0"], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    // --- Temperature resolution ---

    [Fact]
    public void Expand_TemperatureSensor_WithoutSourceOrMetricName_ExpandsToDiscoveredSources()
    {
        var sensor = MakeTemperatureSensor();
        var resources = new DiscoveredResources([], [], ["cpu_temp", "board_temp"]);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(3, result.Count);
        Assert.Equal("temp_all", result[0].Name);
        Assert.False(result[0].Report.Enabled);
        Assert.Equal("temp_all_cpu_temp", result[1].Name);
        Assert.Equal("temp_all_board_temp", result[2].Name);
        Assert.Equal("cpu_temp", result[1].Parameters!["Source"]);
    }

    [Fact]
    public void Expand_TemperatureSensor_WithExplicitSources_UsesExplicitList()
    {
        var sensor = MakeTemperatureSensor(extraParams: new()
        {
            ["MetricName"] = "therm",
            ["Sources"] = new[] { "cpu_temp" }
        });
        var resources = new DiscoveredResources([], [], ["cpu_temp", "board_temp"]);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(2, result.Count);
        Assert.Equal("temp_all_cpu_temp", result[1].Name);
        Assert.Equal("cpu_temp", result[1].Parameters!["Source"]);
        Assert.False(result[1].Parameters!.ContainsKey("Sources"));
        // The template keeps its Sources list — it documents the original intent.
        Assert.True(result[0].Parameters!.ContainsKey("Sources"));
    }

    [Fact]
    public void Expand_TemperatureSensor_WithEmptySources_ExpandsToDiscoveredSources()
    {
        var sensor = MakeTemperatureSensor(extraParams: new()
        {
            ["MetricName"] = "therm",
            ["Sources"] = Array.Empty<string>()
        });
        var resources = new DiscoveredResources([], [], ["cpu_temp", "board_temp"]);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(3, result.Count);
        Assert.Equal("cpu_temp", result[1].Parameters!["Source"]);
    }

    [Fact]
    public void Expand_TemperatureSensor_WithExistingSource_NoExpansion()
    {
        var sensor = MakeTemperatureSensor(extraParams: new() { ["Source"] = "cpu_temp" });
        var resources = new DiscoveredResources([], [], ["cpu_temp", "board_temp"]);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    [Fact]
    public void Expand_TemperatureSensor_V10Compat_WithMetricNameOnly_NoExpansion()
    {
        // v1.0 mode: MetricName acts as source identifier, no Sources param
        var sensor = MakeTemperatureSensor(extraParams: new() { ["MetricName"] = "cpU-therm" });
        var resources = new DiscoveredResources([], [], ["cpU-therm", "gpU-therm"]);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    // --- Normalizer + Resolver combined scenarios ---

    [Fact]
    public void NormalizeAndExpand_EmptyStringInterfaces_ExpandsToDiscovered()
    {
        var sensor = MakeNetworkSensor(extraParams: new() { ["Interfaces"] = "" });
        ParameterNormalizer.Normalize([sensor]);
        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(3, result.Count);
        Assert.Equal("net_bytes_sent_eth0", result[1].Name);
    }

    [Fact]
    public void NormalizeAndExpand_NullPinIds_ExpandsToDiscovered()
    {
        var sensor = MakeGpioSensor(extraParams: new() { ["PinIds"] = (object)null! });
        ParameterNormalizer.Normalize([sensor]);
        var resources = new DiscoveredResources([], ["DI_0", "DI_1"], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(3, result.Count);
        Assert.Equal("gpio_pin_DI_0", result[1].Name);
    }

    [Fact]
    public void NormalizeAndExpand_EmptyStringSources_ExpandsToDiscovered()
    {
        var sensor = MakeTemperatureSensor(extraParams: new()
        {
            ["MetricName"] = "therm",
            ["Sources"] = ""
        });
        ParameterNormalizer.Normalize([sensor]);
        var resources = new DiscoveredResources([], [], ["cpu_temp", "board_temp"]);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(3, result.Count);
        Assert.Equal("cpu_temp", result[1].Parameters!["Source"]);
    }

    [Fact]
    public void NormalizeAndExpand_CommaSeparatedInterfaces_UsesExplicitList()
    {
        var sensor = MakeNetworkSensor(extraParams: new() { ["Interfaces"] = "eth0,wlan0" });
        ParameterNormalizer.Normalize([sensor]);
        var resources = new DiscoveredResources(["eth0", "wlan0", "docker0"], [], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(3, result.Count);
        Assert.Equal("net_bytes_sent_eth0", result[1].Name);
        Assert.Equal("net_bytes_sent_wlan0", result[2].Name);
    }

    [Fact]
    public void NormalizeAndExpand_SystemObjectFromIConfig_ExpandsToDiscovered()
    {
        // IConfiguration binds populated JSON arrays as bare System.Object — content is lost
        var sensor = MakeNetworkSensor(extraParams: new() { ["Interfaces"] = new object() });
        ParameterNormalizer.Normalize([sensor]);
        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        // Normalizer removes unrecoverable key → SensorResolver sees "not configured" → auto-detect
        Assert.Equal(3, result.Count);
    }

    // --- Non-expanding types ---

    [Fact]
    public void Expand_CpuSensor_PassesThrough()
    {
        var sensor = new Sensor
        {
            Name = "cpu_usage",
            Parameters = new Dictionary<string, object>
            {
                ["MetricType"] = "cpu",
                ["MetricName"] = "usage_percent"
            },
            SensorInfo = new SensorInfo { Schema = "double" },
            Report = new SensorReport { Enabled = true, Interval = 5000 },
        };
        var resources = new DiscoveredResources(["eth0"], ["DI_0"], ["cpu_temp"]);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    // --- CloneSensor properties ---

    [Fact]
    public void Expand_ClonedSensor_PreservesReportSettings()
    {
        var sensor = MakeNetworkSensor();
        sensor.Report = new SensorReport { Enabled = true, Interval = 3000, Unit = "ms" };
        var resources = new DiscoveredResources(["eth0"], [], []);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(2, result.Count);

        // Capability template: reporting disabled, everything else preserved.
        Assert.False(result[0].Report.Enabled);
        Assert.Equal(3000, result[0].Report.Interval);
        Assert.Equal("ms", result[0].Report.Unit);

        // Clone: full report settings preserved.
        Assert.True(result[1].Report.Enabled);
        Assert.Equal(3000, result[1].Report.Interval);
        Assert.Equal("ms", result[1].Report.Unit);
    }
}

/// <summary>
/// Tests that verify SensorResolver works correctly when config is loaded
/// through IConfiguration (the actual runtime path), not just in-memory objects.
/// </summary>
public class SensorResolverIConfigurationTests(ITestOutputHelper output)
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    private static DeviceConfiguration LoadFromJson(string json)
    {
        var builder = new ConfigurationBuilder();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        builder.AddJsonStream(stream);
        var config = builder.Build();
        return config.GetSection("DeviceConfigs:Test").Get<DeviceConfiguration>()!;
    }

    private static (DeviceConfiguration config, IConfigurationSection sensorsSection) LoadFromJsonWithSection(string json)
    {
        var builder = new ConfigurationBuilder();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        builder.AddJsonStream(stream);
        var config = builder.Build();
        var deviceConfig = config.GetSection("DeviceConfigs:Test").Get<DeviceConfiguration>()!;
        var sensorsSection = config.GetSection("DeviceConfigs:Test:Sensors");
        return (deviceConfig, sensorsSection);
    }

    [Fact]
    public void IConfiguration_EmptyString_ParameterKeyPresent()
    {
        // IConfiguration binds "Interfaces": [] as empty string ""
        // and "Interfaces": "" as empty string "" — same result
        var json = """
        {
            "DeviceConfigs": {
                "Test": {
                    "Sensors": [{
                        "Name": "net",
                        "SensorGroup": "SYS",
                        "Parameters": {
                            "MetricType": "network",
                            "MetricName": "bytes_sent",
                            "Interfaces": ""
                        },
                        "Report": { "Enabled": true, "Interval": 5000 },
                        "SensorInfo": { "Schema": "long", "Description": "t", "DisplayName": "t" }
                    }]
                }
            }
        }
        """;

        var deviceConfig = LoadFromJson(json);
        var sensor = deviceConfig.Sensors[0];

        Assert.True(sensor.Parameters!.ContainsKey("Interfaces"));
        Assert.Equal("", sensor.Parameters["Interfaces"].ToString());
    }

    [Fact]
    public void IConfiguration_CommaSeparated_ParameterIsString()
    {
        var json = """
        {
            "DeviceConfigs": {
                "Test": {
                    "Sensors": [{
                        "Name": "net",
                        "SensorGroup": "SYS",
                        "Parameters": {
                            "MetricType": "network",
                            "MetricName": "bytes_sent",
                            "Interfaces": "eth0,eth1"
                        },
                        "Report": { "Enabled": true, "Interval": 5000 },
                        "SensorInfo": { "Schema": "long", "Description": "t", "DisplayName": "t" }
                    }]
                }
            }
        }
        """;

        var deviceConfig = LoadFromJson(json);
        var sensor = deviceConfig.Sensors[0];

        Assert.True(sensor.Parameters!.ContainsKey("Interfaces"));
        Assert.IsType<string>(sensor.Parameters["Interfaces"]);
        Assert.Equal("eth0,eth1", sensor.Parameters["Interfaces"].ToString());
    }

    [Fact]
    public void Expand_ViaIConfiguration_EmptyStrings_ExpandToDiscovered()
    {
        var json = """
        {
            "DeviceConfigs": {
                "Test": {
                    "Sensors": [
                        {
                            "Name": "network_bytes_sent",
                            "SensorGroup": "SYS",
                            "Parameters": {
                                "MetricType": "network",
                                "MetricName": "bytes_sent",
                                "Interfaces": ""
                            },
                            "Report": { "Enabled": true, "Interval": 5000 },
                            "SensorInfo": { "Schema": "long", "Description": "t", "DisplayName": "Bytes Sent" }
                        },
                        {
                            "Name": "gpio_pinState",
                            "SensorGroup": "DI",
                            "Parameters": {
                                "MetricType": "gpio",
                                "MetricName": "pinState",
                                "PinIds": ""
                            },
                            "Report": { "Enabled": true, "Interval": 6000 },
                            "SensorInfo": { "Schema": "integer", "Description": "t", "DisplayName": "Pin State" }
                        },
                        {
                            "Name": "temperature",
                            "SensorGroup": "TEMP",
                            "Parameters": {
                                "MetricType": "temperature",
                                "MetricName": "therm",
                                "Sources": ""
                            },
                            "Report": { "Enabled": true, "Interval": 1000 },
                            "SensorInfo": { "Schema": "double", "Description": "t", "DisplayName": "Temperature" }
                        }
                    ]
                }
            }
        }
        """;

        var (deviceConfig, sensorsSection) = LoadFromJsonWithSection(json);
        var resources = new DiscoveredResources(
            ["eth0", "wlan0"],
            ["DI_0", "DI_1"],
            ["CPU-therm", "GPU-therm"]);

        ParameterNormalizer.Normalize(deviceConfig.Sensors, sensorsSection);
        var result = SensorResolver.Resolve(deviceConfig.Sensors, resources, _logger);

        // Network: disabled template kept + 2 expanded
        var networkSensors = result
            .Where(s => s.Parameters!["MetricType"].ToString() == "network").ToList();
        Assert.Equal(3, networkSensors.Count);
        Assert.Equal("network_bytes_sent", networkSensors[0].Name);
        Assert.Equal("network_bytes_sent_eth0", networkSensors[1].Name);
        Assert.Equal("eth0", networkSensors[1].Parameters!["Interface"].ToString());

        // GPIO: disabled template kept + 2 expanded
        var gpioSensors = result
            .Where(s => s.Parameters!["MetricType"].ToString() == "gpio").ToList();
        Assert.Equal(3, gpioSensors.Count);
        Assert.Equal("gpio_pinState_DI_0", gpioSensors[1].Name);

        // Temperature: disabled template kept + 2 expanded
        var tempSensors = result
            .Where(s => s.Parameters!["MetricType"].ToString() == "temperature").ToList();
        Assert.Equal(3, tempSensors.Count);
        Assert.Equal("temperature_CPU-therm", tempSensors[1].Name);
        Assert.Equal("CPU-therm", tempSensors[1].Parameters!["Source"].ToString());
    }

    [Fact]
    public void Expand_ViaIConfiguration_CommaSeparated_UseExplicitList()
    {
        var json = """
        {
            "DeviceConfigs": {
                "Test": {
                    "Sensors": [
                        {
                            "Name": "network_bytes_sent",
                            "SensorGroup": "SYS",
                            "Parameters": {
                                "MetricType": "network",
                                "MetricName": "bytes_sent",
                                "Interfaces": "eth0"
                            },
                            "Report": { "Enabled": true, "Interval": 5000 },
                            "SensorInfo": { "Schema": "long", "Description": "t", "DisplayName": "Bytes Sent" }
                        },
                        {
                            "Name": "temperature",
                            "SensorGroup": "TEMP",
                            "Parameters": {
                                "MetricType": "temperature",
                                "MetricName": "therm",
                                "Sources": "CPU-therm"
                            },
                            "Report": { "Enabled": true, "Interval": 1000 },
                            "SensorInfo": { "Schema": "double", "Description": "t", "DisplayName": "Temperature" }
                        }
                    ]
                }
            }
        }
        """;

        var (deviceConfig, sensorsSection) = LoadFromJsonWithSection(json);
        var resources = new DiscoveredResources(
            ["eth0", "wlan0", "docker0"],
            [],
            ["CPU-therm", "GPU-therm"]);

        ParameterNormalizer.Normalize(deviceConfig.Sensors, sensorsSection);
        var result = SensorResolver.Resolve(deviceConfig.Sensors, resources, _logger);

        // Network: disabled template kept + explicit "eth0" → only 1 expanded
        var networkSensors = result
            .Where(s => s.Parameters!["MetricType"].ToString() == "network").ToList();
        Assert.Equal(2, networkSensors.Count);
        Assert.Equal("network_bytes_sent_eth0", networkSensors[1].Name);

        // Temperature: disabled template kept + explicit ["CPU-therm"] → only 1 expanded
        var tempSensors = result
            .Where(s => s.Parameters!["MetricType"].ToString() == "temperature").ToList();
        Assert.Equal(2, tempSensors.Count);
        Assert.Equal("temperature_CPU-therm", tempSensors[1].Name);
    }

    [Fact]
    public void Expand_ViaIConfiguration_JsonArrays_RecoveredViaSection()
    {
        // JSON arrays are lost during Dictionary<string, object> binding,
        // but can be recovered from the raw IConfigurationSection children.
        var json = """
        {
            "DeviceConfigs": {
                "Test": {
                    "Sensors": [
                        {
                            "Name": "network_bytes_sent",
                            "SensorGroup": "SYS",
                            "Parameters": {
                                "MetricType": "network",
                                "MetricName": "bytes_sent",
                                "Interfaces": ["eth0", "wlan0"]
                            },
                            "Report": { "Enabled": true, "Interval": 5000 },
                            "SensorInfo": { "Schema": "long", "Description": "t", "DisplayName": "Bytes Sent" }
                        },
                        {
                            "Name": "gpio_pinState",
                            "SensorGroup": "DI",
                            "Parameters": {
                                "MetricType": "gpio",
                                "MetricName": "pinState",
                                "PinIds": ["DI_0", "DI_1", "DO_0"]
                            },
                            "Report": { "Enabled": true, "Interval": 6000 },
                            "SensorInfo": { "Schema": "integer", "Description": "t", "DisplayName": "Pin State" }
                        },
                        {
                            "Name": "temperature",
                            "SensorGroup": "TEMP",
                            "Parameters": {
                                "MetricType": "temperature",
                                "MetricName": "therm",
                                "Sources": ["CPU-therm"]
                            },
                            "Report": { "Enabled": true, "Interval": 1000 },
                            "SensorInfo": { "Schema": "double", "Description": "t", "DisplayName": "Temperature" }
                        }
                    ]
                }
            }
        }
        """;

        var (deviceConfig, sensorsSection) = LoadFromJsonWithSection(json);
        var resources = new DiscoveredResources(
            ["eth0", "wlan0", "docker0"],
            ["DI_0", "DI_1", "DO_0", "DO_1"],
            ["CPU-therm", "GPU-therm"]);

        ParameterNormalizer.Normalize(deviceConfig.Sensors, sensorsSection);
        var result = SensorResolver.Resolve(deviceConfig.Sensors, resources, _logger);

        // Network: template + explicit ["eth0", "wlan0"] → 2 expanded (docker0 excluded)
        var networkSensors = result
            .Where(s => s.Parameters!["MetricType"].ToString() == "network").ToList();
        Assert.Equal(3, networkSensors.Count);
        Assert.Equal("network_bytes_sent_eth0", networkSensors[1].Name);
        Assert.Equal("network_bytes_sent_wlan0", networkSensors[2].Name);

        // GPIO: template + explicit ["DI_0", "DI_1", "DO_0"] → 3 expanded (DO_1 excluded)
        var gpioSensors = result
            .Where(s => s.Parameters!["MetricType"].ToString() == "gpio").ToList();
        Assert.Equal(4, gpioSensors.Count);
        Assert.Equal("gpio_pinState_DI_0", gpioSensors[1].Name);
        Assert.Equal("gpio_pinState_DI_1", gpioSensors[2].Name);
        Assert.Equal("gpio_pinState_DO_0", gpioSensors[3].Name);

        // Temperature: template + explicit ["CPU-therm"] → 1 expanded (GPU-therm excluded)
        var tempSensors = result
            .Where(s => s.Parameters!["MetricType"].ToString() == "temperature").ToList();
        Assert.Equal(2, tempSensors.Count);
        Assert.Equal("temperature_CPU-therm", tempSensors[1].Name);
    }

    [Fact]
    public void Expand_ViaIConfiguration_EmptyJsonArrays_TriggerAutoDetect()
    {
        // Empty JSON arrays [] are bound as empty string "" by IConfiguration,
        // but should still trigger auto-detect regardless of recovery method.
        var json = """
        {
            "DeviceConfigs": {
                "Test": {
                    "Sensors": [
                        {
                            "Name": "network_bytes_sent",
                            "SensorGroup": "SYS",
                            "Parameters": {
                                "MetricType": "network",
                                "MetricName": "bytes_sent",
                                "Interfaces": []
                            },
                            "Report": { "Enabled": true, "Interval": 5000 },
                            "SensorInfo": { "Schema": "long", "Description": "t", "DisplayName": "Bytes Sent" }
                        }
                    ]
                }
            }
        }
        """;

        var (deviceConfig, sensorsSection) = LoadFromJsonWithSection(json);
        var resources = new DiscoveredResources(
            ["eth0", "wlan0"],
            [],
            []);

        ParameterNormalizer.Normalize(deviceConfig.Sensors, sensorsSection);
        var result = SensorResolver.Resolve(deviceConfig.Sensors, resources, _logger);

        // Empty array triggers auto-detect → template kept + all discovered interfaces
        var networkSensors = result
            .Where(s => s.Parameters!["MetricType"].ToString() == "network").ToList();
        Assert.Equal(3, networkSensors.Count);
        Assert.Equal("network_bytes_sent_eth0", networkSensors[1].Name);
        Assert.Equal("network_bytes_sent_wlan0", networkSensors[2].Name);
    }
}
