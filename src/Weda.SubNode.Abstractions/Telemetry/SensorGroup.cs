namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Sensor group enumeration for logical grouping of sensors
/// </summary>
public enum SensorGroup
{
    /// <summary>
    /// Analog Input - Analog sensor readings
    /// Example: ai.channel[0], ai.channel[1]
    /// </summary>
    AI,

    /// <summary>
    /// Analog Output - Analog control outputs
    /// Example: ao.channel[0], ao.channel[1]
    /// </summary>
    AO,

    /// <summary>
    /// Digital Input - Digital sensor inputs
    /// Example: di.channel[0], di.channel[1]
    /// </summary>
    DI,

    /// <summary>
    /// Digital Output - Digital control outputs
    /// Example: do.channel[0], do.channel[1]
    /// </summary>
    DO,

    /// <summary>
    /// Temperature - Temperature sensors
    /// Example: temp.cpu, temp.ambient
    /// </summary>
    TEMP,

    /// <summary>
    /// Power - Power monitoring sensors
    /// Example: pwr.voltage, pwr.current
    /// </summary>
    PWR,

    /// <summary>
    /// System - System resource monitoring
    /// Example: sys.cpu, sys.memory
    /// </summary>
    SYS
}

/// <summary>
/// Extension methods for SensorGroup
/// </summary>
public static class SensorGroupExtensions
{
    /// <summary>
    /// Convert SensorGroup enum to string representation
    /// </summary>
    public static string ToStringValue(this SensorGroup sensorGroup)
    {
        return sensorGroup.ToString();
    }

    /// <summary>
    /// Parse string to SensorGroup enum
    /// </summary>
    public static SensorGroup ParseSensorGroup(string value)
    {
        if (Enum.TryParse<SensorGroup>(value, true, out var result))
        {
            return result;
        }

        throw new ArgumentException($"Unknown sensor group: {value}", nameof(value));
    }

    /// <summary>
    /// Get full name of the sensor group
    /// </summary>
    public static string GetFullName(this SensorGroup sensorGroup)
    {
        return sensorGroup switch
        {
            SensorGroup.AI => "Analog Input",
            SensorGroup.AO => "Analog Output",
            SensorGroup.DI => "Digital Input",
            SensorGroup.DO => "Digital Output",
            SensorGroup.TEMP => "Temperature",
            SensorGroup.PWR => "Power",
            SensorGroup.SYS => "System",
            _ => throw new ArgumentOutOfRangeException(nameof(sensorGroup), sensorGroup, null)
        };
    }

    /// <summary>
    /// Get description of the sensor group
    /// </summary>
    public static string GetDescription(this SensorGroup sensorGroup)
    {
        return sensorGroup switch
        {
            SensorGroup.AI => "Analog sensor readings",
            SensorGroup.AO => "Analog control outputs",
            SensorGroup.DI => "Digital sensor inputs",
            SensorGroup.DO => "Digital control outputs",
            SensorGroup.TEMP => "Temperature sensors",
            SensorGroup.PWR => "Power monitoring sensors",
            SensorGroup.SYS => "System resource monitoring",
            _ => throw new ArgumentOutOfRangeException(nameof(sensorGroup), sensorGroup, null)
        };
    }
}
