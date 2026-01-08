using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;

namespace Weda.SubNode.Core.Communication.Common;

/// <summary>
/// Base class for Request-Response communication pattern implementations.
/// Suitable for protocols like TCP, HTTP, Modbus RTU where each request expects a response.
/// </summary>
/// <typeparam name="TRequest">Request message type (e.g., byte[] for TCP, HttpRequestMessage for HTTP)</typeparam>
/// <typeparam name="TResponse">Response message type (e.g., byte[] for TCP, HttpResponseMessage for HTTP)</typeparam>
public abstract class RequestResponseCommunicationBase<TRequest, TResponse>
    : CommunicationBase, IRequestResponseCommunication<TRequest, TResponse>
{
    protected RequestResponseCommunicationBase(
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
    }

    /// <summary>
    /// Send a request and wait for response.
    /// Implementations should handle protocol-specific request-response logic.
    /// </summary>
    /// <param name="request">Request message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response message</returns>
    public abstract Task<TResponse> RequestAsync(TRequest request, CancellationToken cancellationToken = default);
}
