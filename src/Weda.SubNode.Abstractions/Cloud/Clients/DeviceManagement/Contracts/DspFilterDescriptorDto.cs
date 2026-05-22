using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Schema;

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
/// JSON Schema for the strongly-typed parameter class, derived from
/// DataAnnotations at startup.
/// </param>
public record DspFilterDescriptorDto(
    [property: JsonPropertyName("typeName")] string TypeName,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("parameterSchema")] JsonSchemaDto ParameterSchema);
