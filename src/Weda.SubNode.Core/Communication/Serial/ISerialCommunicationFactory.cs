using Weda.SubNode.Abstractions.Communication;

namespace Weda.SubNode.Core.Communication.Serial;

/// <summary>
/// Factory for managing shared serial communication instances.
/// Implementations Multiton pattern with reference counting for serial port sharing.
/// </summary>
/// <remarks>
/// Used when multiple devices share the same serial port.
/// Since serial communication is half-duplex, all devices using the same port must
/// share a single SerialCommunication instance to serialize requests.
/// </remarks>
public interface ISerialCommunicationFactory : IDisposable
{
    /// <summary>
    /// Gets or creates a serial communication instance for the specified port.
    /// If an instance already exists, returns it and increments the reference count.
    /// </summary>
    /// <param name="portName">The serial port name (e.g. "/dev/ttyUSB0", "COM1")</param>
    /// <param name="settings">Serial port settings</param>
    /// <param name="connectionSettings">Optional connection settings</param>
    /// <returns>A serial communication instance (may be shared)</returns>
    SerialCommunication GetOrCreate(
        string portName,
        SerialCommunicationSettings settings,
        ConnectionSettings? connectionSettings = null);

    /// <summary>
    /// Releases a reference to a serial communication instance.
    /// When reference count reaches zero, the instance is disposed.
    /// </summary>
    bool Release(string portName);

    /// <summary>
    /// Gets the current reference count for serial port.
    /// </summary>
    int GetReferenceCount(string portName);

    /// <summary>
    /// Checks if a serial communication instance exists for the specified port.
    /// </summary>
    bool Contains(string portName);
}