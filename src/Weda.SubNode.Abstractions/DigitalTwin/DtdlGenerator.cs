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
    public const string DtdlContext = "dtmi:dtdl:context;3";

    /// <summary>
    /// Namespace prefix for auto-generated DTMIs scoped to a device:
    /// dtmi:sub:{device-cfg-key}:... — the device key is the DeviceConfigs
    /// section key from devicecfg.json, so sensors with the same name on
    /// different devices get distinct DTMIs.
    /// </summary>
    public const string SubNodeNamespacePrefix = "dtmi:sub";

    /// <summary>
    /// Maximum allowed length of a device config key (DeviceConfigs section key).
    /// The key becomes a DTMI namespace segment (dtmi:sub:{device-cfg-key}:...),
    /// so an unbounded key would produce unbounded DTMIs. Enforced fail-fast at
    /// startup by the Host configuration loader.
    /// </summary>
    public const int MaxDeviceKeyLength = 32;

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
        var interfaceId = GenerateDtmi(deviceName, "Interface", deviceKey: deviceName);

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
            if (IsMimeType(sensor.SensorInfo.Schema)) 
                continue;

            var content = GenerateTelemetryContent(sensor, deviceKey: deviceName);
            dtdlInterface.Contents.Add(content);
        }

        return dtdlInterface;
    }

    /// <summary>
    /// Generates a DTDL telemetry content from a sensor definition.
    /// </summary>
    /// <param name="sensor">The sensor to convert to DTDL content.</param>
    /// <param name="deviceKey">Optional device config key used to namespace an auto-generated DTMI.</param>
    /// <returns>A DtdlContent representing the sensor as telemetry.</returns>
    public static DtdlContent GenerateTelemetryContent(Sensor sensor, string? deviceKey = null)
    {
        // NOTE: `unit` is intentionally NOT emitted on the Telemetry content.
        // Core DTDL v3 does not define a `unit` term on a bare Telemetry
        // (it's a QuantitativeTypes-extension construct that requires
        // co-typing with a specific quantitative type). DTDLParser rejects
        // the Interface with "undefined term 'unit'" otherwise.
        // The unit travels in the upload payload via DeviceCapDto.Sensors[i].Unit
        // instead, where cloud / front-end can still read it.
        return new DtdlContent
        {
            Id = string.IsNullOrEmpty(sensor.Dtmi)
                ? GenerateDtmi(sensor.Name, sensor.SensorGroup.ToString(), deviceKey: deviceKey)
                : sensor.Dtmi,
            Type = "Telemetry",
            Name = SanitizeName(sensor.Name),
            DisplayName = sensor.GetEffectiveDisplayName(),
            Description = sensor.SensorInfo.Description,
            Schema = sensor.Schema
        };
    }

    /// <summary>
    /// Generates a DTMI (Digital Twin Model Identifier) using a short hash.
    /// Format: dtmi:sub:{deviceKey}:{namespace}:{shortId};1. Every auto-generated
    /// DTMI is scoped to a device — <paramref name="deviceKey"/> is required.
    /// </summary>
    /// <param name="name">The name to generate the DTMI from.</param>
    /// <param name="namespaceSegment">Optional namespace segment (e.g., "AI", "Interface").</param>
    /// <param name="version">DTMI version number (default: 1).</param>
    /// <param name="deviceKey">The device config key (DeviceConfigs section key) used to namespace the DTMI per device. Required.</param>
    /// <returns>A valid DTMI string.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deviceKey"/> is null or empty.</exception>
    public static string GenerateDtmi(string name, string? namespaceSegment = null, int version = 1, string? deviceKey = null)
    {
        if (string.IsNullOrEmpty(deviceKey))
            throw new ArgumentException(
                "A device key is required to generate a DTMI. Auto-generated DTMIs are always " +
                "scoped to a device (dtmi:sub:{deviceKey}:...) so sensors with the same name on " +
                "different devices get distinct DTMIs.",
                nameof(deviceKey));

        var shortId = GenerateShortId(name);
        var ns = string.IsNullOrEmpty(namespaceSegment)
            ? string.Empty
            : $":{SanitizeNamespace(namespaceSegment)}";

        var prefix = $"{SubNodeNamespacePrefix}:{SanitizeDeviceKey(deviceKey)}";

        return $"{prefix}{ns}:{shortId};{version}";
    }

    /// <summary>
    /// Generates a short, deterministic ID from a name using a SHA256 hash.
    /// The ID is a leading letter followed by 8 lowercase hex chars (9 total).
    /// The leading letter is required: a DTMI path segment MUST start with a
    /// letter under DTDL v3, but a bare hex string starts with a digit ~62% of
    /// the time, which DTDLParser rejects as an invalid identifier.
    /// </summary>
    /// <param name="name">The name to hash.</param>
    /// <returns>A 9-character short ID that always starts with a letter.</returns>
    public static string GenerateShortId(string name)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name.ToLowerInvariant()));
        // First 4 bytes (32 bits) as 8 hex chars, prefixed with 's' so the token
        // is a valid DTMI path segment (letter-first) regardless of the hash.
        return "s" + Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }

    /// <summary>
    /// Populates Dtmi for all sensors that don't have one specified.
    /// Call this when AutoGenEnabled is true.
    /// </summary>
    /// <param name="sensors">The sensors to populate DTMIs for.</param>
    /// <param name="deviceKey">Optional device config key (DeviceConfigs section key) used to namespace generated DTMIs per device.</param>
    public static void PopulateSensorDtmis(IEnumerable<Sensor> sensors, string? deviceKey = null)
    {
        foreach (var sensor in sensors)
        {
            if (string.IsNullOrEmpty(sensor.Dtmi))
            {
                var schema = sensor.SensorInfo.Schema;

                sensor.Dtmi = IsMimeType(schema)
                    ? GenerateMimeTypeDtmi(schema)
                    : GenerateDtmi(sensor.Name, sensor.SensorGroup.ToString(), deviceKey: deviceKey);
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
    /// Sanitizes a device config key into a valid DTMI path segment while keeping
    /// it human-readable: original casing and underscores are preserved so users
    /// can recognize the device in the DTMI (segment rule:
    /// <c>[A-Za-z][A-Za-z0-9_]*</c>, must not end with underscore).
    /// A key with no usable ASCII characters (e.g. fully non-ASCII) falls back to
    /// a hash-based segment so distinct devices still get distinct namespaces.
    /// </summary>
    private static string SanitizeDeviceKey(string deviceKey)
    {
        var sanitized = new string(deviceKey
            .Replace('.', '_')
            .Replace('-', '_')
            .Replace(' ', '_')
            .Where(c => char.IsAsciiLetterOrDigit(c) || c == '_')
            .ToArray());

        while (sanitized.Contains("__"))
            sanitized = sanitized.Replace("__", "_");

        sanitized = sanitized.Trim('_');

        if (sanitized.Length == 0)
            return "d" + GenerateShortId(deviceKey);

        return char.IsAsciiLetter(sanitized[0]) ? sanitized : "d_" + sanitized;
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

    /// <summary>
    /// Determines if a schema is a MIME type (contains '/').
    /// </summary>
    /// <param name="schema"></param>
    /// <returns></returns>
    public static bool IsMimeType(string? schema)
    {
        return !string.IsNullOrEmpty(schema) && schema.Contains('/');
    }

    /// <summary>
    /// Generate DTMI for MIMT type schema using convention.
    /// e.g. "image/jpeg" to "dtmi:advantech:iamge:jpeg"
    /// e.g. "application/json" to "dtmi:advantech:app:json"
    /// </summary>
    public static string GenerateMimeTypeDtmi(string schema)
    {
        var parts = schema.Split('/', 2);
        var category = parts[0] switch
        {
            "application" => "app",
            _ => parts[0]
        };
        var subtype = parts.Length > 1 ? parts[1].Replace("+", "-") : "unknown";
        var dtmi = $"dtmi:advantech:{category}:{subtype}";

        return dtmi;
    }
}
