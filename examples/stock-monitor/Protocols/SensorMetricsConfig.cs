using Weda.SubNode.Abstractions.Telemetry;

namespace StockMonitor.Protocols;


/// <summary>
/// Pre-processed sensor configuration record.
/// </summary>
public record SensorMetricsConfig(Sensor Sensor, StockSensorParameters Parameters);