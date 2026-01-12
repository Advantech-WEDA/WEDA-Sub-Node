namespace Weda.SubNode.Abstractions.Configuration;

/// <summary>
/// Defines the update mode for configuration updates.
/// </summary>
public enum ConfigUpdateMode
{
    /// <summary>
    /// REPLACE mode (default): Replace the entire configuration with the provided payload.
    /// Requires complete payload - all devices and sensors must be present.
    /// This mode is used for Shadow-based systems that store complete state.
    /// </summary>
    Replace,

    /// <summary>
    /// PATCH mode: Only update fields that are explicitly provided.
    /// Missing fields retain their current values.
    /// Use this mode when you want to allow partial configuration updates.
    /// </summary>
    Patch
}

/// <summary>
/// Options for controlling configuration update validation behavior.
/// Use this to enable/disable specific validation checks based on device requirements.
/// </summary>
public record ConfigUpdateOptions
{
    /// <summary>
    /// Default options with whitelist-style behavior.
    /// Allows partial updates - only provided fields are updated.
    /// Unknown sensors are ignored, partial sensor updates are allowed.
    /// </summary>
    public static ConfigUpdateOptions Default => new();

    /// <summary>
    /// Strict options for REPLACE mode (Shadow-compatible).
    /// Requires complete payload with all devices and sensors present.
    /// Unknown sensors will cause validation to fail.
    /// </summary>
    public static ConfigUpdateOptions Strict => new()
    {
        UpdateMode = ConfigUpdateMode.Replace,
        RejectUnknownSensors = true,
        RequireAllSensors = true
    };

    /// <summary>
    /// Alias for Default (backward compatibility).
    /// </summary>
    public static ConfigUpdateOptions Relaxed => Default;

    /// <summary>
    /// The update mode: Replace (complete payload) or Patch (partial updates).
    /// Default: Replace
    /// </summary>
    public ConfigUpdateMode UpdateMode { get; init; } = ConfigUpdateMode.Replace;

    /// <summary>
    /// Validates that DeviceName in the update matches the current device.
    /// Default: true
    /// </summary>
    public bool ValidateDeviceName { get; init; } = true;

    /// <summary>
    /// Validates that period values (ReadTelemetry, SendTelemetry, ReportHealth, ReportConfiguration) are valid.
    /// Default: true
    /// </summary>
    public bool ValidatePeriods { get; init; } = true;

    /// <summary>
    /// Minimum allowed value for ReportConfiguration period in milliseconds.
    /// Default: 300000 (5 minutes)
    /// </summary>
    public int ReportConfigurationMinMs { get; init; } = 300_000;

    /// <summary>
    /// Maximum allowed value for ReportConfiguration period in milliseconds.
    /// Default: 86400000 (24 hours)
    /// </summary>
    public int ReportConfigurationMaxMs { get; init; } = 86_400_000;

    /// <summary>
    /// Validates sensor configurations (name not empty, interval non-negative).
    /// Default: true
    /// </summary>
    public bool ValidateSensors { get; init; } = true;

    /// <summary>
    /// Validates threshold consistency (UpperCritical >= UpperWarning >= LowerWarning >= LowerCritical).
    /// Default: true
    /// </summary>
    public bool ValidateThresholds { get; init; } = true;

    /// <summary>
    /// Rejects configuration updates that contain sensors not in the current configuration.
    /// When false, unknown sensors are silently ignored during apply.
    /// When true, unknown sensors will cause validation to fail.
    /// Default: false (allows whitelist-style partial updates)
    /// </summary>
    public bool RejectUnknownSensors { get; init; } = false;

    /// <summary>
    /// Requires that all existing sensors must be present in the update.
    /// When true, partial updates (only some sensors) will fail validation.
    /// Default: false (allows whitelist-style partial updates)
    /// </summary>
    public bool RequireAllSensors { get; init; } = false;

    /// <summary>
    /// Validates Transform and DSP Filter pipeline parameters before applying updates.
    /// When true, calls ValidateParameters on IConfigurableTransform and IConfigurableDspFilter
    /// implementations to verify parameter validity before the update is applied.
    /// This enables early rejection of invalid parameters with "invalid" status
    /// instead of "updating" → "failed" sequence.
    /// Default: true
    /// </summary>
    public bool ValidatePipelineParameters { get; init; } = true;
}