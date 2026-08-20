using Weda.SubNode.Abstractions.Telemetry;

namespace StockMonitor.Protocols;

/// <summary>
/// A configured sensor bound to the single metric it reports, resolved once at construction time.
/// </summary>
/// <param name="Sensor">The configured sensor.</param>
/// <param name="Parameters">Its parsed stock code and metric.</param>
public record StockSensorBinding(Sensor Sensor, StockSensorParameters Parameters);
