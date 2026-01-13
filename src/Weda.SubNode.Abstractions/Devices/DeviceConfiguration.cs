using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
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
    /// DTDL configuration section containing AutoGenEnabled and DtdlPath settings.
    /// </summary>
    public DtdlConfig Dtdl { get; set; } = new();

    /// <summary>
    /// Runtime property: DTDL interface object - auto-generated or loaded from DtdlPath.
    /// This is populated by InitializeDtdl() method.
    /// </summary>
    [JsonIgnore]
    public object? DtdlInterface { get; set; }

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
    /// Raw device configuration JSON for reporting.
    /// - Initialized from devicecfg.json during startup
    /// - Updated from desired.devicecfg after successful config-update
    /// This is used directly in Report to ensure consistency with config files.
    /// </summary>
    [JsonIgnore]
    public JsonElement? RawDeviceCfgJson { get; set; }

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
    /// Initializes DTDL based on Dtdl.AutoGenEnabled setting.
    /// Priority: Dtdl.AutoGenEnabled (device-level) > SubNodeInfo.AutoGenEnabled (subnode-level)
    /// - When AutoGenEnabled=true: Generates DTDL from Sensor definitions and populates Sensor.Dtmi
    /// - When AutoGenEnabled=false: Validates DtdlPath and Sensor.Dtmi are specified, loads from file
    ///
    /// This method is idempotent - calling it multiple times will not regenerate DTDL if already initialized.
    /// </summary>
    /// <param name="basePath">Optional base path for DtdlPath. If not provided, attempts to find solution root directory automatically.</param>
    /// <param name="logger">Optional logger for warnings and info.</param>
    /// <exception cref="InvalidOperationException">Thrown when validation fails (AutoGenEnabled=false without required fields).</exception>
    /// <exception cref="FileNotFoundException">Thrown when DtdlPath file does not exist (AutoGenEnabled=false).</exception>
    public void InitializeDtdl(string? basePath = null, ILogger? logger = null)
    {
        // Idempotency check: Skip if DTDL already initialized
        if (DtdlInterface != null)
        {
            logger?.LogDebug("DTDL already initialized for device '{DeviceName}', skipping", DeviceName);
            return;
        }

        // Device-level Dtdl.AutoGenEnabled takes priority over SubNode-level setting
        var autoGen = Dtdl.AutoGenEnabled || (SubNodeInfo?.AutoGenEnabled ?? false);

        if (autoGen)
        {
            // Auto-generate mode: Generate DTDL from sensors
            GenerateDtdlFromSensors(logger);
        }
        else
        {
            // Manual mode: Validate and load from file
            ValidateManualDtdlConfiguration();
            LoadDtdlFromFile(basePath);
        }
    }

    /// <summary>
    /// Generates DTDL interface from sensor definitions.
    /// Also populates Dtmi for sensors that don't have one.
    /// </summary>
    private void GenerateDtdlFromSensors(ILogger? logger = null)
    {
        // Populate Dtmi for sensors without one
        DtdlGenerator.PopulateSensorDtmis(Sensors);

        // Generate the DTDL interface
        DtdlInterface = DtdlGenerator.GenerateInterface(
            DeviceName,
            Sensors,
            displayName: null,
            description: $"Auto-generated DTDL for {DeviceName}");

        logger?.LogInformation(
            "Auto-generated DTDL for device '{DeviceName}' with {SensorCount} sensors",
            DeviceName,
            Sensors.Count);
    }

    /// <summary>
    /// Validates configuration when AutoGenEnabled is false.
    /// Ensures DtdlPath and all Sensor.Dtmi are specified.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails.</exception>
    private void ValidateManualDtdlConfiguration()
    {
        var errors = DtdlGenerator.ValidateManualDtdlConfiguration(Sensors, Dtdl.DtdlPath);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"DTDL configuration validation failed for device '{DeviceName}':{Environment.NewLine}" +
                $"- {string.Join($"{Environment.NewLine}- ", errors)}{Environment.NewLine}" +
                $"Hint: Set Dtdl.AutoGenEnabled=true to auto-generate DTDL, " +
                $"or provide Dtdl.DtdlPath and Dtmi for each sensor.");
        }
    }

    /// <summary>
    /// Loads and sets the DTDL interface from the configured Dtdl.DtdlPath.
    /// If DtdlPath is null or empty, this method does nothing.
    /// </summary>
    /// <param name="basePath">Optional base path to combine with DtdlPath. If not provided, attempts to find solution root directory automatically.</param>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public void LoadDtdlFromFile(string? basePath = null)
    {
        var dtdlPath = Dtdl.DtdlPath;
        if (string.IsNullOrEmpty(dtdlPath))
            return;

        // If no basePath provided, try to find solution root directory
        basePath ??= Environment.GetEnvironmentVariable("DTDL_BASE_PATH")
                 ?? FindSolutionRoot()
                 ?? AppContext.BaseDirectory;

        var fullPath = Path.Combine(basePath, dtdlPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"DTDL file not found: {fullPath}{Environment.NewLine}" +
                $"DtdlPath: {dtdlPath}{Environment.NewLine}" +
                $"BasePath: {basePath}{Environment.NewLine}" +
                $"Working directory: {Directory.GetCurrentDirectory()}{Environment.NewLine}" +
                $"App base directory: {AppContext.BaseDirectory}",
                fullPath);
        }

        DtdlInterface = DigitalTwin.DtdlInterface.Load(fullPath);
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