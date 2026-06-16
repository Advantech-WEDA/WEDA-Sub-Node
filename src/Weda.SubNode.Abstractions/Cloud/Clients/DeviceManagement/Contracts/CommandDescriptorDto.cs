using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Describes one device command the SubNode can handle.
/// </summary>
/// <param name="Name">
/// Canonical command name routed through <c>[DeviceCmd("...")]</c> (e.g. "do.set").
/// </param>
/// <param name="Description">Human-readable summary; null when not provided.</param>
/// <param name="Schema">
/// Single DTDL v3 Interface emitted by <c>Weda.Dtdl.Emit.WedaDtdlEmitter.EmitCommand</c>
/// — <c>contents[]</c> has one <c>@type:"Command"</c> entry whose
/// <c>request.schema</c> / <c>response.schema</c> reference Object schemas
/// (Parameters / Result) in <c>schemas[]</c>.
/// </param>
public record CommandDescriptorDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("schema")] JsonObject Schema);
