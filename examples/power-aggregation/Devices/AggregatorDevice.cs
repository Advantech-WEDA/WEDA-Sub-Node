using Microsoft.Extensions.Logging;
using PowerAggregationExample.Communication;
using PowerAggregationExample.Protocols;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Core.Devices;
using DefinitionFactory = PowerAggregationExample.Protocols.AggregatorProtocolParser.DefinitionFactory;

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
    /// Initializes a new instance of AggregatorDevice with config key.
    /// Uses DeviceConfiguration from context.DeviceConfigs[configKey].
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configKey">The configuration key from appsettings.json DeviceConfigs section.</param>
    /// <param name="definitionFactory">Factory to create IAggregatorDefinition for each sensor.</param>
    public AggregatorDevice(
        IWedaApplicationContext context,
        string configKey,
        DefinitionFactory definitionFactory)
        : this(context, context[configKey], definitionFactory)
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
    /// 1. Creating AggregatorCommunication
    /// 2. Creating AggregatorProtocolParser
    /// 3. Registering all sensors and their external sources
    /// </summary>
    private static AggregatorProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        DefinitionFactory definitionFactory)
    {
        var logger = context.LoggerFactory.CreateLogger<AggregatorDevice>();

        // Validate configuration
        if (configuration.Sensors.Count == 0)
        {
            throw new InvalidOperationException(
                $"AggregatorDevice requires at least one sensor in configuration. " +
                $"DeviceName: {configuration.DeviceName}");
        }

        // Create AggregatorCommunication (handles device subscriptions)
        var communication = new AggregatorCommunication(
            context.DeviceRegistry,
            context.LoggerFactory.CreateLogger<AggregatorCommunication>());

        // Create the protocol parser
        var parser = new AggregatorProtocolParser(
            communication,
            context.LoggerFactory);

        // Register all sensors and collect external sources
        foreach (var sensor in configuration.Sensors)
        {
            parser.RegisterSensor(sensor, definitionFactory);
            logger.LogDebug(
                "Registered sensor '{SensorName}' with aggregator",
                sensor.Name);
        }

        // Add external sources to communication
        foreach (var source in parser.AllExternalSources)
        {
            communication.AddExternalSource(source);
        }

        logger.LogInformation(
            "Created AggregatorProtocolParser: Definitions={DefinitionCount}, ExternalSources={SourceCount}",
            parser.DefinitionCount,
            parser.AllExternalSources.Count);

        return parser;
    }
}
