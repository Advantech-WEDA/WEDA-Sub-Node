using System.Text.Json.Nodes;

using Weda.SubNode.Abstractions.Dsp;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// Cached registration entry for one discovered <see cref="IDspFilter"/>
/// implementation, holding both the runtime factory (Dict → instance) and the
/// pre-emitted DTDL v3 Interface for capability upload.
/// </summary>
internal sealed record DspFilterRegistration(
    string TypeName,
    string? Description,
    Type ParameterType,
    Func<Dictionary<string, object>, IDspFilter> Factory,
    JsonObject ParameterSchema);
