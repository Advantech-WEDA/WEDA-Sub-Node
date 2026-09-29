using Microsoft.Extensions.Configuration;
using Shouldly;
using Weda.SubNode.Host.Context;
using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// The RecordingOptions startup seed is bound from devicecfg's SubNode-level section
/// (DeviceConfig:SubNodeSettings:Record), not from systemcfg (task #54506).
///
/// Regression guard: the seed was previously bound via BindConfiguration("Record") with no
/// section prefix, so GetSection("Record") never existed under the real DeviceConfig/SystemConfig
/// roots and the context silently fell back to a default RecordingOptions — a cloud-pushed or
/// file-authored retention value was quietly ignored.
/// </summary>
public class RecordingOptionsSeedBindingTests
{
    private static IConfiguration ConfigWith(params (string key, string value)[] entries)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in entries)
            dict[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void RecordingOptions_SeededFromDeviceCfgSubNodeSettingsRecord()
    {
        var configuration = ConfigWith(
            ("DeviceConfig:SubNodeSettings:Record:Enabled", "true"),
            ("DeviceConfig:SubNodeSettings:Record:RetentionDays", "30"),
            ("DeviceConfig:SubNodeSettings:Record:MaxStorageSizeMb", "2048"));

        using var context = new WedaApplicationContext(options =>
        {
            options.Configuration = configuration;
            options.DeviceOptions.EnableRecording = true;
        });

        context.RecordingOptions.ShouldNotBeNull();
        context.RecordingOptions!.RetentionDays.ShouldBe(30);
        context.RecordingOptions.MaxStorageSizeMb.ShouldBe(2048);
    }

    [Fact]
    public void RecordingOptions_NotSeededFromSystemCfgRecord()
    {
        // systemcfg is policy-locked as non-remote-configurable; its Record must no longer seed.
        var configuration = ConfigWith(
            ("SystemConfig:Record:RetentionDays", "99"),
            ("DeviceConfig:SubNodeSettings:Record:Enabled", "true"));

        using var context = new WedaApplicationContext(options =>
        {
            options.Configuration = configuration;
            options.DeviceOptions.EnableRecording = true;
        });

        context.RecordingOptions.ShouldNotBeNull();
        context.RecordingOptions!.RetentionDays.ShouldBe(7); // default, NOT the systemcfg 99
    }
}
