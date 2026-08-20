using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace StockMonitor.Sensors;

public static class TwseStockDevice
{
    public const string DeviceTypeName = "twse-stock";
}

public class TwseStockCommunication { }
public class TwseStockProperties { }

public class TwseStockConfiguration
    : IConfigurableDevice<TwseStockCommunication, TwseStockProperties>
{
    public static string DeviceTypeName => TwseStockDevice.DeviceTypeName;
    public static string? Description   => "Taiwan Stock Exchange (TWSE) real-time quote HTTP poller.";
}

public class StockQuoteParameters
{
    [Required, JsonPropertyName("stockCode")]
    public string StockCode { get; init; } = string.Empty;

    /// <summary>
    /// The single metric this sensor reports, e.g. <c>"Current"</c> or <c>"Volume"</c>.
    /// See <c>StockMetricCatalog</c> for the supported names and the schema each one reports.
    /// </summary>
    /// <remarks>
    /// One metric per sensor: a sensor is a single telemetry stream, so a price and a share count
    /// cannot share one. Add a sensor per metric instead.
    /// </remarks>
    [Required, JsonPropertyName("metric")]
    public string Metric { get; init; } = string.Empty;
}

public class StockQuoteSensor : IConfigurableSensor<StockQuoteParameters>
{
    public static string DeviceTypeName => TwseStockDevice.DeviceTypeName;
    public static string SensorTypeName => "stock-quote";
    public static string? Description   => "One metric of one TWSE stock code's real-time quote.";
}
