using Weda.SubNode.Abstractions.Events;

namespace Weda.SubNode.Abstractions.Communication;

/// <summary>
/// Communication interface for protocol-agnostic device communication
/// </summary>
public interface ICommunication : IDisposable
{
    /// <summary>
    /// Connection settings (retry, timeout, etc.)
    /// </summary>
    ConnectionSettings Settings { get; }

    /// <summary>
    /// Current communication state
    /// </summary>
    CommunicationState State { get; }

    /// <summary>
    /// Is connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connect to device
    /// </summary>
    Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect from device
    /// </summary>
    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Read raw data from device (protocol-specific)
    /// </summary>
    Task<byte[]> ReadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Write raw data to device (protocol-specific)
    /// </summary>
    Task<bool> WriteAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event: Communication state changed
    /// </summary>
    event EventHandler<ConnectionStateChangedEvent>? StateChanged;
}
