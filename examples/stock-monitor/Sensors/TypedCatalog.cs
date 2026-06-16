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
    /// Comma-separated metric list to request, e.g.
    /// <c>"Current,Volume,Open,High,Low,Change,ChangePercent"</c>.
    /// Free-form string today; could become a flags enum later.
    /// </summary>
    [Required, JsonPropertyName("metrics")]
    public string Metrics { get; init; } = string.Empty;
}

public class StockQuoteSensor : IConfigurableSensor<StockQuoteParameters>
{
    public static string DeviceTypeName => TwseStockDevice.DeviceTypeName;
    public static string SensorTypeName => "stock-quote";
    public static string? Description   => "One TWSE stock code's real-time quote bundle.";
}
