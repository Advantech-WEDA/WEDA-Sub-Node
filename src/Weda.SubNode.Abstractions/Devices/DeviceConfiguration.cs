using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.DigitalTwin;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Device configuration for appsettings.json binding.
/// DeviceName is derived from DeviceConfigs key.
/// Manufacturer, Model, SwVersion are inherited from SubNode section.
/// </summary>
public class DeviceConfiguration
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Auto-populated during device initialization.
    /// </summary>
    public string? DeviceId { get; set; }

    /// <summary>
    /// Auto-set from DeviceConfigs key (e.g., "MyFirstDevice").
    /// </summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Path to DTDL JSON file. Only used when SubNode.AutoGenDtdl is false.
    /// </summary>
    public string? DtdlPath { get; set; }

    /// <summary>
    /// DTDL object - auto-generated or loaded from DtdlPath.
    /// </summary>
    public object? Dtdl { get; set; }

    public List<Sensor> Sensors { get; set; } = [];

    /// <summary>
    /// Transport layer settings (Host, Port, BrokerUrl, etc.)
    /// </summary>
    public Dictionary<string, object> DeviceCommunication { get; set; } = [];

    public ConnectionSettings? ConnectionSettings { get; set; }

    public BackgroundTaskPeriods Periods { get; set; } = new();

    /// <summary>
    /// Protocol-specific settings (e.g., SlaveId for Modbus)
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = [];

    /// <summary>
    /// Device metadata for DTDL generation and cloud reporting.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = [];

    #region Runtime Properties

    /// <summary>
    /// Populated during device initialization from SubNode section.
    /// </summary>
    [JsonIgnore]
    public SubNodeInfo? SubNodeInfo { get; set; }

    [JsonIgnore]
    public string Manufacturer => SubNodeInfo?.Manufacturer ?? "Unknown";

    [JsonIgnore]
    public string Model => SubNodeInfo?.Model ?? "Unknown";

    [JsonIgnore]
    public string SwVersion => SubNodeInfo?.SwVersion ?? "1.0.0";

    [JsonIgnore]
    public SubNodeType SubNodeType => SubNodeInfo?.SubNodeType ?? SubNodeType.CustomDevice;

    [JsonIgnore]
    public DeviceInfo DeviceInfo => new()
    {
        DeviceId = DeviceId,
        DeviceName = DeviceName,
        SubNodeType = SubNodeType,
        Manufacturer = Manufacturer,
        Model = Model
    };

    #endregion

    /// <summary>
    /// Loads and sets the DTDL interface from the configured DtdlPath.
    /// If DtdlPath is null or empty, this method does nothing.
    /// </summary>
    /// <param name="basePath">Optional base path to combine with DtdlPath. If not provided, attempts to find solution root directory automatically.</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
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