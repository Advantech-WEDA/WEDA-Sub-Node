using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Modbus protocol parser for converting register data to C# types.
/// Implements IProtocolParser&lt;ushort[], object&gt; generic interface.
/// This is a stateless data converter - ModbusDevice manages communication directly.
/// Only handles protocol encoding/decoding (data type conversion), NOT calibration or communication.
/// </summary>
public class ModbusProtocolParser : IProtocolParser<ushort[], object>
{
    private readonly ModbusDataType _dataType;

    /// <summary>
    /// Data type name for metadata
    /// </summary>
    public string DataType => _dataType.ToString();

    /// <summary>
    /// Communication property not applicable for Modbus parser.
    /// ModbusDevice manages communication directly due to batch read optimization needs.
    /// </summary>
    public ICommunication Communication =>
        throw new NotSupportedException("ModbusDevice manages communication directly for batch read optimization");

    public ModbusProtocolParser(ModbusDataType dataType)
    {
        _dataType = dataType;
    }

    // ===== Low-Level Protocol Operations (IProtocolParser<ushort[], object>) =====

    /// <summary>
    /// Parse Modbus registers to C# value.
    /// Low-level protocol operation from IProtocolParser&lt;ushort[], object&gt;.
    /// </summary>
    public object Parse(ushort[] registers)
    {
        return _dataType switch
        {
            ModbusDataType.UInt16 => ParseUInt16(registers),
            ModbusDataType.Int16 => ParseInt16(registers),
            ModbusDataType.UInt32 => ParseUInt32(registers),
            ModbusDataType.Int32 => ParseInt32(registers),
            ModbusDataType.Float32 => ParseFloat32(registers),
            ModbusDataType.UInt64 => ParseUInt64(registers),
            ModbusDataType.Int64 => ParseInt64(registers),
            ModbusDataType.Float64 => ParseFloat64(registers),
            ModbusDataType.String16 => ParseString16(registers),
            _ => registers[0]
        };
    }

    /// <summary>
    /// Encode C# value to Modbus registers.
    /// Low-level protocol operation from IProtocolParser&lt;ushort[], object&gt;.
    /// </summary>
    public ushort[] Encode(object value)
    {
        return _dataType switch
        {
            ModbusDataType.UInt16 => EncodeUInt16(Convert.ToUInt16(value)),
            ModbusDataType.Int16 => EncodeInt16(Convert.ToInt16(value)),
            ModbusDataType.UInt32 => EncodeUInt32(Convert.ToUInt32(value)),
            ModbusDataType.Int32 => EncodeInt32(Convert.ToInt32(value)),
            ModbusDataType.Float32 => EncodeFloat32(Convert.ToSingle(value)),
            ModbusDataType.UInt64 => EncodeUInt64(Convert.ToUInt64(value)),
            ModbusDataType.Int64 => EncodeInt64(Convert.ToInt64(value)),
            ModbusDataType.Float64 => EncodeFloat64(Convert.ToDouble(value)),
            ModbusDataType.String16 => EncodeString16(Convert.ToString(value) ?? string.Empty),
            _ => [Convert.ToUInt16(value)]
        };
    }

    // ===== High-Level Telemetry Operations (IProtocolParser<ushort[], object>) =====

    /// <summary>
    /// Parse Modbus frame payload to telemetry measures.
    /// Note: This implementation requires SensorMapping to be provided.
    /// </summary>
    public List<TelemetryMeasure> ParseSensorData(byte[] payload, SensorMapping? sensorMapping = null)
    {
        // TODO: Implement full Modbus frame parsing (MBAP header + PDU)
        // For now, this is a placeholder that throws NotImplementedException
        throw new NotImplementedException("ParseSensorData from byte[] not yet implemented for Modbus");
    }

    /// <summary>
    /// Encode telemetry measures to Modbus frame payload.
    /// </summary>
    public byte[] EncodeSensorData(IEnumerable<TelemetryMeasure> measures)
    {
        // TODO: Implement Modbus frame encoding
        throw new NotImplementedException("EncodeSensorData not yet implemented for Modbus");
    }

    /// <summary>
    /// Encode device command to Modbus frame payload.
    /// </summary>
    public byte[] EncodeCommand(DeviceCommand command)
    {
        // TODO: Implement Modbus command encoding (Function Code 05/06/15/16)
        throw new NotImplementedException("EncodeCommand not yet implemented for Modbus");
    }

    // Parse methods
    private static ushort ParseUInt16(ushort[] registers) => registers[0];

    private static short ParseInt16(ushort[] registers) => (short)registers[0];

    private static uint ParseUInt32(ushort[] registers)
    {
        return (uint)((registers[0] << 16) | registers[1]);
    }

    private static int ParseInt32(ushort[] registers)
    {
        return (int)ParseUInt32(registers);
    }

    private static float ParseFloat32(ushort[] registers)
    {
        // Big-endian byte order (Modbus standard)
        var bytes = new byte[4];
        bytes[0] = (byte)(registers[0] >> 8);
        bytes[1] = (byte)(registers[0] & 0xFF);
        bytes[2] = (byte)(registers[1] >> 8);
        bytes[3] = (byte)(registers[1] & 0xFF);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static ulong ParseUInt64(ushort[] registers)
    {
        return ((ulong)registers[0] << 48) |
               ((ulong)registers[1] << 32) |
               ((ulong)registers[2] << 16) |
               registers[3];
    }

    private static long ParseInt64(ushort[] registers)
    {
        return (long)ParseUInt64(registers);
    }

    private static double ParseFloat64(ushort[] registers)
    {
        var bytes = new byte[8];
        for (int i = 0; i < 4; i++)
        {
            bytes[i * 2] = (byte)(registers[i] >> 8);
            bytes[i * 2 + 1] = (byte)(registers[i] & 0xFF);
        }
        return BitConverter.ToDouble(bytes, 0);
    }

    private static string ParseString16(ushort[] registers)
    {
        // String16 = 16 ASCII characters = 16 registers (each register = 2 bytes but only use 1 byte for ASCII)
        // Modbus typically stores one character per register in the low byte
        var chars = new char[Math.Min(registers.Length, 16)];
        for (int i = 0; i < chars.Length; i++)
        {
            chars[i] = (char)(registers[i] & 0xFF); // Take low byte as ASCII character
        }
        return new string(chars).TrimEnd('\0'); // Remove null terminators
    }

    // Encode methods
    private static ushort[] EncodeUInt16(ushort value) => [value];

    private static ushort[] EncodeInt16(short value) => [(ushort)value];

    private static ushort[] EncodeUInt32(uint value)
    {
        return [
            (ushort)(value >> 16),
            (ushort)(value & 0xFFFF)
        ];
    }

    private static ushort[] EncodeInt32(int value) => EncodeUInt32((uint)value);

    private static ushort[] EncodeFloat32(float value)
    {
        var bytes = BitConverter.GetBytes(value);
        return [
            (ushort)((bytes[0] << 8) | bytes[1]),
            (ushort)((bytes[2] << 8) | bytes[3])
        ];
    }

    private static ushort[] EncodeUInt64(ulong value)
    {
        return [
            (ushort)(value >> 48),
            (ushort)((value >> 32) & 0xFFFF),
            (ushort)((value >> 16) & 0xFFFF),
            (ushort)(value & 0xFFFF)
        ];
    }

    private static ushort[] EncodeInt64(long value) => EncodeUInt64((ulong)value);

    private static ushort[] EncodeFloat64(double value)
    {
        var bytes = BitConverter.GetBytes(value);
        var registers = new ushort[4];
        for (int i = 0; i < 4; i++)
        {
            registers[i] = (ushort)((bytes[i * 2] << 8) | bytes[i * 2 + 1]);
        }
        return registers;
    }

    private static ushort[] EncodeString16(string value)
    {
        // Encode up to 16 ASCII characters into 16 registers
        var registers = new ushort[16];
        var chars = value.PadRight(16, '\0').ToCharArray();

        for (int i = 0; i < 16; i++)
        {
            registers[i] = (ushort)chars[i]; // Store character in low byte of register
        }

        return registers;
    }
}
