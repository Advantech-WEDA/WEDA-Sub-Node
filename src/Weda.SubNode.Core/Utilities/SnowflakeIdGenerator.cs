using System.Security.Cryptography;
using System.Text;

namespace Weda.SubNode.Core.Utilities;

/// <summary>
/// Snowflake ID generator for device IDs
/// Format: 41 bits timestamp + 10 bits machine ID + 12 bits sequence
/// Output: 12-character hex string (e.g., "74fe488d5d54")
/// </summary>
public class SnowflakeIdGenerator
{
    private const long Epoch = 1704067200000L; // 2024-01-01 00:00:00 UTC in milliseconds
    private const int MachineIdBits = 10;
    private const int SequenceBits = 12;
    private const long MaxMachineId = (1L << MachineIdBits) - 1; // 1023
    private const long MaxSequence = (1L << SequenceBits) - 1; // 4095

    private readonly int _machineId;
    private long _lastTimestamp = -1L;
    private long _sequence = 0L;
    private readonly object _lock = new();

    public SnowflakeIdGenerator(int? machineId = null)
    {
        _machineId = machineId ?? GetMachineIdFromSystem();

        if (_machineId < 0 || _machineId > MaxMachineId)
        {
            throw new ArgumentOutOfRangeException(nameof(machineId),
                $"Machine ID must be between 0 and {MaxMachineId}");
        }
    }

    /// <summary>
    /// Generate a new Snowflake ID
    /// </summary>
    /// <returns>Snowflake ID as hex string (12 characters)</returns>
    public string Generate()
    {
        lock (_lock)
        {
            var timestamp = GetCurrentTimestamp();

            if (timestamp < _lastTimestamp)
            {
                throw new InvalidOperationException("Clock moved backwards. Refusing to generate ID");
            }

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MaxSequence;
                if (_sequence == 0)
                {
                    // Sequence overflow, wait for next millisecond
                    timestamp = WaitNextMillisecond(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            // Generate Snowflake ID
            var id = ((timestamp - Epoch) << (MachineIdBits + SequenceBits))
                   | ((long)_machineId << SequenceBits)
                   | _sequence;

            // Convert to hex string (12 characters)
            return id.ToString("x12");
        }
    }

    private static long GetCurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static long WaitNextMillisecond(long lastTimestamp)
    {
        var timestamp = GetCurrentTimestamp();
        while (timestamp <= lastTimestamp)
        {
            timestamp = GetCurrentTimestamp();
        }
        return timestamp;
    }

    /// <summary>
    /// Get machine ID from system (MAC address based)
    /// </summary>
    private static int GetMachineIdFromSystem()
    {
        try
        {
            // Get MAC address and hash it to generate a deterministic machine ID
            var networkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            var macAddress = networkInterfaces
                .Where(nic => nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                           && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .FirstOrDefault() ?? "000000000000";

            // Hash MAC address to get machine ID within valid range
            var hash = SHA1.HashData(Encoding.UTF8.GetBytes(macAddress));
            return Math.Abs(BitConverter.ToInt32(hash, 0)) % ((int)MaxMachineId + 1);
        }
        catch
        {
            // Fallback to random machine ID
            return Random.Shared.Next(0, (int)MaxMachineId + 1);
        }
    }
}
