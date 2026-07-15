using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace CommandHandlerExample.Commands.SensorRead.Models;

/// <summary>
/// Command to read the latest cached value of a named sensor on demand.
/// Maps to payload: data.deviceCmd = "sensor.read"
/// </summary>
/// <remarks>
/// Cloud → SubNode command structure:
/// <code>
/// {
///   "deviceCmd": "sensor.read",
///   "timeout": 10,
///   "respTopic": "...",
///   "parameters": {
///     "deviceName": "MyFirstDevice",
///     "sensorName": "temperature_sensor"
///   }
/// }
/// </code>
/// </remarks>
[DeviceCmd("sensor.read")]
[Display(Name = "Read Sensor Value")]
[Description("Read the latest cached telemetry value of a named sensor.")]
public class SensorReadCommand : CommandData<SensorReadParameters>
{
}

/// <summary>
/// Parameters for the sensor.read command.
/// </summary>
public class SensorReadParameters
{
    /// <summary>
    /// Target device name (optional).
    /// If null or empty, all registered devices are searched for the sensor.
    /// </summary>
    [JsonPropertyName("deviceName")]
    [Display(Name = "Target Device")]
    public string? DeviceName { get; init; }

    /// <summary>
    /// The sensor name to read (must match a sensor name in devicecfg.json).
    /// </summary>
    [JsonPropertyName("sensorName")]
    [Display(Name = "Sensor Name")]
    [Required(ErrorMessage = "Sensor name is required")]
    public string SensorName { get; init; } = string.Empty;
}
