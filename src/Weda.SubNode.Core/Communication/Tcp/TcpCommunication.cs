using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Communication.Common;

namespace Weda.SubNode.Core.Communication.Tcp;

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
    /// Create TcpCommunication from DeviceConfiguration.DeviceCommunication dictionary
    /// </summary>
    public static TcpCommunication CreateTcpCommunication(
        this DeviceConfiguration config,
        ConnectionSettings? connectionSettings = null,
        ILogger<CommunicationBase>? logger = null)
    {
        var comm = config.DeviceCommunication;
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
    /// Implements proper Modbus TCP response reading by first reading the MBAP header
    /// to determine the total message length.
    /// </summary>
    protected override async Task<byte[]> RequestAsyncCore(byte[] request, CancellationToken cancellationToken = default)
    {
        if (!IsConnected || _stream == null)
            throw new InvalidOperationException("Not connected");

        try
        {
            // Write request
            _logger.LogDebug("Sending TCP request with {ByteCount} bytes", request.Length);
            await _stream.WriteAsync(request, cancellationToken);
            await _stream.FlushAsync(cancellationToken);

            // Read MBAP header first (6 bytes: Transaction ID 2 + Protocol ID 2 + Length 2)
            var header = new byte[6];
            var headerBytesRead = await ReadExactAsync(_stream, header, 0, 6, cancellationToken);

            if (headerBytesRead == 0)
            {
                _logger.LogWarning("Connection closed by remote host");
                State = CommunicationState.Disconnected;
                return [];
            }

            if (headerBytesRead < 6)
            {
                _logger.LogWarning("Incomplete MBAP header: {BytesRead} bytes, expected 6", headerBytesRead);
                return header[..headerBytesRead];
            }

            // Extract the Length field from MBAP header (bytes 4-5, big-endian)
            // Length = Unit ID (1) + PDU (function code + data)
            var pduLength = (header[4] << 8) | header[5];

            // Read the remaining bytes (Unit ID + PDU)
            var pdu = new byte[pduLength];
            var pduBytesRead = await ReadExactAsync(_stream, pdu, 0, pduLength, cancellationToken);

            if (pduBytesRead < pduLength)
            {
                _logger.LogWarning("Incomplete PDU: {BytesRead} bytes, expected {Expected}", pduBytesRead, pduLength);
            }

            // Combine header and PDU into complete response
            var totalLength = 6 + pduBytesRead;
            var response = new byte[totalLength];
            Array.Copy(header, 0, response, 0, 6);
            Array.Copy(pdu, 0, response, 6, pduBytesRead);

            _logger.LogDebug("Received TCP response with {ByteCount} bytes (header: 6, pdu: {PduLength})",
                totalLength, pduBytesRead);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during TCP request-response");
            State = CommunicationState.Error;
            throw;
        }
    }

    /// <summary>
    /// Reads exactly the specified number of bytes from the stream, handling partial reads.
    /// </summary>
    private static async Task<int> ReadExactAsync(
        NetworkStream stream,
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        var totalBytesRead = 0;
        while (totalBytesRead < count)
        {
            var bytesRead = await stream.ReadAsync(
                buffer.AsMemory(offset + totalBytesRead, count - totalBytesRead),
                cancellationToken);

            if (bytesRead == 0)
            {
                // Connection closed
                break;
            }

            totalBytesRead += bytesRead;
        }
        return totalBytesRead;
    }
}
