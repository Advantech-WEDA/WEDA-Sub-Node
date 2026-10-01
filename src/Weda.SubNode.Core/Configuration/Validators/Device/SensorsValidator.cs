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

    private static readonly HashSet<string> _validMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/json",
        "application/octet-stream",
        "image/png",
        "image/jpeg"
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
            if (options.ValidateSensors)
            {
                if (string.IsNullOrEmpty(sensor.Name))
                {
                    return ConfigurationValidationResult.Failure(
                        "Sensor name cannot be empty");
                }

                var nameResult = ValidateSensorName(sensor.Name);
                if (!nameResult.IsValid)
                    return nameResult;
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
            if (options.ValidateSensors && sensor.Report != null)
            {
                if (sensor.Report.Interval is not >= SensorReport.MinimumIntervalMs)
                {
                    return ConfigurationValidationResult.Failure(
                        $"Sensor '{sensor.Name}' interval must not be less than {SensorReport.MinimumIntervalMs}");
                }

                // Validate thresholds if provided (if enabled)
                if (options.ValidateThresholds && sensor.Report.Thresholds != null)
                {
                    var thresholdResult = ValidateThresholds(sensor.Name, sensor.Report.Thresholds);
                    if (!thresholdResult.IsValid)
                        return thresholdResult;
                }
            }

            // When recording is enabled the interval is mandatory and must already be at least the
            // record floor: the resolved value is reported back to the cloud, whose validator range-
            // checks the interval field regardless of the enabled flag, so the edge must not emit a
            // null or sub-floor value for an enabled record. A disabled record may omit the interval
            // (or send a sub-floor/zero one); the apply path fills it from the report interval and
            // clamps it up to the floor before it is reported.
            if (options.ValidateSensors && sensor.Record is { Enabled: true }
                && sensor.Record.Interval is not >= SensorRecordingConfig.MinimumIntervalMs)
            {
                return ConfigurationValidationResult.Failure(
                    $"Sensor '{sensor.Name}' record interval is required and must be at least {SensorRecordingConfig.MinimumIntervalMs} ms when recording is enabled");
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
        SubNodeThresholdsDto thresholds)
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
                "Must be DTDL primitive (boolean, double, integer, string, etc.) " +
                "or valid MIME type (application/json, application/octet-stream, image/png, image/jpeg)");
        }

        return ConfigurationValidationResult.Success;
    }

    private static bool IsValidSchema(string schema)
    {
        return _validPrimitives.Contains(schema) ||
               _validMimeTypes.Contains(schema);
    }

    /// <summary>
    /// Validates sensor name for IoTDB compatibility using the shared SensorNameValidator.
    /// </summary>
    private static ConfigurationValidationResult ValidateSensorName(string sensorName)
    {
        return !SensorNameValidator.TryValidate(sensorName, out var errorMessage)
            ? ConfigurationValidationResult.Failure(errorMessage!)
            : ConfigurationValidationResult.Success;
    }
}
