using System.Buffers.Text;
using System.IO.Compression;
using System.Text;

using ErrorOr;

namespace Weda.SubNode.Core.Protocols.Cfx;

/// <summary>
/// Decodes a CFX message payload received over a Pub/Sub transport into its JSON text.
/// </summary>
/// <remarks>
/// CFX endpoints bridged onto MQTT (for example through RabbitMQ) publish the envelope as
/// GZip-compressed UTF-8 JSON. Some bridges and most documentation instead present the same
/// bytes Base64-encoded, and a bridge configured without compression publishes plain JSON.
/// All three encodings are accepted by sniffing the payload rather than trusting configuration,
/// because a mis-declared encoding would otherwise silently drop every message from a line.
/// </remarks>
public static class CfxPayloadCodec
{
    /// <summary>GZip member header, RFC 1952 section 2.3.1: ID1 = 0x1f, ID2 = 0x8b.</summary>
    private const byte GzipId1 = 0x1f;
    private const byte GzipId2 = 0x8b;

    /// <summary>
    /// Upper bound on the decompressed payload, guarding against a decompression bomb from an
    /// untrusted broker. The largest observed real message (a 24-slot Hermes magazine) is ~9 KB.
    /// </summary>
    public const int MaxDecompressedBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Decodes a raw transport payload into CFX envelope JSON.
    /// </summary>
    /// <param name="payload">Raw bytes as delivered by the transport.</param>
    /// <returns>
    /// The envelope JSON on success; otherwise a <c>Cfx.Payload.*</c> error describing which
    /// decoding step failed. Never throws.
    /// </returns>
    public static ErrorOr<string> Decode(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return Error.Validation(
                code: "Cfx.Payload.Empty",
                description: "CFX payload is empty.");
        }

        if (IsGzip(payload))
        {
            return Decompress(payload);
        }

        if (LooksLikeJson(payload))
        {
            return Utf8ToString(payload);
        }

        // Not GZip and not JSON: the bridge (or a captured sample) Base64-wrapped the payload.
        // The decoded bytes are themselves either GZip or JSON, so recurse exactly once.
        return DecodeBase64(payload)
            .Then(decoded => IsGzip(decoded)
                ? Decompress(decoded)
                : LooksLikeJson(decoded)
                    ? Utf8ToString(decoded)
                    : Error.Validation(
                        code: "Cfx.Payload.UnrecognizedEncoding",
                        description: "CFX payload is Base64 but decodes to neither GZip nor JSON."));
    }

    /// <summary>
    /// Returns <c>true</c> when the payload carries a GZip member header.
    /// </summary>
    public static bool IsGzip(ReadOnlySpan<byte> payload) =>
        payload.Length >= 2 && payload[0] == GzipId1 && payload[1] == GzipId2;

    /// <summary>
    /// Returns <c>true</c> when the payload's first non-whitespace byte opens a JSON object or array.
    /// </summary>
    private static bool LooksLikeJson(ReadOnlySpan<byte> payload)
    {
        foreach (var b in payload)
        {
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            {
                continue;
            }

            // A UTF-8 BOM precedes the opening brace on payloads written by some .NET bridges.
            if (b == 0xEF)
            {
                return payload.Length >= 4
                    && payload[1] == 0xBB
                    && payload[2] == 0xBF
                    && LooksLikeJson(payload[3..]);
            }

            return b is (byte)'{' or (byte)'[';
        }

        return false;
    }

    private static ErrorOr<byte[]> DecodeBase64(ReadOnlySpan<byte> payload)
    {
        var buffer = new byte[Base64.GetMaxDecodedFromUtf8Length(payload.Length)];

        var status = Base64.DecodeFromUtf8(payload, buffer, out _, out var written);
        if (status != System.Buffers.OperationStatus.Done)
        {
            return Error.Validation(
                code: "Cfx.Payload.InvalidBase64",
                description: $"CFX payload is neither GZip nor JSON, and Base64 decoding failed ({status}).");
        }

        return buffer[..written];
    }

    private static ErrorOr<string> Decompress(ReadOnlySpan<byte> payload)
    {
        try
        {
            using var source = new MemoryStream(payload.ToArray(), writable: false);
            using var gzip = new GZipStream(source, CompressionMode.Decompress);
            using var destination = new MemoryStream();

            // Copy through a bounded loop rather than CopyTo so a decompression bomb is rejected
            // before it can exhaust memory.
            var buffer = new byte[81920];
            var total = 0;
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > MaxDecompressedBytes)
                {
                    return Error.Validation(
                        code: "Cfx.Payload.TooLarge",
                        description: $"CFX payload exceeds the {MaxDecompressedBytes} byte decompression limit.");
                }

                destination.Write(buffer, 0, read);
            }

            if (total == 0)
            {
                // A truncated GZip member yields no bytes without raising: GZipStream reports
                // end-of-stream rather than throwing. Treat it as corrupt here rather than handing
                // an empty document downstream.
                return Error.Validation(
                    code: "Cfx.Payload.CorruptGzip",
                    description: "CFX payload has a GZip header but decompressed to zero bytes.");
            }

            return Utf8ToString(destination.GetBuffer().AsSpan(0, (int)destination.Length));
        }
        catch (InvalidDataException ex)
        {
            return Error.Validation(
                code: "Cfx.Payload.CorruptGzip",
                description: $"CFX payload has a GZip header but could not be decompressed: {ex.Message}");
        }
    }

    private static ErrorOr<string> Utf8ToString(ReadOnlySpan<byte> utf8)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(utf8);
        }
        catch (DecoderFallbackException ex)
        {
            return Error.Validation(
                code: "Cfx.Payload.InvalidUtf8",
                description: $"CFX payload is not valid UTF-8: {ex.Message}");
        }
    }
}
