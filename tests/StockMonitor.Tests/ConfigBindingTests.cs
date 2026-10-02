using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using StockMonitor.Communication;
using StockMonitor.Protocols;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace StockMonitor.Tests;

/// <summary>
/// Binds the shipped <c>devicecfg.json</c> the way the host does and drives the parser with the
/// result. <c>Sensor.Parameters</c> is a free-form <c>Dictionary&lt;string, object&gt;</c> looked up
/// by ordinal key, so a parameter the parser reads under one spelling and the configuration writes
/// under another binds to nothing and the sensor is silently skipped. These tests pin the spelling.
/// </summary>
public class ConfigBindingTests
{
    private static IEnumerable<Sensor> StockSensors(DeviceConfiguration configuration) =>
        configuration.Sensors.Where(s => !Heartbeat.IsHeartbeat(s));

    private static DeviceConfiguration BindShippedConfig()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../../examples/http-stock-quotes/devicecfg.json"));

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(path, optional: false)
            .Build();

        return configuration
            .GetSection("DeviceConfigs:StockMonitorConfig")
            .Get<DeviceConfiguration>()
            ?? throw new InvalidOperationException("StockMonitorConfig section did not bind.");
    }

    [Fact]
    public void ParametersBindWithTheKeysTheParserReads()
    {
        var configuration = BindShippedConfig();

        StockSensors(configuration).ShouldNotBeEmpty();

        foreach (var sensor in StockSensors(configuration))
        {
            sensor.Parameters.ShouldNotBeNull($"{sensor.Name} bound no parameters");
            sensor.Parameters.ShouldContainKey("StockCode");
            sensor.Parameters.ShouldContainKey("Metric");

            // The plural spelling the example used before one-metric-per-sensor must not linger.
            sensor.Parameters.ShouldNotContainKey("Metrics");
        }
    }

    [Fact]
    public void EveryConfiguredSensorProducesAMeasure()
    {
        // End-to-end over the real config: if a parameter failed to bind, the parser would skip the
        // sensor and this count would silently drop.
        var configuration = BindShippedConfig();

        var index = 0;
        foreach (var sensor in configuration.Sensors)
        {
            sensor.ResourceId = $"rid-{index++}";
        }

        var stockClient = new TwseStockClient(new HttpClient(), NullLogger<TwseStockClient>.Instance);
        var parser = new TwseStockParser(
            configuration,
            new HttpCommunication(stockClient),
            NullLogger<TwseStockParser>.Instance);

        var measures = parser.ParseResponse(
            TwseFixtures.QuotesFor("2395", "2330"),
            configuration.Sensors.Select(s => s.ResourceId).ToHashSet());

        // The heartbeat is synthesised by the SDK, never parsed from a quote.
        measures.Count.ShouldBe(StockSensors(configuration).Count());
        measures.ShouldAllBe(m => m.Metadata == null);
    }
}
