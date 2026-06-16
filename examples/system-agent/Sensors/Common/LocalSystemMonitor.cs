using Weda.SubNode.Abstractions.Devices;

namespace SystemAgentExample.Sensors;

/// <summary>
/// Device-type identity for the system-monitor SubNode. Held in one place so
/// every <see cref="Weda.SubNode.Abstractions.Telemetry.IConfigurableSensor{TParameter}"/>
/// implementation under <c>SystemAgentExample.Sensors</c> can reference it without
/// repeating the literal string.
/// </summary>
public static class SystemMonitor
{
    public const string DeviceTypeName = "system-monitor";
}

/// <summary>
/// Transport-layer POCO for the local system monitor. The monitor reads OS
/// counters directly via in-process APIs — there is no network / serial
/// transport — so this carries no fields. Required by
/// <see cref="IConfigurableDevice{TCommunication, TProperties}"/> which expects
/// a non-null reference type with a parameterless constructor.
/// </summary>
public class LocalSystemCommunication { }

/// <summary>
/// Protocol-layer POCO for the local system monitor. The monitor has no
/// protocol-specific settings (every metric family carries its own
/// per-sensor Parameters). Kept empty for the same reason as
/// <see cref="LocalSystemCommunication"/>.
/// </summary>
public class LocalSystemProperties { }

/// <summary>
/// Strong-typed device configuration descriptor for the local system monitor.
/// Combined with the <c>[DeviceType("system-monitor")]</c> attribute on
/// <see cref="SystemAgentExample.LocalSystemAgentDevice"/>, this is what makes
/// <c>system-agent</c> appear in the SDK's <c>devices[]</c> catalog and routes
/// every <c>AddDevice&lt;LocalSystemAgentDevice&gt;("...")</c> registration onto
/// the typed sensor-dtmi dispatch path.
/// </summary>
public class LocalSystemMonitorConfiguration
    : IConfigurableDevice<LocalSystemCommunication, LocalSystemProperties>
{
    public static string DeviceTypeName => SystemMonitor.DeviceTypeName;

    public static string? Description =>
        "Local system metrics collector — reads OS counters (CPU / memory / disk / network / " +
        "GPIO / temperature / voltage / fan-speed / watchdog / thermal-protection / GPU / " +
        "hwinfo / system) without any external transport.";
}
