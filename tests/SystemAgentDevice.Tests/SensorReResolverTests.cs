using Microsoft.Extensions.Logging;

using NSubstitute;

using SystemAgentExample.Communication;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Regression tests for runtime sensor re-resolution.
///
/// A cloud device-config update is applied in REPLACE mode, so the framework swaps the live
/// per-resource sensors (network_bytes_sent_eth0, …) for the cloud's desired state, which holds
/// the original templates (network_bytes_sent with Interfaces: []). Startup resolution never ran
/// again, so the parser skipped those templates on every poll
/// ("Sensor 'network_bytes_sent' missing Interface parameter, skipping") and their telemetry
/// stopped until the process restarted.
/// </summary>
public class SensorReResolverTests
{
    private const string DeviceId = "74fe488d5d54";

    private readonly ILogger _logger = Substitute.For<ILogger>();

    private static Sensor NetworkTemplate(string name = "network_bytes_sent", object? interfaces = null)
    {
        var parameters = new Dictionary<string, object>
        {
            ["MetricType"] = "network",
            ["MetricName"] = "bytes_sent"
        };
        if (interfaces != null)
            parameters["Interfaces"] = interfaces;

        return new Sensor
        {
            Name = name,
            Parameters = parameters,
            SensorInfo = new SensorInfo { Schema = "long", DisplayName = "Bytes Sent" },
            Report = new SensorReport { Enabled = true, Interval = 5000 },
        };
    }

    private static Sensor BoundSensor(string name, string metricType, string metricName, string key, string value)
    {
        return new Sensor
        {
            Name = name,
            ResourceId = $"existing-{name}",
            Parameters = new Dictionary<string, object>
            {
                ["MetricType"] = metricType,
                ["MetricName"] = metricName,
                [key] = value
            },
            SensorInfo = new SensorInfo { Schema = "long", DisplayName = name },
            Report = new SensorReport { Enabled = true, Interval = 5000 },
        };
    }

    private static Sensor PlainSensor(string name, string metricType, string metricName)
    {
        return new Sensor
        {
            Name = name,
            ResourceId = $"existing-{name}",
            Parameters = new Dictionary<string, object>
            {
                ["MetricType"] = metricType,
                ["MetricName"] = metricName
            },
            SensorInfo = new SensorInfo { Schema = "double", DisplayName = name },
            Report = new SensorReport { Enabled = true, Interval = 5000 },
        };
    }

    private static DeviceConfiguration MakeConfig(params Sensor[] sensors)
    {
        return new DeviceConfiguration
        {
            DeviceName = "AdvantechWedaMainboardExtension",
            DeviceId = DeviceId,
            Enabled = true,
            Sensors = [.. sensors]
        };
    }

    // --- The reported defect ---

    [Fact]
    public void ReResolve_AfterCloudReplacedResolvedSensorsWithTemplates_RestoresPerInterfaceSensors()
    {
        // Cloud REPLACE has just put the unresolved template back in place of the
        // per-interface sensors created at startup.
        var config = MakeConfig(NetworkTemplate(interfaces: Array.Empty<string>()));
        var resources = new DiscoveredResources(["lo", "enp8p1s0"], [], []);

        var changed = SensorReResolver.ReResolve(config, resources, _logger);

        Assert.True(changed);
        Assert.Equal(2, config.Sensors.Count);
        Assert.Equal(
            ["network_bytes_sent_lo", "network_bytes_sent_enp8p1s0"],
            config.Sensors.Select(s => s.Name));
        Assert.Equal("lo", config.Sensors[0].Parameters!["Interface"]);
        Assert.Equal("enp8p1s0", config.Sensors[1].Parameters!["Interface"]);

        // The unresolved template must be gone — it is what the parser was skipping.
        Assert.DoesNotContain(config.Sensors, s => s.Name == "network_bytes_sent");
    }

    [Fact]
    public void ReResolve_NewlyResolvedSensors_ReceiveResourceIds()
    {
        // Runtime resolution happens after the framework enriched the configuration,
        // so cloned sensors would otherwise be unaddressable for telemetry.
        var config = MakeConfig(NetworkTemplate(interfaces: Array.Empty<string>()));
        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        SensorReResolver.ReResolve(config, resources, _logger);

        Assert.All(config.Sensors, s => Assert.False(string.IsNullOrWhiteSpace(s.ResourceId)));
        Assert.Equal(DeviceId, config.Sensors[0].DeviceResourceId);

        // Distinct sensors must not collide on ResourceId.
        Assert.Equal(2, config.Sensors.Select(s => s.ResourceId).Distinct().Count());
    }

    [Fact]
    public void ReResolve_GpioAndTemperatureTemplates_AreAlsoRestored()
    {
        var gpio = new Sensor
        {
            Name = "gpio_pinState",
            Parameters = new Dictionary<string, object> { ["MetricType"] = "gpio", ["MetricName"] = "pinState" },
            SensorInfo = new SensorInfo { Schema = "integer", DisplayName = "GPIO" },
            Report = new SensorReport { Enabled = true, Interval = 6000 },
        };
        var temperature = new Sensor
        {
            Name = "temperature",
            Parameters = new Dictionary<string, object> { ["MetricType"] = "temperature" },
            SensorInfo = new SensorInfo { Schema = "double", DisplayName = "Temperature" },
            Report = new SensorReport { Enabled = true, Interval = 1000 },
        };

        var config = MakeConfig(gpio, temperature);
        var resources = new DiscoveredResources([], ["UIO_GPIO2", "UIO_GPIO4"], ["CPU-therm"]);

        var changed = SensorReResolver.ReResolve(config, resources, _logger);

        Assert.True(changed);
        Assert.Equal(
            ["gpio_pinState_UIO_GPIO2", "gpio_pinState_UIO_GPIO4", "temperature_CPU-therm"],
            config.Sensors.Select(s => s.Name));
    }

    // --- Idempotence: must not churn on every update ---

    [Fact]
    public void ReResolve_AlreadyResolvedSensors_ReportsNoChange()
    {
        var config = MakeConfig(
            BoundSensor("network_bytes_sent_eth0", "network", "bytes_sent", "Interface", "eth0"),
            BoundSensor("gpio_pinState_UIO_GPIO2", "gpio", "pinState", "PinId", "UIO_GPIO2"),
            BoundSensor("temperature_CPU-therm", "temperature", "temperature", "Source", "CPU-therm"));
        var resources = new DiscoveredResources(["eth0", "eth1"], ["UIO_GPIO2"], ["CPU-therm"]);

        var changed = SensorReResolver.ReResolve(config, resources, _logger);

        Assert.False(changed);
        Assert.Equal(3, config.Sensors.Count);
    }

    [Fact]
    public void ReResolve_NonResourceSensors_ReportNoChange()
    {
        var config = MakeConfig(
            PlainSensor("cpu_usage", "cpu", "usage"),
            PlainSensor("memory_total", "memory", "total"),
            PlainSensor("gpio_isSupported", "gpio", "isSupported"));
        var resources = new DiscoveredResources(["eth0"], ["UIO_GPIO2"], ["CPU-therm"]);

        var changed = SensorReResolver.ReResolve(config, resources, _logger);

        Assert.False(changed);
        Assert.Equal(3, config.Sensors.Count);
    }

    [Fact]
    public void ReResolve_RunTwice_IsStable()
    {
        var config = MakeConfig(NetworkTemplate(interfaces: Array.Empty<string>()));
        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        Assert.True(SensorReResolver.ReResolve(config, resources, _logger));
        var afterFirst = config.Sensors.Select(s => s.Name).ToList();

        Assert.False(SensorReResolver.ReResolve(config, resources, _logger));
        Assert.Equal(afterFirst, config.Sensors.Select(s => s.Name));
    }

    [Fact]
    public void ReResolve_TemperatureV1Compat_MetricNameActsAsSource_NoChange()
    {
        // v1.0 config style: MetricName without Source/Sources means already bound.
        var config = MakeConfig(PlainSensor("temperature_cpu", "temperature", "CPU-therm"));
        var resources = new DiscoveredResources([], [], ["CPU-therm", "GPU-therm"]);

        var changed = SensorReResolver.ReResolve(config, resources, _logger);

        Assert.False(changed);
        Assert.Single(config.Sensors);
    }

    // --- Degraded / mixed cases ---

    [Fact]
    public void ReResolve_NoDiscoveredResources_KeepsTemplateAndReportsNoChange()
    {
        // Nothing to expand into — must not claim a change and trigger a pointless task restart.
        var config = MakeConfig(NetworkTemplate(interfaces: Array.Empty<string>()));
        var resources = new DiscoveredResources([], [], []);

        var changed = SensorReResolver.ReResolve(config, resources, _logger);

        Assert.False(changed);
        Assert.Single(config.Sensors);
        Assert.Equal("network_bytes_sent", config.Sensors[0].Name);
    }

    [Fact]
    public void ReResolve_MixedBoundAndTemplate_PreservesBoundSensorsAndTheirResourceIds()
    {
        var bound = BoundSensor("network_bytes_sent_eth0", "network", "bytes_sent", "Interface", "eth0");
        var config = MakeConfig(
            PlainSensor("cpu_usage", "cpu", "usage"),
            bound,
            NetworkTemplate("network_packets_sent", interfaces: Array.Empty<string>()));
        config.Sensors[2].Parameters!["MetricName"] = "packets_sent";

        var resources = new DiscoveredResources(["eth0", "eth1"], [], []);

        var changed = SensorReResolver.ReResolve(config, resources, _logger);

        Assert.True(changed);
        Assert.Equal(
            ["cpu_usage", "network_bytes_sent_eth0", "network_packets_sent_eth0", "network_packets_sent_eth1"],
            config.Sensors.Select(s => s.Name));

        // Existing sensors keep their identity — no ResourceId churn for untouched sensors.
        Assert.Same(bound, config.Sensors[1]);
        Assert.Equal("existing-network_bytes_sent_eth0", config.Sensors[1].ResourceId);
        Assert.Equal("existing-cpu_usage", config.Sensors[0].ResourceId);
    }

    [Fact]
    public void ReResolve_ExplicitInterfaceListFromCloud_ArrivesAsListOfObject_IsHonoured()
    {
        // The framework converts cloud JSON arrays to List<object> before they reach us.
        var config = MakeConfig(NetworkTemplate(interfaces: new List<object> { "eth0" }));
        var resources = new DiscoveredResources(["eth0", "eth1", "lo"], [], []);

        var changed = SensorReResolver.ReResolve(config, resources, _logger);

        Assert.True(changed);
        Assert.Single(config.Sensors);
        Assert.Equal("network_bytes_sent_eth0", config.Sensors[0].Name);
    }

    [Fact]
    public void ReResolve_InvalidatesSensorLookupCache()
    {
        var config = MakeConfig(
            BoundSensor("network_bytes_sent_eth0", "network", "bytes_sent", "Interface", "eth0"),
            NetworkTemplate("network_packets_sent", interfaces: Array.Empty<string>()));

        // Prime the lookup cache so a stale cache would be observable.
        Assert.NotNull(config.GetSensorById("existing-network_bytes_sent_eth0"));

        var resources = new DiscoveredResources(["eth0"], [], []);
        Assert.True(SensorReResolver.ReResolve(config, resources, _logger));

        var newSensor = config.Sensors.First(s => s.Name == "network_packets_sent_eth0");
        Assert.NotNull(config.GetSensorById(newSensor.ResourceId));
    }

    [Fact]
    public void ReResolve_WithoutDeviceId_DoesNotThrow()
    {
        // Constructor path: enrichment has not run yet, so no ResourceIds can be generated.
        var config = MakeConfig(NetworkTemplate(interfaces: Array.Empty<string>()));
        config.DeviceId = null;
        var resources = new DiscoveredResources(["eth0"], [], []);

        var changed = SensorReResolver.ReResolve(config, resources, _logger);

        Assert.True(changed);
        Assert.Single(config.Sensors);
        Assert.Equal("network_bytes_sent_eth0", config.Sensors[0].Name);
    }

    [Fact]
    public void ReResolve_NullArguments_Throw()
    {
        var config = MakeConfig(NetworkTemplate());
        var resources = new DiscoveredResources([], [], []);

        Assert.Throws<ArgumentNullException>(() => SensorReResolver.ReResolve(null!, resources, _logger));
        Assert.Throws<ArgumentNullException>(() => SensorReResolver.ReResolve(config, null!, _logger));
    }

    // --- NeedsResolution predicate ---

    [Theory]
    [InlineData("network", "bytes_sent", null, null, true)]
    [InlineData("network", "bytes_sent", "Interface", "eth0", false)]
    [InlineData("gpio", "pinState", null, null, true)]
    [InlineData("gpio", "pinState", "PinId", "UIO_GPIO2", false)]
    [InlineData("gpio", "isSupported", null, null, false)]
    [InlineData("cpu", "usage", null, null, false)]
    [InlineData("memory", "total", null, null, false)]
    public void NeedsResolution_MatchesResolverBehaviour(
        string metricType, string metricName, string? boundKey, string? boundValue, bool expected)
    {
        var parameters = new Dictionary<string, object>
        {
            ["MetricType"] = metricType,
            ["MetricName"] = metricName
        };
        if (boundKey != null && boundValue != null)
            parameters[boundKey] = boundValue;

        var sensor = new Sensor
        {
            Name = "s",
            Parameters = parameters,
            SensorInfo = new SensorInfo { Schema = "long", DisplayName = "s" },
            Report = new SensorReport { Enabled = true, Interval = 1000 },
        };

        Assert.Equal(expected, SensorResolver.NeedsResolution([sensor]));
    }

    [Fact]
    public void NeedsResolution_TemperatureTemplate_IsTrue()
    {
        var sensor = new Sensor
        {
            Name = "temperature",
            Parameters = new Dictionary<string, object> { ["MetricType"] = "temperature" },
            SensorInfo = new SensorInfo { Schema = "double", DisplayName = "t" },
            Report = new SensorReport { Enabled = true, Interval = 1000 },
        };

        Assert.True(SensorResolver.NeedsResolution([sensor]));
    }
}
