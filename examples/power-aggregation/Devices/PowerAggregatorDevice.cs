using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PowerAggregationExample.Aggregation;
using PowerAggregationExample.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Core.Devices;

namespace PowerAggregationExample.Devices;

public class PowerAggregatorDevice : RequestResponseDeviceBase
{
    public PowerAggregatorDevice(IWedaApplicationContext context)
        : this(context, context.DeviceConfiguration
            ?? throw new InvalidOperationException("DeviceConfiguration not found in ApplicationContext"))
    {
    }

    public PowerAggregatorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : this(context, configuration, CreateParser(context, configuration))
    {
    }

    private PowerAggregatorDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        AggregatorProtocolParser parser)
        : base(context, configuration, parser)
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
    }

    private static AggregatorProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        try
        {
            // Get IConfiguration from the context to properly bind the array
            if (context.Configuration == null)
            {
                throw new InvalidOperationException("IConfiguration not found in application context");
            }

            // Find the configuration section for this device
            var deviceName = configuration.DeviceName;
            var deviceSection = context.Configuration.GetSection($"DeviceConfigs:{deviceName}:Communication:ExternalSources");

            if (!deviceSection.Exists())
            {
                throw new InvalidOperationException(
                    $"ExternalSources not found at DeviceConfigs:{deviceName}:Communication:ExternalSources. " +
                    "Please add Communication.ExternalSources array to appsettings.json");
            }

            // Bind the configuration section directly to List<ExternalDataSource>
            var externalSourcesConfig = new List<ExternalDataSource>();
            deviceSection.Bind(externalSourcesConfig);

            if (externalSourcesConfig.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No valid ExternalSources found in DeviceConfigs:{deviceName}:Communication:ExternalSources");
            }

            context.GetLogger<PowerAggregatorDevice>().LogInformation(
                "Successfully parsed {Count} external sources: {Sources}",
                externalSourcesConfig.Count,
                string.Join(", ", externalSourcesConfig.Select(s => s.GetDisplayName())));

            return CreateParserWithSources(context, externalSourcesConfig);
        }
        catch (Exception ex)
        {
            context.GetLogger<PowerAggregatorDevice>().LogError(ex,
                "Failed to parse ExternalSources configuration");
            throw;
        }
    }

    private static AggregatorProtocolParser CreateParserWithSources(
        IWedaApplicationContext context,
        List<ExternalDataSource> externalSourcesConfig)
    {

        // Create aggregator definition
        var aggregatorDefinition = new PowerAggregatorDefinition(
            externalSources: externalSourcesConfig,
            logger: context.LoggerFactory.CreateLogger<PowerAggregatorDefinition>());

        // Create and return parser
        return new AggregatorProtocolParser(
            deviceRegistry: context.DeviceRegistry,
            aggregatorDefinition: aggregatorDefinition,
            logger: context.LoggerFactory.CreateLogger<AggregatorProtocolParser>());
    }

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        // Log the power calculation (if we have all three values)
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
