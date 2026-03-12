namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Mosbus CRC-16 calculation for RTU frames.
/// Uses polynomial 0xA001 (bit-reversed 0x8005)
/// </summary>
public static class ModbusCrc16
{
    private const ushort Polynomial = 0xA001;

    /// <summary>
    /// Calculate CRC-16 for the given data
    /// </summary>
    /// <param name="data">Data buffer</param>
    /// <param name="offset">Start offset</param>
    /// <param name="length">Number of bytes to calculate</param>
    /// <returns>CRC-16 value (little-endian format for Modbus RTU)</returns>
    public static ushort Calculate(byte[] data, int offset, int length)
    {
        ushort crc = 0xFFFF;
        for (int i = offset; i < offset + length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 0x0001) != 0
                    ? (ushort)((crc >> 1) ^ Polynomial)
                    : (ushort)(crc >> 1);

            }
        }   
        return crc;
    }

    /// <summary>
    /// Calculate CRC-16 for the entire data array.
    /// </summary>
    public static ushort Calculate(byte[] data) => Calculate(data, 0, data.Length);

    /// <summary>
    /// Verify CRC-16 of a complete RTU frame.
    /// The frame should include the 2-byte CRC at the end. 
    /// </summary>
    /// <param name="frame">Complete RTU frame including CRC</param>
    /// <returns></returns>
    public static bool Verify(byte[] frame)
    {
        if (frame.Length < 3) return false;

        var calculated = Calculate(frame, 0, frame.Length - 2);
        var received = (ushort)(frame[^2] | (frame[^1] << 8));

        return calculated == received;
    }

    public static byte[] AppendCrc(byte[] data)
    {
        var crc = Calculate(data);
        var frame = new byte[data.Length + 2];
        Array.Copy(data, frame, data.Length);
        frame[^2] = (byte)(crc & 0xFF);  // CRC low byte
        frame[^1] = (byte)(crc >> 8);    // CRC high byte        
        return frame;
    }
}