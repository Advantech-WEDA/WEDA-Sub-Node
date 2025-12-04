using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Devices;

/// <summary>
/// Base class for Publish-Subscribe (Message Broker) communication pattern devices.
/// Framework handles ReadTelemetryAsync and StartBackgroundTasksAsync internally.
/// User only needs to provide an IPublishSubscribeProtocolParser implementation.
///
/// Use this class for protocols like: MQTT, NATS, AMQP, Kafka, WebSocket
///
/// Inheritance hierarchy example:
/// MyFirstISensingDevice -> MqttISensingDevice -> ISensingDevice -> MessageBrokerDeviceBase -> DeviceBase
/// </summary>
public class MessageBrokerDeviceBase : DeviceBase
{
    /// <summary>
    /// The protocol parser for this device. Protected to allow subclass access.
    /// </summary>
    protected readonly IPublishSubscribeProtocolParser _parser;
    private readonly ConcurrentDictionary<string, TelemetryMeasure> _latestData = new();
    private CancellationTokenSource? _backgroundTasksCts;
    private Task? _subscriptionTask;
    private Task? _healthTask;

    /// <summary>
    /// Initializes a new instance of MessageBrokerDeviceBase.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration.</param>
    /// <param name="parser">Publish-Subscribe protocol parser (Parser owns Communication).</param>
    public MessageBrokerDeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        IPublishSubscribeProtocolParser parser)
        : base(context, configuration, parser.Communication)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        _logger.LogDebug(
            "MessageBrokerDeviceBase initialized with {ParserType}",
            _parser.GetType().Name);
    }

    /// <summary>
    /// Reads telemetry from the device cache.
    /// Returns the latest received data from message broker subscriptions.
    /// Framework implementation - returns cached data from OnTelemetryReceived events.
    /// Sealed to prevent subclasses from overriding framework logic.
    /// </summary>
    public sealed override Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var measures = _latestData.Values.ToList();

        if (measures.Count > 0)
        {
            _logger.LogDebug("Returning {Count} cached telemetry measures", measures.Count);
            RaiseDataReceived(measures);
        }

        return Task.FromResult(measures);
    }

    /// <summary>
    /// Executes a command on the device using the parser.
    /// </summary>
    public override async Task<bool> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Executing command {CommandName} on device {DeviceId}", command.DeviceCmd, DeviceId);

        var result = await _parser.ExecuteCommandAsync(command, cancellationToken);

        if (result.IsError)
        {
            _logger.LogWarning("Command execution failed: {Errors}",
                string.Join(", ", result.Errors.Select(e => e.Description)));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts background tasks for message broker subscription and health reporting.
    /// Framework implementation with automatic event handling.
    /// Sealed to prevent subclasses from overriding framework logic.
    /// </summary>
    protected sealed override Task StartBackgroundTasksAsync(CancellationToken cancellationToken)
    {
        _backgroundTasksCts = new CancellationTokenSource();
        var cts = _backgroundTasksCts.Token;

        // Subscribe to parser's OnTelemetryReceived event
        _parser.OnTelemetryReceived += OnTelemetryReceived;

        _subscriptionTask = Task.Run(async () =>
        {
            try
            {
                _logger.LogDebug("Starting message broker subscription");

                // Start parser subscription
                await _parser.StartAsync(cts);

                _logger.LogInformation("Message broker subscription established");

                // Keep task alive to maintain subscription
                await Task.Delay(Timeout.Infinite, cts);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                _logger.LogInformation("Subscription task cancelled for device {DeviceId}", DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in subscription task for device {DeviceId}", DeviceId);
            }
            finally
            {
                // Clean up subscription
                try
                {
                    await _parser.StopAsync(CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping parser subscription");
                }
            }
        }, cts);

        _healthTask = Task.Run(async () =>
        {
            var period = Configuration.Periods.ReportHealth;
            _logger.LogDebug("Starting health reporting task with period {Period}ms", period);

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    await ReportHealthAsync(cts);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in health reporting task for device {DeviceId}", DeviceId);
                }

                await Task.Delay(period, cts);
            }
        }, cts);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles telemetry data received from parser subscription.
    /// Updates cache and sends telemetry through pipeline.
    /// </summary>
    private void OnTelemetryReceived(List<TelemetryMeasure> measures)
    {
        try
        {
            // Update cache with latest values
            foreach (var measure in measures)
            {
                _latestData.AddOrUpdate(measure.ResourceId, measure, (_, _) => measure);
            }

            _logger.LogDebug("Received {Count} telemetry measures from subscription", measures.Count);

            // Raise data received event
            RaiseDataReceived(measures);

            // Record telemetry in health monitor
            _orchestrator.HealthMonitor.RecordTelemetryReadDuration(TimeSpan.Zero);

            // Send telemetry through pipeline (fire-and-forget for push model)
            _ = SendTelemetryAsync(measures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling received telemetry for device {DeviceId}", DeviceId);
        }
    }
}
