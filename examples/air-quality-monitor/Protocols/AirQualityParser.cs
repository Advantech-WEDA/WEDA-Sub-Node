using System.Text.Json;

using AirQualityMonitor.Communication;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Weda.SubNode.Abstractions.Commands.Contracts;
using Weda.SubNode.Abstractions.Communication;
using Weda.SubNode.Abstractions.Devices;
using Weda.SubNode.Abstractions.Protocols;
using Weda.SubNode.Abstractions.Telemetry;

namespace AirQualityMonitor.Protocols;

public class AirQualityParser(
    DeviceConfiguration configuration,
    HttpCommunication communication,
    ILogger<AirQualityParser> logger) : IRequestResponseProtocolParser
{
    public ICommunication Communication => communication;

    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(CancellationToken cancellationToken = default)
    {
        var enabledSensors = configuration.Sensors
            .Where(s => s.Report.Enabled)
            .Select(s => s.ResourceId)
            .ToHashSet();

        return await ReadTelemetryAsync(enabledSensors, cancellationToken);
    }

    public async Task<List<TelemetryMeasure>> ReadTelemetryAsync(
        IEnumerable<string> sensorResourceIds, 
        CancellationToken cancellationToken = default)
    {
        var measures = new List<TelemetryMeasure>();

        if (!communication.IsConnected)
        {
            logger.LogWarning("Communication not connected");
            return measures;
        }

        try
        {
            var response = await communication.RequestAsync(new AirQualityRequest(), cancellationToken);

            if (response == null || response.Count == 0)
            {
                logger.LogWarning("No data received from API");
                return measures;
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var jsonPayload = JsonSerializer.Serialize(response);

            foreach (var resourceId in sensorResourceIds)
            {
                measures.Add(new TelemetryMeasure
                {
                    ResourceId = resourceId,
                    Value = jsonPayload,
                    Timestamp = timestamp
                });
            }

            logger.LogDebug("Parsed {Count} telemetry measures with {Records} air quality records",
                measures.Count, response.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading air quality data");
        }

        return measures;   
    }
}