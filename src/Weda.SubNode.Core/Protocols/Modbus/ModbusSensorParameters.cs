using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Core.Protocols.Modbus;

/// <summary>
/// Per-sensor protocol-specific parameters for a Modbus register read.
/// </summary>
/// <remarks>
/// Bound by <c>IConfigurableSensor&lt;ModbusSensorParameters&gt;</c> on the
/// sensor's strongly-typed configuration class. Surfaced to cloud / front-end
/// via the emitted DTDL Interface (Sensor category,
/// <c>contents[].name = "Parameters"</c>). Reuses the existing
/// <see cref="ModbusRegisterType"/> and <see cref="ModbusDataType"/> enums
/// declared in <c>ModbusDeviceConfiguration.cs</c>.
/// </remarks>
public class ModbusSensorParameters
{
    /// <summary>
    /// Starting register address (0..65535).
    /// </summary>
    [Required]
    [Range(0, ushort.MaxValue)]
    [Display(Name = "Register Address")]
    [Description("Starting register address.")]
    [JsonPropertyName("registerAddress")]
    public ushort RegisterAddress { get; init; }

    /// <summary>
    /// Number of registers to read in a single batch (1..125 per Modbus TCP spec).
    /// Defaults to 1.
    /// </summary>
    [Range(1, 125)]
    [Display(Name = "Register Count")]
    [Description("Number of registers to read (1..125 per Modbus TCP spec).")]
    [JsonPropertyName("registerCount")]
    public ushort RegisterCount { get; init; } = 1;

    /// <summary>
    /// Modbus register class (Coil / DiscreteInput / HoldingRegister / InputRegister).
    /// Defaults to <see cref="ModbusRegisterType.HoldingRegister"/>.
    /// </summary>
    [Display(Name = "Register Type")]
    [Description("Modbus register class.")]
    [JsonPropertyName("registerType")]
    public ModbusRegisterType RegisterType { get; init; } = ModbusRegisterType.HoldingRegister;

    /// <summary>
    /// Wire data type used to decode the raw register bytes.
    /// Defaults to <see cref="ModbusDataType.UInt16"/>.
    /// </summary>
    [Display(Name = "Data Type")]
    [Description("Wire data type used to decode the register payload.")]
    [JsonPropertyName("dataType")]
    public ModbusDataType DataType { get; init; } = ModbusDataType.UInt16;
}
