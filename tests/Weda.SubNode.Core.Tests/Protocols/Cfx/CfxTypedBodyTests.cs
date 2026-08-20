using CFX.Production;
using CFX.Production.Hermes;
using CFX.Production.TestAndInspection;
using CFX.ResourcePerformance;
using CFX.Structures;

using Weda.SubNode.Core.Protocols.Cfx;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class CfxTypedBodyTests
{
    [Fact]
    public void Deserialize_StationStateChanged_ResolvesSemiStateCodesToNamedStates()
    {
        // Arrange — the raw JSON carries the SEMI E10/E58 codes as the strings "1400" and "5000";
        // the typed path is what turns them into named states, which is the main reason application
        // code would prefer it over the raw body.
        var body = BodyOf("CFX.ResourcePerformance.StationStateChanged");

        // Act
        var result = CfxTypedBody.Deserialize<StationStateChanged>(body);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal(ResourceState.PRD_Engineering, result.Value.OldState);
        Assert.Equal(ResourceState.USD, result.Value.NewState);
        Assert.Equal(TimeSpan.Parse("00:29:01.0840472"), result.Value.OldStateDuration);
    }

    [Fact]
    public void Deserialize_FaultOccurred_ReadsFaultStructure()
    {
        // Arrange
        var body = BodyOf("CFX.ResourcePerformance.FaultOccurred");

        // Act
        var result = CfxTypedBody.Deserialize<FaultOccurred>(body);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal("#A008_0004", result.Value.Fault.FaultCode);
        Assert.Equal("Up/down basket in sensor error", result.Value.Fault.Description);
    }

    [Fact]
    public void Deserialize_WorkStarted_ReadsUnits()
    {
        // Arrange
        var body = BodyOf("CFX.Production.WorkStarted");

        // Act
        var result = CfxTypedBody.Deserialize<WorkStarted>(body);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal("SUNJSONG-DEMO-SN624", result.Value.PrimaryIdentifier);
        Assert.Equal(1, result.Value.UnitCount);
        Assert.Equal("CIRCUIT1", Assert.Single(result.Value.Units).PositionName);
    }

    [Fact]
    public void Deserialize_MagazineArrived_ReadsEveryHermesUnit()
    {
        // Arrange
        var body = BodyOf("CFX.Production.Hermes.MagazineArrived");

        // Act
        var result = CfxTypedBody.Deserialize<MagazineArrived>(body);

        // Assert
        Assert.False(result.IsError);
        Assert.Equal("SUNJSONG-MAGAZINE-002", result.Value.MagazineData.MagazineId);
        Assert.Equal(24, result.Value.MagazineData.HermesUnits.Count);
    }

    [Fact]
    public void Deserialize_UnitsInspected_ResolvesNestedMeasurementDiscriminator()
    {
        // Arrange — the nested $type is what makes type resolution unavoidable for typed access.
        var body = BodyOf("CFX.Production.TestAndInspection.UnitsInspected");

        // Act
        var result = CfxTypedBody.Deserialize<UnitsInspected>(body);

        // Assert
        Assert.False(result.IsError);

        var measurement = Assert.Single(
            Assert.Single(Assert.Single(result.Value.InspectedUnits).Inspections).Measurements);

        Assert.Equal(
            "CFX.Structures.SolderPasteInspection.SolderPasteMeasurement",
            measurement.GetType().FullName);
    }

    [Fact]
    public void Deserialize_TypeFromAnotherAssembly_IsRejected()
    {
        // Arrange — the binder must refuse a $type outside the CFX SDK assembly rather than
        // constructing whatever the payload names.
        const string hostile = """
            {
              "$type": "System.IO.FileInfo, System.Private.CoreLib",
              "OldState": "1400"
            }
            """;

        // Act
        var result = CfxTypedBody.Deserialize<StationStateChanged>(hostile);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Cfx.Body.DeserializationFailed", result.FirstError.Code);
    }

    [Fact]
    public void Deserialize_TypeOutsideCfxNamespace_IsRejected()
    {
        // Arrange
        const string hostile = """
            {
              "$type": "System.Diagnostics.Process",
              "OldState": "1400"
            }
            """;

        // Act
        var result = CfxTypedBody.Deserialize<StationStateChanged>(hostile);

        // Assert
        Assert.True(result.IsError);
        Assert.Equal("Cfx.Body.DeserializationFailed", result.FirstError.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_BlankBody_ReturnsEmptyError(string body)
    {
        var result = CfxTypedBody.Deserialize<StationStateChanged>(body);

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Body.Empty", result.FirstError.Code);
    }

    [Fact]
    public void Deserialize_MalformedJson_ReturnsDeserializationError()
    {
        var result = CfxTypedBody.Deserialize<StationStateChanged>("{\"OldState\":");

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Body.DeserializationFailed", result.FirstError.Code);
    }

    [Fact]
    public void Deserialize_JsonNullLiteral_ReturnsNullError()
    {
        var result = CfxTypedBody.Deserialize<StationStateChanged>("null");

        Assert.True(result.IsError);
        Assert.Equal("Cfx.Body.Null", result.FirstError.Code);
    }

    private static string BodyOf(string messageName)
    {
        var json = CfxPayloadCodec.Decode(CfxFixtures.GzipPayload(messageName)).Value;
        return CfxEnvelopeReader.Read(json).Value.MessageBodyJson;
    }
}
