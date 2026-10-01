using System.Text.Json;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Context;
using Xunit;

namespace Weda.SubNode.Core.Tests.Configuration;

/// <summary>
/// Tests for the SubNode-level settings section (subNodeSettings.record) on devicecfg (task #54505).
/// Recording configuration now lives here, remotely configurable via the DeviceConfig desired
/// pipeline. subNodeSettings is a top-level container parallel to subNode / deviceConfigs, and
/// unlike subNode it must NOT be stripped from the reported round-trip.
/// </summary>
public class SubNodeSettingsContractTests
{
    private static SubNodeConfigUpdateMessage Deserialize(string json) =>
        JsonSerializer.Deserialize<SubNodeConfigUpdateMessage>(json)!;

    [Fact]
    public void DesiredDeviceCfg_WithSubNodeSettingsRecord_DeserializesRecord()
    {
        var message = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{
              "subNode":{"name":"TestNode"},
              "subNodeSettings":{"record":{"retentionDays":30,"maxStorageSizeMb":2048}},
              "deviceConfigs":{}
            }}}}}
            """);

        var record = message.Data!.Cfg!.Desired!.SubNodeDeviceConfig!.SubNodeSettings!.Record!;
        record.RetentionDays.ShouldBe(30);
        record.MaxStorageSizeMb.ShouldBe(2048);
    }

    [Fact]
    public void SubNodeRecordConfig_TryValidate_RejectsOutOfRange()
    {
        var record = new SubNodeRecordConfigDto { RetentionDays = 999 };

        record.TryValidate(out var error).ShouldBeFalse();
        error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public void ReportedDeviceCfg_PreservesSubNodeSettings_NotStrippedLikeSubNode()
    {
        // The reported round-trip drops the incoming `subNode` and re-emits it from the
        // device's own identity. subNodeSettings is a distinct top-level key and must
        // survive verbatim — this is what makes it a usable remote-config channel.
        using var rawDoc = JsonDocument.Parse("""
            {
              "subNode":{"name":"IncomingShouldBeDropped"},
              "subNodeSettings":{"record":{"retentionDays":30,"maxStorageSizeMb":2048}},
              "deviceConfigs":{}
            }
            """);

        var reported = new SubNodeReportedConfigSections();
        var subNodeInfo = new SubNodeInfo { Name = "RealIdentity" };

        reported.SetRawDeviceCfgWithMessage(
            rawDoc.RootElement,
            new ConfigUpdateMessageDto { Status = "success" },
            subNodeInfo);

        var root = reported.RawDeviceCfg!.Value;

        // subNode is re-emitted from identity, not echoed from the incoming payload
        root.GetProperty("subNode").GetProperty("name").GetString().ShouldBe("RealIdentity");

        // subNodeSettings survives verbatim
        root.TryGetProperty("subNodeSettings", out var settings).ShouldBeTrue();
        settings.GetProperty("record").GetProperty("retentionDays").GetInt32().ShouldBe(30);
        settings.GetProperty("record").GetProperty("maxStorageSizeMb").GetInt32().ShouldBe(2048);
    }

}
