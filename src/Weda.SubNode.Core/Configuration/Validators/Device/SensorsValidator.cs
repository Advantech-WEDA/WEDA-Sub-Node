using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Configuration.Validators;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Configuration.Validators.Device;

/// <summary>
/// Validates sensor configurations.
/// Checks sensor names, intervals, thresholds, and sensor list consistency.
/// </summary>
public class SensorsValidator : IConfigurationPropertyValidator
{
    private static readonly HashSet<string> _validPrimitives = new(StringComparer.OrdinalIgnoreCase)
    {
        "boolean", "date", "dateTime", "double", "duration",
        "float", "integer", "long", "string", "time"
    };

    public string PropertyName => "Sensors";

    public ConfigurationValidationResult Validate(ConfigurationValidationContext context)
    {
        var desiredConfig = context.DesiredConfig;
        var currentConfig = context.CurrentConfig;
        var options = context.Options;

        // If Sensors section is not provided, skip validation
        if (desiredConfig.Sensors == null)
            return ConfigurationValidationResult.Success;

        foreach (var sensor in desiredConfig.Sensors)
        {
            // Validate sensor name (if enabled)
            if (options.ValidateSensors && string.IsNullOrEmpty(sensor.Name))
            {
                return ConfigurationValidationResult.Failure(
                    "Sensor name cannot be empty");
            }

            // Check for unknown sensors (if enabled)
            if (options.RejectUnknownSensors)
            {
                var existingSensor = currentConfig.Sensors.FirstOrDefault(s =>
                    s.Name.Equals(sensor.Name, StringComparison.OrdinalIgnoreCase));

                if (existingSensor == null)
                {
                    return ConfigurationValidationResult.Failure(
                        $"Unknown sensor '{sensor.Name}' in configuration update");
                }
            }

            // Validate sensor config values
            if (options.ValidateSensors && sensor.Config != null)
            {
                if (sensor.Config.Interval <= 0)
                {
                    return ConfigurationValidationResult.Failure(
                        $"Sensor '{sensor.Name}' interval must be greater than 0");
                }

                // Validate thresholds if provided (if enabled)
                if (options.ValidateThresholds && sensor.Config.Thresholds != null)
                {
                    var thresholdResult = ValidateThresholds(sensor.Name, sensor.Config.Thresholds);
                    if (!thresholdResult.IsValid)
                        return thresholdResult;
                }
            }

            var schemaResult = ValidateSchema(sensor);
            if (!schemaResult.IsValid)
                return schemaResult;
        }

        // Check if all sensors are required (if enabled)
        if (options.RequireAllSensors)
        {
            foreach (var existingSensor in currentConfig.Sensors)
            {
                var found = desiredConfig.Sensors.Any(s =>
                    s.Name.Equals(existingSensor.Name, StringComparison.OrdinalIgnoreCase));

                if (!found)
                {
                    return ConfigurationValidationResult.Failure(
                        $"Missing sensor '{existingSensor.Name}' in configuration update " +
                        $"(RequireAllSensors is enabled)");
                }
            }
        }

        return ConfigurationValidationResult.Success;
    }

    private static ConfigurationValidationResult ValidateThresholds(
        string sensorName,
        Abstractions.Cloud.Clients.DeviceManagement.Contracts.SubNodeThresholdsDto thresholds)
    {
        if (thresholds.UpperCritical.HasValue && thresholds.UpperWarning.HasValue &&
            thresholds.UpperCritical < thresholds.UpperWarning)
        {
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensorName}': UpperCritical must be >= UpperWarning");
        }

        if (thresholds.LowerWarning.HasValue && thresholds.LowerCritical.HasValue &&
            thresholds.LowerWarning < thresholds.LowerCritical)
        {
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensorName}': LowerWarning must be >= LowerCritical");
        }

        if (thresholds.UpperWarning.HasValue && thresholds.LowerWarning.HasValue &&
            thresholds.UpperWarning < thresholds.LowerWarning)
        {
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensorName}': UpperWarning must be >= LowerWarning");
        }

        return ConfigurationValidationResult.Success;
    }

    private static ConfigurationValidationResult ValidateSchema(SubNodeSensorReportDto sensor)
    {
        var schema = sensor.SensorInfo?.Schema;

        if (string.IsNullOrWhiteSpace(schema))
        {
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensor.Name}': Schema is required");
        }

        if (!IsValidSchema(schema))
        {
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensor.Name}': Invalid schema '{schema}'. " +
                "Must be DTDL primitive (boolean, double, integer, string, etc.) or MIME type (image/jpeg, application/json, etc.)");
        }

        if (IsMimeType(schema) && !string.IsNullOrEmpty(sensor.Dtmi))
        {
            return ConfigurationValidationResult.Failure(
                $"Sensor '{sensor.Name}': MIME type schema '{schema}' cannot have custom DTMI. " +
                "Please remove Dtmi field.");
        }

        return ConfigurationValidationResult.Success;
    }

    private static bool IsValidSchema(string schema)
    {
        if (_validPrimitives.Contains(schema))
            return true;
        
        if (IsMimeType(schema))
        {
            var parts = schema.Split('/');
            return parts.Length == 2 && 
                !string.IsNullOrWhiteSpace(parts[0]) &&
                !string.IsNullOrWhiteSpace(parts[1]);
        }

        return false;
    }

    private static bool IsMimeType(string schema) => schema.Contains('/');
}
