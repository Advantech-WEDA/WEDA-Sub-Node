using System.Text.Json.Nodes;

using Weda.SubNode.Abstractions.Transforms;

namespace Weda.SubNode.Core.Transforms;

/// <summary>
/// Cached registration entry for one discovered <see cref="ITelemetryTransform"/>
/// implementation, holding both the runtime factory (Dict → instance) and the
/// pre-emitted DTDL v3 Interface for capability upload.
/// </summary>
internal sealed record TransformRegistration(
    string TypeName,
    string? Description,
    Type ParameterType,
    Func<Dictionary<string, object>, ITelemetryTransform> Factory,
    JsonObject ParameterSchema);
