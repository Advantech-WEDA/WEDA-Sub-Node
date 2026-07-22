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

    /// <summary>
    /// Runtime property: the <c>DeviceTypeName</c> static value of the device's
    /// runtime class when that class implements
    /// <see cref="IConfigurableDevice{TCommunication, TProperties}"/>; null otherwise.
    /// Populated by the host loader from the type registered via
    /// <c>AddDevice&lt;TDevice&gt;("sectionName")</c>. Non-null triggers the typed
    /// sensor-dtmi dispatch path; null falls back to legacy <c>DtdlGenerator</c>.
    /// </summary>
    [JsonIgnore]
    public string? DeviceTypeName { get; set; }

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

    [JsonIgnore]
    private Dictionary<string, Sensor>? _sensorLookup;

    [JsonIgnore]
    private bool _dtdlInitialized;

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
        if (_dtdlInitialized && DtdlInterface != null)
        {
            logger?.LogDebug("DTDL already initialized for device '{DeviceName}', skipping", DeviceName);
            return;
        }

        _dtdlInitialized = true;

        // Typed dispatch takes precedence over both autogen and manual file
        // modes: when a device is strongly-typed (DeviceTypeName resolved from
        // [DeviceType] / IConfigurableDevice via the host loader), the sensor
        // type Interfaces in the catalog ARE the authoritative DTDL — we
        // don't want a stale per-sensor autogen Interface OR a hand-written
        // DtdlPath file overriding them.
        if (!string.IsNullOrEmpty(DeviceTypeName) && TypedSensorDispatch.Resolve is not null)
        {
            GenerateDtdlFromSensors(logger);
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
    /// Builds the per-device autogen Interface and resolves each sensor's dtmi.
    /// <list type="bullet">
    ///   <item>Always: the legacy <c>DtdlGenerator</c> emits one per-device
    ///         <see cref="DtdlInterface"/> with each sensor as a Telemetry
    ///         content (autogen <c>@id</c> per content). WedaNode requires this
    ///         single Interface so the telemetry schema travels with the upload
    ///         payload; it is added to <c>dtdl[]</c> by the mapping layer.</item>
    ///   <item>Typed dispatch (preferred): when the device's runtime class
    ///         implements <see cref="IConfigurableDevice{TComm, TProps}"/>
    ///         (signalled via non-null <see cref="DeviceTypeName"/>) and Core's
    ///         <c>SensorTypeRegistry</c> has installed
    ///         <see cref="TypedSensorDispatch.Resolve"/>, each sensor's
    ///         <see cref="Sensor.Dtmi"/> is overwritten to the matching sensor
    ///         TYPE Interface dtmi for the capabilities mapping. The autogen
    ///         Interface above already captured the autogen dtmis into its
    ///         Telemetry <c>@id</c> strings, so the overwrite does not affect
    ///         it. Type Interfaces (extending <c>Sensor:base</c>) ship via the
    ///         catalog alongside the autogen Interface.</item>
    ///   <item>Untyped fallback: no overwrite. <see cref="Sensor.Dtmi"/> stays
    ///         on the autogen value, matching the legacy single-Interface
    ///         payload. A warning surfaces to drive migration.</item>
    /// </list>
    /// </summary>
    private void GenerateDtdlFromSensors(ILogger? logger = null)
    {
        // Build the per-device autogen Interface FIRST, while sensor.Dtmi still
        // carries the autogen dtmi. WedaNode expects this single Interface
        // (with each sensor as a Telemetry content) so that the telemetry
        // schema travels in the upload payload — typed dispatch below will
        // overwrite sensor.Dtmi to the type Interface dtmi, but the Telemetry
        // @id strings captured here are unaffected by that later mutation.
        DtdlGenerator.PopulateSensorDtmis(Sensors, deviceKey: DeviceName);

        DtdlInterface = DtdlGenerator.GenerateInterface(
            DeviceName,
            Sensors,
            displayName: null,
            description: $"Auto-generated DTDL for {DeviceName}");

        if (!string.IsNullOrEmpty(DeviceTypeName) && TypedSensorDispatch.Resolve is { } resolve)
        {
            // Per-sensor try/catch: one mis-shaped sensor must NOT prevent the
            // remaining sensors from resolving. Failing sensors keep their
            // autogen dtmi (populated above) so the upload payload still has
            // something addressable for them; their failure is logged.
            var resolved = 0;
            var failed = new List<string>();
            foreach (var sensor in Sensors)
            {
                // The reserved heartbeat keeps its platform-owned dtmi. It is the one
                // sanctioned exception to the auto-gen-only DTMI policy: it carries no
                // Parameters, so typed dispatch has nothing to match on, and the dtmi IS
                // its identity — the SDK recognises the liveness beat by that value.
                // Without this it would survive only by resolve() happening to throw.
                if (Heartbeat.IsHeartbeat(sensor))
                {
                    continue;
                }

                try
                {
                    sensor.Dtmi = resolve(DeviceTypeName, sensor);
                    resolved++;
                }
                catch (Exception ex)
                {
                    failed.Add($"{sensor.Name}: {ex.Message}");
                }
            }

            if (failed.Count > 0)
            {
                logger?.LogWarning(
                    "Typed dispatch partial: {Resolved}/{Total} sensors resolved for device " +
                    "'{DeviceName}' (type '{DeviceType}'). {FailCount} fell back to autogen dtmi:\n  - {Failures}",
                    resolved, Sensors.Count, DeviceName, DeviceTypeName, failed.Count,
                    string.Join("\n  - ", failed));
            }
            else
            {
                logger?.LogInformation(
                    "Typed dispatch resolved {SensorCount} sensors for device '{DeviceName}' (type '{DeviceType}')",
                    Sensors.Count, DeviceName, DeviceTypeName);
            }
            return;
        }

        if (!string.IsNullOrEmpty(DeviceTypeName))
        {
            logger?.LogWarning(
                "Device '{DeviceName}' (type '{DeviceType}') is strongly-typed but " +
                "TypedSensorDispatch.Resolve hook is unset — staying on legacy autogen dtmis. " +
                "Ensure SensorTypeRegistry is initialised before configuration load.",
                DeviceName, DeviceTypeName);
        }
        else
        {
            logger?.LogWarning(
                "Device '{DeviceName}' is not strongly-typed (no IConfigurableDevice registered " +
                "for its DeviceConfigs section). Using legacy DtdlGenerator autogen " +
                "({SensorCount} sensors). Migrate via IConfigurableDevice + IConfigurableSensor " +
                "to enable strong-typed catalog.",
                DeviceName, Sensors.Count);
        }
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

    public Sensor? GetSensorById(string sensorResourceId)
    {
        _sensorLookup ??= Sensors.ToDictionary(s => s.ResourceId);
        return _sensorLookup.TryGetValue(sensorResourceId, out var sensor) ? sensor : null;
    }

    /// <summary>
    /// Invalidates the sensor lookup cache.
    /// Call this after modifying the Sensors collection to ensure GetSensorById returns fresh results.
    /// </summary>
    public void InvalidateSensorLookup()
    {
        _sensorLookup = null;
    }
}