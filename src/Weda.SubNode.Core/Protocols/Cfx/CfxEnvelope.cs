using System.Text.Json;

using ErrorOr;

namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// A CFX envelope read from broker JSON, with its message body kept as raw JSON text.
/// </summary>
/// <remarks>
/// <para>
/// This is a read-only projection of the envelope, parsed with <see cref="System.Text.Json"/> and
/// deliberately performing no type resolution. See <see cref="CfxEnvelopeReader"/> for why the
/// official SDK's deserializer is not used on the receive path.
/// </para>
/// <para>
/// Application code that wants strongly-typed access to a body can pass
/// <see cref="MessageBodyJson"/> to <c>CFX.CFXEnvelope.FromJson</c> or to
/// <c>CfxTypedBody.Deserialize&lt;T&gt;</c>, having first satisfied itself that the source is trusted.
/// </para>
/// </remarks>
public sealed record CfxEnvelope
{
    /// <summary>Fully-qualified CFX message name, for example <c>CFX.Production.WorkStarted</c>.</summary>
    public required string MessageName { get; init; }

    /// <summary>
    /// Raw JSON text of the <c>MessageBody</c> object, exactly as received.
    /// </summary>
    public required string MessageBodyJson { get; init; }

    /// <summary>
    /// Assembly-qualified type discriminator declared by the body's <c>$type</c> property,
    /// for example <c>CFX.Production.WorkStarted, CFX</c>. Null when the body omits it.
    /// </summary>
    public string? MessageBodyType { get; init; }

    /// <summary>CFX specification version the publisher claims, for example <c>1.3</c>.</summary>
    public string? Version { get; init; }

    /// <summary>
    /// Publisher timestamp, with its original UTC offset preserved.
    /// </summary>
    /// <remarks>
    /// Endpoints stamp local time with an offset (for example <c>+08:00</c>). The offset is retained
    /// here rather than normalised, so that a downstream consumer can still recover the endpoint's
    /// wall-clock time.
    /// </remarks>
    public DateTimeOffset? TimeStamp { get; init; }

    /// <summary>Publisher-assigned unique message identifier.</summary>
    public string? UniqueId { get; init; }

    /// <summary>CFX handle of the publishing endpoint, for example <c>SUNJSONG.SLD880A.0001</c>.</summary>
    public string? Source { get; init; }

    /// <summary>Intended recipient's CFX handle; null for broadcast events.</summary>
    public string? Target { get; init; }

    /// <summary>Correlation identifier when the message answers a request.</summary>
    public string? RequestId { get; init; }
}

/// <summary>
/// Parses CFX envelope JSON without resolving any .NET types from the payload.
/// </summary>
/// <remarks>
/// <para>
/// The official CFX SDK deserializes envelopes with Newtonsoft's
/// <c>TypeNameHandling.Auto</c> and no <c>SerializationBinder</c>, and its
/// <c>CFXEnvelope.FromJson</c> additionally falls back to <c>Type.GetType(MessageName)</c>. Because
/// <c>CFXEnvelope.MessageBody</c> is declared as <c>object</c>, the <c>$type</c> discriminator in a
/// received payload is honoured and resolved against loaded assemblies. On a subscriber consuming a
/// shared or internet-reachable broker, both are attacker-controlled inputs to type resolution.
/// </para>
/// <para>
/// The receive path therefore parses with <see cref="System.Text.Json"/> and keeps the body as text.
/// Nothing in the payload can name a type, so no type is ever constructed from broker data. This
/// costs nothing for the raw-JSON telemetry mapping, which needs the body as text regardless.
/// </para>
/// </remarks>
public static class CfxEnvelopeReader
{
    private const string MessageNameProperty = "MessageName";
    private const string MessageBodyProperty = "MessageBody";
    private const string TypeDiscriminatorProperty = "$type";

    /// <summary>
    /// Parses CFX envelope JSON into a <see cref="CfxEnvelope"/>.
    /// </summary>
    /// <param name="json">Envelope JSON, as produced by <see cref="CfxPayloadCodec.Decode"/>.</param>
    /// <returns>
    /// The parsed envelope on success; otherwise a <c>Cfx.Envelope.*</c> error describing what was
    /// wrong with the document. Never throws.
    /// </returns>
    public static ErrorOr<CfxEnvelope> Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Error.Validation(
                code: "Cfx.Envelope.Empty",
                description: "CFX envelope JSON is empty.");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return Error.Validation(
                code: "Cfx.Envelope.MalformedJson",
                description: $"CFX envelope is not well-formed JSON: {ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return Error.Validation(
                    code: "Cfx.Envelope.NotAnObject",
                    description: $"CFX envelope must be a JSON object but was {root.ValueKind}.");
            }

            if (!root.TryGetProperty(MessageNameProperty, out var messageNameElement)
                || messageNameElement.ValueKind != JsonValueKind.String)
            {
                return Error.Validation(
                    code: "Cfx.Envelope.MissingMessageName",
                    description: "CFX envelope has no string 'MessageName' property.");
            }

            var messageName = messageNameElement.GetString();
            if (string.IsNullOrWhiteSpace(messageName))
            {
                return Error.Validation(
                    code: "Cfx.Envelope.BlankMessageName",
                    description: "CFX envelope 'MessageName' is blank.");
            }

            if (!root.TryGetProperty(MessageBodyProperty, out var bodyElement)
                || bodyElement.ValueKind != JsonValueKind.Object)
            {
                return Error.Validation(
                    code: "Cfx.Envelope.MissingMessageBody",
                    description: $"CFX envelope '{messageName}' has no 'MessageBody' object.");
            }

            return new CfxEnvelope
            {
                MessageName = messageName,
                MessageBodyJson = bodyElement.GetRawText(),
                MessageBodyType = ReadOptionalString(bodyElement, TypeDiscriminatorProperty),
                Version = ReadOptionalString(root, "Version"),
                TimeStamp = ReadOptionalTimeStamp(root, "TimeStamp"),
                UniqueId = ReadOptionalString(root, "UniqueID"),
                Source = ReadOptionalString(root, "Source"),
                Target = ReadOptionalString(root, "Target"),
                RequestId = ReadOptionalString(root, "RequestID"),
            };
        }
    }

    private static string? ReadOptionalString(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;

    private static DateTimeOffset? ReadOptionalTimeStamp(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var element)
            && element.ValueKind == JsonValueKind.String
            && element.TryGetDateTimeOffset(out var timestamp)
                ? timestamp
                : null;
}
