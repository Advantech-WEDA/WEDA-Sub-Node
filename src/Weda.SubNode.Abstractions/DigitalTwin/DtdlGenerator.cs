using System.Security.Cryptography;
using System.Text;
using Weda.SubNode.Abstractions.Telemetry;

namespace Weda.SubNode.Abstractions.DigitalTwin;

/// <summary>
/// Generates DTDL (Digital Twin Definition Language) interfaces from sensor definitions.
/// Used when SubNode.AutoGenEnabled is set to true.
/// </summary>
public static class DtdlGenerator
{
    /// <summary>
    /// Default DTDL context for v2 specification.
    /// </summary>
    public const string DtdlContext = "dtmi:dtdl:context;2";

    /// <summary>
    /// Default namespace prefix for auto-generated DTMIs.
    /// </summary>
    public const string DefaultNamespacePrefix = "dtmi:autogen";

    /// <summary>
    /// Generates a DTDL interface from a collection of sensors.
    /// </summary>
    /// <param name="deviceName">The device name for the interface ID.</param>
    /// <param name="sensors">The sensors to include as telemetry contents.</param>
    /// <param name="displayName">Optional display name for the interface.</param>
    /// <param name="description">Optional description for the interface.</param>
    /// <returns>A DtdlInterface containing telemetry definitions for each sensor.</returns>
    public static DtdlInterface GenerateInterface(
        string deviceName,
        IEnumerable<Sensor> sensors,
        string? displayName = null,
        string? description = null)
    {
        var interfaceId = GenerateDtmi(deviceName, "Interface");

        var dtdlInterface = new DtdlInterface
        {
            Id = interfaceId,
            Type = "Interface",
            Context = DtdlContext,
            DisplayName = displayName ?? FormatDisplayName(deviceName),
            Description = description ?? $"Auto-generated DTDL interface for {deviceName}",
            Contents = []
        };

        foreach (var sensor in sensors)
        {
            var content = GenerateTelemetryContent(sensor);
            dtdlInterface.Contents.Add(content);
        }

        return dtdlInterface;
    }

    /// <summary>
    /// Generates a DTDL telemetry content from a sensor definition.
    /// </summary>
    /// <param name="sensor">The sensor to convert to DTDL content.</param>
    /// <returns>A DtdlContent representing the sensor as telemetry.</returns>
    public static DtdlContent GenerateTelemetryContent(Sensor sensor)
    {
        return new DtdlContent
        {
            Id = string.IsNullOrEmpty(sensor.Dtmi)
                ? GenerateDtmi(sensor.Name, sensor.SensorGroup.ToString())
                : sensor.Dtmi,
            Type = "Telemetry",
            Name = SanitizeName(sensor.Name),
            DisplayName = sensor.GetEffectiveDisplayName(),
            Description = sensor.SensorInfo.Description,
            Schema = sensor.GetEffectiveSchema(),
            Unit = sensor.Report.Unit
        };
    }

    /// <summary>
    /// Generates a DTMI (Digital Twin Model Identifier) using a short hash.
    /// Format: dtmi:autogen:{namespace}:{shortId};1
    /// </summary>
    /// <param name="name">The name to generate the DTMI from.</param>
    /// <param name="namespaceSegment">Optional namespace segment (e.g., "AI", "Interface").</param>
    /// <param name="version">DTMI version number (default: 1).</param>
    /// <returns>A valid DTMI string.</returns>
    public static string GenerateDtmi(string name, string? namespaceSegment = null, int version = 1)
    {
        var shortId = GenerateShortId(name);
        var ns = string.IsNullOrEmpty(namespaceSegment)
            ? string.Empty
            : $":{SanitizeNamespace(namespaceSegment)}";

        return $"{DefaultNamespacePrefix}{ns}:{shortId};{version}";
    }

    /// <summary>
    /// Generates a short, URL-safe ID from a name using SHA256 hash.
    /// The ID is 8 characters long and uses base32-like encoding.
    /// </summary>
    /// <param name="name">The name to hash.</param>
    /// <returns>An 8-character short ID.</returns>
    public static string GenerateShortId(string name)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name.ToLowerInvariant()));
        // Take first 5 bytes (40 bits) and encode as base32-like string (8 chars)
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }

    /// <summary>
    /// Populates Dtmi for all sensors that don't have one specified.
    /// Call this when AutoGenEnabled is true.
    /// </summary>
    /// <param name="sensors">The sensors to populate DTMIs for.</param>
    public static void PopulateSensorDtmis(IEnumerable<Sensor> sensors)
    {
        foreach (var sensor in sensors)
        {
            if (string.IsNullOrEmpty(sensor.Dtmi))
            {
                sensor.Dtmi = GenerateDtmi(sensor.Name, sensor.SensorGroup.ToString());
            }
        }
    }

    /// <summary>
    /// Validates that all sensors have required fields for manual DTDL mode.
    /// Call this when AutoGenEnabled is false.
    /// </summary>
    /// <param name="sensors">The sensors to validate.</param>
    /// <param name="dtdlPath">The DTDL path to validate.</param>
    /// <returns>A list of validation errors, empty if valid.</returns>
    public static List<string> ValidateManualDtdlConfiguration(
        IEnumerable<Sensor> sensors,
        string? dtdlPath)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dtdlPath))
        {
            errors.Add("DtdlPath is required when AutoGenEnabled is false.");
        }

        foreach (var sensor in sensors)
        {
            if (string.IsNullOrWhiteSpace(sensor.Dtmi))
            {
                errors.Add($"Sensor '{sensor.Name}' is missing required Dtmi field when AutoGenEnabled is false.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Sanitizes a name for use in DTDL (removes invalid characters, ensures valid identifier).
    /// DTDL names must match pattern: ^[a-zA-Z](?:[a-zA-Z0-9_]*[a-zA-Z0-9])?$
    /// </summary>
    private static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "unnamed";

        // Replace dots, dashes, spaces with underscores
        var sanitized = name
            .Replace('.', '_')
            .Replace('-', '_')
            .Replace(' ', '_');

        // Ensure starts with letter
        if (!char.IsLetter(sanitized[0]))
            sanitized = "s_" + sanitized;

        // Remove consecutive underscores and trim trailing underscore
        while (sanitized.Contains("__"))
            sanitized = sanitized.Replace("__", "_");

        sanitized = sanitized.TrimEnd('_');

        return sanitized;
    }

    /// <summary>
    /// Sanitizes a namespace segment for DTMI (lowercase, alphanumeric only).
    /// </summary>
    private static string SanitizeNamespace(string segment)
    {
        return new string(segment.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray());
    }

    /// <summary>
    /// Formats a device name as a display name.
    /// </summary>
    private static string FormatDisplayName(string name)
    {
        return string.Join(" ",
            name.Replace('.', ' ')
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => char.ToUpper(word[0]) + word[1..].ToLower()));
    }
}
