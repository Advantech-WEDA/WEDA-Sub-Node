using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Utilities;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Modbus-specific configuration extensions for DeviceConfiguration
/// </summary>
public static class ModbusDeviceConfigurationExtensions
{
    /// <summary>
    /// Get Modbus SlaveId (protocol layer setting)
    /// </summary>
    public static byte GetModbusSlaveId(this DeviceConfiguration config)
    {
        return Convert.ToByte(config.Communication.GetValueOrDefault("SlaveId", 1));
    }

    /// <summary>
    /// Get Modbus ByteOrder (protocol layer setting)
    /// Default: BigEndian (standard Modbus)
    /// </summary>
    public static ModbusByteOrder GetModbusByteOrder(this DeviceConfiguration config)
    {
        var byteOrderStr = config.Communication.GetValueOrDefault("ByteOrder")?.ToString() ?? "BigEndian";
        return Enum.Parse<ModbusByteOrder>(byteOrderStr);
    }

    /// <summary>
    /// Get Modbus sensor register from Sensor
    /// </summary>
    public static ModbusSensorRegister ToModbusRegister(this Sensor sensor)
    {
        var parameters = sensor.Parameters ?? new Dictionary<string, object>();

        return new ModbusSensorRegister
        {
            ResourceId = sensor.ResourceId,
            Name = sensor.Name,
            Dtmi = sensor.Dtmi,
            SensorGroup = sensor.SensorGroup,
            RegisterType = Enum.Parse<ModbusRegisterType>(
                parameters.GetValueOrDefault("RegisterType")?.ToString() ?? "HoldingRegister"),
            RegisterAddress = Convert.ToUInt16(parameters.GetValueOrDefault("RegisterAddress", 0)),
            RegisterCount = Convert.ToUInt16(parameters.GetValueOrDefault("RegisterCount", 1)),
            DataType = Enum.Parse<ModbusDataType>(
                parameters.GetValueOrDefault("DataType")?.ToString() ?? "UInt16"),
            Scale = Convert.ToDouble(parameters.GetValueOrDefault("Scale", 1.0)),
            Offset = Convert.ToDouble(parameters.GetValueOrDefault("Offset", 0.0)),
            Metadata = sensor.Metadata
        };
    }
}

/// <summary>
/// Modbus sensor register mapping (internal representation)
/// </summary>
public class ModbusSensorRegister
{
    public string? ResourceId { get; set; }
    public required string Name { get; set; }
    public required string Dtmi { get; set; }
    public SensorGroup? SensorGroup { get; set; }
    public ModbusRegisterType RegisterType { get; set; } = ModbusRegisterType.HoldingRegister;
    public ushort RegisterAddress { get; set; }
    public ushort RegisterCount { get; set; } = 1;
    public ModbusDataType DataType { get; set; } = ModbusDataType.UInt16;
    public double Scale { get; set; } = 1.0;
    public double Offset { get; set; } = 0.0;
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Modbus register types
/// </summary>
public enum ModbusRegisterType
{
    Coil = 1,
    DiscreteInput = 2,
    HoldingRegister = 3,
    InputRegister = 4
}

/// <summary>
/// Modbus data types
/// </summary>
public enum ModbusDataType
{
    UInt16,
    Int16,
    UInt32,
    Int32,
    Float32,
    UInt64,
    Int64,
    Float64,
    String16    // 16-character ASCII string (16 registers)
}

/// <summary>
/// Modbus byte order modes for multi-register data types (32-bit, 64-bit).
/// Different Modbus devices use different byte/word ordering conventions.
/// </summary>
/// <remarks>
/// For a 32-bit float value with IEEE 754 bytes A(MSB), B, C, D(LSB):
/// - BigEndian (ABCD): Standard Modbus - High word first, MSB first in each word
/// - LittleEndian (DCBA): Low word first, LSB first in each word
/// - BigEndianByteSwap (BADC): High word first, bytes swapped within words
/// - LittleEndianByteSwap (CDAB): Low word first, bytes swapped within words
/// </remarks>
public enum ModbusByteOrder
{
    /// <summary>
    /// Big Endian Word + Big Endian Byte (ABCD) - Standard Modbus
    /// Register[0] = AB (high word, MSB first)
    /// Register[1] = CD (low word, MSB first)
    /// Default and most common Modbus byte order.
    /// </summary>
    BigEndian = 0,

    /// <summary>
    /// Little Endian Word + Little Endian Byte (DCBA)
    /// Register[0] = DC (low word, LSB first)
    /// Register[1] = BA (high word, LSB first)
    /// </summary>
    LittleEndian = 1,

    /// <summary>
    /// Big Endian Word + Little Endian Byte (BADC) - Byte swapped
    /// Register[0] = BA (high word, LSB first)
    /// Register[1] = DC (low word, LSB first)
    /// </summary>
    BigEndianByteSwap = 2,

    /// <summary>
    /// Little Endian Word + Big Endian Byte (CDAB) - Word swapped
    /// Register[0] = CD (low word, MSB first)
    /// Register[1] = AB (high word, MSB first)
    /// </summary>
    LittleEndianByteSwap = 3
}
