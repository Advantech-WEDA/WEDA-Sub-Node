using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Core.Communication;

/// <summary>
/// Static factory for creating TCP communication instances
/// Provides convenient creation methods following the pattern:
/// - Tcp.Default (localhost:502)
/// - Tcp.Create(host, port)
/// </summary>
public static class Tcp
{
    /// <summary>
    /// Create a TCP communication instance with default settings (localhost:502)
    /// </summary>
    public static TcpCommunication Default => Create("localhost", 502);

    /// <summary>
    /// Create a TCP communication instance with specified host and port
    /// </summary>
    /// <param name="host">TCP server host address</param>
    /// <param name="port">TCP server port</param>
    /// <param name="settings">Optional connection settings</param>
    /// <param name="logger">Optional logger</param>
    /// <returns>A new TcpCommunication instance</returns>
    public static TcpCommunication Create(
        string host,
        int port,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
    {
        return new TcpCommunication(host, port, settings, logger);
    }
}

/// <summary>
/// Extension methods for creating TcpCommunication from DeviceConfiguration
/// </summary>
public static class TcpCommunicationExtensions
{
    /// <summary>
    /// Create TcpCommunication from DeviceConfiguration.Communication dictionary
    /// </summary>
    public static TcpCommunication CreateTcpCommunication(
        this DeviceConfiguration config,
        ConnectionSettings? connectionSettings = null,
        ILogger<CommunicationBase>? logger = null)
    {
        var comm = config.Communication;
        var host = comm.GetValueOrDefault("Host") as string ?? "localhost";
        var port = Convert.ToInt32(comm.GetValueOrDefault("Port", 502));

        return new TcpCommunication(host, port, connectionSettings, logger);
    }
}

/// <summary>
/// TCP communication implementation (for Modbus TCP, raw TCP, etc.)
/// Implements request-response pattern where each request expects a response.
/// </summary>
public class TcpCommunication : RequestResponseCommunicationBase<byte[], byte[]>
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public TcpCommunication(
        string host,
        int port,
        ConnectionSettings? settings = null,
        ILogger<CommunicationBase>? logger = null)
        : base(settings, logger)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _port = port;
    }

    protected override async Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Connecting to TCP at {Host}:{Port}", _host, _port);
            State = CommunicationState.Connecting;

            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port, cancellationToken);
            _stream = _client.GetStream();

            State = CommunicationState.Connected;
            _logger.LogInformation("Connected to TCP at {Host}:{Port}", _host, _port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to TCP at {Host}:{Port}", _host, _port);
            State = CommunicationState.Error;
            _client?.Dispose();
            _client = null;
            _stream = null;
            return false;
        }
    }

    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Disconnecting from TCP at {Host}:{Port}", _host, _port);

            _stream?.Close();
            _client?.Close();

            _stream = null;
            _client = null;

            State = CommunicationState.Disconnected;
            _logger.LogInformation("Disconnected from TCP");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from TCP");
        }
        await Task.CompletedTask;
    }

    /// <summary>
    /// Send a request and wait for response.
    /// For TCP, this writes the request data and then reads the response.
    /// </summary>
    public override async Task<byte[]> RequestAsync(byte[] request, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
            throw new InvalidOperationException("Not connected");

        try
        {
            // Write request
            _logger.LogDebug("Sending TCP request with {ByteCount} bytes", request.Length);
            await _stream.WriteAsync(request, cancellationToken);
            await _stream.FlushAsync(cancellationToken);

            // Read response
            var buffer = new byte[256]; // Modbus typical response size
            var bytesRead = await _stream.ReadAsync(buffer, cancellationToken);

            if (bytesRead == 0)
            {
                _logger.LogWarning("Connection closed by remote host");
                State = CommunicationState.Disconnected;
                return Array.Empty<byte>();
            }

            var response = new byte[bytesRead];
            Array.Copy(buffer, response, bytesRead);

            _logger.LogDebug("Received TCP response with {ByteCount} bytes", bytesRead);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TCP request-response");
            State = CommunicationState.Error;
            throw;
        }
    }
}
