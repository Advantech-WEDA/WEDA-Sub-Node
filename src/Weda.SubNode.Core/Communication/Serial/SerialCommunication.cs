using System.IO.Ports;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Core.Communication.Common;

namespace Weda.SubNode.Core.Communication.Serial;

public class SerialCommunication(
    SerialCommunicationSettings serialSettings,
    ConnectionSettings? settings = null,
    ILogger<CommunicationBase>? logger = null) 
    : RequestResponseCommunicationBase<byte[], byte[]>(settings, logger)
{
    private readonly SerialCommunicationSettings _serialSettings = serialSettings ?? throw new ArgumentNullException(nameof(serialSettings));
    private SerialPort? _serialPort;

    protected override Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Opening serial port {PortName} at {BaudRate} baud",
                _serialSettings.PortName, _serialSettings.BaudRate);

            State = CommunicationState.Connecting;
            
            _serialPort = new SerialPort(
                _serialSettings.PortName,
                _serialSettings.BaudRate,
                _serialSettings.Parity,
                _serialSettings.DataBits,
                _serialSettings.StopBits)
            {
                ReadTimeout = _serialSettings.ReadTimeoutMs,
                WriteTimeout = _serialSettings.WriteTimeoutMs
            };

            _serialPort.Open();

            State = CommunicationState.Connected;

            _logger.LogInformation("Serial port {PortName} opened successfully", _serialSettings.PortName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open serial port {PortName}", _serialSettings.PortName);
            State = CommunicationState.Error;
            _serialPort?.Dispose();
            _serialPort = null;
            return Task.FromResult(false);
        }
    }

    public override Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_serialPort != null)
        {
            _logger.LogInformation("Closing serial port {PortName}", _serialSettings.PortName);
            _serialPort.Close();
            _serialPort.Dispose();
            _serialPort = null;
        }

        State = CommunicationState.Disconnected;
        return Task.CompletedTask;
    }

    protected override async Task<byte[]> RequestAsyncCore(byte[] request, CancellationToken cancellationToken = default)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
            throw new InvalidOperationException("Serial port is not open");

        _serialPort.DiscardInBuffer();
        _serialPort.DiscardOutBuffer();

        // Write request
        await _serialPort.BaseStream.WriteAsync(request, cancellationToken);
        await _serialPort.BaseStream.FlushAsync(cancellationToken);

        _logger.LogDebug("Sent {ByteCount} bytes to serial port", request.Length);

        // Read response - use timeout from settings
        var buffer = new byte[256];
        var totalRead = 0;

        // Wait for first byte with timeout
        using var timeoutCts = new CancellationTokenSource(_serialSettings.ReadTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Read available bytes
            while (totalRead < buffer.Length)
            {
                // Check if data available, if not wait a bit
                if (_serialPort.BytesToRead == 0)
                {
                    if (totalRead > 0)
                    {
                        // Already have some data, wait briefly for more
                        await Task.Delay(20, linkedCts.Token);
                        if (_serialPort.BytesToRead == 0)
                            break; // No more data coming
                    }
                    else
                    {
                        // No data yet, wait for first byte
                        await Task.Delay(10, linkedCts.Token);
                        continue;
                    }
                }

                var bytesRead = await _serialPort.BaseStream.ReadAsync(
                    buffer.AsMemory(totalRead, Math.Min(_serialPort.BytesToRead, buffer.Length - totalRead)),
                    linkedCts.Token);
                
                if (bytesRead == 0) break;
                totalRead += bytesRead;
            }   
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            if (totalRead == 0)
                throw new TimeoutException($"No response received within {_serialSettings.ReadTimeoutMs}ms");
        }

        _logger.LogDebug("Received {ByteCount} bytes from serial port", totalRead);
        return buffer[..totalRead];
    }
}