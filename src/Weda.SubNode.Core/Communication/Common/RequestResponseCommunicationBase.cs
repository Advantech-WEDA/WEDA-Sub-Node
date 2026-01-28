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
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    protected RequestResponseCommunicationBase(
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
    }

    /// <summary>
    /// Send a request and wait for response.
    /// When RequestLock is enabled (default), only one request can be processed at a time.
    /// Uses ReadTimeoutMs as the lock acquisition timeout to prevent queue buildup.
    /// </summary>
    /// <param name="request">Request message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response message</returns>
    /// <exception cref="TimeoutException">Thrown when lock cannot be acquired within timeout</exception>
    public async Task<TResponse> RequestAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        if (Settings.RequestLock)
        {
            var acquired = await _requestLock.WaitAsync(Settings.ReadTimeoutMs, cancellationToken);
            if (!acquired)
            {
                _logger.LogWarning(
                    "RequestLock timeout after {TimeoutMs}ms. " +
                    "This indicates sensor interval is too small for the device read speed. " +
                    "Increase sensor interval or optimize device communication.",
                    Settings.ReadTimeoutMs);
                throw new TimeoutException(
                    $"RequestLock timeout after {Settings.ReadTimeoutMs}ms. " +
                    "Sensor interval may be too small for device read speed.");
            }

            try
            {
                return await RequestAsyncCore(request, cancellationToken);
            }
            finally
            {
                _requestLock.Release();
            }
        }

        return await RequestAsyncCore(request, cancellationToken);
    }

    /// <summary>
    /// Core implementation for sending a request and waiting for response.
    /// Implementations should handle protocol-specific request-response logic.
    /// </summary>
    /// <param name="request">Request message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response message</returns>
    protected abstract Task<TResponse> RequestAsyncCore(TRequest request, CancellationToken cancellationToken = default);
}
