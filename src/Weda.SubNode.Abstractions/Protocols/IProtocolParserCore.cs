using Weda.SubNode.Abstractions.Communication;

namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Core protocol parser interface - All parsers must implement this.
/// This interface only contains essential properties without forcing implementation patterns.
/// </summary>
public interface IProtocolParserCore
{
    /// <summary>
    /// Underlying communication instance (Parser owns Communication)
    /// </summary>
    ICommunication Communication { get; }

    /// <summary>
    /// Protocol name (e.g., "Modbus TCP", "ISensing MQTT", "OPC-UA")
    /// </summary>
    string ProtocolName { get; }

    /// <summary>
    /// Supported data types for this protocol
    /// </summary>
    IReadOnlyList<string> SupportedDataTypes { get; }

    /// <summary>
    /// Whether this protocol supports bidirectional communication (read and write)
    /// </summary>
    bool SupportsBidirectional { get; }

    /// <summary>
    /// Refresh internal sensor metadata after configuration changes.
    /// Called by DeviceBase when sensors are added/removed/updated at runtime.
    /// Parsers that cache sensor metadata must implement this to update their internal state.
    /// </summary>
    void RefreshSensorMetadata() { }
}
