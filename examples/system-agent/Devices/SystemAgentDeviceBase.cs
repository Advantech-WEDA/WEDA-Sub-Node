using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SystemAgentExample.Communication;
using SystemAgentExample.Protocols;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;

namespace SystemAgentExample.Devices;

/// <summary>
/// Base class for system agent devices that are coupled to local metrics collection.
/// Responsible for assembling the configuration, LocalSystemCommunication, and the 
/// SystemMetricsParser into a functional Request-Response device structure 
/// that collects CPU, memory, disk, and network metrics.
/// </summary>
public class SystemAgentDeviceBase : RequestResponseDeviceBase
{
    /// <summary>
    /// Initializes a new instance of SystemAgentDeviceBase.
    /// Sensors without specific resource identifiers (Interface, PinId, MetricName)
    /// are auto-expanded into per-resource sensors before device initialization.
    /// </summary>
    /// <param name="context">Application context managing all framework services.</param>
    /// <param name="configuration">Device configuration containing sensor settings.</param>
    /// <param name="communication">Communication instance for system metrics collection.</param>
    public SystemAgentDeviceBase(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
        : base(context, ExpandSensors(context, configuration, communication), CreateParser(context, configuration, communication))
    {
        _logger.LogInformation(
            "SystemAgentDevice initialized ({SensorCount} sensors after expansion)",
            configuration.Sensors.Count);
    }

    /// <summary>
    /// Creates the SystemMetricsParser for this device.
    /// Parser uses the provided communication instance.
    /// </summary>
    private static IRequestResponseProtocolParser CreateParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
    {
        var loggerFactory = context.LoggerFactory;
        return new SystemMetricsParser(
            configuration,
            communication,
            loggerFactory.CreateLogger<SystemMetricsParser>());
    }

    /// <summary>
    /// Discovers available system resources and expands template sensors
    /// into per-resource sensors before passing to the base constructor.
    /// </summary>
    private static DeviceConfiguration ExpandSensors(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        LocalSystemCommunication communication)
    {
        var logger = context.LoggerFactory.CreateLogger<SystemAgentDeviceBase>();
        var resources = communication.DiscoverAvailableResources();

        logger.LogInformation(
            "Discovered resources — Network: [{Networks}], GPIO: [{Gpio}], Temperature: [{Temp}]",
            string.Join(", ", resources.NetworkInterfaces),
            string.Join(", ", resources.GpioPins),
            string.Join(", ", resources.TemperatureSources));

        // Recover array values lost during IConfiguration Dictionary<string, object> binding
        // by reading the raw IConfigurationSection children (e.g., "Sensors:0:Parameters:Interfaces:0")
        var sensorsSection = context.Configuration?
            .GetSection($"DeviceConfig:DeviceConfigs:{configuration.DeviceName}:Sensors");

        ParameterNormalizer.Normalize(configuration.Sensors, sensorsSection);

        configuration.Sensors = SensorExpander.Expand(configuration.Sensors, resources, logger);

        // Update RawDeviceCfgJson so shadow reports the expanded sensor list
        // (device mgmt needs to see individual sensors, not the template)
        UpdateRawDeviceCfgWithExpandedSensors(configuration, logger);

        return configuration;
    }

    /// <summary>
    /// Rebuilds RawDeviceCfgJson with the expanded sensor list so that
    /// shadow report contains the actual runtime sensors (not the unexpanded templates).
    /// </summary>
    private static void UpdateRawDeviceCfgWithExpandedSensors(
        DeviceConfiguration configuration, ILogger logger)
    {
        if (!configuration.RawDeviceCfgJson.HasValue)
            return;

        try
        {
            var rawJson = configuration.RawDeviceCfgJson.Value;

            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                foreach (var prop in rawJson.EnumerateObject())
                {
                    if (prop.Name == "DeviceConfigs")
                    {
                        writer.WritePropertyName("DeviceConfigs");
                        writer.WriteStartObject();

                        foreach (var deviceConfigProp in prop.Value.EnumerateObject())
                        {
                            if (deviceConfigProp.Name == configuration.DeviceName)
                            {
                                // Replace this device's Sensors with expanded list
                                writer.WritePropertyName(deviceConfigProp.Name);
                                writer.WriteStartObject();

                                foreach (var field in deviceConfigProp.Value.EnumerateObject())
                                {
                                    if (field.Name == "Sensors")
                                    {
                                        writer.WritePropertyName("Sensors");
                                        WriteSensorsAsDeviceCfgFormat(writer, configuration.Sensors);
                                    }
                                    else
                                    {
                                        field.WriteTo(writer);
                                    }
                                }

                                writer.WriteEndObject();
                            }
                            else
                            {
                                deviceConfigProp.WriteTo(writer);
                            }
                        }

                        writer.WriteEndObject();
                    }
                    else
                    {
                        prop.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            }

            stream.Position = 0;
            using var doc = JsonDocument.Parse(stream);
            configuration.RawDeviceCfgJson = doc.RootElement.Clone();

            logger.LogDebug("Updated RawDeviceCfgJson with {Count} expanded sensors", configuration.Sensors.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update RawDeviceCfgJson with expanded sensors");
        }
    }

    /// <summary>
    /// Writes sensors in the same JSON format as devicecfg.json:
    /// Only Name, SensorGroup (string), Parameters, Report (Enabled+Interval), SensorInfo (Schema+Description+DisplayName).
    /// </summary>
    private static void WriteSensorsAsDeviceCfgFormat(Utf8JsonWriter writer, List<Sensor> sensors)
    {
        writer.WriteStartArray();

        foreach (var sensor in sensors)
        {
            writer.WriteStartObject();

            writer.WriteString("Name", sensor.Name);
            writer.WriteString("SensorGroup", sensor.SensorGroup.ToString());

            // Parameters
            writer.WritePropertyName("Parameters");
            writer.WriteStartObject();
            if (sensor.Parameters != null)
            {
                foreach (var kvp in sensor.Parameters)
                {
                    writer.WriteString(kvp.Key, kvp.Value?.ToString() ?? "");
                }
            }
            writer.WriteEndObject();

            // Report
            writer.WritePropertyName("Report");
            writer.WriteStartObject();
            writer.WriteBoolean("Enabled", sensor.Report.Enabled);
            writer.WriteNumber("Interval", sensor.Report.Interval);
            writer.WriteEndObject();

            // SensorInfo
            writer.WritePropertyName("SensorInfo");
            writer.WriteStartObject();
            writer.WriteString("Schema", sensor.SensorInfo.Schema);
            writer.WriteString("Description", sensor.SensorInfo.Description);
            writer.WriteString("DisplayName", sensor.SensorInfo.DisplayName);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }
}
