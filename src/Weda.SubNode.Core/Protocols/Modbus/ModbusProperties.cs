using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Protocol-specific settings for a Modbus device, independent of transport
/// (TCP / RTU / ASCII).
/// </summary>
/// <remarks>
/// Paired with a transport-layer POCO (such as
/// <see cref="Weda.SubNode.Core.Communication.Tcp.TcpCommunicationSettings"/>)
/// via <c>IConfigurableDevice&lt;TCommunication, ModbusProperties&gt;</c> on the
/// device's strongly-typed configuration class. Surfaced to cloud / front-end via
/// the emitted DTDL Interface (<c>contents[].name = "Properties"</c>).
/// </remarks>
public class ModbusProperties
{
    /// <summary>
    /// Modbus unit / slave identifier (1..247). Defaults to 1.
    /// </summary>
    [Range(1, 247)]
    [Display(Name = "Slave ID")]
    [Description("Modbus unit / slave identifier.")]
    [JsonPropertyName("slaveId")]
    public byte SlaveId { get; init; } = 1;

    /// <summary>
    /// Word + byte ordering used to decode multi-register values. Defaults to
    /// <see cref="ModbusByteOrder.BigEndian"/> (the Modbus standard ABCD).
    /// </summary>
    [Display(Name = "Byte Order")]
    [Description("Word and byte ordering for multi-register values.")]
    [JsonPropertyName("byteOrder")]
    public ModbusByteOrder ByteOrder { get; init; } = ModbusByteOrder.BigEndian;
}
