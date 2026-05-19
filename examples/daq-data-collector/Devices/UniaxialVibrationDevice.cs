using daq_data_collector.Communication;
using daq_data_collector.Communication.Pipeline;
using daq_data_collector.Protocols;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Devices;

namespace daq_data_collector.Devices;

/// <summary>
/// Uniaxial vibration measurement device backed by a single-axis accelerometer.
/// Inherits StreamingDeviceBase directly (streaming pattern).
/// Assembles DaqCommunication + DaqMetricsParser in the constructor,
/// and handles configuration validation and stream lifecycle.
/// Registers PhmFeatureTransform on the raw payload sensor for telemetry pipeline integration.
/// </summary>
public class UniaxialVibrationDevice : StreamingDeviceBase
{
    public UniaxialVibrationDevice(IWedaApplicationContext context, string configKey)
        : this(context, context[configKey])
    {
    }

    public UniaxialVibrationDevice(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
        : base(context, configuration, CreateStreamingParser(context, configuration))
    {
        EnableDataReceivedTracking = true;
        DataReceived += OnDataReceived;
        DataProcessed += OnDataProcessed;

        // ✓ Apply global TelemetryInterval to sensors that use default Report.Interval
        ApplyDefaultTelemetryInterval(configuration);

        // Note: RegisterPhmTransform is deferred to OnAfterInitializeAsync 
        // because ResourceId is not yet generated (UUID) at this point
    }

    // ── Extraction ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Extract the raw vibration sensor from configuration.
    /// </summary>
    private static Sensor? ExtractRawSensor(DeviceConfiguration configuration)
    {
        return configuration.Sensors?.FirstOrDefault(s =>
            s.SensorInfo?.DisplayName?.Contains("Raw Vibration") ?? false);
    }

    /// <summary>
    /// Extract PHM sensor name-to-Sensor mapping from configuration.
    /// </summary>
    private static Dictionary<string, Sensor> ExtractPhmSensorMapping(DeviceConfiguration configuration)
    {
        var phMSensorNames = new[]
        {
            "timestamp_timestamp", "device_time",
            "x_axis_rms_mg", "x_axis_peak_mg", "x_axis_peak_to_peak_displacement", "x_axis_oa_velocity",
            "x_axis_deviation", "x_axis_skewness", "x_axis_kurtosis", "x_axis_crest_factor"
        };

        return phMSensorNames
            .Select(sensorName => new
            {
                Name = sensorName,
                Sensor = configuration.Sensors?.FirstOrDefault(s =>
                    s.Name.Equals(sensorName, StringComparison.OrdinalIgnoreCase))
            })
            .Where(x => x.Sensor != null)
            .ToDictionary(x => x.Name, x => x.Sensor!);
    }

    /// <summary>
    /// Apply the global TelemetryInterval from Properties to all sensors
    /// that don't have an explicitly configured interval.
    /// This allows simplified configuration by omitting Report.Interval for each sensor.
    /// </summary>
    private static void ApplyDefaultTelemetryInterval(DeviceConfiguration configuration)
    {
        var telemetryIntervalMs = configuration.Properties.TryGetValue("TelemetryInterval", out var ti)
            ? int.Parse(ti!.ToString()!)
            : 1000;  // Fallback default

        foreach (var sensor in configuration.Sensors)
        {
            // Only override if the sensor uses the default SensorReport.Interval value (1000)
            // This preserves explicitly configured intervals
            if (Math.Abs(sensor.Report.Interval - 1000.0) < 0.001)
            {
                sensor.Report.Interval = telemetryIntervalMs;
            }
        }
    }

    // ── Assembly ──────────────────────────────────────────────────────────────

    private static DaqMetricsParser CreateStreamingParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var loggerFactory = context.LoggerFactory;

        // Extract and cache raw sensor early (used for DaqMetricsParser)
        var rawSensor = ExtractRawSensor(configuration);

        // Read DAQ parameters from devicecfg.json Properties block
        var props = configuration.Properties;
        var samplingRate = props.TryGetValue("AccelerationSamplingRate", out var sr)
            ? int.Parse(sr!.ToString()!) : 2500;

        // ✓ Read TelemetryInterval for deriving FrameIntervalSeconds
        var telemetryIntervalMs = props.TryGetValue("TelemetryInterval", out var ti)
            ? int.Parse(ti!.ToString()!) : 1000;

        var decimationFactor = props.TryGetValue("DecimationFactor", out var df)
            ? int.Parse(df!.ToString()!) : 2;

        // ✓ Derive FrameIntervalSeconds from TelemetryInterval and DecimationFactor
        // FrameIntervalSeconds = TelemetryInterval / (1000 * DecimationFactor)
        var frameIntervalSeconds = (double)telemetryIntervalMs / (1000.0 * decimationFactor);
        var frameSize = (int)(samplingRate * frameIntervalSeconds);

        // Log the derived parameters for debugging
        var logger = loggerFactory.CreateLogger<UniaxialVibrationDevice>();
        logger.LogInformation(
            "DAQ Configuration: SamplingRate={samplingRate}Hz, TelemetryInterval={telemetryInterval}ms, " +
            "DecimationFactor={decimationFactor} → FrameIntervalSeconds={frameInterval:F4}s, FrameSize={frameSize}",
            samplingRate, telemetryIntervalMs, decimationFactor, frameIntervalSeconds, frameSize);

        var daqConfig = configuration.DeviceCommunication;
        var daqModuleDeviceNumber = daqConfig.TryGetValue("DaqModuleDeviceNumber", out var dmdn) ? int.Parse(dmdn!.ToString()!) : 0;

        var collector = new DaqCollector(
            samplingRate: samplingRate,
            frameSize: frameSize,
            frameIntervalSeconds: frameIntervalSeconds,
            daqModuleDeviceNumber: daqModuleDeviceNumber,
            logger: loggerFactory.CreateLogger<DaqCollector>());

        var communication = new DaqCommunication(
            collector,
            loggerFactory.CreateLogger<DaqCommunication>());

        return new DaqMetricsParser(
            communication,
            loggerFactory.CreateLogger<DaqMetricsParser>(),
            decimationFactor,
            rawSensor);  // Pass only the raw sensor (extracted earlier), not the entire Sensors list
    }

    /// <summary>
    /// Build PHM transform configuration including sensor mappings and raw sensor ResourceId.
    /// Returns a tuple of (PHM sensor mappings, raw sensor ResourceId).
    /// </summary>
    private (Dictionary<string, string> PhMSensorResourceIds, string RawSensorResourceId)? BuildPhmResourceMapping(DeviceConfiguration configuration)
    {
        // Extract sensors using centralized helper methods
        var rawSensor = ExtractRawSensor(configuration);
        if (rawSensor == null)
        {
            _logger.LogWarning("Raw DAQ payload sensor not found in configuration");
            return null;
        }

        var sensorMapping = ExtractPhmSensorMapping(configuration);
        if (sensorMapping.Count == 0)
        {
            _logger.LogWarning("No PHM sensors found in configuration");
            return null;
        }

        // Extract ResourceIds from sensor mapping and log them
        var phMSensorResourceIds = sensorMapping
            .Select(kvp =>
            {
                _logger.LogDebug("PHM sensor '{name}' ResourceId: {resourceId}", kvp.Key, kvp.Value.ResourceId);
                return kvp;
            })
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ResourceId);

        return (phMSensorResourceIds, rawSensor.ResourceId);
    }

    /// <summary>
    /// Register PhmFeatureTransform on the raw payload sensor.
    /// This connects the transform pipeline to emit PHM features from raw DAQ frames.
    /// </summary>
    private void RegisterPhmTransform(
        IWedaApplicationContext context,
        DeviceConfiguration configuration,
        (Dictionary<string, string> PhMSensorResourceIds, string RawSensorResourceId) resourceMapping)
    {
        try
        {
            var loggerFactory = context.LoggerFactory;

            // Recalculate frameSize (same logic as CreateStreamingParser)
            var props = configuration.Properties;
            var samplingRate = props.TryGetValue("AccelerationSamplingRate", out var sr) ? int.Parse(sr!.ToString()!) : 2500;
            var frameIntervalSeconds = props.TryGetValue("FrameIntervalSeconds", out var fis) ? double.Parse(fis!.ToString()!) : 1.0;
            var frameSize = (int)(samplingRate * frameIntervalSeconds);

            // Get the raw payload sensor to access its Report property
            var rawSensor = ExtractRawSensor(configuration);

            if (rawSensor == null)
            {
                _logger.LogWarning("Raw DAQ payload sensor not found in configuration");
                return;
            }

            // Create and register the transform
            var transform = new PhmFeatureTransform(
                samplingRate: samplingRate,
                fftSize: frameSize,  // FftSize = FrameSize in current implementation
                rawPayloadResourceId: resourceMapping.RawSensorResourceId,  // Pass actual UUID from config
                phMSensorResourceIds: resourceMapping.PhMSensorResourceIds,  // Pass PHM output sensor ResourceIds (UUIDs)
                logger: loggerFactory.CreateLogger<PhmFeatureTransform>());

            rawSensor.Report?.AddTransform(transform);

            _logger.LogInformation(
                "PhmFeatureTransform registered on raw sensor: SamplingRate={rate} Hz, FrameSize={frameSize}, RawSensorResourceId={resourceId}, PHM sensors={phMCount}",
                samplingRate, frameSize, resourceMapping.RawSensorResourceId, resourceMapping.PhMSensorResourceIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering PhmFeatureTransform");
            throw;
        }
    }

    // ── Lifecycle hooks ───────────────────────────────────────────────────────

    protected override async Task OnAfterInitializeAsync(CancellationToken ct)
    {
        // ResourceId is now UUID (after EnrichConfiguration)
        // Build PHM transform config first, then register transform if config is valid
        var resourceMapping = BuildPhmResourceMapping(Configuration);
        if (resourceMapping != null)
        {
            RegisterPhmTransform(_context, Configuration, resourceMapping.Value);
        }

        // Continue with base initialization (e.g., start stream if configured to auto-start)
        await base.OnAfterInitializeAsync(ct);
    }

    protected override Task OnBeforeInitializeAsync(CancellationToken ct)
    {
        _logger.LogInformation("Validating UniaxialVibrationDevice configuration...");

        if (string.IsNullOrWhiteSpace(Configuration.DeviceName))
            throw new InvalidOperationException("DeviceName is required but not configured.");

        var props = Configuration.Properties;

        // Validate AccelerationSamplingRate
        if (props.TryGetValue("AccelerationSamplingRate", out var sr) &&
            int.TryParse(sr?.ToString(), out var samplingRate) &&
            samplingRate <= 0)
            throw new InvalidOperationException($"AccelerationSamplingRate must be a positive integer, got: {samplingRate}");

        // Validate DecimationFactor
        if (props.TryGetValue("DecimationFactor", out var df) &&
            int.TryParse(df?.ToString(), out var decimationFactor) &&
            decimationFactor <= 0)
            throw new InvalidOperationException($"DecimationFactor must be a positive integer, got: {decimationFactor}");

        // Validate FrameIntervalSeconds
        if (props.TryGetValue("FrameIntervalSeconds", out var fis) &&
            double.TryParse(fis?.ToString(), out var frameIntervalSeconds) &&
            frameIntervalSeconds <= 0)
            throw new InvalidOperationException($"FrameIntervalSeconds must be a positive number, got: {frameIntervalSeconds}");

        var daqConfig = Configuration.DeviceCommunication;
        if (daqConfig.TryGetValue("DaqModuleDeviceNumber", out var dmdn) &&
            int.TryParse(dmdn?.ToString(), out var daqModule) &&
            daqModule < 0)
            throw new InvalidOperationException($"DaqModuleDeviceNumber must be non-negative, got: {daqModule}");

        // Validate required sensors are configured
        if (Configuration.Sensors == null || Configuration.Sensors.Count == 0)
            throw new InvalidOperationException("No sensors configured in devicecfg.json");

        var rawSensor = ExtractRawSensor(Configuration);
        if (rawSensor == null)
            throw new InvalidOperationException("Raw DAQ payload sensor ('daqraw:vibration:payload') is required but not configured");

        var phMSensors = Configuration.Sensors.Count(s => s.Report?.Enabled ?? false);
        _logger.LogInformation("Sensor configuration validated: {count} sensors configured for reporting", phMSensors);

        _logger.LogInformation("Configuration validation passed.");
        return base.OnBeforeInitializeAsync(ct);
    }

    protected override ConfigurationValidationResult ValidateConfigurationUpdate(
        SubNodeConfigUpdateMessage message)
    {
        var baseResult = base.ValidateConfigurationUpdate(message);
        if (!baseResult.IsValid)
            return baseResult;

        return ConfigurationValidationResult.Success;
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnDataReceived(object? sender, DataReceivedEvent e)
    {
        _logger.LogDebug("Telemetry received: {Count} measures", e.Data.Count);
    }

    private void OnDataProcessed(object? sender, DataProcessedEvent e)
    {
        _logger.LogDebug("Telemetry processed: {Count} measures", e.Data.Count);
    }

    ~UniaxialVibrationDevice()
    {
        DataReceived -= OnDataReceived;
        DataProcessed -= OnDataProcessed;
    }
}
