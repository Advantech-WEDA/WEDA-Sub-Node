using ErrorOr;
using Weda.SubNode.Abstractions.Devices;
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
    /// Read telemetry data synchronously (request-response).
    /// Parser internally handles all mapping logic using DeviceConfiguration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of telemetry measures with ResourceIds</returns>
    Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default);

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
