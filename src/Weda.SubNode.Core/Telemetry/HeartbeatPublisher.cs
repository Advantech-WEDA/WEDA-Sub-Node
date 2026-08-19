using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Telemetry;

/// <summary>
/// Publishes a single SubNode liveness beat onto the existing telemetry path.
/// </summary>
/// <remarks>
/// <para>
/// One beat is one <see cref="TelemetryMeasure"/> carrying a constant <c>true</c> against
/// the heartbeat sensor's ResourceId. It is sent on the SubNode's own
/// DeviceId, so it is independent of how many devices the application has configured and
/// of whether any of them are healthy — see <see cref="Heartbeat"/> for why liveness is
/// deliberately decoupled from sensor configuration.
/// </para>
/// <para>
/// This type owns the beat, not the schedule. The caller decides when to beat.
/// </para>
/// </remarks>
public sealed class HeartbeatPublisher
{
    private readonly IWedaCloudService _cloudService;
    private readonly SubNodeInfo _subNodeInfo;
    private readonly Sensor _sensor;
    private readonly ILogger<HeartbeatPublisher> _logger;

    /// <summary>
    /// Creates a publisher for the given SubNode and its declared heartbeat sensor.
    /// </summary>
    /// <param name="cloudService">Cloud uplink used to send the beat.</param>
    /// <param name="subNodeInfo">SubNode identity; supplies the cloud-assigned DeviceId.</param>
    /// <param name="sensor">
    /// The reserved heartbeat sensor declared in <c>devicecfg.json</c>. Its
    /// <c>ResourceId</c> identifies the beat, so enriched telemetry resolves it against
    /// the sensor registry rather than falling back to <c>"unknown"</c>.
    /// </param>
    /// <param name="logger">Logger for beat outcomes.</param>
    public HeartbeatPublisher(
        IWedaCloudService cloudService,
        SubNodeInfo subNodeInfo,
        Sensor sensor,
        ILogger<HeartbeatPublisher> logger)
    {
        _cloudService = cloudService ?? throw new ArgumentNullException(nameof(cloudService));
        _subNodeInfo = subNodeInfo ?? throw new ArgumentNullException(nameof(subNodeInfo));
        _sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Emits one liveness beat.
    /// </summary>
    /// <param name="cancellationToken">Cancelled on host shutdown.</param>
    /// <returns>
    /// <see langword="true"/> when the beat reached the cloud; <see langword="false"/> when
    /// it was skipped (SubNode not yet registered) or the uplink failed.
    /// </returns>
    /// <remarks>
    /// Never throws for a transport or uplink fault. The next beat is the recovery
    /// mechanism, and an unhandled exception here would stop liveness permanently —
    /// leaving a healthy SubNode looking Disconnected, which is the one failure direction
    /// this design must avoid. Cancellation is deliberately allowed to propagate so
    /// shutdown is not mistaken for a fault.
    /// </remarks>
    public async Task<bool> PublishAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var deviceId = _subNodeInfo.DeviceId;
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            // Pre-registration there is no DeviceId and no telemetry topic assignment.
            // Debug, not warning: this is expected during startup and self-resolves.
            _logger.LogDebug("Skipping heartbeat: SubNode is not registered yet");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_sensor.ResourceId))
        {
            // ResourceIds are assigned during device initialization. Beating before that
            // would emit a measure the platform cannot attribute to anything.
            _logger.LogDebug("Skipping heartbeat: sensor '{Name}' has no ResourceId yet", _sensor.Name);
            return false;
        }

        var telemetryData = new TelemetryData
        {
            Measures = [CreateMeasure()]
        };

        try
        {
            var sent = await _cloudService.SendTelemetryAsync(deviceId, telemetryData, cancellationToken);

            if (sent)
            {
                _logger.LogDebug("Heartbeat sent for SubNode {DeviceId}", deviceId);
            }
            else
            {
                _logger.LogWarning(
                    "Heartbeat was not accepted by the cloud uplink for SubNode {DeviceId}. " +
                    "The platform may read this SubNode as Abnormal if this persists.",
                    deviceId);
            }

            return sent;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send heartbeat for SubNode {DeviceId}. Retrying on the next beat.",
                deviceId);
            return false;
        }
    }

    /// <summary>
    /// Builds the beat measure: a constant <c>true</c> against the heartbeat sensor's
    /// ResourceId.
    /// </summary>
    /// <remarks>
    /// Metadata is deliberately left unset. <see cref="TelemetryMeasure.Metadata"/> is the
    /// framework's chunked-transfer descriptor, not a free-form bag — the WedaNode telemetry
    /// proxy validates it whenever it is present and rejects the <em>entire</em> message
    /// with <c>measures[0].metadata.transferId: transfer ID is required</c>. A liveness beat
    /// has nothing to chunk, so populating it would guarantee the beat never arrives and the
    /// platform read this SubNode as Disconnected. The beat is identified by the sensor's
    /// reserved DTMI, which the enrichment stage resolves; see <see cref="Heartbeat"/>.
    /// </remarks>
    private TelemetryMeasure CreateMeasure() => new()
    {
        ResourceId = _sensor.ResourceId,
        Value = true
    };
}
