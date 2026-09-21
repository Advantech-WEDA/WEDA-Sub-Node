using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Weda.SubNode.Abstractions.Storage.Recordings;
using Weda.SubNode.Core.Context;
using Weda.SubNode.Core.Storage;
using Xunit;

namespace Weda.SubNode.Core.Tests.Storage;

/// <summary>
/// Tests for <see cref="RecordingCleanupScheduler"/>.
/// </summary>
/// <remarks>
/// The deletion predicate is covered by <see cref="BinaryRecordStorageTests"/>; what is covered
/// here is the seam that actually failed in the field - when cleanup runs, and against which
/// retention value. Work item 53692: expired recordings survived on the device because the only
/// sweep ever scheduled ran at process start, before the files existed.
/// </remarks>
public class RecordingCleanupSchedulerTests : IDisposable
{
    private readonly ILoggerFactory _loggerFactory = new NullLoggerFactory();
    private readonly string _testDirectory;
    private readonly RecordingOptions _options;
    private readonly RecordingOptionsProvider _optionsProvider;
    private readonly BinaryRecordStorage _storage;
    private readonly RecordingService _service;

    public RecordingCleanupSchedulerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"cleanup-scheduler-test-{Guid.NewGuid():N}");
        _options = new RecordingOptions
        {
            StorageDirectory = _testDirectory,
            RetentionDays = 7,
            // Isolate the age-based policy from the ring-buffer one unless a test opts in.
            MaxStorageSizeMb = 0,
            MinFreeDiskSpaceMb = 0
        };
        _optionsProvider = new RecordingOptionsProvider(_options);
        _storage = new BinaryRecordStorage(_loggerFactory.CreateLogger<BinaryRecordStorage>(), _optionsProvider);
        _service = new RecordingService(
            _loggerFactory.CreateLogger<RecordingService>(), _storage, new DeviceRegistry(), _optionsProvider);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
        _loggerFactory.Dispose();
        GC.SuppressFinalize(this);
    }

    private RecordingCleanupScheduler CreateScheduler(TimeSpan? interval = null, IDynamicRecordStorage? dynamicStorage = null)
        => new(
            _loggerFactory.CreateLogger<RecordingCleanupScheduler>(),
            _service,
            dynamicStorage,
            interval ?? TimeSpan.FromMinutes(RecordingOptions.DefaultCleanupIntervalMinutes));

    /// <summary>
    /// Writes one recording file dated <paramref name="ageInDays"/> days ago.
    /// </summary>
    private async Task<string> WriteRecordingAgedAsync(string sensorId, int ageInDays)
    {
        var date = DateTime.UtcNow.AddDays(-ageInDays);
        var timestamp = new DateTimeOffset(date, TimeSpan.Zero).ToUnixTimeMilliseconds();
        await _storage.WriteAsync(sensorId, 1000, SchemaType.Double, new RecordingDataPoint(timestamp, 42.5, SchemaType.Double));

        var path = Path.Combine(_testDirectory, sensorId, $"{date:yyyy-MM-dd}_1000_double.bin");
        File.Exists(path).ShouldBeTrue("precondition: the aged recording was written");
        return path;
    }

    [Fact]
    public async Task RunOnceAsync_DeletesRecordingsPastRetention()
    {
        // Arrange
        var expired = await WriteRecordingAgedAsync("sensor-expired", ageInDays: 10);
        var fresh = await WriteRecordingAgedAsync("sensor-fresh", ageInDays: 2);
        using var scheduler = CreateScheduler();

        // Act
        var result = await scheduler.RunOnceAsync();

        // Assert
        File.Exists(expired).ShouldBeFalse();
        File.Exists(fresh).ShouldBeTrue();
        result.FilesDeleted.ShouldBe(1);
        result.BytesFreed.ShouldBeGreaterThan(0);
        result.FilesFailed.ShouldBe(0);
    }

    /// <summary>
    /// The regression from work item 53692: recordings copied in after the process started were
    /// never swept, because the one sweep on the schedule had already run at startup.
    /// </summary>
    [Fact]
    public async Task Sweep_AfterStartup_CollectsRecordingsThatArrivedSinceTheLastSweep()
    {
        // Arrange - start with nothing on disk, exactly as a device does after a fresh boot
        using var scheduler = CreateScheduler();
        var startupSweep = await scheduler.RunOnceAsync();
        startupSweep.FilesDeleted.ShouldBe(0, "nothing was on disk when the process started");

        // Expired recordings land afterwards (restored from a backup, copied in by an operator)
        var expired = await WriteRecordingAgedAsync("sensor-expired", ageInDays: 30);

        // Act - the next scheduled sweep, not a restart
        var nextSweep = await scheduler.RunOnceAsync();

        // Assert
        File.Exists(expired).ShouldBeFalse();
        nextSweep.FilesDeleted.ShouldBe(1);
    }

    [Fact]
    public async Task RunOnceAsync_RetentionDisabled_KeepsEveryRecording()
    {
        // Arrange
        _optionsProvider.UpdateStorageSettings(new RecordingStorageSettings(RetentionDays: 0, MaxStorageSizeMb: 0, MinFreeDiskSpaceMb: 0));
        var ancient = await WriteRecordingAgedAsync("sensor-ancient", ageInDays: 4000);
        using var scheduler = CreateScheduler();

        // Act
        var result = await scheduler.RunOnceAsync();

        // Assert
        File.Exists(ancient).ShouldBeTrue();
        result.FilesDeleted.ShouldBe(0);
    }

    /// <summary>
    /// A retention change pushed from the cloud must bite on the next sweep, not the next boot.
    /// </summary>
    [Fact]
    public async Task RunOnceAsync_AfterRetentionChange_UsesTheNewRetention()
    {
        // Arrange - 5 days old: inside the 7-day window the scheduler started with
        var recording = await WriteRecordingAgedAsync("sensor-1", ageInDays: 5);
        using var scheduler = CreateScheduler();

        (await scheduler.RunOnceAsync()).FilesDeleted.ShouldBe(0);
        File.Exists(recording).ShouldBeTrue();

        // Act - an operator tightens retention to 3 days at runtime
        _service.UpdateStorageSettings(new RecordingStorageSettings(RetentionDays: 3, MaxStorageSizeMb: 0, MinFreeDiskSpaceMb: 0));
        var result = await scheduler.RunOnceAsync();

        // Assert
        File.Exists(recording).ShouldBeFalse();
        result.FilesDeleted.ShouldBe(1);
    }

    [Fact]
    public async Task RunOnceAsync_SweepsDynamicStorageWithTheSameCutoff()
    {
        // Arrange
        var dynamicDir = Path.Combine(_testDirectory, "dynamic");
        var dynamicStorage = new DynamicRecordStorage(_loggerFactory.CreateLogger<DynamicRecordStorage>(), dynamicDir);
        var expiredTimestamp = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();
        await dynamicStorage.AppendAsync("sensor-json", expiredTimestamp, new byte[] { 1, 2, 3 }, SchemaType.ApplicationJson);

        Directory.GetFiles(dynamicDir).ShouldNotBeEmpty("precondition: the dynamic recording was written");

        using var scheduler = CreateScheduler(dynamicStorage: dynamicStorage);

        // Act
        var result = await scheduler.RunOnceAsync();

        // Assert - MIME-typed recordings age out under the same policy as slot-based ones
        Directory.GetFiles(dynamicDir).ShouldBeEmpty();
        result.FilesDeleted.ShouldBe(1);
    }

    [Fact]
    public async Task RunOnceAsync_OverSizeLimit_DeletesOldestUntilWithinLimit()
    {
        // Arrange - a 1 MB cap against three ~675 KB daily files
        await WriteRecordingAgedAsync("sensor-1", ageInDays: 3);
        await WriteRecordingAgedAsync("sensor-1", ageInDays: 2);
        await WriteRecordingAgedAsync("sensor-1", ageInDays: 1);
        _optionsProvider.UpdateStorageSettings(new RecordingStorageSettings(RetentionDays: 7, MaxStorageSizeMb: 1, MinFreeDiskSpaceMb: 0));

        using var scheduler = CreateScheduler();

        // Act - no write is in flight, so only the scheduled sweep can enforce the cap
        var result = await scheduler.RunOnceAsync();

        // Assert
        result.FilesDeleted.ShouldBeGreaterThan(0);
        var remaining = Directory.GetFiles(_testDirectory, "*.bin", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
        remaining.ShouldBeLessThanOrEqualTo(1024L * 1024L);
    }

    [Fact]
    public void Start_WithZeroInterval_DoesNotSchedule()
    {
        // Arrange
        using var scheduler = CreateScheduler(interval: TimeSpan.Zero);

        // Act
        scheduler.Start();

        // Assert - a zero interval disables cleanup rather than spinning a timer
        scheduler.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task Start_RunsASweepImmediately()
    {
        // Arrange - what was on disk before the process came up must not wait for the first tick
        var expired = await WriteRecordingAgedAsync("sensor-expired", ageInDays: 10);
        using var scheduler = CreateScheduler(interval: TimeSpan.FromHours(1));

        // Act
        scheduler.Start();

        // Assert
        scheduler.IsRunning.ShouldBeTrue();
        await WaitUntilAsync(() => !File.Exists(expired), TimeSpan.FromSeconds(5));
        File.Exists(expired).ShouldBeFalse();
    }

    [Fact]
    public void Start_CalledTwice_SchedulesOnce()
    {
        using var scheduler = CreateScheduler(interval: TimeSpan.FromHours(1));

        scheduler.Start();
        scheduler.Start();

        scheduler.IsRunning.ShouldBeTrue();
    }

    [Fact]
    public void Dispose_StopsTheSchedule()
    {
        var scheduler = CreateScheduler(interval: TimeSpan.FromHours(1));
        scheduler.Start();

        scheduler.Dispose();

        scheduler.IsRunning.ShouldBeFalse();
        Should.Throw<ObjectDisposedException>(() => scheduler.Start());
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline && !condition())
        {
            await Task.Delay(25);
        }
    }
}
