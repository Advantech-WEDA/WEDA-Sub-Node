using ErrorOr;
using Microsoft.Extensions.Logging;
using PowerAggregationExample.Communication;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Protocols;

/// <summary>
/// PubSub protocol parser for the aggregator device.
/// Implements IPubSubProtocolParser to work with PubSubDeviceBase.
///
/// Architecture: AggregatorDevice -> AggregatorProtocolParser -> AggregatorCommunication
/// - Parser receives telemetry from Communication and forwards to PubSubDeviceBase
/// - Parser is created by AggregatorDevice (protocol layer)
/// - Communication is provided by subclass like PowerAggregatorDevice (transport layer)
/// </summary>
public class AggregatorProtocolParser : IPubSubProtocolParser
{
    private readonly AggregatorCommunication _communication;
    private readonly ILogger<AggregatorProtocolParser> _logger;

    public ICommunication Communication => _communication;
    public string ProtocolName => "Aggregator";
    public IReadOnlyList<string> SupportedDataTypes => ["double", "int", "float"];
    public bool SupportsBidirectional => false;

    /// <summary>
    /// Event raised when telemetry data is received from the aggregator.
    /// PubSubDeviceBase subscribes to this and pushes data into SensorCache.
    /// </summary>
    public event Action<List<TelemetryMeasure>>? OnTelemetryReceived;

    /// <summary>
    /// Creates an AggregatorProtocolParser with provided communication.
    /// </summary>
    /// <param name="communication">The aggregator communication instance</param>
    /// <param name="loggerFactory">Logger factory</param>
    public AggregatorProtocolParser(
        AggregatorCommunication communication,
        ILoggerFactory? loggerFactory = null)
    {
        var factory = loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        _communication = communication ?? throw new ArgumentNullException(nameof(communication));
        _logger = factory.CreateLogger<AggregatorProtocolParser>();

        // Forward telemetry events from communication to parser
        _communication.OnTelemetryReceived += measures =>
        {
            _logger.LogDebug("Forwarding {Count} aggregated measures to PubSubDeviceBase", measures.Count);
            OnTelemetryReceived?.Invoke(measures);
        };
    }

    /// <summary>
    /// Starts the aggregator subscription by connecting to source devices.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting aggregator parser subscription");
        var connected = await _communication.ConnectAsync(cancellationToken);
        if (!connected)
        {
            _logger.LogError("Failed to start aggregator parser - could not connect to source devices");
            throw new InvalidOperationException("Failed to connect to source devices");
        }
        _logger.LogInformation("Aggregator parser subscription started");
    }

    /// <summary>
    /// Stops the aggregator subscription.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Stopping aggregator parser subscription");
        await _communication.DisconnectAsync(cancellationToken);
        _logger.LogInformation("Aggregator parser subscription stopped");
    }

    /// <summary>
    /// Execute command (not supported by aggregator).
    /// </summary>
    public Task<ErrorOr<object>> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AggregatorProtocolParser received command: {Command}", command.DeviceCmd);
        return Task.FromResult<ErrorOr<object>>(Error.Failure("Aggregator does not support command execution"));
    }

    public void Dispose()
    {
        _communication.Dispose();
        GC.SuppressFinalize(this);
    }
}
