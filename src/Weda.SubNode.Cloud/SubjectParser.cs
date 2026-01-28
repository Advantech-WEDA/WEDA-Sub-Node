namespace Weda.SubNode.Cloud;

/// <summary>
/// Parsed information from NATS subject.
/// Subject format: {protoVer}.{groupId}.{deviceId}.subnode.shadow.{configType}.{action}
/// Example: eco1j.weda.74fe48845d54.subnode.shadow.devicecfg.delta
/// </summary>
public sealed record SubjectInfo(
    string ProtoVer,
    string GroupId,
    string DeviceId,
    string? ConfigType = null,
    string? Action = null);

/// <summary>
/// Parser for NATS subject strings.
/// Extracts protoVer, groupId, deviceId from subject format.
/// </summary>
public static class SubjectParser
{
    /// <summary>
    /// Parses a NATS subject string to extract routing information.
    /// </summary>
    /// <param name="subject">The NATS subject string</param>
    /// <returns>Parsed subject info, or null if parsing fails</returns>
    public static SubjectInfo? Parse(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        var parts = subject.Split('.');
        if (parts.Length < 3)
            return null;

        var protoVer = parts[0];
        var groupId = parts[1];
        var deviceId = parts[2];

        // Optional: extract configType and action if present
        // Format: {protoVer}.{groupId}.{deviceId}.subnode.shadow.{configType}.{action}
        string? configType = parts.Length >= 6 ? parts[5] : null;
        string? action = parts.Length >= 7 ? parts[6] : null;

        return new SubjectInfo(protoVer, groupId, deviceId, configType, action);
    }
}
