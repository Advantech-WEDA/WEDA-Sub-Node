using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Telemetry;

namespace Weda.SubNode.Host;

/// <summary>
/// Drives the SubNode liveness heartbeat for the life of the host.
/// </summary>
/// <remarks>
/// <para>
/// Beats only while some device declares the reserved heartbeat sensor in
/// <c>devicecfg.json</c> (see <see cref="Heartbeat"/>) and that sensor is effectively
/// enabled. Applications that declare no such sensor send nothing; checking for one costs a
/// scan of the device configuration once per <see cref="HeartbeatLoop.ConfigurationPollInterval"/>.
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
/// from the cloud — that sets its <c>Report.Enabled</c> to <c>false</c>, or disables the
/// device that declares it, stops liveness reporting, and the platform will then read this
/// SubNode as Disconnected with no fault anywhere on the device. SD-Heartbeat-Committed-Scope
/// §"The heartbeat sensor" calls for <c>hb</c> to be filtered out of the customer-facing
/// telemetry-configs surface for exactly this reason; that filtering is a platform-side
/// control and cannot be enforced here. <see cref="HeartbeatLoop"/> logs the stop, and
/// resumes the beat by itself as soon as configuration enables the sensor again.
/// </para>
/// </remarks>
internal sealed class HeartbeatHostedService : BackgroundService
{
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
        // Logs under this service's category, so existing log filters keep matching.
        var loop = new HeartbeatLoop(_context, _deviceRegistry, _logger, _publisherLogger);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var wait = await loop.RunOnceAsync(stoppingToken);
                await Task.Delay(wait, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        _logger.LogInformation("SubNode heartbeat stopped");
    }
}
