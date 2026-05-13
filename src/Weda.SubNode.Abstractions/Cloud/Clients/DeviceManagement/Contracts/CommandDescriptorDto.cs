using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Schema;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Describes one device command the SubNode can handle, including both the
/// invocation parameter shape and the response shape.
/// </summary>
/// <param name="Name">
/// Command name routed through <c>[DeviceCmd("...")]</c> (e.g. "do.set").
/// </param>
/// <param name="Description">Human-readable summary; null when not provided.</param>
/// <param name="ParameterSchema">
/// JSON Schema for the command's TParameter, derived from DataAnnotations.
/// </param>
/// <param name="ResponseSchema">
/// JSON Schema for the command's TResult; cloud uses this so agents and UI
/// know what payload shape to expect on response.
/// </param>
/// <param name="AutoAck">
/// True when the command auto-acknowledges; false when the handler is
/// expected to send the ack explicitly. Sourced from <c>[AutoAck]</c>.
/// </param>
public record CommandDescriptorDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("parameterSchema")] JsonSchemaDto ParameterSchema,
    [property: JsonPropertyName("responseSchema")] JsonSchemaDto ResponseSchema,
    [property: JsonPropertyName("autoAck")] bool AutoAck);
