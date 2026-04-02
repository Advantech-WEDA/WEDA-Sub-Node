using System.Text.RegularExpressions;

namespace SystemAgentExample.Communication.Utilities;

/// <summary>
/// Helper methods for platform-specific string normalization and utilities.
/// 
/// This class ensures that metric labels conform to Prometheus naming conventions:
/// - Metric labels must contain only [a-zA-Z0-9_]
/// - No spaces, colons, slashes, or hyphens are allowed
/// 
/// Reference: https://prometheus.io/docs/concepts/data_model/#metric-names-and-labels
/// </summary>
public static partial class PlatformHelpers
{
    [GeneratedRegex(@"[^a-zA-Z0-9_]+")]
    private static partial Regex NonPrometheusChars();
    /// <summary>
    /// Normalizes drive/partition names across platforms to Prometheus-compatible format.
    /// Strips colons, slashes, and path prefixes; returns lowercase alphanumeric with underscores.
    /// Root partition "/" maps to "root".
    /// </summary>
    public static string NormalizeDriveName(string driveName)
    {
        var normalized = driveName.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(normalized) || normalized == "/")
            return "root";
        if (normalized.EndsWith(':'))
            return normalized.TrimEnd(':').ToLowerInvariant();
        return normalized.Replace("/", "_").TrimStart('_');
    }

    /// <summary>
    /// Sanitizes network interface names to be Prometheus-compatible.
    /// 
    /// Prometheus label values can only contain alphanumeric characters and underscores.
    /// Callers should pass NetworkInterface.Description (hardware driver name, always English).
    /// If Description sanitizes to empty, falls back to sanitizing the friendlyName.
    /// </summary>
    public static string SanitizeInterfaceName(string description, string? friendlyName = null)
    {
        var sanitized = NonPrometheusChars().Replace(description, "_").Trim('_').ToLowerInvariant();
        if (!string.IsNullOrEmpty(sanitized))
            return sanitized;

        if (!string.IsNullOrEmpty(friendlyName))
        {
            sanitized = NonPrometheusChars().Replace(friendlyName, "_").Trim('_').ToLowerInvariant();
            if (!string.IsNullOrEmpty(sanitized))
                return sanitized;
        }

        return description.Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
    }
}
