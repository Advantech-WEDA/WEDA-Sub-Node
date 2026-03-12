using Microsoft.Extensions.Logging;

using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Tcp;

namespace Weda.SubNode.Core.Protocols.Modbus.Communication;

/// <summary>
/// Modbus TCP communication.
/// Handles MBAP header framing for Modbus TCP protocol.
///
/// Request: PDU [SlaveId, FC, Data...] → +MBAP → TCP frame
/// Response: TCP frame → -MBAP → PDU [SlaveId, FC, Data...]
/// </summary>
public class ModbusTcpCommunication : RequestResponseCommunicationBase<byte[], byte[]>
{
    private readonly TcpCommunication _inner;
    private ushort _transactionId;

    public ModbusTcpCommunication(TcpCommunication inner, ILogger? logger = null)
        : base(inner.Settings, logger as ILogger<CommunicationBase>)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    protected override Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
        => _inner.ConnectAsync(cancellationToken);

    public override Task DisconnectAsync(CancellationToken cancellationToken = default)
        => _inner.DisconnectAsync(cancellationToken);

    /// <summary>
    /// Send Modbus PDU and receive response PDU.
    /// Automatically handles MBAP header wrapping/unwrapping.
    /// </summary>
    /// <param name="pdu">PDU bytes: [SlaveId, FC, Data...]</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response PDU: [SlaveId, FC, Data...]</returns>
    protected override async Task<byte[]> RequestAsyncCore(byte[] pdu, CancellationToken cancellationToken = default)
    {
        if (pdu == null || pdu.Length < 2)
            throw new ArgumentException("PDU must contain at least SlaveId and FunctionCode", nameof(pdu));

        // Build TCP frame: MBAP header (6 bytes) + PDU
        var tcpFrame = BuildTcpFrame(pdu);

        _logger.LogDebug("Sending Modbus TCP frame: {FrameLength} bytes (MBAP + PDU)", tcpFrame.Length);

        // Send via TCP
        var response = await _inner.RequestAsync(tcpFrame, cancellationToken);

        if (response.Length < 7)
        {
            _logger.LogWarning("Invalid Modbus TCP response: {Length} bytes (minimum 7 required)", response.Length);
            throw new InvalidOperationException($"Invalid Modbus TCP response length: {response.Length}");
        }

        // Strip MBAP header, return PDU [SlaveId, FC, Data...]
        var responsePdu = response[6..]; // Skip 6-byte MBAP header

        _logger.LogDebug("Received Modbus TCP response: {PduLength} bytes PDU", responsePdu.Length);

        return responsePdu;
    }

    /// <summary>
    /// Build Modbus TCP frame with MBAP header.
    /// MBAP Header (6 bytes):
    ///   - Transaction ID (2 bytes): Incremented for each request
    ///   - Protocol ID (2 bytes): Always 0x0000 for Modbus
    ///   - Length (2 bytes): Number of following bytes (PDU length)
    /// </summary>
    private byte[] BuildTcpFrame(byte[] pdu)
    {
        var transactionId = ++_transactionId;
        var length = (ushort)pdu.Length;

        var frame = new byte[6 + pdu.Length];

        // MBAP Header
        frame[0] = (byte)(transactionId >> 8);
        frame[1] = (byte)(transactionId & 0xFF);
        frame[2] = 0x00; // Protocol ID high
        frame[3] = 0x00; // Protocol ID low
        frame[4] = (byte)(length >> 8);
        frame[5] = (byte)(length & 0xFF);

        // PDU (includes SlaveId as first byte)
        Array.Copy(pdu, 0, frame, 6, pdu.Length);

        return frame;
    }
}
