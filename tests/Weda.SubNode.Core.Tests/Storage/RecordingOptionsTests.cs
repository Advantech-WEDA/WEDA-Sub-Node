using Shouldly;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

/// <summary>
/// Tests for <see cref="RecordingOptions"/>, <see cref="RecordingStorageSettings"/> and
/// <see cref="RecordingOptionsProvider"/>.
/// </summary>
public class RecordingOptionsTests
{
    /// <summary>
    /// The ring-buffer defaults. Recordings are evicted once they age past 7 days or the
    /// recording directory exceeds 1 GB.
    /// </summary>
    [Fact]
    public void Defaults_MatchTheConfiguredFifoPolicy()
    {
        var options = new RecordingOptions();

        options.MaxStorageSizeMb.ShouldBe(1024);
        options.RetentionDays.ShouldBe(7);
    }

    /// <summary>
    /// Cleanup must sweep several times a day. A daily sweep is anchored to process start, which
    /// makes retention depend on uptime and leaves a device that restarts often with no sweep at
    /// all beyond the one at boot (work item 53692).
    /// </summary>
    [Fact]
    public void DefaultCleanupInterval_IsWellUnderADay()
    {
        var options = new RecordingOptions();

        options.CleanupInterval.ShouldNotBeNull();
        options.CleanupInterval!.Value.ShouldBeLessThanOrEqualTo(TimeSpan.FromHours(6));
    }

    [Fact]
    public void CleanupInterval_WhenZero_IsNull()
    {
        new RecordingOptions { CleanupIntervalMinutes = 0 }.CleanupInterval.ShouldBeNull();
    }

    [Fact]
    public void StorageDirectory_IsConfigurable()
    {
        var options = new RecordingOptions { StorageDirectory = "/var/lib/weda/recording" };

        options.StorageDirectory.ShouldBe("/var/lib/weda/recording");
        Should.NotThrow(options.Validate);
    }

    [Fact]
    public void Validate_EmptyStorageDirectory_Throws()
    {
        var options = new RecordingOptions { StorageDirectory = "  " };

        Should.Throw<ArgumentException>(options.Validate);
    }

    [Theory]
    [InlineData(-1, 128, 128)]
    [InlineData(366, 128, 128)]
    [InlineData(7, -1, 128)]
    [InlineData(7, 102401, 128)]
    [InlineData(7, 128, -1)]
    [InlineData(7, 128, 102401)]
    public void StorageSettings_Validate_OutOfRange_Throws(int retentionDays, int maxStorageSizeMb, int minFreeDiskSpaceMb)
    {
        var settings = new RecordingStorageSettings(retentionDays, maxStorageSizeMb, minFreeDiskSpaceMb);

        Should.Throw<ArgumentOutOfRangeException>(settings.Validate);
    }

    [Fact]
    public void GetRetentionCutoff_SubtractsTheRetentionWindow()
    {
        var settings = new RecordingStorageSettings(RetentionDays: 7, MaxStorageSizeMb: 1024, MinFreeDiskSpaceMb: 128);
        var now = new DateTimeOffset(2026, 9, 16, 8, 0, 0, TimeSpan.Zero);

        settings.GetRetentionCutoff(now).ShouldBe(new DateTimeOffset(2026, 9, 9, 8, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void GetRetentionCutoff_RetentionDisabled_IsNull()
    {
        var settings = new RecordingStorageSettings(RetentionDays: 0, MaxStorageSizeMb: 128, MinFreeDiskSpaceMb: 128);

        settings.GetRetentionCutoff(DateTimeOffset.UtcNow).ShouldBeNull();
    }

    [Fact]
    public void WithStorageSettings_LeavesTheOriginalUntouched()
    {
        var original = new RecordingOptions { RetentionDays = 7, MaxStorageSizeMb = 128, FlushIntervalSeconds = 42 };

        var updated = original.WithStorageSettings(new RecordingStorageSettings(RetentionDays: 3, MaxStorageSizeMb: 64, MinFreeDiskSpaceMb: 32));

        original.RetentionDays.ShouldBe(7);
        original.MaxStorageSizeMb.ShouldBe(128);
        updated.RetentionDays.ShouldBe(3);
        updated.MaxStorageSizeMb.ShouldBe(64);
        updated.MinFreeDiskSpaceMb.ShouldBe(32);
        updated.FlushIntervalSeconds.ShouldBe(42, "unrelated settings survive a storage-limit update");
    }

    [Fact]
    public void Provider_Update_PublishesANewInstance()
    {
        var provider = new RecordingOptionsProvider(new RecordingOptions { RetentionDays = 7 });
        var before = provider.Current;

        provider.UpdateStorageSettings(new RecordingStorageSettings(RetentionDays: 3, MaxStorageSizeMb: 128, MinFreeDiskSpaceMb: 128));

        provider.Current.RetentionDays.ShouldBe(3);
        before.RetentionDays.ShouldBe(7, "a snapshot already taken by a reader does not change underneath it");
    }

    [Fact]
    public void Provider_Update_RejectsOutOfRangeSettings()
    {
        var provider = new RecordingOptionsProvider(new RecordingOptions());

        Should.Throw<ArgumentOutOfRangeException>(() =>
            provider.UpdateStorageSettings(new RecordingStorageSettings(RetentionDays: 400, MaxStorageSizeMb: 128, MinFreeDiskSpaceMb: 128)));

        provider.Current.RetentionDays.ShouldBe(7, "a rejected update leaves the previous policy in force");
    }

    [Fact]
    public void CleanupResult_Add_SumsEveryPass()
    {
        var combined = new RecordingCleanupResult(3, 2, 100, 1) + new RecordingCleanupResult(4, 1, 50, 0);

        combined.ShouldBe(new RecordingCleanupResult(7, 3, 150, 1));
    }
}
