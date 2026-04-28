using daq_data_collector.Communication;
using daq_data_collector.Communication.Pipeline;
using daq_data_collector.Protocols;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;
using Weda.SubNode.Abstractions.Configuration;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Events;
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

        // Register PhmFeatureTransform on the raw payload sensor (C6 integration)
        RegisterPhmTransform(context, configuration);
    }

    // ── Assembly ──────────────────────────────────────────────────────────────

    private static DaqMetricsParser CreateStreamingParser(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        var loggerFactory = context.LoggerFactory;

        // Read DAQ parameters from devicecfg.json Properties block
        var props = configuration.Properties;
        var samplingRate = props.TryGetValue("AccelerationSamplingRate", out var sr) ? int.Parse(sr!.ToString()!) : 2500;
        var frameIntervalSeconds = props.TryGetValue("FrameIntervalSeconds", out var fis) ? double.Parse(fis!.ToString()!) : 1.0;
        var frameSize = (int)(samplingRate * frameIntervalSeconds);
        var decimationFactor = props.TryGetValue("DecimationFactor", out var df) ? int.Parse(df!.ToString()!) : 2;

        var daqConfig = configuration.DeviceCommunication;
        var daqModuleDeviceNumber = daqConfig.TryGetValue("DaqModuleDeviceNumber", out var dmdn) ? int.Parse(dmdn!.ToString()!) : 0;

        var collector = new DaqCollector(
            samplingRate: samplingRate,
            frameSize: frameSize,
            daqModuleDeviceNumber: daqModuleDeviceNumber,
            logger: loggerFactory.CreateLogger<DaqCollector>());

        var communication = new DaqCommunication(
            collector,
            loggerFactory.CreateLogger<DaqCommunication>());

        return new DaqMetricsParser(
            communication,
            loggerFactory.CreateLogger<DaqMetricsParser>(),
            decimationFactor);
    }

    /// <summary>
    /// Register PhmFeatureTransform on the raw payload sensor.
    /// This connects the transform pipeline to emit PHM features from raw DAQ frames.
    /// </summary>
    private void RegisterPhmTransform(
        IWedaApplicationContext context,
        DeviceConfiguration configuration)
    {
        try
        {
            var loggerFactory = context.LoggerFactory;

            // Recalculate frameSize (same logic as CreateStreamingParser)
            var props = configuration.Properties;
            var samplingRate = props.TryGetValue("AccelerationSamplingRate", out var sr) ? int.Parse(sr!.ToString()!) : 2500;
            var frameIntervalSeconds = props.TryGetValue("FrameIntervalSeconds", out var fis) ? double.Parse(fis!.ToString()!) : 1.0;
            var frameSize = (int)(samplingRate * frameIntervalSeconds);

            // Find the raw payload sensor
            var rawSensor = Configuration.Sensors?.FirstOrDefault(s => 
                s.SensorInfo?.DisplayName?.Contains("Raw Vibration") ?? false);

            if (rawSensor == null)
            {
                _logger.LogWarning("Raw DAQ payload sensor not found in configuration");
                return;
            }

            // Create and register the transform
            var transform = new PhmFeatureTransform(
                samplingRate: samplingRate,
                fftSize: frameSize,  // FftSize = FrameSize in current implementation
                logger: loggerFactory.CreateLogger<PhmFeatureTransform>());

            rawSensor.Report?.AddTransform(transform);

            _logger.LogInformation(
                "PhmFeatureTransform registered on raw sensor: SamplingRate={rate} Hz, FrameSize={frameSize}",
                samplingRate, frameSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering PhmFeatureTransform");
            throw;
        }
    }

    // ── Lifecycle hooks ───────────────────────────────────────────────────────

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

        var rawSensor = Configuration.Sensors.FirstOrDefault(s => 
            s.SensorInfo?.DisplayName?.Contains("Raw Vibration") ?? false);
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

    ~UniaxialVibrationDevice()
    {
        DataReceived -= OnDataReceived;
    }
}
