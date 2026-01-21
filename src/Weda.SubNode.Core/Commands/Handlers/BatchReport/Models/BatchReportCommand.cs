using Weda.SubNode.Abstractions.Commands;

namespace Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;

/// <summary>
/// Command to query historical telemetry and emit batch records.
/// Maps to payload: data.deviceCmd = "report"
/// </summary>
public record BatchReportCommand(
    string ReportType,
    TimeRange TimeRange,
    SensorFilter SensorFilter,
    string RespTopic,
    string DataTopic,
    int MaxBatchesPerMessage = 10,
    int TransmissionRateLimit = 100,
    int Timeout = 300) : ICommand
{
    public string DeviceCmd => "report";
}