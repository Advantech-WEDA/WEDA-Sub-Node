namespace Weda.SubNode.Abstractions.Protocols;

/// <summary>
/// Factory for creating protocol parsers
/// </summary>
public interface IProtocolParserFactory
{
    /// <summary>
    /// Create a parser for the specified data type
    /// </summary>
    /// <param name="dataType">Data type identifier (e.g., "Float32", "String16")</param>
    /// <returns>Protocol parser instance</returns>
    IProtocolParser<byte[], object> CreateParser(string dataType);
}
