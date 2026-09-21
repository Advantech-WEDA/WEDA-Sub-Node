using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Storage;
using Weda.SubNode.Abstractions.Storage.Recordings;

namespace Weda.SubNode.Core.Storage;

/// <summary>
/// Runs recording cleanup - age-based retention and ring-buffer FIFO - on a repeating schedule.
/// </summary>
/// <remarks>
/// <para>
/// The sweep repeats on a short interval (hourly by default) rather than once a day. A daily
/// sweep is anchored to process start, which makes retention depend on uptime: files that land
/// just after the startup sweep sit a full extra day on disk, and a device that restarts more
/// often than once a day never runs anything but the startup sweep - it sees whatever was on
/// disk when it booted and nothing written since. That is the defect this class exists to close.
/// </para>
/// <para>
/// Every sweep is logged with its cutoff and its tally, so "ran and found nothing" is
/// distinguishable from "never ran" without attaching a debugger to a device in the field.
/// </para>
/// </remarks>
public sealed class RecordingCleanupScheduler : IDisposable
{
    private readonly ILogger<RecordingCleanupScheduler> _logger;
    private readonly IRecordingService _recordingService;
    private readonly IDynamicRecordStorage? _dynamicStorage;
    private readonly TimeSpan _interval;
    private readonly TimeProvider _timeProvider;
    private readonly CancellationTokenSource _stopping = new();

    private ITimer? _timer;
    private int _sweepInProgress;
    private bool _disposed;

    /// <summary>
    /// Creates a scheduler. Nothing runs until <see cref="Start"/> is called.
    /// </summary>
    /// <param name="logger">Logger for sweep reporting.</param>
    /// <param name="recordingService">The recording service whose storage is swept.</param>
    /// <param name="dynamicStorage">
    /// Storage for MIME-typed recordings (JSON, images), swept with the same cutoff. Optional:
    /// pass <c>null</c> when the host has none.
    /// </param>
    /// <param name="interval">
    /// How often to sweep. Must be positive; <see cref="Start"/> refuses to schedule otherwise.
    /// </param>
    /// <param name="timeProvider">Clock used for the cutoff and the timer. Defaults to the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> or <paramref name="recordingService"/> is null.</exception>
    public RecordingCleanupScheduler(
        ILogger<RecordingCleanupScheduler> logger,
        IRecordingService recordingService,
        IDynamicRecordStorage? dynamicStorage,
        TimeSpan interval,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(recordingService);

        _logger = logger;
        _recordingService = recordingService;
        _dynamicStorage = dynamicStorage;
        _interval = interval;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Whether the repeating sweep is currently scheduled.
    /// </summary>
    public bool IsRunning => _timer is not null;

    /// <summary>
    /// Starts sweeping: once immediately, then every interval.
    /// </summary>
    /// <remarks>
    /// The immediate sweep clears whatever aged out while the process was down. Calling this
    /// more than once is a no-op.
    /// </remarks>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_timer is not null)
            return;

        if (_interval <= TimeSpan.Zero)
        {
            _logger.LogWarning(
                "Recording cleanup is disabled (CleanupIntervalMinutes=0). Recording storage will only shrink while data is being written.");
            return;
        }

        _logger.LogInformation(
            "Recording cleanup scheduled every {IntervalMinutes} minute(s); retention={RetentionDays}d, maxStorage={MaxStorageSizeMb}MB, minFreeDisk={MinFreeDiskSpaceMb}MB",
            _interval.TotalMinutes,
            _recordingService.StorageSettings.RetentionDays,
            _recordingService.StorageSettings.MaxStorageSizeMb,
            _recordingService.StorageSettings.MinFreeDiskSpaceMb);

        _timer = _timeProvider.CreateTimer(
            callback: _ => TriggerSweep(),
            state: null,
            dueTime: TimeSpan.Zero,
            period: _interval);
    }

    /// <summary>
    /// Runs one cleanup sweep and reports what it did.
    /// </summary>
    /// <remarks>
    /// Exposed so a host can force a sweep, and so the schedule can be tested without waiting
    /// on a timer. Safe to call while the scheduler is running.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The combined tally of every pass in this sweep.</returns>
    public async Task<RecordingCleanupResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var settings = _recordingService.StorageSettings;
        var now = _timeProvider.GetUtcNow();
        var cutoff = settings.GetRetentionCutoff(now);
        var result = RecordingCleanupResult.Empty;

        if (cutoff is null)
        {
            _logger.LogDebug("Retention sweep skipped: RetentionDays=0 (disabled)");
        }
        else
        {
            result += await _recordingService.CleanupAsync(cutoff.Value, cancellationToken);

            // MIME-typed recordings age out under exactly the same policy; sweeping only the
            // slot-based files would leave images and JSON payloads on disk forever.
            if (_dynamicStorage is not null)
            {
                var dynamicDeleted = await _dynamicStorage.CleanupAsync(cutoff.Value, cancellationToken);
                result += new RecordingCleanupResult(dynamicDeleted, dynamicDeleted, BytesFreed: 0, FilesFailed: 0);
            }
        }

        if (settings.SizeLimitEnabled)
        {
            result += await _recordingService.EnforceStorageLimitsAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Recording cleanup swept: cutoff={Cutoff}, scanned={FilesScanned}, deleted={FilesDeleted}, freed={BytesFreed}B, failed={FilesFailed}",
            cutoff?.ToString("O") ?? "disabled",
            result.FilesScanned,
            result.FilesDeleted,
            result.BytesFreed,
            result.FilesFailed);

        return result;
    }

    /// <summary>
    /// Timer entry point: starts a sweep unless one is still running.
    /// </summary>
    /// <remarks>
    /// Sweeps must not stack. A pass over a directory holding years of recordings can outlast
    /// the interval, and overlapping passes would race each other deleting the same files.
    /// </remarks>
    private void TriggerSweep()
    {
        if (Interlocked.Exchange(ref _sweepInProgress, 1) == 1)
        {
            _logger.LogDebug("Recording cleanup skipped: previous sweep still running");
            return;
        }

        _ = SweepAndReleaseAsync();
    }

    private async Task SweepAndReleaseAsync()
    {
        try
        {
            await RunOnceAsync(_stopping.Token);
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            _logger.LogDebug("Recording cleanup cancelled during shutdown");
        }
        catch (Exception ex)
        {
            // A failed sweep must never take the host down, and must never stop the schedule:
            // the next interval gets another attempt.
            _logger.LogError(ex, "Recording cleanup sweep failed");
        }
        finally
        {
            Volatile.Write(ref _sweepInProgress, 0);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Cancel first so an in-flight sweep unwinds instead of holding shutdown while it
        // walks the whole recording directory.
        _stopping.Cancel();
        _timer?.Dispose();
        _timer = null;
        _stopping.Dispose();
    }
}
