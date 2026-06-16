namespace Weda.SubNode.Abstractions.Telemetry;

/// <summary>
/// Abstractions-side hook that lets <see cref="Devices.DeviceConfiguration.InitializeDtdl"/>
/// call into Core's <c>SensorTypeRegistry</c> without taking a hard dependency
/// on Core (which would invert the project graph).
///
/// <para>Core's <c>SensorTypeRegistry</c> sets <see cref="Resolve"/> at startup
/// (when its lazy registry is first materialised). When unset (Abstractions-only
/// consumers / no Core), typed dispatch is unavailable and
/// <c>DeviceConfiguration.InitializeDtdl</c> falls back to the legacy
/// <c>DtdlGenerator</c> path.</para>
/// </summary>
public static class TypedSensorDispatch
{
    /// <summary>
    /// Given a typed device's <c>DeviceTypeName</c> and one of its sensor
    /// instances, returns the dtmi of the matching sensor TYPE Interface.
    /// Throws when zero / multiple sensor types match (POCO discriminator bug).
    /// </summary>
    public static Func<string, Sensor, string>? Resolve { get; set; }
}
