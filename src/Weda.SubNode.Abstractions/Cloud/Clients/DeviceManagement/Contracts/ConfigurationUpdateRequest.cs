using Weda.SubNode.Abstractions.Cloud.Clients.Common;

namespace Weda.SubNode.Abstractions.Cloud.Clients.DeviceManagement.Contracts;

/// <summary>
/// Configuration update request (received from cloud - desired state)
/// </summary>
public class ConfigurationUpdateRequest : Request<ConfigUpdateDataDto>
{
    /// <summary>
    /// Create a new configuration update request
    /// </summary>
    public static ConfigurationUpdateRequest Create(ConfigUpdateDataDto data)
    {
        return Create<ConfigurationUpdateRequest>(data);
    }
}

/// <summary>
/// Configuration update data
/// </summary>
public record ConfigUpdateDataDto(
    string DeviceId,
    string ConfigVersion,
    string UpdateType,                    // "full" or "incremental"
    ConfigStateDto Cfg,
    ConfigUpdateMetadataDto? Metadata);

/// <summary>
/// Configuration state wrapper (desired/reported pattern)
/// </summary>
public record ConfigStateDto(
    DesiredConfigDto? Desired,
    ReportedConfigDto? Reported);

/// <summary>
/// Desired configuration from cloud
/// </summary>
public record DesiredConfigDto(
    IReadOnlyList<SensorConfigUpdateDto>? Sensors,
    CommunicationConfigDto? Communication,
    SystemConfigDto? System);

/// <summary>
/// Reported configuration from device
/// </summary>
public record ReportedConfigDto(
    IReadOnlyList<SensorConfigReportDto>? Sensors,
    CommunicationConfigDto? Communication,
    SystemConfigDto? System,
    DeviceStatusDto? DeviceStatus);

/// <summary>
/// Sensor configuration update (from cloud)
/// </summary>
public record SensorConfigUpdateDto(
    string SensorId,
    bool Enabled,
    int Interval,
    DspConfigDto? DspConfig,
    ThresholdsDto? Thresholds,
    CalibrationDto? Calibration);

/// <summary>
/// Sensor configuration report (to cloud)
/// </summary>
public record SensorConfigReportDto(
    string SensorId,
    bool Enabled,
    int Interval,
    DspConfigDto? DspConfig,
    ThresholdsDto? Thresholds,
    CalibrationDto? Calibration,
    string Status,                        // "active", "inactive", "error"
    string? ErrorMessage,
    DateTimeOffset? LastUpdateTime);

/// <summary>
/// DSP (Digital Signal Processing) configuration
/// </summary>
public record DspConfigDto(
    string Type,                          // "kalman", "movingAverage", "lowPass", etc.
    bool Enabled,
    Dictionary<string, object> Parameters);

/// <summary>
/// Sensor threshold configuration
/// </summary>
public record ThresholdsDto(
    double? UpperWarning,
    double? UpperCritical,
    double? LowerWarning,
    double? LowerCritical);

/// <summary>
/// Sensor calibration configuration
/// </summary>
public record CalibrationDto(
    double Offset,
    double ScaleFactor,
    DateTimeOffset? LastCalibrationDate);

/// <summary>
/// Communication configuration
/// </summary>
public record CommunicationConfigDto(
    int ConnectionTimeout,
    int RetryAttempts,
    int RetryInterval,
    bool EnableCompression,
    int BatchSize,
    string BatchTimeout);                 // ISO 8601 duration (e.g., "PT30S")

/// <summary>
/// System configuration
/// </summary>
public record SystemConfigDto(
    string LogLevel,                      // "debug", "info", "warning", "error"
    bool EnableHealthReporting,
    string HealthReportInterval,          // ISO 8601 duration
    bool EnableMetricsCollection,
    string MetricsReportInterval);        // ISO 8601 duration

/// <summary>
/// Device status (reported only)
/// </summary>
public record DeviceStatusDto(
    string State,                         // "connected", "disconnected", "error"
    DateTimeOffset LastConnectedTime,
    string? FirmwareVersion,
    double? CpuUsage,
    double? MemoryUsage,
    double? Temperature);

/// <summary>
/// Configuration update metadata
/// </summary>
public record ConfigUpdateMetadataDto(
    string Priority,                      // "high", "normal", "low"
    bool RollbackOnFailure,
    bool ValidationRequired,
    string Source,
    string? Reason);
