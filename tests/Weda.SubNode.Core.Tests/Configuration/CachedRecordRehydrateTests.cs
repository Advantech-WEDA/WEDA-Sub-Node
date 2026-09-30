using System.Text.Json;
using NSubstitute;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Core.Configuration;
using Xunit;

namespace Weda.SubNode.Core.Tests.Configuration;

/// <summary>
/// On startup the node-level record is re-applied from the devicecfg cache after the per-device
/// rehydrate, so a cloud-pushed record survives a restart (task #54509). This is the node-level
/// counterpart to ApplyCachedConfiguration, which only replays per-device sensors/periods.
/// </summary>
public class CachedRecordRehydrateTests
{
    private static SubNodeConfigUpdateMessage Deserialize(string json) =>
        JsonSerializer.Deserialize<SubNodeConfigUpdateMessage>(json)!;

    [Fact]
    public void ApplyCachedRecord_WithRecordInCache_AppliesToService()
    {
        var cached = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{
              "subNodeSettings":{"record":{"retentionDays":21,"maxStorageSizeMb":777}}
            }}}}}
            """);
        var recordingService = Substitute.For<IRecordingService>();

        var applied = ConfigurationUpdateHelper.ApplyCachedRecord(cached, recordingService);

        applied.ShouldBeTrue();
        recordingService.Received(1).ApplyRecordConfig(Arg.Is<SubNodeRecordConfigDto>(
            r => r.RetentionDays == 21 && r.MaxStorageSizeMb == 777));
    }

    [Fact]
    public void ApplyCachedRecord_NoRecordInCache_DoesNothing()
    {
        var cached = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{
              "deviceConfigs":{"device-1":{"enabled":true}}
            }}}}}
            """);
        var recordingService = Substitute.For<IRecordingService>();

        var applied = ConfigurationUpdateHelper.ApplyCachedRecord(cached, recordingService);

        applied.ShouldBeFalse();
        recordingService.DidNotReceive().ApplyRecordConfig(Arg.Any<SubNodeRecordConfigDto>());
    }

    [Fact]
    public void ApplyCachedRecord_InvalidRecordInCache_DoesNotApply()
    {
        // A cached record that is out of range is not applied (defensive: cache could be edited).
        var cached = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{
              "subNodeSettings":{"record":{"retentionDays":9999}}
            }}}}}
            """);
        var recordingService = Substitute.For<IRecordingService>();

        var applied = ConfigurationUpdateHelper.ApplyCachedRecord(cached, recordingService);

        applied.ShouldBeFalse();
        recordingService.DidNotReceive().ApplyRecordConfig(Arg.Any<SubNodeRecordConfigDto>());
    }

    /// <summary>
    /// Wiring guard: the context calls the node-level record rehydrate on startup, after the
    /// per-device rehydrate. Without this, the helper is correct but never invoked.
    /// </summary>
    [Fact]
    public void Context_OnStartup_RehydratesRecordFromCache()
    {
        var cached = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{
              "subNodeSettings":{"record":{"retentionDays":21,"maxStorageSizeMb":777,"minFreeDiskSpaceMb":64}}
            }}}}}
            """);

        var cache = Substitute.For<Weda.SubNode.Abstractions.Storage.IConfigurationCache>();
        cache.ExistsAsync(Weda.SubNode.Abstractions.Cloud.Subscriptions.SubscriptionTypes.DeviceConfig)
            .Returns(true);
        cache.GetRawConfigurationAsync(Weda.SubNode.Abstractions.Cloud.Subscriptions.SubscriptionTypes.DeviceConfig)
            .Returns(cached);

        var recordingService = Substitute.For<IRecordingService>();
        // The cleanup scheduler reads StorageSettings on startup; give the mock a value so it
        // does not NRE before the rehydrate wiring under test runs.
        recordingService.StorageSettings.Returns(
            new Weda.SubNode.Abstractions.Storage.Recordings.RecordingStorageSettings(
                RetentionDays: 7, MaxStorageSizeMb: 1024, MinFreeDiskSpaceMb: 128));

        using var context = new Weda.SubNode.Host.Context.WedaApplicationContext(options =>
        {
            options.ConfigurationCache = cache;
            options.RecordingService = recordingService;
            options.DeviceOptions.EnableRecording = true;
        });

        recordingService.Received(1).ApplyRecordConfig(Arg.Is<SubNodeRecordConfigDto>(
            r => r.RetentionDays == 21 && r.MaxStorageSizeMb == 777));
    }

    /// <summary>
    /// The startup sweep must run with the cached cloud record, not the base devicecfg values.
    /// A sweep that starts before the rehydrate uses the base retention, and on every restart
    /// it irreversibly deletes files the cloud said to keep (base 7 days, cloud 30 days: the
    /// 7-30 day files go).
    /// </summary>
    [Fact]
    public async Task Context_OnStartup_FirstSweepUsesTheCachedRecord()
    {
        var cached = Deserialize("""
            {"data":{"cfg":{"desired":{"devicecfg":{
              "subNodeSettings":{"record":{"retentionDays":30,"maxStorageSizeMb":1024,"minFreeDiskSpaceMb":128}}
            }}}}}
            """);

        var cache = Substitute.For<Weda.SubNode.Abstractions.Storage.IConfigurationCache>();
        cache.ExistsAsync(Weda.SubNode.Abstractions.Cloud.Subscriptions.SubscriptionTypes.DeviceConfig)
            .Returns(true);
        cache.GetRawConfigurationAsync(Weda.SubNode.Abstractions.Cloud.Subscriptions.SubscriptionTypes.DeviceConfig)
            .Returns(cached);

        // Behave like the real service: the base limits are in force until the cached record
        // is applied.
        var settings = new Weda.SubNode.Abstractions.Storage.Recordings.RecordingStorageSettings(
            RetentionDays: 7, MaxStorageSizeMb: 1024, MinFreeDiskSpaceMb: 128);
        var recordingService = Substitute.For<IRecordingService>();
        recordingService.StorageSettings.Returns(_ => settings);
        recordingService
            .When(s => s.ApplyRecordConfig(Arg.Any<SubNodeRecordConfigDto>()))
            .Do(ci => settings = ci.Arg<SubNodeRecordConfigDto>().ToStorageSettings());

        var firstCutoff = new TaskCompletionSource<DateTimeOffset>(TaskCreationOptions.RunContinuationsAsynchronously);
        recordingService.CleanupAsync(Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                firstCutoff.TrySetResult(ci.Arg<DateTimeOffset>());
                return Task.FromResult(Weda.SubNode.Abstractions.Storage.Recordings.RecordingCleanupResult.Empty);
            });

        using var context = new Weda.SubNode.Host.Context.WedaApplicationContext(options =>
        {
            options.ConfigurationCache = cache;
            options.RecordingService = recordingService;
            options.DeviceOptions.EnableRecording = true;
        });

        var cutoff = await firstCutoff.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // 30-day retention puts the cutoff about 30 days back; the base 7 days would put it
        // about 7 days back and delete everything in between.
        cutoff.ShouldBeLessThan(DateTimeOffset.UtcNow.AddDays(-29));
    }
}
