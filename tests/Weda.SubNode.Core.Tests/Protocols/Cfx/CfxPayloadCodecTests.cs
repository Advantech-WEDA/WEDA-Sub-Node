using System.IO.Compression;
using System.Text;
using System.Text.Json;

using Weda.SubNode.Core.Protocols.Cfx;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class CfxPayloadCodecTests
{
    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Decode_RealGzipCapture_ReturnsEnvelopeJson(string messageName)
    {
        // Arrange
        var payload = CfxFixtures.GzipPayload(messageName);

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert — asserted against the parsed document rather than the text, because endpoints do
        // not agree on JSON formatting (see Decode_CapturesUseBothCompactAndPrettyJson).
        Assert.False(result.IsError);

        using var document = JsonDocument.Parse(result.Value);
        Assert.Equal(messageName, document.RootElement.GetProperty("MessageName").GetString());
    }

    [Fact]
    public void Decode_CapturesUseBothCompactAndPrettyJson()
    {
        // Endpoints on the same line disagree on whether to pretty-print the envelope, so no
        // consumer may assume either form. Pinned here so a formatting-sensitive assertion cannot
        // creep back in.
        var forms = CfxFixtures.MessageNames()
            .Select(name => CfxPayloadCodec.Decode(CfxFixtures.GzipPayload(name)).Value)
            .Select(json => json.Contains("\"MessageName\":\"", StringComparison.Ordinal)
                ? "compact"
                : "pretty")
            .ToList();

        Assert.Contains("compact", forms);
        Assert.Contains("pretty", forms);
    }

    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Decode_Base64WrappedGzipCapture_ReturnsSameJsonAsRawGzip(string messageName)
    {
        // Arrange — documentation and some bridges present the same bytes Base64-encoded.
        var gzip = CfxFixtures.GzipPayload(messageName);
        var base64 = Encoding.ASCII.GetBytes(Convert.ToBase64String(gzip));

        // Act
        var fromGzip = CfxPayloadCodec.Decode(gzip);
        var fromBase64 = CfxPayloadCodec.Decode(base64);

        // Assert
        Assert.False(fromBase64.IsError);
        Assert.Equal(fromGzip.Value, fromBase64.Value);
    }

    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Decode_PlainJson_ReturnsItUnchanged(string messageName)
    {
        // Arrange — a bridge configured without compression publishes plain JSON.
        var json = Decompress(CfxFixtures.GzipPayload(messageName));
        var payload = Encoding.UTF8.GetBytes(json);

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(json, result.Value);
    }

    [Fact]
    public void Decode_Base64WrappedPlainJson_ReturnsJson()
    {
        // Arrange
        const string json = """{"MessageName":"CFX.EndpointConnected","MessageBody":{}}""";
        var payload = Encoding.ASCII.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(json)));

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(json, result.Value);
    }

    [Fact]
    public void Decode_JsonWithUtf8Bom_ReturnsJson()
    {
        // Arrange — some .NET bridges emit a BOM ahead of the opening brace.
        const string json = """{"MessageName":"CFX.EndpointConnected","MessageBody":{}}""";
        var payload = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray();

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert
        Assert.False(result.IsError);
        Assert.Contains("CFX.EndpointConnected", result.Value);
    }

    [Fact]
    public void Decode_LeadingWhitespaceBeforeJson_ReturnsJson()
    {
        // Arrange
        var payload = Encoding.UTF8.GetBytes("  \r\n\t{\"MessageName\":\"CFX.EndpointConnected\"}");

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert
        Assert.False(result.IsError);
    }

    [Fact]
    public void Decode_EmptyPayload_ReturnsEmptyError()
    {
        // Act
        var result = CfxPayloadCodec.Decode([]);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Cfx.Payload.Empty", result.FirstError.Code);
    }

    [Fact]
    public void Decode_GzipWithGarbledDeflateStream_IsRejected()
    {
        // Arrange — a complete 10-byte GZip header followed by garbage.
        var payload = new byte[]
        {
            0x1f, 0x8b, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF,
        };

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert — garbage may or may not form a decodable deflate block, so it surfaces either as
        // a corrupt stream or as the non-UTF-8 bytes it expands to. Both are correct rejections; the
        // guarantee under test is that nothing is ever passed through as if it were valid.
        Assert.True(result.IsError);
        Assert.StartsWith("Cfx.Payload.", result.FirstError.Code);
    }

    [Fact]
    public void Decode_TruncatedGzipHeader_ReturnsCorruptGzipError()
    {
        // Arrange — a header cut short mid-transfer. GZipStream reports end-of-stream rather than
        // throwing, so without an explicit zero-length check this would decode to an empty document.
        var payload = new byte[] { 0x1f, 0x8b, 0x08, 0x00, 0xAA };

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Cfx.Payload.CorruptGzip", result.FirstError.Code);
    }

    [Fact]
    public void Decode_NeitherGzipNorJsonNorBase64_ReturnsInvalidBase64Error()
    {
        // Arrange
        var payload = Encoding.ASCII.GetBytes("this is not a CFX payload!!");

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Cfx.Payload.InvalidBase64", result.FirstError.Code);
    }

    [Fact]
    public void Decode_ValidBase64OfGarbage_ReturnsUnrecognizedEncodingError()
    {
        // Arrange
        var payload = Encoding.ASCII.GetBytes(Convert.ToBase64String([0x01, 0x02, 0x03, 0x04]));

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Cfx.Payload.UnrecognizedEncoding", result.FirstError.Code);
    }

    [Fact]
    public void Decode_DecompressionBomb_IsRejectedWithoutExhaustingMemory()
    {
        // Arrange — highly compressible input that expands past the guard limit.
        var bomb = Compress(new byte[CfxPayloadCodec.MaxDecompressedBytes + 1024]);

        // Act
        var result = CfxPayloadCodec.Decode(bomb);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Cfx.Payload.TooLarge", result.FirstError.Code);
    }

    [Fact]
    public void Decode_GzipContainingInvalidUtf8_ReturnsInvalidUtf8Error()
    {
        // Arrange — 0xC3 starts a two-byte sequence that is never completed.
        var payload = Compress([(byte)'{', 0xC3]);

        // Act
        var result = CfxPayloadCodec.Decode(payload);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Cfx.Payload.InvalidUtf8", result.FirstError.Code);
    }

    [Fact]
    public void IsGzip_DetectsGzipMemberHeader()
    {
        Assert.True(CfxPayloadCodec.IsGzip([0x1f, 0x8b, 0x08]));
        Assert.False(CfxPayloadCodec.IsGzip("{}"u8));
        Assert.False(CfxPayloadCodec.IsGzip([0x1f]));
        Assert.False(CfxPayloadCodec.IsGzip([]));
    }

    public static TheoryData<string> AllMessages() => CfxFixtures.AllMessageNames();

    private static string Decompress(byte[] payload)
    {
        using var source = new MemoryStream(payload);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static byte[] Compress(byte[] content)
    {
        using var destination = new MemoryStream();
        using (var gzip = new GZipStream(destination, CompressionLevel.Optimal))
        {
            gzip.Write(content);
        }

        return destination.ToArray();
    }
}
