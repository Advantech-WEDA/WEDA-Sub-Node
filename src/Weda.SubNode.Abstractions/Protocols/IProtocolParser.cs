namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Protocol parser interface for converting raw protocol data to C# data types
/// Parser only handles protocol-specific encoding/decoding, NOT calibration or scaling
/// </summary>
/// <typeparam name="TRawData">Raw data type from communication layer (e.g., byte[], ushort[])</typeparam>
/// <typeparam name="TValue">Parsed C# value type (usually object to support multiple types)</typeparam>
public interface IProtocolParser<in TRawData, out TValue>
{
    /// <summary>
    /// Data type identifier for this parser (e.g., "Float32", "String16")
    /// </summary>
    string DataType { get; }

    /// <summary>
    /// Parse raw protocol data to C# value
    /// Example: Modbus registers (ushort[]) -> float
    /// </summary>
    TValue Parse(TRawData rawData);
}

/// <summary>
/// Bidirectional protocol parser for read/write operations
/// </summary>
public interface IBidirectionalProtocolParser<TRawData, TValue> : IProtocolParser<TRawData, TValue>
{
    /// <summary>
    /// Encode C# value to raw protocol data (for write operations)
    /// Example: float -> Modbus registers (ushort[])
    /// </summary>
    TRawData Encode(TValue value);
}
