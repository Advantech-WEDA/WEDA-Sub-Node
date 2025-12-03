using ErrorOr;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Generic protocol parser interface for bidirectional protocol conversion.
/// This is the unified design supporting both binary protocols (Modbus) and text protocols (ISensing).
///
/// Design principles:
/// - TRawData: Protocol-specific raw data format (e.g., ushort[] for Modbus, string for ISensing JSON)
/// - TValue: Intermediate parsed value type (e.g., object for Modbus primitives, ISensingSensorData for ISensing)
/// - Supports both low-level (Parse/Encode raw data) and high-level (ParseSensorData/EncodeCommand) operations
/// </summary>
/// <typeparam name="TRawData">Protocol-specific raw data type (ushort[], byte[], string, etc.)</typeparam>
/// <typeparam name="TValue">Intermediate parsed value type</typeparam>
public interface IProtocolParser<TRawData, TValue>
{
    // ===== Communication Layer =====

    /// <summary>
    /// Gets the underlying communication instance.
    /// This property allows DeviceOrchestrator to access ICommunication for connection management,
    /// while keeping DeviceBase isolated from communication payload details.
    /// </summary>
    ICommunication Communication { get; }

    // ===== Low-Level Protocol Operations =====

    /// <summary>
    /// Parse raw protocol data to intermediate value.
    /// This is the protocol-specific parsing logic (e.g., Modbus registers -> int/float).
    /// </summary>
    /// <param name="rawData">Protocol-specific raw data</param>
    /// <returns>Intermediate parsed value</returns>
    TValue Parse(TRawData rawData);

    /// <summary>
    /// Encode intermediate value to raw protocol data.
    /// This is the reverse operation of Parse.
    /// </summary>
    /// <param name="value">Intermediate value</param>
    /// <returns>Protocol-specific raw data</returns>
    TRawData Encode(TValue value);

    // ===== High-Level Telemetry Operations =====

    /// <summary>
    /// Parse protocol payload to telemetry measures.
    /// Converts raw protocol data to standardized TelemetryMeasure list.
    /// </summary>
    /// <param name="payload">Raw protocol payload (typically byte[])</param>
    /// <param name="sensorMapping">Optional sensor mapping for field-to-resourceId conversion</param>
    /// <returns>List of telemetry measures</returns>
    List<TelemetryMeasure> ParseSensorData(byte[] payload, SensorMapping? sensorMapping = null);

    /// <summary>
    /// Encode telemetry measures to protocol payload.
    /// Used for simulation or forwarding scenarios.
    /// </summary>
    /// <param name="measures">Telemetry measures to encode</param>
    /// <returns>Protocol payload as byte array</returns>
    byte[] EncodeSensorData(IEnumerable<TelemetryMeasure> measures);

    /// <summary>
    /// Encode device command to protocol payload.
    /// </summary>
    /// <param name="command">Device command to encode</param>
    /// <returns>Protocol payload as byte array</returns>
    byte[] EncodeCommand(DeviceCommand command);
}

/// <summary>
/// Non-generic protocol parser interface for backward compatibility and simple scenarios.
/// This is equivalent to IProtocolParser&lt;byte[], object&gt;.
/// </summary>
public interface IProtocolParser : IProtocolParser<byte[], object>
{
    /// <summary>
    /// Parse protocol payload to telemetry measures (string overload for text-based protocols like JSON).
    /// </summary>
    /// <param name="payload">Protocol payload as string</param>
    /// <param name="sensorMapping">Optional sensor mapping</param>
    /// <returns>List of telemetry measures</returns>
    List<TelemetryMeasure> ParseSensorData(string payload, SensorMapping? sensorMapping = null);
}

/// <summary>
/// Protocol parser metadata interface.
/// Provides information about parser capabilities and supported data types.
/// </summary>
public interface IProtocolParserMetadata
{
    /// <summary>
    /// Protocol name (e.g., "Modbus", "ISensing", "OPC-UA")
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// Supported data types for this protocol
    /// </summary>
    IReadOnlyList<string> SupportedDataTypes { get; }

    /// <summary>
    /// Whether this protocol supports bidirectional communication
    /// </summary>
    bool SupportsBidirectional { get; }
}

/// <summary>
/// Base class for protocol parsers with common functionality.
/// Provides a convenient starting point for implementing protocol parsers.
/// </summary>
/// <typeparam name="TRawData">Protocol-specific raw data type</typeparam>
/// <typeparam name="TValue">Intermediate parsed value type</typeparam>
public abstract class ProtocolParserBase<TRawData, TValue> : IProtocolParser<TRawData, TValue>, IProtocolParserMetadata
{
    /// <inheritdoc />
    public abstract ICommunication Communication { get; }

    /// <inheritdoc />
    public abstract string ProtocolName { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> SupportedDataTypes { get; }

    /// <inheritdoc />
    public virtual bool SupportsBidirectional => true;

    /// <inheritdoc />
    public abstract TValue Parse(TRawData rawData);

    /// <inheritdoc />
    public abstract TRawData Encode(TValue value);

    /// <inheritdoc />
    public abstract List<TelemetryMeasure> ParseSensorData(byte[] payload, SensorMapping? sensorMapping = null);

    /// <inheritdoc />
    public abstract byte[] EncodeSensorData(IEnumerable<TelemetryMeasure> measures);

    /// <inheritdoc />
    public abstract byte[] EncodeCommand(DeviceCommand command);
}
