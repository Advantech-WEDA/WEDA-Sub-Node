using ErrorOr;

using Microsoft.Extensions.Logging;

using Weda.SubNode.Abstractions.Cloud;
using Weda.SubNode.Abstractions.Cloud.Clients.Telemetry.Contracts;
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Core.Commands.Handlers.BatchReport.Models;

namespace Weda.SubNode.Core.Commands.Handlers.BatchReport;

/// <summary>
/// Handler for the "report" command that queries historical telemetry and sends batch records.
/// </summary>
/// <remarks>
/// Response handling (Received/Success/Failed) is managed by CommandDispatcher.
/// This handler only focuses on business logic and returns the result.
/// </remarks>
public class BatchReportCommandHandler : ICommandHandler<BatchReportCommand, BatchReportResult>
{
    public async Task<ErrorOr<BatchReportResult>> HandleAsync(
        BatchReportCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<BatchReportCommandHandler>();
        var recordingService = context.RecordingService;
        var cloudService = context.CloudService;
        var subNodeId = context.SubNodeInfo.Id;

        // Validate recording service is available
        if (recordingService is null)
        {
            return Errors.Command.ValidationFailed("RecordingService is not configured");
        }

        // Validate SubNode is registered
        if (string.IsNullOrEmpty(subNodeId))
        {
            return Errors.Command.ValidationFailed("SubNode is not registered");
        }

        var startTime = DateTimeOffset.FromUnixTimeMilliseconds(command.TimeRange.StartTime);
        var endTime = DateTimeOffset.FromUnixTimeMilliseconds(command.TimeRange.EndTime);

        logger.LogInformation(
            "Executing BatchReport: {StartTime} to {EndTime}, MaxBatches={MaxBatches}, RateLimit={RateLimit}",
            startTime, endTime, command.MaxBatchesPerMessage, command.TransmissionRateLimit);

        // Step 1. Get all sensor IDs
        var sensorIdsResult = await recordingService.GetSensorIdsAsync(cancellationToken);
        if (sensorIdsResult.IsError)
        {
            return sensorIdsResult.Errors;
        }

        var sensorIds = FilterSensors(sensorIdsResult.Value, command.SensorFilter);
        if (sensorIds.Count == 0)
        {
            logger.LogInformation("No sensors match the filter criteria");
            return new BatchReportResult(0);
        }

        logger.LogDebug("Processing {Count} sensors", sensorIds.Count);

        // Step 2. Query and batch send
        var batchBuffer = new List<BatchTelemetryMeasureDto>();
        var messageCount = 0;

        foreach (var sensorId in sensorIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var recordingResult = await recordingService.GetRecordingsAsync(sensorId, startTime, endTime, cancellationToken);

            if (recordingResult.IsError)
            {
                logger.LogWarning("Failed to get recordings for sensor {SensorId}: {Error}", sensorId, recordingResult.Errors.First().Description);
                continue;
            }

            // Convert recording to batch measures
            foreach (var measure in recordingResult.Value.Measures)
            {
                batchBuffer.Add(new BatchTelemetryMeasureDto
                {
                    Id = sensorId,
                    Interval = measure.Interval,
                    StartTimeStamp = measure.StartTimeStamp,
                    Values = measure.Values.Select(v => (double?)v).ToList()
                });

                // Check if we should send a batch
                if (batchBuffer.Count >= command.MaxBatchesPerMessage)
                {
                    await SendBatchAsync(cloudService, subNodeId, batchBuffer, logger, cancellationToken);
                    batchBuffer.Clear();
                    messageCount++;

                    if (command.TransmissionRateLimit > 0)
                    {
                        var delayMs = 1000 / command.TransmissionRateLimit;
                        await Task.Delay(delayMs, cancellationToken);
                    }
                }
            }
        }

        // Send remaining batch
        if (batchBuffer.Count > 0)
        {
            await SendBatchAsync(cloudService, subNodeId, batchBuffer, logger, cancellationToken);
            messageCount++;
        }

        logger.LogInformation("BatchReport completed: {MessageCount} messages sent", messageCount);

        return new BatchReportResult(messageCount);
    }

    private static IReadOnlyList<string> FilterSensors(
        IReadOnlyList<string> sensorIds,
        SensorFilter? filter)
    {
        if (filter is null)
        {
            return sensorIds;
        }

        var result = sensorIds.AsEnumerable();

        if (filter.Include is { Length: > 0 })
        {
            var includeSet = new HashSet<string>(filter.Include, StringComparer.OrdinalIgnoreCase);
            result = result.Where(id => includeSet.Contains(id));
        }

        if (filter.Exclude is { Length: > 0})
        {
            var excludeSet = new HashSet<string>(filter.Exclude, StringComparer.OrdinalIgnoreCase);
            result = result.Where(id => !excludeSet.Contains(id));
        }

        return result.ToList();
    }

    private static async Task SendBatchAsync(
        IWedaCloudService cloudService,
        string subNodeId,
        List<BatchTelemetryMeasureDto> measures,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var message = BatchTelemetrySendMessage.Create(subNodeId, measures);
        var success = await cloudService.SendBatchTelemetryAsync(subNodeId, message, cancellationToken);

        if (!success)
        {
            logger.LogWarning("Failed to send batch telemetry with {Count} measures", measures.Count);
        }
        else
        {
            logger.LogDebug("Sent batch with {Count} measures", measures.Count);
        }
    }
}

/// <summary>
/// Result of the BatchReport command execution.
/// </summary>
public record BatchReportResult(int TotalBatches);
