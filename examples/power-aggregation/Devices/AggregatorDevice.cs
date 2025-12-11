using Microsoft.Extensions.Logging;
using PowerAggregationExample.Aggregation;
using PowerAggregationExample.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;

namespace PowerAggregationExample.Devices;

/// <summary>
/// Base class for aggregator devices that combine data from multiple source devices.
/// Inherits from PubSubDeviceBase for PubSub communication pattern.
///
/// Architecture: Device -> Parser -> Communication -> Definitions
/// - AggregatorDevice: Handles parser creation, sensor registration, and communication setup
/// - Subclasses: Only need to provide DefinitionFactory
///
/// All sensors defined in DeviceConfiguration.Sensors are registered with the aggregator.
/// Each sensor's Parameters should define the source mappings for the aggregation logic.
///
/// Usage (subclass example):
/// <code>
/// public class PowerAggregatorDevice : AggregatorDevice
/// {
///     public PowerAggregatorDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
///         : base(context, configuration, CreateDefinitionFactory(context)) { }
///
///     private static DefinitionFactory CreateDefinitionFactory(IWedaApplicationContext context)
///         => sensor => new PowerAggregatorDefinition(sensor, context.GetLogger&lt;PowerAggregatorDefinition&gt;());
/// }
/// </code>
/// </summary>
public class AggregatorDevice : PubSubDeviceBase
{
    /// <summary>
    /// Factory delegate for creating IAggregatorDefinition from a Sensor.
    /// </summary>
    public delegate IAggregatorDefinition DefinitionFactory(Sensor sensor);

    /// <summary>
    /// Initializes a new instance of AggregatorDevice.
    /// Uses DeviceConfiguration from ApplicationContext.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="definitionFactory">Factory to create IAggregatorDefinition for each sensor.</param>
    public AggregatorDevice(
        IWedaApplicationContext context,
        DefinitionFactory definitionFactory)
        : this(
            context,
            context.DeviceConfiguration
                ?? throw new InvalidOperationException("DeviceConfiguration not found in ApplicationContext"),
            definitionFactory)
    {
    }

    /// <summary>
    /// Initializes a new instance of AggregatorDevice with explicit configuration.
    /// All sensors in the configuration will be registered with the aggregator.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="definitionFactory">Factory to create IAggregatorDefinition for each sensor.</param>
    public AggregatorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        DefinitionFactory definitionFactory)
        : base(context, configuration, CreateParser(context, configuration, definitionFactory))
    {
        _logger.LogDebug(
            "AggregatorDevice initialized ({SensorCount} sensors)",
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Creates the AggregatorProtocolParser with communication setup.
    /// Handles:
    /// 1. Creating AggregatorProtocolParser with definition factory
    /// 2. Registering all sensors from configuration
    /// 3. Creating AggregatorCommunication
    /// 4. Wrapping in Protocols.AggregatorProtocolParser for PubSubDeviceBase
    /// </summary>
    private static Protocols.AggregatorProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        DefinitionFactory definitionFactory)
    {
        var logger = context.LoggerFactory.CreateLogger<AggregatorDevice>();

        // Create the aggregation protocol parser with definition factory
        var aggregationParser = new AggregatorProtocolParser(
            sensor => definitionFactory(sensor),
            context.LoggerFactory.CreateLogger<AggregatorProtocolParser>());

        // Register all sensors from configuration
        if (configuration.Sensors.Count == 0)
        {
            throw new InvalidOperationException(
                $"AggregatorDevice requires at least one sensor in configuration. " +
                $"DeviceName: {configuration.DeviceName}");
        }

        foreach (var sensor in configuration.Sensors)
        {
            aggregationParser.RegisterSensor(sensor);
            logger.LogDebug(
                "Registered sensor '{SensorName}' with aggregator",
                sensor.Name);
        }

        logger.LogInformation(
            "Created AggregatorProtocolParser: Definitions={DefinitionCount}, ExternalSources={SourceCount}",
            aggregationParser.DefinitionCount,
            aggregationParser.AllExternalSources.Count);

        // Create AggregatorCommunication with the parser
        var communication = new AggregatorCommunication(
            context.DeviceRegistry,
            aggregationParser,
            context.LoggerFactory.CreateLogger<AggregatorCommunication>());

        // Wrap in protocol parser for PubSubDeviceBase
        return new Protocols.AggregatorProtocolParser(
            communication,
            context.LoggerFactory);
    }
}
