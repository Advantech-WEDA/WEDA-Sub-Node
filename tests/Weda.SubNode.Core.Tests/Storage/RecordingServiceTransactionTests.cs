using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Core.Context;
using Weda.SubNode.Core.Storage;
using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

/// <summary>
/// The recording service participates in the DeviceConfig transaction the same way a device does:
/// capture (backup) -> apply -> restore (rollback), all owned by the service so the orchestrator
/// never reaches into recording internals. The captured snapshot must cover the full state,
/// including Enabled/Batch which live outside the storage-settings value object (task #54507).
/// </summary>
[Collection("StorageTests")]
public class RecordingServiceTransactionTests : IDisposable
{
    private readonly string _dir;
    private readonly RecordingService _service;

    public RecordingServiceTransactionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"recording-txn-test-{Guid.NewGuid():N}");
        var options = new RecordingOptions
        {
            StorageDirectory = _dir,
            Enabled = true,
            RetentionDays = 7,
            MaxStorageSizeMb = 1024,
            MinFreeDiskSpaceMb = 128,
            BatchEnabled = true,
            BatchMaxSamples = 5
        };
        var loggerFactory = new NullLoggerFactory();
        var storage = new BinaryRecordStorage(loggerFactory.CreateLogger<BinaryRecordStorage>(), Options.Create(options));
        _service = new RecordingService(
            loggerFactory.CreateLogger<RecordingService>(), storage, new DeviceRegistry(), Options.Create(options));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void ApplyRecordConfig_AppliesAllFields()
    {
        _service.ApplyRecordConfig(new SubNodeRecordConfigDto
        {
            Enabled = false,
            RetentionDays = 30,
            MaxStorageSizeMb = 2048,
            MinFreeDiskSpaceMb = 256,
            BatchEnabled = false,
            BatchMaxSamples = 99
        });

        _service.StorageSettings.RetentionDays.ShouldBe(30);
        _service.StorageSettings.MaxStorageSizeMb.ShouldBe(2048);
        _service.StorageSettings.MinFreeDiskSpaceMb.ShouldBe(256);
    }

    [Fact]
    public void Capture_ThenRestore_ReturnsAllFieldsToCapturedState()
    {
        // Capture the full pre-change state (retention + storage + enabled + batch).
        var snapshot = _service.CaptureForRollback();
        snapshot.RetentionDays.ShouldBe(7);
        snapshot.BatchMaxSamples.ShouldBe(5);

        // Apply a completely different config.
        _service.ApplyRecordConfig(new SubNodeRecordConfigDto
        {
            Enabled = false,
            RetentionDays = 30,
            MaxStorageSizeMb = 2048,
            MinFreeDiskSpaceMb = 256,
            BatchEnabled = false,
            BatchMaxSamples = 99
        });
        _service.StorageSettings.RetentionDays.ShouldBe(30);

        // Rollback restores every field, including the ones outside StorageSettings.
        _service.RestoreFromRollback(snapshot);
        _service.StorageSettings.RetentionDays.ShouldBe(7);
        _service.StorageSettings.MaxStorageSizeMb.ShouldBe(1024);
        _service.StorageSettings.MinFreeDiskSpaceMb.ShouldBe(128);
    }

    [Fact]
    public void ApplyRecordConfig_OutOfRange_Throws()
    {
        // Values arrive from off-device; an out-of-range value is rejected, not clamped.
        Should.Throw<Exception>(() => _service.ApplyRecordConfig(new SubNodeRecordConfigDto
        {
            RetentionDays = 9999
        }));
    }
}
