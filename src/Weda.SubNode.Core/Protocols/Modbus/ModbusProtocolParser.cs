using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Modbus protocol parser for converting register data to C# types.
/// Implements IProtocolParser&lt;ushort[], object&gt; generic interface.
/// This is a stateless data converter - ModbusDevice manages communication directly.
/// Only handles protocol encoding/decoding (data type conversion), NOT calibration or communication.
///
/// Supports configurable byte order for different Modbus device implementations:
/// - BigEndian (ABCD): Standard Modbus - High word first, MSB first in each word [Default]
/// - LittleEndian (DCBA): Low word first, LSB first in each word
/// - BigEndianByteSwap (BADC): High word first, bytes swapped within words
/// - LittleEndianByteSwap (CDAB): Low word first, bytes swapped within words
/// </summary>
public class ModbusProtocolParser : IProtocolParser<ushort[], object>
{
    private readonly ModbusDataType _dataType;
    private readonly ModbusByteOrder _byteOrder;

    /// <summary>
    /// Data type name for metadata
    /// </summary>
    public string DataType => _dataType.ToString();

    /// <summary>
    /// Byte order mode for multi-register data types
    /// </summary>
    public ModbusByteOrder ByteOrder => _byteOrder;

    /// <summary>
    /// Communication property not applicable for Modbus parser.
    /// ModbusDevice manages communication directly due to batch read optimization needs.
    /// </summary>
    public ICommunication Communication =>
        throw new NotSupportedException("ModbusDevice manages communication directly for batch read optimization");

    /// <summary>
    /// Creates a Modbus protocol parser with the specified data type and default Big Endian byte order.
    /// </summary>
    /// <param name="dataType">The Modbus data type to parse/encode</param>
    public ModbusProtocolParser(ModbusDataType dataType)
        : this(dataType, ModbusByteOrder.BigEndian)
    {
    }

    /// <summary>
    /// Creates a Modbus protocol parser with the specified data type and byte order.
    /// </summary>
    /// <param name="dataType">The Modbus data type to parse/encode</param>
    /// <param name="byteOrder">The byte order mode for multi-register types</param>
    public ModbusProtocolParser(ModbusDataType dataType, ModbusByteOrder byteOrder)
    {
        _dataType = dataType;
        _byteOrder = byteOrder;
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
            ModbusDataType.UInt32 => ParseUInt32(registers, _byteOrder),
            ModbusDataType.Int32 => ParseInt32(registers, _byteOrder),
            ModbusDataType.Float32 => ParseFloat32(registers, _byteOrder),
            ModbusDataType.UInt64 => ParseUInt64(registers, _byteOrder),
            ModbusDataType.Int64 => ParseInt64(registers, _byteOrder),
            ModbusDataType.Float64 => ParseFloat64(registers, _byteOrder),
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
            ModbusDataType.UInt32 => EncodeUInt32(Convert.ToUInt32(value), _byteOrder),
            ModbusDataType.Int32 => EncodeInt32(Convert.ToInt32(value), _byteOrder),
            ModbusDataType.Float32 => EncodeFloat32(Convert.ToSingle(value), _byteOrder),
            ModbusDataType.UInt64 => EncodeUInt64(Convert.ToUInt64(value), _byteOrder),
            ModbusDataType.Int64 => EncodeInt64(Convert.ToInt64(value), _byteOrder),
            ModbusDataType.Float64 => EncodeFloat64(Convert.ToDouble(value), _byteOrder),
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
        throw new NotImplementedException("ParseSensorData from byte[] not yet implemented for Modbus");
    }

    /// <summary>
    /// Encode telemetry measures to Modbus frame payload.
    /// </summary>
    public byte[] EncodeSensorData(IEnumerable<TelemetryMeasure> measures)
    {
        throw new NotImplementedException("EncodeSensorData not yet implemented for Modbus");
    }

    /// <summary>
    /// Encode device command to Modbus frame payload.
    /// </summary>
    public byte[] EncodeCommand(DeviceCommand command)
    {
        throw new NotImplementedException("EncodeCommand not yet implemented for Modbus");
    }

    #region Parse Methods

    private static ushort ParseUInt16(ushort[] registers) => registers[0];

    private static short ParseInt16(ushort[] registers) => (short)registers[0];

    private static uint ParseUInt32(ushort[] registers, ModbusByteOrder byteOrder)
    {
        // Extract bytes from registers based on byte order
        byte[] bytes = ExtractBytes32(registers, byteOrder);
        return BitConverter.ToUInt32(bytes, 0);
    }

    private static int ParseInt32(ushort[] registers, ModbusByteOrder byteOrder)
    {
        return (int)ParseUInt32(registers, byteOrder);
    }

    private static float ParseFloat32(ushort[] registers, ModbusByteOrder byteOrder)
    {
        byte[] bytes = ExtractBytes32(registers, byteOrder);
        return BitConverter.ToSingle(bytes, 0);
    }

    private static ulong ParseUInt64(ushort[] registers, ModbusByteOrder byteOrder)
    {
        byte[] bytes = ExtractBytes64(registers, byteOrder);
        return BitConverter.ToUInt64(bytes, 0);
    }

    private static long ParseInt64(ushort[] registers, ModbusByteOrder byteOrder)
    {
        return (long)ParseUInt64(registers, byteOrder);
    }

    private static double ParseFloat64(ushort[] registers, ModbusByteOrder byteOrder)
    {
        byte[] bytes = ExtractBytes64(registers, byteOrder);
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

    #endregion

    #region Encode Methods

    private static ushort[] EncodeUInt16(ushort value) => [value];

    private static ushort[] EncodeInt16(short value) => [(ushort)value];

    private static ushort[] EncodeUInt32(uint value, ModbusByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return PackBytes32(bytes, byteOrder);
    }

    private static ushort[] EncodeInt32(int value, ModbusByteOrder byteOrder)
    {
        return EncodeUInt32((uint)value, byteOrder);
    }

    private static ushort[] EncodeFloat32(float value, ModbusByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return PackBytes32(bytes, byteOrder);
    }

    private static ushort[] EncodeUInt64(ulong value, ModbusByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return PackBytes64(bytes, byteOrder);
    }

    private static ushort[] EncodeInt64(long value, ModbusByteOrder byteOrder)
    {
        return EncodeUInt64((ulong)value, byteOrder);
    }

    private static ushort[] EncodeFloat64(double value, ModbusByteOrder byteOrder)
    {
        var bytes = BitConverter.GetBytes(value);
        return PackBytes64(bytes, byteOrder);
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

    #endregion

    #region Byte Order Conversion Helpers

    /// <summary>
    /// Extract 4 bytes from 2 registers in the correct order for BitConverter (little-endian).
    /// Input registers contain bytes based on the device's byte order mode.
    /// Output is always [LSB, ..., MSB] for BitConverter on little-endian systems.
    /// </summary>
    private static byte[] ExtractBytes32(ushort[] registers, ModbusByteOrder byteOrder)
    {
        var bytes = new byte[4];

        switch (byteOrder)
        {
            case ModbusByteOrder.BigEndian:
                // ABCD: reg[0]=AB (high word, MSB first), reg[1]=CD (low word, MSB first)
                // BitConverter needs [D, C, B, A] (LSB first)
                bytes[3] = (byte)(registers[0] >> 8);    // A (MSB)
                bytes[2] = (byte)(registers[0] & 0xFF);  // B
                bytes[1] = (byte)(registers[1] >> 8);    // C
                bytes[0] = (byte)(registers[1] & 0xFF);  // D (LSB)
                break;

            case ModbusByteOrder.LittleEndian:
                // DCBA: reg[0]=DC (low word, LSB first), reg[1]=BA (high word, LSB first)
                // BitConverter needs [D, C, B, A] (LSB first)
                bytes[0] = (byte)(registers[0] >> 8);    // D (LSB)
                bytes[1] = (byte)(registers[0] & 0xFF);  // C
                bytes[2] = (byte)(registers[1] >> 8);    // B
                bytes[3] = (byte)(registers[1] & 0xFF);  // A (MSB)
                break;

            case ModbusByteOrder.BigEndianByteSwap:
                // BADC: reg[0]=BA (high word, LSB first), reg[1]=DC (low word, LSB first)
                // BitConverter needs [D, C, B, A] (LSB first)
                bytes[3] = (byte)(registers[0] & 0xFF);  // A (MSB)
                bytes[2] = (byte)(registers[0] >> 8);    // B
                bytes[1] = (byte)(registers[1] & 0xFF);  // C
                bytes[0] = (byte)(registers[1] >> 8);    // D (LSB)
                break;

            case ModbusByteOrder.LittleEndianByteSwap:
                // CDAB: reg[0]=CD (low word, MSB first), reg[1]=AB (high word, MSB first)
                // BitConverter needs [D, C, B, A] (LSB first)
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

    /// <summary>
    /// Extract 8 bytes from 4 registers in the correct order for BitConverter (little-endian).
    /// </summary>
    private static byte[] ExtractBytes64(ushort[] registers, ModbusByteOrder byteOrder)
    {
        var bytes = new byte[8];

        switch (byteOrder)
        {
            case ModbusByteOrder.BigEndian:
                // Standard Modbus Big Endian
                // reg[0]=most significant word, reg[3]=least significant word
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
                // Little Endian Word + Little Endian Byte
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
                bytes[7] = (byte)(registers[0] & 0xFF);
                bytes[6] = (byte)(registers[0] >> 8);
                bytes[5] = (byte)(registers[1] & 0xFF);
                bytes[4] = (byte)(registers[1] >> 8);
                bytes[3] = (byte)(registers[2] & 0xFF);
                bytes[2] = (byte)(registers[2] >> 8);
                bytes[1] = (byte)(registers[3] & 0xFF);
                bytes[0] = (byte)(registers[3] >> 8);
                break;

            case ModbusByteOrder.LittleEndianByteSwap:
                // Little Endian Word + Big Endian Byte (word swapped)
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

    /// <summary>
    /// Pack 4 bytes (from BitConverter, little-endian) into 2 registers based on byte order.
    /// </summary>
    private static ushort[] PackBytes32(byte[] bytes, ModbusByteOrder byteOrder)
    {
        var registers = new ushort[2];

        switch (byteOrder)
        {
            case ModbusByteOrder.BigEndian:
                // ABCD: reg[0]=AB (high word), reg[1]=CD (low word)
                // bytes from BitConverter: [D=0, C=1, B=2, A=3] (LSB first)
                registers[0] = (ushort)((bytes[3] << 8) | bytes[2]);  // AB
                registers[1] = (ushort)((bytes[1] << 8) | bytes[0]);  // CD
                break;

            case ModbusByteOrder.LittleEndian:
                // DCBA: reg[0]=DC (low word), reg[1]=BA (high word)
                registers[0] = (ushort)((bytes[0] << 8) | bytes[1]);  // DC
                registers[1] = (ushort)((bytes[2] << 8) | bytes[3]);  // BA
                break;

            case ModbusByteOrder.BigEndianByteSwap:
                // BADC: reg[0]=BA (high word, byte swapped), reg[1]=DC (low word, byte swapped)
                registers[0] = (ushort)((bytes[2] << 8) | bytes[3]);  // BA
                registers[1] = (ushort)((bytes[0] << 8) | bytes[1]);  // DC
                break;

            case ModbusByteOrder.LittleEndianByteSwap:
                // CDAB: reg[0]=CD (low word), reg[1]=AB (high word)
                registers[0] = (ushort)((bytes[1] << 8) | bytes[0]);  // CD
                registers[1] = (ushort)((bytes[3] << 8) | bytes[2]);  // AB
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "Unknown byte order");
        }

        return registers;
    }

    /// <summary>
    /// Pack 8 bytes (from BitConverter, little-endian) into 4 registers based on byte order.
    /// </summary>
    private static ushort[] PackBytes64(byte[] bytes, ModbusByteOrder byteOrder)
    {
        var registers = new ushort[4];

        switch (byteOrder)
        {
            case ModbusByteOrder.BigEndian:
                // Standard Modbus Big Endian
                registers[0] = (ushort)((bytes[7] << 8) | bytes[6]);
                registers[1] = (ushort)((bytes[5] << 8) | bytes[4]);
                registers[2] = (ushort)((bytes[3] << 8) | bytes[2]);
                registers[3] = (ushort)((bytes[1] << 8) | bytes[0]);
                break;

            case ModbusByteOrder.LittleEndian:
                registers[0] = (ushort)((bytes[0] << 8) | bytes[1]);
                registers[1] = (ushort)((bytes[2] << 8) | bytes[3]);
                registers[2] = (ushort)((bytes[4] << 8) | bytes[5]);
                registers[3] = (ushort)((bytes[6] << 8) | bytes[7]);
                break;

            case ModbusByteOrder.BigEndianByteSwap:
                registers[0] = (ushort)((bytes[6] << 8) | bytes[7]);
                registers[1] = (ushort)((bytes[4] << 8) | bytes[5]);
                registers[2] = (ushort)((bytes[2] << 8) | bytes[3]);
                registers[3] = (ushort)((bytes[0] << 8) | bytes[1]);
                break;

            case ModbusByteOrder.LittleEndianByteSwap:
                registers[0] = (ushort)((bytes[1] << 8) | bytes[0]);
                registers[1] = (ushort)((bytes[3] << 8) | bytes[2]);
                registers[2] = (ushort)((bytes[5] << 8) | bytes[4]);
                registers[3] = (ushort)((bytes[7] << 8) | bytes[6]);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(byteOrder), byteOrder, "Unknown byte order");
        }

        return registers;
    }

    #endregion
}
