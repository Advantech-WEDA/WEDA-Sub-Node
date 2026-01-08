using Microsoft.Extensions.Logging;

using PowerAggregationExample.Aggregation;
using PowerAggregationExample.Devices;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

using DefinitionFactory = PowerAggregationExample.Protocols.AggregatorProtocolParser.DefinitionFactory;

namespace PowerAggregationExample;

/// <summary>
/// Power aggregator device that calculates P = V × I from voltage and current sensors.
///
/// This is a simplified subclass that only provides a DefinitionFactory.
/// All parser creation, sensor registration, and communication setup
/// is handled by AggregatorDevice base class.
///
/// Configuration in appsettings.json:
/// <code>
/// {
///   "Sensors": [{
///     "Name": "power001",
///     "Parameters": {
///       "VoltageSource": "VoltageSensor/voltage001",
///       "CurrentSource": "CurrentSensor/current001"
///     }
///   }]
/// }
/// </code>
/// </summary>
public class PowerAggregatorDevice : AggregatorDevice
{
    /// <summary>
    /// Constructor following WedaApplicationBuilder convention.
    /// Required for automatic device instantiation via AddDevice&lt;TDevice&gt;().
    /// </summary>
    /// <param name="context">Application context</param>
    /// <param name="configuration">Device configuration</param>
    public PowerAggregatorDevice(IWedaApplicationContext context, DeviceConfiguration configuration)
        : base(context, configuration, CreateDefinitionFactory(context))
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        foreach (var measure in e.Data)
        {
            _logger.LogInformation(
                "[PowerSensor] DataReceived: ResourceId={ResourceId}, Value={Value:F2}, Timestamp={Timestamp}",
                measure.ResourceId, measure.Value, e.Timestamp);
        }
    }

    /// <summary>
    /// Creates the definition factory for PowerAggregatorDefinition.
    /// </summary>
    private static DefinitionFactory CreateDefinitionFactory(IWedaApplicationContext context)
        => sensor => new PowerAggregatorDefinition(
            sensor,
            context.GetLogger<PowerAggregatorDefinition>());
}
