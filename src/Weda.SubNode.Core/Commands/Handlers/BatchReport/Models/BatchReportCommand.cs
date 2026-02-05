using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;

/// <summary>
/// Command to query historical telemetry and emit batch records.
/// Maps to payload: data.deviceCmd = "report.historical"
/// </summary>
/// <remarks>
/// TimeRange defaults:
/// - If TimeRange is null, defaults to last 10 minutes (now - 10min to now)
/// - StartTime/EndTime are Unix milliseconds
/// </remarks>
[DeviceCmd("report.historical")]
public class BatchReportCommand : CommandData<BatchReportParameters>
{
    /// <summary>
    /// Gets the effective time range, applying defaults if needed.
    /// </summary>
    /// <returns>A TimeRange with StartTime and EndTime in Unix milliseconds.</returns>
    public TimeRange GetEffectiveTimeRange()
    {
        if (Parameters.TimeRange is not null)
        {
            return Parameters.TimeRange;
        }

        // Default to last 10 minutes
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var tenMinutesAgo = now - (10 * 60 * 1000);
        return new TimeRange(tenMinutesAgo, now);
    }
}

public class BatchReportParameters
{
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
    /// Maximum number of batches (measures) per message.
    /// </summary>
    [JsonPropertyName("maxBatchesPerMessage")]
    [Range(1, 1000, ErrorMessage = "MaxBatchesPerMessage must be between 1 and 1000")]
    public int MaxBatchesPerMessage { get; init; } = 1;

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
}