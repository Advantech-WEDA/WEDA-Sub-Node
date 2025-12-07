using System.Text.Json;
using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Device configuration (matches DMA registration payload)
/// </summary>
public class DeviceConfiguration
{
    /// <summary>
    /// Whether this device configuration is enabled (default: true)
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Device ID, derived from DMA or DeviceIdStorage (e.g., "74fe488d5d54-ffff")
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Device name (e.g., "adam4612", "temp-sensor-1")
    /// </summary>
    public required string DeviceName { get; set; }

    /// <summary>
    /// Device type (e.g., "adamEthernet", "modbusRTU")
    /// </summary>
    public required DeviceType DeviceType { get; set; }

    /// <summary>
    /// Device type name - OPTIONAL, defaults to DeviceConfigs key if not specified
    ///
    /// When using ScanDevicesFromConfiguration():
    /// - If empty/null: Uses the configuration key name (e.g., "MyFirstDevice" from DeviceConfigs["MyFirstDevice"])
    /// - If specified: Uses the provided value (supports short names and fully qualified names)
    ///
    /// Examples:
    /// - Config key: "TcpModbusDevice" → Automatically resolves to TcpModbusDevice class
    /// - Config key: "MyFirstDevice" → Searches in YOUR project first
    /// - Explicit: "Weda.SubNode.Devices.Generic.TcpModbusDevice, Weda.SubNode.Devices" → Full qualified name
    ///
    /// Resolution priority:
    /// 1. Fully qualified name (if assembly specified)
    /// 2. Your project assembly (PRIORITY - avoids naming conflicts)
    /// 3. SDK built-in devices (Weda.SubNode.Devices.Generic)
    /// 4. Other dependencies
    /// </summary>
    public string? DeviceTypeName { get; set; }

    /// <summary>
    /// Path to the DTDL JSON file (optional, for configuration).
    /// </summary>
    public string? DtdlPath { get; set; }

    /// <summary>
    /// DTDL (Digital Twin Definition Language) object.
    /// Can be a DtdlInterface or any other structured object.
    /// </summary>
    public object? Dtdl { get; set; }

    /// <summary>
    /// Device capabilities
    /// </summary>
    public required DeviceCapabilities DeviceCapabilities { get; set; }

    /// <summary>
    /// Sensors list
    /// </summary>
    public List<Sensor> Sensors { get; set; } = [];

    /// <summary>
    /// Communication settings (not part of registration payload, for internal use)
    /// e.g., Modbus: { "Host": "192.168.1.10", "Port": 502, "SlaveId": 1 }
    /// </summary>
    public Dictionary<string, object> Communication { get; set; } = [];

    /// <summary>
    /// Connection settings for retry, timeout, and security configuration
    /// Used by communication layer (TCP, Serial, etc.)
    /// Can be configured in appsettings.json or received from cloud
    /// </summary>
    public ConnectionSettings? ConnectionSettings { get; set; }

    /// <summary>
    /// Background task periods (not part of registration payload, for internal use)
    /// </summary>
    public BackgroundTaskPeriods Periods { get; set; } = new();

    /// <summary>
    /// Custom properties (not part of registration payload, for internal use)
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = [];

    [JsonIgnore]
    public DeviceInfo DeviceInfo => new DeviceInfo
    {
        DeviceId = DeviceId,
        DeviceName = DeviceName,
        DeviceType = DeviceType,
        Manufacturer = DeviceCapabilities.Manufacturer,
        Model = DeviceCapabilities.Model
    };

    /// <summary>
    /// Loads and sets the DTDL interface from the configured DtdlPath.
    /// If DtdlPath is null or empty, this method does nothing.
    /// </summary>
    /// <param name="basePath">Optional base path to combine with DtdlPath. If not provided, attempts to find solution root directory automatically.</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is invalid or cannot be deserialized.</exception>
    public void LoadDtdl(string? basePath = null)
    {
        if (string.IsNullOrEmpty(DtdlPath))
            return;

        // If no basePath provided, try to find solution root directory
        basePath ??= Environment.GetEnvironmentVariable("DTDL_BASE_PATH")
                 ?? FindSolutionRoot()
                 ?? AppContext.BaseDirectory;

        var fullPath = Path.Combine(basePath, DtdlPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"DTDL file not found: {fullPath}{Environment.NewLine}" +
                $"DtdlPath: {DtdlPath}{Environment.NewLine}" +
                $"BasePath: {basePath}{Environment.NewLine}" +
                $"Working directory: {Directory.GetCurrentDirectory()}{Environment.NewLine}" +
                $"App base directory: {AppContext.BaseDirectory}",
                fullPath);
        }

        Dtdl = DtdlInterface.Load(fullPath);
    }

    /// <summary>
    /// Finds the solution root directory by searching for .sln or .git directory.
    /// Starts from AppContext.BaseDirectory and walks up the directory tree.
    /// Also checks common container mount points like /workspace.
    /// </summary>
    /// <returns>The solution root directory path, or null if not found.</returns>
    private static string? FindSolutionRoot()
    {
        // Check common container mount point first (for Dev Container compatibility)
        if (Directory.Exists("/workspace") &&
            (Directory.GetFiles("/workspace", "*.sln").Length > 0 ||
             Directory.Exists("/workspace/.git")))
        {
            return "/workspace";
        }

        // Walk up from current directory
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            // Check for .sln file or .git directory (common indicators of solution root)
            if (directory.GetFiles("*.sln").Length > 0 ||
                directory.GetDirectories(".git").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

/// <summary>
/// Device capabilities
/// </summary>
public class DeviceCapabilities
{
    /// <summary>
    /// Manufacturer (e.g., "Advantech")
    /// </summary>
    public required string Manufacturer { get; set; }

    /// <summary>
    /// Model (e.g., "SubNode", "ADAM-6052")
    /// </summary>
    public required string Model { get; set; }

    /// <summary>
    /// SubNode software version (e.g., "1.0")
    /// </summary>
    public required string SubNodeSwVersion { get; set; }

    public required Dictionary<string, object> DeviceInfo { get; set; } = [];
}

/// <summary>
/// Background task execution periods (in milliseconds)
/// </summary>
public class BackgroundTaskPeriods
{
    /// <summary>
    /// Default telemetry reading period in milliseconds (default: 5000ms).
    /// Used as fallback when a sensor's Config.Interval is not set (0 or negative).
    /// Each sensor can override this by setting its own Config.Interval value.
    /// </summary>
    public int ReadTelemetry { get; set; } = 5000;

    /// <summary>
    /// Telemetry sending/upload period in milliseconds.
    /// Controls how telemetry data is sent to the cloud:
    /// - 0: Realtime mode - send immediately after each read (default)
    /// - >0: Batch mode - collect data for this duration, then send as a batch
    ///
    /// In batch mode, multiple readings are collected into a List and sent together,
    /// reducing network overhead for high-frequency sensors.
    /// </summary>
    public int SendTelemetry { get; set; } = 0;

    /// <summary>
    /// Health reporting period (default: 60000ms)
    /// </summary>
    public int ReportHealth { get; set; } = 60000;

    /// <summary>
    /// Command polling period (default: 1000ms)
    /// </summary>
    public int PollCommands { get; set; } = 1000;

    /// <summary>
    /// Configuration sync/report period (default: 1800000ms = 30 minutes).
    /// Periodically reports device configuration to cloud
    /// to ensure reported state is synchronized even if update response fails.
    /// Valid range: 300000ms (5 min) ~ 86400000ms (24 hours), or 0 to disable.
    /// </summary>
    public int ReportConfiguration { get; set; } = 1_800_000;
}

/// <summary>
/// Device configurations collection (key-value style)
/// Example appsettings.json:
/// {
///   "Devices": {
///     "temp-sensor-1": { "DeviceId": "...", "DeviceCapabilities": {...}, ... },
///     "fan-1": { ... }
///   }
/// }
/// </summary>
public class DeviceConfigurations : Dictionary<string, DeviceConfiguration>
{
}
