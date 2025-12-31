namespace Weda.SubNode.Abstractions.Configuration;

/// <summary>
/// Result of configuration validation.
/// </summary>
public record ConfigurationValidationResult(
    bool IsValid,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ConfigurationValidationResult Success => new(true);

    /// <summary>
    /// Creates a failed validation result with an error message.
    /// </summary>
    public static ConfigurationValidationResult Failure(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Result of custom configuration update handling.
/// </summary>
public record CustomConfigUpdateResult(
    bool IsSuccess,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Creates a successful result indicating the custom config was applied.
    /// </summary>
    public static CustomConfigUpdateResult Success() => new(true);

    /// <summary>
    /// Creates a failed result with an error message.
    /// </summary>
    public static CustomConfigUpdateResult Failure(string errorMessage) => new(false, errorMessage);
}