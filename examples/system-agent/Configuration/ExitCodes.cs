namespace SystemAgentExample.Configuration;

/// <summary>
/// Exit codes for application startup failures.
/// Follows standard Unix exit code conventions.
/// </summary>
public static class ExitCodes
{
    /// <summary>
    /// Success - no errors
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Configuration file not found
    /// </summary>
    public const int ConfigFileNotFound = 1;

    /// <summary>
    /// Configuration file has invalid JSON format
    /// </summary>
    public const int InvalidJsonFormat = 2;

    /// <summary>
    /// Required configuration field is missing
    /// </summary>
    public const int MissingRequiredField = 3;

    /// <summary>
    /// Invalid configuration value (e.g., invalid URL format)
    /// </summary>
    public const int InvalidConfigValue = 4;

    /// <summary>
    /// General configuration validation error
    /// </summary>
    public const int ConfigValidationError = 5;

    /// <summary>
    /// Cloud connection failure
    /// </summary>
    public const int CloudConnectionError = 10;

    /// <summary>
    /// Device initialization failure
    /// </summary>
    public const int DeviceInitializationError = 11;

    /// <summary>
    /// Unexpected runtime error
    /// </summary>
    public const int UnexpectedError = 99;
}
