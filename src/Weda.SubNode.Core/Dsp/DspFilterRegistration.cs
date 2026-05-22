using Weda.SubNode.Abstractions.Dsp;
using Weda.SubNode.Abstractions.Schema;

namespace Weda.SubNode.Core.Dsp;

/// <summary>
/// Cached registration entry for one discovered <see cref="IDspFilter"/>
/// implementation, holding both the runtime factory (Dict → instance) and the
/// pre-emitted parameter schema for capability upload.
/// </summary>
internal sealed record DspFilterRegistration(
    string TypeName,
    string? Description,
    Type ParameterType,
    Func<Dictionary<string, object>, IDspFilter> Factory,
    JsonSchemaDto ParameterSchema);
