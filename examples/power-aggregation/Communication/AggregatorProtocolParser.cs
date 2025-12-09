using ErrorOr;
using Microsoft.Extensions.Logging;
using PowerAggregationExample.Aggregation;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace PowerAggregationExample.Communication;

/// <summary>
/// Protocol parser wrapper for AggregatorCommunication.
/// Adapts the aggregator communication pattern to IRequestResponseProtocolParser interface.
/// </summary>
public class AggregatorProtocolParser : IRequestResponseProtocolParser
{
    private readonly AggregatorCommunication _communication;
    private readonly ILogger<AggregatorProtocolParser> _logger;

    public ICommunication Communication => _communication;
    public string ProtocolName => "Aggregator";
    public IReadOnlyList<string> SupportedDataTypes => new List<string> { "double", "int", "float" };
    public bool SupportsBidirectional => false;

    public AggregatorProtocolParser(
        IDeviceRegistry deviceRegistry,
        IAggregatorDefinition aggregatorDefinition,
        ILogger<AggregatorProtocolParser>? logger = null)
    {
        _communication = new AggregatorCommunication(deviceRegistry, aggregatorDefinition, null);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
            .CreateLogger<AggregatorProtocolParser>();
    }

    public Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var aggregatedData = _communication.GetAggregatedData();

        if (aggregatedData == null)
        {
            _logger.LogDebug("No aggregated data available yet");
            return Task.FromResult(new List<TelemetryMeasure>());
        }

        return Task.FromResult(new List<TelemetryMeasure>(aggregatedData));
    }

    public Task<ErrorOr<object>> ExecuteCommandAsync(DeviceCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AggregatorProtocolParser received command: {Command}", command.DeviceCmd);
        // Aggregator doesn't support commands by default
        return Task.FromResult<ErrorOr<object>>(Error.Failure("Aggregator does not support command execution"));
    }

    public Task<bool> WriteSensorDataAsync(IEnumerable<TelemetryMeasure> measures, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("AggregatorProtocolParser does not support writing sensor data");
        return Task.FromResult(false);
    }

    public void Dispose()
    {
        _communication.Dispose();
        GC.SuppressFinalize(this);
    }
}
