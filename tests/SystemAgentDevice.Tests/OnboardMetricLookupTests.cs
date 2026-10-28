using SystemAgentExample.Protocols;

using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace SystemAgentDevice.Tests;

/// <summary>
/// Picks one reading out of the per-source dictionary an onboard collector returns
/// (temperature, voltage, fan speed), using the sensor's <c>Source</c> parameter.
/// </summary>
public class OnboardMetricLookupTests
{
    // Readings keyed the way the collector keys them on an ARK-2250L
    private static readonly Dictionary<string, double?> Ark2250LVoltages = new()
    {
        ["Vcore"] = 0.674,
        ["5V"] = 5.008,
        ["12V"] = 12.082,
        ["CMOS Battery"] = 2.97,
    };

    private static Sensor MakeSensor(string? metricName, string? source = null)
    {
        var parameters = new Dictionary<string, object> { ["MetricType"] = "voltage" };
        if (metricName != null) parameters["MetricName"] = metricName;
        if (source != null) parameters["Source"] = source;
        return new Sensor { Name = "voltage", Parameters = parameters };
    }

    [Fact]
    public void Source_SelectsThatReading()
    {
        var value = OnboardMetricLookup.Find(Ark2250LVoltages, MakeSensor("voltage", "CMOS Battery"), "voltage");

        Assert.Equal(2.97, value);
    }

    [Fact]
    public void Source_MatchesCaseInsensitively()
    {
        var value = OnboardMetricLookup.Find(Ark2250LVoltages, MakeSensor("voltage", "vcore"), "voltage");

        Assert.Equal(0.674, value);
    }

    [Fact]
    public void SourceNameInMetricName_SelectsThatReading()
    {
        var value = OnboardMetricLookup.Find(Ark2250LVoltages, MakeSensor("12V"), "voltage");

        Assert.Equal(12.082, value);
    }

    [Fact]
    public void UnresolvedTemplate_SeveralReadings_ReturnsNull()
    {
        // Reproduction of the ARK-2250L symptom: four rails, a generic "voltage" sensor,
        // no single value to pick
        Assert.Null(OnboardMetricLookup.Find(Ark2250LVoltages, MakeSensor("voltage"), "voltage"));
    }

    [Theory]
    [InlineData("voltage")]
    [InlineData("Voltage")]
    [InlineData(null)]
    public void UnresolvedTemplate_SingleReading_ReturnsIt(string? metricName)
    {
        var single = new Dictionary<string, double?> { ["Vcore"] = 1.05 };

        Assert.Equal(1.05, OnboardMetricLookup.Find(single, MakeSensor(metricName), "voltage"));
    }

    [Fact]
    public void UnknownSource_ReturnsNull()
    {
        Assert.Null(OnboardMetricLookup.Find(Ark2250LVoltages, MakeSensor("voltage", "24V"), "voltage"));
    }

    [Fact]
    public void NoReadings_ReturnsNull()
    {
        Assert.Null(OnboardMetricLookup.Find(null, MakeSensor("voltage", "Vcore"), "voltage"));
        Assert.Null(OnboardMetricLookup.Find(new Dictionary<string, double?>(), MakeSensor("voltage", "Vcore"), "voltage"));
    }

    [Fact]
    public void SourceWithoutReading_ReturnsNull()
    {
        var values = new Dictionary<string, double?> { ["Vcore"] = null };

        Assert.Null(OnboardMetricLookup.Find(values, MakeSensor("voltage", "Vcore"), "voltage"));
    }
}

public class MissingValueTrackerTests
{
    [Fact]
    public void FirstMiss_IsReported_RepeatedMissesAreNot()
    {
        var tracker = new MissingValueTracker();

        Assert.True(tracker.MarkMissing("voltage"));
        Assert.False(tracker.MarkMissing("voltage"));
        Assert.True(tracker.MarkMissing("fanspeed"));
    }

    [Fact]
    public void MissAfterRecovery_IsReportedAgain()
    {
        var tracker = new MissingValueTracker();
        tracker.MarkMissing("voltage");

        tracker.MarkPresent("voltage");

        Assert.True(tracker.MarkMissing("voltage"));
    }
}
