namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Telemetry data
/// </summary>
public record TelemetryData
{
    /// <summary>
    /// Array of telemetry measures
    /// </summary>
    public required List<TelemetryMeasure> Measures { get; init; }
}

/// <summary>
/// Single telemetry measure
/// </summary>
public record TelemetryMeasure
{
    /// <summary>
    /// Sensor Resource ID (matches Sensor.ResourceId, e.g., "21af0dc4-9254-5389-a7dd-df64d7cf782c")
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Sensor Short ID (matches the last five characters of Sensor.ResourceId, e.g., "f782c")
    /// </summary>
    public string SensorId => ResourceId[^5..];

    /// <summary>
    /// Value object (protocol-parsed physical value)
    /// Unit and dataType are defined in DTDL
    /// </summary>
    public required object Value { get; init; }

    /// <summary>
    /// Timestamp in milliseconds (Unix epoch)
    /// </summary>
    public long Timestamp { get; init; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Optional metadata dictionary for additional telemetry information.
    /// Examples: stock name, exchange, field type (open/high/low/close), unit, etc.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}