namespace StockMonitor.Protocols;

/// <summary>
/// Strongly-typed sensor parameters for stock monitoring.
/// </summary>
public record StockSensorParameters(string StockCode, List<string> Metrics);