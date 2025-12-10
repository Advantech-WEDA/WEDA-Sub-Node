using Microsoft.Extensions.Logging;
using PowerAggregationExample.Communication;
using PowerAggregationExample.Protocols;

using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Devices;

namespace PowerAggregationExample.Devices;

/// <summary>
/// Base class for aggregator devices that combine data from multiple source devices.
/// Inherits from PubSubDeviceBase for PubSub communication pattern.
///
/// This class is responsible for creating the Parser layer (AggregatorProtocolParser).
/// Subclasses are responsible for providing the Communication layer and IAggregatorDefinition.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: PowerAggregatorDevice -> AggregatorDevice -> PubSubDeviceBase -> DeviceBase
///
/// Layered Responsibility (following StockMonitorDevice pattern):
/// - AggregatorDevice: Creates Parser (protocol layer)
/// - PowerAggregatorDevice: Creates Communication (transport layer) + IAggregatorDefinition (aggregation logic)
/// </summary>
public class AggregatorDevice : PubSubDeviceBase
{
    /// <summary>
    /// Initializes a new instance of AggregatorDevice.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="communication">Aggregator communication instance for subscribing to source devices.</param>
    public AggregatorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        AggregatorCommunication communication)
        : base(context, configuration, CreateParser(context, communication))
    {
        _logger.LogDebug(
            "AggregatorDevice initialized ({SensorCount} sensors)",
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Creates the AggregatorProtocolParser for this device.
    /// Parser uses the provided communication.
    /// </summary>
    private static AggregatorProtocolParser CreateParser(
        IWedaApplicationContext context,
        AggregatorCommunication communication)
    {
        return new AggregatorProtocolParser(
            communication,
            context.LoggerFactory);
    }
}
