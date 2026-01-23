using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;

/// <summary>
/// Command to query historical telemetry and emit batch records.
/// Maps to payload: data.deviceCmd = "report"
/// </summary>
/// <remarks>
/// TimeRange defaults:
/// - If TimeRange is null, defaults to last 10 minutes (now - 10min to now)
/// - StartTime/EndTime are Unix milliseconds
/// </remarks>
[DeviceCmd("report")]
public record BatchReportCommand : ICommand
{
    /// <summary>
    /// Report type identifier.
    /// </summary>
    [JsonPropertyName("reportType")]
    [Required(ErrorMessage = "ReportType is required")]
    public string ReportType { get; init; } = string.Empty;

    /// <summary>
    /// Time range for historical data query.
    /// If null, defaults to last 10 minutes.
    /// </summary>
    [JsonPropertyName("timeRange")]
    public TimeRange? TimeRange { get; init; }

    /// <summary>
    /// Filter for sensor selection.
    /// </summary>
    [JsonPropertyName("sensorFilter")]
    public SensorFilter? SensorFilter { get; init; }

    /// <summary>
    /// Response topic for command acknowledgment.
    /// If empty, no response will be sent.
    /// </summary>
    [JsonPropertyName("respTopic")]
    public string RespTopic { get; init; } = string.Empty;

    /// <summary>
    /// Maximum number of batches (measures) per message.
    /// </summary>
    [JsonPropertyName("maxBatchesPerMessage")]
    [Range(1, 1000, ErrorMessage = "MaxBatchesPerMessage must be between 1 and 1000")]
    public int MaxBatchesPerMessage { get; init; } = 10;

    /// <summary>
    /// Maximum number of samples per batch.
    /// When a sensor has more samples than this limit, it will be split into multiple batches.
    /// </summary>
    [JsonPropertyName("maxBatchSize")]
    [Range(1, 100000, ErrorMessage = "MaxBatchSize must be between 1 and 100000")]
    public int MaxBatchSize { get; init; } = 10000;

    /// <summary>
    /// Rate limit for transmission (messages per second).
    /// 0 means no rate limiting.
    /// </summary>
    [JsonPropertyName("transmissionRateLimit")]
    [Range(0, 10000, ErrorMessage = "TransmissionRateLimit must be between 0 and 10000")]
    public int TransmissionRateLimit { get; init; } = 0;

    /// <summary>
    /// Command timeout in seconds.
    /// </summary>
    [JsonPropertyName("timeout")]
    [Range(1, 3600, ErrorMessage = "Timeout must be between 1 and 3600 seconds")]
    public int Timeout { get; init; } = 300;

    /// <summary>
    /// Device command name for routing.
    /// </summary>
    [JsonIgnore]
    public string DeviceCmd => "report";

    /// <summary>
    /// Gets the effective time range, applying defaults if needed.
    /// </summary>
    /// <returns>A TimeRange with StartTime and EndTime in Unix milliseconds.</returns>
    public TimeRange GetEffectiveTimeRange()
    {
        if (TimeRange is not null)
        {
            return TimeRange;
        }

        // Default to last 10 minutes
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var tenMinutesAgo = now - (10 * 60 * 1000);
        return new TimeRange(tenMinutesAgo, now);
    }
}
