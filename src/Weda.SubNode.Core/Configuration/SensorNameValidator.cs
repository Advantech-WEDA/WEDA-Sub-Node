using System.Text.RegularExpressions;
using ErrorOr;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Core.Configuration;

/// <summary>
/// Validates sensor names for IoTDB compatibility.
/// This validator ensures sensor names can be used as IoTDB identifiers.
/// </summary>
public static partial class SensorNameValidator
{
    /// <summary>
    /// Regex pattern for valid IoTDB identifiers.
    /// Rules:
    /// 1. Must start with a letter or underscore
    /// 2. Can only contain letters, digits, and underscores
    /// </summary>
    [GeneratedRegex(@"^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex IoTDBIdentifierPattern();

    /// <summary>
    /// Validates a sensor name for IoTDB compatibility.
    /// </summary>
    /// <param name="sensorName">The sensor name to validate</param>
    /// <returns>True if the sensor name is valid for IoTDB, false otherwise</returns>
    public static bool IsValidSensorName(string sensorName)
    {
        if (string.IsNullOrEmpty(sensorName))
            return false;

        return IoTDBIdentifierPattern().IsMatch(sensorName);
    }

    /// <summary>
    /// Validates a sensor name and returns an error message if invalid.
    /// </summary>
    /// <param name="sensorName">The sensor name to validate</param>
    /// <param name="errorMessage">The error message if validation fails, null otherwise</param>
    /// <returns>True if the sensor name is valid, false otherwise</returns>
    public static bool TryValidate(string sensorName, out string? errorMessage)
    {
        if (string.IsNullOrEmpty(sensorName))
        {
            errorMessage = "Sensor name cannot be empty";
            return false;
        }

        if (!IoTDBIdentifierPattern().IsMatch(sensorName))
        {
            errorMessage = $"Sensor name '{sensorName}' is invalid for IoTDB. " +
                "Name must start with a letter or underscore, and contain only letters, digits, and underscores.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Validates all sensors in a device configuration.
    /// Returns ErrorOr with list of invalid sensor names and their error messages.
    /// </summary>
    /// <param name="sensors">The sensors to validate</param>
    /// <param name="deviceName">The device name for error message context</param>
    /// <returns>Success if all sensors are valid, or Error with validation details</returns>
    public static ErrorOr<Success> ValidateAll(IEnumerable<Sensor> sensors, string deviceName)
    {
        var errors = new List<Error>();

        foreach (var sensor in sensors)
        {
            if (!TryValidate(sensor.Name, out var errorMessage))
            {
                errors.Add(Error.Validation(
                    code: "SensorName.Invalid",
                    description: $"Device '{deviceName}': {errorMessage}"));
            }
        }

        return errors.Count > 0
            ? errors
            : Result.Success;
    }
}
