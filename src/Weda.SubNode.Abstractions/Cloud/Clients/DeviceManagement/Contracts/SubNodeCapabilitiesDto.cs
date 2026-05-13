using System.Text.Json.Serialization;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Catalogue of extensible capabilities a SubNode binary provides:
/// configurable transforms, DSP filters, and commands.
/// </summary>
/// <remarks>
/// Built once at SubNode startup by reflecting the three registries
/// (TransformFactory / DspFilterFactory / CommandRegistry) and attached
/// to <c>DeviceCapDto.Capabilities</c> at configuration upload time so
/// cloud can validate and render UI without round-tripping back to SubNode.
/// </remarks>
public record SubNodeCapabilitiesDto(
    [property: JsonPropertyName("transforms")] IReadOnlyList<TransformDescriptorDto> Transforms,
    [property: JsonPropertyName("dspFilters")] IReadOnlyList<DspFilterDescriptorDto> DspFilters,
    [property: JsonPropertyName("commands")] IReadOnlyList<CommandDescriptorDto> Commands);
