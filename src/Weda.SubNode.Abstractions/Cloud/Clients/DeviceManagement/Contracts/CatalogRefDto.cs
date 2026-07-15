using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Thin catalog reference: just enough to point at a capability definition.
/// The full DTDL Interface lives in <see cref="DeviceConfigurationDto.Dtdl"/>
/// and is resolved by the <c>dtmi</c> field.
/// </summary>
public record CatalogRefDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("dtmi")] string Dtmi);

/// <summary>
/// Sensor TYPE catalog reference. Same <c>name</c>/<c>dtmi</c> as
/// <see cref="CatalogRefDto"/> plus <c>deviceType</c> so the frontend can filter
/// available sensor types by the currently-selected device type (matches
/// <c>IConfigurableSensor&lt;TParameter&gt;.DeviceTypeName</c>).
/// </summary>
public record SensorTypeCatalogRefDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("dtmi")] string Dtmi,
    [property: JsonPropertyName("deviceType")] string DeviceType);
