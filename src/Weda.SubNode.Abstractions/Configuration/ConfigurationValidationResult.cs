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