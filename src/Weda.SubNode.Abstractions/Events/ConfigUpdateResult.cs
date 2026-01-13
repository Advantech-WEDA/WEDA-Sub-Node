using Weda.SubNode.Abstractions.Devices;

namespace Weda.SubNode.Abstractions.Events;

/// <summary>
/// Result of applying a configuration update on a device.
/// Returned to SubNodeManager for centralized report publishing.
/// </summary>
public sealed record ConfigUpdateResult
{
    /// <summary>
    /// The status of the configuration update.
    /// </summary>
    public required DeviceConfigUpdateStatus Status { get; init; }

    /// <summary>
    /// Whether a DTMI delta was detected (requires re-upload of configurations).
    /// </summary>
    public bool HasDtmiDelta { get; init; }

    /// <summary>
    /// Error message when Status is Invalid or Failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// The device configuration after update (for building report).
    /// </summary>
    public required DeviceConfiguration Configuration { get; init; }

    /// <summary>
    /// Device type name for reporting (e.g., "adamEthernet").
    /// </summary>
    public required string DeviceTypeName { get; init; }

    /// <summary>
    /// Creates a result indicating no update was required.
    /// </summary>
    /// <param name="config">The device configuration.</param>
    /// <param name="deviceTypeName">The device type name.</param>
    /// <returns>A ConfigUpdateResult with Skipped status.</returns>
    public static ConfigUpdateResult NoUpdateRequired(DeviceConfiguration config, string deviceTypeName)
        => new()
        {
            Status = DeviceConfigUpdateStatus.Skipped,
            Configuration = config,
            DeviceTypeName = deviceTypeName
        };

    /// <summary>
    /// Creates a result indicating validation failed.
    /// </summary>
    /// <param name="config">The device configuration.</param>
    /// <param name="deviceTypeName">The device type name.</param>
    /// <param name="errorMessage">The validation error message.</param>
    /// <returns>A ConfigUpdateResult with Invalid status.</returns>
    public static ConfigUpdateResult Invalid(DeviceConfiguration config, string deviceTypeName, string errorMessage)
        => new()
        {
            Status = DeviceConfigUpdateStatus.Invalid,
            ErrorMessage = errorMessage,
            Configuration = config,
            DeviceTypeName = deviceTypeName
        };

    /// <summary>
    /// Creates a result indicating the update succeeded.
    /// </summary>
    /// <param name="config">The device configuration.</param>
    /// <param name="deviceTypeName">The device type name.</param>
    /// <param name="hasDtmiDelta">Whether DTMI delta was detected.</param>
    /// <returns>A ConfigUpdateResult with Success status.</returns>
    public static ConfigUpdateResult Success(DeviceConfiguration config, string deviceTypeName, bool hasDtmiDelta = false)
        => new()
        {
            Status = DeviceConfigUpdateStatus.Success,
            HasDtmiDelta = hasDtmiDelta,
            Configuration = config,
            DeviceTypeName = deviceTypeName
        };

    /// <summary>
    /// Creates a result indicating the update failed.
    /// </summary>
    /// <param name="config">The device configuration.</param>
    /// <param name="deviceTypeName">The device type name.</param>
    /// <param name="errorMessage">The failure error message.</param>
    /// <returns>A ConfigUpdateResult with Failed status.</returns>
    public static ConfigUpdateResult Failed(DeviceConfiguration config, string deviceTypeName, string errorMessage)
        => new()
        {
            Status = DeviceConfigUpdateStatus.Failed,
            ErrorMessage = errorMessage,
            Configuration = config,
            DeviceTypeName = deviceTypeName
        };
}

/// <summary>
/// Status of a configuration update operation.
/// </summary>
public enum DeviceConfigUpdateStatus
{
    /// <summary>
    /// Update was skipped (no changes required or not applicable).
    /// </summary>
    Skipped = 0,

    /// <summary>
    /// Validation failed before applying update.
    /// </summary>
    Invalid = 1,

    /// <summary>
    /// Update was applied successfully.
    /// </summary>
    Success = 2,

    /// <summary>
    /// Update failed during application.
    /// </summary>
    Failed = 3
}

/// <summary>
/// Result of validating a configuration update (Phase 1 of two-phase commit).
/// Used by SubNodeManager to validate all devices before applying any changes.
/// </summary>
public sealed record ConfigUpdateValidationResult
{
    /// <summary>
    /// Whether the validation passed.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Whether no update is required (empty desired config).
    /// </summary>
    public bool IsSkipped { get; init; }

    /// <summary>
    /// Whether a DTMI delta was detected (requires re-upload).
    /// </summary>
    public bool HasDtmiDelta { get; init; }

    /// <summary>
    /// Error message when IsValid is false.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Device type name for reporting.
    /// </summary>
    public required string DeviceTypeName { get; init; }

    /// <summary>
    /// Creates a result indicating validation passed.
    /// </summary>
    public static ConfigUpdateValidationResult Valid(string deviceTypeName, bool hasDtmiDelta = false)
        => new()
        {
            IsValid = true,
            DeviceTypeName = deviceTypeName,
            HasDtmiDelta = hasDtmiDelta
        };

    /// <summary>
    /// Creates a result indicating validation failed.
    /// </summary>
    public static ConfigUpdateValidationResult Invalid(string deviceTypeName, string errorMessage)
        => new()
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            DeviceTypeName = deviceTypeName
        };

    /// <summary>
    /// Creates a result indicating no update is required.
    /// </summary>
    public static ConfigUpdateValidationResult Skipped(string deviceTypeName)
        => new()
        {
            IsValid = true,
            IsSkipped = true,
            DeviceTypeName = deviceTypeName
        };
}
