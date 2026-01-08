using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;

namespace Weda.SubNode.Core.Communication.WebSocket;

/// <summary>
/// WebSocket communication implementation for bidirectional streaming data.
/// Implements IStreamingCommunication for continuous data flow scenarios.
/// </summary>
public class WebSocketCommunication : StreamingCommunicationBase<byte[], byte[]>
{
    private readonly string _uri;
    private ClientWebSocket? _webSocket;

    /// <summary>
    /// Buffer size for receiving WebSocket messages (default: 4KB)
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 4096;

    /// <summary>
    /// Keep-alive interval for WebSocket connection (default: 30 seconds)
    /// </summary>
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    public WebSocketCommunication(
        string uri,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
        if (string.IsNullOrWhiteSpace(uri))
            throw new ArgumentException("URI cannot be null or empty", nameof(uri));

        _uri = uri;
    }

    /// <summary>
    /// WebSocket URI
    /// </summary>
    public string Uri => _uri;

    /// <summary>
    /// Check if WebSocket is connected and in Open state
    /// </summary>
    public new bool IsConnected => base.IsConnected &&
                                   _webSocket?.State == WebSocketState.Open;

    protected override async Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            State = CommunicationState.Connecting;

            _webSocket?.Dispose();
            _webSocket = new ClientWebSocket();

            // Configure WebSocket options
            _webSocket.Options.KeepAliveInterval = KeepAliveInterval;

            // Apply security settings if configured
            if (Settings.Security != null)
            {
                ApplySecuritySettings(_webSocket.Options, Settings.Security);
            }

            _logger.LogDebug("Connecting to WebSocket: {Uri}", _uri);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(Settings.ConnectionTimeoutMs);

            await _webSocket.ConnectAsync(new Uri(_uri), timeoutCts.Token);

            State = CommunicationState.Connected;
            _logger.LogInformation("WebSocket connected to {Uri}", _uri);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to WebSocket: {Uri}", _uri);
            State = CommunicationState.Error;
            return false;
        }
    }

    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_webSocket == null || State == CommunicationState.Disconnected)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Disconnecting from WebSocket");

            if (_webSocket.State == WebSocketState.Open)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(Settings.WriteTimeoutMs);

                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client closing",
                    timeoutCts.Token);
            }

            _logger.LogInformation("WebSocket disconnected from {Uri}", _uri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during WebSocket disconnect");
        }
        finally
        {
            State = CommunicationState.Disconnected;
            _webSocket?.Dispose();
            _webSocket = null;
        }
    }

    /// <summary>
    /// Establish a bidirectional stream with the WebSocket server.
    /// Outgoing messages are sent in the background while incoming messages are yielded.
    /// </summary>
    public override async IAsyncEnumerable<byte[]> StreamAsync(
        IAsyncEnumerable<byte[]> requests,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_webSocket == null || !IsConnected)
        {
            throw new InvalidOperationException("WebSocket is not connected");
        }

        // Start sending requests in background
        var sendTask = SendRequestsAsync(requests, cancellationToken);

        // Receive responses
        var buffer = new byte[ReceiveBufferSize];

        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            WebSocketReceiveResult result;
            using var ms = new MemoryStream();

            do
            {
                try
                {
                    result = await _webSocket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("WebSocket server initiated close: {Status} - {Description}",
                            result.CloseStatus, result.CloseStatusDescription);
                        State = CommunicationState.Disconnected;
                        yield break;
                    }

                    ms.Write(buffer, 0, result.Count);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }
                catch (WebSocketException ex)
                {
                    _logger.LogError(ex, "WebSocket receive error");
                    State = CommunicationState.Error;
                    yield break;
                }
            }
            while (!result.EndOfMessage);

            var data = ms.ToArray();
            _logger.LogTrace("Received {Count} bytes from WebSocket", data.Length);
            yield return data;
        }

        // Wait for send task to complete
        try
        {
            await sendTask;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Send task completed with error");
        }
    }

    private async Task SendRequestsAsync(
        IAsyncEnumerable<byte[]> requests,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in requests.WithCancellation(cancellationToken))
            {
                if (_webSocket == null || !IsConnected)
                    break;

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(request),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken);

                _logger.LogTrace("Sent {Count} bytes to WebSocket", request.Length);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending to WebSocket");
            throw;
        }
    }

    /// <summary>
    /// Send a single message to the WebSocket server.
    /// Useful for one-off messages outside of streaming context.
    /// </summary>
    public async Task<bool> SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_webSocket == null || !IsConnected)
        {
            _logger.LogWarning("Cannot send: WebSocket is not connected");
            return false;
        }

        try
        {
            await _webSocket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                cancellationToken);

            _logger.LogTrace("Sent {Count} bytes to WebSocket", data.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending to WebSocket");
            return false;
        }
    }

    /// <summary>
    /// Send a text message to the WebSocket server.
    /// </summary>
    public async Task<bool> SendTextAsync(string text, CancellationToken cancellationToken = default)
    {
        if (_webSocket == null || !IsConnected)
        {
            _logger.LogWarning("Cannot send: WebSocket is not connected");
            return false;
        }

        try
        {
            var data = System.Text.Encoding.UTF8.GetBytes(text);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);

            _logger.LogTrace("Sent text message ({Count} bytes) to WebSocket", data.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending text to WebSocket");
            return false;
        }
    }

    /// <summary>
    /// Apply security settings to WebSocket options
    /// </summary>
    private void ApplySecuritySettings(ClientWebSocketOptions options, SecuritySettings security)
    {
        // Apply credentials if configured
        if (!string.IsNullOrEmpty(security.Username) && !string.IsNullOrEmpty(security.Password))
        {
            options.Credentials = new System.Net.NetworkCredential(security.Username, security.Password);
            _logger.LogDebug("WebSocket authentication configured with username: {Username}", security.Username);
        }

        // Note: TLS/SSL is handled automatically by using wss:// URI scheme
        // Client certificates can be added to options.ClientCertificates if needed
        if (!string.IsNullOrEmpty(security.ClientCertificatePath))
        {
            try
            {
                var clientCert = string.IsNullOrEmpty(security.ClientCertificatePassword)
                    ? System.Security.Cryptography.X509Certificates.X509CertificateLoader
                        .LoadCertificateFromFile(security.ClientCertificatePath)
                    : System.Security.Cryptography.X509Certificates.X509CertificateLoader
                        .LoadPkcs12FromFile(security.ClientCertificatePath, security.ClientCertificatePassword);

                options.ClientCertificates.Add(clientCert);
                _logger.LogInformation("WebSocket client certificate loaded from: {Path}", security.ClientCertificatePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load client certificate from {Path}", security.ClientCertificatePath);
                throw;
            }
        }
    }
}

/// <summary>
/// Static factory for creating WebSocket communication instances
/// </summary>
public static class WebSocket
{
    /// <summary>
    /// Default WebSocket communication (ws://localhost:8080)
    /// </summary>
    public static WebSocketCommunication Default => Create("ws://localhost:8080");

    /// <summary>
    /// Create WebSocket communication with specified URI
    /// </summary>
    public static WebSocketCommunication Create(
        string uri,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
    {
        return new WebSocketCommunication(uri, settings, logger);
    }

    /// <summary>
    /// Create WebSocket communication with host and port
    /// </summary>
    public static WebSocketCommunication Create(
        string host,
        int port,
        string path = "/",
        bool secure = false,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
    {
        var scheme = secure ? "wss" : "ws";
        var uri = $"{scheme}://{host}:{port}{path}";
        return new WebSocketCommunication(uri, settings, logger);
    }
}
