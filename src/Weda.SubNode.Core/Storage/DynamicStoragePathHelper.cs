namespace Weda.SubNode.Core.Storage;

// Helper for dynamic storage file path generation.
public static class DynamicStoragePathHelper
{
    public const int CurrentVersion = 1;
    public const string IndexExtension = ".idx";
    public const string DataExtension = ".dat";

    /// <summary>
    /// Gets the index file path for a sensor on a specific date.
    /// </summary>
    public static string GetIndexPath(string dataDir, string sensorId, DateOnly date, int version = CurrentVersion)
        => Path.Combine(dataDir, $"{sensorId}_{date:yyyy-MM-dd}_v{version}{IndexExtension}");

    /// <summary>
    /// Gets the data file path for a sensor on a specific date.
    /// </summary>
    public static string GetDataPath(string dataDir, string sensorId, DateOnly date, int version = CurrentVersion)
        => Path.Combine(dataDir, $"{sensorId}_{date:yyyy-MM-dd}_v{version}{DataExtension}");    

    /// <summary>
    /// Gets the date from a Unix timestamp (ms).
    /// </summary>
    public static DateOnly GetDateFromTimestamp(long timestampMs)
        => DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime);
}