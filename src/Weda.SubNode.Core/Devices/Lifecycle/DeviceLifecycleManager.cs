using System.Diagnostics;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Devices.StateMachine;

namespace Weda.SubNode.Core.Devices.Lifecycle;

/// <summary>
/// Thread-safe device lifecycle manager with sealed template methods.
/// Provides lifecycle management with extension hooks and proper state coordination.
/// </summary>
public sealed class DeviceLifecycleManager
{
    private readonly string _deviceId;
    private readonly IDeviceStateMachine _stateMachine;
    private readonly ILifecycleHooks _hooks;
    private readonly ILogger<DeviceLifecycleManager> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Statistics tracking
    private int _initializeCount;
    private int _startCount;
    private int _stopCount;
    private int _disposeCount;
    private readonly List<TimeSpan> _initializeDurations = new();
    private readonly List<TimeSpan> _startDurations = new();
    private readonly List<TimeSpan> _stopDurations = new();
    private readonly List<TimeSpan> _disposeDurations = new();

    // Disposal tracking
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeviceLifecycleManager"/> class.
    /// </summary>
    public DeviceLifecycleManager(
        string deviceId,
        IDeviceStateMachine stateMachine,
        ILifecycleHooks hooks,
        ILogger<DeviceLifecycleManager> logger)
    {
        _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Event raised during lifecycle stage execution.
    /// </summary>
    public event EventHandler<DeviceLifecycleEvent>? LifecycleExecuting;

    /// <summary>
    /// Gets whether the manager has been disposed.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// Initializes the device. Sealed template method.
    /// Transitions state: Initializing → Ready
    /// </summary>
    public async Task<ErrorOr<Success>> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            // Verify current state allows initialization
            if (_stateMachine.CurrentStatus != DeviceStatus.Initializing)
            {
                return Error.Conflict(
                    code: "DeviceLifecycle.InvalidState",
                    description: $"Cannot initialize device in {_stateMachine.CurrentStatus} state");
            }

            var stopwatch = Stopwatch.StartNew();
            EmitLifecycleEvent(LifecycleStage.Initialize, LifecyclePhase.Before);

            _logger.LogInformation(
                "Initializing device {DeviceId}",
                _deviceId);

            try
            {
                // Call hook
                var hookResult = await _hooks.OnInitializeAsync(cancellationToken);
                if (hookResult.IsError)
                {
                    stopwatch.Stop();
                    EmitLifecycleEvent(LifecycleStage.Initialize, LifecyclePhase.After,
                        stopwatch.Elapsed, hookResult.FirstError.Description);
                    return hookResult.Errors;
                }

                // Transition to Ready
                var transitionResult = _stateMachine.TryTransition(DeviceStatus.Ready);
                if (transitionResult.IsError)
                {
                    stopwatch.Stop();
                    EmitLifecycleEvent(LifecycleStage.Initialize, LifecyclePhase.After,
                        stopwatch.Elapsed, transitionResult.FirstError.Description);
                    return transitionResult.Errors;
                }

                stopwatch.Stop();
                _initializeCount++;
                RecordDuration(_initializeDurations, stopwatch.Elapsed);
                EmitLifecycleEvent(LifecycleStage.Initialize, LifecyclePhase.After, stopwatch.Elapsed);

                _logger.LogInformation(
                    "Device initialized successfully in {Duration}ms", stopwatch.ElapsedMilliseconds);

                return Result.Success;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                EmitLifecycleEvent(LifecycleStage.Initialize, LifecyclePhase.After,
                    stopwatch.Elapsed, ex.Message);

                _logger.LogError(ex,
                    "Device {DeviceId} initialization failed",
                    _deviceId);

                return Error.Failure(
                    code: "DeviceLifecycle.InitializeFailed",
                    description: $"Initialization failed: {ex.Message}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Starts the device. Sealed template method.
    /// Transitions state: Ready → Running
    /// </summary>
    public async Task<ErrorOr<Success>> StartAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            // Verify current state allows starting
            if (_stateMachine.CurrentStatus != DeviceStatus.Ready)
            {
                return Error.Conflict(
                    code: "DeviceLifecycle.InvalidState",
                    description: $"Cannot start device in {_stateMachine.CurrentStatus} state");
            }

            var stopwatch = Stopwatch.StartNew();
            EmitLifecycleEvent(LifecycleStage.Start, LifecyclePhase.Before);

            _logger.LogInformation(
                "Starting device {DeviceId}",
                _deviceId);

            try
            {
                // Call hook
                var hookResult = await _hooks.OnStartAsync(cancellationToken);
                if (hookResult.IsError)
                {
                    stopwatch.Stop();
                    EmitLifecycleEvent(LifecycleStage.Start, LifecyclePhase.After,
                        stopwatch.Elapsed, hookResult.FirstError.Description);
                    return hookResult.Errors;
                }

                // Transition to Running
                var transitionResult = _stateMachine.TryTransition(DeviceStatus.Running);
                if (transitionResult.IsError)
                {
                    stopwatch.Stop();
                    EmitLifecycleEvent(LifecycleStage.Start, LifecyclePhase.After,
                        stopwatch.Elapsed, transitionResult.FirstError.Description);
                    return transitionResult.Errors;
                }

                stopwatch.Stop();
                _startCount++;
                RecordDuration(_startDurations, stopwatch.Elapsed);
                EmitLifecycleEvent(LifecycleStage.Start, LifecyclePhase.After, stopwatch.Elapsed);

                _logger.LogInformation(
                    "Device {DeviceId} started successfully in {Duration}ms",
                    _deviceId, stopwatch.ElapsedMilliseconds);

                return Result.Success;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                EmitLifecycleEvent(LifecycleStage.Start, LifecyclePhase.After,
                    stopwatch.Elapsed, ex.Message);

                _logger.LogError(ex,
                    "Device {DeviceId} start failed",
                    _deviceId);

                return Error.Failure(
                    code: "DeviceLifecycle.StartFailed",
                    description: $"Start failed: {ex.Message}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Stops the device. Sealed template method.
    /// Transitions state: Running → Stopped
    /// </summary>
    public async Task<ErrorOr<Success>> StopAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            // Verify current state allows stopping
            if (_stateMachine.CurrentStatus != DeviceStatus.Running)
            {
                return Error.Conflict(
                    code: "DeviceLifecycle.InvalidState",
                    description: $"Cannot stop device in {_stateMachine.CurrentStatus} state");
            }

            var stopwatch = Stopwatch.StartNew();
            EmitLifecycleEvent(LifecycleStage.Stop, LifecyclePhase.Before);

            _logger.LogInformation(
                "Stopping device {DeviceId}",
                _deviceId);

            try
            {
                // Call hook
                var hookResult = await _hooks.OnStopAsync(cancellationToken);
                if (hookResult.IsError)
                {
                    stopwatch.Stop();
                    EmitLifecycleEvent(LifecycleStage.Stop, LifecyclePhase.After,
                        stopwatch.Elapsed, hookResult.FirstError.Description);
                    return hookResult.Errors;
                }

                // Transition to Stopped
                var transitionResult = _stateMachine.TryTransition(DeviceStatus.Stopped);
                if (transitionResult.IsError)
                {
                    stopwatch.Stop();
                    EmitLifecycleEvent(LifecycleStage.Stop, LifecyclePhase.After,
                        stopwatch.Elapsed, transitionResult.FirstError.Description);
                    return transitionResult.Errors;
                }

                stopwatch.Stop();
                _stopCount++;
                RecordDuration(_stopDurations, stopwatch.Elapsed);
                EmitLifecycleEvent(LifecycleStage.Stop, LifecyclePhase.After, stopwatch.Elapsed);

                _logger.LogInformation(
                    "Device {DeviceId} stopped successfully in {Duration}ms",
                    _deviceId, stopwatch.ElapsedMilliseconds);

                return Result.Success;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                EmitLifecycleEvent(LifecycleStage.Stop, LifecyclePhase.After,
                    stopwatch.Elapsed, ex.Message);

                _logger.LogError(ex,
                    "Device {DeviceId} stop failed",
                    _deviceId);

                return Error.Failure(
                    code: "DeviceLifecycle.StopFailed",
                    description: $"Stop failed: {ex.Message}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Disposes the device. Sealed template method.
    /// Can be called from any state. Idempotent - safe to call multiple times.
    /// </summary>
    public async Task<ErrorOr<Success>> DisposeAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Idempotent - already disposed
            if (_isDisposed)
            {
                _logger.LogDebug("Device {DeviceId} already disposed", _deviceId);
                return Result.Success;
            }

            var stopwatch = Stopwatch.StartNew();
            EmitLifecycleEvent(LifecycleStage.Dispose, LifecyclePhase.Before);

            _logger.LogInformation(
                "Disposing device {DeviceId}",
                _deviceId);

            try
            {
                // Call hook
                var hookResult = await _hooks.OnDisposeAsync(cancellationToken);
                if (hookResult.IsError)
                {
                    _logger.LogWarning(
                        "Device {DeviceId} dispose hook returned error: {Error}",
                        _deviceId, hookResult.FirstError.Description);
                    // Continue with disposal even if hook fails
                }

                // Mark as disposed
                _isDisposed = true;

                stopwatch.Stop();
                _disposeCount++;
                RecordDuration(_disposeDurations, stopwatch.Elapsed);
                EmitLifecycleEvent(LifecycleStage.Dispose, LifecyclePhase.After, stopwatch.Elapsed);

                _logger.LogInformation(
                    "Device {DeviceId} disposed successfully in {Duration}ms",
                    _deviceId, stopwatch.ElapsedMilliseconds);

                return Result.Success;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                EmitLifecycleEvent(LifecycleStage.Dispose, LifecyclePhase.After,
                    stopwatch.Elapsed, ex.Message);

                _logger.LogError(ex,
                    "Device {DeviceId} disposal failed",
                    _deviceId);

                // Mark as disposed anyway to prevent further operations
                _isDisposed = true;

                return Error.Failure(
                    code: "DeviceLifecycle.DisposeFailed",
                    description: $"Disposal failed: {ex.Message}");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Gets lifecycle statistics.
    /// </summary>
    public LifecycleStatistics GetStatistics()
    {
        return new LifecycleStatistics
        {
            DeviceId = _deviceId,
            InitializeCount = _initializeCount,
            StartCount = _startCount,
            StopCount = _stopCount,
            DisposeCount = _disposeCount,
            AverageInitializeDuration = CalculateAverage(_initializeDurations),
            AverageStartDuration = CalculateAverage(_startDurations),
            AverageStopDuration = CalculateAverage(_stopDurations),
            AverageDisposeDuration = CalculateAverage(_disposeDurations),
            IsDisposed = _isDisposed
        };
    }

    private void EmitLifecycleEvent(
        LifecycleStage stage,
        LifecyclePhase phase,
        TimeSpan? duration = null,
        string? error = null)
    {
        LifecycleExecuting?.Invoke(this, new DeviceLifecycleEvent(
            DeviceId: _deviceId,
            Stage: stage,
            Timestamp: DateTimeOffset.UtcNow)
        {
            Phase = phase,
            Duration = duration,
            Error = error
        });
    }

    private void RecordDuration(List<TimeSpan> durations, TimeSpan duration)
    {
        durations.Add(duration);
        // Keep only last 100 samples
        if (durations.Count > 100)
        {
            durations.RemoveAt(0);
        }
    }

    private TimeSpan CalculateAverage(List<TimeSpan> durations)
    {
        if (durations.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var avgMs = durations.Average(d => d.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(avgMs);
    }

    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(
                _deviceId,
                $"Device {_deviceId} has been disposed");
        }
    }
}

/// <summary>
/// Lifecycle statistics.
/// </summary>
public sealed record LifecycleStatistics
{
    public required string DeviceId { get; init; }
    public required int InitializeCount { get; init; }
    public required int StartCount { get; init; }
    public required int StopCount { get; init; }
    public required int DisposeCount { get; init; }
    public TimeSpan AverageInitializeDuration { get; init; }
    public TimeSpan AverageStartDuration { get; init; }
    public TimeSpan AverageStopDuration { get; init; }
    public TimeSpan AverageDisposeDuration { get; init; }
    public required bool IsDisposed { get; init; }
}
