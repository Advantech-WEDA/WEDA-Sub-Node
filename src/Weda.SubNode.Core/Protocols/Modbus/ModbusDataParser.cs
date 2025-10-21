namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Modbus data parser - converts raw register bytes to physical values
/// </summary>
public static class ModbusDataParser
{
    /// <summary>
    /// Parse raw Modbus register data to physical value
    /// </summary>
    public static object ParseRegisters(ushort[] registers, ModbusDataType dataType, double scale = 1.0, double offset = 0.0)
    {
        if (registers == null || registers.Length == 0)
            throw new ArgumentException("Registers cannot be null or empty", nameof(registers));

        object rawValue = dataType switch
        {
            ModbusDataType.UInt16 => ParseUInt16(registers),
            ModbusDataType.Int16 => ParseInt16(registers),
            ModbusDataType.UInt32 => ParseUInt32(registers),
            ModbusDataType.Int32 => ParseInt32(registers),
            ModbusDataType.Float32 => ParseFloat32(registers),
            ModbusDataType.UInt64 => ParseUInt64(registers),
            ModbusDataType.Int64 => ParseInt64(registers),
            ModbusDataType.Float64 => ParseFloat64(registers),
            _ => throw new NotSupportedException($"Data type {dataType} is not supported")
        };

        // Apply scale and offset: physical_value = raw_value * scale + offset
        var numericValue = Convert.ToDouble(rawValue);
        return numericValue * scale + offset;
    }

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

    private static uint ParseUInt32(ushort[] registers)
    {
        if (registers.Length < 2)
            throw new ArgumentException("Need at least 2 registers for UInt32");
        // Big-endian: [high, low]
        return ((uint)registers[0] << 16) | registers[1];
    }

    private static int ParseInt32(ushort[] registers)
    {
        if (registers.Length < 2)
            throw new ArgumentException("Need at least 2 registers for Int32");
        return (int)(((uint)registers[0] << 16) | registers[1]);
    }

    private static float ParseFloat32(ushort[] registers)
    {
        if (registers.Length < 2)
            throw new ArgumentException("Need at least 2 registers for Float32");

        var bytes = new byte[4];
        bytes[0] = (byte)(registers[0] >> 8);
        bytes[1] = (byte)(registers[0] & 0xFF);
        bytes[2] = (byte)(registers[1] >> 8);
        bytes[3] = (byte)(registers[1] & 0xFF);

        return BitConverter.ToSingle(bytes, 0);
    }

    private static ulong ParseUInt64(ushort[] registers)
    {
        if (registers.Length < 4)
            throw new ArgumentException("Need at least 4 registers for UInt64");

        return ((ulong)registers[0] << 48) |
               ((ulong)registers[1] << 32) |
               ((ulong)registers[2] << 16) |
               registers[3];
    }

    private static long ParseInt64(ushort[] registers)
    {
        if (registers.Length < 4)
            throw new ArgumentException("Need at least 4 registers for Int64");
        return (long)ParseUInt64(registers);
    }

    private static double ParseFloat64(ushort[] registers)
    {
        if (registers.Length < 4)
            throw new ArgumentException("Need at least 4 registers for Float64");

        var bytes = new byte[8];
        for (int i = 0; i < 4; i++)
        {
            bytes[i * 2] = (byte)(registers[i] >> 8);
            bytes[i * 2 + 1] = (byte)(registers[i] & 0xFF);
        }

        return BitConverter.ToDouble(bytes, 0);
    }
}
