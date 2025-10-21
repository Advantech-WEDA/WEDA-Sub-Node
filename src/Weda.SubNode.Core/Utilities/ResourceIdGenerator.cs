using System.Security.Cryptography;
using System.Text;

namespace Weda.SubNode.Core.Utilities;

/// <summary>
/// Resource ID generator using UUID5 algorithm and MAC-based device ID
/// </summary>
public static class ResourceIdGenerator
{
    /// <summary>
    /// Generate device resource ID from MAC address
    /// Format: {macAddress}-ffff
    /// Example: "74:fe:48:8d:5d:54" → "74fe488d5d54-ffff"
    /// </summary>
    /// <param name="macAddress">MAC address in format "XX:XX:XX:XX:XX:XX" or "XX-XX-XX-XX-XX-XX"</param>
    /// <returns>Device resource ID</returns>
    public static string GenerateDeviceIdFromMac(string macAddress)
    {
        ArgumentException.ThrowIfNullOrEmpty(macAddress);

        var cleanMac = macAddress
            .Replace(":", "")
            .Replace("-", "")
            .Replace(" ", "")
            .ToLowerInvariant();

        if (cleanMac.Length != 12)
        {
            throw new ArgumentException(
                $"Invalid MAC address format. Expected 12 hex characters, got {cleanMac.Length}",
                nameof(macAddress));
        }

        if (!cleanMac.All(c => char.IsAsciiHexDigit(c)))
        {
            throw new ArgumentException(
                "MAC address must contain only hexadecimal characters",
                nameof(macAddress));
        }

        return $"{cleanMac}-ffff";
    }

    /// <summary>
    /// Generate sensor resource ID from device ID and index
    /// Format: {deviceIdPrefix}-{index:D4}
    /// Example: deviceId="74fe488d5d54-ffff", index=1 → "74fe488d5d54-0001"
    /// </summary>
    /// <param name="deviceId">Device resource ID</param>
    /// <param name="index">Sensor index (1-based)</param>
    /// <returns>Sensor resource ID</returns>
    public static string GenerateSensorIdFromDeviceId(string deviceId, int index)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        if (index < 1)
        {
            throw new ArgumentException("Sensor index must be >= 1", nameof(index));
        }

        if (index > 9999)
        {
            throw new ArgumentException("Sensor index must be <= 9999", nameof(index));
        }

        // Extract prefix (everything before last dash)
        var lastDashIndex = deviceId.LastIndexOf('-');
        if (lastDashIndex < 0)
        {
            throw new ArgumentException(
                "Invalid device ID format. Expected format: {prefix}-{suffix}",
                nameof(deviceId));
        }

        var prefix = deviceId[..lastDashIndex];
        return $"{prefix}-{index:D4}";
    }

    /// <summary>
    /// Generate multiple sensor resource IDs at once
    /// </summary>
    /// <param name="deviceId">Device resource ID</param>
    /// <param name="count">Number of sensors</param>
    /// <returns>List of sensor resource IDs</returns>
    public static List<string> GenerateSensorIdsFromDeviceId(string deviceId, int count)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        if (count < 1)
        {
            throw new ArgumentException("Sensor count must be >= 1", nameof(count));
        }

        var sensorIds = new List<string>(count);
        for (int i = 1; i <= count; i++)
        {
            sensorIds.Add(GenerateSensorIdFromDeviceId(deviceId, i));
        }

        return sensorIds;
    }

    /// <summary>
    /// Generate a resource ID for a sensor using UUID5 algorithm
    /// Algorithm: uuid.uuid5(namespace, f"{device_name}.{resourcename}")
    /// </summary>
    /// <param name="deviceId">Identity of device resource, derived from Device Agent</param>
    /// <param name="resourceName">Name of the device sensor, like txAmount or freeAmount</param>
    /// <param name="groupId">Unique identifier for the organization or tenant (4-char alphanumeric)</param>
    /// <returns>Resource ID in UUID format</returns>
    public static string GenerateResourceId(string deviceId, string resourceName, string groupId = "weda")
    {
        var namespaceUuid = GroupIdToUuid(groupId);
        var name = $"{deviceId}.{resourceName}";
        return GenerateUuid5(namespaceUuid, name).ToString();
    }

    /// <summary>
    /// Convert 4-char groupId to UUID namespace
    /// Uses the groupId as a seed to generate a deterministic UUID
    /// </summary>
    private static Guid GroupIdToUuid(string groupId)
    {
        // Use SHA-1 hash of groupId to generate a deterministic UUID namespace
        var groupIdBytes = Encoding.UTF8.GetBytes(groupId);
        var hash = SHA1.HashData(groupIdBytes);

        // Take first 16 bytes for UUID
        var uuid = new byte[16];
        Array.Copy(hash, 0, uuid, 0, 16);

        // Set version to 5 and variant to RFC 4122
        uuid[6] = (byte)((uuid[6] & 0x0F) | 0x50);
        uuid[8] = (byte)((uuid[8] & 0x3F) | 0x80);

        return new Guid(uuid);
    }

    /// <summary>
    /// Generate UUID version 5 (name-based SHA-1)
    /// </summary>
    /// <param name="namespaceId">Namespace UUID</param>
    /// <param name="name">Name to hash</param>
    /// <returns>UUID version 5</returns>
    private static Guid GenerateUuid5(Guid namespaceId, string name)
    {
        // Convert namespace UUID to byte array in network byte order (big-endian)
        var namespaceBytes = namespaceId.ToByteArray();
        SwapGuidBytes(namespaceBytes);

        // Convert name to UTF-8 bytes
        var nameBytes = Encoding.UTF8.GetBytes(name);

        // Concatenate namespace and name
        var data = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, data, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, data, namespaceBytes.Length, nameBytes.Length);

        // Compute SHA-1 hash
        var hash = SHA1.HashData(data);

        // Take first 16 bytes and set version and variant bits
        var uuid = new byte[16];
        Array.Copy(hash, 0, uuid, 0, 16);

        // Set version to 5 (0101 in bits 12-15 of time_hi_and_version)
        uuid[6] = (byte)((uuid[6] & 0x0F) | 0x50);

        // Set variant to RFC 4122 (10 in bits 6-7 of clock_seq_hi_and_reserved)
        uuid[8] = (byte)((uuid[8] & 0x3F) | 0x80);

        // Convert back to .NET Guid format (swap bytes)
        SwapGuidBytes(uuid);

        return new Guid(uuid);
    }


    /// <summary>
    /// Swap byte order for GUID conversion between network order and .NET format
    /// </summary>
    private static void SwapGuidBytes(byte[] guid)
    {
        // Swap time_low (4 bytes)
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);

        // Swap time_mid (2 bytes)
        (guid[4], guid[5]) = (guid[5], guid[4]);

        // Swap time_hi_and_version (2 bytes)
        (guid[6], guid[7]) = (guid[7], guid[6]);
    }
}
