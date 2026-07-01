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
        var acquisitionRate = props.TryGetValue("AcquisitionRateHz", out var ar)
            ? int.Parse(ar!.ToString()!) : 1000;

        var observationWindowSeconds = props.TryGetValue("ObservationWindowSeconds", out var ow)
            ? double.Parse(ow!.ToString()!) : 1.0;
        var observationWindowMs = (int)(observationWindowSeconds * 1000);
        var frameSize = (int)(acquisitionRate * observationWindowSeconds);
        var freqResolutionHz = 1.0 / observationWindowSeconds;
        var nyquistHz = acquisitionRate / 2.0;

        // Log the derived parameters at startup
        var logger = loggerFactory.CreateLogger<UniaxialVibrationDevice>();
        logger.LogInformation(
            "DAQ Configuration: AcquisitionRate={acquisitionRate}Hz, ObservationWindow={observationWindow}s, " +
            "FrameSize={frameSize}, FreqResolution={freqResolution:F3}Hz, Nyquist={nyquist}Hz",
            acquisitionRate, observationWindowSeconds, frameSize, freqResolutionHz, nyquistHz);

        // Log per-sensor reporting cadence
        foreach (var sensor in configuration.Sensors)
        {
            if (sensor.Report?.Enabled ?? false)
            {
                var intervalMs = sensor.Report.Interval;
                if (intervalMs < observationWindowMs || intervalMs % observationWindowMs != 0)
                    logger.LogWarning(
                        "Sensor '{name}': Report.Interval={interval}ms is not a positive integer multiple of ObservationWindow={window}ms",
                        sensor.Name, intervalMs, observationWindowMs);
            }
        }

        var daqConfig = configuration.DeviceCommunication;
        var daqModuleDeviceNumber = daqConfig.TryGetValue("DaqModuleDeviceNumber", out var dmdn) ? int.Parse(dmdn!.ToString()!) : 0;

        var collector = new DaqCollector(
            samplingRate: acquisitionRate,
            frameSize: frameSize,
            frameIntervalSeconds: observationWindowSeconds,
            daqModuleDeviceNumber: daqModuleDeviceNumber,
            logger: loggerFactory.CreateLogger<DaqCollector>());

        var communication = new DaqCommunication(
            collector,
            loggerFactory.CreateLogger<DaqCommunication>());

        return new DaqMetricsParser(
            communication,
            loggerFactory.CreateLogger<DaqMetricsParser>(),
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
            var acquisitionRate = props.TryGetValue("AcquisitionRateHz", out var ar) ? int.Parse(ar!.ToString()!) : 1000;
            var observationWindowSeconds = props.TryGetValue("ObservationWindowSeconds", out var ow) ? double.Parse(ow!.ToString()!) : 1.0;
            var frameSize = (int)(acquisitionRate * observationWindowSeconds);

            // Get the raw payload sensor to access its Report property
            var rawSensor = ExtractRawSensor(configuration);

            if (rawSensor == null)
            {
                _logger.LogWarning("Raw DAQ payload sensor not found in configuration");
                return;
            }

            // Build per-sensor upload interval map from devicecfg.json Report.Interval
            var sensorIntervals = ExtractPhmSensorMapping(configuration)
                .ToDictionary(kvp => kvp.Key, kvp => (int)kvp.Value.Report.Interval);

            // Create and register the transform
            var transform = new PhmFeatureTransform(
                samplingRate: acquisitionRate,
                fftSize: frameSize,  // FftSize = FrameSize in current implementation
                rawPayloadResourceId: resourceMapping.RawSensorResourceId,  // Pass actual UUID from config
                phMSensorResourceIds: resourceMapping.PhMSensorResourceIds,  // Pass PHM output sensor ResourceIds (UUIDs)
                sensorIntervalMs: sensorIntervals,
                observationWindowMs: (int)(observationWindowSeconds * 1000),
                logger: loggerFactory.CreateLogger<PhmFeatureTransform>());

            // Align the raw sensor's interval loop cadence to observationWindowMs so the transform
            // fires every observation window (e.g. 1000ms) instead of Report.Interval (e.g. 2000ms).
            // This does NOT cause raw payload to be uploaded: the transform output REPLACES the raw
            // measure in TelemetryPipeline.ExecuteTransformStageAsync (result.AddRange(current)).
            // GroupSensorsByInterval() is called in OnStartAsync, after this OnAfterInitializeAsync
            // hook, so this runtime override is reflected in the actual interval loop timing.
            rawSensor.Report.Interval = (int)(observationWindowSeconds * 1000);
            rawSensor.Report?.AddTransform(transform);

            _logger.LogInformation(
                "PhmFeatureTransform registered on raw sensor: AcquisitionRate={rate} Hz, FrameSize={frameSize}, " +
                "PHM sensors={phMCount}, Intervals=[{intervals}]",
                acquisitionRate, frameSize, resourceMapping.PhMSensorResourceIds.Count,
                string.Join(", ", sensorIntervals.Select(kv => $"{kv.Key}:{kv.Value}ms")));
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

        // Validate AcquisitionRateHz (error only if present and invalid; missing → uses default 1000)
        if (props.TryGetValue("AcquisitionRateHz", out var ar) &&
            int.TryParse(ar?.ToString(), out var acquisitionRate) &&
            acquisitionRate <= 0)
            throw new InvalidOperationException($"AcquisitionRateHz must be a positive integer, got: {acquisitionRate}");

        // Validate ObservationWindowSeconds and compute effective window
        var effectiveWindowMs = 1000;
        if (props.TryGetValue("ObservationWindowSeconds", out var ow))
        {
            if (!double.TryParse(ow?.ToString(), out var owSeconds) || owSeconds <= 0)
                throw new InvalidOperationException($"ObservationWindowSeconds must be a positive number, got: {ow}");
            effectiveWindowMs = (int)(owSeconds * 1000);
        }

        // Validate per-sensor Report.Interval (must be a positive integer multiple of observation window)
        foreach (var sensor in Configuration.Sensors.Where(s => s.Report?.Enabled ?? false))
        {
            var intervalMsRounded = (int)Math.Round(sensor.Report!.Interval);
            if (intervalMsRounded <= 0 || intervalMsRounded < effectiveWindowMs || intervalMsRounded % effectiveWindowMs != 0)
                throw new InvalidOperationException(
                    $"Sensor '{sensor.Name}': Report.Interval={sensor.Report.Interval}ms must be a positive integer multiple of ObservationWindow={effectiveWindowMs}ms");
        }

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

        // Derive current effective observation window
        var props = Configuration.Properties;
        var effectiveWindowMs = 1000;
        if (props.TryGetValue("ObservationWindowSeconds", out var ow) &&
            double.TryParse(ow?.ToString(), out var owSeconds) && owSeconds > 0)
            effectiveWindowMs = (int)(owSeconds * 1000);

        // Validate per-sensor Report.Interval in the incoming update
        var desiredConfig = message.Data?.Cfg?.Desired?.SubNodeDeviceConfig?.DeviceConfigs?
            .GetValueOrDefault(Configuration.DeviceName);
        foreach (var sensor in desiredConfig?.Sensors?.Where(s => s.Report?.Enabled == true) ?? [])
        {
            var intervalMs = sensor.Report!.Interval;
            if (intervalMs <= 0)
                return ConfigurationValidationResult.Failure(
                    $"Sensor '{sensor.Name}': Report.Interval must be positive, got: {intervalMs}");
            if (intervalMs < effectiveWindowMs || intervalMs % effectiveWindowMs != 0)
                return ConfigurationValidationResult.Failure(
                    $"Sensor '{sensor.Name}': Report.Interval={intervalMs}ms must be a positive integer multiple of ObservationWindow={effectiveWindowMs}ms");
        }

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
