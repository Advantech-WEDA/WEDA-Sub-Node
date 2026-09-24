using Microsoft.Extensions.Configuration;
using Shouldly;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Host.Context;
using Xunit;

namespace Weda.SubNode.Core.Tests;

/// <summary>
/// The context's RecordingOptions is a live view backed by the RecordingOptionsProvider (the
/// single source of truth), not a frozen startup snapshot. A runtime update — the only way
/// recording is configured now that code-level injection is gone — is visible through it (#54511).
/// </summary>
public class RecordingOptionsProviderSourceTests
{
    [Fact]
    public void ContextRecordingOptions_ReflectsRuntimeUpdate()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DeviceConfig:SubNodeSettings:Record:RetentionDays"] = "7"
            })
            .Build();

        using var context = new WedaApplicationContext(options =>
        {
            options.Configuration = configuration;
            options.DeviceOptions.EnableRecording = true;
        });

        context.RecordingOptions!.RetentionDays.ShouldBe(7);

        // Apply a runtime update through the service (as the transaction does).
        context.RecordingService!.ApplyRecordConfig(new Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts.SubNodeRecordConfigDto
        {
            RetentionDays = 30,
            MaxStorageSizeMb = 1024,
            MinFreeDiskSpaceMb = 128
        });

        // The context view reflects the new value, not the startup snapshot.
        context.RecordingOptions!.RetentionDays.ShouldBe(30);
    }
}
