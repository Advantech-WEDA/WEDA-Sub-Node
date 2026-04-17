using Microsoft.Extensions.Logging;

using NSubstitute;

using SystemAgentExample.Communication;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace SystemAgentDevice.Tests;

public class SensorExpanderTests
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

    // --- Network expansion ---

    [Fact]
    public void Expand_NetworkSensor_WithoutInterface_ExpandsToDiscoveredInterfaces()
    {
        var sensor = MakeNetworkSensor();
        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        var result = SensorExpander.Expand([sensor], resources, _logger);

        Assert.Equal(2, result.Count);
        Assert.Equal("net_bytes_sent_eth0", result[0].Name);
        Assert.Equal("net_bytes_sent_eth1", result[1].Name);
        Assert.Equal("eth0", result[0].Parameters!["Interface"]);
        Assert.Equal("eth1", result[1].Parameters!["Interface"]);
    }

    [Fact]
    public void Expand_NetworkSensor_WithExistingInterface_NoExpansion()
    {
        var sensor = MakeNetworkSensor(extraParams: new() { ["Interface"] = "eth0" });
        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        var result = SensorExpander.Expand([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    [Fact]
    public void Expand_NetworkSensor_NoDiscoveredInterfaces_KeepsOriginal()
    {
        var sensor = MakeNetworkSensor();
        var resources = new DiscoveredResources([], [], []);

        var result = SensorExpander.Expand([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    // --- GPIO expansion ---

    [Fact]
    public void Expand_GpioSensor_WithoutPinId_ExpandsToDiscoveredPins()
    {
        var sensor = MakeGpioSensor();
        var resources = new DiscoveredResources([], ["DI_0", "DI_1"], []);

        var result = SensorExpander.Expand([sensor], resources, _logger);

        Assert.Equal(2, result.Count);
        Assert.Equal("gpio_pin_DI_0", result[0].Name);
        Assert.Equal("gpio_pin_DI_1", result[1].Name);
        Assert.Equal("DI_0", result[0].Parameters!["PinId"]);
    }

    [Fact]
    public void Expand_GpioSensor_WithExplicitPinIds_UsesExplicitList()
    {
        var sensor = MakeGpioSensor(extraParams: new()
        {
            ["PinIds"] = new[] { "0", "1" }
        });
        var resources = new DiscoveredResources([], ["DI_0", "DI_1", "DO_0"], []);

        var result = SensorExpander.Expand([sensor], resources, _logger);

        Assert.Equal(2, result.Count);
        Assert.Equal("gpio_pin_0", result[0].Name);
        Assert.Equal("0", result[0].Parameters!["PinId"]);
        Assert.Equal("1", result[1].Parameters!["PinId"]);
        // PinIds array should be removed from expanded sensor
        Assert.False(result[0].Parameters!.ContainsKey("PinIds"));
    }

    [Fact]
    public void Expand_GpioSensor_WithExistingPinId_NoExpansion()
    {
        var sensor = MakeGpioSensor(extraParams: new() { ["PinId"] = "DI_0" });
        var resources = new DiscoveredResources([], ["DI_0", "DI_1"], []);

        var result = SensorExpander.Expand([sensor], resources, _logger);

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

        var result = SensorExpander.Expand([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
    }

    // --- Temperature expansion ---

    [Fact]
    public void Expand_TemperatureSensor_WithoutMetricName_ExpandsToDiscoveredSources()
    {
        var sensor = MakeTemperatureSensor();
        var resources = new DiscoveredResources([], [], ["cpu_temp", "board_temp"]);

        var result = SensorExpander.Expand([sensor], resources, _logger);

        Assert.Equal(2, result.Count);
        Assert.Equal("temp_all_cpu_temp", result[0].Name);
        Assert.Equal("temp_all_board_temp", result[1].Name);
        Assert.Equal("cpu_temp", result[0].Parameters!["MetricName"]);
    }

    [Fact]
    public void Expand_TemperatureSensor_WithExistingMetricName_NoExpansion()
    {
        var sensor = MakeTemperatureSensor(extraParams: new() { ["MetricName"] = "cpu_temp" });
        var resources = new DiscoveredResources([], [], ["cpu_temp", "board_temp"]);

        var result = SensorExpander.Expand([sensor], resources, _logger);

        Assert.Single(result);
        Assert.Same(sensor, result[0]);
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

        var result = SensorExpander.Expand([sensor], resources, _logger);

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

        var result = SensorExpander.Expand([sensor], resources, _logger);

        Assert.Single(result);
        Assert.True(result[0].Report.Enabled);
        Assert.Equal(3000, result[0].Report.Interval);
        Assert.Equal("ms", result[0].Report.Unit);
    }
}
