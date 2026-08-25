using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// Composes the <c>application/json</c> telemetry value reported for a CFX message: the envelope's
/// routing fields alongside the message body.
/// </summary>
/// <remarks>
/// <para>
/// The routing fields travel inside the reported value rather than in
/// <c>TelemetryMeasure.Metadata</c>. A measure's metadata is the framework's chunked-transfer
/// descriptor, not a free-form bag: <c>transferId</c> is minted only when a value is split into
/// chunks, and an <c>application/json</c> value is never chunked, because
/// <c>TelemetrySendMessage.AdaptValue</c> converts a JSON string into a <c>JsonElement</c> before
/// the size test that would trigger chunking. Metadata attached to such a measure therefore
/// reaches the WedaNode without the <c>transferId</c> its telemetry proxy requires, and the whole
/// measure is rejected with
/// <c>measures[0].metadata.transferId: transfer ID is required</c>.
/// </para>
/// <para>
/// Carrying the fields beside the body also makes a stored value self-describing: a consumer
/// reading it back still knows which endpoint emitted the message, and when.
/// </para>
/// </remarks>
internal static class CfxTelemetryPayload
{
    /// <summary>Fully-qualified CFX message name, for example <c>CFX.Production.WorkStarted</c>.</summary>
    internal const string MessageNameProperty = "messageName";

    /// <summary>CFX handle of the publishing endpoint.</summary>
    internal const string SourceProperty = "source";

    /// <summary>Publisher-assigned unique message identifier.</summary>
    internal const string UniqueIdProperty = "uniqueId";

    /// <summary>CFX specification version claimed by the publisher.</summary>
    internal const string VersionProperty = "version";

    /// <summary>Publisher timestamp, round-trip formatted with its original UTC offset.</summary>
    internal const string TimeStampProperty = "timeStamp";

    /// <summary>Transport topic the message arrived on.</summary>
    internal const string TopicProperty = "topic";

    /// <summary>The CFX message body, verbatim.</summary>
    internal const string BodyProperty = "body";

    /// <summary>
    /// Builds the JSON document reported as a sensor's value.
    /// </summary>
    /// <param name="envelope">The decoded CFX envelope.</param>
    /// <param name="topic">The topic the message arrived on; omitted when blank.</param>
    /// <returns>JSON text carrying the routing fields and the body.</returns>
    /// <exception cref="JsonException">
    /// Thrown when the body is not well-formed JSON. It reached here via
    /// <see cref="CfxEnvelopeReader"/>, which already parsed it, so this indicates a programming
    /// error rather than a malformed payload.
    /// </exception>
    internal static string Compose(CfxEnvelope envelope, string? topic)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var buffer = new ArrayBufferWriter<byte>(envelope.MessageBodyJson.Length + 256);

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            writer.WriteString(MessageNameProperty, envelope.MessageName);

            WriteIfPresent(writer, SourceProperty, envelope.Source);
            WriteIfPresent(writer, UniqueIdProperty, envelope.UniqueId);
            WriteIfPresent(writer, VersionProperty, envelope.Version);
            WriteIfPresent(writer, TopicProperty, topic);

            if (envelope.TimeStamp is { } stamp)
            {
                // Round-trip format keeps the endpoint's original UTC offset legible downstream.
                writer.WriteString(TimeStampProperty, stamp.ToString("O"));
            }

            writer.WritePropertyName(BodyProperty);

            // Written raw so the body is passed through byte-for-byte rather than re-serialized:
            // nothing is reordered, renamed, or lost, including the nested $type discriminators
            // that polymorphic CFX bodies rely on.
            writer.WriteRawValue(envelope.MessageBodyJson);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteIfPresent(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            writer.WriteString(propertyName, value);
        }
    }
}
