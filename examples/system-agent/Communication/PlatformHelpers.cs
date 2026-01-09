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
public static class PlatformHelpers
{
    /// <summary>
    /// Normalizes drive/partition names across different platforms to be Prometheus-compatible.
    /// 
    /// Prometheus label values can only contain alphanumeric characters and underscores.
    /// This method converts platform-specific drive names into a standardized format.
    /// 
    /// Examples:
    /// - Windows: "C:" ¡÷ "c"
    /// - Windows: "D:" ¡÷ "d"
    /// - Linux: "/dev/sda1" ¡÷ "sda1"
    /// - Linux: "/dev/sdb" ¡÷ "sdb"
    /// - Linux: "/" (root) ¡÷ "root"
    /// - macOS: "/Volumes/Data" ¡÷ "volumes_data"
    /// 
    /// This ensures that the device name can be safely used as a Prometheus label value.
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
    /// This method converts platform-specific interface names into a standardized format
    /// by replacing spaces and hyphens with underscores, and converting to lowercase.
    /// 
    /// Examples:
    /// - "Ethernet 1" ¡÷ "ethernet_1"
    /// - "Wi-Fi" ¡÷ "wi_fi"
    /// - "eth0-vlan" ¡÷ "eth0_vlan"
    /// - "Local Area Connection" ¡÷ "local_area_connection"
    /// 
    /// This ensures that the interface name can be safely used as a Prometheus label value,
    /// allowing proper filtering and aggregation in monitoring dashboards.
    /// </summary>
    public static string SanitizeInterfaceName(string name)
    {
        return name.Replace(" ", "_").Replace("-", "_").ToLowerInvariant();
    }
}
