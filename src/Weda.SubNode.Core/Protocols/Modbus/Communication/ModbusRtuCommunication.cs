using Microsoft.Extensions.Logging;

using Weda.SubNode.Core.Communication.Common;
using Weda.SubNode.Core.Communication.Serial;

namespace Weda.SubNode.Core.Protocols.Modbus.Communication;


/// <summary>
/// Modbus RTU communication.
/// Handles CRC-16 framing for Modbus RTU protocol.
///
/// Request: PDU [SlaveId, FC, Data...] → +CRC → RTU frame
/// Response: RTU frame → -CRC (verified) → PDU [SlaveId, FC, Data...]
/// </summary>
public class ModbusRtuCommunication : RequestResponseCommunicationBase<byte[], byte[]>
{
    private readonly SerialCommunication _inner;

    public ModbusRtuCommunication(SerialCommunication inner, ILogger? logger = null)
        : base(inner.Settings, logger as ILogger<CommunicationBase>)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    protected override async Task<bool> ConnectCoreAsync(CancellationToken cancellationToken = default)
    {
        var connected = await _inner.ConnectAsync(cancellationToken);
        // Sync state from inner communication
        State = _inner.State;
        return connected;
    }

    public override async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _inner.DisconnectAsync(cancellationToken);
        // Sync state from inner communication
        State = _inner.State;
    }

    protected override async Task<byte[]> RequestAsyncCore(byte[] pdu, CancellationToken cancellationToken = default)
    {
        if (pdu == null || pdu.Length < 2)
            throw new ArgumentException("PDU must contain at least SlaveId and FunctionCode", nameof(pdu));

        // Build RTU frame: PDU + CRC (2 bytes)
        var rtuFrame = ModbusCrc16.AppendCrc(pdu);

        _logger.LogDebug("Sending Modbus RTU frame: {FrameLength} bytes (PDU + CRC)", rtuFrame.Length);

        // Send via Serial
        var response = await _inner.RequestAsync(rtuFrame, cancellationToken);

        if (response.Length < 5) // SlaveId + FC + 1 byte data + 2 CRC
        {
            _logger.LogWarning("Invalid Modbus RTU response: {Length} bytes (minimum 5 required)", response.Length);
            throw new InvalidOperationException($"Invalid Modbus RTU response length: {response.Length}");
        }

        // Verify CRC
        if (!ModbusCrc16.Verify(response))
        {
            _logger.LogWarning("Modbus RTU CRC verification failed");
            throw new InvalidOperationException("Modbus RTU CRC verification failed");
        }

        // Strip CRC, return PDU [SlaveId, FC, Data...]
        var responsePdu = response[..^2];

        _logger.LogDebug("Received Modbus RTU response: {PduLength} bytes PDU", responsePdu.Length);

        return responsePdu;
    }
}