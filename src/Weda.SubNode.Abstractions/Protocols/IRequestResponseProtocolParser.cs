using ErrorOr;

using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Request-Response pattern protocol parser.
/// Used for synchronous protocols where device actively polls/requests data.
/// Examples: Modbus TCP/RTU, OPC-UA Read, REST API, BACnet
///
/// Parser is responsible for:
/// - Communication with the device
/// - Protocol-specific parsing logic
/// - Mapping protocol fields to ResourceIds (internally using DeviceConfiguration)
/// </summary>
public interface IRequestResponseProtocolParser : IProtocolParserCore
{
    /// <summary>
    /// Read telemetry data synchronously for all enabled sensors (request-response).
    /// Parser internally handles all mapping logic using DeviceConfiguration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of telemetry measures with ResourceIds</returns>
    Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Read telemetry data for specific sensors only (request-response).
    /// Used by per-sensor interval scheduling to read only sensors that are due.
    /// Default implementation calls ReadTelemetryAsync() and filters results.
    /// Parsers can override for optimized per-sensor reading.
    /// </summary>
    /// <param name="sensorResourceIds">ResourceIds of sensors to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of telemetry measures for specified sensors</returns>
    Task<List<TelemetryMeasure>> ReadTelemetryAsync(
        IEnumerable<string> sensorResourceIds,
        CancellationToken cancellationToken = default)
    {
        // Default implementation: read all and filter
        // Parsers can override for optimized per-sensor reading
        return ReadTelemetryAsync(cancellationToken)
            .ContinueWith(t =>
            {
                var requestedIds = sensorResourceIds.ToHashSet();
                return t.Result.Where(m => requestedIds.Contains(m.ResourceId)).ToList();
            }, cancellationToken);
    }

    /// <summary>
    /// Execute command synchronously (request-response).
    /// Device calls this method and waits for command execution confirmation.
    /// </summary>
    /// <param name="command">Device command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command execution result or error</returns>
    Task<ErrorOr<object>> ExecuteCommandAsync(
        DeviceCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Write sensor data to device (if protocol supports write operations).
    /// </summary>
    /// <param name="measures">Telemetry measures to write</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if write succeeded</returns>
    Task<bool> WriteSensorDataAsync(
        IEnumerable<TelemetryMeasure> measures,
        CancellationToken cancellationToken = default);
}
