namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Background task execution periods (in milliseconds).
/// </summary>
public class BackgroundTaskPeriods
{
    public const int MinReportConfigurationPeriod = 60_000;
    public const int MaxReportConfigurationPeriod = 604_800_000;
    public const int DefaultReportConfigurationPeriod = 86_400_000;

    /// <summary>
    /// Health reporting period (default: 60000ms = 1 minute)
    /// </summary>
    public int ReportHealth { get; set; } = 60000;

    /// <summary>
    /// Command polling period (default: 1000ms = 1 second)
    /// </summary>
    public int PollCommands { get; set; } = 1000;

    /// <summary>
    /// Configuration sync period (default: 86400000ms = 24 hours).
    /// Range: 60000ms (1 minute) to 604800000ms (7 days).
    /// </summary>
    public int ReportConfiguration { get; set; } = DefaultReportConfigurationPeriod;

    /// <summary>
    /// When true (default), telemetry is batched and sent at intervals based on minimum sensor interval.
    /// When false telemetry is sent immediately after processing (no batching).
    /// </summary>
    public bool BatchSend { get; set; } = true;
}
