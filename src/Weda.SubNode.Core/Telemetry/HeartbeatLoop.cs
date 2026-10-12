using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Telemetry;

/// <summary>
/// Keeps the SubNode liveness heartbeat in step with configuration for the life of the
/// process: beating while the reserved heartbeat sensor is effectively enabled, silent while
/// it is not, and beating again as soon as configuration enables it.
/// </summary>
/// <remarks>
/// <para>
/// The heartbeat is an ordinary sensor entry (see <see cref="Heartbeat"/>), so a devicecfg
/// update can disable it — directly through its <c>Report.Enabled</c>, or by disabling the
/// device that declares it — and enable it again later. A loop that resolves the sensor once
/// and stops for good on the first disable leaves a healthy SubNode reading as Disconnected
/// until the process restarts. This type re-reads configuration on every call instead, so
/// every way back to an enabled sensor (re-enabling the device or the sensor, declaring the
/// sensor in a later update, a device that started disabled) restarts the beat by itself.
/// </para>
/// <para>
/// It is driven by repeated calls to <see cref="RunOnceAsync"/>, each of which returns how
/// long to wait before the next one. While beating, that is the time to the next beat;
/// otherwise it is <see cref="ConfigurationPollInterval"/>. A scan of the registered devices'
/// sensor lists is all one poll costs.
/// </para>
/// <para>
/// Not thread-safe: one caller drives it, as the heartbeat hosted service does.
/// </para>
/// </remarks>
public sealed class HeartbeatLoop
{
    /// <summary>
    /// How often configuration is checked while the heartbeat is not beating: before the
    /// SubNode is registered, while the sensor is missing or disabled, and after a failed
    /// configuration read. Short, so a SubNode is seen as Connected within seconds of being
    /// registered or re-enabled rather than after a full beat interval.
    /// </summary>
    public static readonly TimeSpan ConfigurationPollInterval = TimeSpan.FromSeconds(1);

    private readonly IWedaApplicationContext _context;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly ILogger _logger;
    private readonly ILogger<HeartbeatPublisher> _publisherLogger;
    private readonly TimeProvider _timeProvider;

    private State _state = State.Starting;
    private Sensor? _sensor;
    private HeartbeatPublisher? _publisher;
    private int _intervalMs;
    private DateTimeOffset _lastBeatAt;
    private DateTimeOffset _nextBeatAt;
    private bool _beatConfirmed;
    private int _reportedDeclarations = 1;
    private string? _reportedReadFailure;

    /// <summary>
    /// Creates a loop. Nothing is sent until <see cref="RunOnceAsync"/> is called.
    /// </summary>
    /// <param name="context">
    /// Supplies the SubNode identity and the cloud uplink. Read on every call, because the
    /// SubNode DeviceId is assigned at registration, after the loop is created.
    /// </param>
    /// <param name="deviceRegistry">The registered devices whose configuration may declare the sensor.</param>
    /// <param name="logger">Logger for state changes: armed, stopped, resumed, unreadable configuration.</param>
    /// <param name="publisherLogger">Logger for the publisher that sends each beat.</param>
    /// <param name="timeProvider">Clock for the beat schedule. Defaults to the system clock.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    public HeartbeatLoop(
        IWedaApplicationContext context,
        IDeviceRegistry deviceRegistry,
        ILogger logger,
        ILogger<HeartbeatPublisher> publisherLogger,
        TimeProvider? timeProvider = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _publisherLogger = publisherLogger ?? throw new ArgumentNullException(nameof(publisherLogger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    private enum State
    {
        /// <summary>No call has completed yet.</summary>
        Starting,

        /// <summary>Waiting for the SubNode DeviceId or the sensor ResourceId.</summary>
        Registering,

        /// <summary>Sending a beat every interval.</summary>
        Beating,

        /// <summary>The sensor is declared, but it or its device is disabled.</summary>
        Disabled,

        /// <summary>No device declares the sensor.</summary>
        NotDeclared,
    }

    /// <summary>
    /// Brings the heartbeat in line with the current configuration and sends a beat if one
    /// is due.
    /// </summary>
    /// <param name="cancellationToken">Cancelled on host shutdown.</param>
    /// <returns>How long to wait before calling again.</returns>
    /// <remarks>
    /// Never throws for a configuration or uplink fault: the next call is the recovery, and an
    /// exception escaping here would end liveness for the rest of the process. Cancellation
    /// propagates so shutdown is not mistaken for a fault.
    /// </remarks>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async Task<TimeSpan> RunOnceAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryFindSensor(out var sensor, out var declared))
            return ConfigurationPollInterval;

        if (sensor is null)
        {
            Stop(declared ? State.Disabled : State.NotDeclared);
            return ConfigurationPollInterval;
        }

        var subNodeInfo = _context.SubNodeInfo;
        if (string.IsNullOrWhiteSpace(subNodeInfo.DeviceId) || string.IsNullOrWhiteSpace(sensor.ResourceId))
        {
            // Pre-registration there is no DeviceId, and ResourceIds are assigned during device
            // initialization; a beat before then could not be attributed to anything. Only the
            // start moves to Registering: a stopped heartbeat keeps its state, so its restart
            // is still reported as a resume.
            if (_state == State.Starting)
                _state = State.Registering;
            return ConfigurationPollInterval;
        }

        var now = _timeProvider.GetUtcNow();
        if (_state != State.Beating || !ReferenceEquals(sensor, _sensor) || Heartbeat.ResolveInterval(sensor) != _intervalMs)
            Arm(sensor, subNodeInfo, now);

        if (now >= _nextBeatAt)
        {
            _lastBeatAt = now;
            _nextBeatAt = now.AddMilliseconds(_intervalMs);
            await BeatAsync(cancellationToken);
        }

        var wait = _nextBeatAt - _timeProvider.GetUtcNow();
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    /// <summary>
    /// Finds the heartbeat sensor across all registered devices.
    /// </summary>
    /// <param name="sensor">The effectively-enabled heartbeat sensor, or null when there is none.</param>
    /// <param name="declared">Whether any device declares the sensor, enabled or not.</param>
    /// <returns>False when configuration could not be read; the caller retries on the next poll.</returns>
    /// <remarks>
    /// Liveness is a property of the SubNode, so more than one enabled declaration is a
    /// configuration mistake rather than a reason to beat twice: the first is used and the
    /// rest are reported.
    /// </remarks>
    private bool TryFindSensor(out Sensor? sensor, out bool declared)
    {
        sensor = null;
        declared = false;
        var enabled = 0;

        try
        {
            foreach (var device in _deviceRegistry.GetAllDevices())
            {
                // Copied first: a configuration update can replace or grow the list while it
                // is read here.
                var sensors = device.Configuration?.Sensors?.ToArray();
                declared |= Heartbeat.IsDeclaredBy(sensors);
                if (Heartbeat.FindEnabled(sensors) is { } found)
                {
                    enabled++;
                    sensor ??= found;
                }
            }
        }
        catch (Exception ex)
        {
            var reason = $"{ex.GetType().Name}: {ex.Message}";
            if (reason != _reportedReadFailure)
            {
                _reportedReadFailure = reason;
                _logger.LogWarning(ex,
                    "Could not read the heartbeat configuration; checking again in {DelayMs} ms",
                    ConfigurationPollInterval.TotalMilliseconds);
            }
            sensor = null;
            declared = false;
            return false;
        }

        _reportedReadFailure = null;
        if (enabled > 1 && enabled != _reportedDeclarations)
        {
            _logger.LogWarning(
                "{Count} devices declare a heartbeat sensor; the SubNode emits one liveness signal, " +
                "so only '{Sensor}' is used",
                enabled, sensor!.Name);
        }
        _reportedDeclarations = enabled;
        return true;
    }

    /// <summary>
    /// Starts beating on <paramref name="sensor"/>, or follows a change to it while beating.
    /// </summary>
    private void Arm(Sensor sensor, SubNodeInfo subNodeInfo, DateTimeOffset now)
    {
        var interval = Heartbeat.ResolveInterval(sensor);
        if (interval != _intervalMs && interval != sensor.Report.Interval)
        {
            _logger.LogWarning(
                "Heartbeat interval {Configured} ms is below the {Floor} ms floor; beating at {Effective} ms instead",
                sensor.Report.Interval, Heartbeat.MinIntervalMilliseconds, interval);
        }

        if (_state == State.Beating)
        {
            // Same heartbeat, new sensor object or interval: keep the cadence, measured from
            // the last beat, instead of beating early.
            _nextBeatAt = _lastBeatAt.AddMilliseconds(interval);
            _logger.LogDebug(
                "SubNode heartbeat follows a configuration change on sensor '{Sensor}' (interval {IntervalMs} ms)",
                sensor.Name, interval);
        }
        else
        {
            // Beat at once so the SubNode is seen as Connected without waiting a full
            // interval, as after a restart.
            _nextBeatAt = now;
            _beatConfirmed = false;

            if (_state is State.Disabled or State.NotDeclared)
            {
                // Pairs with the warning that reported the stop.
                _logger.LogInformation(
                    "Heartbeat sensor '{Sensor}' is enabled; liveness reporting resumed (interval {IntervalMs} ms)",
                    sensor.Name, interval);
            }
            else
            {
                // Debug: this is the healthy path. To confirm a heartbeat is running on a
                // device, raise the level: Serilog__MinimumLevel__Default=Debug
                _logger.LogDebug(
                    "SubNode heartbeat ARMED from sensor '{Sensor}' (interval {IntervalMs} ms)",
                    sensor.Name, interval);
            }
        }

        _sensor = sensor;
        _intervalMs = interval;
        _publisher = new HeartbeatPublisher(_context.CloudService, subNodeInfo, sensor, _publisherLogger);
        _state = State.Beating;
    }

    /// <summary>
    /// Stops beating and reports why, once per change of state.
    /// </summary>
    private void Stop(State next)
    {
        if (_state == next)
            return;

        if (next == State.Disabled)
        {
            if (_state == State.Beating)
            {
                // Surfaced deliberately: from here the platform sees silence and reports
                // Disconnected, with nothing else on the device indicating why.
                _logger.LogWarning(
                    "Heartbeat sensor '{Sensor}' was disabled by configuration; liveness reporting has stopped " +
                    "and the platform will read this SubNode as Disconnected until it is enabled again",
                    _sensor?.Name ?? Heartbeat.SensorName);
            }
            else
            {
                _logger.LogWarning(
                    "SubNode heartbeat is DISABLED: sensor '{Sensor}' is declared but disabled by configuration " +
                    "(its Report.Enabled, or the device that declares it, is off). The platform will read this " +
                    "SubNode as Disconnected until it is enabled.",
                    Heartbeat.SensorName);
            }
        }
        else
        {
            // Warning rather than Debug: this is the failure mode that looks exactly like a
            // dead node from the cloud. Naming the sensor the SDK looks for makes the fix obvious.
            _logger.LogWarning(
                "SubNode heartbeat is DISABLED: no enabled sensor named '{Sensor}' was found. "
                + "This SubNode publishes no liveness signal and the platform will read it as "
                + "Disconnected. Declare a sensor named '{Sensor}' in devicecfg.json to enable it.",
                Heartbeat.SensorName, Heartbeat.SensorName);
        }

        _state = next;
        _sensor = null;
        _publisher = null;
        _intervalMs = 0;
    }

    private async Task BeatAsync(CancellationToken cancellationToken)
    {
        var sent = await _publisher!.PublishAsync(cancellationToken);
        if (sent && !_beatConfirmed)
        {
            // Once per arming, and kept distinct from ARMED: that one only says the loop
            // started, while this says a beat was accepted by the uplink. The pair separates
            // a working heartbeat from one running but silently failing to publish.
            _beatConfirmed = true;
            _logger.LogDebug(
                "SubNode heartbeat CONFIRMED: first beat accepted by the cloud uplink "
                + "(sensor '{Sensor}', every {IntervalMs} ms from here)",
                _sensor!.Name, _intervalMs);
        }
    }
}
