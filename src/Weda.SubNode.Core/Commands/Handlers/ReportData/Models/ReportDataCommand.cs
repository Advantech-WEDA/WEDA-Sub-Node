using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
[Display(Name = "Report Data")]
[Description("Query a specific telemetry data point by sensor and timestamp.")]
public class ReportDataCommand : CommandData<ReportDataParameters>
{
}

/// <summary>
/// Parameters for report.data command.
/// </summary>
public class ReportDataParameters
{
    [Required]
    [JsonPropertyName("sensorShortResourceId")]
    [Display(Name = "Sensor Short Resource ID")]
    [Description("Short resource ID of the sensor to query (e.g., 'f782c').")]
    public string SensorShortResourceId { get; init; } = string.Empty;

    [Required]
    [JsonPropertyName("resourceTimestamp")]
    [Display(Name = "Resource Timestamp")]
    [Description("Unix timestamp (milliseconds) of the data point to retrieve.")]
    public long ResourceTimestamp { get; init; }

    [JsonPropertyName("transferId")]
    [Display(Name = "Transfer ID")]
    [Description("Optional identifier targeting a specific transfer (e.g., a specific MIME chunk set). When null, retrieves all measures for the sensor at the given timestamp.")]
    public string? TransferId { get; init; }
}
