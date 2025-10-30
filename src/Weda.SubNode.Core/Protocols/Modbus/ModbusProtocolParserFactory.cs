using Weda.SubNode.Abstractions.Protocols;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Factory for creating Modbus protocol parsers
/// </summary>
public class ModbusProtocolParserFactory
{
    /// <summary>
    /// Create a parser for the specified Modbus data type
    /// </summary>
    public IProtocolParser<ushort[], object> CreateParser(ModbusDataType dataType)
    {
        return new ModbusProtocolParser(dataType);
    }

    /// <summary>
    /// Create a parser from sensor register configuration
    /// </summary>
    public IProtocolParser<ushort[], object> CreateParser(ModbusSensorRegister register)
    {
        return new ModbusProtocolParser(register.DataType);
    }
}
