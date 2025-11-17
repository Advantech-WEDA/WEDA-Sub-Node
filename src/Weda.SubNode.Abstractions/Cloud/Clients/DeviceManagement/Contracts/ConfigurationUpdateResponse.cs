using Weda.SubNode.Abstractions.Cloud.Clients.Common;
using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Configuration update response (reported state to cloud)
/// </summary>
public class ConfigurationUpdateResponse : Response<ConfigUpdateReportDto>
{
    /// <summary>
    /// Create a new configuration update response with auto-generated audit fields
    /// </summary>
    public static ConfigurationUpdateResponse Create(ConfigUpdateReportDto data, string reqSeqId)
    {
        return new ConfigurationUpdateResponse
        {
            ReqSeqId = reqSeqId,
            RspSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Code = 0,
            Message = "Configuration updated successfully",
            Data = data
        };
    }

    /// <summary>
    /// Create error response
    /// </summary>
    public static ConfigurationUpdateResponse CreateError(string reqSeqId, int errorCode, string errorMessage)
    {
        return new ConfigurationUpdateResponse
        {
            ReqSeqId = reqSeqId,
            RspSeqId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Code = errorCode,
            Message = errorMessage,
            Data = null
        };
    }
}

/// <summary>
/// Configuration update report data (reported state)
/// </summary>
public record ConfigUpdateReportDto(
    string DeviceId,
    string ConfigVersion,
    ConfigStateDto Cfg,
    ConfigUpdateResultDto Result)
{
    /// <summary>
    /// Convert DeviceConfiguration to reported state
    /// </summary>
    public static ConfigUpdateReportDto FromDeviceConfiguration(
        DeviceConfiguration deviceConfig,
        string configVersion,
        ConfigUpdateResultDto result)
    {
        var reportedSensors = deviceConfig.Sensors
            .Select(s => new SensorConfigReportDto(
                SensorId: s.ResourceId,
                Enabled: s.Config.Enabled,
                Interval: (int)s.Config.Interval,
                DspConfig: ConvertDspConfig(s.Config),
                Thresholds: ConvertThresholds(s.Config),
                Calibration: ConvertCalibration(s.Config),
                Status: "active",
                ErrorMessage: null,
                LastUpdateTime: DateTimeOffset.UtcNow))
            .ToList();

        var communicationConfig = new CommunicationConfigDto(
            ConnectionTimeout: 5000,
            RetryAttempts: 3,
            RetryInterval: 1000,
            EnableCompression: false,
            BatchSize: 10,
            BatchTimeout: "PT30S");

        var systemConfig = new SystemConfigDto(
            LogLevel: "info",
            EnableHealthReporting: true,
            HealthReportInterval: TimeSpan.FromMilliseconds(deviceConfig.Periods.ReportHealth).ToString(),
            EnableMetricsCollection: true,
            MetricsReportInterval: "PT60S");

        var deviceStatus = new DeviceStatusDto(
            State: "connected",
            LastConnectedTime: DateTimeOffset.UtcNow,
            FirmwareVersion: deviceConfig.DeviceCapabilities.SubNodeSwVersion,
            CpuUsage: null,
            MemoryUsage: null,
            Temperature: null);

        var reported = new ReportedConfigDto(
            Sensors: reportedSensors,
            Communication: communicationConfig,
            System: systemConfig,
            DeviceStatus: deviceStatus);

        var cfg = new ConfigStateDto(
            Desired: null,
            Reported: reported);

        return new ConfigUpdateReportDto(
            DeviceId: deviceConfig.DeviceId ?? string.Empty,
            ConfigVersion: configVersion,
            Cfg: cfg,
            Result: result);
    }

    private static DspConfigDto? ConvertDspConfig(Weda.SubNode.Abstractions.Telemetry.SensorConfig config)
    {
        // Check if DSP pipeline is configured
        if (config.DspPipeline == null || config.DspPipeline.Count == 0)
        {
            return null;
        }

        // Use first enabled DSP filter (order is array index, no sorting needed)
        var firstFilter = config.DspPipeline
            .FirstOrDefault(f => f.Enabled);

        if (firstFilter == null)
        {
            return null;
        }

        return new DspConfigDto(
            Type: firstFilter.Type,
            Enabled: firstFilter.Enabled,
            Parameters: firstFilter.Parameters);
    }

    private static ThresholdsDto? ConvertThresholds(Weda.SubNode.Abstractions.Telemetry.SensorConfig config)
    {
        // Check if thresholds are configured
        if (config.Thresholds == null)
        {
            return null;
        }

        return new ThresholdsDto(
            UpperWarning: config.Thresholds.UpperWarning,
            UpperCritical: config.Thresholds.UpperCritical,
            LowerWarning: config.Thresholds.LowerWarning,
            LowerCritical: config.Thresholds.LowerCritical);
    }

    private static CalibrationDto? ConvertCalibration(Weda.SubNode.Abstractions.Telemetry.SensorConfig config)
    {
        // Extract calibration from transform pipeline
        var calibrationTransform = config.TransformPipeline
            .FirstOrDefault(t => t.Type.Equals("Calibration", StringComparison.OrdinalIgnoreCase));

        if (calibrationTransform == null || !calibrationTransform.Enabled)
        {
            return null;
        }

        // Extract parameters
        var offset = calibrationTransform.Parameters.TryGetValue("Offset", out var offsetObj)
            ? Convert.ToDouble(offsetObj)
            : 0.0;

        var scaleFactor = calibrationTransform.Parameters.TryGetValue("Scale", out var scaleObj)
            ? Convert.ToDouble(scaleObj)
            : 1.0;

        return new CalibrationDto(
            Offset: offset,
            ScaleFactor: scaleFactor,
            LastCalibrationDate: null); // No longer tracking calibration date
    }
}

/// <summary>
/// Configuration update result
/// </summary>
public record ConfigUpdateResultDto(
    string Status,                        // "success", "partial", "failed"
    string? ErrorMessage,
    IReadOnlyList<SensorUpdateResultDto>? SensorResults,
    DateTimeOffset AppliedTime);

/// <summary>
/// Individual sensor update result
/// </summary>
public record SensorUpdateResultDto(
    string SensorId,
    bool Success,
    string? ErrorMessage);
