using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Describes one configurable DSP filter the SubNode can attach to a sensor
/// pipeline at runtime.
/// </summary>
/// <param name="TypeName">
/// Stable identifier matching <c>DspFilterConfig.Type</c> (e.g. "kalman").
/// </param>
/// <param name="Description">Human-readable summary; null when not provided.</param>
/// <param name="ParameterSchema">
/// DTDL v3 Interface (as a <see cref="JsonObject"/>) for the strongly-typed parameter
/// class, emitted by <c>Weda.Dtdl.Emit.WedaDtdlEmitter</c> from DataAnnotations.
/// </param>
public record DspFilterDescriptorDto(
    [property: JsonPropertyName("typeName")] string TypeName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("parameterSchema")] JsonObject ParameterSchema);
