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
    /// Refresh internal sensor metadata after configuration changes.
    /// Called by DeviceBase when sensors are added/removed/updated at runtime.
    /// Parsers that cache sensor metadata must implement this to update their internal state.
    /// </summary>
    void RefreshSensorMetadata() { }
}
