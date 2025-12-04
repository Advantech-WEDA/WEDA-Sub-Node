using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;

namespace Weda.SubNode.Core.Communication;

/// <summary>
/// Base class for Streaming communication pattern implementations.
/// Suitable for protocols like WebSocket, gRPC bidirectional streaming, Server-Sent Events
/// where data flows continuously in both directions.
/// </summary>
/// <typeparam name="TRequest">Outgoing message type</typeparam>
/// <typeparam name="TResponse">Incoming message type</typeparam>
public abstract class StreamingCommunicationBase<TRequest, TResponse>
    : CommunicationBase, IStreamingCommunication<TRequest, TResponse>
{
    protected StreamingCommunicationBase(
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
    }

    /// <summary>
    /// Establish a bidirectional stream.
    /// Implementations should handle protocol-specific streaming logic.
    /// </summary>
    /// <param name="requests">Outgoing message stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Incoming message stream</returns>
    public abstract IAsyncEnumerable<TResponse> StreamAsync(
        IAsyncEnumerable<TRequest> requests,
        CancellationToken cancellationToken = default);
}
