namespace Weda.SubNode.Abstractions.Devices;

/// <summary>
/// Background task execution periods (in milliseconds).
/// </summary>
public class BackgroundTaskPeriods
{
    public const int MinReportConfigurationPeriod = 60_000;
    public const int DefaultReportConfigurationPeriod = 1_800_000;

    /// <summary>
    /// Health reporting period (default: 60000ms = 1 minute)
    /// </summary>
    public int ReportHealth { get; set; } = 60000;

    /// <summary>
    /// Command polling period (default: 1000ms = 1 second)
    /// </summary>
    public int PollCommands { get; set; } = 1000;

    /// <summary>
    /// Configuration sync period (default: 1800000ms = 30 minutes).
    /// Minimum: 60000ms (1 minute).
    /// </summary>
    public int ReportConfiguration { get; set; } = DefaultReportConfigurationPeriod;
}
