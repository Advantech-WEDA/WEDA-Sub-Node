using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

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
/// DTDL v3 Interface (as a <see cref="JsonObject"/>) for the command's TParameter,
/// emitted by <c>Weda.Dtdl.Emit.DtdlInterfaceEmitter</c>.
/// </param>
/// <param name="ResponseSchema">
/// DTDL v3 Interface (as a <see cref="JsonObject"/>) for the command's TResult;
/// cloud uses this so agents and UI know what payload shape to expect on response.
/// </param>
/// <param name="AutoAck">
/// True when the command auto-acknowledges; false when the handler is
/// expected to send the ack explicitly. Sourced from <c>[AutoAck]</c>.
/// </param>
public record CommandDescriptorDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("parameterSchema")] JsonObject ParameterSchema,
    [property: JsonPropertyName("responseSchema")] JsonObject ResponseSchema,
    [property: JsonPropertyName("autoAck")] bool AutoAck);
