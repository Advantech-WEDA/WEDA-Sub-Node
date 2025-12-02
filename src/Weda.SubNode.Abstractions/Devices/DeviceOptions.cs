namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Device application-level feature options (Application Layer)
/// Controls which features are enabled for devices
/// </summary>
public class DeviceOptions
{
    /// <summary>
    /// Enable command receiving from cloud (downlink)
    /// When enabled, devices can receive and execute commands from the cloud platform
    /// </summary>
    public bool EnableCommands { get; set; } = false;

    /// <summary>
    /// Enable configuration update receiving from cloud (downlink)
    /// When enabled, devices can receive and apply configuration updates from the cloud platform
    /// </summary>
    public bool EnableConfigUpdates { get; set; } = false;

    /// <summary>
    /// Enable telemetry sending to cloud (uplink)
    /// When enabled, devices will send telemetry data to the cloud platform
    /// </summary>
    public bool EnableTelemetry { get; set; } = false;

    /// <summary>
    /// Enable health reporting to cloud (uplink)
    /// When enabled, devices will send health status reports to the cloud platform
    /// </summary>
    public bool EnableHealthReporting { get; set; } = false;

    /// <summary>
    /// Default timeout for command execution (in milliseconds).
    /// Individual commands can override this via DeviceCommand.Timeout.
    /// Default: 30000ms (30 seconds) as per UC9884 specification.
    /// </summary>
    public int DefaultCommandTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Default device options (all features disabled by default)
    /// </summary>
    public static DeviceOptions Default => new();
}
