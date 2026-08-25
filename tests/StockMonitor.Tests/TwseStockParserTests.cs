using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using StockMonitor.Communication;
using StockMonitor.Models;
using StockMonitor.Protocols;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

using Xunit;

namespace StockMonitor.Tests;

/// <summary>
/// Pins the response-to-measure mapping. A sensor is one telemetry stream, so each configured
/// sensor must yield exactly one measure per poll, carrying the sensor's own resource id.
/// </summary>
public class TwseStockParserTests
{
    private const string Code = "2395";

    private static TwseStockParser CreateParser(DeviceConfiguration configuration)
    {
        var stockClient = new TwseStockClient(new HttpClient(), NullLogger<TwseStockClient>.Instance);
        var communication = new HttpCommunication(stockClient);
        return new TwseStockParser(configuration, communication, NullLogger<TwseStockParser>.Instance);
    }

    private static DeviceConfiguration ConfigurationWith(params string[] metrics)
    {
        var configuration = new DeviceConfiguration { DeviceName = "StockMonitorConfig" };

        foreach (var metric in metrics)
        {
            configuration.Sensors.Add(new Sensor
            {
                // Assigned by DeviceInitializer at runtime, which these tests do not run.
                ResourceId = $"rid-{metric.ToLowerInvariant()}",
                Name = $"stock_{Code}_{metric.ToLowerInvariant()}",
                SensorGroup = SensorGroup.SYS,
                Parameters = new Dictionary<string, object>
                {
                    ["StockCode"] = Code,
                    ["Metric"] = metric,
                },
                SensorInfo = new SensorInfo { Schema = StockMetricCatalog.SchemaFor(metric) },
                Report = new SensorReport { Enabled = true, Interval = 10_000 },
            });
        }

        return configuration;
    }

    private static TwseStockResponse ResponseWithQuote() => new()
    {
        RtCode = "0000",
        MsgArray =
        [
            new TwseStockQuote
            {
                Code = Code,
                Name = "Advantech",
                TradePrice = "412.50",
                OpenPrice = "410.00",
                HighPrice = "415.00",
                LowPrice = "409.00",
                YesterdayPrice = "408.00",
                AccumulatedVolume = "12345",
                TimestampLong = "1755000000000",
            },
        ],
    };

    private static HashSet<string> AllResourceIds(DeviceConfiguration configuration) =>
        configuration.Sensors.Select(s => s.ResourceId).ToHashSet();

    [Fact]
    public void ParseResponse_EmitsExactlyOneMeasurePerSensor()
    {
        // Regression guard: the parser previously emitted one measure per metric while stamping
        // every one of them with the same sensor's ResourceId, collapsing seven different
        // quantities onto a single telemetry stream.
        var configuration = ConfigurationWith("Current", "Volume", "Open", "High", "Low", "Change", "ChangePercent");
        var parser = CreateParser(configuration);

        var measures = parser.ParseResponse(ResponseWithQuote(), AllResourceIds(configuration));

        measures.Count.ShouldBe(configuration.Sensors.Count);
        measures.Select(m => m.ResourceId).Distinct().Count().ShouldBe(configuration.Sensors.Count);
    }

    [Fact]
    public void ParseResponse_LeavesMeasureMetadataUnset()
    {
        // A measure's metadata is the framework's chunked-transfer descriptor. A numeric value is
        // never chunked, so it never receives a transferId -- and the WedaNode telemetry proxy
        // rejects any measure that carries metadata without one, which silently drops every
        // reading. Mirrors the equivalent guard on the CFX parser.
        var configuration = ConfigurationWith("Current", "Volume");
        var parser = CreateParser(configuration);

        var measures = parser.ParseResponse(ResponseWithQuote(), AllResourceIds(configuration));

        measures.ShouldNotBeEmpty();
        measures.ShouldAllBe(m => m.Metadata == null);
    }

    [Fact]
    public void ParseResponse_ReportsVolumeAsLongAndPricesAsDouble()
    {
        // The declared schema has to match the runtime type: volume is a share count, everything
        // else is a price or a ratio.
        var configuration = ConfigurationWith("Current", "Volume");
        var parser = CreateParser(configuration);

        var measures = parser.ParseResponse(ResponseWithQuote(), AllResourceIds(configuration));

        ValueOf(measures, configuration, "Current").ShouldBeOfType<double>();
        ValueOf(measures, configuration, "Volume").ShouldBeOfType<long>();
    }

    [Fact]
    public void ParseResponse_SkipsSensorsThatWereNotRequested()
    {
        var configuration = ConfigurationWith("Current", "Volume");
        var parser = CreateParser(configuration);
        var onlyFirst = new HashSet<string> { configuration.Sensors[0].ResourceId };

        var measures = parser.ParseResponse(ResponseWithQuote(), onlyFirst);

        measures.Select(m => m.ResourceId).ShouldBe([configuration.Sensors[0].ResourceId]);
    }

    [Fact]
    public void ParseResponse_UsesThePublisherTimestamp()
    {
        var configuration = ConfigurationWith("Current");
        var parser = CreateParser(configuration);

        var measures = parser.ParseResponse(ResponseWithQuote(), AllResourceIds(configuration));

        measures.ShouldHaveSingleItem().Timestamp.ShouldBe(1755000000000L);
    }

    [Fact]
    public void ParseResponse_ReturnsNothingWhenTheApiReportsFailure()
    {
        var configuration = ConfigurationWith("Current");
        var parser = CreateParser(configuration);

        var measures = parser.ParseResponse(
            new TwseStockResponse { RtCode = "9999", RtMessage = "failure" },
            AllResourceIds(configuration));

        measures.ShouldBeEmpty();
    }

    private static object? ValueOf(
        List<TelemetryMeasure> measures, DeviceConfiguration configuration, string metric)
    {
        var sensor = configuration.Sensors.Single(
            s => (string)s.Parameters!["Metric"] == metric);

        return measures.Single(m => m.ResourceId == sensor.ResourceId).Value;
    }
}
