using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Telemetry;
using Weda.SubNode.Core.Commands.Handlers.ReportData.Models;

namespace Weda.SubNode.Core.Commands.Handlers.ReportData;

/// <summary>
/// Handler for the "report.data" command that queries specific telemetry data by sensor and timestamp.
/// Returns data in realtime telemetry format for consistency with live streams.
/// </summary>
/// <remarks>
/// Response handling:
/// - Primitive data: Published to realtime telemetry subject (*.rl.enr)
/// - MIME data > 1MB: Chunked, each chunk published as separate NATS message (future)
///
/// This handler sends its own initial ack, so auto ack is disabled.
/// </remarks>
[Logging(LogLevel.Information)]
[AutoAck(false)]
public class ReportDataCommandHandler : ICommandHandler<ReportDataCommand, ReportDataResult>
{
    public async Task<ErrorOr<ReportDataResult>> HandleAsync(
        ReportDataCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<ReportDataCommandHandler>();
        var recordingService = context.RecordingService;
        var cloudService = context.CloudService;
        var subNodeId = context.SubNodeInfo.Id;
        var executedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var parameters = command.Parameters;
        var resultData = new ReportDataResultData
        {
            SensorShortResourceId = parameters.SensorShortResourceId,
            ResourceTimestamp = parameters.ResourceTimestamp,
            TransferId = parameters.TransferId,
            DataTransferred = false
        };

        // Validate recording service is available
        if (recordingService is null)
        {
            return ReportDataResult.Error(
                ReportDataStatusCode.StorageError,
                "RecordingService is not configured",
                resultData,
                executedAt);
        }

        // Validate SubNode is registered
        if (string.IsNullOrEmpty(subNodeId))
        {
            return ReportDataResult.Error(
                ReportDataStatusCode.PermissionDenied,
                "SubNode is not registered",
                resultData,
                executedAt);
        }

        logger.LogInformation(
            "Executing ReportData: SensorId={SensorId}, Timestamp={Timestamp}, TransferId={TransferId}",
            parameters.SensorShortResourceId, parameters.ResourceTimestamp, parameters.TransferId);

        // Send initial ACK
        await SendAckAsync(cloudService, subNodeId, command.RespTopic, command.DeviceCmd,
            command.SeqId, command.ReqSeqId, resultData, logger, cancellationToken);

        try
        {
            // Query the specific timestamp (with small range to find exact match)
            var targetTime = DateTimeOffset.FromUnixTimeMilliseconds(parameters.ResourceTimestamp);
            var startTime = targetTime.AddMilliseconds(-1);
            var endTime = targetTime.AddMilliseconds(1);

            var recordingResult = await recordingService.GetRecordingsAsync(
                parameters.SensorShortResourceId, startTime, endTime, cancellationToken);

            if (recordingResult.IsError)
            {
                logger.LogWarning("Failed to get recordings for sensor {SensorId}: {Error}",
                    parameters.SensorShortResourceId, recordingResult.FirstError.Description);
                return ReportDataResult.Error(
                    ReportDataStatusCode.StorageError,
                    recordingResult.FirstError.Description,
                    resultData,
                    executedAt);
            }

            var recording = recordingResult.Value;
            if (recording.Measures.Count == 0)
            {
                logger.LogInformation("No data found for sensor {SensorId} at timestamp {Timestamp}",
                    parameters.SensorShortResourceId, parameters.ResourceTimestamp);
                return ReportDataResult.Error(
                    ReportDataStatusCode.NoDataAvailable,
                    "No data found for specified sensor and timestamp",
                    resultData,
                    executedAt);
            }

            // Find the exact value at the requested timestamp
            double? foundValue = null;
            long foundTimestamp = 0;

            foreach (var measure in recording.Measures)
            {
                for (int i = 0; i < measure.Values.Count; i++)
                {
                    var valueTimestamp = measure.StartTimeStamp + (i * measure.Interval);
                    if (valueTimestamp == parameters.ResourceTimestamp)
                    {
                        foundValue = measure.Values[i];
                        foundTimestamp = valueTimestamp;
                        break;
                    }
                }
                if (foundValue.HasValue) break;
            }

            if (!foundValue.HasValue)
            {
                logger.LogInformation("Exact timestamp not found for sensor {SensorId} at {Timestamp}",
                    parameters.SensorShortResourceId, parameters.ResourceTimestamp);
                return ReportDataResult.Error(
                    ReportDataStatusCode.NoDataAvailable,
                    "No data found at exact timestamp",
                    resultData,
                    executedAt);
            }

            // Build the full resource ID for telemetry (need to find it from device configs)
            var fullResourceId = FindFullResourceId(context, parameters.SensorShortResourceId);
            if (string.IsNullOrEmpty(fullResourceId))
            {
                // Fallback: construct a placeholder resource ID
                fullResourceId = $"00000000-0000-0000-0000-00000{parameters.SensorShortResourceId}";
            }

            // Send as realtime telemetry format
            var telemetryData = new TelemetryData
            {
                Measures =
                [
                    new TelemetryMeasure
                    {
                        ResourceId = fullResourceId,
                        Value = foundValue.Value,
                        Timestamp = foundTimestamp,
                        Metadata = parameters.TransferId != null
                            ? new Dictionary<string, object> { ["transferId"] = parameters.TransferId }
                            : null
                    }
                ]
            };

            var sendResult = await cloudService.SendTelemetryAsync(subNodeId, telemetryData, cancellationToken);
            if (!sendResult)
            {
                logger.LogWarning("Failed to send telemetry for sensor {SensorId}", parameters.SensorShortResourceId);
                return ReportDataResult.Error(
                    ReportDataStatusCode.GenericError,
                    "Failed to send telemetry data",
                    resultData,
                    executedAt);
            }

            logger.LogInformation("ReportData completed: sent value {Value} for sensor {SensorId} at {Timestamp}",
                foundValue, parameters.SensorShortResourceId, foundTimestamp);

            var completedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            return ReportDataResult.Success(
                "Data retrieval complete",
                resultData with { DataTransferred = true },
                executedAt,
                completedAt);
        }
        catch (OperationCanceledException)
        {
            return ReportDataResult.Error(
                ReportDataStatusCode.Timeout,
                "Operation was cancelled",
                resultData,
                executedAt);
        }
    }

    /// <summary>
    /// Finds the full resource ID from device configs by sensor short ID.
    /// </summary>
    private static string? FindFullResourceId(IWedaApplicationContext context, string sensorShortId)
    {
        foreach (var deviceConfig in context.DeviceConfigs.Values)
        {
            foreach (var sensor in deviceConfig.Sensors)
            {
                if (sensor.ShortId == sensorShortId)
                {
                    return sensor.ResourceId;
                }
            }
        }
        return null;
    }

    private static async Task SendAckAsync(
        Abstractions.Cloud.IWedaCloudService cloudService,
        string deviceId,
        string? respTopic,
        string deviceCmd,
        ulong seqId,
        string? reqSeqId,
        ReportDataResultData resultData,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(respTopic))
            return;

        try
        {
            var response = new CommandResponse
            {
                DeviceId = deviceId,
                SeqId = seqId,
                ReqSeqId = reqSeqId,
                Data = new CommandResponseData
                {
                    DeviceCmd = deviceCmd,
                    MsgType = "ack",
                    Status = ReportDataStatusCode.Success,
                    Message = "Data query started",
                    ResultData = resultData
                }
            };

            await cloudService.SendCommandResponseAsync(respTopic, response, cancellationToken);
            logger.LogDebug("ACK sent for report.data command");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to send ACK");
        }
    }
}
