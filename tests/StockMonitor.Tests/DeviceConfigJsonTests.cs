using System.Text.Json;

using Shouldly;

using StockMonitor.Protocols;

using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace StockMonitor.Tests;

/// <summary>
/// Pins the shipped <c>devicecfg.json</c>. The example doubles as the reference configuration, so a
/// sensor whose declared schema or unit disagrees with the metric it reports teaches the wrong shape.
/// </summary>
public class DeviceConfigJsonTests
{
    private static readonly JsonDocument Config = LoadConfig();

    private static JsonDocument LoadConfig()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../../examples/stock-monitor/devicecfg.json"));

        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static IEnumerable<JsonElement> Sensors =>
        Config.RootElement
            .GetProperty("DeviceConfigs")
            .GetProperty("StockMonitorConfig")
            .GetProperty("Sensors")
            .EnumerateArray();

    private static string Name(JsonElement sensor) => sensor.GetProperty("Name").GetString()!;

    private static string Metric(JsonElement sensor) =>
        sensor.GetProperty("Parameters").GetProperty("Metric").GetString()!;

    [Fact]
    public void EverySensorRequestsExactlyOneMetric()
    {
        // One sensor is one telemetry stream. A comma-separated metric list would put several
        // different quantities onto the same stream.
        Sensors.ShouldNotBeEmpty();

        foreach (var sensor in Sensors)
        {
            var parameters = sensor.GetProperty("Parameters");
            parameters.TryGetProperty("Metrics", out _).ShouldBeFalse(
                $"{Name(sensor)} still declares a plural 'Metrics' list");
            Metric(sensor).ShouldNotBeNullOrWhiteSpace();
            StockMetricCatalog.IsKnown(Metric(sensor)).ShouldBeTrue(
                $"{Name(sensor)} requests unknown metric '{Metric(sensor)}'");
        }
    }

    [Fact]
    public void EverySensorDeclaresTheSchemaAndUnitOfItsMetric()
    {
        foreach (var sensor in Sensors)
        {
            var metric = Metric(sensor);

            sensor.GetProperty("SensorInfo").GetProperty("Schema").GetString()
                .ShouldBe(StockMetricCatalog.SchemaFor(metric), $"{Name(sensor)} schema");

            // Unit lives on Report, matching the other examples in this repo.
            sensor.GetProperty("Report").GetProperty("Unit").GetString()
                .ShouldBe(StockMetricCatalog.UnitFor(metric), $"{Name(sensor)} unit");
        }
    }

    [Fact]
    public void EverySensorUsesTheSystemGroup()
    {
        // A TWSE quote is polled over HTTP, not read from an analog channel; the group also feeds
        // the auto-generated DTMI namespace, so equivalent sensors must not diverge here.
        foreach (var sensor in Sensors)
        {
            sensor.GetProperty("SensorGroup").GetString()
                .ShouldBe(nameof(SensorGroup.SYS), $"{Name(sensor)} group");
        }
    }

    [Fact]
    public void SensorNamesAreUnique()
    {
        var names = Sensors.Select(Name).ToList();
        names.Distinct().Count().ShouldBe(names.Count);
    }
}
