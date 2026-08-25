using CFX;

using ErrorOr;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// Opt-in bridge from a received CFX message body to the official SDK's strongly-typed classes.
/// </summary>
/// <remarks>
/// <para>
/// The receive path in <see cref="CfxEnvelopeReader"/> keeps message bodies as JSON text and resolves
/// no types, which is what the raw-JSON telemetry mapping needs. Application code that wants typed
/// access — <c>StationStateChanged.NewState</c> rather than a JSON string — uses this class.
/// </para>
/// <para>
/// CFX bodies are polymorphic: <c>UnitsInspected.InspectedUnits[].Inspections[].Measurements[]</c> is
/// declared as a base <c>Measurement</c> and carries its own nested <c>$type</c> discriminator, so
/// type resolution genuinely is required for a faithful deserialization. The SDK's own serializer
/// performs that resolution with Newtonsoft's <c>TypeNameHandling.Auto</c> and no
/// <c>SerializationBinder</c>, meaning any loadable type can be named by a payload.
/// </para>
/// <para>
/// This class performs the same deserialization behind an <see cref="ISerializationBinder"/> that
/// admits only types from the CFX SDK assembly, so a hostile <c>$type</c> is rejected instead of
/// constructed.
/// </para>
/// </remarks>
public static class CfxTypedBody
{
    /// <summary>Assembly that declares every legitimate CFX message and structure type.</summary>
    private static readonly string CfxAssemblyName = typeof(CFXMessage).Assembly.GetName().Name!;

    private static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        SerializationBinder = new CfxAssemblyOnlyBinder(),
    };

    /// <summary>
    /// Deserializes a CFX message body into its SDK type.
    /// </summary>
    /// <typeparam name="T">The expected CFX message type, for example <c>CFX.Production.WorkStarted</c>.</typeparam>
    /// <param name="messageBodyJson">
    /// Raw body JSON, as carried by <see cref="CfxEnvelope.MessageBodyJson"/>.
    /// </param>
    /// <returns>
    /// The typed body on success; otherwise a <c>Cfx.Body.*</c> error. Never throws.
    /// </returns>
    public static ErrorOr<T> Deserialize<T>(string messageBodyJson)
        where T : CFXMessage
    {
        if (string.IsNullOrWhiteSpace(messageBodyJson))
        {
            return Error.Validation(
                code: "Cfx.Body.Empty",
                description: "CFX message body JSON is empty.");
        }

        try
        {
            var body = JsonConvert.DeserializeObject<T>(messageBodyJson, Settings);

            if (body is null)
            {
                return Error.Validation(
                    code: "Cfx.Body.Null",
                    description: $"CFX message body deserialized to null for {typeof(T).FullName}.");
            }

            return body;
        }
        catch (JsonException ex)
        {
            return Error.Validation(
                code: "Cfx.Body.DeserializationFailed",
                description:
                    $"Failed to deserialize CFX message body as {typeof(T).FullName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Restricts <c>$type</c> resolution to the CFX SDK assembly.
    /// </summary>
    private sealed class CfxAssemblyOnlyBinder : ISerializationBinder
    {
        private readonly DefaultSerializationBinder _inner = new();

        public Type BindToType(string? assemblyName, string typeName)
        {
            // A payload naming any assembly other than the CFX SDK is rejected outright rather than
            // resolved, so a hostile broker cannot select a type for construction.
            if (!string.IsNullOrEmpty(assemblyName)
                && !string.Equals(assemblyName, CfxAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                throw new JsonSerializationException(
                    $"CFX type discriminator names assembly '{assemblyName}', but only "
                    + $"'{CfxAssemblyName}' types may be deserialized.");
            }

            if (!typeName.StartsWith("CFX.", StringComparison.Ordinal))
            {
                throw new JsonSerializationException(
                    $"CFX type discriminator names type '{typeName}', which is outside the CFX namespace.");
            }

            return _inner.BindToType(CfxAssemblyName, typeName);
        }

        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName) =>
            _inner.BindToName(serializedType, out assemblyName, out typeName);
    }
}
