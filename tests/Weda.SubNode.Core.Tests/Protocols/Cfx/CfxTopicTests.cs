using Weda.SubNode.Core.Protocols.Cfx;

using Xunit;

namespace Weda.SubNode.Core.Tests.Protocols.Cfx;

public class CfxTopicTests
{
    [Theory]
    [InlineData("SUNJSONG.SLD880A.0001", "SUNJSONG/SLD880A/0001")]
    [InlineData("SUNJSONG.SBL820S.0001", "SUNJSONG/SBL820S/0001")]
    [InlineData("Vendor.Device", "Vendor/Device")]
    [InlineData("  SUNJSONG.SLD880A.0001  ", "SUNJSONG/SLD880A/0001")]
    public void ToTopicPrefix_ReplacesDotsWithSlashes(string handle, string expected) =>
        Assert.Equal(expected, CfxTopic.ToTopicPrefix(handle));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToTopicPrefix_BlankHandle_Throws(string? handle) =>
        Assert.Throws<ArgumentException>(() => CfxTopic.ToTopicPrefix(handle!));

    [Fact]
    public void SubscriptionFilterFor_CoversEveryMessageOfOneEndpoint() =>
        Assert.Equal(
            "SUNJSONG/SLD880A/0001/CFX/#",
            CfxTopic.SubscriptionFilterFor("SUNJSONG.SLD880A.0001"));

    [Fact]
    public void SubscriptionFilterFor_HonoursCustomRoot() =>
        Assert.Equal(
            "SUNJSONG/SLD880A/0001/IPCCFX/#",
            CfxTopic.SubscriptionFilterFor("SUNJSONG.SLD880A.0001", "IPCCFX"));

    [Theory]
    [InlineData(1, "+/CFX/#")]
    [InlineData(3, "+/+/+/CFX/#")]
    [InlineData(4, "+/+/+/+/CFX/#")]
    public void SubscriptionFilterForAnyEndpoint_BuildsWildcardPrefix(int segments, string expected) =>
        Assert.Equal(expected, CfxTopic.SubscriptionFilterForAnyEndpoint(segments));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SubscriptionFilterForAnyEndpoint_NonPositiveSegments_Throws(int segments) =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => CfxTopic.SubscriptionFilterForAnyEndpoint(segments));

    [Theory]
    // The namespace path below the root varies from zero to two segments deep, so handle extraction
    // must not assume a fixed topic depth.
    [InlineData("SUNJSONG/SLD880A/0001/CFX/EndpointConnected", "SUNJSONG.SLD880A.0001")]
    [InlineData("SUNJSONG/SLD880A/0001/CFX/ResourcePerformance/StationOnline", "SUNJSONG.SLD880A.0001")]
    [InlineData("SUNJSONG/SBL820S/0001/CFX/Production/Hermes/MagazineArrived", "SUNJSONG.SBL820S.0001")]
    [InlineData("SUNJSONG/SLD880A/0001/CFX/Sensor/Identification/IdentifiersRead", "SUNJSONG.SLD880A.0001")]
    public void TryParseHandle_ExtractsHandleRegardlessOfTopicDepth(string topic, string expected)
    {
        Assert.True(CfxTopic.TryParseHandle(topic, out var handle));
        Assert.Equal(expected, handle);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no/root/segment/here")]
    [InlineData("CFX/EndpointConnected")]
    public void TryParseHandle_WithoutUsableHandle_ReturnsFalse(string? topic)
    {
        Assert.False(CfxTopic.TryParseHandle(topic, out var handle));
        Assert.Equal(string.Empty, handle);
    }
}
