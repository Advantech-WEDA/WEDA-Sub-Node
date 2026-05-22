using System.ComponentModel;
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
[Description("Query historical telemetry within a time range and emit batched records.")]
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
    [JsonPropertyName("timeRange")]
    [Description("Time range for historical data query (Unix milliseconds). Defaults to last 10 minutes when omitted.")]
    public TimeRange? TimeRange { get; init; }

    [JsonPropertyName("sensorFilter")]
    [Description("Optional filter selecting which sensors to include / exclude.")]
    public SensorFilter? SensorFilter { get; init; }

    [JsonPropertyName("maxBatchesPerMessage")]
    [Range(1, 1000, ErrorMessage = "MaxBatchesPerMessage must be between 1 and 1000")]
    [DefaultValue(1)]
    [Description("Maximum number of batches per outbound message.")]
    public int MaxBatchesPerMessage { get; init; } = 1;

    [JsonPropertyName("maxBatchSize")]
    [Range(1, 100000, ErrorMessage = "MaxBatchSize must be between 1 and 100000")]
    [DefaultValue(10000)]
    [Description("Maximum samples per batch. Larger sensor result sets are split into multiple batches.")]
    public int MaxBatchSize { get; init; } = 10000;

    [JsonPropertyName("transmissionRateLimit")]
    [Range(0, 10000, ErrorMessage = "TransmissionRateLimit must be between 0 and 10000")]
    [DefaultValue(0)]
    [Description("Transmission rate limit (messages per second). 0 disables rate limiting.")]
    public int TransmissionRateLimit { get; init; } = 0;
}