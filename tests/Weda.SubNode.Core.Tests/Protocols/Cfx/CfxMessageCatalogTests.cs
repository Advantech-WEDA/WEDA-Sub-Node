using Weda.SubNode.Core.Protocols.Cfx;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class CfxMessageCatalogTests
{
    [Fact]
    public void SupportedMessageNames_MatchesTheCapturedFixtureSet()
    {
        // The catalogue and the captured payloads must not drift apart: a name in the catalogue with
        // no capture behind it is untested, and a capture with no catalogue entry warns at startup.
        Assert.Equal(
            CfxFixtures.MessageNames().OrderBy(n => n, StringComparer.Ordinal),
            CfxMessageCatalog.SupportedMessageNames.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void SupportedMessageNames_HasNoDuplicates() =>
        Assert.Equal(
            CfxMessageCatalog.SupportedMessageNames.Count,
            CfxMessageCatalog.SupportedMessageNames.Distinct().Count());

    [Theory]
    [InlineData("CFX.EndpointConnected")]
    [InlineData("cfx.endpointconnected")]
    [InlineData("CFX.Production.Hermes.MagazineArrived")]
    public void IsSupported_KnownName_ReturnsTrue(string messageName) =>
        Assert.True(CfxMessageCatalog.IsSupported(messageName));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("CFX.Production.WorkStartedTypo")]
    [InlineData("CFX.Maintenance.MaintenanceRecorded")]
    public void IsSupported_UnknownName_ReturnsFalse(string? messageName) =>
        Assert.False(CfxMessageCatalog.IsSupported(messageName));

    [Theory]
    [InlineData("CFX.EndpointConnected", "cfx_endpoint_connected")]
    [InlineData("CFX.ResourcePerformance.StationStateChanged", "cfx_station_state_changed")]
    [InlineData("CFX.Production.Hermes.MagazineArrived", "cfx_magazine_arrived")]
    [InlineData("CFX.Sensor.Identification.IdentifiersRead", "cfx_identifiers_read")]
    [InlineData("CFX.Production.LoadingAndUnloading.UnitsLoaded", "cfx_units_loaded")]
    [InlineData("CFX.Production.TestAndInspection.UnitsInspected", "cfx_units_inspected")]
    public void ToSensorName_DerivesSnakeCaseFromLeafName(string messageName, string expected) =>
        Assert.Equal(expected, CfxMessageCatalog.ToSensorName(messageName));

    [Theory]
    // A run of capitals is one word, so "TransactionID" must not degrade into "transaction_i_d".
    [InlineData("CFX.Fake.TransactionID", "cfx_transaction_id")]
    [InlineData("CFX.Fake.HTTPRequest", "cfx_http_request")]
    [InlineData("CFX.Fake.Simple", "cfx_simple")]
    public void ToSensorName_TreatsCapitalRunsAsOneWord(string messageName, string expected) =>
        Assert.Equal(expected, CfxMessageCatalog.ToSensorName(messageName));

    [Fact]
    public void ToSensorName_EveryCatalogueEntry_ProducesAUniqueName()
    {
        var names = CfxMessageCatalog.SupportedMessageNames
            .Select(CfxMessageCatalog.ToSensorName)
            .ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ToSensorName_BlankName_Throws(string? messageName) =>
        Assert.Throws<ArgumentException>(() => CfxMessageCatalog.ToSensorName(messageName!));

    [Theory]
    [InlineData("CFX.EndpointConnected", "EndpointConnected")]
    [InlineData("CFX.ResourcePerformance.StationOnline", "ResourcePerformance/StationOnline")]
    [InlineData("CFX.Production.Hermes.MagazineArrived", "Production/Hermes/MagazineArrived")]
    public void ToRelativeTopicPath_MirrorsTheCfxNamespace(string messageName, string expected) =>
        Assert.Equal(expected, CfxMessageCatalog.ToRelativeTopicPath(messageName));

    [Theory]
    [InlineData("Production.WorkStarted")]
    [InlineData("NotCfx.Production.WorkStarted")]
    [InlineData("")]
    public void ToRelativeTopicPath_UnqualifiedName_Throws(string messageName) =>
        Assert.Throws<ArgumentException>(() => CfxMessageCatalog.ToRelativeTopicPath(messageName));
}
