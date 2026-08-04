using System.Text.Json;

using Weda.SubNode.Core.Protocols.Cfx;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class CfxEnvelopeReaderTests
{
    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Read_RealCapture_ExtractsEnvelopeFields(string messageName)
    {
        // Arrange
        var json = DecodeFixture(messageName);

        // Act
        var result = CfxEnvelopeReader.Read(json);

        // Assert
        Assert.False(result.IsError);

        var envelope = result.Value;
        Assert.Equal(messageName, envelope.MessageName);
        Assert.Equal("1.3", envelope.Version);
        Assert.StartsWith("SUNJSONG.", envelope.Source);
        Assert.NotNull(envelope.UniqueId);
        Assert.NotNull(envelope.TimeStamp);

        // Every captured message is a broadcast event, so there is no addressed recipient.
        Assert.Null(envelope.Target);
        Assert.Null(envelope.RequestId);
    }

    [Theory]
    [MemberData(nameof(AllMessages))]
    public void Read_RealCapture_KeepsMessageBodyAsWellFormedJson(string messageName)
    {
        // Arrange
        var json = DecodeFixture(messageName);

        // Act
        var envelope = CfxEnvelopeReader.Read(json).Value;

        // Assert — the body round-trips as its own JSON document.
        using var body = JsonDocument.Parse(envelope.MessageBodyJson);
        Assert.Equal(JsonValueKind.Object, body.RootElement.ValueKind);
        Assert.Equal($"{messageName}, CFX", envelope.MessageBodyType);
    }

    [Fact]
    public void Read_PreservesPublisherUtcOffset()
    {
        // Arrange — endpoints stamp local time with an offset; it must survive parsing rather than
        // being normalised to UTC, so the endpoint's wall-clock time stays recoverable.
        var json = DecodeFixture("CFX.ResourcePerformance.StationStateChanged");

        // Act
        var envelope = CfxEnvelopeReader.Read(json).Value;

        // Assert
        Assert.Equal(TimeSpan.FromHours(8), envelope.TimeStamp!.Value.Offset);
        Assert.Equal(17, envelope.TimeStamp.Value.Hour);
    }

    [Fact]
    public void Read_StationStateChanged_BodyCarriesSemiStateCodesAsStrings()
    {
        // Arrange
        var json = DecodeFixture("CFX.ResourcePerformance.StationStateChanged");

        // Act
        var envelope = CfxEnvelopeReader.Read(json).Value;

        // Assert — states are numeric SEMI E10/E58 codes carried as JSON strings, not numbers.
        using var body = JsonDocument.Parse(envelope.MessageBodyJson);
        Assert.Equal("1400", body.RootElement.GetProperty("OldState").GetString());
        Assert.Equal("5000", body.RootElement.GetProperty("NewState").GetString());
        Assert.Equal("00:29:01.0840472", body.RootElement.GetProperty("OldStateDuration").GetString());
    }

    [Fact]
    public void Read_MagazineArrived_RetainsEveryNestedHermesUnit()
    {
        // Arrange — the deepest captured body; nothing about it may be flattened or truncated.
        var json = DecodeFixture("CFX.Production.Hermes.MagazineArrived");

        // Act
        var envelope = CfxEnvelopeReader.Read(json).Value;

        // Assert
        using var body = JsonDocument.Parse(envelope.MessageBodyJson);
        var units = body.RootElement
            .GetProperty("MagazineData")
            .GetProperty("HermesUnits");

        Assert.Equal(24, units.GetArrayLength());

        // "Lenght" is misspelled in the CFX specification itself; a corrected spelling here would
        // silently read nothing.
        Assert.Equal(140.0, units[0].GetProperty("Lenght").GetDouble());
    }

    [Fact]
    public void Read_UnitsInspected_RetainsNestedTypeDiscriminators()
    {
        // Arrange
        var json = DecodeFixture("CFX.Production.TestAndInspection.UnitsInspected");

        // Act
        var envelope = CfxEnvelopeReader.Read(json).Value;

        // Assert — polymorphism is recursive: measurements carry their own $type.
        using var body = JsonDocument.Parse(envelope.MessageBodyJson);
        var measurement = body.RootElement
            .GetProperty("InspectedUnits")[0]
            .GetProperty("Inspections")[0]
            .GetProperty("Measurements")[0];

        Assert.Equal(
            "CFX.Structures.SolderPasteInspection.SolderPasteMeasurement, CFX",
            measurement.GetProperty("$type").GetString());

        // UnitsInspected spells the field "TransactionId" while other messages use "TransactionID".
        Assert.True(body.RootElement.TryGetProperty("TransactionId", out _));
    }

    [Fact]
    public void Read_BlankJson_ReturnsEmptyError()
    {
        var result = CfxEnvelopeReader.Read("   ");

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Envelope.Empty", result.FirstError.Code);
    }

    [Fact]
    public void Read_MalformedJson_ReturnsMalformedError()
    {
        var result = CfxEnvelopeReader.Read("{\"MessageName\":");

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Envelope.MalformedJson", result.FirstError.Code);
    }

    [Fact]
    public void Read_JsonArray_ReturnsNotAnObjectError()
    {
        var result = CfxEnvelopeReader.Read("[]");

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Envelope.NotAnObject", result.FirstError.Code);
    }

    [Fact]
    public void Read_WithoutMessageName_ReturnsMissingMessageNameError()
    {
        var result = CfxEnvelopeReader.Read("""{"MessageBody":{}}""");

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Envelope.MissingMessageName", result.FirstError.Code);
    }

    [Fact]
    public void Read_WithBlankMessageName_ReturnsBlankMessageNameError()
    {
        var result = CfxEnvelopeReader.Read("""{"MessageName":"  ","MessageBody":{}}""");

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Envelope.BlankMessageName", result.FirstError.Code);
    }

    [Fact]
    public void Read_WithoutMessageBody_ReturnsMissingMessageBodyError()
    {
        var result = CfxEnvelopeReader.Read("""{"MessageName":"CFX.EndpointConnected"}""");

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Envelope.MissingMessageBody", result.FirstError.Code);
    }

    [Fact]
    public void Read_WithNullMessageBody_ReturnsMissingMessageBodyError()
    {
        var result = CfxEnvelopeReader.Read(
            """{"MessageName":"CFX.EndpointConnected","MessageBody":null}""");

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Envelope.MissingMessageBody", result.FirstError.Code);
    }

    [Fact]
    public void Read_DoesNotResolveTypesFromPayload()
    {
        // Arrange — a hostile $type must be carried through as inert text, never resolved. This is
        // the property that makes the receive path safe against the CFX SDK's unbounded
        // TypeNameHandling.
        const string json = """
            {
              "MessageName": "CFX.EndpointConnected",
              "MessageBody": {
                "$type": "System.Diagnostics.Process, System.Diagnostics.Process"
              }
            }
            """;

        // Act
        var result = CfxEnvelopeReader.Read(json);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(
            "System.Diagnostics.Process, System.Diagnostics.Process",
            result.Value.MessageBodyType);
    }

    public static TheoryData<string> AllMessages() => CfxFixtures.AllMessageNames();

    private static string DecodeFixture(string messageName) =>
        CfxPayloadCodec.Decode(CfxFixtures.GzipPayload(messageName)).Value;
}
