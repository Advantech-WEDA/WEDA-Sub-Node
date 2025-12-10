namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Modbus data parser - converts raw register bytes to physical values.
/// Supports multiple byte order conventions used by different Modbus devices.
/// </summary>
public static class ModbusDataParser
{
    /// <summary>
    /// Parse raw Modbus register data to physical value with default BigEndian byte order.
    /// </summary>
    public static object ParseRegisters(
        ushort[] registers,
        ModbusDataType dataType,
        double scale = 1.0,
        double offset = 0.0)
    {
        return ParseRegisters(registers, dataType, ModbusByteOrder.BigEndian, scale, offset);
    }

    /// <summary>
    /// Parse raw Modbus register data to physical value with specified byte order.
    /// </summary>
    public static object ParseRegisters(
        ushort[] registers,
        ModbusDataType dataType,
        ModbusByteOrder byteOrder,
        double scale = 1.0,
        double offset = 0.0)
    {
        if (registers == null || registers.Length == 0)
            throw new ArgumentException("Registers cannot be null or empty", nameof(registers));

        object rawValue = dataType switch
        {
            ModbusDataType.UInt16 => ParseUInt16(registers),
            ModbusDataType.Int16 => ParseInt16(registers),
            ModbusDataType.UInt32 => ParseUInt32(registers, byteOrder),
            ModbusDataType.Int32 => ParseInt32(registers, byteOrder),
            ModbusDataType.Float32 => ParseFloat32(registers, byteOrder),
            ModbusDataType.UInt64 => ParseUInt64(registers, byteOrder),
            ModbusDataType.Int64 => ParseInt64(registers, byteOrder),
            ModbusDataType.Float64 => ParseFloat64(registers, byteOrder),
            _ => throw new NotSupportedException($"Data type {dataType} is not supported")
        };

        // Apply scale and offset: physical_value = raw_value * scale + offset
        var numericValue = Convert.ToDouble(rawValue);
        return numericValue * scale + offset;
    }

    #region 16-bit types (byte order doesn't apply to single register)

    private static ushort ParseUInt16(ushort[] registers)
    {
        if (registers.Length < 1)
            throw new ArgumentException("Need at least 1 register for UInt16");
        return registers[0];
    }

    private static short ParseInt16(ushort[] registers)
    {
        if (registers.Length < 1)
            throw new ArgumentException("Need at least 1 register for Int16");
        return (short)registers[0];
    }

    #endregion

    #region 32-bit types

    private static uint ParseUInt32(ushort[] registers, ModbusByteOrder byteOrder)
    {
        if (registers.Length < 2)
            throw new ArgumentException("Need at least 2 registers for UInt32");

        var bytes = GetBytes32(registers, byteOrder);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static int ParseInt32(ushort[] registers, ModbusByteOrder byteOrder)
    {
        if (registers.Length < 2)
            throw new ArgumentException("Need at least 2 registers for Int32");

        var bytes = GetBytes32(registers, byteOrder);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static float ParseFloat32(ushort[] registers, ModbusByteOrder byteOrder)
    {
        if (registers.Length < 2)
            throw new ArgumentException("Need at least 2 registers for Float32");

        var bytes = GetBytes32(registers, byteOrder);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <summary>
    /// Convert 2 Modbus registers to 4 bytes in little-endian order for BitConverter.
    /// Handles different Modbus byte order conventions.
    /// </summary>
    /// <remarks>
    /// For a 32-bit value with IEEE 754 bytes A(MSB), B, C, D(LSB):
    /// - BigEndian (ABCD): reg[0]=AB, reg[1]=CD → bytes=[D,C,B,A]
    /// - LittleEndian (DCBA): reg[0]=DC, reg[1]=BA → bytes=[D,C,B,A]
    /// - BigEndianByteSwap (BADC): reg[0]=BA, reg[1]=DC → bytes=[D,C,B,A]
    /// - LittleEndianByteSwap (CDAB): reg[0]=CD, reg[1]=AB → bytes=[D,C,B,A]
    /// </remarks>
    private static byte[] GetBytes32(ushort[] registers, ModbusByteOrder byteOrder)
    {
        var bytes = new byte[4];

        switch (byteOrder)
        {
            case ModbusByteOrder.BigEndian:
                // ABCD: reg[0]=AB (high word), reg[1]=CD (low word)
                // BitConverter needs [LSB...MSB] = [D,C,B,A]
                bytes[3] = (byte)(registers[0] >> 8);    // A (MSB)
                bytes[2] = (byte)(registers[0] & 0xFF);  // B
                bytes[1] = (byte)(registers[1] >> 8);    // C
                bytes[0] = (byte)(registers[1] & 0xFF);  // D (LSB)
                break;

            case ModbusByteOrder.LittleEndian:
                // DCBA: reg[0]=DC (low word, swapped), reg[1]=BA (high word, swapped)
                // BitConverter needs [LSB...MSB] = [D,C,B,A]
                bytes[0] = (byte)(registers[0] >> 8);    // D (LSB)
                bytes[1] = (byte)(registers[0] & 0xFF);  // C
                bytes[2] = (byte)(registers[1] >> 8);    // B
                bytes[3] = (byte)(registers[1] & 0xFF);  // A (MSB)
                break;

            case ModbusByteOrder.BigEndianByteSwap:
                // BADC: reg[0]=BA (high word, bytes swapped), reg[1]=DC (low word, bytes swapped)
                // BitConverter needs [LSB...MSB] = [D,C,B,A]
                bytes[2] = (byte)(registers[0] >> 8);    // B
                bytes[3] = (byte)(registers[0] & 0xFF);  // A (MSB)
                bytes[0] = (byte)(registers[1] >> 8);    // D (LSB)
                bytes[1] = (byte)(registers[1] & 0xFF);  // C
                break;

            case ModbusByteOrder.LittleEndianByteSwap:
                // CDAB: reg[0]=CD (low word), reg[1]=AB (high word)
                // BitConverter needs [LSB...MSB] = [D,C,B,A]
                bytes[1] = (byte)(registers[0] >> 8);    // C
                bytes[0] = (byte)(registers[0] & 0xFF);  // D (LSB)
                bytes[3] = (byte)(registers[1] >> 8);    // A (MSB)
                bytes[2] = (byte)(registers[1] & 0xFF);  // B
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "Unknown byte order");
        }

        return bytes;
    }

    #endregion

    #region 64-bit types

    private static ulong ParseUInt64(ushort[] registers, ModbusByteOrder byteOrder)
    {
        if (registers.Length < 4)
            throw new ArgumentException("Need at least 4 registers for UInt64");

        var bytes = GetBytes64(registers, byteOrder);
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static long ParseInt64(ushort[] registers, ModbusByteOrder byteOrder)
    {
        if (registers.Length < 4)
            throw new ArgumentException("Need at least 4 registers for Int64");

        var bytes = GetBytes64(registers, byteOrder);
        return BitConverter.ToInt64(bytes, 0);
    }

    private static double ParseFloat64(ushort[] registers, ModbusByteOrder byteOrder)
    {
        if (registers.Length < 4)
            throw new ArgumentException("Need at least 4 registers for Float64");

        var bytes = GetBytes64(registers, byteOrder);
        return BitConverter.ToDouble(bytes, 0);
    }

    /// <summary>
    /// Convert 4 Modbus registers to 8 bytes in little-endian order for BitConverter.
    /// Handles different Modbus byte order conventions.
    /// </summary>
    private static byte[] GetBytes64(ushort[] registers, ModbusByteOrder byteOrder)
    {
        var bytes = new byte[8];

        switch (byteOrder)
        {
            case ModbusByteOrder.BigEndian:
                // Standard Modbus Big Endian: reg[0]=most significant, reg[3]=least significant
                // Each register: high byte first
                // BitConverter needs [LSB...MSB]
                bytes[7] = (byte)(registers[0] >> 8);
                bytes[6] = (byte)(registers[0] & 0xFF);
                bytes[5] = (byte)(registers[1] >> 8);
                bytes[4] = (byte)(registers[1] & 0xFF);
                bytes[3] = (byte)(registers[2] >> 8);
                bytes[2] = (byte)(registers[2] & 0xFF);
                bytes[1] = (byte)(registers[3] >> 8);
                bytes[0] = (byte)(registers[3] & 0xFF);
                break;

            case ModbusByteOrder.LittleEndian:
                // Little Endian: reg[0]=least significant, reg[3]=most significant
                // Each register: low byte first
                bytes[0] = (byte)(registers[0] >> 8);
                bytes[1] = (byte)(registers[0] & 0xFF);
                bytes[2] = (byte)(registers[1] >> 8);
                bytes[3] = (byte)(registers[1] & 0xFF);
                bytes[4] = (byte)(registers[2] >> 8);
                bytes[5] = (byte)(registers[2] & 0xFF);
                bytes[6] = (byte)(registers[3] >> 8);
                bytes[7] = (byte)(registers[3] & 0xFF);
                break;

            case ModbusByteOrder.BigEndianByteSwap:
                // Big Endian Word + Little Endian Byte (bytes swapped within words)
                // reg[0]=most significant word (bytes swapped), reg[3]=least significant word
                bytes[6] = (byte)(registers[0] >> 8);
                bytes[7] = (byte)(registers[0] & 0xFF);
                bytes[4] = (byte)(registers[1] >> 8);
                bytes[5] = (byte)(registers[1] & 0xFF);
                bytes[2] = (byte)(registers[2] >> 8);
                bytes[3] = (byte)(registers[2] & 0xFF);
                bytes[0] = (byte)(registers[3] >> 8);
                bytes[1] = (byte)(registers[3] & 0xFF);
                break;

            case ModbusByteOrder.LittleEndianByteSwap:
                // Little Endian Word + Big Endian Byte (word swapped)
                // reg[0]=least significant word (MSB first), reg[3]=most significant word
                bytes[1] = (byte)(registers[0] >> 8);
                bytes[0] = (byte)(registers[0] & 0xFF);
                bytes[3] = (byte)(registers[1] >> 8);
                bytes[2] = (byte)(registers[1] & 0xFF);
                bytes[5] = (byte)(registers[2] >> 8);
                bytes[4] = (byte)(registers[2] & 0xFF);
                bytes[7] = (byte)(registers[3] >> 8);
                bytes[6] = (byte)(registers[3] & 0xFF);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "Unknown byte order");
        }

        return bytes;
    }

    #endregion
}
