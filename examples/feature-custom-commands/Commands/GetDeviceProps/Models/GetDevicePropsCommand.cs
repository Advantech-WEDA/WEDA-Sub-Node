using System.ComponentModel;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace CommandHandlerExample.Commands.GetDeviceProps.Models;

/// <summary>
/// "props.get" — query runtime device properties. Payload:
/// <c>{ "parameters": { "options": { "deviceName": "...", "includeSensors": "true" } } }</c>.
/// Note: the map must be a property of a parameters DTO — a raw dictionary as
/// the CommandData&lt;T&gt; type breaks DTDL emission at startup.
/// </summary>
[DeviceCmd("props.get")]
[Description("Query runtime device properties; input options and result data are key/value maps.")]
public class GetDevicePropsCommand : CommandData<GetDevicePropsParameters>
{
}

/// <summary>
/// Parameters: a dynamic option bag (DTDL Map, string → string).
/// Supported keys: deviceName, includeSensors ("true"/"false").
/// </summary>
public class GetDevicePropsParameters
{
    [JsonPropertyName("options")]
    public Dictionary<string, string> Options { get; init; } = [];
}
