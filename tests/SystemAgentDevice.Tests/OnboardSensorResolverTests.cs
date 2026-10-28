using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;

using NSubstitute;

using SystemAgentExample.Communication;
using SystemAgentExample.Devices;

using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Onboard (SUSI) sensors — temperature, voltage, fan speed — expand from a generic
/// template into one sensor per discovered source, so nobody has to type a rail or
/// source name into <c>MetricName</c>.
/// </summary>
public class OnboardSensorResolverTests
{
    // The rails SUSI reports on an ARK-2250L (SusiiotDemo capability dump, 2026-10-28)
    private static readonly string[] Ark2250LRails = ["Vcore", "5V", "12V", "CMOS Battery"];

    private static readonly Regex IoTDbIdentifier = new("^[a-zA-Z_][a-zA-Z0-9_]*$");

    private readonly ILogger _logger = Substitute.For<ILogger>();

    private static Sensor MakeSensor(
        string name, string metricType, string? metricName, Dictionary<string, object>? extraParams = null)
    {
        var parameters = new Dictionary<string, object> { ["MetricType"] = metricType };
        if (metricName != null)
            parameters["MetricName"] = metricName;
        if (extraParams != null)
            foreach (var kv in extraParams)
                parameters[kv.Key] = kv.Value;

        return new Sensor
        {
            Name = name,
            SensorGroup = SensorGroup.PWR,
            Parameters = parameters,
            SensorInfo = new SensorInfo { Schema = "double", DisplayName = "Voltage", Description = "Voltage sensor reading" },
            Report = new SensorReport { Enabled = true, Interval = 10000 },
        };
    }

    private static DiscoveredResources Rails(params string[] rails) =>
        new([], [], [], VoltageSources: rails);

    // --- Voltage ---

    [Fact]
    public void Voltage_GenericTemplate_ExpandsToOneSensorPerDiscoveredRail()
    {
        var sensor = MakeSensor("voltage", "voltage", "voltage");

        var result = SensorResolver.Resolve([sensor], Rails(Ark2250LRails), _logger);

        Assert.Equal(
            ["voltage", "voltage_Vcore", "voltage_5V", "voltage_12V", "voltage_CMOS_Battery"],
            result.Select(s => s.Name));
        Assert.False(result[0].Report.Enabled);
        Assert.All(result.Skip(1), clone => Assert.True(clone.Report.Enabled));
        // The raw rail name is what the collector keys readings by
        Assert.Equal(Ark2250LRails, result.Skip(1).Select(s => s.Parameters!["Source"]));
        // MetricName stays the type discriminator
        Assert.All(result.Skip(1), clone => Assert.Equal("voltage", clone.Parameters!["MetricName"]));
    }

    [Theory]
    [InlineData("voltage", "voltage")]
    [InlineData("Voltage", "Voltage")]   // the capitalised form found on the ARK-2250L config
    [InlineData("voltage", null)]
    public void Voltage_GenericTemplate_ExpandsRegardlessOfCaseOrMissingMetricName(string metricType, string? metricName)
    {
        var sensor = MakeSensor("voltage", metricType, metricName);

        var result = SensorResolver.Resolve([sensor], Rails("Vcore", "12V"), _logger);

        Assert.Equal(["voltage", "voltage_Vcore", "voltage_12V"], result.Select(s => s.Name));
    }

    [Fact]
    public void Voltage_ExplicitSources_ExpandsOnlyTheListedRails()
    {
        var sensor = MakeSensor("voltage", "voltage", "voltage", new() { ["Sources"] = new[] { "12V" } });

        var result = SensorResolver.Resolve([sensor], Rails(Ark2250LRails), _logger);

        Assert.Equal(["voltage", "voltage_12V"], result.Select(s => s.Name));
        Assert.False(result[1].Parameters!.ContainsKey("Sources"));
    }

    [Fact]
    public void Voltage_RailNameInMetricName_IsBoundAndPassesThrough()
    {
        // v1.0-style config, as hand-applied on the ARK-2250L: MetricName names the rail
        var sensor = MakeSensor("voltage_vcore", "voltage", "Vcore");

        var result = SensorResolver.Resolve([sensor], Rails(Ark2250LRails), _logger);

        Assert.Same(sensor, Assert.Single(result));
    }

    [Fact]
    public void Voltage_WithSource_IsBoundAndPassesThrough()
    {
        var sensor = MakeSensor("voltage_Vcore", "voltage", "voltage", new() { ["Source"] = "Vcore" });

        var result = SensorResolver.Resolve([sensor], Rails(Ark2250LRails), _logger);

        Assert.Same(sensor, Assert.Single(result));
    }

    [Fact]
    public void Voltage_NoRailsDiscovered_KeepsTemplateAsIs()
    {
        var sensor = MakeSensor("voltage", "voltage", "voltage");

        var result = SensorResolver.Resolve([sensor], Rails(), _logger);

        Assert.Same(sensor, Assert.Single(result));
    }

    [Fact]
    public void Voltage_RailNamesWithSpacesOrSymbols_YieldValidSensorNames()
    {
        var sensor = MakeSensor("voltage", "voltage", "voltage");

        var result = SensorResolver.Resolve([sensor], Rails("+3.3V", "5V Standby", "CMOS Battery"), _logger);

        Assert.Equal(
            ["voltage", "voltage_3_3V", "voltage_5V_Standby", "voltage_CMOS_Battery"],
            result.Select(s => s.Name));
        Assert.All(result, s => Assert.Matches(IoTDbIdentifier, s.Name));
        Assert.Equal("+3.3V", result[1].Parameters!["Source"]);
    }

    [Fact]
    public void Voltage_RailsCollidingAfterNameCleanup_KeepsTheFirstOnly()
    {
        var sensor = MakeSensor("voltage", "voltage", "voltage");

        var result = SensorResolver.Resolve([sensor], Rails("5V", "+5V"), _logger);

        Assert.Equal(["voltage", "voltage_5V"], result.Select(s => s.Name));
        Assert.Equal("5V", result[1].Parameters!["Source"]);
    }

    [Fact]
    public void Voltage_ReResolvingTemplateAndClones_DoesNotDuplicate()
    {
        var template = MakeSensor("voltage", "voltage", "voltage");
        template.Report.Enabled = false;
        var existingClone = MakeSensor("voltage_Vcore", "voltage", "voltage", new() { ["Source"] = "Vcore" });

        var result = SensorResolver.Resolve([template, existingClone], Rails("Vcore", "12V"), _logger);

        Assert.Equal(["voltage", "voltage_12V", "voltage_Vcore"], result.Select(s => s.Name));
        Assert.Same(existingClone, result[2]);
    }

    [Fact]
    public void NeedsResolution_GenericVoltageTemplate_IsTrue_BoundRail_IsFalse()
    {
        Assert.True(SensorResolver.NeedsResolution([MakeSensor("voltage", "voltage", "voltage")]));
        Assert.False(SensorResolver.NeedsResolution([MakeSensor("voltage_vcore", "voltage", "Vcore")]));
    }

    // --- Fan speed ---

    [Fact]
    public void Fanspeed_GenericTemplate_ExpandsToOneSensorPerDiscoveredFan()
    {
        var sensor = MakeSensor("fanspeed", "fanspeed", "fanspeed");
        var resources = new DiscoveredResources([], [], [], FanSources: ["CPU", "System"]);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(["fanspeed", "fanspeed_CPU", "fanspeed_System"], result.Select(s => s.Name));
        Assert.Equal("CPU", result[1].Parameters!["Source"]);
    }

    [Fact]
    public void Fanspeed_NoFansDiscovered_KeepsTemplateAsIs()
    {
        var sensor = MakeSensor("fanspeed", "fanspeed", "fanspeed");

        var result = SensorResolver.Resolve([sensor], new DiscoveredResources([], [], []), _logger);

        Assert.Same(sensor, Assert.Single(result));
    }

    // --- Temperature (same rule) ---

    [Fact]
    public void Temperature_GenericMetricNameWithoutSources_ExpandsToDiscoveredSources()
    {
        // Previously "therm" itself was taken as the source name, which matches nothing
        var sensor = MakeSensor("temperature", "temperature", "therm");
        var resources = new DiscoveredResources([], [], ["CPU"]);

        var result = SensorResolver.Resolve([sensor], resources, _logger);

        Assert.Equal(["temperature", "temperature_CPU"], result.Select(s => s.Name));
        Assert.Equal("CPU", result[1].Parameters!["Source"]);
    }

    [Fact]
    public void Temperature_SourceNameInMetricName_StaysBound()
    {
        var sensor = MakeSensor("temperature_cpu", "temperature", "CPU");

        var result = SensorResolver.Resolve([sensor], new DiscoveredResources([], [], ["CPU", "System"]), _logger);

        Assert.Same(sensor, Assert.Single(result));
    }
}
