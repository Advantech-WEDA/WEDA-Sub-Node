namespace Weda.SubNode.Abstractions.Protocols;

// Note: This file contains Modbus-specific parser interfaces (legacy)
// For ISensing and other message-based protocols, use IProtocolParser from IProtocolParser.cs

/// <summary>
/// Protocol parser interface for Modbus data type conversion
/// </summary>
public interface IModbusDataParser<in TRawData, out TValue>
{
    string DataType { get; }
    TValue Parse(TRawData rawData);
}

/// <summary>
/// Factory for creating Modbus protocol parsers
/// </summary>
public interface IProtocolParserFactory
{
    /// <summary>
    /// Create a Modbus parser for the specified data type
    /// </summary>
    /// <param name="dataType">Data type identifier (e.g., "Float32", "String16")</param>
    /// <returns>Protocol parser instance</returns>
    IModbusDataParser<byte[], object> CreateParser(string dataType);
}
