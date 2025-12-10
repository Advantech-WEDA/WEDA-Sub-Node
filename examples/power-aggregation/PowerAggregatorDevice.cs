using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PowerAggregationExample.Aggregation;
using PowerAggregationExample.Communication;
using PowerAggregationExample.Devices;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;

namespace PowerAggregationExample;

/// <summary>
/// Pre-configured power aggregator device that calculates P = V × I from voltage and current sensors.
/// Automatically creates AggregatorCommunication and PowerAggregatorDefinition.
///
/// Architecture: Device -> Parser -> Communication
/// Inheritance: PowerAggregatorDevice -> AggregatorDevice -> PubSubDeviceBase -> DeviceBase
///
/// Layered Responsibility (following TwseStockMonitorDevice pattern):
/// - AggregatorDevice: Creates Parser (protocol layer)
/// - PowerAggregatorDevice: Creates Communication (transport layer) + PowerAggregatorDefinition (aggregation logic)
///
/// Telemetry flow:
/// Source Devices → DataProcessed → PowerAggregatorDefinition (cache + sync) →
/// OnTelemetryReceived → SensorCache → Interval Sample → EnqueueTelemetryAsync → Batch Send
/// </summary>
public class PowerAggregatorDevice : AggregatorDevice
{
    /// <summary>
    /// Creates a PowerAggregatorDevice with ApplicationContext only.
    /// Automatically retrieves configuration from context and creates communication.
    /// </summary>
    public PowerAggregatorDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration
            ?? throw new InvalidOperationException("DeviceConfiguration not found in ApplicationContext"))
    {
    }

    /// <summary>
    /// Creates a PowerAggregatorDevice with explicit configuration.
    /// Automatically creates AggregatorCommunication with PowerAggregatorDefinition.
    /// </summary>
    public PowerAggregatorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateAggregatorCommunication(context, configuration))
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    /// <summary>
    /// Creates the AggregatorCommunication with PowerAggregatorDefinition for power calculation.
    /// </summary>
    private static AggregatorCommunication CreateAggregatorCommunication(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        // Parse external sources configuration
        var externalSources = ParseExternalSources(context, configuration);

        // Create PowerAggregatorDefinition with the external sources
        var aggregatorDefinition = new PowerAggregatorDefinition(
            externalSources,
            context.GetLogger<PowerAggregatorDefinition>());

        context.GetLogger<PowerAggregatorDevice>().LogInformation(
            "Created PowerAggregatorDefinition with {Count} external sources: {Sources}",
            externalSources.Count,
            string.Join(", ", externalSources.Select(s => $"{s.GetDisplayName()}[{s.SourceKey}]")));

        // Create AggregatorCommunication
        return new AggregatorCommunication(
            context.DeviceRegistry,
            aggregatorDefinition,
            context.GetLogger<AggregatorCommunication>());
    }

    /// <summary>
    /// Parses ExternalSources configuration from appsettings.json.
    /// </summary>
    private static List<ExternalDataSource> ParseExternalSources(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        if (context.Configuration == null)
        {
            throw new InvalidOperationException("IConfiguration not found in application context");
        }

        var deviceName = configuration.DeviceName;
        var deviceSection = context.Configuration.GetSection($"DeviceConfigs:{deviceName}:Communication:ExternalSources");

        if (!deviceSection.Exists())
        {
            throw new InvalidOperationException(
                $"ExternalSources not found at DeviceConfigs:{deviceName}:Communication:ExternalSources. " +
                "Please add Communication.ExternalSources array to appsettings.json");
        }

        var externalSources = new List<ExternalDataSource>();
        deviceSection.Bind(externalSources);

        if (externalSources.Count == 0)
        {
            throw new InvalidOperationException(
                $"No valid ExternalSources found in DeviceConfigs:{deviceName}:Communication:ExternalSources");
        }

        return externalSources;
    }

    /// <summary>
    /// Event handler for telemetry data received from device.
    /// Logs the power calculation result.
    /// </summary>
    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        var voltage = e.Data.FirstOrDefault(m => m.ResourceId.Contains("voltage"))?.Value;
        var current = e.Data.FirstOrDefault(m => m.ResourceId.Contains("current"))?.Value;
        var power = e.Data.FirstOrDefault(m => m.ResourceId.Contains("power"))?.Value;

        if (voltage != null && current != null && power != null)
        {
            _logger.LogInformation(
                "Power calculation: {Voltage:F2} V × {Current:F2} A = {Power:F2} W",
                voltage, current, power);
        }
    }
}
