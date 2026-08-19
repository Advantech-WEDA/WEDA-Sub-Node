using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Telemetry;

namespace Weda.SubNode.Host;

/// <summary>
/// Drives the SubNode liveness heartbeat on a fixed interval.
/// </summary>
/// <remarks>
/// <para>
/// Inert unless some device declares the reserved heartbeat sensor in
/// <c>devicecfg.json</c> (see <see cref="Heartbeat"/>). No sensor, no beat and no cost, so
/// applications that do not want a heartbeat are unaffected.
/// </para>
/// <para>
/// The beat is sent on the <em>SubNode's</em> DeviceId, not the owning device's, so one
/// SubNode produces one liveness signal no matter how many devices it runs — and a fault in
/// the device that happens to host the sensor does not make the whole node look offline.
/// The sensor is configuration; it is never polled.
/// </para>
/// <para>
/// <strong>Operational consequence of config-driven enablement.</strong> Because the
/// heartbeat is an ordinary sensor entry, a configuration update — including one pushed
/// from the cloud — that sets its <c>Report.Enabled</c> to <c>false</c> will stop liveness
/// reporting, and the platform will then read this SubNode as Disconnected with no fault
/// anywhere on the device. SD-Heartbeat-Committed-Scope §"The heartbeat sensor" calls for
/// <c>hb</c> to be filtered out of the customer-facing telemetry-configs surface for
/// exactly this reason; that filtering is a platform-side control and cannot be enforced
/// here. This service logs the transition so the cause is at least visible on the device.
/// </para>
/// </remarks>
internal sealed class HeartbeatHostedService : BackgroundService
{
    /// <summary>
    /// How often to re-check for a usable heartbeat sensor before the first beat.
    /// Hosted services start in registration order, so this service is running before
    /// <see cref="DeviceHostedService"/> has registered the SubNode and assigned sensor
    /// ResourceIds. Polling briefly means a restarted SubNode is seen as Connected within
    /// seconds instead of waiting out a full beat interval.
    /// </summary>
    private static readonly TimeSpan ReadinessPollInterval = TimeSpan.FromSeconds(1);

    private readonly IWedaApplicationContext _context;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly ILogger<HeartbeatHostedService> _logger;
    private readonly ILogger<HeartbeatPublisher> _publisherLogger;

    public HeartbeatHostedService(
        IWedaApplicationContext context,
        IDeviceRegistry deviceRegistry,
        ILogger<HeartbeatHostedService> logger,
        ILogger<HeartbeatPublisher> publisherLogger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _deviceRegistry = deviceRegistry ?? throw new ArgumentNullException(nameof(deviceRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _publisherLogger = publisherLogger ?? throw new ArgumentNullException(nameof(publisherLogger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var sensor = await WaitForHeartbeatSensorAsync(stoppingToken);
            if (sensor is null)
                return;

            var interval = Heartbeat.ResolveInterval(sensor);
            if (interval != sensor.Report.Interval)
            {
                _logger.LogWarning(
                    "Heartbeat interval {Configured} ms is below the {Floor} ms floor; beating at {Effective} ms instead",
                    sensor.Report.Interval, Heartbeat.MinIntervalMilliseconds, interval);
            }

            // Deliberately Warning, not Information. Logging healthy startup at Warning is
            // normally wrong; it is right here because the shipped appsettings.json pins
            // Serilog to Warning, and this is the only statement of whether liveness is
            // running at all. Silence from a heartbeat is indistinguishable from a dead
            // node, so an operator must not have to raise the log level to find out which
            // one they have. One line per process start is not noise.
            _logger.LogWarning(
                "SubNode heartbeat ARMED from sensor '{Sensor}' (interval {IntervalMs} ms)",
                sensor.Name, interval);

            var publisher = new HeartbeatPublisher(
                _context.CloudService, _context.SubNodeInfo, sensor, _publisherLogger);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));
            var beatConfirmed = false;

            // Beat immediately so a restarted SubNode is seen as Connected without waiting
            // a full interval, then settle into the cadence.
            do
            {
                if (!sensor.IsEffectivelyEnabled)
                {
                    // Surfaced deliberately: from here the platform will see silence and
                    // report Disconnected, with nothing else on the device indicating why.
                    _logger.LogWarning(
                        "Heartbeat sensor '{Sensor}' was disabled by configuration; " +
                        "liveness reporting has stopped and the platform will read this SubNode as Disconnected",
                        sensor.Name);
                    break;
                }

                var sent = await publisher.PublishAsync(stoppingToken);

                if (sent && !beatConfirmed)
                {
                    // Also Warning, and also once: "armed" only says the loop started, while
                    // this says a beat was actually accepted by the uplink. That is the fact
                    // an operator needs to distinguish a working heartbeat from one that is
                    // running but silently failing to publish. Subsequent beats stay at Debug.
                    beatConfirmed = true;
                    _logger.LogWarning(
                        "SubNode heartbeat CONFIRMED: first beat accepted by the cloud uplink "
                        + "(sensor '{Sensor}', every {IntervalMs} ms from here)",
                        sensor.Name, interval);
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }

        _logger.LogInformation("SubNode heartbeat stopped");
    }

    /// <summary>
    /// Waits until a declared heartbeat sensor is usable, or the host shuts down.
    /// </summary>
    /// <returns>
    /// The heartbeat sensor once the SubNode is registered and the sensor has a
    /// ResourceId, or <see langword="null"/> if the host stopped first.
    /// </returns>
    /// <remarks>
    /// Returns immediately when no device declares the sensor — that is the disabled case
    /// and must not spin. Devices are created before this service starts, so their
    /// configured sensors are already visible; only the SubNode DeviceId and the sensor
    /// ResourceIds are assigned later, during device initialization.
    /// </remarks>
    private async Task<Sensor?> WaitForHeartbeatSensorAsync(CancellationToken cancellationToken)
    {
        var sensor = FindHeartbeatSensor();
        if (sensor is null)
        {
            // Warning rather than Debug: this is the failure mode that looks exactly like a
            // dead node from the cloud. Previously it was invisible at the shipped Warning
            // log level, so a SubNode reported Disconnected gave an operator nothing to go
            // on. Naming the sensor the SDK looked for makes the fix obvious.
            _logger.LogWarning(
                "SubNode heartbeat is DISABLED: no enabled sensor named '{Sensor}' was found. "
                + "This SubNode publishes no liveness signal and the platform will read it as "
                + "Disconnected. Declare a sensor named '{Sensor}' in devicecfg.json to enable it.",
                Heartbeat.SensorName, Heartbeat.SensorName);
            return null;
        }

        while (string.IsNullOrWhiteSpace(_context.SubNodeInfo.DeviceId)
               || string.IsNullOrWhiteSpace(sensor.ResourceId))
        {
            await Task.Delay(ReadinessPollInterval, cancellationToken);
        }

        return sensor;
    }

    /// <summary>
    /// Finds the single heartbeat sensor across all registered devices.
    /// </summary>
    /// <remarks>
    /// Liveness is a property of the SubNode, so more than one declaration is a
    /// configuration mistake rather than a reason to beat twice. The first is used and the
    /// rest are reported.
    /// </remarks>
    private Sensor? FindHeartbeatSensor()
    {
        var sensors = _deviceRegistry.GetAllDevices()
            .Select(device => Heartbeat.FindEnabled(device.Configuration?.Sensors))
            .OfType<Sensor>()
            .ToList();

        if (sensors.Count > 1)
        {
            _logger.LogWarning(
                "{Count} devices declare a heartbeat sensor; the SubNode emits one liveness signal, " +
                "so only '{Sensor}' is used",
                sensors.Count, sensors[0].Name);
        }

        return sensors.FirstOrDefault();
    }
}
