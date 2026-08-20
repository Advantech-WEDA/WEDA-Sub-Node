namespace StockMonitor.Protocols;

/// <summary>
/// Strongly-typed sensor parameters for stock monitoring: one sensor reports one metric of one
/// stock code, because a sensor is a single telemetry stream.
/// </summary>
/// <param name="StockCode">TWSE stock code, for example <c>2395</c>.</param>
/// <param name="Metric">Metric name from <see cref="StockMetricCatalog"/>.</param>
public record StockSensorParameters(string StockCode, string Metric);
