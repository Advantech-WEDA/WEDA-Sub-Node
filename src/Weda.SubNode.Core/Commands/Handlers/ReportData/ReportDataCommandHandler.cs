using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Commands;
using Weda.SubNode.Abstractions.Commands.Attributes;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Context;
using Weda.SubNode.Abstractions.Devices;
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
/// The framework sends the ack and owns the executedAt/completedAt timestamps; this handler
/// only produces the result.
/// </remarks>
[Logging(LogLevel.Information)]
public class ReportDataCommandHandler : ICommandHandler<ReportDataCommand, ReportDataResult>
{
    public async Task<ErrorOr<ReportDataResult>> HandleAsync(
        ReportDataCommand command,
        IWedaApplicationContext context,
        CancellationToken cancellationToken = default)
    {
        var logger = context.GetLogger<ReportDataCommandHandler>();
        var cloudService = context.CloudService;
        var subNodeId = context.SubNodeInfo.Id;

        var parameters = command.Parameters;
        var resultData = new ReportDataResultData
        {
            SensorShortResourceId = parameters.SensorShortResourceId,
            ResourceTimestamp = parameters.ResourceTimestamp,
            TransferId = parameters.TransferId,
            DataTransferred = false
        };

        // Validate SubNode is registered
        if (string.IsNullOrEmpty(subNodeId))
        {
            return ReportDataResult.Error(
                ReportDataStatusCode.PermissionDenied,
                "SubNode is not registered",
                resultData);
        }

        logger.LogInformation(
            "Executing ReportData: SensorId={SensorId}, Timestamp={Timestamp}, TransferId={TransferId}",
            parameters.SensorShortResourceId, parameters.ResourceTimestamp, parameters.TransferId);

        try
        {
            var (device, sensor) = FindDeviceAndSensor(context, parameters.SensorShortResourceId);

            if (device is null || sensor is null)
            {
                logger.LogWarning("Device or sensor not found for short ID {SensorId}",
                    parameters.SensorShortResourceId);
                return ReportDataResult.Error(
                    ReportDataStatusCode.NoDataAvailable,
                    "Sensor not found",
                    resultData);
            }

            var measures = await device.ReadSensorTelemetryAsync(sensor.ResourceId, cancellationToken);

            if (measures.Count == 0)
            {
                logger.LogInformation("No cached data for sensor {SensorId}", parameters.SensorShortResourceId);
                return ReportDataResult.Error(
                    ReportDataStatusCode.NoDataAvailable,
                    "No data available for specified sensor",
                    resultData);
            }

            var telemetryData = new TelemetryData { Measures = measures };
            var sendResult = await cloudService.SendTelemetryAsync(subNodeId, telemetryData, cancellationToken);
            if (!sendResult)
            {
                logger.LogWarning("Failed to send telemetry for sensor {SensorId}", parameters.SensorShortResourceId);
                return ReportDataResult.Error(
                    ReportDataStatusCode.GenericError,
                    "Failed to send telemetry data",
                    resultData);
            }

            logger.LogInformation("ReportData completed: sent {Count} measures for sensor {SensorId}",
                measures.Count, parameters.SensorShortResourceId);

            return ReportDataResult.Success(
                "Data retrieval complete",
                resultData with { DataTransferred = true });
        }
        catch (OperationCanceledException)
        {
            return ReportDataResult.Error(
                ReportDataStatusCode.Timeout,
                "Operation was cancelled",
                resultData);
        }
    }

    private static (IDevice? device, Sensor? sensor) FindDeviceAndSensor(IWedaApplicationContext context, string sensorShortResourceId)
    {
        foreach (var device in context.DeviceRegistry.GetAllDevices())
        {
            var sensor = device.Configuration.Sensors
                .FirstOrDefault(s => s.ShortId == sensorShortResourceId);
            if (sensor != null)
                return (device, sensor);
        }
        return (null, null);
    }
}
