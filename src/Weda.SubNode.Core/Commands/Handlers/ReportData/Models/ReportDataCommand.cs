using System.Text.Json.Serialization;
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Contracts;

namespace Weda.SubNode.Core.Commands.Handlers.ReportData.Models;

/// <summary>
/// Command to query specific telemetry data (primitive or MIME) by sensor and timestamp.
/// Maps to payload: data.deviceCmd = "report.data"
/// </summary>
/// <remarks>
/// Response format: Realtime telemetry format (same as live realtime streams)
/// - Primitive data → published to *.rl.enr subject
/// - MIME data > 1MB → chunked, each chunk published as separate NATS message
/// </remarks>
[DeviceCmd("report.data")]
public class ReportDataCommand : CommandData<ReportDataParameters>
{
}

/// <summary>
/// Parameters for report.data command.
/// </summary>
public class ReportDataParameters
{
    /// <summary>
    /// Short resource ID of the sensor to query (e.g., "f782c").
    /// </summary>
    [JsonPropertyName("sensorShortResourceId")]
    public required string SensorShortResourceId { get; init; }

    /// <summary>
    /// Unix timestamp in milliseconds for the data point to retrieve.
    /// </summary>
    [JsonPropertyName("resourceTimestamp")]
    public required long ResourceTimestamp { get; init; }

    /// <summary>
    /// Optional unique identifier for the transfer operation.
    /// If provided, retrieves the specific transfer (e.g., a specific MIME chunk set).
    /// If null, retrieves all measures for the sensor at the given timestamp.
    /// </summary>
    [JsonPropertyName("transferId")]
    public string? TransferId { get; init; }
}