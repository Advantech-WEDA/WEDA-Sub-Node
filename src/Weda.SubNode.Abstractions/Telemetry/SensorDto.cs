namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Data transfer object for sensor information in recording context.
/// Contains only essential fields needed for recording API responses.
/// </summary>
/// <param name="ResourceId">Sensor Resource ID (full UUID, e.g., "21af0dc4-5389-a7dd-df64d7cf782c")</param>
/// <param name="ShortId">Short ID derived from the last 5 characters of ResourceId (e.g., "f782c")</param>
/// <param name="Name">Sensor name or channel identifier (e.g., "ai.channel[0]", "temperature.sensor")</param>
/// <param name="Record">Sensor recording configuration for local storage.</param>
/// <param name="DeviceResourceId">Reference to parent device resource ID (e.g., "74fe488d5d54-ffff")</param>
public record RecordingSensorDto(
    string ResourceId,
    string ShortId,
    string Name,
    SensorRecordingConfig Record,
    string DeviceResourceId
)
{
    /// <summary>
    /// Creates a RecordingSensorDto from a Sensor entity.
    /// </summary>
    public static RecordingSensorDto FromSensor(Sensor sensor) => new(
        ResourceId: sensor.ResourceId,
        ShortId: sensor.ShortId,
        Name: sensor.Name,
        Record: sensor.Record,
        DeviceResourceId: sensor.DeviceResourceId
    );
}