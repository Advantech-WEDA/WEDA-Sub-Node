using System.Text.Json;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Xunit;

namespace Weda.SubNode.Core.Tests.Configuration;

/// <summary>
/// Tests for config reset detection (task #44829).
/// An explicit JSON null on devicecfg (or its DeviceConfigs) is a reset request;
/// an absent key or an empty object keeps the existing no-update behavior.
/// </summary>
public class ConfigResetDetectionTests
{
    private static SubNodeConfigUpdateMessage Deserialize(string json) =>
        JsonSerializer.Deserialize<SubNodeConfigUpdateMessage>(json)!;

    [Fact]
    public void IsDeviceCfgReset_DeviceCfgExplicitNull_ShouldBeTrue()
    {
        var message = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":null}}}}
            """);

        message.Data!.Cfg!.Desired!.IsDeviceCfgReset.ShouldBeTrue();
    }

    [Fact]
    public void IsDeviceCfgReset_DeviceConfigsExplicitNull_ShouldBeTrue()
    {
        var message = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{"SubNode":{"Name":"TestDevice"},"DeviceConfigs":null}}}}}
            """);

        message.Data!.Cfg!.Desired!.IsDeviceCfgReset.ShouldBeTrue();
    }

    [Fact]
    public void IsDeviceCfgReset_DeviceCfgAbsent_ShouldBeFalse()
    {
        var message = Deserialize("""
            {"data":{"cfg":{"desired":{}}}}
            """);

        message.Data!.Cfg!.Desired!.IsDeviceCfgReset.ShouldBeFalse();
    }

    [Fact]
    public void IsDeviceCfgReset_DeviceCfgEmptyObject_ShouldBeFalse()
    {
        var message = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{}}}}}
            """);

        message.Data!.Cfg!.Desired!.IsDeviceCfgReset.ShouldBeFalse();
    }

    [Fact]
    public void IsDeviceCfgReset_DeviceConfigsEmptyObject_ShouldBeFalse()
    {
        var message = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{"DeviceConfigs":{}}}}}}
            """);

        message.Data!.Cfg!.Desired!.IsDeviceCfgReset.ShouldBeFalse();
    }

    [Fact]
    public void IsDeviceCfgReset_DeviceConfigsWithContent_ShouldBeFalse()
    {
        var message = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{"DeviceConfigs":{"MyDevice":{"Enabled":true}}}}}}}
            """);

        message.Data!.Cfg!.Desired!.IsDeviceCfgReset.ShouldBeFalse();
    }

    [Fact]
    public void DesiredWithNullDeviceCfg_RoundTrip_ShouldPreserveExplicitNull()
    {
        var message = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":null}}}}
            """);

        var json = JsonSerializer.Serialize(message.Data!.Cfg!.Desired);
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("devicecfg", out var deviceCfg).ShouldBeTrue();
        deviceCfg.ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
